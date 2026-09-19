# Annotations

> **Runnable version:** the `Annotations` demo.
> `dotnet run --project src/SampleApp -- run -e Annotations`
>
> The demos are built on every commit and their page counts are asserted by
> `DemoSmokeTests`, so one that stops working fails the build. The code on this page is
> prose and has no such protection. See
> [Before any of this runs](index.md#before-any-of-this-runs) - this fork needs a backend
> registered before it will draw anything.

This sample shows how to create PDF annotations.

PdfPinata supports the creation of the following annotations:
* [Text annotations](#text-annotations)
* [Text annotations opened](#text-annotations-opened)
* [Rubber stamp annotations](#rubber-stamp-annotations)
* [Text markup annotations](#text-markup-annotations) — highlight, underline, strike out and squiggly


## Text annotations

```cs
// Create a PDF text annotation
PdfTextAnnotation textAnnot = new PdfTextAnnotation();
textAnnot.Title = "This is the title";
textAnnot.Subject = "This is the subject";
textAnnot.Contents = "This is the contents of the annotation.\rThis is the 2nd line.";
textAnnot.Icon = PdfTextAnnotationIcon.Note;
 
gfx.DrawString("The first text annotation", font, XBrushes.Black, 30, 50, XStringFormats.Default);
 
// Convert rectangle from world space to page space. This is necessary because the annotation is
// placed relative to the bottom left corner of the page with units measured in point.
XRect rect = gfx.Transformer.WorldToDefaultPage(new XRect(new XPoint(30, 60), new XSize(30, 30)));
textAnnot.Rectangle = new PdfRectangle(rect);
 
// Add the annotation to the page
page.Annotations.Add(textAnnot);
```


## Text annotations opened
```cs
// Create another PDF text annotation which is open and transparent
textAnnot = new PdfTextAnnotation();
textAnnot.Title = "Annotation 2 (title)";
textAnnot.Subject = "Annotation 2 (subject)";
textAnnot.Contents = "This is the contents of the 2nd annotation.";
textAnnot.Icon = PdfTextAnnotationIcon.Help;
textAnnot.Color = XColors.LimeGreen;
textAnnot.Opacity = 0.5;
textAnnot.Open = true;
 
gfx.DrawString("The second text annotation (opened)", font, XBrushes.Black, 30, 140, XStringFormats.Default);
 
rect = gfx.Transformer.WorldToDefaultPage(new XRect(new XPoint(30, 150), new XSize(30, 30)));
textAnnot.Rectangle = new PdfRectangle(rect);
 
// Add the 2nd annotation to the page
page.Annotations.Add(textAnnot);
```


## Rubber stamp annotations

```cs
// Create a so called rubber stamp annotation. I'm not sure if it is useful, but at least
// it looks impressive...
PdfRubberStampAnnotation rsAnnot = new PdfRubberStampAnnotation();
rsAnnot.Icon = PdfRubberStampAnnotationIcon.TopSecret;
rsAnnot.Flags = PdfAnnotationFlags.ReadOnly;
 
rect = gfx.Transformer.WorldToDefaultPage(new XRect(new XPoint(100, 400), new XSize(350, 150)));
rsAnnot.Rectangle = new PdfRectangle(rect);
 
// Add the rubber stamp annotation to the page
page.Annotations.Add(rsAnnot);
```


## Text markup annotations

These mark up a run of text on the page: `PdfHighlightAnnotation` washes it with colour,
`PdfUnderlineAnnotation` rules a line beneath it, `PdfStrikeOutAnnotation` rules one through it, and
`PdfSquigglyAnnotation` rules a wavy one beneath it. All four are used the same way.

```cs
gfx.DrawString("Hello world!", font, XBrushes.Black, 30, 42, XStringFormats.Default);

var highlight = new PdfHighlightAnnotation();
highlight.Title = "This is the title";
highlight.Contents = "This is the contents of the annotation.";
highlight.Color = XColors.Yellow;

// Convert the band to cover from world space to page space, as for the other annotations.
highlight.AddQuad(gfx.Transformer.WorldToDefaultPage(new XRect(new XPoint(30, 30), new XSize(70, 16))));

page.Annotations.Add(highlight);
```

A run of text is not a rectangle — it wraps — so what is marked up is a list of quadrilaterals
rather than a single box, one per line or per word. Call `AddQuad` once for each:

```cs
var strikeOut = new PdfStrikeOutAnnotation();
foreach (var line in linesOfTheParagraph)
    strikeOut.AddQuad(gfx.Transformer.WorldToDefaultPage(line));
page.Annotations.Add(strikeOut);
```

The annotation rectangle is then the box enclosing the quadrilaterals, and is kept up to date for
you. Setting `Rectangle` and adding no quadrilaterals at all marks up that rectangle alone, which is
the simple case; adding one takes over from it.

`Opacity` applies as it does to the other annotations. A highlight is drawn under the Multiply blend
mode, so the text keeps showing through the colour rather than being painted over by it.

---

PDF supports some more pretty types of annotations like PdfLineAnnotation, PdfSquareAnnotation,
PdfCircleAnnotation, PdfSoundAnnotation, or PdfMovieAnnotation.
If you need one of them, feel encouraged to implement it. It is quite easy.
