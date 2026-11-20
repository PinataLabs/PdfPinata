# Spec — page rendering, issue #370

This library writes PDF and reads it back, but it cannot draw what it reads.
`docs-website/docs/reference/faq.md` sends anyone who wants a page as an image to Ghostscript or
PDFium. This note is the design for **page rendering**: interpreting an existing page's content and
drawing it onto a SkiaSharp canvas, in a new package, `PdfPinata.Rendering.Skia`.
[BobLd/PdfPig.Rendering.Skia](https://github.com/BobLd/PdfPig.Rendering.Skia) is the model for the
Skia surface. The rest of this note explains why ours is not as thin as that one.

The table is the plan. Items 1, 2 and 8 are built (#371), and item 4 (#373).
`page-rendering-interpreter.md` sets them out as they were built: what crosses the seam, how broken
content is read, how a glyph is chosen and decoded, and where each of the other items is added.

| item | what | status |
|---|---|---|
| 1 | Content-stream interpreter and device seam, in the core | done |
| 2 | Graphics state, paths, clipping, Form XObjects | done |
| 3 | DeviceGray, DeviceRGB, DeviceCMYK, Indexed, ICCBased by `/N` | v1 |
| 4 | Embedded TrueType font programs | done |
| 5 | Standard 14 fonts through `FontResolver`, with AFM widths | v1 |
| 6 | Image XObjects and inline images, masks and soft masks, DCT through `SKCodec` | v1 |
| 7 | `PdfPinata.Rendering.Skia`: canvas, picture, bitmap, encoded image | v1 |
| 8 | Diagnostics instead of exceptions | done |
| 9 | `PdfPage.UserUnit` | v1 |
| 10 | CFF (`/FontFile3`) and Type 1 (`/FontFile`) font programs | deferred |
| 11 | Type 3 fonts | deferred; their advances are read (#371) |
| 12 | Shadings (`sh`, shading patterns) and tiling patterns | deferred |
| 13 | Blend modes, transparency groups, soft-mask groups | deferred; reported where they change what is drawn |
| 14 | Separation, DeviceN, Lab, and ICC colour management | deferred |
| 15 | CCITTFax, JBIG2 and JPX images | deferred |
| 16 | Optional content | deferred; content of a group that is off is drawn and reported |
| 17 | Annotation appearances, and the option to draw them | deferred |
| 18 | `PdfTextExtractor` and `PdfImagePlacementReader` on the new interpreter | follow-up |
| 19 | A `RenderPage` demo in SampleApp and on the docs site | follow-up |

---

## The words

In this repository "render" already means *producing* PDF. `PdfDocumentRenderer`,
`XGraphicsPdfRenderer`, `PinataLayout.Rendering` and the charting renderers all write content
streams. `GLOSSARY.md` therefore separates **layout rendering** (that sense) from **page rendering**
(this one), and keeps **rasterize** for producing pixels. A Skia canvas can record an `SKPicture` or
write SVG as easily as fill a bitmap, so page rendering does not always rasterize. The package keeps
the name `PdfPinata.Rendering.Skia` to match PdfPig's.

## Why most of the work is in the core

PdfPig's renderer is thin because PdfPig already parses fonts, decodes images and models colour
spaces. This library does almost none of that on the read side. When this note was written, it had:

- a content-stream parser, `ContentReader` → `CSequence` of `COperator`, with every standard
  operator in `OpCodeName`;
- two walkers that each track part of the graphics state for their own purpose:
  `PdfTextExtractor.Walker` and `PdfImagePlacementReader`;
- decoding for Flate, LZW, ASCIIHex, ASCII85 and RunLength, with predictors. DCT, CCITTFax, JBIG2
  and JPX are recognised and not applied (`Filtering.IsRecognisedButNotImplemented`);
- `OpenTypeFontface`, which reads a table directory for subsetting and decodes no outlines;
- no parser for CFF or Type 1, no standard 14 metrics, no read-side colour-space, image-sample,
  shading or pattern model, and no `/UserUnit` accessor.

So **the interpreter goes in the core**, in a new `Pdf.Rendering` namespace, and the Skia package
is only a device. This follows the rule the rest of the library keeps: the core does the PDF work
and carries no imaging dependency, and backends sit behind seams. It also means the two existing
walkers can later move onto one interpreter (item 18), and a device for another backend stays
possible. Putting the interpreter in the Skia package would have tied all of the PDF work to
SkiaSharp. A separate dependency-free `PdfPinata.Rendering` package was the other option. It was
rejected because the extraction walkers it would eventually absorb are already in the core.

## Item 1 — the device seam

The interpreter resolves everything that is PDF and hands the device only what is drawn:

- a path to fill (with its rule: nonzero or even-odd) or stroke (with width, caps, joins, miter
  limit and dash), in a colour and an alpha;
- a path to intersect the clip with;
- a glyph, **as an outline path**, never as text: the interpreter has already chosen the glyph,
  positioned it with the PDF's widths and applied the text render mode. So a glyph is a fill, a
  stroke or a clip like any other path, and the seam has no member for one. Its path is the same
  instance each time that glyph is drawn, which is what a device caches on;
- an image as decoded samples with a transform, every mask already applied to them;
- save and restore, of the clip, which is the only state a device keeps.

**Every call carries its own transform**, from the coordinates of what it is handed to device
space. A device that kept the CTM would have had to keep it the way the interpreter does, and two
devices would have come to disagree. Carrying it also lets a stroke be drawn under the matrix, so
a pen under a transform that scales x and y differently keeps its shape.

The seam is public, because this repository has no `InternalsVisibleTo` and the Skia package must
implement it. It is marked `[Experimental("PPEXP001")]`, because it will grow when shadings,
patterns and transparency groups arrive (items 12 and 13). It is an abstract class, `PageDevice`,
and not an interface, so that what is added later is a `virtual` member whose base body says the
device cannot draw it, and a device written before it still compiles. An interface could only do
that with a default member, which the netstandard2.1 runtime kept for Unity does not take. PolySharp supplies the attribute on netstandard2.1. The Skia package's own entry points
are not experimental.

## Items 2 and 3 — graphics state and colour

The interpreter keeps the graphics-state stack (`q`/`Q`), the CTM (`cm`), the path under
construction, the clip (`W`/`W*`), line state, colour and constant alpha from `/ExtGState` (`/CA`,
`/ca`). It follows Form XObjects with their `/Matrix` and `/BBox`.

Colour reaches the device as RGB:

- DeviceCMYK goes to RGB by the simple complement-and-multiply formula. The same four CMYK numbers
  are a different colour on every press (see `PdfConformanceWriter.Enforce`), and v1 does not try
  to choose one.
- ICCBased uses its `/Alternate`, or a device space chosen from `/N`. The profile is not applied.
- Indexed looks up its base space.

Real ICC handling is item 14.

## Item 4 — embedded TrueType

**This library parses font programs itself rather than handing the bytes to Skia.**
`SKTypeface.FromData` with `GetGlyphPath` would have been far less code. But on Windows Skia goes
through DirectWrite, which refuses bare CFF and Type 1 and treats a broken subset differently from
FreeType on Linux. The same page would then draw differently on different machines, and the
fidelity tests run on both.

v1 reads `glyf` and `loca` outlines (simple and composite glyphs) in `Fonts.OpenType`, beside
`OpenTypeFontface`, and chooses each glyph by the PDF's rules rather than the font's alone:

- simple fonts: `/Encoding`, including `/Differences`, then the font's (3,1) or (1,0) `cmap`;
- symbolic fonts: the (3,0) `cmap`, trying the code and the code plus `0xF000`;
- composite fonts: the CMap, then `/CIDToGIDMap` (`/Identity` or a stream).

Glyphs are positioned by the PDF's `/Widths` or `/W`, never by the font's own advances, because the
PDF's numbers are the ones its producer laid the text out with. A Type 3 font's `/Widths` are in
glyph space and go through its `/FontMatrix`; every other font's are thousandths of text space.

The text extractor (`FontInfo`) ignores `/Encoding` today and reads a font with no `/ToUnicode` as
Latin-1. `GlyphNames`, written here, is what item 18 gives it: glyph name to character, and the
characters of WinAnsiEncoding, MacRomanEncoding and StandardEncoding.

Parsed font programs are cached **per document**, keyed by the font dictionary's object reference.

## Item 5 — standard 14 fonts

A standard 14 font has no font program, so its outlines come through
`GlobalFontSettings.FontResolver`, just as an `IGlyphOutlineProvider` takes its bytes. The point is
that one seam decides which face a family means. The resolver is read **only when such a font is
drawn**, so a document whose fonts are all embedded renders with no resolver registered. One that
needs it and finds it unset throws the seam's own descriptive `InvalidOperationException`. That is
the one exception page rendering lets through (item 8).

Helvetica, Times and Courier map to the faces with their metrics. Liberation Sans, Serif and Mono
are what `PinnedFontResolver` serves the tests. Symbol and ZapfDingbats have no free equivalent;
the clones are GPL or AGPL. They are therefore asked of the resolver by name and nothing is
bundled. Where the resolver cannot serve them, their glyphs are skipped and reported.

Widths come from `/Widths` when the font has them. When it does not, they come from Adobe's Core14
AFM files, which Adobe allows to be redistributed. A generator under `tools/` turns those into a
checked-in C# table. Like `tools/UnicodeTableGenerator`, it is outside `PdfPinata.slnx` and run by
hand.

## Item 6 — images

The core decodes an image XObject or inline image into one neutral sample buffer:

- every filter it already implements;
- `/BitsPerComponent` 1, 2, 4, 8 and 16;
- `/Decode` arrays;
- `/ImageMask` stencils, painted in the current fill colour;
- `/Mask`, as a stencil or a colour-key array, and `/SMask`.

DCT is the one filter decoded outside the core, by `SKCodec` in the Skia package, because the core
carries no imaging dependency. The device decodes it *for* the core, through
`PageDevice.DecodeImage`, and hands the samples back, so the `/Decode` array and the masks are
applied in one place and a device draws only finished samples. CCITTFax, JBIG2 and JPX are item 15. An image the renderer cannot
decode is skipped and reported.

## Item 7 — the Skia surface

The primitive draws a page onto a canvas the caller owns:

```csharp
PageRenderResult Render(this PdfPage page, SKCanvas canvas, PageRenderOptions? options = null);
```

It is itself thin: `PageView.Of`, in the core, works out where the page lands from the box,
`/Rotate`, `/UserUnit`, the scale and the region, and `PageRendering.Render` draws it onto the
package's `PageDevice`.

Everything else is a thin layer over it: `ToSKPicture`, `ToSKBitmap`, and `ToImage(stream, format)`
for PNG or JPEG. Each has a `PdfDocument` overload taking a page index. The caller owns the lifetime
of every canvas, bitmap and picture.

| option | default | meaning |
|---|---|---|
| `Scale` | 1 | 1 is 72 DPI |
| `Box` | effective crop box | which page box is drawn: crop, media, bleed, trim or art |
| `Region` | the whole box | the rectangle to draw |
| `Background` | white | painted before the page |
| `AntiAlias` | on | |

**A region is in points from the top-left of the page as displayed**, after the box, `/Rotate` and
`/UserUnit` are applied. That is the space a caller cutting tiles or zooming thinks in, and the same
top-left space `XGraphics` draws in. Output always comes out the way the page is displayed. A
default-user-space region would have been closer to the PDF, but every caller would then have had
to work out the rotation themselves.

There is no option for annotations yet. An option that did nothing would be worse than none, so it
arrives with item 17.

The package references the core and `SkiaSharp` alone. It does not reference `PdfPinata.Skia`, which
is the *writing* backend, and rendering needs no backend registered. It targets
`netstandard2.1;net8.0;net10.0;net11.0`, like every shipped package, and is `IsTrimmable` and
`IsAotCompatible`. The core interpreter uses no reflection. The Blazor trim check does **not**
prove that, because ILLink analyses only the code the sample reaches and the sample renders
nothing. The proof is the core's `EnableTrimAnalyzer`, which checks every member at build time, and
the same analyzers that `IsAotCompatible` turns on in the Skia package.

## Item 8 — diagnostics, not exceptions

Real files are often slightly broken, and a renderer that throws on the first bad operator is no
use for drawing them. So an operator with the wrong operands, an unreadable font program or an
undecodable image is **skipped**, and the reason is recorded on `PageRenderResult`. That follows
the same reasoning as `DocumentRenderer.ImageFailed` in `image-failure-reporting.md`: carry on, but
do not lose the reason. The one exception is an unset seam that is needed (item 5). That is a
configuration error rather than a defect in the file, and it should not be hidden in a list.

`PageRenderResult` is in the core and is not experimental, because the Skia package's own entry
points return it. It holds one entry for each different problem and counts the times it was met,
and keeps at most 256 entries, so a hostile page costs a bounded amount of memory to describe.

## Item 9 — `/UserUnit`

`PdfPage.Keys.UserUnit` exists and nothing reads it. v1 adds `PdfPage.UserUnit`, which is 1 when
absent, and page rendering scales by it.

## Concurrency

**One render at a time per `PdfDocument`.** Lazy reading and the object model are not thread-safe,
and the font cache is per document. Separate documents render in parallel. The XML comment on
`Render` says so. Tests that render one shared document from several tests need a
`[NotInParallel]` key (see CLAUDE.md, "Tests in one class run at the same time").

## How fidelity is judged

`PdfPinata.Rendering.Skia.Tests` compares each rendered page with **Ghostscript's** rasterization of
the same page, through the Magick.NET path the existing tests use. Every comparison has a
tolerance, a mean channel difference or a share of pixels over a threshold, because anti-aliasing
never matches byte for byte. These tests are `[Rasterizing]`. The inputs are:

- the SampleApp demos;
- `ConformanceCorpus`;
- small hand-written PDFs that each test one feature: a clip, a dash, a composite glyph, a
  `/Differences` entry, a 1-bit image with `/Decode [1 0]`, and so on.

A checked-in golden PNG is used only where Ghostscript itself draws a feature wrongly. The note
that adds one must say why.

**The package is not added to `PACKAGES`** in `release.yml` and `release-prerelease.yml` until it
draws the SampleApp demos and the conformance corpus within tolerance. Until then it is in
`PdfPinata.slnx`, built and tested in CI, and published nowhere.

## What was deliberately left out of v1

Items 10 to 17 are not oversights. Each is a separate issue, and each is a large piece of work on
its own:

- **CFF and Type 1** need parsers of their own. CFF is the more urgent, because every OpenType face
  with PostScript outlines embeds as `/FontFile3`, which this library's own writer produces.
- **Shadings, patterns and transparency groups** are why the device seam is experimental.
- **ICC colour management** means choosing a CMS. Skia has none for CMYK.
- **JPX and JBIG2** each need a codec that SkiaSharp does not have.

Moving the extraction walkers onto the interpreter (item 18) is a follow-up rather than part of v1,
so that v1 does not also make the renderer depend on extraction behaving the same.
