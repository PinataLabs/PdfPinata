---
title: Tables
description: Build PinataLayout tables with columns, rows and cells, merge and shade cells, and repeat heading rows on every page a long table reaches.
demos: [Tables]
---

A PinataLayout table is a grid of columns and rows whose cells hold paragraphs, images and text
frames. The renderer sizes each row to its content and breaks a long table across pages by itself,
repeating the heading rows at the top of each new page. Use a table for data in rows and columns,
such as the lines of an invoice or a report's figures. For a few aligned values, tab stops are
lighter; see [Paragraphs and text layout](./paragraphs-and-layout.md).

The table types are in the `PinataLayout.DocumentObjectModel.Tables` namespace.

## Columns first, then rows

`Section.AddTable()` creates the table. Add every column before you add a row: each row gets one cell
per column, and `AddColumn` throws an `InvalidOperationException` once the table has rows. `AddColumn` takes the column's width, and the
column's `Format` sets the paragraph format for every cell in it, so alignment is set once:

```csharp demo=Tables snippet=columns
```

`AddRow()` returns a `Row`, and `row.Cells[index]` is the cell in each column. Add content with
`cell.AddParagraph(text)`. `Rows.LeftIndent` moves the whole table from the margin, and
`Rows.Alignment` centres it or sets it against the right margin.

Formatting cascades from the table to the column, the row and the cell. `Table.Borders`,
`Table.Shading` and `Table.Format` apply to every cell; `Row.Borders`, `Row.Shading` and `Row.Format`
override them for one row; the cell's own properties override both. A table and each cell can also
take a `Style`.

## Heading rows that repeat

Set `Row.HeadingFormat = true` on the rows at the top of the table. When the table runs onto another
page, those rows are drawn again at the top of it. In a tagged document their cells also become
header cells, so a screen reader can announce the column name before each value.

```csharp demo=Tables snippet=heading-rows
```

The heading is the unbroken run of `HeadingFormat` rows that starts at row 0. A heading row after an
ordinary row cannot repeat, and the renderer throws an `InvalidOperationException` that names the
row. If every row of the table is a heading row, nothing repeats.

## Merge cells

`Cell.MergeRight` is the number of cells to the right that the cell spans, and `Cell.MergeDown` is
the number of rows below it. Put the content in the first cell and leave the cells it covers empty.

```csharp demo=Tables snippet=banding-and-merge-down
```

The invoice uses `MergeRight` for its totals, so the label spans the first four columns and the
amount sits in the last:

```csharp demo=Invoice snippet=total-rows
```

## Borders and shading

`Borders` has `Top`, `Bottom`, `Left` and `Right` borders, each with a `Width`, `Color` and `Style`.
Setting `Borders.Width` or `Borders.Color` sets all four. `Shading.Color` fills the background.

`Table.SetEdge` draws a border around, or along one edge of, a block of cells. It takes the first
column, the first row, the number of columns and rows, which edges to draw (`Edge.Box`, `Edge.Bottom`,
`Edge.Interior` and others), a line style, a width and an optional colour:

```csharp demo=Tables snippet=set-edge
```

`Table.SetShading` fills a block of cells the same way. The demo finishes with a total row that is
shaded, bold and boxed:

```csharp demo=Tables snippet=total-row
```

## Row height and alignment inside cells

A row grows to fit its tallest cell. `Row.Height` with `Row.HeightRule` sets a minimum
(`RowHeightRule.AtLeast`) or a fixed height (`RowHeightRule.Exactly`). `Row.VerticalAlignment` and
`Cell.VerticalAlignment` place the content at the top, centre or bottom of the cell. `TopPadding` and
`BottomPadding` on a row, and `LeftPadding` and `RightPadding` on a column or the table, set the space
between the content and the cell's edges. Horizontal alignment is a paragraph setting: set it on the
column's, row's or cell's `Format`.

## Keep rows together

A row is never split across pages. To stop a page break between rows that belong together:

- `Row.KeepWith` is the number of rows after this one that must stay on the same page as it.
- Cells merged with `MergeDown` keep the rows they span together.
- `Table.KeepTogether` keeps the whole table on one page, if it fits.

## Things to know

- **Add all columns before the first row.** A column added after a row throws.
- **`HeadingFormat` must start at the first row.** Mark every row from row 0 down to the last heading
  row. A title band above the column names needs the flag too, as in the demo.
- **Merged cells are counts, not indexes.** `MergeRight = 4` spans five cells: the cell itself and
  four more.
- **Merged rows move as one block.** Rows joined by `MergeDown` or `KeepWith` go to the next page
  together, so a large merged block can leave a gap at the foot of the page before it.
- **Describe data tables for screen readers.** `Table.Summary` is written into the tagged output. See
  [Accessibility](../standards/accessibility.md).
- **A footnote cannot go in a cell.** See [Footnotes](./footnotes.md).

## See it in action

[The Tables demo](../demos.mdx#tables) builds an 80-row table of quarterly figures that runs over two
pages. It has a two-row repeating heading, region cells merged down four rows, alternate row shading,
a rule under each region and a boxed total row.

<details>
<summary>The full Tables demo</summary>

```csharp demo=Tables
```

</details>
