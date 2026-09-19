---
title: Barcodes
description: Draw Code 3 of 9, interleaved 2 of 5, OMR marks and ECC200 Data Matrix symbols with XGraphics, and control their size, text, direction and quiet zone.
demos: [Barcodes]
---

PdfPinata draws four kinds of code as vector graphics, so they stay sharp at any zoom and in print.
Three are linear barcodes: Code 3 of 9 (Code 39), interleaved 2 of 5, and OMR marks for mail-sorting
machines. The fourth is the ECC200 Data Matrix, a two-dimensional code. They are in the core
`PdfPinata` package, in the `PdfPinata.Drawing.BarCodes` namespace, and need no extra package.

A code is an object, not a single call. You create it with its text, its size and the direction it
runs in, then draw it at a point. Linear codes are drawn with `XGraphics.DrawBarCode` and the Data
Matrix with `XGraphics.DrawMatrixCode`.

## Code 3 of 9

Code 3 of 9 takes the digits, the capital letters A to Z, the space and the characters
`- . $ / + %`. It adds its own start and stop characters, so do not add asterisks yourself.

```csharp demo=Barcodes snippet=code39
```

The `XSize` is the box the code fills. The bars are scaled to fill its full width, so a longer text
gives thinner bars in the same box. The point passed to `DrawBarCode` is the top-left corner of the
box, unless you change the `Anchor`.

`DrawBarCode` has overloads with and without a brush and a font. The font is for the text under or
over the bars. If you give none, the code uses your font resolver's default font at one sixth of the
code's height.

## Interleaved 2 of 5

Interleaved 2 of 5 takes digits only, and an even number of them, because each group of five bars
carries two digits. It is denser than Code 3 of 9.

```csharp demo=Barcodes snippet=code25
```

If your number has an odd count of digits, add a leading zero.

## Wide-to-narrow ratio

Both linear codes are made of wide and narrow bars. `WideNarrowRatio` sets how much wider a wide bar
is. It must be from 2 to 3, and the default is 2.6. A larger ratio is easier for a scanner to read and
needs more width for the same text.

```csharp demo=Barcodes snippet=ratio
```

## Human-readable text

`TextLocation` puts the code's text above or below the bars, or leaves it out:

```csharp demo=Barcodes snippet=text-location
```

`Above` and `Below` draw the text outside the box. `AboveEmbedded` and `BelowEmbedded` draw it inside
the box. With any setting but `None`, the bars take four fifths of the box's height.

## Direction and anchor

`CodeDirection` turns the code about the point you draw it at. The four values are `LeftToRight`
(the default), `RightToLeft`, `TopToBottom` and `BottomToTop`. You do not need to set a transform
yourself.

```csharp demo=Barcodes snippet=direction
```

`Anchor` says which part of the code's box lands on that point. There are nine `AnchorType` values,
from `TopLeft` (the default) to `BottomRight`. `MiddleCenter` centres the code on the point.

```csharp demo=Barcodes snippet=anchor
```

## OMR marks

OMR marks tell a mail-sorting machine how to handle a sheet. They are not read as characters. The
text you give is parsed as a whole number, and the code draws one mark for each bit of it that is set,
lowest bit first, after a synchronisation mark.

```csharp demo=Barcodes snippet=omr
```

`MakerDistance` sets the space between marks, in points (12 by default, one sixth of an inch).
`MakerThickness` sets the thickness of each mark (1 by default).

## Data Matrix

`CodeDataMatrix` draws an ECC200 Data Matrix. You give the text, the symbol size in modules (rows and
columns), and the size to draw it at:

```csharp demo=Barcodes snippet=data-matrix
```

ECC200 allows only certain sizes. The square sizes run from 10 × 10 to 144 × 144 modules. The
rectangular sizes are 8 × 18, 8 × 32, 12 × 26, 12 × 36, 16 × 36 and 16 × 48. A size that is not on the
list, or too small for the text, throws an `InvalidOperationException` when you draw the symbol.
PdfPinata does not truncate the text or pick a larger symbol for you.

A rectangular symbol suits a label that is wide but short. Give it a box with the same proportions as
the symbol, or the modules come out stretched:

```csharp demo=Barcodes snippet=rectangular
```

If you give no size, the symbol is drawn at 2 points per module.

### Quiet zone

A scanner needs a blank margin, the quiet zone, around a symbol to find its edges. `QuietZone` sets it
in modules. The margin is drawn inside the size you give, in white, so a wider quiet zone makes the
symbol itself smaller:

```csharp demo=Barcodes snippet=quiet-zone
```

## Things to know

- **Invalid text throws.** Code 3 of 9 and interleaved 2 of 5 throw an `ArgumentException` when you
  create the code, or set its `Text`, with characters they cannot encode. For interleaved 2 of 5,
  an odd number of digits is also invalid.
- **Linear codes need a size.** A linear code created without an `XSize` throws when you draw it.
- **Leave your own quiet zone round linear codes.** Code 3 of 9 and interleaved 2 of 5 fill their box
  edge to edge. Leave blank space on each side when you place them.
- **No check digits.** Neither linear code adds a check digit. If your scanner expects one, calculate
  it and add it to the text.
- **Data Matrix uses ASCII encodation only.** `DataMatrixEncoding` also names `C40`, `Text`, `X12`,
  `EDIFACT` and `Base256`, but they throw `NotImplementedException`. ASCII can carry any text a Data
  Matrix can hold. It is less compact for long runs of one kind of character.
- **Data Matrix carries bytes, not Unicode.** A character above U+00FF throws an
  `InvalidOperationException` when you draw the symbol.
- **A Data Matrix is never taller than it is wide.** If you give more rows than columns, the two
  numbers are swapped.
- **OMR marks always start with a synchronisation mark, and the lowest bit is always set.** The
  `SynchronizeCode` property has no effect, and 1382 draws the same marks as 1383. Text that is not a
  number is read as zero.
- **`BarCode.FromType` cannot make a Data Matrix.** It throws for `CodeType.DataMatrix`. Create a
  `CodeDataMatrix` and draw it with `DrawMatrixCode`.

## See it in action

[The Barcodes demo](../demos.mdx#barcodes) draws every code on three pages, with each ratio, text
location, direction, anchor, symbol size and quiet zone side by side.

<details>
<summary>The full Barcodes demo</summary>

```csharp demo=Barcodes
```

</details>
