## Why

A drop cap is the oldest ornament in typesetting and one of the few a reader notices when it is
absent. `XTextFormatter` cannot produce one, and neither can MigraDoc.

The reason is the same in both engines: **there is one measure per column and it never changes**.
`XTextFormatter.CreateLayout` computes `columnWidth` once and reuses it for every line; the only
thing that varies down a paragraph is `lineStart`, and it varies by exactly one bit — first line of
a paragraph, or not. MigraDoc's `ParagraphRenderer.LeftIndent` has the same shape: a `FirstLineIndent`
branch and nothing else. Neither can say "the first three lines are narrower".

`SampleApp/Demos/MagazineDemo.cs` shows what a caller has to do instead, and it is instructive:
draw the initial letter separately, then add one word at a time to a probe rectangle, asking
`GetLayout` after each, until the answer stops fitting — then flow the remainder into a second
rectangle underneath. That is roughly thirty lines to place one letter, it re-measures the text
O(words) times, and it silently produces the wrong thing if the cap's depth is not an exact multiple
of the line height.

`XGraphicsPath.AddString` landed in `fix-drawing-gaps` and changes what a good drop cap can do:
optical alignment wants the cap's **ink** box, and a path gives that where `MeasureString` gives
only the advance. The 'W' of a serif face overhangs its left side bearing; a drop cap set flush by
advance looks indented, and one set flush by ink looks right.

## What Changes

- **`XTextFormatter.DropCap`** — a property carrying the initial letter's font and how many lines
  deep it sits. Set it, call `DrawString`, and the formatter draws the cap and reserves the room
  beside it. The caller writes one property instead of thirty lines.
- **A measure that varies by line.** `CreateLayout` stops treating the column width and the line
  start as constants and asks for them per line. This is the whole of the change; the drop cap is
  its first caller and `shape-side-wrap` will be its second.
- **The cap is placed by ink, not by advance**, using `XGraphicsPath.AddString` through the
  registered `IGlyphOutlineProvider` when there is one, and by advance when there is not. A drop cap
  must not become the second feature that requires a backend seam nobody has registered.
- **The `Magazine` demo drops its measuring loop** and uses the property, which is worth more than
  the code it deletes: the demo currently teaches a workaround as though it were a technique.

Not in scope, and deliberately: MigraDoc. Its equivalent needs a new `Area` subclass and interacts
with page breaks, floating shapes and `KeepWith`; it belongs with `shape-side-wrap`, which needs the
same subclass. Doing the core engine first proves the idea in the simpler of the two.

## Capabilities

### New Capabilities

- `drop-cap`: an initial letter set into the opening lines of a text block, and the room the
  surrounding lines leave for it.
- `variable-line-measure`: text laid out where the width available to a line depends on how far
  down the block it sits, rather than being one number for the whole block.

### Modified Capabilities

None. `XTextFormatter` has no spec today, and every existing behaviour is unchanged where no drop
cap is asked for.

## Impact

**Code**

- `PdfPinata/Drawing.Layout/XTextFormatter.cs` — `CreateLayout` asks for the measure per line;
  new `DropCap` property; the cap drawn in `DrawString`.
- `PdfPinata/Drawing.Layout/` — a small type carrying the cap's font, depth and gutter.
- `SampleApp/Demos/MagazineDemo.cs` — the measuring loop comes out.

**Dependencies**: none. `AddString` is used where a provider is registered and skipped where it is
not.

**Packages**: additive public API on `PdfPinata`. No signature changes; a caller who never sets
`DropCap` sees identical output, which the change tests directly.

**Interaction with `shape-side-wrap`**: both need a per-line measure. This change introduces it in
`XTextFormatter` alone. If the two are built at the same time they should agree on the shape of it
before either lands.
