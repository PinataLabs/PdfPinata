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
| 9 | Glyph outlines from an embedded TrueType font program (#373); standard 14 fonts (#374) | done; plug-in point only |
| 10 | Images (#375), CCITTFax (#385) | done, but JBIG2 and JPX (#385) |
| 11 | `Identity-V` and vertical writing (#373); CMaps other than `Identity-H` and `Identity-V` | done; deliberately not done |
| 12 | A limit on the operators of one page | deliberately not done |
| 13 | Type 3 fonts (their advances are read; their glyphs are not drawn) | advance only |
| 14 | Transparency groups (#383) | reported, not applied |
| 15 | Optional content (#386) | done: hidden content is not drawn and not reported |
| 16 | Shadings and patterns (#382): `sh`, shading patterns, tiling patterns, PDF functions | done |

---

## The device

`PageDevice` is an abstract class with six abstract members and four virtual ones. The three
`Try…` members are set out under [Shadings and patterns](#shadings-and-patterns):

```csharp
void Save();
void Restore();
void Clip(PagePath path, XMatrix transform, XFillMode fillMode);
void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint);
void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint);
void DrawImage(PageImage image, XMatrix transform, PagePaint paint);
virtual PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data);   // null: cannot
virtual bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint);    // false: cannot
virtual bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint);
virtual bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint);
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
so with `IsShared`, which is what a device caches its own path against. That holds for the first
18 MiB of outline a document's fonts keep, about a million points; a glyph past that is handed over
unshared, each time anew.

**It is a class and not an interface**, so that shadings, patterns, blend modes and transparency
groups can arrive as `virtual` members whose base body says the device cannot draw them.
`DecodeImage` was the first of that kind, and the three `Try…` members for shadings and patterns
are the next. An interface could only do that with a default member, which
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

## Shadings and patterns

**A shading or a pattern is something to paint with, not something to draw.** It crosses the seam
as a `PageShader` inside a `PageShaderPaint`, and the three operations that take a paint each have
a twin that takes one: `TryFill`, `TryStroke` and `TryDrawStencil`. That covers every use. A path
filled or stroked in a pattern is the first two. A glyph is a path, so it is the same two, and its
outline is still the shared instance. A stencil mask painted in a pattern is the third. `sh` is a
fill of everything the interpreter draws into, under whatever the clip leaves, so it needs no
member of its own.

**The shader has its own transform, and it is not the path's.** `PageShaderPaint` holds the
shader, a transform from the shader's coordinates straight to device space, and the alpha. A
pattern's `/Matrix` is over the default space of the content stream that names it: the page's
space, or the space a form began in. It is not over what `cm` has made of that space since. So
the interpreter keeps the space each stream began in beside its resources, and a pattern's
transform is its matrix followed by that. For `sh` it is the transform in force. A device is
handed two transforms and has no rule to remember.

**The device learns no PDF.** These are all resolved in the core, and none reaches a device:
`/Function`, `/Domain`, `/ColorSpace`, `/BBox`, `/Background` as an entry, a pattern's `/Matrix`,
`/PaintType`, `/XStep`, `/YStep` and the shading type number.

| shader | what it is |
|---|---|
| `PageAxialShading` | Type 2. `Start`, `End`, `Stops`, `ExtendStart`, `ExtendEnd`, `Background`. |
| `PageRadialShading` | Type 3. Two centres and two radii, and the same four as an axial shading. |
| `PageMeshShading` | Types 1 and 4 to 7. `Points`, three a triangle. `Colors`, three bytes a point. `Bounds`, `Background`. |
| `PageTiling` | A tiling pattern. `Cell`, a `PagePicture`. `Tile`, the rectangle that repeats. |

The hierarchy is closed: the constructors are internal. `PageShader.IsShared` says the instance
is handed over each time the content paints with the same shading or pattern, as
`PagePath.IsShared` does for a glyph, and a device keeps what it builds from one.

**Stops.** A gradient's function, its `/Domain` and its colour space are in its stops. The first
is at 0 and the last at 1. There is a stop at each end, and one either side of every place the
function may jump or turn a corner: a stitching function's `/Bounds`, and the samples of a table.
Two stops at one offset are a hard edge. Between those, a stretch is halved wherever the colour
at its quarters and its middle is more than one part in 255 off the straight line, eight times
at most. A gradient has at most 256 stops. So an exponential function of exponent 1 is two stops,
and a stitching of three such that each jump to the next is six.

**Extension is per end.** `ExtendStart` and `ExtendEnd` are separate, and where an end is not
extended the shader is transparent beyond it. Skia extends both ends or neither, so the Skia
device gives an end that is not extended a second, transparent stop where its first is. A radial
shading needs more. Two circles of it may pass through one point, a PDF paints the later of them
that it paints at all, and Skia paints the later whether or not it lies past an end. So where an
end is not extended the Skia device lays the gradient over itself run backwards.

**`/BBox` and `/Background`.** The core lowers a shading's `/BBox` to a save, a clip and a restore
round the call. `/Background` is a property of the shader a pattern paints with, and null in the
shader `sh` paints with, which ignores it. It is on the shader and not painted first by the core,
because under an alpha below 1 a background painted on its own shows through the shading. The
Skia device composes the two into one shader, so the alpha applies once. Both are clipped to
`/BBox`, which is what Ghostscript does.

**Meshes.** A patch of type 6 or 7 is cut 8 by 8 and each cell into two triangles. A type 6
patch's four inner control points are worked out from its edges. A type 1 shading is its function
on a grid of 64 by 64 over `/Domain`, under `/Matrix`. Colours are blended in RGB, and are 8 bits
a channel. Where a mesh has a `/Function`, a vertex carries the function's input. The input runs
in a straight line across a triangle, so the triangle is cut along the lines where the input is
that of one of the function's stops. A hard edge of the function is then a hard edge across the
triangle.

**Padding.** Each vertex of a triangle mesh, type 4 with its flag and type 5 without, is padded to
a whole byte, as ISO 32000-1 8.7.4.5.5 and 8.7.4.5.6 say and Ghostscript and PDFium read it. A
patch of type 6 or 7 is not padded. The standard says nothing of a patch, pdf.js and PDFium read
patches unpadded, and Ghostscript pads each one. This follows the two, and all three agree
wherever a patch's data ends on a byte.

**Functions are read once a render.** `RenderContext.Functions`, a `FunctionReader`, keeps each
function by the identity of its dictionary, so every shading and every stitching that names one
shares a single instance. A stitching whose 256 pieces each name the stitching below it, four
deep, is five objects to read and not four billion. A function that cannot be read is kept as
that too, so a stream that fails to decode is decoded and reported once however many shadings
name it. A stitching is the exception: it may fail for where it was named, too deep, and is read
again where it is named next, while what it is made of stays kept. Each function also finds its
breakpoints once, the inputs where it may jump that a gradient puts stops either side of, and a
stitching looks up only those of each piece's that land inside the piece, so the search is as
long as the functions are many and not as long as the paths through them. Each function knows its `Cost`, the most work
one evaluation can be: a step of a calculator program, a sample of a table or an output each count
one. `RenderContext.ShadingWork` is charged that cost for every colour found through a function,
and 64 for every triangle of a mesh, across every shading of the render. It also holds the bytes
the render decodes from the streams of functions and meshes, 128 MiB. Each stream is decoded only
as far as it can be read: a sampled function to its table's size, a calculator program to one
byte past 64 KiB, and a mesh to three whole vertices for each triangle it may be drawn as. Every
table a render keeps was charged against those bytes, so they bound what is held as well: a
table past them is not read again later but reported and skipped. Once either budget is spent,
the shading being read is not drawn, and no shading is read after it.

**A tiling cell is recorded by the core.** `ContentInterpreter.Record` runs a content stream
through an interpreter of its own onto an internal `PictureRecorder`, and answers a `PagePicture`:
device calls, kept, in the pattern's own coordinates. `PagePicture.Replay(device, transform)` makes
them again. The nested interpreter has its own device, state stack and surface, so `sh` in a cell
fills the cell. It shares a `RenderContext` with the render it is part of: the diagnostics, the
fonts, the limits on content that draws content, the content already parsed and the shaders
already read. Recording in the core means a device holds no handle the core has to manage, and a
test reads a cell with no imaging dependency. A transparency group (#383) is content of the same
kind and is meant to be recorded by the same function.

**A device repeats one rectangle.** `PageTiling.Tile` is the cell's `/BBox` corner with the steps
as its size. A negative step tiles as its size does. Where a step is shorter than the cell, the
neighbouring cells that reach into the tile are replayed into the picture, and the whole is
clipped to the tile. So a device needs no notion of overlap.

**An uncoloured pattern's colour is in the picture.** The cell is recorded from a graphics state
whose colours are the tint, and every operator that sets a colour is passed over. One recording
is kept for each colour a pattern is painted in.

**A pattern colour space** is `/Pattern` or `[/Pattern base]`, as a `PatternColorSpace`. Selecting
it sets the pattern that paints nothing, which is not reported. A pattern name the resources lack
paints nothing and is `MissingResource`.

### A device written before these

It overrides none of the three, and the base answers false. `PaintRouter`, which is the one place
a paint becomes a device call, then reports `UnsupportedFeature`, with the subject `sh` or
`/Pattern`, and calls the solid `Fill`, `Stroke` or `DrawImage` in one colour that stands for the
shader. For a gradient that is the mean of its stops. For a mesh it is the mean of its corners.
For an uncoloured tiling it is the tint, and for a coloured one the mean of the colours its cell
paints with. A device may also answer false for one kind of shader and true for the others.
`PagePicture.Replay` does the same for a shader call the device answers false to, and answers
false itself.

### Limits

Each is reported once, as `LimitReached` unless it says otherwise, and what could be read is
drawn.

| input | limit | past it |
|---|---|---|
| Sampled function table | 16 MiB by `/Size`, outputs and `/BitsPerSample`, asked before the stream is decoded. 8 inputs. | The function is not read. A stream shorter than its `/Size` is `InvalidValue`. |
| Function nesting | 8 deep, counted where a function is named, though it was read where it nests less. A function made of itself is caught by its reference. 32 inputs or outputs. | The function is not read. |
| Functions read | 4,096 dictionaries a render. One named again is not read again. | The function is not read. |
| Calculator program | 64 KiB, braces 32 deep, a stack of 100. | Not read. A program that runs off its stack, divides by zero, or takes the root or the logarithm of a number out of range answers zeros and is not reported. |
| Shading work | 2^27 for a render: a function's `Cost` for each colour, 64 for each triangle. About a second of the longest calculator program, or two million triangles. | The shading being read, and every one after it, is not drawn. |
| Shading bytes | 128 MiB decoded for a render from the streams of functions and meshes, which bounds the sample tables kept: eight of the largest. Each stream is decoded no further than it can be read. | The shading being read, and every one after it, is not drawn. |
| Gradient stops | 256 | Never more are made. |
| Mesh | 262,144 triangles | Reading stops and what was read is drawn. A lattice whose rows are longer than its data is `InvalidValue`. |
| Cells and forms | Counted together by `FormVisits`: once at a time, 32 deep, 4,096 in all. | The pattern paints nothing. This is what stops a cell that paints with its own pattern. |
| One picture | 65,536 device calls | The rest of the cell is not recorded, but for the restores that keep it in balance. |
| Overlapping cells | 16 copies in one tile | Each tile is drawn from one cell. |
| Tile size on the device | A step of a quarter of a pixel | The pattern is painted as one colour. Not asked inside a cell, whose size on the device is not known there. |
| `/XStep`, `/YStep`, `/BBox` | A step of zero, or a box with no area | `InvalidValue`. The pattern paints nothing. |
| Shader transform | Not finite, or with no area | `InvalidValue`. Nothing is painted. |
| Shaders kept | 1,024 shadings and 1,024 patterns, 32 colours of one uncoloured pattern | The shader is read again each time, and `IsShared` is false. Not reported. Reading it again draws on the shading work, so a shading past those kept and painted without end is bounded too. |

The calculator language has no loop and no procedure call. `if` and `ifelse` are compiled to
jumps forwards, so one evaluation runs each instruction at most once.

### What was left out

- **`/Order 3` in a sampled function** is read as 1, and reported as `UnsupportedFeature`, subject
  `/Order`. A cubic spline between samples is not computed.
- **The order overlapping cells stack in.** Where a step is shorter than the cell, each tile is
  drawn with the tile's own cell over the neighbours that reach into it. The standard does not say
  which of two overlapping cells is on top, and Ghostscript stacks them differently, so where cells
  overlap the two can disagree.
- **A shading pattern's `/ExtGState`** is not applied. It is reported, subject `/ExtGState`.
- **Exact patch colours.** A patch is a grid of flat triangles, so its colours are bilinear
  between its corners across each cell and not across the curved surface. The cut is always 8 by 8,
  whatever the patch's size on the device.
- **Colour interpolation in the shading's own space.** Stops and mesh colours are blended in RGB.
- **A tile-by-tile fallback** for a device that draws no shaders. Such a device paints a tiling
  pattern in one colour.
- **Public constructors for the shader types.** A device's own tests take a shader from a render.
- **A sharp tile under a turned matrix, in the Skia device.** Skia repeats a picture of one tile,
  drawn once at the device's resolution, and resamples it where the pattern's matrix turns it. Its
  edges are then soft. Drawing each tile as paths instead leaves seams between tiles that are
  meant to join, so it was not done.

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

**Optional content applies to annotations.** An annotation whose `/OC`, or whose appearance stream's
`/OC`, is switched off is not drawn, as a `Do` of the same form would not be, and nothing is reported.

## Optional content

Content that belongs to a group the render's configuration switches off is not drawn, and is not
reported: a viewer does not report it either (#386).

**What is hidden.** Marked content opened by `/OC /Name BDC`, where `/Name` is in the resources'
`/Properties`; a form or image XObject with an `/OC` entry; and an annotation or its appearance
stream with one. The entry names a group, or a membership dictionary that makes one answer of
several, read as the rest of this section says.

**Hidden content still changes the graphics state** (ISO 32000-1 8.11.3.2). While a hidden sequence
is open the interpreter runs every operator as it always does, and `GuardedDevice.Hidden` turns
`Fill`, `Stroke` and `DrawImage` into nothing. So `q`, `Q`, `cm`, colours, line state, `gs`, text
state and the text matrix, which a show-text operator advances, all take effect, and so does a clip,
which still reaches the device. `Do` and inline images are skipped outright, because neither leaves
anything behind that a later operator could read: a form's state is its own.

**What hidden content still reports.** Anything that would only paint is not reported, because
nothing of it would be drawn: a shading (`sh`), an image, a form and an XObject that is neither. What
changes the graphics state is still reported, a `gs` with a blend mode or soft mask, a missing
ExtGState or a bad operand, because hidden content changes the state a later visible operator
draws under. Text is reported as visible text is, since it advances the pen and a clipping render
mode needs its outlines.

**A stream of its own starts with nothing hidden.** `RunStream` gives each stream an empty
marked-content stack and zero hidden sequences, and puts the caller's back when it ends. A form
is not entered from hidden content at all, so the rule is reached by the streams that will be run
from a paint: pattern cells, soft masks and Type 3 glyphs. A cell is not part of the marked
content it is painted from. Whether the paint is drawn is decided where it is made, by `Hidden`
on the device, and the cell's own `/OC` sequences are its own.

**Marked content is a stack per content stream.** `BMC` and `BDC` push whether they are hidden,
`EMC` pops, and the device is hidden while any entry on the stack is. A sequence inside a hidden one
is hidden whatever it names, and a hidden one inside a visible one hides only itself.
Three cases a malformed file makes, all decided:

- An `EMC` with nothing open in its stream is reported as `OperatorOutOfPlace` on `EMC` and does
  nothing. In particular a form's `EMC` cannot end a sequence the page opened, because a form has a
  stack of its own.
- A `BDC` or `BMC` never ended is ended where its content stream ends, and reported as
  `OperatorOutOfPlace` on `BDC`. A hidden sequence left open on the page therefore cannot hide the
  annotations, and one left open in a form cannot hide what drew the form.
- An `/OC` that names nothing readable is visible and nothing is said: a name the properties do not
  hold, a property that is not a dictionary, a dictionary written in the content in place of a name,
  an `/OC` entry that is an integer or a name, or a dictionary whose `/Type` is not `/OCG` or
  `/OCMD`. Nothing throws.

**Choosing the configuration.** `PageRendering.Render(…, optionalContent)` takes a
`PageOptionalContent`, null for `/D`. `Configuration` names an entry of `/OCProperties /Configs` by
its `/Name`, the first where two have it. A named configuration is read as `/D` is, and does not
inherit from it: its own `/BaseState`, `/ON`, `/OFF` and `/Intent` apply. `/BaseState /Unchanged` starts from what
`/D` says, the groups' states at the start of a render, and then applies the configuration's own
`/ON` and `/OFF` (Table 101). A `/Configs` entry whose `/Name` is not a string is not a match. If the
properties cannot be read at all, nothing is hidden, one `InvalidValue` on `/OC` is reported and the
answer is kept. A name no entry has is
reported as `InvalidValue` on `/OC`, once, and `/D` is used. `On` and `Off` are group dictionaries,
by identity, switched over whichever configuration is chosen; a group in both is off. A group the
configuration's `/Intent` does not count has no effect on visibility whatever the caller says,
because that is a property of the group and not of its state. `/AS` auto states are not applied.

**How a membership is read.** A group counts only where one of its `/Intent`
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
may cost the whole operand budget.

**Listing the layers** (#431). `OptionalContentLayers.Read(document)` reads `/OCProperties` into a
snapshot a layer panel is built from: `Groups` (each with `Name`, `Intents` and `Dictionary`), `Default`
(`/D`) and `Configurations` (`/Configs`), each configuration with `Name`, `Creator`, `BaseState`,
`Intents`, `IsOn(group)`, the `Order` tree and `RadioButtonGroups`. **A group's identity is its
`Dictionary`**, the object `On` and `Off` compare by `ReferenceIdentity`, and a configuration's is its
`Name`, so what a panel lists goes straight back in. A configuration's states come from `OptionalContent` itself,
through `OptionalContent.For` and `IsOn`, so `/BaseState`, `/Unchanged` over `/D`, `/ON` and `/OFF` mean
here what they mean to a render; `OptionalContentLayersTests` checks that each configuration draws what
its `IsOn` says. `IsOn` ignores `/Intent`, which `OptionalContent` applies on top; compare
`Intents` of the group and the configuration for that. `/Order` follows Table 101: an array after a group is
nested under it, and an array that starts with a string is a label over the rest. Reading is tolerant and
throws on nothing: no `/OCProperties` is an empty model with a default configuration that has every group
on; an entry that is not a dictionary, is not typed `/OCG`, or is named twice is dropped; a `/Configs`
entry that is not a dictionary is dropped; an `/Order` or `/RBGroups` entry naming no listed group is
dropped. An `/Order` array met again while it is being read is ignored, and one is read 32 deep and
10,000 entries long.

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
Drawing many distinct large images is still paid for once each. What bounds that is
`DecodeLimit` (#398): each filter of an image's chain stops near the samples its size declares, as
`page-rendering.md` sets out, so a stream that inflates to gigabytes costs the image's size and no more.

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

The first of those is a choice among viewers, not a rule of the standard. T.4 does not define a
tag bit without an end-of-line code and ISO 32000-1 7.4.6 does not either; Ghostscript reads none,
and MuPDF and PDFium read one before every row. Data written one way and declared the other
decodes wrongly in one of the two camps. Truncated data has no agreed drawing either: MuPDF and
PDFium draw the rows it does not hold black, Ghostscript leaves them unpainted, and this decoder
draws them white (and reports it).

**The output is sized by the data and the image, never by `/Columns` alone.** Rows are counted in
`long`: `/Columns` up to `int.MaxValue` is a row of 256 MiB, and every sum of run lengths is a
`long` too, so a column count above 2^30 cannot wrap into a negative changing element. Where
`/Rows` is absent the rows are counted in a first pass and the output allocated once at its final
size, so data that holds no rows allocates none and a 256 MiB result costs 256 MiB, not the
doubling's 504. Viewers decode by `/Columns` and not by `/Width`, so a mismatch is drawn as they
draw it, but `PageImages` refuses an image whose `/Columns` is more than twice its `/Width` and
over 65536, which is what stops 300 one pixel images naming 2 billion columns from allocating
76.8 GiB between them.

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
| #373 TrueType | `EmbeddedTrueTypeGlyphSource`, for `/FontFile2`. Built: see the next section | The first branch of `BuiltInGlyphSources.Select` |
| #374 standard 14 | A class derived from `PageGlyphSource` that reads `RequiredSeams.FontResolver` in `GetOutline` and nowhere else, and answers AFM widths from `TryGetAdvance`, which is asked for invisible text too and so reads no seam. Its outlines come from the resolved face's `OpenTypeFontface.Glyphs`, through `TrueTypeOutlinePaths.ToPagePath`, and a code's glyph through `GlyphNames` and `TrueTypeGlyphs.CharacterMap`, all as the next section sets out | The last branch of `BuiltInGlyphSources.Select` |
| #375 images | The bodies of `PageImages.ReadXObject` and `ReadInline`, which answer a `PageImage` or report and answer null. Done: see Images | `PageImages.cs` |
| #376 Skia | `SkiaPageDevice`, derived from `PageDevice`; done | Its own package: `PageView.Of`, then `PageRendering.Render` |
| #383 groups | A form recorded apart and laid down once | `ContentInterpreter.Record`, which a tiling cell already uses, and a `virtual` member for the composition |

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
TrueTypeCharacterMap CharacterMap(int platform, int encoding);   // formats 0, 4, 6 and 12, or null; the same instance each time
long HeldBytes { get; }                               // the most this can come to hold; it does not change
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
  the end of the stream, or mapped to a glyph number the font has not, has no glyph.
- **Simple fonts.** A font with an `/Encoding` that is not symbolic is tried by name first, and a
  symbolic font, or a nonsymbolic one with no `/Encoding` at all, by code first. A font that names
  `/WinAnsiEncoding` or `/MacRomanEncoding` is tried by name first whatever its flags say, as
  ISO 32000-1 9.6.6.4 has it. The other way is tried after the first finds nothing, because real
  files often say a font is symbolic when it is not, or the reverse. A code that `/Differences`
  names `.notdef` has no glyph, and is not looked up another way.
  - *By name*: the code's glyph name from `/Differences`, or else its character from the base
    encoding, which is `/StandardEncoding` where a nonsymbolic font's `/Encoding` names none. A name
    becomes a character through `GlyphNames` (the Adobe Glyph List, `uniXXXX` and `uXXXX`). Then the
    (3,1) `cmap`, or (3,10), (0,3) or (0,4); then the character's MacRomanEncoding code in the (1,0)
    `cmap`; and only then the `post` table's names, the name `/Differences` gives, or for a code of
    the base encoding each name its character goes by. That is 9.6.6.4's order, and MuPDF's: `post`
    is for a name the `cmap` cannot map.
  - *By code*: the code as it is, in each `cmap`. For a symbolic font, the (3,0) `cmap` at the code,
    then at the code plus 0xF000, 0xF100 and 0xF200, then the (1,0) `cmap`, then the Unicode one. For
    a nonsymbolic font, the Unicode `cmap`, then the (1,0) one, then the (3,0) one.
  - **Where the name gave a character the font has no glyph for, the code is looked up in the (3,0)
    `cmap` alone.** In the Unicode or (1,0) `cmap` the code means another character: WinAnsi's 0x80
    is the euro, and a font without one has Adieresis at Mac 0x80. pdf.js and MuPDF draw nothing
    there, and so does this, with a `MissingGlyph`.
  - A font program with no `cmap` table at all is taken to number its glyphs by the codes. One whose
    `cmap` has only subtables this does not read, such as format 2, is not.
  - A subtable that is malformed for a code is passed over for the next. Only where none has a
    glyph is the fault the reason reported. A format 12 subtable whose groups are out of order is
    malformed for every code, and so is a format 4 subtable of more than 256 segments that are.

**A nonsymbolic font with no `/Encoding` is read by code, not in StandardEncoding.** ISO 32000-1
9.6.6.1 gives such a font StandardEncoding, which makes 0x27 quoteright and 0xE9 Oslash. pdf.js and
MuPDF look the code up in the font's `cmap` as it is, and draw the straight quote and e acute; users
judge a rendering against those viewers, so this follows them. An `/Encoding` dictionary with
`/Differences` and no `/BaseEncoding` is still StandardEncoding, as both viewers read it.

**Glyph 0, `.notdef`, is drawn only where a file asks for it.** A CID that `/CIDToGIDMap` or
`/Identity` sends to glyph 0 draws it, and so does code 0 of a simple font that nothing else maps,
where glyph 0 has contours. pdf.js does both, because some files keep a real glyph there (its issue
17333 is a check box). Any other code that comes to no glyph draws nothing and is reported.

`GlyphNames` reads `Fonts/GlyphNameTables.g.cs`, which `tools/GlyphNameTableGenerator` writes from
the checked-in Adobe Glyph List. It is the core's one map from glyph names to characters.

### What is kept, and the bound on it

**The rule: whatever page rendering reads from a font and keeps is kept in the document's
`RenderFontCache`, under the PDF object it was read from, and charged in bytes to a budget for the
whole document.** A `RenderFont` and a glyph source keep references to the document's own objects
and a few numbers, and nothing that grows.

Three rounds of budgets, one for each cache that was known of, each left a structure that grew with
the number of font dictionaries and was charged to nothing: outlines for each code, then outlines
for each font, then `/W2` read into each font. A font dictionary costs about 150 bytes of file, so
whatever a font dictionary holds is multiplied by thousands in a file of kilobytes. The rule
removes the place such a structure could be, and `FontMemoryTests.AFontAndItsGlyphSourceKeepNothingThatGrows`
fails the build when a field of any other type is added to those classes.

The rule has two halves, and each does a different job:

- **Kept under the object it was read from.** Font dictionaries that share a `/FontFile2` stream, a
  descendant, a `/W` or `/W2` array or a `/CIDToGIDMap` stream share one copy of what was read from
  it, and a font program shared by 64 font dictionaries is decoded once.
- **Charged to the document's budget.** Sharing alone is no bound: a `/W` array of three numbers is
  20 bytes of file as an object of its own, and reads to half a megabyte, so a file of fonts that
  share nothing would still hold what its font dictionaries multiply. Everything read is charged,
  and what does not fit is let go of.

There are two budgets, because what is kept is of two kinds:

- **Data that can be read again, 144 MiB**: font programs, `/CIDToGIDMap` streams, widths, vertical
  metrics and a simple font's glyph choices. Past the budget the data least recently used is let
  go of, at any time, and read again if it is asked for. That changes nothing a device has been
  handed. It is room for two font programs of the largest size read, their names, and the widths
  and maps that go with them.
- **Glyph outlines, 18 MiB**: what 1,048,576 points cost, at sixteen bytes and a verb each. Each
  outline is charged 96 bytes for its entry as well as its points, so an empty glyph is not free.
  None is let go of during a page. A glyph past the budget is decoded each time it is drawn and
  handed over unshared, so a device does not keep it either: it costs time, about 7 ms for a glyph
  of 65,535 points, and not memory. Between pages, where outlines are over half the budget, those
  of the font programs least recently drawn from are forgotten.

They are not one budget. Outlines cannot be let go of during a page, so a page that filled a shared
budget with them would leave no room to hold a font program from one glyph to the next.

**Outlines are kept by glyph of the font program, not by code or by font dictionary.** A source
numbers the glyph of a code through the internal `PageGlyphSource.GlyphOf` and names what the glyph
is a glyph of through `OutlineOwner`, which for an embedded font is its `/FontFile2` stream. So the
CIDs a `/CIDToGIDMap` sends to one glyph share one path, and so do the font dictionaries over one
font program. A source of the caller's own is its own owner and numbers each code as its own glyph.

**A font program of more than 64 MiB is not read, and not decoded past that.** A TrueType font has at most 65,535 glyphs.
The largest there are, CJK fonts embedded whole, run to 20 or 30 MB, and at the far end to about
50; the limit allows a kilobyte for every glyph a font can have. A stream that decodes to more is
reported as `MissingGlyph`, with the limit, against each glyph shown in it. The limit is under half
the data budget so that two of the largest can be held together.

**A page reads at most 1 GiB of font program.** Letting go of a font program and reading it again
costs its whole decoding, so a page that selects three fonts of 63 MiB in turn decodes 63 MiB at
each selection, a fifth of a second for 15 bytes of content. Past the allowance a font program that would
have to be read is not, and its glyphs are reported with the reason. Fonts still held go on
drawing. The next page has an allowance of its own, and nothing is kept of the refusal.

**Widths are a table, not a dictionary.** `/Widths`, `/W` and `/W2` are each read into a
`WidthTable`, one array of doubles as long as the highest code given a number: at most 0.5 MiB for
a `/W` and 1.5 MiB for a `/W2`, however the array is written. `FontWidths` and `VerticalMetrics`
hold the default and the array, and ask the cache for the table at each code.

#### Every field, and what covers it

*(a)* means of a fixed size and held once for the object of the file it stands for. *(b)* means
charged to one of the two budgets.

| held by | field | what it is | covered by |
|---|---|---|---|
| `RenderFontCache` | `_fonts` | one `RenderFont` for each font dictionary selected | (a): each is of a fixed size, smaller than the dictionary it stands for. Past 4096 they are all forgotten as a page begins |
| | `_data`, `_recent` | the data kept and its order of use | (b): each entry charged what it holds and 192 bytes |
| | `_outlines` | the outlines kept, by owner and glyph | (b): each outline charged its points and 96 bytes, each owner 192 bytes |
| | `_dataBytes`, `_outlineBytes`, `_programBytesRead`, `_clock` | four numbers | fixed |
| `RenderFont` | `_cache`, `Source` | the cache, and the glyph source | references |
| | `_widths`, `_vertical` | `FontWidths` and `VerticalMetrics`, below | (a) |
| | `IsComposite`, `UnsupportedReason`, `EncodingNote` | a flag and two messages | fixed |
| `FontWidths` | `_simpleFont`, `_w` | the font dictionary or the `/W` array, the document's own | references |
| | `_defaultWidth`, `_stated` | a number and a flag | fixed |
| `VerticalMetrics` | `_w2`, `_defaultY`, `_defaultAdvance` | the `/W2` array and two numbers | references, fixed |
| `EmbeddedTrueTypeGlyphSource` | `_cache`, `_font`, `_descendant`, `_fontFile` | the cache and the document's own dictionaries | references |
| data: `FontProgram` | `Glyphs`, `Unreadable` | the decoded program, or why it cannot be read | (b), under the `/FontFile2` stream |
| data: `TrueTypeGlyphs` | `_data` | the font program's bytes | (b): `HeldBytes` |
| | `_tables` | the table directory | (b): 96 bytes a table in `HeldBytes` |
| | `_postNames` | the `post` names, read when a glyph is asked for by name | (b): counted in `HeldBytes` before they are read, as no more names than glyphs and no more characters than `post` has bytes |
| | `_characterMaps` | one `TrueTypeCharacterMap` for each of the seven subtables asked for | (b): counted in `HeldBytes`. A subtable holds four numbers and the program's own bytes |
| data: `CidToGidMap` | `_map` | the first 131,072 bytes of the stream, which is all a CID can reach | (b), under the stream |
| data: `WidthTable` | `_values` | the numbers of `/Widths`, `/W` or `/W2` | (b), under the font dictionary or the array |
| data: `SimpleFontGlyphs` | `_differences`, `_chosen`, the flags | 256 names, 256 glyph numbers | (b), under the font dictionary. It holds no font program: one is handed to it for each choice |

**So no structure that grows with the number of font dictionaries is uncharged.** What a font
dictionary adds to memory is its `RenderFont`, its source, its `FontWidths` and its
`VerticalMetrics`: about 300 bytes, all fields above, none a collection. Everything else is an
entry of `_data` or `_outlines`.

What the rule does not cover, because it is not kept for a font:

- **The decoding itself, which is capped where it is done.** `/FontFile2` is decoded under a
  `DecodeLimit` (#398) of 64 MiB and `/CIDToGIDMap` under one of 131,072 bytes, which is all a CID
  can reach, in `EmbeddedTrueTypeGlyphSource.TryRead`. A font program whose filters had more to
  give is declined whole and reported: no part of it is kept. A stream that inflates to 512 MiB
  or to 1900 MiB from a file of one or two megabytes peaks at 192 MiB while it is decoded, three
  times the limit, and holds nothing after. A stream with no filter is the file's own bytes,
  which the document already holds, and is measured as it is.
- **What one call uses and lets go of**: a table being read, a glyph being decoded, each bounded by
  the 65,536 CIDs or points there can be.
- **A caller's own `PageGlyphSource`**, which holds what its author makes it hold. Its outlines are
  charged like any other.

#### What was weighed

For outlines, **a total budget alone**, past which outlines are unshared, holds memory flat, but
once early pages fill it every later page draws every glyph unshared, decoding each one each time
it is drawn, for the rest of the document. **Eviction alone** keeps sharing, but an outline let go
of during a page is decoded again as a new shared path, and a device keeps one path of its own for
each shared one it is handed until the render ends: a page that cycled fonts past the budget would
grow the device without bound, and the Skia device keeps an `SKPath` for each. So each is used
where the other fails: the total within a page, where the device's cache lives, and eviction
between pages, where no device holds a path. The other way, to report a glyph past the budget and
skip it, would leave text out of a real font whose glyphs are many and large.

For a format 12 `cmap` subtable whose groups are out of order, **refusing it** against **sorting
the groups once**. Sorting builds twelve bytes for each group that would have to be charged and
kept in step with the program, for a subtable the format forbids and FreeType refuses. So it is
refused: the next subtable is tried, and where none has the glyph the reason is reported. A format
4 subtable out of order is one FreeType does read, in turn, so it is read in turn here where it
has at most 256 segments, and refused past that. Both formats are searched by halves where they
are in order, and whether they are is found once for a font program, in one pass.

**A text object clips to at most 4,194,304 points of glyph outline.** A glyph shown in a clip mode
is copied into the clip in device space, each time it is shown, so sharing the outline does not
bound the clip: 60,000 shows of one glyph of 65,535 points would be four billion points. Past the
limit a glyph is left out of the clip, and `LimitReached` is reported once for the text object.

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

**Flate data with a missing or wrong Adler-32 is not drawn.** The inflater throws on the
checksum after it has inflated everything, and the image is reported and dropped whole. Drawing
what was inflated is #409.
`/Interpolate` is not applied.

**Moving the extraction walkers onto this interpreter** stays the follow-up `page-rendering.md` makes
it (item 18). `FormVisits` here repeats the limits of the text extractor's own until then.
