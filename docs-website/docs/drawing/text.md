---
title: Drawing text
description: Place a line of text with XGraphics.DrawString, align it, measure it, space it, outline it and put a link on it.
demos: [Text]
---

`XGraphics.DrawString` draws one line of text on a page. You give it the string, an `XFont`, a brush
for the colour, and either a point or a rectangle. An `XStringFormat` controls where the text sits
and how it is spaced. For text that wraps over several lines, use
[XTextFormatter](./text-layout.md) instead.

This page assumes you have registered a font resolver. See [Fonts](../fonts-and-text/fonts.md).

## At a point or in a rectangle

With a point, the text's baseline sits on the point and the text runs to the right of it. With a
rectangle and a format, the format places the text inside the rectangle. The library does not draw
the rectangle:

```csharp demo=Text snippet=point-and-rect
```

## Alignment

`XStringFormats` has ready-made formats for the nine combinations of top, centre and bottom with
left, centre and right:

```csharp demo=Text snippet=presets
```

It also has `BaseLineLeft`, `BaseLineCenter` and `BaseLineRight`, which put the baseline on the top
edge of the rectangle. `XStringFormats.Default` is `BaseLineLeft`, and it is what `DrawString` uses
when you pass no format.

To build your own format, set `Alignment` (an `XStringAlignment`: `Near`, `Center` or `Far`) and
`LineAlignment` (an `XLineAlignment`). `LineAlignment` has `Near`, `Center`, `Far` and `BaseLine`,
and three more that follow the HTML canvas: `Hanging`, `Ideographic` and `SvgMiddle`.

`BaseLine` reads only the top edge of the rectangle. Give the rectangle a height of 0:

```csharp demo=Text snippet=baseline
```

## Measure text

`MeasureString` returns the width and height of a string in the same units you draw in. Use it to
fit a rule under the text, place the next word, or check whether a line fits:

```csharp demo=Text snippet=measure
```

If you draw with an `XStringFormat` that changes spacing, pass the same format to
`MeasureString(text, font, format)`. The measurement then includes the spacing.

## Spacing, scaling and slant

`XStringFormat` carries the PDF text-state settings:

- `CharacterSpacing` adds space after every glyph, in points. A negative value tightens the text.
- `WordSpacing` adds space after every space character only.
- `HorizontalScaling` stretches or squeezes the glyphs, as a percentage. 100 is normal.
- `ObliqueAngle` slants the upright glyphs by an angle in degrees. This is not an italic: a real
  italic face has different letter shapes.

```csharp demo=Text snippet=spacing
```

## Superscripts and subscripts

`TextRise` moves the baseline up (positive) or down (negative) by a number of points without
changing the glyph size. For a superscript or a subscript, use a smaller font as well:

```csharp demo=Text snippet=text-rise
```

## Fill, outline, or both

Which of a brush and a pen you pass decides how the glyphs are painted. A brush fills them, a pen
outlines them, and both together fill and then outline them. You must pass at least one:

```csharp demo=Text snippet=fill-stroke
```

The brush sets the text colour. `XBrushes` has named colours, and `new XSolidBrush(colour)` takes an
`XColor` made with `XColor.FromArgb`, `XColor.FromCmyk` or `XColor.FromGrayScale`. See
[Shapes, pens and brushes](./shapes-pens-and-brushes.md) for colours, pens and transparency.

## Links on text

A link is a clickable rectangle on the page. The text under it is a separate drawing, and the
library draws nothing for the link. Measure the text, draw it, and add the link over the same area:

```csharp demo=Text snippet=links
```

- `AddWebLink` opens a URL.
- `AddNamedDestination` marks a place in the document, and `AddNamedLink` goes to it. A named link
  still works after pages are moved, inserted or resized.
- `AddDocumentLink` goes to a page by number. The first page is 1.

The rectangles use the same coordinates and units as your drawing.
[Navigation and viewer preferences](../interactive/navigation-and-viewer-preferences.md) covers
destinations in more detail.

## Things to know

- **`DrawString` draws one line.** It does not wrap. A line feed does not start a new line: it is
  dropped, and the words either side of it run together. A tab is drawn as one space. See
  [Control characters](../fonts-and-text/unicode-and-embedding.md#control-characters).
- **A new `XStringFormat` is not the default format.** `new XStringFormat()` aligns the text to the
  top left of the rectangle, but `DrawString` without a format aligns the baseline. If you create a
  format only to set spacing or direction, also set `LineAlignment = XLineAlignment.BaseLine`, or the
  text moves down by the height of the font.
- **A link is invisible.** Without the colour and underline you draw yourself, nobody can see it.
- **Underline with the font or with the format.** `XFontStyle.Underline` draws a plain line.
  `XStringFormat.Underline` and `Strikeout` take an `XTextDecoration` for dotted and dashed lines,
  and `DecorationColor` gives the line its own colour. See [Fonts](../fonts-and-text/fonts.md).
- **Right-to-left text is reordered for you.** `XStringFormat.TextDirection` sets the paragraph
  direction. See [International text](../fonts-and-text/international-text.md).

## See it in action

[The Text demo](../demos.mdx#text) places text at a point and in all nine alignments, measures it,
draws the spacing settings side by side, paints text three ways, and adds three kinds of link.

<details>
<summary>The full Text demo</summary>

```csharp demo=Text
```

</details>
