---
title: Gradients
description: Fill shapes with linear and radial gradients, extend them past their ends, and move or stretch them with a transform of their own.
demos: [Gradients]
---

A gradient brush blends between two colours. `XLinearGradientBrush` blends along a line, and
`XRadialGradientBrush` blends outwards from one circle to another. Use a gradient brush anywhere a
brush goes: to fill a shape, a path or text, or as the brush of an `XPen`. Both brushes are in the
core `PdfPinata` package and need no backend.

A gradient is written into the PDF as a shading pattern: type 2 (axial) for a linear gradient and
type 3 (radial) for a radial one. A reader draws the blend itself, so it stays smooth at every zoom.

## Radial gradients

`XRadialGradientBrush` takes a centre, two radii and two colours. The first colour is on the circle
with the first radius, and the second colour is on the circle with the second radius. With a first
radius of 0, the first colour is a point at the centre:

```csharp demo=Gradients snippet=radial
```

The first radius can be larger than the second. The blend then runs inwards.

### Two centres

The other constructor takes a separate centre for each circle. Put the first circle, a point, off
the centre of the second to make a highlight, as on a lit sphere:

```csharp demo=Gradients snippet=two-centres
```

Keep the first circle inside the second. If it is not, the shape of the blend becomes a cone, which
is correct PDF but seldom what you want.

## Extending past the ends

A gradient paints nothing before its start or after its end. For a radial gradient, the start is the
first circle and the end is the second circle. So a radial gradient that fills a rectangle leaves the
corners outside its outer circle unpainted. Set `ExtendRight` to paint them in the second colour:

```csharp demo=Gradients snippet=extend-right
```

`ExtendLeft` does the same at the start. A first radius above 0 leaves a hole inside the first
circle, and `ExtendLeft` fills it with the first colour:

```csharp demo=Gradients snippet=extend-left
```

Both properties are on `XLinearGradientBrush` too. There, the start is the first point and the end is
the second point:

```csharp demo=Gradients snippet=linear-extend
```

Both properties are `false` by default, and they have the same names as in PDFsharp.

## Transforms

The gradient's points and radii are in the same coordinates as the shape you fill. The transform of
the `XGraphics` applies to the brush in the same way as to the shape, so a gradient drawn under a
`ScaleTransform` or a `RotateTransform` scales and turns with its shape.

A gradient brush also has a transform of its own. Set `Transform`, or call `TranslateTransform`,
`ScaleTransform`, `RotateTransform` or `MultiplyTransform` on the brush. This transform applies to
the brush alone, before the transform of the `XGraphics`. Use it to stretch the circles of a radial
gradient into ellipses:

```csharp demo=Gradients snippet=radial-transform
```

or to turn a linear gradient without calculating new end points:

```csharp demo=Gradients snippet=linear-transform
```

A brush's rotation turns about the origin, not about the shape. To turn a brush about a point, move
that point to the origin first and back afterwards, as the example does.

## Transparency

Either colour of a gradient can have an alpha. The gradient then blends from one transparency to the
other, as well as from one colour to the other:

```csharp demo=Gradients snippet=radial-fade
```

A gradient with transparency is drawn through a soft mask, which PDF/A-1 does not allow. See
[PDF/A](../standards/pdf-a.md).

## Things to know

- **Only two colours.** A gradient blends between two colours. For more colour stops, draw several
  gradients next to each other, or nest radial gradients with matching radii.
- **Unpainted means unpainted.** Without `ExtendLeft` or `ExtendRight`, the parts of the shape past
  the gradient's ends show what was on the page before, not white.
- **The same brush can fill many shapes.** The gradient's coordinates are page coordinates, not
  coordinates relative to the shape. Two rectangles filled with the same brush show two parts of one
  gradient.

## See it in action

[The Gradients demo](../demos.mdx#gradients) draws every gradient on this page side by side, with
and without extension. [Shapes, pens and brushes](shapes-pens-and-brushes.md) covers the other
brushes and the shapes they fill.

<details>
<summary>The full Gradients demo</summary>

```csharp demo=Gradients
```

</details>
