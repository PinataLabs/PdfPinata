# Spec — the page rendering interpreter and the device seam, issue #371

`page-rendering.md` is the design for drawing an existing page onto a SkiaSharp canvas (#370). This
note is the first part of it as built: the interpreter in the core, in `src/PdfPinata/Pdf.Rendering`,
and the public seam it draws onto. Colour spaces, glyph outlines, images and the Skia device are
separate issues, and each has one place here to plug into. The last section names them.

| item | what | status |
|---|---|---|
| 1 | The device seam: `PageDevice`, `PagePath`, `PagePaint`, `PageStroke`, `PageImage` | done |
| 2 | Graphics-state stack, CTM, paths, painting, clipping, `/ExtGState` line state and alpha | done |
| 3 | Form XObjects, with `/Matrix`, `/BBox`, their own resources and a guard against recursion | done |
| 4 | Text state, positioning by the PDF's widths, and all eight text render modes | done |
| 5 | Diagnostics on `PageRenderResult`, and one exception that escapes | done |
| 6 | Page geometry: `PageView` | done |
| 7 | DeviceGray, DeviceRGB and DeviceCMYK | done |
| 8 | Indexed and ICCBased colour (#372) | plug-in point only |
| 9 | Glyph outlines (#373, #374) | plug-in point only |
| 10 | Images (#375) | plug-in point only |
| 11 | CMaps other than `Identity-H`, and vertical writing | deliberately not done |
| 12 | A limit on the operators of one page | deliberately not done |

---

## The device

`PageDevice` is an abstract class with six abstract members and one virtual one:

```csharp
void Save();
void Restore();
void Clip(PagePath path, XMatrix transform, XFillMode fillMode);
void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint);
void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint);
void DrawImage(PageImage image, XMatrix transform, PagePaint paint);
virtual PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data);   // null: cannot
```

**Every call carries its own transform, from the coordinates of what it is handed straight to
device space.** A device keeps no transform and composes none. Three things follow from that one
decision. A stroke is drawn under the matrix and not after it, so the pen under a transform that
scales x and y differently is the ellipse the content asked for. A glyph outline is built once, in
text space, and drawn at any size by the matrix. And an image's matrix already holds the unit
square and the row order, so a device turns nothing over.

**The only state a device keeps is its clip.** `StateStack` is the one caller of `Save` and
`Restore`: every `q`, every form and the page itself push it, and it pops to nothing when the render
ends, whatever the content left saved and whatever was thrown. So a device may map the two onto a
canvas's own save stack.

**There is no member for a glyph.** The design said a device receives "a glyph, as an outline path,
never as text", and it does: as a `Fill`, a `Stroke` or part of a `Clip`. The interpreter splits the
text render mode into those, so the clip modes need nothing from a device that paths do not already
need. A glyph's `PagePath` is the same instance each time that glyph of that font is drawn, and says
so with `IsShared`, which is what a device caches its own path against.

**It is a class and not an interface**, so that shadings, patterns, blend modes and transparency
groups can arrive as `virtual` members whose base body says the device cannot draw them.
`DecodeImage` is the first of that kind. An interface could only do that with a default member, which
the netstandard2.1 runtime kept for Unity does not take.

The line cap, the line join and the fill rule are `XLineCap`, `XLineJoin` and `XFillMode`. They
already say exactly this, and three more enums saying it again would have been three more to keep
in step.

## What crosses the seam

- `PagePath` is immutable: `Verbs` and `Points` as spans, cubic curves only, `re` as four lines and
  a close. `PagePathBuilder` is public because a `PageGlyphSource` builds outlines with one. It is
  not `XGlyphOutline`, which promises points already scaled to a type size.
- `PagePaint` is red, green, blue and alpha, each from 0 to 1. The alpha is `/ca` for a fill, a
  filled glyph and an image, and `/CA` for a stroke.
- `PageStroke` is the line state, every length in the path's own coordinates. The dash pattern is
  always an even number of lengths: a pattern written with an odd number is handed over twice,
  which is what repeating it means, and one that is all zeros is a solid line.
- `PageImage` is finished samples: `Rgba32`, or `Alpha8` for a stencil mask that is painted in the
  paint's colour. **A device never draws encoded data.** It decodes a JPEG *for* the core, through
  `DecodeImage`, and the core applies the masks to what comes back. The other way round, a JPEG
  crossing the seam with its masks beside it, would have made every device compose a soft mask
  for that one case. What comes back is RGB and not the JPEG's own components, because a codec
  turns a CMYK or an Adobe-inverted JPEG to RGB itself and `SKCodec` has no way to hand back
  the four inks. So a `/Decode` array on a JPEG is the codec's to honour, not the core's.

## The interpreter

`ContentInterpreter` is one render of one page. `Execute` is the only place an operator is told from
another. A form XObject is drawn by the same instance, so there is one state stack and one place
problems are reported; what belongs to a content stream (its resources, the path being built, the
pending clip, the text object) is put aside while a form runs and put back after.

The CTM starts as `PageView.Transform`. There is no second matrix to remember to apply.

A path is kept in user space as it is written and painted under the transform in force at the
painting operator. `W` and `W*` are remembered and applied after the painting operator that follows,
which is the order ISO 32000-1 8.5.4 gives.

**Text is laid out here, not in a device and not in a font.** A shown string is split into codes,
one byte for a simple font and two for a composite one. Each code advances by the PDF's own width,
then character spacing, word spacing after the single byte 32 and nothing else, and horizontal
scaling. The widths are `/Widths` from `/FirstChar` with the descriptor's `/MissingWidth`, or the
descendant's `/W` and `/DW`. A font program's own advances are never read.

The clip modes gather glyph outlines in device space and narrow the clip to them all when the text
object ends. **The clip is narrowed only where at least one glyph had an outline.** A text object
that set a clip mode and showed nothing, or whose font cannot be drawn, would otherwise clip away
the rest of the page.

A stroked glyph is the one place an outline is copied: the line width is a length in user space and
the outline is not, so the outline is taken to user space and one matrix then governs both.

## Reading content that is broken

`ContentReader.ReadContent` throws at the first fault and loses every operator of the stream, those
before the fault included. `CParser.ReadContentPastFaults` reads on instead: the operands gathered
when the fault was met are dropped, one byte is skipped where the lexer stopped without moving, and
the reading carries on. The faults are handed back with the position each was met at, so the
interpreter reports each in the order of the content. After 64 faults the rest of the stream is
left unread, because each costs an exception.

A name that is no operator is one of those faults, and is not reported between `BX` and `EX`. It
is content written for a newer reader far more often than it is damage, so it does not count
towards the 64 and has an allowance sixteen times as large.

**Arrays may be written 32 deep inside one another and no deeper.** `CParser` reads each array by
a call inside the one that reads the array it is in, and a stack overflow cannot be caught: content
of nothing but opening brackets ended the process. The limit is in `ParseArray`, so it holds for
`ReadContent` as well, which now throws a `ContentReaderException` where it ran out of stack.

`CLexer.KeepStringBytes` keeps a string's bytes where the lexer would decode it as UTF-16. A string
shown by `Tj` is character codes, and a composite font whose first code is 0xFEFF opens with the
same two bytes as a byte order mark.

## Diagnostics

`PageRenderResult.Diagnostics` holds one entry for each different problem, keyed by its
`PageRenderProblem` and its subject. A problem met again raises `Occurrences`. At most 256 entries
are kept and the rest are counted in `OmittedDiagnostics`, so a page that repeats one bad operator a
million times costs one entry, and one that names a million missing resources costs 256 and a count.

Every operator is run inside a guard that records whatever it throws as `OperatorFailed`. **Two
things pass the guard**: being out of memory, and `UnsetSeamException`, which carries the exception
of a seam that is needed and unset. `PageRendering.Render` throws the seam's own exception in its
place.

What a device throws is caught nearer than that. The interpreter calls the caller's device through
`GuardedDevice`, which records the exception against the operator being drawn and goes no further,
so the clip to the page's box, the clip a text object ends with and the restores a render ends with
are covered as well as the operators are. It also remembers which saves the device made, and a
`Restore` reaches the device only where the `Save` it matches did.

A page that ends with states still saved is not reported. Many real pages do, nothing was left out,
and the states are restored on the device all the same. A `Q` with nothing saved is reported.

Three limits keep a malformed page from running away, and each is reported as `LimitReached`: a form
is entered at most once at a time, 32 deep and 4096 times in all; `q` is only counted past 256 saved
states; and the 64 faults above. A fourth is silent: a composite font's `/W` gives widths to four
times as many CIDs as there are and is then read no further, because a `/W` that gives the whole
range one width, run after run, took minutes to read.

## Page geometry

`PageView.Of(page, box, scale, region)` works out where a page lands: the box, `/Rotate`,
`/UserUnit`, the scale and the region, with y turned downwards. It gives the transform the
interpreter starts from and the size in pixels of a bitmap that holds the region. The interpreter
clips to the box before it draws anything. Clipping to the region is the device's caller's, because
a region is a rectangle of the device and a canvas already has one.

A page that states no box with any area is drawn as US Letter, which is what viewers take it to be,
and reported. A box that is asked for and has no area, such as a crop box that misses the media
box, is the media box.

`/UserUnit` is read from the key, because `PdfPage.UserUnit` (#377) is being added separately.

## Fonts are kept on the document

`PdfDocument.RenderFontCache` holds one `RenderFont` for each font dictionary, keyed by the
dictionary's reference, or by the dictionary itself where it is written in place. A `RenderFont`
holds the widths, the glyph source and the outline of each glyph asked for so far.

What a font cannot do is reported by each render that shows it, not once when it is read: the font
outlives the render, and the second page to use it has to say so as well.

Fonts made with a caller's own glyph sources are kept for that call only.

## Where the other issues plug in

None of these edits `ContentInterpreter`.

| issue | what it adds | where |
|---|---|---|
| #372 colour | Classes derived from `RenderColorSpace`: `ComponentCount`, `ToRgb`, and `InitialColor` where it is not all zeros | `ColorSpaces.Resolve`, the branch on an array's family |
| #373 TrueType | A class derived from `PageGlyphSource` for `/FontFile2` | The first branch of `BuiltInGlyphSources.Select` |
| #374 standard 14 | A class derived from `PageGlyphSource` that reads `RequiredSeams.FontResolver` in `GetOutline` and nowhere else, and answers AFM widths from `TryGetAdvance`, which is asked for invisible text too and so reads no seam | The last branch of `BuiltInGlyphSources.Select` |
| #375 images | The bodies of `PageImages.ReadXObject` and `ReadInline`, which answer a `PageImage` or report and answer null | `PageImages.cs` |
| #376 Skia | A class derived from `PageDevice` | Its own package: `PageView.Of`, then `PageRendering.Render` |

A `PageGlyphSource` is asked for an outline by the character code of a simple font or the CID of a
composite one, and answers it in text space before the type size is applied, where a glyph one em
wide spans 0 to 1. Choosing the glyph, through `/Encoding`, the font program's `cmap` or
`/CIDToGIDMap`, is the source's work. It is public and experimental so that the tests here can set
text with no font file, and so that a caller can supply a kind of font this library does not read.

## What was deliberately left out

**CMaps and vertical writing.** A composite font's codes are read as two bytes that are the CID,
which is what `Identity-H` says and what this library writes. Any other CMap is read the same way
and reported, and `Identity-V` is set horizontally and reported. Reading CMaps belongs with choosing
glyphs (#373).

**A limit on the operators of a page.** Forms are bounded and the file's size bounds the rest, so a
page is slow before it is endless.

**The unset seam has no test here.** Nothing in this unit reads a seam, and a test cannot throw the
internal exception that carries one. The first glyph source that reads the font resolver (#374)
brings the test with it.

**Moving the extraction walkers onto this interpreter** stays the follow-up `page-rendering.md` makes
it (item 18). `FormVisits` here repeats the limits of the text extractor's own until then.
