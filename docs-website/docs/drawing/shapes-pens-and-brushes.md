---
title: Shapes, pens and brushes
description: Draw lines, curves, shapes and paths with XGraphics, and control how they are stroked, filled, coloured, transformed and clipped.
demos: [Vectors]
---

`XGraphics` is the drawing surface of a PDF page. Text, images, lines and shapes all go through it,
and PinataLayout draws through the same surface. This page covers vector graphics: the shapes
`XGraphics` can draw, the pens that outline them, the brushes that fill them, and the transforms and
clipping that place them. All of it is in the core `PdfPinata` package. Only turning text into a
path needs a backend.

## Get a drawing surface

Create an `XGraphics` for a page with `XGraphics.FromPdfPage`, and dispose it when you have finished
drawing:

```csharp
PdfDocument document = new PdfDocument();
PdfPage page = document.AddPage();
using XGraphics gfx = XGraphics.FromPdfPage(page);

gfx.DrawLine(XPens.Black, 50, 50, 250, 50);
```

Coordinates are in points, 72 to the inch. The origin is the top-left corner of the page and y grows
downwards, so an A4 page runs from (0, 0) to about (595, 842). To work in other units, pass an
`XGraphicsUnit` such as `XGraphicsUnit.Millimeter` to `FromPdfPage`.

To draw on a page of an existing document, open it in a mode that allows changes (see
[Opening documents](../existing-pdfs/opening-documents.md)). `FromPdfPage` has overloads that take an
`XGraphicsPdfPageOptions`: `Append` draws over the existing content (the default), `Prepend` draws
under it, and `Replace` clears the page first.

## Shapes, pens and brushes

Every closed shape has overloads that take a pen, a brush, or both. The pen outlines the shape and the
brush fills it. Pass `null` for either one to leave it out.

```csharp demo=Vectors snippet=pen-and-brush
```

In the Vectors demo, `Panel` is a helper that draws a titled box and hands the lambda a rectangle to
draw in. The closed shapes are `DrawRectangle`, `DrawRectangles`, `DrawRoundedRectangle`,
`DrawEllipse`, `DrawPolygon`, `DrawPie` and `DrawClosedCurve`. `DrawRectangles` draws many
rectangles as one path, which writes less to the file than a call per rectangle.

`XPens` and `XBrushes` hold ready-made pens and brushes, one per named colour in `XColors`. They are
read-only: changing the width of `XPens.Black` throws. Create a new `XPen`, or call `Clone()` on a
ready-made one, when you need to change it.

### Fill modes

A shape whose outline crosses itself can be filled two ways. `XFillMode.Alternate` (even-odd) leaves
areas that are enclosed twice empty. `XFillMode.Winding` (non-zero) fills them.

```csharp demo=Vectors snippet=fill-modes
```

## Lines, curves and arcs

Open shapes take a pen only: `DrawLine`, `DrawLines` (a polyline), `DrawBezier`, `DrawBeziers`,
`DrawCurve` (a cardinal spline through the points) and `DrawArc`. The two middle points of a Bezier
are control points. The curve bends towards them but does not pass through them.

```csharp demo=Vectors snippet=bezier
```

`DrawBeziers` chains curves: each curve after the first starts where the last one ended, so the
array holds 1 + 3n points for n curves. `DrawCurve` takes a tension. At 0 the spline is a polyline,
and higher values bend it more, far enough to overshoot the points.

An arc is part of an ellipse. The rectangle you pass is the whole ellipse, not the bounds of the arc.
Angles are in degrees, measured from 3 o'clock, and a positive sweep turns clockwise.

```csharp demo=Vectors snippet=arc
```

## Paths

An `XGraphicsPath` collects lines, curves, arcs and shapes into one figure, which you then draw once
with `DrawPath`. Two figures in one path under the alternate fill mode give a shape with a hole.

```csharp demo=Vectors snippet=path-with-hole
```

`CloseFigure` joins the current figure's end to its start. `AddPath` appends another path, and its
`connect` argument decides whether the new path joins the current figure with a line or starts a
figure of its own. `AddArc` has an overload that takes a start point, an end point and radii, as SVG
does.

### Text as a shape

`AddString` adds the outlines of a string to a path. You can then fill the letters with a gradient
or clip to their shape. It needs a glyph outline provider from your backend, and throws until one is
registered:

```csharp
GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();

XGraphicsPath path = new XGraphicsPath();
// Use any family your font resolver serves.
path.AddString("Clip!", new XFontFamily("Arial"), XFontStyle.Bold, 90,
    new XRect(0, 0, 250, 140), XStringFormats.Center);
gfx.DrawPath(new XPen(XColors.Purple, 2), XBrushes.Orchid, path);
```

With PdfPinata.ImageSharp, use `ImageSharpGlyphOutlineProvider` instead. Both are in the
`PdfPinata.Utils` namespace. Text in a path is no longer text: readers cannot search or copy it. To
only outline text, use the `DrawString` overloads that take an `XPen` as well as a brush.

## Pens

An `XPen` has a colour or a brush, and a width in points. Its other properties are `LineCap`
(`Flat`, `Round`, `Square`), `LineJoin` (`Miter`, `Round`, `Bevel`), `MiterLimit`, `DashStyle` and
`DashPattern`.

```csharp demo=Vectors snippet=line-caps
```

`Round` and `Square` caps reach past the end point by half the pen's width, so a `Flat` line looks
shorter. A mitre on a sharp corner can reach far past it. `MiterLimit` sets how far it may reach
before the join is bevelled instead.

For your own dashes, set `DashPattern` to alternating dash and gap lengths, in multiples of the pen's
width. `DashOffset` moves the pattern along the line.

```csharp demo=Vectors snippet=dash-pattern
```

## Brushes and colours

`XSolidBrush` fills with one colour. `XLinearGradientBrush` blends between two colours along a line
or across a rectangle, and `XRadialGradientBrush` blends outwards from a centre.

```csharp demo=Vectors snippet=gradients
```

An `XPen` can take a brush in place of a colour, so a stroke can carry a gradient:

```csharp demo=Vectors snippet=pen-from-brush
```

### Transparency

`XColor.FromArgb(alpha, red, green, blue)` takes an alpha from 0 (transparent) to 255 (opaque). Any
pen or brush made from such a colour draws with transparency. A gradient can fade out if one of its
colours is transparent:

```csharp demo=Vectors snippet=fade-to-transparent
```

### CMYK colours

`XColor.FromCmyk(cyan, magenta, yellow, black)` takes four values from 0 to 1. A fifth, leading
argument sets the alpha, also from 0 to 1. By default a document converts every colour to RGB when it
writes it, CMYK colours included. To keep CMYK values as CMYK, set the document's colour mode:

```csharp
document.Options.ColorMode = PdfColorMode.Cmyk;

gfx.DrawRectangle(new XSolidBrush(XColor.FromCmyk(1, 0.68, 0, 0.12)), 30, 60, 50, 50);
```

`PdfColorMode.Cmyk` converts every colour to CMYK, including the RGB ones. `PdfColorMode.Undefined`
writes each colour in the colour space it was made in. `XColor.FromGrayScale` makes a grey.

## Transforms and graphics state

`TranslateTransform`, `ScaleTransform`, `RotateTransform` and `ShearTransform` change the coordinate
system for everything drawn after them. `RotateAtTransform` and `ScaleAtTransform` turn or scale about
a point. `MultiplyTransform` applies any `XMatrix`. Transforms add to each other, and their order
matters: rotation turns about the current origin, so move the origin first.

`Save` returns an `XGraphicsState`, and `Restore` with that state undoes every transform and clip set
since:

```csharp demo=Vectors snippet=rotate
```

`BeginContainer` and `EndContainer` work the same way. Use a container in a method that is handed an
`XGraphics`: the method puts the surface back as it found it, however many states its caller saved.

## Clipping

`IntersectClip` limits drawing to a rectangle or a path. The clip belongs to the graphics state, so
set it after a `Save` and remove it with the matching `Restore`:

```csharp demo=Vectors snippet=clip
```

## Things to know

- **One `XGraphics` per page at a time.** Creating a second one for a page throws until you dispose
  the first.
- **There is no reset.** The only way to undo a transform or a clip is `Restore` (or `EndContainer`).
  A `Save` without its `Restore` affects everything drawn after it.
- **The corner of a rounded rectangle is an ellipse size, not a radius.** `new XSize(30, 30)` gives
  corners with a 15-point radius.
- **CMYK needs the colour mode.** Without `ColorMode = PdfColorMode.Cmyk` (or `Undefined`), a CMYK
  colour is written as RGB.
- **PDF/A limits colour and transparency.** PDF/A-1 refuses transparency when you save. A CMYK
  document that claims PDF/A needs an output intent profile. See [PDF/A](../standards/pdf-a.md).

## See it in action

[The Vectors demo](../demos.mdx#vectors) draws every shape above on four pages, with the pen, brush
and transform settings side by side.

<details>
<summary>The full Vectors demo</summary>

```csharp demo=Vectors
```

</details>
