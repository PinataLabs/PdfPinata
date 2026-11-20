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
| 9 | Glyph outlines from an embedded TrueType font program (#373); standard 14 fonts (#374) | done; plug-in point only |
| 10 | Images (#375) | plug-in point only |
| 11 | `Identity-V` and vertical writing (#373); CMaps other than `Identity-H` and `Identity-V` | done; deliberately not done |
| 12 | A limit on the operators of one page | deliberately not done |
| 13 | Type 3 fonts (their advances are read; their glyphs are not drawn) | advance only |
| 14 | Transparency groups (#383) and optional content (#386) | reported, not applied |

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

A path is painted in the user space of the transform in force at the painting operator, which is
the one the pen is measured in. A point is placed by the transform in force when it is added, so
where `cm` or `Q` changes the transform in the middle of a path, which is not allowed and is still
found in files, the points before the change stay where they were. `PathInProgress` keeps the
points in the user space the path began in: a point added under another transform is taken to that
space as it is added, and the whole path is taken to the painting operator's space once, when it
is painted. **Each point is mapped at most twice, however often the transform changes.** The first
version rewrote every point already in the path at each change, so 40,000 `q cm l Q` inside one
path, which is 2.5 KB compressed, took most of a minute. A path painted under the transform it
began under, which is every well-formed one, is handed over as the content wrote it. `W` and `W*`
are remembered and applied after the painting operator that follows, which is the order
ISO 32000-1 8.5.4 gives.

**Text is laid out here, not in a device and not in a font.** A shown string is split into codes,
one byte for a simple font and two for a composite one. Each code advances by the PDF's own width,
then character spacing, word spacing after the single byte 32 and nothing else, and horizontal
scaling. The widths are `/Widths` from `/FirstChar` with the descriptor's `/MissingWidth`, or the
descendant's `/W` and `/DW`. A font program's own advances are never read.

Those widths are thousandths of text space, with one exception. **A Type 3 font's `/Widths` are in
glyph space, and its `/FontMatrix` takes them to text space** (ISO 32000-1 9.6.5): 50 under a
matrix of a hundredth is half an em. A Type 3 glyph is not drawn yet, but its advance is read
now, because a page from TeX sets Type 3 beside other fonts and everything after a Type 3 run
is placed by it.

A composite font whose CMap is `Identity-V` is set down the page (ISO 32000-1 9.7.4.3). Each glyph
is drawn with its position vector on the pen, and the pen then moves by the vertical advance plus
character spacing, with no horizontal scaling. `VerticalMetrics` reads both from the descendant's
`/W2` and `/DW2`; where they say nothing the advance is -1000 and the vector is half the CID's
horizontal width across and 880 up. A number in a `TJ` array moves the pen down rather than back.

The clip modes gather glyph outlines in device space and narrow the clip to them all when the text
object ends. **The clip is narrowed only where at least one glyph had an outline.** A text object
that set a clip mode and showed nothing, or whose font cannot be drawn, would otherwise clip away
the rest of the page.

A stroked glyph's line width is a length in user space and its outline is not, so one of the two
has to be taken to the other. Where the outline's space is user space scaled the same in every
direction, the line state goes to the outline: the width, the dashes and the phase are divided by
the scale, and the device is handed the glyph's own shared `PagePath` under the glyph's matrix, as
it is for a fill. That is all text not stretched by `Tz` or by a text matrix that scales x and y
differently. For such stretched text the pen is an ellipse in the outline's space, which a
`PageStroke` cannot say, so the outline is copied into user space and that path is not shared.

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

**A page written as several streams is drawn from those that can be read.** One stream of a
`/Contents` array that cannot be decoded is left out and reported as `UnreadableContent`, with the
count, and the others are drawn. `PdfContentStreams.TryGetReadablePageContent` does this for
rendering alone: the text extractor and the image placement reader still give up the whole page
through `TryGetPageContent`, because a reader that answers half of what a page says has answered
wrongly without saying so.

**Two things are reported that are drawn wrongly and not left out**, as `UnsupportedFeature`, the
way `/BM` and `/SMask` are, so that `IsComplete` is false for a page a viewer would show
differently:

- A form with `/Group /S /Transparency` drawn where `/ca` or `/CA` is below 1, or whose group is a
  knockout group (`/K true`). A group is drawn on its own and laid on the page once at that alpha;
  here each piece of it is drawn at that alpha, so pieces that overlap show through each other.
  With both alphas at 1 and no knockout the two are the same picture and nothing is reported,
  because a producer such as cairo makes every form a group. Subject `/Group`. #383 composes them.
- Content in `/OC … BDC`, or an XObject with an `/OC` entry, whose optional content group is off
  in the catalog's default configuration (`/OCProperties /D`: `/BaseState`, `/ON`, `/OFF`), or
  whose membership dictionary evaluates off by its `/VE` expression or else its `/P` policy over
  `/OCGs`. The content is drawn. Subject `/OC`. A `/VE` is read 32 deep and 1,024 operands long,
  and what lies past either is taken to be visible, because an expression may name itself. `OptionalContent` answers the question and hides
  nothing; #386 applies it.

A page that ends with states still saved is not reported. Many real pages do, nothing was left out,
and the states are restored on the device all the same. A `Q` with nothing saved is reported.

Three limits keep a malformed page from running away, and each is reported as `LimitReached`: a form
is entered at most once at a time, 32 deep and 4096 times in all; `q` is only counted past 256 saved
states; and the 64 faults above. **A form's content is decoded and parsed once in a render**, the
first time it is drawn, and kept by the form's reference until the render ends: twelve forms that
each draw the next twice reach the last some two thousand times inside those limits, and reading
its stream again each time is what such a page cost. Its faults are still reported each time it
is drawn. A fourth is silent: a composite font's `/W` gives widths to four
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
| #373 TrueType | `EmbeddedTrueTypeGlyphSource`, for `/FontFile2`. Built: see the next section | The first branch of `BuiltInGlyphSources.Select` |
| #374 standard 14 | A class derived from `PageGlyphSource` that reads `RequiredSeams.FontResolver` in `GetOutline` and nowhere else, and answers AFM widths from `TryGetAdvance`, which is asked for invisible text too and so reads no seam. Its outlines come from the resolved face's `OpenTypeFontface.Glyphs`, through `TrueTypeOutlinePaths.ToPagePath`, and a code's glyph through `GlyphNames` and `TrueTypeGlyphs.CharacterMap`, all as the next section sets out | The last branch of `BuiltInGlyphSources.Select` |
| #375 images | The bodies of `PageImages.ReadXObject` and `ReadInline`, which answer a `PageImage` or report and answer null | `PageImages.cs` |
| #376 Skia | A class derived from `PageDevice` | Its own package: `PageView.Of`, then `PageRendering.Render` |

**The font rows hold for a font whose glyphs are outlines, set along x in codes of a fixed
length**: TrueType, CFF, Type 1 and the standard 14. Three kinds of font are not that, and each
edits the interpreter as well as adding a source:

- **Type 3.** A glyph is a content stream of `/CharProcs` with its own resources, drawn under
  `/FontMatrix`, that sets its own colour (`d0`) or takes the text's (`d1`). It is not an outline,
  so `DrawGlyph` has to run it as it runs a form, under the same limits.
- **A CMap other than `Identity-H`.** Its codespace ranges give codes of one to four bytes in one
  string. `ShowString` has to split a string by the CMap, where it takes `RenderFont.BytesPerCode`
  at a time, and map each code to a CID. The word-spacing rule goes with it: `Tw` is added after a
  code that is the single byte 32 and after no longer code, which becomes a question about each
  code and not about the font.
- **Vertical writing.** A glyph advances down the page by `/W2` and `/DW2` and is placed by a
  position vector, so `FontWidths`, the advance in `ShowString` and the direction a `TJ`
  adjustment moves in all change.

A `PageGlyphSource` is asked for an outline by the character code of a simple font or the CID of a
composite one, and answers it in text space before the type size is applied, where a glyph one em
wide spans 0 to 1. Choosing the glyph, through `/Encoding`, the font program's `cmap` or
`/CIDToGIDMap`, is the source's work. It is public and experimental so that the tests here can set
text with no font file, and so that a caller can supply a kind of font this library does not read.

## Embedded TrueType font programs (#373)

**The font program is decoded here, never handed to a platform's font engine.** DirectWrite and
FreeType draw a broken subset differently, and a page has to look the same everywhere. Nothing is
hinted: a glyph is its outline as designed.

### The decoder, for any TrueType face

`Fonts.OpenType/TrueTypeGlyphs` is internal and knows nothing of PDF, so #374 draws a face the font
resolver serves with it as well.

```csharp
static TrueTypeGlyphs Read(byte[] fontProgram);      // InvalidDataException: not TrueType, or no head, loca or glyf
int UnitsPerEm { get; }                               // from head; 16 to 16384, else 2048
int GlyphCount { get; }                               // maxp's count, or fewer where loca has room for fewer
TrueTypeOutline GetOutline(int glyph);                // null: no such glyph; Empty: no contours; throws InvalidDataException
TrueTypeCharacterMap CharacterMap(int platform, int encoding);   // formats 0, 4, 6 and 12, or null
bool HasCharacterMaps { get; }
int GlyphNamed(string name);                          // from post formats 1 and 2, or -1
```

`OpenTypeFontface.Glyphs` reads one from a face's bytes on first asking. A `TrueTypeOutline` is the
font program's own shape: points in font units, y upwards, each on the curve or a quadratic control
point, and where each contour ends, with a composite's components already placed.
`Pdf.Rendering/TrueTypeOutlinePaths.ToPagePath(outline, unitsPerEm)` turns one into a `PagePath` in
ems: each contour closed, an on-curve point implied between two control points, a contour that
begins on a control point begun at its last point or halfway between the two, and each quadratic
curve the cubic that traces it.

It is not built on `OpenTypeFontface`'s own reading. That reads every table it knows when it is
made, through one shared cursor, and reads past the end of a truncated table; one broken `name` or
`OS/2` table in an embedded subset would cost every glyph. Here every read is checked against the end
of its table, a table that runs past the end of the data is kept as far as the data goes, and what
is malformed throws `InvalidDataException` for the glyph that meets it and no other.

A composite glyph's components are placed by an offset, by matched points, or by a scale, an x and y
scale or a 2 by 2 transform. An offset is scaled with the component only where the font sets
`SCALED_COMPONENT_OFFSET`; not scaling it is the OpenType default. **A glyph is held to eight
levels of composites, 1024 components and 65,536 points in all**: a glyph built of itself is
reported rather than followed forever, a few levels that each use the one below four times would
otherwise be tens of thousands of contours, and a thousand references to one glyph of 65,535 points,
a few hundred bytes of font, would otherwise be gigabytes of outline.

### Choosing the glyph

`EmbeddedTrueTypeGlyphSource` reads nothing when it is made. The font program is read when the first
outline is asked for, so a font used only for invisible text costs nothing, and one that cannot be
read is reported as `MissingGlyph` against each glyph shown in it, with the reason.

- **Composite fonts.** The code is the CID, by `Identity-H` or `Identity-V`. `/CIDToGIDMap` is
  `/Identity`, which is also what no entry means, or a stream of two bytes for each CID. A CID past
  the end of the stream has no glyph.
- **Simple fonts.** The descriptor's `/Flags` say which way is tried first, and the other way is
  tried after it, because real files often say a font is symbolic when it is not, or the reverse.
  A font that names `/WinAnsiEncoding` or `/MacRomanEncoding` is tried by name first whatever its
  flags say, as ISO 32000-1 9.6.6.4 has it. A code that `/Differences` names `.notdef` has no glyph,
  and is not looked up another way.
  - *By name*, first for a nonsymbolic font: the code's glyph name from `/Differences`, or else its
    character from the base encoding, which is `/StandardEncoding` where a nonsymbolic font names
    none. A name becomes a character through `GlyphNames` (the Adobe Glyph List, `uniXXXX` and
    `uXXXX`). Then the (3,1) `cmap`, or (3,10), (0,3) or (0,4); then the `post` table's names: the
    name `/Differences` gives, or for a code of the base encoding each name its character goes by;
    then the character's MacRomanEncoding code in the (1,0) `cmap`.
  - *By code*, first for a symbolic font: the (3,0) `cmap` at the code, then at the code plus
    0xF000, 0xF100 and 0xF200; then the (1,0) `cmap` at the code; then the Unicode `cmap` at the code.
  - A font program with no `cmap` at all is taken to number its glyphs by the codes.

Glyph 0, `.notdef`, is never drawn: a code that comes to it has no glyph, which is reported.

`GlyphNames` reads `Fonts/GlyphNameTables.g.cs`, which `tools/GlyphNameTableGenerator` writes from
the checked-in Adobe Glyph List. It is the core's one map from glyph names to characters.

### Caching

The source belongs to the `RenderFont` that `RenderFontCache` keeps for each font dictionary, keyed
by its reference, so a font program is decoded once per document, and each glyph's `PagePath` once
per font. Two font dictionaries that share one `/FontFile2` stream decode it twice.

## What was deliberately left out

**CMaps.** A composite font's codes are read as two bytes that are the CID, which is what
`Identity-H` and `Identity-V` say and what this library writes. Any other CMap, named or embedded,
is read the same way, set horizontally and reported.

**Hinting, and a glyph source for TrueType collections.** A PDF may not embed a collection, and
`TrueTypeGlyphs.Read` refuses one.

**A true union of the glyphs a text object clips to.** The glyphs are one path clipped by nonzero
winding, which is their union where their contours wind the same way. Two glyphs that overlap and
wind opposite ways, as a glyph does over its own mirror image under a later `Tm`, cancel where they
overlap. A union needs path operations the core does not have and a device does, so it waits for a
page that needs it.

**A limit on the operators of a page.** Forms are bounded and the file's size bounds the rest, so a
page is slow before it is endless.

**The unset seam has no test here.** Nothing in this unit reads a seam, and a test cannot throw the
internal exception that carries one. The first glyph source that reads the font resolver (#374)
brings the test with it.

**Moving the extraction walkers onto this interpreter** stays the follow-up `page-rendering.md` makes
it (item 18). `FormVisits` here repeats the limits of the text extractor's own until then.
