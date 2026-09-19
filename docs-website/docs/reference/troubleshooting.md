---
title: Troubleshooting
description: Common PdfPinata errors and surprises, organised by what you see, with the cause and the fix for each.
---

Each section starts with what you see: an exception message or a symptom in the output. Messages
are quoted as the library writes them, so you can search this page for the text of your error.

## Setup and fonts

### "No IFontResolver has been configured"

```text
InvalidOperationException: No IFontResolver has been configured. Set GlobalFontSettings.FontResolver
before performing any font operation, e.g. 'GlobalFontSettings.FontResolver = new SkiaFontResolver();'
from the PdfPinata.Skia package.
```

The core package cannot read fonts on its own. Register a backend once, when your application
starts, before any code creates an `XFont` or renders a PinataLayout document:

```csharp
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
```

`SkiaFontResolver` is in the `PdfPinata.Utils` namespace and `SkiaImageSource` is in
`PdfPinata.Skia`. `ImageSource` ships in the core package but its namespace is
`PinataLayout.DocumentObjectModel.Shapes`. See [Installation](../installation.md).

### "No ImageSource implementation has been configured"

```text
InvalidOperationException: No ImageSource implementation has been configured. Set
ImageSource.ImageSourceImpl before loading images, e.g. 'ImageSource.ImageSourceImpl = new
SkiaImageSource();' from the PdfPinata.Skia package.
```

The font resolver is registered, but the image decoder is not. Set `ImageSource.ImageSourceImpl`
as shown above.

### "No IGlyphOutlineProvider has been configured"

Only `XGraphicsPath.AddString` needs a glyph outline provider. If you add text to a path, set
`GlobalFontSettings.GlyphOutlineProvider` to `new SkiaGlyphOutlineProvider()` or
`new ImageSharpGlyphOutlineProvider()`.

### "Must not change font resolver after is was once used."

You set `GlobalFontSettings.FontResolver` to a new resolver after a font was already created. The
resolver cannot change once a font exists, because fonts are cached against it. Set it once, at
startup. Setting the same instance again is allowed and does nothing.

### "No Fonts installed on this device!"

This `FileNotFoundException` comes from the backend's resolver. It found no font files in the
operating system's font folders, which is common in minimal Docker images. Do one of these:

1. Install a fonts package in the image.
2. Give the resolver your own font files before the first font is created:

```csharp
var resolver = new SkiaFontResolver();
resolver.SetupFontsFiles(new[] { "Fonts/NotoSans-Regular.ttf", "Fonts/NotoSans-Bold.ttf" });
GlobalFontSettings.FontResolver = resolver;
```

3. Write your own `IFontResolver` that serves font bytes from your application.

### Text comes out in the wrong font

When the resolver does not know the family name you asked for, it uses the first family it found
and does not throw. Check the spelling of the family name and check that the font is installed
where the resolver can find it. To make an unknown family throw `InvalidOperationException` when
you create the `XFont`, set `NullIfFontNotFound = true` on the resolver. See
[Fonts](../fonts-and-text/fonts.md).

### DllNotFoundException or TypeInitializationException from SkiaSharp or HarfBuzzSharp

SkiaSharp and HarfBuzzSharp call native libraries (`libSkiaSharp`, `libHarfBuzzSharp`). The
PdfPinata packages do not choose the native assets for you, so a missing native library often shows
up on Linux or in a container as a `TypeInitializationException` with a `DllNotFoundException`
inside it. Reference the native assets package for each platform you deploy to, such as
`SkiaSharp.NativeAssets.Linux.NoDependencies` and `HarfBuzzSharp.NativeAssets.Linux`. Use
`SkiaSharp.NativeAssets.Linux` instead if the target has `libfontconfig1`. Keep the native assets
version the same as the SkiaSharp or HarfBuzzSharp version that PdfPinata brings in. See
[Platforms and deployment](./platforms-and-deployment.md).

### NU1107 or NU1608 about SixLabors.ImageSharp

`PdfPinata.ImageSharp` needs ImageSharp 2.1.x and declares the range `[2.1.13,3.0.0)`. If another
package brings in ImageSharp 3, NuGet reports `NU1107`. If your project references ImageSharp 3
directly, NuGet uses your version and reports the warning `NU1608`. Treat that warning as an error,
because ImageSharp 3 is not binary compatible with 2.1:

```xml
<WarningsAsErrors>$(WarningsAsErrors);NU1608</WarningsAsErrors>
```

If ImageSharp 3 loads anyway, the backend throws an `InvalidOperationException` that starts
"PdfPinata.ImageSharp is built against SixLabors.ImageSharp 2.1.x, but version ... was loaded at
runtime." If your application needs ImageSharp 3, use `PdfPinata.Skia`, which does not depend on
ImageSharp.

## Text

### Characters show as empty boxes

The font has no glyph for those characters, so the PDF shows the font's `.notdef` glyph, which is
usually a box. Choose a font that covers the script, or register a fallback list so that PdfPinata
tries other families for characters the main font lacks:

```csharp
GlobalFontSettings.FontFallback = new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

See [Unicode and font embedding](../fonts-and-text/unicode-and-embedding.md).

### Arabic letters are not joined, or Indic text looks wrong

The core package puts right-to-left text in the correct order, but it maps each character to one
glyph. Joined Arabic forms, Indic reordering, ligatures and kerning need a text shaper. Install
`PdfPinata.HarfBuzz`, reference the HarfBuzzSharp native assets, and register the shaper at startup:

```csharp
GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();
```

See [International text](../fonts-and-text/international-text.md).

### Newlines are ignored by DrawString

`XGraphics.DrawString` draws one line. It drops `\n` and `\r`, and it draws a tab as one space.
`MeasureString` does split at line breaks, so the two can disagree. To draw several lines, use
`XTextFormatter` from `PdfPinata.Drawing.Layout`, or PinataLayout:

```csharp
var formatter = new XTextFormatter(gfx);
formatter.DrawString("First line\nSecond line", font, XBrushes.Black, new XRect(40, 40, 300, 200));
```

See [Text layout](../drawing/text-layout.md).

## Existing documents

### "This document was opened with PdfDocumentOpenMode.X and ... needs a document opened with ..."

```text
InvalidOperationException: This document was opened with PdfDocumentOpenMode.Import and saving the
document needs a document opened with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.
```

The open mode you pass to `PdfReader.Open` decides what you can do with the document. `Import` and
`ReadOnly` documents cannot be changed or saved. Open with `Modify` to change and save a document,
or with `Append` to save it incrementally. To copy pages out of a document, open it with `Import`;
otherwise you get "A PDF document must be opened with PdfDocumentOpenMode.Import to import pages
from it." See [Opening documents](../existing-pdfs/opening-documents.md).

### "This document was certified with PdfCertificationLevel.X and ... is not permitted by that certification."

The document carries a certifying signature that forbids the change you tried. A certification
level can allow no changes, form filling only, or form filling and annotations. A full `Save` is
never allowed on a certified document. See [Digital signatures](../standards/digital-signatures.md).

### A signature is invalid after I save the document

`PdfDocument.Save` rewrites the whole file from the object model. That changes the signed bytes, so
every existing signature stops verifying, and earlier revisions are lost. To add to a signed
document, open it with `PdfDocumentOpenMode.Append` and call `SaveIncremental`, which appends a new
revision and leaves the signed bytes alone. To sign, write the output through `PdfSigner.Sign`
rather than `Save`. See [Incremental saving](../existing-pdfs/incremental-saving.md).

### "Only a document opened with PdfDocumentOpenMode.Append can be saved incrementally."

`SaveIncremental` needs a document opened with `Append`. A document opened with `Modify` has its
objects renumbered, and a new document has nothing to append to. `SaveIncremental` also needs an
empty output stream; it cannot write back into the stream the document was read from.

### PdfPage.Resize refuses the document

`Resize` and `ResizePages` throw an `InvalidOperationException` in these cases:

- "This document is tagged." Resizing moves the page content into a form XObject, which breaks the
  structure tree. If the document comes from PinataLayout and you do not need the tags, set
  `TagContent = false` on the `PdfDocumentRenderer` before you render.
- "This document is signed." Resizing would invalidate the signature.
- "This document is encrypted." A document read from an encrypted file cannot be resized. A new
  document that you have set a password on can.
- "An XGraphics object is open on this page." Dispose of the `XGraphics` first.
- The document was opened in `ReadOnly` or `Import` mode. Open it in `Modify` mode.

### "Size cannot be set on a page that already has content on it"

Setting `Width`, `Height` or `Size` on a page writes a new media box and does not move the content,
so the page would be cropped. Call `PdfPage.Resize` to scale the content into the new size. See
[Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).

## Standards

### PDF/A is refused for a CMYK document

```text
InvalidOperationException: A PDF/A document has to embed an ICC profile saying what its colours
mean, and CMYK numbers mean nothing at all without one ...
```

PdfPinata supplies an sRGB profile for an RGB document, but it cannot guess which press a CMYK
document is for. Set `Options.OutputIntentIccProfile` to the ICC profile your print work uses. A
document whose `Options.ColorMode` is `Undefined` is refused for the same reason; set it to
`PdfColorMode.Rgb` or supply a profile.

Other PDF/A refusals name their own fix. The most common are "Set Info.Title." (a PDF/A document
needs a title) and "A PDF/A document may not be encrypted." See [PDF/A](../standards/pdf-a.md).

## Drawing and forms

### "The size of the XPdfForm is to small."

An `XForm` must be at least 1 point wide and 1 point high. The exception is an
`ArgumentNullException` on the `viewBox` parameter, although nothing is null. Check the size you
pass, and skip drawing the form when it would be smaller than a point.

### "A field's partial name cannot contain a period"

A period joins the names of nested fields into a full name such as `address.street`, so a field
named with a period in it could not be found again. Nest the fields instead: add a field named
`street` to the `Fields` of a field named `address`. See [Forms](../interactive/forms.md).
