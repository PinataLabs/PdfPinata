## Why

MigraDoc cannot flow text beside a shape. An image or a text frame either interrupts the column
across its full width or is ignored by the text entirely and overlaps it. There is no third
behaviour, so there is no pull quote with text down one side, no image with a caption column beside
it, and no sidebar.

The feature was designed and abandoned. `Floating` declares its side values under a comment that
says so:

```csharp
internal enum Floating
{
  TopBottom = 0, //Default
  None,          //The element is ignored

  //Served for future extensions:
  Left,
  Right,
  BothSides,
}
```

`Floating.Left`, `Right` and `BothSides` are read **zero** times anywhere in the renderer. Every call
site asks only `!= Floating.None`. `ShapeRenderer.GetFloating` can return only `TopBottom` or `None`,
and the public `WrapStyle` enumeration offers only `TopBottom`, `None` and `Through` — so the
document object model cannot express "wrap around this" even in principle.

Meanwhile `WrapFormat` carries `DistanceLeft`, `DistanceRight`, `DistanceTop` and `DistanceBottom`.
Those four properties exist, are public, are serialised, and mean nothing on the two axes that
matter, because nothing ever puts text beside a shape for them to hold it off.

The result is the workaround in `SampleApp/Demos/MagazineDemo.cs`: a pull quote positioned by hand
with the body text split into two separate flows above and below it, the split computed by the
caller. Every document that wants a sidebar writes that by hand.

## What Changes

- **`WrapStyle` gains the values that mean "wrap around".** A shape can ask for text on its left, on
  its right, on whichever side has more room, or on both.
- **`Floating.Left`, `Right` and `BothSides` stop being decoration** and are honoured by the
  formatter, which is what those three values were reserved for.
- **`Area` gains a non-rectangular implementation.** `Area.GetFittingRect(yPosition, height)` is
  already abstract and already called once per line by `ParagraphRenderer`; today the only
  implementation is a rectangle, for which it is trivial. An area that knows about obstacles makes
  text flow around them **without touching the line-breaking loop at all**.
- **`WrapFormat`'s four distances start meaning something on all four sides.**
- **The `Magazine` demo drops its hand-split flow** and sets a wrap style.

Not in scope, and deliberately: contour wrapping to a shape's outline rather than to its bounding
box; wrapping around a shape that spans a page break; and text flowing beside a *table*. Each is a
larger problem than this one and none is needed to stop callers splitting their text by hand.

## Capabilities

### New Capabilities

- `shape-side-wrap`: text flowing beside a shape rather than above and below it, which side it
  flows on, and how far it is held off.

### Modified Capabilities

None. `WrapStyle` gains values but no existing value changes meaning: a document that does not ask
for a side wrap lays out exactly as it does today.

## Impact

**Code**

- `PinataLayout.DocumentObjectModel/.../enums/WrapStyle.cs` — new values. Public API on the DOM.
- `PinataLayout.Rendering/MigraDoc.Rendering/Area.cs` — a second implementation beside `Rectangle`.
- `PinataLayout.Rendering/MigraDoc.Rendering/ShapeRenderer.cs` — `GetFloating` returns the side
  values.
- `PinataLayout.Rendering/MigraDoc.Rendering/TopDownFormatter.cs` — the three places that ask
  `!= Floating.None` have to ask something more specific.
- `SampleApp/Demos/MagazineDemo.cs` — the hand-split flow comes out.

**Serialisation**: `WrapStyle` is written to and read from MDDDL, and the DOM's serialisation is
generated — see `docs/specs/generated-serialization.md`. New enumeration values have to survive a
round trip and the generator has to be checked rather than assumed.

**Compatibility**: a document written with a new `WrapStyle` value cannot be read by an older
version of the library. Additive within this fork; worth a line in the release notes.

**Interaction with `drop-cap-layout`**: both need a measure that varies by line. That change
introduces one in `XTextFormatter`; this one needs the same idea in MigraDoc's `Area`. They are
separate engines and will be separate code, but they should not be separate *ideas* — whichever
lands second should follow the first.
