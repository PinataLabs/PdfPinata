# Spec — page rendering, issue #370

This library writes PDF and reads it back, but it cannot draw what it reads.
`docs-website/docs/reference/faq.md` sends anyone who wants a page as an image to Ghostscript or
PDFium. This note is the design for **page rendering**: interpreting an existing page's content and
drawing it onto a SkiaSharp canvas, in a new package, `PdfPinata.Rendering.Skia`.
[BobLd/PdfPig.Rendering.Skia](https://github.com/BobLd/PdfPig.Rendering.Skia) is the model for the
Skia surface. The rest of this note explains why ours is not as thin as that one.

The table is the plan. Items 1, 2 and 8 are built (#371), and `page-rendering-interpreter.md` sets
them out as they were built: what crosses the seam, how broken content is read, and where each of
the other items is added.

| item | what | status |
|---|---|---|
| 1 | Content-stream interpreter and device seam, in the core | done |
| 2 | Graphics state, paths, clipping, Form XObjects | done |
| 3 | DeviceGray, DeviceRGB, DeviceCMYK, Indexed, ICCBased by `/N` | v1 |
| 4 | Embedded TrueType font programs | v1 |
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
| 17 | Annotation appearances, and the option to draw them | done (#387) |
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
- CalGray and CalRGB are DeviceGray and DeviceRGB, with no white point, gamma or matrix applied,
  which is what ICCBased gets too. Nothing is reported. Separation, DeviceN, Lab and Pattern are
  reported and drawn in black until their sub-issue lands.

Real ICC handling is item 14.

## Item 4 — embedded TrueType

**This library parses font programs itself rather than handing the bytes to Skia.**
`SKTypeface.FromData` with `GetGlyphPath` would have been far less code. But on Windows Skia goes
through DirectWrite, which refuses bare CFF and Type 1 and treats a broken subset differently from
FreeType on Linux. The same page would then draw differently on different machines, and the
fidelity tests run on both.

v1 reads `glyf` and `loca` outlines (simple and composite glyphs) by extending `OpenTypeFontface`,
and chooses each glyph by the PDF's rules rather than the font's alone:

- simple fonts: `/Encoding`, including `/Differences`, then the font's (3,1) or (1,0) `cmap`;
- symbolic fonts: the (3,0) `cmap`, trying the code and the code plus `0xF000`;
- composite fonts: the CMap, then `/CIDToGIDMap` (`/Identity` or a stream).

Glyphs are positioned by the PDF's `/Widths` or `/W`, never by the font's own advances, because the
PDF's numbers are the ones its producer laid the text out with. A Type 3 font's `/Widths` are in
glyph space and go through its `/FontMatrix`; every other font's are thousandths of text space.

The text extractor (`FontInfo`) ignores `/Encoding` today and reads a font with no `/ToUnicode` as
Latin-1. The encoding code written here is what item 18 gives it.

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
applied in one place and a device draws only finished samples. CCITTFax, JBIG2 and JPX are item 15.
An image the renderer cannot decode is skipped and reported. `page-rendering-interpreter.md` says
how it was built (#375).

**An image is inflated to a cap, because Flate does not say how big it will be.** A few kilobytes
can expand to gigabytes, and bounding the image by its `/Width` and `/Height` before decoding does
nothing if the filter has already inflated the whole stream. `DecodeLimit` (#398) is the cap: a
filter handed one stops at `MaximumLength` bytes and sets `Truncated` if the stream had more, and
one handed none behaves as it always did. Every filter that expands honours it, Flate, LZW,
RunLength, ASCII85 and ASCIIHex, because a chain such as `[/FlateDecode /RunLengthDecode]` is
only as bounded as its last filter: RunLength turns two bytes into 128. `Filtering.Decode` also
cuts what any one filter returns to the cap, so the bound does not rest on each filter keeping to
it. For an image the cap is the sample bytes the dictionary
declares, `ceil(Width * Components * BitsPerComponent / 8) * Height`. The cap counts what Flate or
LZW itself produces, before a predictor is undone, so with a PNG predictor it includes the one
filter-type byte in front of each row. Cut-short data is read as far as it goes: a PNG or TIFF
predictor drops a final part row rather than returning pixels it could not undo, and a Flate or
ASCII85 filter that is handed the cut-short output of an earlier one returns what it could decode
rather than throwing. ASCII85 drops a final part group only when the cut came before its `~>`
marker; one after it, or between its two characters, left the whole stream. With the cap set to an image's sample bytes, `Truncated` means the stream had
*more* than the image needs, and an image that is short shows as output shorter than its sample
bytes with `Truncated` false.

`PageImages` holds each filter of an image's chain to a limit of its own. The filter that produces
the samples stops at the sample bytes, plus each row's tag byte under a PNG predictor, counted in
the predictor's own rows and never less than one whole row, since a predictor drops a part row and a
row declared wider than the image would otherwise leave nothing; what a stream holds past that is not
drawn, as viewers do not draw it, and is not reported. That row is held to the allowance of a filter
before the last, below, so a large `/Columns` cannot lift the cap. Only Flate and LZW undo a
predictor, and a `/DecodeParms` value of the wrong type is read as its default for the cap and left
for the filter to refuse. Short data is #375's case: it is padded with zeros and reported, and less than half
is refused. A filter before the last produces encoded data, which can be longer than what it decodes
to: RunLength spends a byte for every 128 and two for one at worst, and a JPEG of a few samples is
mostly headers and profiles. So each of those, and every filter before `/DCTDecode`, may produce
twice the sample bytes and 16 MiB more; one that would produce more is reported as `LimitReached`
and the image is not drawn. A limit shared by the whole chain would cut an incompressible RunLength
image short. A JPEG's own size says nothing about its samples, so `/DCTDecode` is held only by
`MaximumSamples`, after the device has decoded it.

**Content streams have a cap too**, `ContentInterpreter.MaximumContentLength`, 128 MiB of inflated
content shared by the page's streams and every form drawn in a render. Flate squeezes operators
about ten to one, so a legitimate page reaches it only with over ten megabytes of compressed
content, and the largest maps and drawings seen are a few tens of megabytes inflated. The cost per
byte is what sets it low: parsing 128 MiB of operators already builds a graph several times that
size, so a higher cap would only move the exhaustion from the inflater to the parser. A stream cut by
it is drawn as far as it goes and reported as `UnreadableContent`. A stream filtered through
ASCIIHex, ASCII85 or RunLength is held to the same cap, so that no chain of filters gets past it.

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
| `Annotations` | on | draw each annotation's normal appearance over the page |
| `AntiAlias` | on | |

**A region is in points from the top-left of the page as displayed**, after the box, `/Rotate` and
`/UserUnit` are applied. That is the space a caller cutting tiles or zooming thinks in, and the same
top-left space `XGraphics` draws in. Output always comes out the way the page is displayed. A
default-user-space region would have been closer to the PDF, but every caller would then have had
to work out the rotation themselves.

**Annotations are drawn by default** (#387), because a viewer shows them and the page is not what a
reader sees without them. `Annotations = false` draws the content alone, and the core's
`PageRendering.Render` takes the same choice as its `annotations` argument. The rules are in
`page-rendering-interpreter.md`, "Annotations".

The package references the core and `SkiaSharp` alone. It does not reference `PdfPinata.Skia`, which
is the *writing* backend, and rendering needs no backend registered. It targets
`netstandard2.1;net8.0;net10.0;net11.0`, like every shipped package, and is `IsTrimmable` and
`IsAotCompatible`. The core interpreter uses no reflection. The Blazor trim check does **not**
prove that, because ILLink analyses only the code the sample reaches and the sample renders
nothing. The proof is the core's `EnableTrimAnalyzer`, which checks every member at build time, and
the same analyzers that `IsAotCompatible` turns on in the Skia package.

### As built (#376)

`SkiaPageDevice` is the `PageDevice`. It is public and experimental, as the seam is, so that a
caller can pass it to `PageRendering.Render` with glyph sources of their own; the entry points above
are not experimental. Five decisions are worth knowing.

- **Each call's transform is put on the canvas after the matrix the canvas had when the device was
  made.** A caller who translated or scaled their canvas finds the page drawn under that. `Render`
  puts the canvas's matrix, clip and save stack back as it found them.
- **The background and the clip are whole pixels**: `PageView.PixelWidth` by `PixelHeight`, not the
  region's exact size times the scale. Otherwise the last column and row of a bitmap would be half
  painted and blend with whatever the canvas held.
- **A glyph's `SKPath` is kept for the device's life**, which is one render, keyed by the shared
  `PagePath` instance. Any other path is built for the call that draws it and freed after it.
- **Images are sampled linearly with mipmaps when anti-aliasing is on**, and by nearest sample when
  it is off. `/Interpolate` is not read yet.
- **`ToSKPicture` and `ToSKBitmap` return no `PageRenderResult`.** `ToImage` returns one, because it
  has nothing else to return. A caller who needs the diagnostics of a picture or a bitmap renders
  onto a canvas of their own with `Render`.

What the comparison with Ghostscript measured when it was written, at 144 DPI unless the test
says otherwise:

| test | mean channel difference | share of pixels over 64 |
|---|---|---|
| fills in DeviceRGB and DeviceGray | 0.267 | 0 |
| caps, joins and a hairline | 1.088 | 0.0055 |
| dashes | 0.867 | 0.0119 |
| curves under an uneven scale | 0.364 | 0.0020 |
| nonzero and even-odd fills | 0.157 | 0 |
| nested and restored clips | 0.488 | 0.0040 |
| constant alpha | 0.095 | 0 |
| `/Rotate` 90, 180 and 270 | 0.293, 0.209, 0.298 | 0.0003, 0, 0.0004 |
| a region at 216 DPI | 0.305 | 0.0004 |

What is left is almost all anti-aliasing, with two exceptions. A hairline that lies on a boundary
between two rows of pixels is one light row to Ghostscript and two middle-grey rows to Skia. A dash
of no length that falls exactly on the end of a path is a dot to Ghostscript and nothing to Skia.
DeviceCMYK is not compared at all: Ghostscript converts it through an ICC profile and page
rendering does not, so a test checks the colours it paints instead. Text and images join the
comparison when their issues (#373, #374, #375) land.

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
