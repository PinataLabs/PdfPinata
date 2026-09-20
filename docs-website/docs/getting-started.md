---
title: Getting Started
description: Make your first PDF with PdfPinata, first by drawing on a page with XGraphics and then by laying out a document with PinataLayout.
demos: [HelloWorld]
---

This page makes a one-page PDF that says "Hola, mundo!". You make it twice: first by drawing on
the page with `XGraphics`, then by describing a document and letting PinataLayout lay it out.
[Overview](./overview.md#two-ways-to-make-a-page) explains when to use each way.

You need the .NET 8 SDK or later. The steps use the `PdfPinata.Skia` backend.

## Create the project

1. Create a console application:

   ```sh
   dotnet new console -o HelloPdf
   cd HelloPdf
   ```

2. Add the Skia backend. It brings in the core `PdfPinata` package:

   ```sh
   dotnet add package PdfPinata.Skia
   ```

3. If you work on Linux, add the SkiaSharp native library. Use the SkiaSharp version that
   `dotnet list package --include-transitive` shows:

   ```sh
   dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies --version 4.152.1
   ```

[Installation](./installation.md) explains the backends and the native libraries.

## Register the backend

Replace the contents of `Program.cs` with these lines. They must run before you create any font or
image:

```csharp
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Skia;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
```

`SkiaFontResolver` finds the fonts installed on the machine. `SkiaImageSource` is not needed for
this page, because the page has no images, but most applications need it soon.

## Draw a page with XGraphics

1. Create the document and fill in its properties. A PDF reader shows these under "document
   properties". Add these lines below the registration:

   ```csharp demo=HelloWorld snippet=new-document
   ```

2. Add a page, get an `XGraphics` for it, and draw the string:

   ```csharp demo=HelloWorld snippet=draw-text
   ```

   The demo uses "Liberation Sans", a font that the demo app carries with it. Change the family
   name to a font that is installed on your machine, for example "Arial" on Windows.

3. Save the document. Add this line at the end of `Program.cs`:

   ```csharp
   document.Save("HelloWorld.pdf");
   ```

4. Run the application:

   ```sh
   dotnet run
   ```

5. Open `HelloWorld.pdf`. It is in the project folder.

`XGraphics` measures in points, 1/72 inch, from the top-left corner of the page. `page.Width.Point`
gives the page width in points.

A new page gets its size from the region settings of the machine: A4 where the region uses metric
units, and US Letter where it does not. If the size matters, set it on the page before you draw.
See [Pages and orientation](./drawing/pages-and-orientation.md).

## Lay out the same page with PinataLayout

With PinataLayout you do not draw at coordinates. You add sections and paragraphs, and the renderer
places them on pages.

1. Add the PinataLayout renderer to the project:

   ```sh
   dotnet add package PinataLayout.Rendering
   ```

2. Replace the contents of `Program.cs` with this program:

   ```csharp
   using PdfPinata.Fonts;
   using PdfPinata.Skia;
   using PdfPinata.Utils;
   using PinataLayout.DocumentObjectModel;
   using PinataLayout.DocumentObjectModel.Shapes;
   using PinataLayout.Rendering;

   GlobalFontSettings.FontResolver = new SkiaFontResolver();
   ImageSource.ImageSourceImpl = new SkiaImageSource();

   Document document = new Document();
   document.Info.Title = "Hola, mundo!";

   Section section = document.AddSection();
   Paragraph paragraph = section.AddParagraph("Hola, mundo!");
   paragraph.Format.Font.Size = 30;
   paragraph.Format.Font.Bold = true;

   PdfDocumentRenderer renderer = new PdfDocumentRenderer(true) { Document = document };
   renderer.RenderDocument();
   renderer.PdfDocument.Save("HelloLayout.pdf");
   ```

3. Run the application and open `HelloLayout.pdf`.

A `Document` holds one or more sections. By default each section starts on a new page, and each
has its own page setup, headers and footers. PinataLayout pages are A4 unless you change the page
setup. `PdfDocumentRenderer` lays out the document and writes the pages into
`renderer.PdfDocument`, which is an ordinary `PdfDocument`. You can draw on its pages with
`XGraphics` before you save it.

The `true` argument writes text as Unicode, so any character that the font contains can appear.
Every demo passes `true`.

## Things to know

- **Register the backend first.** `new XFont(...)` and `new Document()` both throw an
  `InvalidOperationException` if no font resolver is registered. After the first font exists, you
  cannot change the resolver.
- **An unknown font family does not cause an error.** If `SkiaFontResolver` cannot find the family
  you ask for, it uses another installed font. The PDF looks wrong, but nothing tells you. On a
  server or in a container, few fonts or none are installed. Read [Fonts](./fonts-and-text/fonts.md)
  before you deploy.
- **PinataLayout's default font is Arial.** The `Normal` style uses the font resolver's default
  family, which is "Arial" for the Skia and ImageSharp resolvers. To use another family for the
  whole document, set `document.Styles[StyleNames.Normal].Font.Name`.
- **`DrawString` places text differently with a rectangle and with a point.** With an `XRect` and
  `XStringFormats.Center`, the string is centred in the rectangle. With an `XPoint` and no format,
  the point is where the text's baseline starts, not its top.
- **Namespaces are `PdfPinata.*` and `PinataLayout.*`.** Code from PDFsharp or MigraDoc examples
  uses `PdfSharp.*` and `MigraDoc.*`. Change the `using` lines. See
  [Migrating](./reference/migrating.md).
- **PinataLayout tags its output by default.** The PDF carries a structure tree for accessibility.
  A tagged document cannot be resized with `PdfPage.Resize`. To turn tagging off, set
  `renderer.TagContent = false`.

## Where next

- [Text](./drawing/text.md) and [Shapes, pens and brushes](./drawing/shapes-pens-and-brushes.md)
  for more about drawing.
- [Documents, sections and styles](./layout/documents-sections-and-styles.md) and
  [Paragraphs and text layout](./layout/paragraphs-and-layout.md) for more about PinataLayout.
- [Fonts](./fonts-and-text/fonts.md) to control which font files your documents use.

## See it in action

[The HelloWorld demo](./demos.mdx#helloworld) draws the page from this guide and prints every
document property on the page as well.

<details>
<summary>The full HelloWorld demo</summary>

```csharp demo=HelloWorld
```

</details>
