---
title: Text layout with XTextFormatter
description: Wrap text into a rectangle with XTextFormatter, with alignment, columns, indents, truncation and measuring, and know when to use PinataLayout instead.
demos: [Layout]
---

`XGraphics.DrawString` draws one line and does not wrap. `XTextFormatter`, in the
`PdfPinata.Drawing.Layout` namespace, flows text into a rectangle. It breaks lines at spaces, starts a
new paragraph at each line feed (`\n`), and can align, justify, indent, split the text into columns
and measure it before you draw.

Use it for a block of text in a fixed place on a page: an address, a note, a caption, a column of
copy. For a document whose text runs over several pages, use PinataLayout instead (see
[When to use PinataLayout](#when-to-use-pinatalayout)).

## Wrap text into a rectangle

Create one formatter for the `XGraphics` you draw with, set its properties, and call `DrawString`
with a rectangle. The formatter keeps its settings between calls. The demo draws the same paragraph
with each of the four alignments:

```csharp demo=Layout snippet=alignments
```

`XParagraphAlignment.Justify` makes every line of a paragraph flush at both sides, apart from the
last line, which stays on the left.

## Vertical alignment

`VerticalAlignment` places the block at the `Top`, `Middle` or `Bottom` of the rectangle. You can
also pass a `TextFormatAlignment` to `DrawString`, which sets the horizontal and vertical alignment
together:

```csharp demo=Layout snippet=vertical-alignment
```

## When the text does not fit

By default, text that is too long for the rectangle is cut off at the last line that fits. Nothing
marks the cut. Set `Ellipsis` to end the last line with a mark instead.
`XTextFormatter.DefaultEllipsis` is the ellipsis character (…):

```csharp demo=Layout snippet=ellipsis
```

`Ellipsis` is a string, so you can use three full stops for a font that has no ellipsis character.

Two other settings change what happens at the edges:

- `AllowVerticalOverflow = true` draws every line, even below the bottom of the rectangle.
- `LineBreak = false` stops the formatter wrapping. The text runs past the right edge, but line feeds
  in the text still start new lines.

```csharp demo=Layout snippet=no-line-break
```

## Columns

`Columns` divides the rectangle into columns of equal width, with `ColumnGap` points between them.
The text fills the first column, then the next:

```csharp demo=Layout snippet=columns
```

`ColumnGap` is 18 points unless you change it.

## Indents and gaps

- `Indent` indents the first line of each paragraph, in points. With `IndentAllLines = true`, it
  indents every line.
- `ParagraphGap` adds space after each paragraph.
- `LineGap` adds space between all lines.

```csharp demo=Layout snippet=indents
```

To set the line height itself, pass `lineHeight` as the last argument of `DrawString` or
`GetLayout`. `LineGap` is added to it.

## Lists

`XTextFormatter` has no list support. To make a list with a hanging indent, draw the marker yourself
and flow the item text into a rectangle that starts to the right of it. `GetLayout` tells you how
tall each item was, so you know where the next one starts:

```csharp demo=Layout snippet=lists
```

For bullet and numbered lists that the library lays out, use PinataLayout.

## Measure before you draw

`GetLayout` lays the text out without drawing it and returns the rectangle it needs. Give it a
rectangle of the width you want and more height than the text can need. Turn on
`AllowVerticalOverflow` while you measure, so that no text is cut off:

```csharp demo=Layout snippet=measure
```

Use this to size a box, a background or a border to its text, or to decide whether a block fits in
the space left on a page.

## Rotated text

`Rotation` turns the text by an angle in degrees about the top left corner of the rectangle.
Positive angles turn it anticlockwise:

```csharp demo=Layout snippet=rotation
```

The text turns about the corner, not about the centre of the rectangle. At 90 degrees it runs up
the page from that corner.

## Drop caps and text around shapes

Two more features are for magazine-style pages:

- `DropCap` takes an `XDropCap`, which sets the first letter of the text in a larger font, a given
  number of lines deep.
- `Obstacles` is a list of shapes that the text flows around. Add a `RectangleObstacle` for each
  area to keep clear, for example a picture or a pull quote. Give its position relative to the top
  left corner of the layout rectangle.

[The Magazine demo](../demos.mdx#magazine) uses both, and
[the Newspaper demo](../demos.mdx#newspaper) sets a front page in columns.

## When to use PinataLayout

`XTextFormatter` lays out one block in one rectangle that you choose. It does not continue text on
the next page, and it has no styles, tables, lists, headers or footers. Move to PinataLayout when you
need any of these:

- text that flows from page to page
- styles shared across a document
- tables, lists, footnotes, or headers and footers
- a structure tree for accessible, tagged PDF

See [Documents, sections and styles](../layout/documents-sections-and-styles.md) and
[Paragraphs and text layout](../layout/paragraphs-and-layout.md).

## Things to know

- **Settings stay on the formatter.** A property you set applies to every later `DrawString` call on
  that formatter. The demo sets `Ellipsis`, `LineBreak` and `Columns` back after each use. Passing a
  `TextFormatAlignment` to `DrawString` also changes the formatter's `Alignment` and
  `VerticalAlignment` for later calls.
- **Cut-off text is lost without a warning.** Measure with `GetLayout` first if the whole text must
  appear, or set `Ellipsis` so that the cut shows.
- **`Obstacles` and `Rotation` cannot be used together.** If both are set, `DrawString` and
  `GetLayout` throw `InvalidOperationException`. To turn text that flows around an obstacle, rotate
  the `XGraphics` instead.
- **Right-to-left paragraphs work.** Set `TextDirection` to a `BidiParagraphDirection`. See
  [International text](../fonts-and-text/international-text.md).
- **One formatter draws on one `XGraphics`.** For a new page, create a new formatter with the new
  page's `XGraphics`.

## See it in action

[The Layout demo](../demos.mdx#layout) shows the four alignments, truncation, columns, indents, a
hand-built list, vertical alignment, measuring with `GetLayout`, and rotated blocks.

<details>
<summary>The full Layout demo</summary>

```csharp demo=Layout
```

</details>
