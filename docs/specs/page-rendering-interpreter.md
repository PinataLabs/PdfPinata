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
| 8 | Indexed and ICCBased colour (#372), CalGray and CalRGB as the device spaces | done |
| 9 | Glyph outlines (#373, #374) | plug-in point only |
| 10 | Images (#375), CCITTFax (#385) | done, but JBIG2 and JPX (#385) |
| 11 | CMaps other than `Identity-H`, and vertical writing | deliberately not done |
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
  turns a CMYK JPEG to RGB itself and `SKCodec` has no way to hand back the four inks. So the
  core applies a JPEG's `/Decode` array and colour key to that RGB only where the RGB is the
  image's own components: a JPEG of one component or of three.
- **A JPEG's colours come back with any Adobe inversion undone.** That is the codec's convention,
  and `DecodeImage` states it. A JPEG of four components with an Adobe `APP14` marker stores its
  inks inverted, as Photoshop writes them. A PDF reads the samples as they are stored, so a file
  embedding such a JPEG says `/Decode [1 0 1 0 1 0 1 0]` to invert them back, and SkiaSharp
  decodes a cyan one to `rgb(1,255,255)`, which is what Ghostscript draws for that file. The core
  therefore takes that `/Decode` as applied already. It reads the JPEG's markers up to its first
  scan, stepping over each by its length, and reports `UnsupportedFeature` where the two do not
  come together: the marker with the default `/Decode`, which Ghostscript draws near-black and the
  device cyan, or the inverting `/Decode` with no marker. Any other `/Decode`, and a colour key,
  on a JPEG of four components is reported and not applied.

  SkiaSharp inverts every JPEG of four components, marker or no marker, so for a file with no
  marker `SkiaPageDevice` undoes it (#413): it decodes two copies in which one component's
  quantisation table is zeros, which makes that component's samples 128, one without black and one
  without cyan. Skia's `R = c * k / 255` then gives each ink to within a level, and the device
  answers the RGB of the inks as they are stored. The zero table goes in just before the first
  scan under an identifier the file never defines, because a decoder takes a table as it stands
  when the scan starts and a later definition of the same identifier would overwrite it. The
  file's own decode is never made, as its size is in the header and a JPEG has no alpha. A file
  that defines or uses all four identifiers cannot be copied, and is declined and reported as an
  unsupported image.

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
is painted. **Each point is mapped at most three times, however often the transform changes**:
as it is added, once if the path is re-based, and when it is painted. The first
version rewrote every point already in the path at each change, so 40,000 `q cm l Q` inside one
path, which is 2.5 KB compressed, took most of a minute. A path painted under the transform it
began under, which is every well-formed one, is handed over as the content wrote it.

A transform with no inverse, such as `1 0 0 0 0 0 cm`, has no user space to take a point back to.
A path begun under one keeps its points as written for as long as that transform stays, and when
a point is added under another they are **re-based once**: onto the new transform where it has an inverse,
and onto device space where it does not either. Either base has an inverse, so the path is never
re-based again and the cost stays linear. So `q 1 0 0 0 0 0 cm 0 0 m 10 10 l Q 20 20 l S` draws
the first two points flattened onto one line and the third where the restored transform puts it.
The first fix round kept the points as written for the whole path and then mapped every one of them
through the singular transform, which flattened the third point too (#400). A path painted under
a transform with no inverse is handed to the device in the space its points were placed in, with
the transform of that space, so `0 0 m 10 0 l 10 10 l h 1 0 0 0 0 0 cm f` fills the triangle its
points make rather than a line. **A stroke painted under such a transform paints nothing**, and
reports nothing: a pen is a width in user space and the transform leaves it none, which is what
Ghostscript and pdf.js draw, so `10 w 20 50 m 80 50 l 1 0 0 0 0 0 cm S` leaves the page blank.
The fill and clip of the same path stand, and `B` still fills. `PathInProgress.ToPath` changes nothing
and can be asked again. `W` and `W*`
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

## Annotations

After the page's content, and unless `PageRendering.Render` was told `annotations: false`, each
entry of `/Annots` that has an appearance is drawn through the Form XObject path (ISO 32000-1
12.5.5). They are drawn in the order listed, each from the graphics state the page began in and
under the clip of the page's box, whatever the content left set or clipped to: the page's save is undone and begun again, so a `W n` outside a `q` in the content does not cut the annotations. The appearance is `/AP /N`, or,
where that is a dictionary of states, the stream `/AS` names. A dictionary of states with no `/AS`
or one that names no state draws nothing. That follows Ghostscript and the PDF reference; pdf.js
instead draws a check box with no `/AS` from its field's value, and this does not. An `/AP`, `/N` or state entry that is present and is not a stream is reported as `InvalidValue`, and an absent one is not.

The box is mapped on to the rectangle by the algorithm of 12.5.5: `/BBox` is taken through
`/Matrix`, and the rectangle that encloses the result is scaled and moved on to `/Rect`. The
interpreter then draws the form with that map as the CTM, and `DrawForm` applies `/Matrix` and the
clip to `/BBox` as it does for a `Do`. A `/Rect` with no area draws nothing and is not reported. An
appearance whose box has no area once the matrix is applied, one with no `/BBox`, and a `/Rect`
that is not four finite numbers are reported as `InvalidValue`, as is a bad `/Matrix`. The form's
own resources are used, and the page's where it has none. An appearance that draws itself is
stopped by `FormVisits`, as any form is. The annotations have a `FormVisits` of their own, begun
when the content is done, so a page whose content entered 4096 forms still has its annotations
drawn. It has the content's limit of 4096 and no more: a page with more than that many
appearance-bearing annotations is outside real use, and the limit is what bounds the work a file
can ask for. Past it the rest are reported once as `LimitReached`.

**The no-zoom and no-rotate flags (bits 4 and 5) are not applied.** The appearance is drawn scaled and turned with the page, and where the view is not at 1 to 1 and upright an annotation carrying either is reported as `UnsupportedFeature`.

`/F` is read as an integer, and a real with no fractional part (`2.0`) is read as the integer it
equals, as viewers do, so a hidden annotation written that way is still hidden.

**`/NeedAppearances` is not applied.** Viewers disagree on a form that sets it: pdf.js makes the
appearances again, PDFium discards the stored ones, and Ghostscript draws them. The stored
appearance of a `/Widget` is drawn, and when the catalog's `/AcroForm /NeedAppearances` is true
and a widget is drawn the render is reported once as `UnsupportedFeature` on `/NeedAppearances`,
so its result is not complete.

**Not drawn:** a hidden annotation (`/F` bit 2), one with the no-view flag (bit 6), and a
`/Popup`. The invisible flag (bit 1) only matters for annotations of a type the viewer does not
know and that have no appearance, so it changes nothing here. The print flag is not read: this is
screen rendering, and a print mode that kept only annotations with bit 3 set was left out, because
no caller has asked for it. A widget with no `/AP` is not drawn. `/MK` is not consulted: a reader
shows an appearance in place of one built from it.

**Transparency is not applied to an annotation.** An annotation's `/CA` below 1 is reported as
`UnsupportedFeature` on `/CA`, and the appearance is drawn opaque. Blend modes are handled as for any form.

**Optional content is reported, not applied.** An `/OC` on the annotation and an `/OC` on its
appearance stream are each reported as `UnsupportedFeature` where the group is switched off, as a
`Do` of the same form would report its own, and the appearance is drawn all the same.

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
  `/OCGs`. The content is drawn. Subject `/OC`. A group counts only where one of its `/Intent`
  names, `/View` where it has none, is among the configuration's `/Intent`, which is also `/View`
  where `/D` has none and takes in every intent where it names `/All`. A group that does not count
  has no effect on visibility (ISO 32000-1 8.11.2.3 and 8.11.4.3), so it is left out of `/OCGs` and
  of a `/VE`, rather than read as on: read as on, `/AllOff`, `/AnyOff` or `/Not` over it would
  turn it to hidden. A membership with no group left has no effect, as Table 99 says of one whose
  `/OCGs` names none, and an `/And`, `/Or` or `/Not` none of whose operands has an effect has none
  either. So a configuration `/Intent` of `[]` makes everything visible, which is what 8.11.2.3
  asks. A dictionary whose `/Type` is neither `/OCG` nor absent is not a group, wherever it is
  named: by `/OC`, in `/OCGs` or as a `/VE` operand, it makes the membership visible as a whole.
  A `/VE` is read 32 deep and 1,024 operands long. An expression any part of which cannot be read, or lies
  past either limit, is visible as a whole: a `/Not` over it does not turn it round, so
  `[/Not [/Xor …]]` is visible. Every operand is read for that reason, rather than stopping at the
  first that decides. `OptionalContent` answers once per membership for one render, keyed by
  `ReferenceIdentity`, because a page may name one membership thousands of times and each answer
  may cost the whole operand budget. It answers the question and hides nothing; #386 applies it.

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

## Images

`PageImages` turns an image XObject or an inline image into a `PageImage`, or reports why it
cannot and answers null. Both kinds take one path: an inline image's dictionary is read by the
document parser and every abbreviation in it, of a key, a device space, `/Indexed` or a filter, is
written out in full first, so the colour spaces never see one. An inline image may name a space of
the `/ColorSpace` resources, and so may an XObject.

**The samples are read where they lie.** `ImageSamples` reads one component at a time from the
unfiltered bytes: 1, 2, 4, 8 or 16 bits, high bits first, each row from a byte of its own. Each
sample goes through `/Decode` to the number it stands for and through the space's `ToRgb`. An
indexed image's default `/Decode` is 0 to 2^bits − 1, so the number an Indexed space is handed
is the index itself. A space of one component and up to 8 bits has at most 256 colours, and they
are worked out once.

**Data that stops short is drawn as far as it goes**: a sample past the end reads as 0, and the
image is reported. Data of less than half the image is refused, so that a few bytes cannot stand
for an image of any size the limit allows.

An inline image's dictionary may nest arrays and dictionaries at most 32 deep. The document parser
reads a nested array by a nested call, so `Parser.ReadDictionary(string, int)` counts the depth
where it recurses and throws past it, and the image is reported. Counting the brackets in the text
instead would refuse a palette written as a binary literal string, whose bytes `0x5B` and `0x3C`
are ordinary colours, and a count that skipped strings could be fooled by text the lexer reads
another way. The bound is only on this path: a document is read as before. Data a filter fails on
is not drawn at all, because the filters throw rather than hand back what they had decoded.

**Each image XObject is decoded once a render.** The interpreter keeps what `PageImages` answers,
keyed by the XObject's reference through `ReferenceIdentity`, so a page that draws one image a
hundred times decodes it once. A failure is kept too, and reported the first time. A stencil
mask is kept like any other image, because what is decoded is its coverage and the fill colour
is applied at each draw. What is kept is bounded at 1 GiB of samples, one image of the largest
size; an image that does not fit is decoded at each draw, as before. An image of at most 1 MiB
is kept against a budget of its own, 64 MiB, so that large images that have spent the first do
not make a small tile decode at every draw (#413). Five draws of a 16 KB Flate
stream that inflates to a 1-bit image of 2^28 samples took 11.9 s and allocated 5.2 GiB in a
Release build, and take 2.5 s and 1 GiB now, which is one draw's. Inline images are not kept,
and nor is an image whose `/ColorSpace` names a space of the resources, because two forms can
draw it with different resources and so in different colours. The first filter is handed a copy
of the stream's bytes, because `/ASCII85Decode` compacts the array it is given in place and the
stream's own bytes are read again by the next render.
Drawing many distinct large images is still paid for once each, so the bound on what a filter
inflates to, below, is what limits that.

**An image is refused before anything is decoded when it has more than 2^28 samples.** Its RGBA is
then 1 GiB, which a page of A0 scanned at 400 dots to the inch stays under. A `/Width` and
`/Height` are a few bytes, and past this they ask for more memory than any page warrants, and
soon for more than one .NET array holds.

The masks:

- `/ImageMask` answers `Alpha8`, 255 where a sample decodes to 0, which the interpreter paints in
  the fill colour.
- A `/Mask` that is an image is the same stencil, multiplied into the image's alpha.
- A `/Mask` that is an array is a colour key, compared with each sample as it is written, before
  `/Decode`.
- An image drawn under a mask of another size is scaled to the larger width by the larger height,
  at most 2^28 samples and at most 64 times the larger of the two sample counts. The 64 bites only
  when the two cross, one wider and the other taller, because otherwise the drawn size is the
  mask's own count (a scan's fine text mask over a coarse image is never capped by it). Past
  either limit the mask is drawn at the image's size and `LimitReached` is reported (#413).
- An `/SMask` is the alpha itself and overrides a `/Mask`. Its `/Matte` is taken back out of the
  colours, in RGB: exact for a device space of one or three components, close for the rest.
- A mask finer than the image, in either direction, does not lose its detail: the image is scaled
  up to the mask's size by the nearest of its samples, and the mask is applied at its own size. A
  scan split into layers puts a coarse colour image under a fine mask of the text, and thinning
  the mask to the image would draw the text as blocks. A mask coarser than the image is stretched
  over it. Neither grows the image past 2^28 samples; past that the mask is made the image's
  size. A mask that cannot be read is reported, and the image is drawn without it.

**CCITTFax is a filter like any other** (#385), `CcittFaxDecode` in `Pdf.Filters`, written from
ITU-T T.4 and T.6, so a stream decodes through `Filtering` and `TryUnfilter` as well as here.
`PageImages` calls the decoder underneath it, `CcittFaxDecoder.Decode`, for the one thing the
`Filter` signature cannot carry: what was wrong with the data. Damaged or short data is drawn as
far as it decodes, the image's `/Height` in rows with the rest white, and the damage is reported
as `InvalidValue`; it never reaches the half-the-data refusal above. `/BlackIs1` belongs to the
filter, which answers 1 for black when it is true. The image's `/Decode` then reads those samples
as it reads any others, so such a file says `/Decode [1 0]` to draw its black as black, and
Ghostscript draws one without it inverted too. The decoder's bounds: each row costs at least one
bit, a row is held as its changing elements, and the output is at most 256 MiB. Two conventions
are Ghostscript's, the only encoder whose output the tests were checked against. In Group 3 mixed
coding without end-of-line codes there is no tag bit, and every K-th row is one-dimensional. And
with `/EncodedByteAlign`, an end-of-line code ends on a byte, so it is looked for before the
padding only where `/EndOfLine` is true: four zeros of padding before a white run of 1792,
`00000001000`, look like one.

**DCT is the one filter the core does not undo.** The filters before it are undone, and the JPEG
that is left is handed to `PageDevice.DecodeImage`; one followed by another filter is reported,
because what a JPEG decoder answers is not bytes a filter reads. The JPEG's own size is the image's
size. An image in a space this version cannot resolve is reported and not drawn, rather than drawn
as a block of fallback colour.

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
| #375 images | The bodies of `PageImages.ReadXObject` and `ReadInline`, which answer a `PageImage` or report and answer null. Done: see Images | `PageImages.cs` |
| #376 Skia | `SkiaPageDevice`, derived from `PageDevice`; done | Its own package: `PageView.Of`, then `PageRendering.Render` |

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

## Indexed and ICCBased colour

`ColorSpaces.Resolve` answers, for an array that opens with one of these families:

- **`/Indexed`** is an `IndexedColorSpace`: one component, an index rounded to the nearest whole
  number and held between 0 and the highest index. The highest index is held to 0 to 255. The
  lookup table is a string or a stream, and bytes it lacks are zeros, reported as `InvalidValue`
  `/Indexed`. Each entry is `ComponentCount` bytes of the base, each read as byte / 255, through the
  base's own `ToRgb`. A base that is itself Indexed, or unsupported, makes the space unsupported
  and drawn in black. An unsupported base has been reported already, and is not reported twice.
- **`/ICCBased`** is an `IccBasedColorSpace` standing for another space. It is the `/Alternate`
  when there is one that is supported, is not Indexed and has as many components as `/N` where
  `/N` is 1, 3 or 4. Otherwise it is DeviceGray, DeviceRGB or DeviceCMYK by `/N`. An alternate
  that cannot stand in is reported, and `/N` is used. With neither, the space is reported as
  `InvalidValue` `/ICCBased` and drawn in black. It is its own class and not the device space,
  because every component of an ICCBased colour starts at zero and DeviceCMYK starts black.
- **A definition that names itself**, by a reference from its base or alternate back to it, or
  that nests more than eight deep, is reported as `InvalidValue` and drawn in black. The resolver
  keeps the arrays it is inside of and does not follow one into itself.

**How an image reads an Indexed space (#375).** An image's samples are integers, so they are not
passed through `ToRgb`, which takes the doubles an `sc` gives. Resolve the image's `/ColorSpace`
with `ColorSpaces.Resolve`, and when it is an `IndexedColorSpace`, call `Lookup(int index)` with the
sample. It holds an index outside the table to the ends, builds the whole palette on the first
call and answers from it after, so calling it for every pixel costs an array read. `HighValue`
is the highest index there is an entry for. The image's `/Decode` default for an Indexed space is
`[0 2^n-1]`, which makes the index the sample itself. For any other space, pass the samples
scaled by `/Decode` to `ToRgb` as before.

## What was deliberately left out

**CMaps and vertical writing.** A composite font's codes are read as two bytes that are the CID,
which is what `Identity-H` says and what this library writes. Any other CMap is read the same way
and reported, and `Identity-V` is set horizontally and reported. Reading CMaps belongs with choosing
glyphs (#373).

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

**A bound on what a filter inflates to.** The limit on samples is checked before decoding, but
`FlateDecode` inflates the whole stream whatever the image needs, so a small stream that inflates
to gigabytes is still inflated. Bounding it belongs in the filter, where every stream would gain.
TODO: PR #406 adds `DecodeLimit`, a cap on Flate output. Once it merges, `PageImages.TryApply`
passes `FilterParms(decodeParms, new DecodeLimit(expectedSampleBytes))`, plus one byte a row where
a PNG predictor is in force, and treats `Truncated` as data that stops short.

**Flate data with a missing or wrong Adler-32 is not drawn.** The inflater throws on the
checksum after it has inflated everything, and the image is reported and dropped whole. Drawing
what was inflated is #409.
`/Interpolate` is not applied.

**Moving the extraction walkers onto this interpreter** stays the follow-up `page-rendering.md` makes
it (item 18). `FormVisits` here repeats the limits of the text extractor's own until then.
