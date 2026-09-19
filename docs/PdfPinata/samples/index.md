# PdfPinata > Samples

Samples for [PdfPinata](../index.md).

These pages came from upstream PDFsharp and are prose: nothing builds them and nothing runs them,
so they say what was true when they were written. **`SampleApp` is the executable version.** Every
demo in it is built on every commit and its page count is asserted by `DemoSmokeTests`, so a demo
that stops working fails the build; a code block on one of these pages cannot.

```powershell
dotnet run --project src/SampleApp -- list      # what the demos cover
dotnet run --project src/SampleApp -- run       # write one PDF per demo into src/SampleApp/output
dotnet run --project src/SampleApp -- run -e Vectors    # just the one
```

Each demo prints the source that drew its PDF. It comes off disk where the file is there to read -
a checkout, in other words - and out of the assembly otherwise, which is what a published binary
and any other machine get. Editing a demo's source without rebuilding is the one way to make the
panel disagree with what ran.

## Before any of this runs

The core package carries no imaging or font dependency of its own, so a fresh program has two seams
to fill in before it can draw anything. Both throw a descriptive `InvalidOperationException` when
read unset, and none of the sample code below shows them being set:

```cs
using PdfPinata.Fonts;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;

GlobalFontSettings.FontResolver = new SkiaFontResolver();     // PdfPinata.Skia
ImageSource.ImageSourceImpl = new SkiaImageSource();          // and the same for images
```

`ImageSource` is a trap for the eye: it ships in the **PdfPinata** assembly but its namespace is
`PinataLayout.DocumentObjectModel.Shapes`, so registering it needs that `using` from code that otherwise has nothing to do with MigraDoc. `PdfPinata.ImageSharp` supplies
`ImageSharpFontResolver` and `ImageSharpImageSource` in place of the Skia pair.

The font resolver must be set **before any font is created** — the setter throws once one has been.

A second consequence for the code below: several samples ask for `new XFont("Verdana", 16)`, which
works only if the resolver you installed can find Verdana. `SkiaFontResolver` asks the machine, so
that line works on a Windows desktop and does not on a Linux build agent with no fonts installed at
all. The demos serve their own faces as embedded resources for exactly this reason, and so does
[Font Resolver](FontResolver.md).

Two more things the sample code assumes and this repository does not provide:

- **`../../../../../PDFs/…`** is upstream's tree of sample PDFs, which is not part of this fork.
  Any page that copies a file out of it needs a PDF of your own put in its place.
- **`PdfSharp.*` namespaces.** Code copied from PDFsharp's own documentation reads `using
  PdfSharp.Drawing;`; here it is `PdfPinata.Drawing`, and the same for every other namespace and
  for `PdfPinata.ProductVersionInfo`.

## The samples, and the demo that covers each

| sample | runnable version |
|---|---|
| [Hello World](HelloWorld.md) | `HelloWorld` |
| [Graphics](Graphics.md) | `Vectors` |
| [Annotations](Annotations.md) | `Annotations` |
| [Booklet](Booklet.md) | `Imposition` |
| [Bookmarks](Bookmarks.md) | `Outline`, and `Structure` for the MigraDoc route |
| [Colors CMYK](ColorsCMYK.md) | `Compress`, which measures what `ColorMode` costs |
| [Combine Documents](CombineDocuments.md) | `Assemble` |
| [Concatenate Documents](ConcatenateDocuments.md) | `Assemble` |
| [Export Images](ExportImages.md) | none |
| [Font Resolver](FontResolver.md) | `Fonts` |
| [Multiple Pages](MultiplePages.md) | `Layout` |
| [Page Sizes](PageSizes.md) | `Orientation`, and `PageResize` for changing one afterwards |
| [Preview](Preview.md) | none — see the page |
| [Protect Document](ProtectDocument.md) | `Protect` |
| [Unprotect Document](UnprotectDocument.md) | `Protect` |
| [Split Document](SplitDocument.md) | `Assemble` |
| [Text Layout](TextLayout.md) | `Text` |
| [Two Pages on One](TwoPagesOnOne.md) | `Imposition` |
| [Unicode](Unicode.md) | `Unicode` |
| [Watermark](Watermark.md) | `Imposition` |
| [Work on Pdf Objects](WorkOnPdfObjects.md) | `Inspect`, and `Navigation` for the open action |
| [XForms](XForms.md) | `Imposition` |
| [Clock](Clock.md) | `Vectors` for the drawing; the ASP.NET part has no equivalent |

## What the demos cover that no sample does

Thirteen of the thirty demos have no page here at all, because upstream never wrote one:
`Images`, `ImageFailures`, `Barcodes`, `Charts`, `Tables`, `Bleed`, `Forms`, `Invoice`, `Ddl`,
`Footnotes`, and the three combined layouts `Newspaper`, `Magazine` and `SideWrap`.

`dotnet run --project src/SampleApp -- list` is the current list. This table is a snapshot and the list
is not.
