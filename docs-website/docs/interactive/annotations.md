---
title: Annotations
description: Add notes, links, highlights, shapes, stamps and file attachments to PDF pages, and place them in the right coordinates.
demos: [Annotations]
---

An annotation is an object that sits on top of a page rather than in its content: a sticky note, a
link, a highlight, a stamp, an attached file. A reader draws it, and the person reading can often
open, hide, move or print it separately from the page. Use an annotation when the mark is a comment
on the page or something to click. When the mark is part of the page itself, draw it with `XGraphics`
instead.

Every annotation type is in the `PdfPinata.Pdf.Annotations` namespace of the core `PdfPinata`
package. You add one to a page with `page.Annotations.Add(annotation)`.

## Place an annotation on the page

An annotation's position is in PDF's own page coordinates: points measured **up** from the
**bottom-left** corner of the page. `XGraphics` draws in coordinates measured down from the top left.
To convert, use the graphics object's transformer:

- `gfx.Transformer.WorldToDefaultPage(XRect)` returns the rectangle in page coordinates. Wrap it in a
  `PdfRectangle` to set `Rectangle`.
- `gfx.Transformer.WorldToDefaultPage(XPoint)` converts a single point. Use it for the two ends of a
  line annotation.

The conversion also applies any transform you have set on the `XGraphics`, so the annotation lands on
what you drew at the same coordinates.

## Notes

A `PdfTextAnnotation` is a sticky note. The reader draws its icon and shows `Contents` in a pop-up.
`Title` is the label in the pop-up's title bar, usually the author's name.

```csharp demo=Annotations snippet=note
```

`Icon` takes a `PdfTextAnnotationIcon`: `Comment`, `Help`, `Insert`, `Key`, `NewParagraph`, `Note` or
`Paragraph`. `Open = true` shows the pop-up when the document opens. `Color` tints the note, and
`Opacity` applies to the whole annotation.

## Links

`XGraphics` has one method per kind of link. Each takes a rectangle in drawing coordinates and
converts it for you:

```csharp demo=Annotations snippet=links
```

- `AddWebLink(rect, url)` opens a web address.
- `AddDocumentLink(rect, pageNumber)` goes to a page of the same document. The page number starts
  at 1.
- `AddNamedLink(rect, name)` goes to a named destination. Create the destination with
  `gfx.AddNamedDestination(name, point)` on the page it belongs to.

Each method returns the `PdfLinkAnnotation`, so you can set `Contents` on it afterwards. `PdfPage` has
the same methods, taking a `PdfRectangle` in page coordinates, plus `AddFileLink` for a file on disk.
`PdfPage.AddDocumentLink(rect, pageNumber, destinationTop)` also says how far down the target page to
land. A link is only a clickable area: draw the text and any underline yourself.

## Highlight, underline and strike out

Four classes mark a run of text: `PdfHighlightAnnotation`, `PdfUnderlineAnnotation`,
`PdfStrikeOutAnnotation` and `PdfSquigglyAnnotation`. Each covers one or more quadrilaterals that you
add with `AddQuad`, in page coordinates. The demo measures the run of text with `MeasureString`,
converts the box, and adds it:

```csharp demo=Annotations snippet=mark
```

Text that wraps onto a second line needs one quadrilateral per line. Add them all to the same
annotation; the annotation's rectangle grows to enclose them.

```csharp demo=Annotations snippet=two-quads
```

`Color` and `Opacity` belong to the annotation, not to the page, so you can mark the same text twice.
A highlight is drawn in the Multiply blend mode, so the text shows through the colour. If you add no
quadrilaterals, the annotation marks its `Rectangle`. `ClearQuads` removes them all.

## Shapes, lines and free text

Readers draw `/Square`, `/Circle`, `/Line` and `/FreeText` annotations only from an appearance stream,
so PdfPinata draws that stream for you and redraws it when you change a property. None of the demos
shows these four, so here is a short example:

```csharp
// A rectangle with a red border and a pale fill.
PdfSquareAnnotation box = new PdfSquareAnnotation(document)
{
    Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(new XRect(56, 600, 200, 80))),
    Color = XColors.Firebrick,
    Interior = XColors.MistyRose,
    BorderWidth = 2,
    Contents = "Check these figures",
};
page.Annotations.Add(box);

// An arrow between two points.
PdfLineAnnotation arrow = new PdfLineAnnotation(document);
arrow.SetLine(
    gfx.Transformer.WorldToDefaultPage(new XPoint(300, 700)),
    gfx.Transformer.WorldToDefaultPage(new XPoint(420, 640)));
arrow.EndEnding = PdfLineEnding.ClosedArrow;
arrow.Interior = XColors.Black;
page.Annotations.Add(arrow);

// Text shown on the page itself rather than in a pop-up.
PdfFreeTextAnnotation label = new PdfFreeTextAnnotation(document)
{
    Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(new XRect(56, 720, 220, 40))),
    Font = new XFont("Arial", 10),   // any family your font resolver serves
    TextColor = XColors.DarkBlue,
    Contents = "Figures from the March survey.",
};
page.Annotations.Add(label);
```

- `PdfSquareAnnotation` and `PdfCircleAnnotation` fill their `Rectangle`. `Color` is the border,
  `Interior` the fill and `BorderWidth` the border width in points. A circle is an ellipse that fits
  the rectangle.
- `PdfLineAnnotation` goes from `Start` to `End`. `SetLine` moves both at once. `StartEnding` and
  `EndEnding` take a `PdfLineEnding`, such as `OpenArrow`, `ClosedArrow`, `Circle` or `Diamond`, and
  `Interior` fills them.
- `PdfFreeTextAnnotation` draws `Contents` in `Font` and `TextColor`, wrapped to its rectangle.
  `Alignment` takes an `XParagraphAlignment` (from `PdfPinata.Drawing.Layout`). `Color` is the
  background, and the border uses `TextColor`.

## Stamps

A `PdfRubberStampAnnotation` shows one of 15 standard stamps, such as `Draft`, `Approved`,
`Confidential` or `Final`. The reader draws the stamp.

```csharp demo=Annotations snippet=stamp
```

For a stamp with your own artwork, draw it on an `XForm` with `XGraphics.FromForm` and pass the form
to `SetAppearance`.

## File attachments

A `PdfFileAttachmentAnnotation` carries a file inside the PDF and shows an icon the reader can open
it from. `PdfEmbeddedFile` holds the bytes, `PdfFileSpecification` names them, and the annotation
points at the specification. Both types are in `PdfPinata.Pdf.Advanced`.

```csharp demo=Annotations snippet=attachment
```

`Icon` takes `Graph`, `PushPin`, `Paperclip` or `Tag`. The constructor sets
`PdfAnnotationFlags.Locked`, so a reader will not let the person drag the icon away.

## Other annotation types

`PdfGenericAnnotation` takes any subtype name, for example
`new PdfGenericAnnotation(document, "/Polygon")`. Write its entries through `Elements`, and give it a
drawing with `SetAppearance`.

`Flags` takes `PdfAnnotationFlags` on any annotation: `Hidden`, `Print`, `NoZoom`, `NoRotate`,
`NoView`, `ReadOnly`, `Locked` and others.

## Things to know

- **Coordinates go up from the bottom.** Every rectangle and point is in page coordinates. If an
  annotation appears in the wrong place, check that you converted it with
  `gfx.Transformer.WorldToDefaultPage`.
- **Some annotations are drawn by the reader.** Note icons, attachment icons and standard stamps have
  no drawing in the file. A renderer that draws only appearance streams shows nothing for them.
- **A line sets its own rectangle.** `PdfLineAnnotation` works out `Rectangle` from its ends and line
  endings. If you assign `Rectangle`, the line overwrites it.
- **An empty shape removes its drawing.** A square or circle with no border and no fill, or a shape
  smaller than one point in either direction, has no appearance.
- **Free text needs a font resolver.** A `PdfFreeTextAnnotation` draws text, so a font resolver must be
  registered before it is added to a page. See [Installation](../installation.md).
- **Annotations read from a file are generic.** In a document you opened, `page.Annotations[index]`
  returns a `PdfGenericAnnotation`, not the typed class. Its `Subtype` property says what kind it is,
  and `Elements` holds its entries.
- **Justified free text is stored as left-aligned.** PDF has no code for justified text in a free text
  annotation. PdfPinata draws it justified, but a reader that redraws it aligns it left.
- **Annotations are appended.** `page.Annotations` has `Add` and `Remove`, but no `Insert`.
- **Not everything is covered.** There are no classes for polygon, polyline, ink or pop-up
  annotations. A line has no caption or leader lines, and free text has no callout line.

## See it in action

[The Annotations demo](../demos.mdx#annotations) marks up text four ways, adds notes with every icon,
three kinds of link, a file attachment and four stamps.

:::note
The demo's last page compares PdfPinata with PDFKit and lists line and free text annotations as
missing. That entry is out of date: `PdfLineAnnotation` and `PdfFreeTextAnnotation` are available,
as shown above.
:::

<details>
<summary>The full Annotations demo</summary>

```csharp demo=Annotations
```

</details>
