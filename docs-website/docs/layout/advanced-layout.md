---
title: Columns, drop caps and wrapping
description: Set text in columns, open it with a drop cap, flow it around a pull quote, and run a PinataLayout paragraph down the side of a shape.
demos: [Newspaper, Magazine, SideWrap]
---

This page covers newspaper columns, drop caps, pull quotes and sidebars. Two engines do this work,
and you choose by how you build the page:

- **`XTextFormatter`**, in the core `PdfPinata` package (namespace `PdfPinata.Drawing.Layout`), lays
  text out in a rectangle you give it on an `XGraphics`. It can split the rectangle into columns, set
  a drop cap, and keep text clear of rectangles you name. Use it when you place things on the page
  yourself.
- **PinataLayout** lays out a whole document. A text frame, image or chart can ask for the text beside
  it to run down one side. Use it when the renderer decides where things go.

A PinataLayout section has no column setting. For multi-column text, use `XTextFormatter`.

## Set text in columns

Set `Columns` and `ColumnGap` on the formatter. It divides the layout rectangle into that many columns
of equal width and fills each in turn. `ColumnGap` is in points, and the default is 18.

```csharp demo=Newspaper snippet=columns
```

Then draw the text into the whole rectangle:

```csharp demo=Newspaper snippet=columns-draw
```

The formatter draws nothing in the gutters. To draw a rule down each one, work out the gutter
positions with the same sums the formatter uses:

```csharp demo=Newspaper snippet=gutter-rules
```

The formatter keeps its settings between calls. Set `Columns` back to 1 before you draw a caption or
a headline with the same formatter.

## Open with a drop cap

A drop cap is one property. Give `DropCap` an `XDropCap` with a font and a depth in lines. The
formatter takes the first character of the text, scales it so its foot sits on the baseline of the
last line it spans, and shortens those lines to leave room for it.

```csharp demo=Magazine snippet=drop-cap
```

The size of the font you pass is ignored: the formatter works out the size from the depth. The font
gives only the family and the style. `XDropCap.Gutter` sets the space between the cap and the text,
in points; when you leave it null, the space is one space character of the body font.

The cap takes only the first character, so write the text in full. In a multi-column block, the cap
takes room in the first column only.

## Flow text around a pull quote or picture

To keep text off something you have drawn in the block, add a `RectangleObstacle` to the formatter's
`Obstacles`. The lines level with the obstacle are shortened to clear it, in every column it stands
in. The lines above and below it run the full width. One `DrawString` call lays out the whole block.

```csharp demo=Magazine snippet=obstacle
```

The obstacle's rectangle is **relative to the layout rectangle**, not to the page: (0, 0) is the top
left corner of the block. To draw the quote itself, add the block's corner to get page coordinates,
as the demo does with `quoteInBlock` and `quote`.

The `padding` argument keeps text that distance from the obstacle on all four sides. Each obstacle
has its own padding.

When an obstacle stands clear of both edges of a column, a line has room on both sides of it. The
formatter fills the wider side and leaves the narrower one empty. It never splits one line across an
obstacle. The pull quote in the demo straddles the gutter, so each column keeps its outside edge.

## Keep text inside a box

A word wider than its column is drawn past the column's edge, and a layout rectangle can reach past
the box you drew around it. To cut text off at the edge of a box, clip to the box. `XGraphics` has no
way to undo a clip except to restore a state saved before it:

```csharp demo=Newspaper snippet=clip-sidebar
```

## Run text beside a shape in a PinataLayout document

In a PinataLayout document, set `WrapFormat.Style` on a shape to one of the four side-wrap styles. The
paragraphs after the shape are laid out beside it: lines level with the shape are shortened, and lines
above and below it run the full width. You do not split or measure the text.

```csharp demo=SideWrap snippet=wrap-frame
```

| `WrapStyle` | Where the text goes |
| --- | --- |
| `Right` | Down the right of the shape. Put the shape at the left. |
| `Left` | Down the left of the shape. Put the shape at the right. |
| `Largest` | Down whichever side has more room. |
| `Both` | Either side. It lays out the same as `Largest`. |
| `TopBottom` | Above and below the shape, never beside it. This is the default. |
| `None`, `Through` | The text ignores the shape and can run over it. |

`Left` and `Right` name **the side the text takes**, not the side the shape sits on. If you get it
backwards, nothing fails and the page still looks deliberate.

The four `WrapFormat` distances hold the text off the shape. `DistanceLeft` and `DistanceRight` keep
the text away at the sides. `DistanceTop` and `DistanceBottom` make the shape taller for wrapping, so
a line that would clear it by a hair moves past it instead.

## Things to know

- **`XTextFormatter` does not tell you what did not fit.** Text that runs past the last column is
  dropped, and no call returns the rest. For text that must continue in another block, use
  obstacles to keep it in one block, or use PinataLayout, which breaks pages for you.
- **Obstacles and `Rotation` do not mix.** If a formatter has obstacles and a `Rotation` that is not
  zero, `DrawString` and `GetLayout` throw `InvalidOperationException`. To turn a block that has
  obstacles, leave `Rotation` at zero and rotate the `XGraphics` before you draw.
- **A null in `Obstacles` throws.** Remove an entry rather than leaving a null in the list.
- **Clear the formatter after use.** `DropCap` and `Obstacles` stay set for the next `DrawString`
  call. Set `DropCap` to null and call `Obstacles.Clear()` when you are done.
- **A drop cap looks best with a glyph outline provider.** When
  `GlobalFontSettings.GlyphOutlineProvider` is set (the backends supply `SkiaGlyphOutlineProvider` and
  `ImageSharpGlyphOutlineProvider`), the cap sits flush with the margin by its ink. Without one it is
  placed by its advance width, which leaves a small gap. Nothing throws either way.
- **Obstacles are rectangles.** Text does not follow the outline of a picture, in either engine.
- **A shape floats only when anchored to the text.** Side wrapping needs `RelativeVertical` set to
  `Paragraph` or `Line`. A shape anchored to the `Page` or `Margin` is placed at a fixed position,
  and the text is laid out as if it were not there.
- **Side wrapping is for paragraphs.** Wrapping a table beside a shape is not supported.
- **A shape that does not fit falls back.** When a side-wrapped shape is taller than the room left on
  the page, or would cross a page break, it is laid out as `TopBottom` instead.
- **Side-wrap styles need a matching reader.** A document saved as [DDL](./ddl.md) with `Left`,
  `Right`, `Largest` or `Both` can be read only by a version of this library that has those styles.

## See it in action

- The [Newspaper demo](../demos.mdx#newspaper) sets a front page in five justified columns with
  gutter rules and a clipped sidebar.
- The [Magazine demo](../demos.mdx#magazine) opens a feature with a drop cap in two columns, and
  flows the next page around a pull quote.
- The [SideWrap demo](../demos.mdx#sidewrap) puts a text frame beside a paragraph with each of the four
  side-wrap styles, one per page.

<details>
<summary>The full Newspaper demo</summary>

```csharp demo=Newspaper
```

</details>

<details>
<summary>The full Magazine demo</summary>

```csharp demo=Magazine
```

</details>

<details>
<summary>The full SideWrap demo</summary>

```csharp demo=SideWrap
```

</details>
