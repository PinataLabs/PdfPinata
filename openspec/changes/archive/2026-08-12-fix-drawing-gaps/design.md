## Context

Four gaps, found by drawing pages and looking at them rather than by any test. Three fail silently.
They are unrelated in the code and related in kind: each is a place where the API's name promises
something the implementation does not do, and says nothing about it.

What already exists matters a great deal to the cost of each, and the four are not equal:

| gap | what is already there | size |
|---|---|---|
| Gradient alpha | `PdfSoftMask`, `PdfFormXObject`, `PdfTransparencyGroupAttributes`, `PdfExtGState.SoftMask` — all present, none reached from the gradient path | small |
| `AddString` | Nothing. No glyph outline decoding anywhere in the tree | large |
| `BaseLine` | Two `if` statements to delete and the placement arithmetic to check | very small |
| Table heading | One method, `CalcLastHeaderRow`, 12 lines | very small |

Two constraints shape everything below.

**The core package carries no font or imaging dependency.** That is the property the whole backend
split exists to protect, and `netstandard2.1` is still a target for Unity. Anything that needs to
rasterize or interpret a font belongs behind a seam, not in `PdfPinata`.

**Opaque output must not change.** The library has produced gradients for years; a change that
rewrites every gradient's content stream would be a change nobody can review by reading a diff.

## Goals / Non-Goals

**Goals:**

- Gradients honour alpha, through the mechanism every other PDF producer uses.
- `AddString` produces real geometry, for both TrueType and CFF outlines.
- The two silent failures and the one surprising exception stop being surprising.
- Nothing that works today produces different bytes tomorrow, except where a document was already
  wrong.

**Non-Goals:**

- **Multi-stop gradients.** `XLinearGradientBrush` takes exactly two colours. A stitching function
  (`FunctionType 3`) would be needed for more, and there is no API to reach it.
- **`AddPie`, `AddClosedCurve`, `AddPath`.** Unimplemented in the same file as `AddString`, and
  nothing to do with it. Separate change.
- **Kerning and OpenType features.** Out of scope here as they are everywhere else — see
  `pdfkit-text-parity.md`.
- **A general transparency-group API.** The soft mask built here is internal to the gradient path.
  Exposing transparency groups as a public concept is a much larger design.
- **Making MigraDoc's heading rule more permissive.** A repeated heading must be at the top of a
  table; that is the PDF-shaped constraint and not a limitation to lift.

## Decisions

### 1. Gradient alpha: a luminosity soft mask, following PDFKit

The mechanism is settled by the PDF specification and by what readers actually implement. PDFKit's
`lib/gradient.js` is the clearest reference implementation and does exactly this:

```text
  ExtGState                          the gradient is painted with /Gs1 gs applied
    /SMask << /Type /Mask
              /S /Luminosity         luminosity, not /Alpha
              /G  <form>          ┐
           >>                     │
                                  │
  Form XObject  ◄─────────────────┘
    /Group << /S /Transparency
              /CS /DeviceGray >>     grey: luminance IS the alpha
    /BBox  [ the painted area ]
    stream: /Pattern cs /Sh1 scn     a second shading, same geometry,
            <rect> re f              whose C0/C1 are the alpha values
```

The colour shading is unchanged. A second shading is built with the identical `/Coords` and
`/Function` shape, but `DeviceGray` and `C0`/`C1` set to the source colours' alpha expressed as
0..1. Luminance of the mask becomes the alpha of the paint, which is what `/S /Luminosity` means.

**Alternative considered — `/ca` on the ExtGState.** A single constant alpha for the whole
gradient. Rejected: it cannot vary along the axis, which is the entire point. It would make
`FromArgb(0,…) → FromArgb(255,…)` a uniform half-transparent band, which is a different wrong answer
rather than a right one.

**Alternative considered — pre-multiplying alpha into the colours.** Blend each stop toward an
assumed white background. Rejected: it is only correct over white, and silently wrong over anything
else — exactly the class of failure this change exists to remove.

**Why the opaque path must be untouched:** the branch is `if either colour's A < 255`. Where it is
false, not a byte differs. This is testable directly — save the same document before and after and
compare — and task 1.6 does.

**Where the BBox comes from.** PDFKit uses the whole page. This library realizes a brush at the
point of use and knows the shape being filled, so the BBox is that shape's bounds in the form's
space. Falling back to the page box is acceptable and simpler; a too-large BBox costs rendering
time, not correctness. Start with the page box, narrow it only if a test shows it matters.

### 2. `AddString`: a backend seam, not an in-library outline decoder

This is the significant architectural decision in the change.

**In-library decoding** would mean parsing the `glyf` table's contours — quadratic B-splines,
on-curve and off-curve points, implied midpoints, composite glyphs with their transforms — and then
a Type 2 charstring interpreter for CFF. The first is perhaps 250 lines and well documented; the
second is 600 or more, with hint operators, subroutine biasing, and seac. `GlyphDataTable` holds the
glyph bytes as `byte[]` and decodes nothing, so all of it would be new.

**A backend seam** costs an interface, two implementations of a dozen lines each, and one more
static to register. Both backends already ship a library that has solved this:

- **Skia**: `SKTypeface.FromStream(bytes)` → `SKFont` → `SKTypeface.GetGlyphs(text)` →
  `SKFont.GetGlyphPath(glyph)`, then walk the `SKPath` with `SKPath.Iterator` and translate
  `Move`/`Line`/`Quad`/`Cubic`/`Close` into `CoreGraphicsPath.MoveTo`/`LineTo`/`BezierTo`/
  `CloseSubpath`. A quadratic converts to a cubic exactly: control points at
  `p0 + 2/3(q - p0)` and `p2 + 2/3(q - p2)`.
- **ImageSharp**: SixLabors.Fonts renders glyphs through an `IGlyphRenderer` whose callbacks are
  already move/line/quadratic/cubic/end — a direct translation with no path walking at all.

Decisive: the seam handles **CFF outlines on day one**. An in-library `glyf` decoder would leave
Source Code Pro — the one CFF font in this repository, carried precisely to exercise that path —
producing an empty path, which is the failure being fixed.

It is also the shape the codebase already uses twice. A caller who has registered a backend has
already accepted this; a caller who has not gets the same descriptive exception the other two seams
throw, rather than a silently empty path.

**Alternative considered — text rendering mode 7 (add to clip).** Would give clipping only, not a
fillable or widenable path, and nothing towards `AddString`'s signature. It is a different feature
worth having on its own.

**Alternative considered — put it on `IFontResolver`.** Rejected: that interface is implemented by
every consumer who has written a custom resolver, and adding a member would break all of them. A
separate interface with its own registration breaks nobody.

**Shape of the seam:**

```csharp
public interface IGlyphOutlineProvider
{
    // Outlines for the text, in em units with the origin at the baseline start, so the
    // caller scales and positions. Returns an empty sequence for an empty string.
    IEnumerable<XGlyphOutline> GetOutlines(string text, string familyName,
                                           bool isBold, bool isItalic, double emSize);
}
```

Registered as `GlobalFontSettings.GlyphOutlineProvider`, beside `FontResolver`, with the same
set-once-and-report-clearly semantics. The provider gets font *bytes* by calling the registered
`IFontResolver` — it does not resolve fonts itself, so the two seams cannot disagree about which
face a family means.

**Positioning** reuses what `DrawString` already computes. `XGraphics` has the alignment arithmetic
for a rectangle and a format; `AddString` must call the same code rather than a copy, or the two
will drift and the "path agrees with DrawString" scenario will start failing.

### 3. `BaseLine`: delete the guard, keep the placement

The guard is `XGraphics.cs:1295` and `XGraphicsPath.cs:391`. Removing them is not enough on its own
— the placement code below has to be checked to confirm it uses only the rectangle's `Y` for a
`BaseLine` alignment, and does not derive anything from the height. If it does, that derivation is
the bug the guard was hiding.

Purely widening: every input that worked still works, and inputs that threw now draw. No caller can
observe a difference except by catching the exception.

### 4. Table heading: make the silence loud, leave the rule alone

`CalcLastHeaderRow` walks from row 0 and `break`s at the first row without `HeadingFormat`. That is
correct — a heading that repeats has to be at the top. The defect is that a row marked outside that
run is discarded without a word.

After computing the run, scan the remaining rows. If any carries `HeadingFormat`, throw
`InvalidOperationException` naming the row index and the rule.

**Alternative considered — an event, as `DocumentRenderer.ImageFailed` does.** That precedent exists
and was right there, because an unreadable image is a *data* problem and a five hundred page report
should not die of one. A misplaced heading flag is a *programming* problem in the document being
built, discoverable at once, with one fix. Throwing is proportionate; an event nobody subscribes to
would reproduce the silence.

**Where it throws** matters: during formatting, before pages are written, so no caller receives half
a document.

## Risks / Trade-offs

- **Soft masks are the part of PDF readers most likely to differ** → Verify against Ghostscript
  through the existing rasterizing test collection, and check the produced structure against the
  specification rather than only the pixels. Test both a gradient over a solid fill and one over an
  image.
- **A third static seam is a third thing to configure, and a fourth would be too many** → It is
  registered exactly like the two that exist, ships with both backends, and throws a message naming
  the fix. If this pattern is reached for again, that is the moment to design a single backend
  registration rather than adding a fourth static.
- **The two backends may produce subtly different outlines** for the same font — different curve
  subdivision, different handling of degenerate contours → Assert on bounds and non-emptiness rather
  than on point counts or exact coordinates. Do not add golden images for glyph paths.
- **`AddString` is the largest piece and the least used** → It is last in the task order. Tasks 1
  through 3 are independently valuable and can ship without it.
- **The heading throw is a behaviour change in a rendering path** → Narrow by construction: it fires
  only where the document already renders wrongly. Worth a line in the release notes even so.
- **Demo comments become wrong** → `Magazine` explains at length why it fakes a scrim and strokes
  its title. Both explanations survive as history in this change and must come out of the demo when
  the features land. Task 5 covers it; it is not optional tidying, it is the demo lying otherwise.

## Migration Plan

Additive throughout. `IGlyphOutlineProvider` and its registration property are new public API on
`PdfPinata`; both backends gain a class. No existing signature changes.

The one behavioural break is the table heading throw. A document that hits it is a document whose
heading never repeated, so the migration is: mark the row above it too, or unmark the row. The
exception message says which.

Rollback is per-item — the four are independent and land in separate commits.

## Open Questions

- **Does `XGraphics`'s `BaseLine` placement path read the rectangle height anywhere?** Determines
  whether task 3 is two deletions or a small arithmetic fix. Answerable in ten minutes with the file
  open; not worth blocking the proposal.
- **What should `AddString` do for a glyph the font has no outline for** — a bitmap-only glyph, or
  `.notdef`? Skia returns false for bitmap glyphs. Skipping silently matches how the rest of the
  text path treats missing glyphs, but it is worth confirming against what `DrawString` does with
  the same character before choosing.
- **Should the alpha shading's BBox be the filled shape rather than the page?** Correctness is
  unaffected. Measure before optimising.
