# Spec — five interactive-layer features ported from PDFsharp 7.0

Tied to no single upstream issue. PDFsharp 7.0-preview (empira) added a set of features this fork
had no counterpart for. Five of them were ported together, as ideas rather than code: PdfPinata
descends from PDFsharp 1.5 through PdfSharpCore and its object model, naming and conventions differ
too much for upstream's classes to drop in.

| # | feature | upstream | here | status |
|---|---|---|---|---|
| 6 | typed annotations on read | `PdfAnnotation.CreateAnnotation` | `PdfAnnotation.FromDictionary` (internal), used by `PdfAnnotations[i]` | **done** |
| 7 | missing annotation types and a markup base | `PdfMarkupAnnotation`, `PdfInk/Poly/Popup/Caret/RedactAnnotation`, `PdfBorderStyle`, `PdfBorderEffect` | `PdfMarkupAnnotation`, `PdfInkAnnotation`, `PdfPolygonAnnotation`, `PdfPolyLineAnnotation`, `PdfPopupAnnotation`, `PdfCaretAnnotation`, `PdfRedactAnnotation` | **done**, border objects left out |
| 8 | XMP metadata strategy | `MetadataManager.Strategy`, `DocumentMetadataStrategy` | `PdfDocumentOptions.MetadataStrategy`, `PdfMetadataStrategy` | **done**, `UserGenerated` left out |
| 9 | signature field lock and seed value | `PdfFormSignatureFieldLock`, `PdfFormSignatureFieldSeedValue` | `PdfSignatureField.Lock` / `.SeedValue`, `PdfSignatureOptions.LockAction` | **done**, seed values not honoured |
| 10 | page events | `PageEvents`, `PageGraphicsEvents` | `PdfDocument.PageAdded` / `PageRemoved` / `PageGraphicsCreated` | **done**, per-operation drawing events left out |

The numbers follow the list the work was planned from; items 1 to 5 of that list were ported
separately.

---

## 6 — an annotation read from a file is its own class

`PdfAnnotations[i]` used to wrap every annotation it read in `PdfGenericAnnotation`, so reading a
line's endpoints meant reading `/L` by hand. It now goes through `PdfAnnotation.FromDictionary`,
which switches on `/Subtype` over the nineteen subtypes with a class and falls back to the generic
one.

**Wrapping writes nothing.** Every class gained a constructor over a read dictionary that takes the
dictionary over - entries, stream, cross-reference slot - and calls none of the initialisation a
new annotation gets. That is load-bearing for the self-drawing subtypes: their ordinary
constructors write defaults and their `OnAppearanceInvalidated` redraws, and either, run over a read
annotation, would replace the appearance the file carried with one this library made up.
`ReadingAnAnnotationThatDrawsItselfLeavesItsAppearanceAlone` compares the `/AP /N` bytes across a
second save for ten subtypes, and `ASquareReadFromAFileStillPaintsTheAppearanceTheFileCarried`
rasterizes one whose appearance is a green cross - nothing this library would draw for a square.
Changing a property the appearance is drawn from still redraws it, as it would for one made here.

Three classes needed more than the constructor:

- **`PdfSquareCircleAnnotation` kept its interior and border width in fields**, so a read one would
  have reported the defaults. Both now read `/IC` and `/BS`, as `PdfLineAnnotation` always did, so
  the difference CLAUDE.md warned about is gone.
- **`PdfFreeTextAnnotation` reads its ink and size back out of `/DA`.** The face cannot be recovered:
  `/DA` names a form resource, not a file, so a read one redrawn uses the default face at the size
  the file asked for.
- **`PdfLinkAnnotation` gave a link with no `/BS` a zero-width border at write time** - a hack for old
  Adobe readers. A link read from a file is left with what it had, because no `/BS` is a one-point
  border by ISO 32000-1 and adding one changed how the link looked.

**A widget merged with its field** is wrapped as `PdfWidgetAnnotation` and shares its entries with
any `PdfAcroField` made over the same dictionary, as the generic wrapper did before. A value set
through the field is still in the file, which `AMergedFieldAndWidgetCanStillBeFilledInAfterItsWidgetIsRead`
pins.

### A defect it exposed: re-typing forgot a change

Re-typing an object - the copy constructor `PdfObject(PdfObject)`, which every typed wrapper uses -
made a new object whose `IsDirty` was false whatever the old one's was. `SaveIncremental` appends
only what says it changed, so a change made through one wrapper and then re-wrapped by the next
typed read was silently left out of the appended revision. It surfaced in item 9: `PdfSigner` marks
the AcroForm changed, and a second `Catalog.AcroForm` read re-wrapped it, dropping the whole form -
and the signature - from the signed revision. The copy constructor now carries the flag over.

## 7 — six new annotation classes and a markup base

| class | draws itself | geometry |
|---|---|---|
| `PdfInkAnnotation` | yes, round caps and joins | `AddStroke(params XPoint[])`, `Strokes`; `/Rect` computed |
| `PdfPolygonAnnotation` | yes, filled with `Interior` | `SetVertices`, `Vertices`; `/Rect` computed |
| `PdfPolyLineAnnotation` | yes, with `StartEnding` / `EndEnding` | as polygon |
| `PdfCaretAnnotation` | yes, a chevron filling `/Rect` | `/Rect` is the geometry |
| `PdfRedactAnnotation` | yes, an outline per region | `AddQuad`, `Quads`, else `/Rect` |
| `PdfPopupAnnotation` | no - it is the reader's window | `/Rect` is where the window goes |

All five that draw follow the interactive layer's rules: through `XForm` and `XGraphics`, redrawn on
every change, the appearance removed when asked to draw nothing, and points in default user space.
Their tests count pixels. The polyline's endings are `PdfLineAnnotation`'s, moved unchanged into
`LineEndings`; the redaction's `/QuadPoints` handling is the text markup classes', moved into
`QuadPoints`. `PdfAnnotationTransformer` now moves a redaction's quadrilaterals and a caret's `/RD`
on page resize.

**A redaction marks; it removes nothing.** Applying one - deleting what is under it - is a content
rewrite this library does not do, and the class says so before anything else, because a caller who
assumes otherwise publishes what they meant to hide. A caret's `/Sy /P` is recorded but drawn as a
caret: a pilcrow is text, and drawing it would need a font resolver for a triangle.

**`PdfMarkupAnnotation`** now sits between `PdfAnnotation` and every markup subtype in ISO 32000-1
Table 170 - text, free text, line, square, circle, polygon, polyline, the four text markups, stamp,
caret, ink, file attachment and redaction - and carries what none had: `Popup` (written both ways,
with the pop-up's `/Parent`), `InReplyTo`, `ReplyType`, `RichText` and `Intent`. `/T`, `/Subj`,
`/CreationDate` and `/CA` are markup entries too but stay on `PdfAnnotation`: moving a public
property down a class breaks callers, while inserting a class above an existing one does not.

**Left out:** `PdfBorderStyle` and `PdfBorderEffect` objects. Three classes' `BorderWidth` setters
replace `/BS` wholesale, and every self-drawn appearance draws a solid border; a style object
offering dashed, bevelled or inset borders would be a promise the appearances do not keep. Doing it
properly means teaching each appearance the styles - worth doing, and not a port. `/BE` stays a key
on the square, circle and poly annotations, as it was.

## 8 — `PdfMetadataStrategy`

| member | no claim | with a PDF/A or PDF/UA claim |
|---|---|---|
| `KeepExisting` (default) | no packet written; a read packet left as it was | fresh packet, as always |
| `AutoGenerate` | fresh packet from `/Info` every save, replacing any | fresh packet |
| `NoMetadata` | packet removed | refused, at `ClaimConformance` and at save |

The default is today's behaviour, so no existing document changes and the veraPDF corpus is
untouched. `WriteXmpMetadata` is now `AutoGenerate`'s older spelling rather than a second switch.
The packet `AutoGenerate` writes goes through the same writer, `CustomizeMetadata` and
`AddMetadataContributor` a claim uses.

**Left out:** upstream's `UserGenerated`, which raises an event for user code to build the packet.
Here that is what the two hooks already do under `AutoGenerate`. `AutoGenerate` also does not
preserve schemas a read packet had beyond what `/Info` and the hooks supply; a caller who needs one
kept adds it through a contributor.

## 9 — `/Lock` and `/SV`

`PdfSignatureFieldLock` (Table 233), `PdfSignatureSeedValue` (Table 234) and
`PdfCertificateSeedValue` (Table 235) model the dictionaries, and `PdfSignatureField.Lock` and
`.SeedValue` hang them off a field as the indirect objects ISO 32000-1 requires. Names are read and
written with their solidus; certificates are DER bytes; policy OIDs are dotted strings.

**The signer honours a lock it is asked for.** `PdfSignatureOptions.LockAction` and `LockFields`
make `PdfSigner` write `/Lock` on the field it creates, a `/FieldMDP` signature reference beside any
`/DocMDP` one, and set every covered field read-only in the signed revision - never the signature's
own field. `ALockedSignatureStillVerifies` confirms the signature still covers the whole file.

**Left out:** honouring a seed value, and honouring a lock an *author* placed on an empty field.
Both need signing into an existing field, which `digital-signatures.md` already lists as not done:
`PdfSigner` always creates its own field, so it is never the application those dictionaries speak
to. Also not done: `PdfTextField.Text` does not check `ReadOnly` (the `Value` setter does), so a
locked text field can still be written through `Text` - an older gap this work found and did not
widen.

## 10 — page events

`PageAdded`, `PageRemoved` and `PageGraphicsCreated` on `PdfDocument`. Each costs one null check when
nobody listens, which matters because PinataLayout draws every page through `FromPdfPage`.

- `PageAdded` fires after the page is in the tree and the count is right; a range is reported page
  by page after all of it is in. A page from `AddPage` still has the default size when it fires -
  PinataLayout sets the size afterwards - so drawing belongs on `PageGraphicsCreated`.
- `PageGraphicsCreated` fires from every `FromPdfPage` overload after the surface exists and before
  the caller has it. The handler draws inside `Save`/`Restore`, so state it leaves set does not move
  the caller's drawing; a handler that throws has the surface disposed so the page stays drawable.
- Re-entrancy is not guarded: a handler adding a page is raised again inside itself, and a handler
  asking for a second surface for the same page gets the existing "already exists" exception.
- `MovePage` raises nothing.

**Tagged output is the caller's.** Content a handler draws is outside any marked sequence, which
PDF/UA forbids. A header or watermark is decoration and belongs inside `XGraphics.BeginArtifact()`;
the library cannot tell a watermark from a heading, so it does not wrap it.
`ALaidOutDocumentRaisesOneSurfacePerPage` draws that way through a tagged PinataLayout render.

**Left out:** upstream's per-operation `PageGraphicsAction` events, raised on every draw call. They
would put a check on every drawing operation for a use nobody here has asked for.

## Tests

`Annotations/TypedAnnotationReadingTests`, `Annotations/MarkupAnnotationTypesTests`,
`IO/MetadataStrategyTests`, `Forms/SignatureFieldLockTests`, `IO/PageEventsTests`.
