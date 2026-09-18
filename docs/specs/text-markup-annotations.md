# Spec — text markup annotations, issue #342

[ststeiger/PdfPinata#342](https://github.com/ststeiger/PdfSharpCore/issues/342) asks for text
highlight annotations. What follows is the design as built, on `feat/text-markup-annotations`.

| item | what | status |
|---|---|---|
| 1 | `PdfHighlightAnnotation` and its three siblings do not exist | done |
| 2 | A text markup annotation without `/QuadPoints` draws nothing | done, with item 1 |
| 3 | `PdfHelper.Rasterize` renders an unpainted transparency group as black | done, turned up on the way |

---

## The defect

Two of them, and fixing only the first leaves the reporter where they started.

### Item 1 — the type does not exist

`PdfPinata/Pdf.Annotations/` held `PdfLinkAnnotation`, `PdfTextAnnotation`,
`PdfRubberStampAnnotation`, `PdfWidgetAnnotation`, `PdfFileAttachmentAnnotation` and the `internal`
`PdfGenericAnnotation`. The code in the issue does not compile.

Nor was there a workaround. `PdfGenericAnnotation` is `internal` and `PdfAnnotation` is `abstract`
with no public way to set `/Subtype`, so the only route was a subclass written in user code —
which is what the StackOverflow answer the issue links to amounts to. `PdfFormXObject`'s
constructors are `internal` too, so an appearance stream could not be built from outside either.

`docs/PdfPinata/samples/Annotations.md` had advertised the whole family for years, with the note
"If you need one of them, feel encouraged to implement it. It is quite easy."

### Item 2 — a subtype wrapper alone still draws nothing

This is the part that is not quite as easy as advertised, and the reason the reporter could not get
the StackOverflow answer to work.

PDF 32000-1 section 12.5.6.10, Table 179 makes `/QuadPoints` **required** for `/Highlight`,
`/Underline`, `/StrikeOut` and `/Squiggly`. An annotation of the right subtype carrying only
`/Rect` is drawn by nothing.

Confirmed by direct probe — three variants of one `/Highlight` over the string `Hello world!`,
rasterized through Ghostscript at 150 dpi and counted for yellow pixels:

| variant | dictionary | yellow pixels |
|---|---|---|
| a | `/Subtype /Highlight` + `/Rect` + `/C` — what a subtype wrapper produces | **0** |
| b | the same, plus `/QuadPoints` | 2257 |
| c | the same, plus an `/AP` appearance stream with `/BM /Multiply` | 2155 |

Row (c) is there because the viewers that synthesize an appearance from `/QuadPoints` — Ghostscript
and Acrobat among them — are not all of them. It is also the only way `Opacity` is honoured at all:
`/CA` on the annotation is ignored once an `/AP` is present, so the value has to be carried into the
appearance's graphics state instead.

---

## Design

`PdfTextMarkupAnnotation`, public and abstract, carries what the four share; each subtype is its
`/Subtype` name and how it draws one quadrilateral.

### Quadrilaterals, and the rectangle

A run of text is not a rectangle — it wraps, and one line of it can be a different height from the
line above — so the marked-up region is a list of quadrilaterals. `AddQuad`, `ClearQuads` and
`Quads` are the API; `/Rect` is recomputed as the box enclosing them, which is what the
specification asks of it and what a viewer regenerating the appearance assumes.

**An annotation with no quads of its own is marked up over the one its `/Rect` describes.** Without
this, the code in the issue — which sets `Rectangle` and never mentions a quadrilateral — would
still silently draw nothing, which is the entire complaint. Adding a quad takes over.

The four corners are written upper-left, upper-right, lower-left, lower-right. The specification's
prose asks for "the four vertices in counterclockwise order", which would put the lower two the
other way round, but no producer writes them that way and viewers read this order instead.

### The appearance stream

`/AP /N` is a form XObject whose `/Resources` hold

```
/ExtGState << /GS0 << /Type /ExtGState /BM /Multiply /ca <opacity> /CA <opacity> >> >>
```

with `/GS0 gs` first in the content, then the colour, then one `DrawQuad` per quadrilateral. Multiply
is what keeps a highlight from painting over the text it marks.

| subtype | drawn as |
|---|---|
| `/Highlight` | the quadrilateral filled |
| `/Underline` | a bar along the foot, a fourteenth of the height thick, that far above the bottom |
| `/StrikeOut` | the same bar at three sevenths of the height |
| `/Squiggly` | a zigzag stroked along the foot, clipped to the end of the quadrilateral |

The thickness is `TextMarkupGeometry.RuleThickness`, floored at 0.25pt so a rule over a very small
quadrilateral does not thin to a hairline, which is a different width on screen from in print.

### When the appearance is built

`PdfObject.Owner` is `null` until `PdfAnnotations.Add` sets `Document` (`PdfObject.cs:163`), and an
appearance stream is an object in the document — so it cannot be made in the constructor.

There is no save-time hook to lean on instead: `PdfPage.PrepareForSave` (`PdfPage.cs:806`) does not
walk a page's annotations, which is why `PdfTextField.PrepareForSave` (`PdfTextField.cs:274`) never
fires either. That is left alone here rather than changed, because making it fire would start
generating appearances for existing widget annotations that do without them today.

So the appearance is built eagerly, through two hooks added to `PdfAnnotation`:

- `OnAddedToPage`, called by `PdfAnnotations.Add`, which is the first moment there is an owner;
- `OnAppearanceInvalidated`, called by the `Rectangle`, `Color` and `Opacity` setters — the
  properties an appearance is drawn from, and not the ones that merely describe it.

Both are `internal virtual` and no-ops on the base, so nothing changes for the other annotations.
`RebuildAppearance` does nothing while `Owner` is null, so properties set before the annotation is
added and properties set after it both end up in the appearance.

### Traps

- **The form object is made once and rewritten in place.** Making a new one per change would leave
  the stream it replaces behind in the document — twenty colour changes, twenty dead streams. This
  also means `CreateStream` is only ever called once; it throws on a dictionary that already has a
  stream, so subsequent writes set `Stream.Value`.
- **Every number goes through `PdfEncoders.Format`** (`Pdf.Internal/PdfEncoders.cs:592`), which
  exists so that a German decimal separator never reaches Acrobat.
- `UpdateRectangle` writes through `Elements.SetRectangle` rather than the `Rectangle` property, so
  that recomputing the box does not re-enter the appearance build that asked for it.
- The subtype is set in each concrete constructor, as `PdfRubberStampAnnotation` does, rather than
  read from an abstract member the base constructor calls.

---

## Item 3 — turned up on the way

`PdfHelper.Rasterize` did `img.Alpha(AlphaOption.Deactivate)`, which drops the alpha channel and
leaves whatever colour was underneath rather than compositing. Ghostscript renders a page carrying
an annotation drawn under a blend mode into a transparency group, and every pixel of that group that
was never painted then comes out **black** — a solid block the size of the annotation rectangle.

This cost an hour of chasing a defect that was in the harness rather than in the library: the
appearance streams were right all along. Now `BackgroundColor` then `Alpha(AlphaOption.Remove)`,
which composites. No existing golden-image test changes — nothing they render was transparent.

---

## Verification

`PdfPinata.Test/Annotations/TextMarkupAnnotationTests.cs`, 21 tests over the dictionary:

- each subtype names itself; a quad is written as the four corners in the order above;
- `/Rect` is the box around every quad, over one and over several; quads read back as given;
- **an annotation given only a rectangle is still drawn** — the issue's own code, asserted;
- `/AP /N` is a form whose `/BBox` is the rectangle;
- colour set before the annotation is added reaches the appearance, and colour set after it does
  too — the owner-is-null ordering trap in both directions;
- opacity reaches `/ca`, `/CA` and sits beside `/BM /Multiply`;
- twenty colour changes leave the document's object count unmoved and the same form in place;
- an annotation never added to a page has no `/AP` at all;
- the underline, strike out and squiggly geometry; every quad drawn;
- the whole thing survives a save and a read, with the quads added before the annotation is put on a
  page and with them added after — the array is built without an owning document in the first case.

`PdfPinata.Test/Annotations/TextMarkupRenderingTests.cs`, 8 golden-image tests in the rasterizing
collection, which count coloured pixels rather than compare against a reference image — the question
being asked is "is anything drawn at all", and the answer before this change was **0**:

- a highlight washes the line, and **does not paint over it** — the count of dark pixels stays within
  60 of the same page unmarked, which a plain fill instead of Multiply would not manage;
- underline, strike out and squiggly each make their mark;
- two quads on one annotation give two bands, at a little over twice one band — a little over,
  because the first band is over the text and loses the pixels the glyphs take, while the second is
  over empty page;
- an annotation given only a rectangle is drawn;
- at a quarter opacity the wash is diluted towards white and no longer answers as yellow, while at
  full opacity it does.

Whole suite green on net8.0 and net10.0, 276 passed on each, one pre-existing skip
(`CanCreatePdfOver2gb`). Solution builds with 0 warnings.

## Cost

1,250 lines of new files: 761 of library across six, 489 of tests. The base class is half the
library total; the four subtypes are about 30 lines of substance each. Plus 55 lines of changes to
`PdfAnnotation`, `PdfAnnotations`, `PdfHelper` and the sample.

## Not in scope

- **Typed read-back of imported markup annotations.** `PdfAnnotations[int]`
  (`PdfAnnotations.cs:103`) still hands back `PdfGenericAnnotation` for everything read from an
  existing file, so a `/Highlight` that was read cannot be downcast to work with. Worth doing; it
  touches the import path and is a separate change.
- **Finding the text to mark up in an existing PDF**, which the StackOverflow question the issue
  links to is really about. PdfPinata has no text-position extraction; the caller supplies the
  quadrilaterals.
- `PdfLineAnnotation`, `PdfSquareAnnotation`, `PdfCircleAnnotation`, and the rest of the family the
  sample still invites contributions of.
