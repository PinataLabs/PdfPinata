## Why

Building the demonstration app (`docs/specs/demonstration-app.md`) put eleven demos across the
drawing surface, and four of them had to be rewritten because the API they reached for does not do
what its name says. Three of the four **fail silently** — no exception, no warning, just a page
missing the thing that was asked for — which is the worst way for a library to say no.

They were found because something drew a page and a human looked at it. Nothing in the test suite
would have caught any of them, and nothing in the API tells a caller in advance.

| gap | what a caller sees today |
|---|---|
| Gradient transparency | `XLinearGradientBrush` between a transparent colour and an opaque one paints a flat opaque band over whatever it was meant to veil |
| `XGraphicsPath.AddString` | Draws nothing. Reports through `DiagnosticsHelper` and returns, so the path is empty and the title vanishes |
| `XLineAlignment.BaseLine` | Throws unless the layout rectangle's height is exactly `0` — including for `XStringFormats.Default`, which *is* `BaseLineLeft` |
| MigraDoc repeated table heading | `HeadingFormat = true` on the row of column names does nothing at all if the row above it is not also marked |

## What Changes

- **Gradients honour alpha.** When either colour of an `XLinearGradientBrush` or
  `XRadialGradientBrush` has an alpha below 255, the shading pattern is drawn under a luminosity
  soft mask built from the same geometry, so the gradient fades out as well as across. Fully opaque
  gradients — every gradient that works today — emit byte-identical content streams.
- **`XGraphicsPath.AddString` produces a real path**, through a new backend seam that supplies glyph
  outlines. The path can then be filled with a gradient, used as a clip, or widened, none of which
  `DrawString` with a pen can do.
- **`IGlyphOutlineProvider`** — a third static seam beside `GlobalFontSettings.FontResolver` and
  `ImageSource.ImageSourceImpl`, implemented by both shipped backends. Unset, it throws the same
  kind of descriptive `InvalidOperationException` the other two seams already throw. This keeps the
  core package free of a font-rasterizing dependency, which is the property the whole backend split
  exists to protect.
- **`XLineAlignment.BaseLine` accepts a layout rectangle of any height**, placing the baseline on
  the rectangle's top edge and ignoring the rest. The `InvalidOperationException` it throws today is
  removed.
- **A misplaced `HeadingFormat` is refused rather than ignored.** A row marked as a heading which is
  not part of the unbroken run starting at row 0 throws `InvalidOperationException` naming the row
  and the rule, at format time. The rule itself does not change — a repeated heading has to be at
  the top of the table — only its silence does.
- **BREAKING (narrow):** a MigraDoc document that marks a heading row out of position renders today
  and will throw after this change. It renders *without a repeating heading*, so every such document
  is already producing the wrong output; the throw names the fix.

Not in scope, and deliberately: `AddPie`, `AddClosedCurve` and `AddPath` are unimplemented in the
same file as `AddString` and stay that way here — they need no new seam and no research, and folding
them in would hide the one change that does.

## Capabilities

### New Capabilities

- `gradient-transparency`: alpha in gradient stops, realised as a luminosity soft mask on the
  shading pattern.
- `glyph-outlines`: the backend seam that turns text into path geometry, and the
  `XGraphicsPath.AddString` overloads built on it.
- `baseline-text-alignment`: what `XLineAlignment.BaseLine` means when the layout rectangle has a
  height.
- `repeating-table-headings`: which rows of a MigraDoc table repeat onto later pages, and what
  happens when a document asks for a heading that cannot repeat.

### Modified Capabilities

None. `page-resize` is the only existing spec and none of its requirements change.

## Impact

**Code**

- `PdfPinata/Pdf.Advanced/PdfShading.cs`, `PdfShadingPattern.cs` — alpha detection and the mask
  branch. `PdfSoftMask`, `PdfFormXObject`, `PdfTransparencyGroupAttributes` and
  `PdfExtGState.SoftMask` already exist and are unused by this path; the change wires them up rather
  than adding them.
- `PdfPinata/Drawing/XGraphicsPath.cs` — two `AddString` overloads, currently
  `HandleNotImplemented`.
- `PdfPinata/Drawing/XGraphics.cs:1295` — the `BaseLine` guard, and the matching guard in
  `XGraphicsPath.cs:391`.
- `PdfPinata/Fonts/` — the new `IGlyphOutlineProvider` seam and its registration point.
- `PdfPinata.Skia/`, `PdfPinata.ImageSharp/` — one provider implementation each.
- `PinataLayout.Rendering/MigraDoc.Rendering/TableRenderer.cs` — `CalcLastHeaderRow`.

**Dependencies**: none added. Both backends already reference the library that can produce outlines
(SkiaSharp's `SKFont.GetGlyphPath`, SixLabors.Fonts' glyph renderer).

**Packages**: additive public API on `PdfPinata` (`IGlyphOutlineProvider`, the registration
property, working `AddString`). No signature changes, so nothing that compiles today stops
compiling.

**Demos**: `Magazine` currently builds its scrim from 140 translucent bands and strokes its title
with a pen, both with comments explaining that the direct route does not work. Both comments become
wrong when this lands, and the demo should be revisited to show the real thing.
