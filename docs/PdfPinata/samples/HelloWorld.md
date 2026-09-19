# Hello World

> **Runnable version:** the `HelloWorld` demo.
> `dotnet run --project src/SampleApp -- run -e HelloWorld`
>
> The demos are built on every commit and their page counts are asserted by
> `DemoSmokeTests`, so one that stops working fails the build. The code on this page is
> prose and has no such protection. See
> [Before any of this runs](index.md#before-any-of-this-runs) - this fork needs a backend
> registered before it will draw anything.

Is the obligatory "Hello World" program.


## Code

```cs
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Utils;

// The one line the upstream sample has no need of. PdfPinata carries no font dependency
// of its own, so a backend has to supply the resolver, and it must be set before any font is
// created - the setter throws once one has been.
GlobalFontSettings.FontResolver = new SkiaFontResolver();

// Create a new PDF document
PdfDocument document = new PdfDocument();
document.Info.Title = "Created with PdfPinata";

// Create an empty page
PdfPage page = document.AddPage();

// Get an XGraphics object for drawing
XGraphics gfx = XGraphics.FromPdfPage(page);

// Create a font. Whether a family name resolves is the resolver's business, not the
// library's: SkiaFontResolver asks the machine, so "Verdana" works on Windows and does not
// on a Linux agent with no fonts installed. A program that must lay out identically
// everywhere ships its own faces - see FontResolver.md.
XFont font = new XFont("Verdana", 20, XFontStyle.BoldItalic);

// Draw the text
gfx.DrawString(
    "Hello, World!", font, XBrushes.Black,
    new XRect(0, 0, page.Width, page.Height),
    XStringFormats.Center);

// Save the document...
document.Save("HelloWorld.pdf");
```

The namespaces are `PdfPinata.*`, not `PdfSharp.*` — this is a fork, and code copied from
PDFsharp's own documentation will not compile until the `using` lines are changed.
