---
title: Installation
description: Install the PdfPinata packages, choose a font and image backend, add its native libraries, and register it when your application starts.
---

PdfPinata is a set of NuGet packages. The core package, `PdfPinata`, contains no font or image
code. Every application installs one **backend** package as well and registers it once, when the
application starts. If you draw text or load an image before you register a backend, PdfPinata
throws an `InvalidOperationException` that names the property to set.

## Requirements

- **.NET 8 or .NET 10.** Every package targets `net8.0` and `net10.0`.
- **.NET Standard 2.1.** Every package except `PdfPinata.Signing` also targets `netstandard2.1`.
  This target exists for Unity, whose scripting runtime cannot load `net8.0` assemblies.
- **Native libraries for SkiaSharp and HarfBuzzSharp**, if you use `PdfPinata.Skia` or
  `PdfPinata.HarfBuzz`. See [Add the native libraries](#add-the-native-libraries).

## Choose your packages

| To do this | Install |
|---|---|
| Draw pages with `XGraphics`, or change existing PDFs | `PdfPinata.Skia` or `PdfPinata.ImageSharp` (either one brings in `PdfPinata`) |
| Lay out documents with paragraphs, tables and styles | `PinataLayout.Rendering` and a backend |
| Draw charts on an `XGraphics` page | `PdfPinata.Charting` and a backend |
| Shape Arabic, Indic scripts, ligatures and kerning | `PdfPinata.HarfBuzz`, in addition to a backend |
| Sign documents | `PdfPinata.Signing` (.NET 8 and later) |
| Attach a Factur-X or ZUGFeRD invoice | `PdfPinata.EInvoice` |

`PinataLayout.Rendering` brings in `PinataLayout.DocumentObjectModel` and `PdfPinata.Charting`. All
nine packages have the same version number, so install the same version of each.

## Choose a backend

The backend reads font files and decodes images for PdfPinata. Install one of the two.

| | `PdfPinata.Skia` | `PdfPinata.ImageSharp` |
|---|---|---|
| Built on | SkiaSharp | SixLabors.ImageSharp 2.1 and SixLabors.Fonts 1.0 |
| Native libraries | Yes, one package for each operating system | No, fully managed |
| Licence of the dependency | MIT | Apache-2.0 |
| Works beside ImageSharp 3 in your app | Yes | No |

**`PdfPinata.Skia` is the recommended backend.** Choose `PdfPinata.ImageSharp` when you cannot ship
native libraries.

`PdfPinata.ImageSharp` accepts ImageSharp versions from 2.1.13 up to, but not including, 3.0.
ImageSharp 3.0 changed to the Six Labors Split License, and it is not binary compatible with 2.1.
If your application already uses ImageSharp 3, use `PdfPinata.Skia`. The
[Licensing](./reference/licensing.md) page has more about this.

## Install the packages

1. Open a terminal in the folder of your project.
2. Add the backend:

   ```sh
   dotnet add package PdfPinata.Skia
   ```

3. If you lay out documents with PinataLayout, add the renderer:

   ```sh
   dotnet add package PinataLayout.Rendering
   ```

4. If you need text shaping, add HarfBuzz:

   ```sh
   dotnet add package PdfPinata.HarfBuzz
   ```

5. If your application runs on Linux, add the native libraries. See the next section.

## Add the native libraries

SkiaSharp and HarfBuzzSharp call native code. Their packages bring in the native libraries for
Windows and macOS. **On Linux you must add them yourself.**

For `PdfPinata.Skia`, add one of these two packages:

- `SkiaSharp.NativeAssets.Linux.NoDependencies` does not need fontconfig on the machine. Use it
  in slim containers.
- `SkiaSharp.NativeAssets.Linux` needs `libfontconfig1` on the machine.

For `PdfPinata.HarfBuzz`, add `HarfBuzzSharp.NativeAssets.Linux`.

A native package must have the same version as the managed package it belongs to. To see the
versions of SkiaSharp and HarfBuzzSharp that your PdfPinata packages use, run
`dotnet list package --include-transitive`. Then add the native packages with those versions, for
example:

```xml
<ItemGroup>
  <PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.152.1" />
  <PackageReference Include="HarfBuzzSharp.NativeAssets.Linux" Version="14.2.1.201" />
</ItemGroup>
```

You can also list `SkiaSharp.NativeAssets.Win32` and `SkiaSharp.NativeAssets.macOS` explicitly.
This does no harm and makes every version visible in your project file.

`PdfPinata.ImageSharp` needs no native libraries.
[Platforms and deployment](./reference/platforms-and-deployment.md) has more about Linux, containers
and Unity.

## Register the backend at startup

PdfPinata reads its backend from static properties. Set them once, at the start of your
application, before you create any font, image or document.

For `PdfPinata.Skia`:

```csharp
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();
```

For `PdfPinata.ImageSharp`:

```csharp
using PdfPinata.Fonts;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp.PixelFormats;

GlobalFontSettings.FontResolver = new ImageSharpFontResolver();
ImageSource.ImageSourceImpl = new ImageSharpImageSource<Rgba32>();
GlobalFontSettings.GlyphOutlineProvider = new ImageSharpGlyphOutlineProvider();
```

If you installed `PdfPinata.HarfBuzz`, register the shaper too. You can also name the font
families to try when a font has no glyph for a character:

```csharp
using PdfPinata.HarfBuzz;

GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();
GlobalFontSettings.FontFallback = new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

This table says what each property does.

| Property | What it does | If you do not set it |
|---|---|---|
| `GlobalFontSettings.FontResolver` | Finds the font file for a family name and style. | Required. Any font, and any PinataLayout `Document`, throws. |
| `ImageSource.ImageSourceImpl` | Decodes image files. | Required to load an image. Loading one throws. |
| `GlobalFontSettings.GlyphOutlineProvider` | Turns text into outlines, for `XGraphicsPath.AddString` and for placing `XTextFormatter` drop caps. | `AddString` throws, and a drop cap sits slightly off the margin. Everything else works. |
| `GlobalFontSettings.TextShaper` | Applies the font's shaping rules. | Each character maps to one glyph. Latin text is correct, without kerning or ligatures. |
| `GlobalFontSettings.FontFallback` | Names the families to try for a missing glyph. | A missing glyph is drawn as the font's `.notdef` glyph, usually an empty box. |

`SkiaFontResolver` and `ImageSharpFontResolver` find the fonts installed on the operating system.
If your application must run where you cannot install fonts, you can serve font files of your own.
See [Fonts](./fonts-and-text/fonts.md).

## Things to know

- **Set `FontResolver` before the first font exists.** After PdfPinata has created a font, setting a
  different resolver throws `InvalidOperationException`. Setting the same instance again is
  ignored. `GlobalFontSettings.IsFontResolverSet` tells you whether a resolver is registered.
- **A PinataLayout `Document` needs the resolver too.** `new Document()` asks the resolver for the
  name of its default font, so register the backend before you create a document.
- **The settings are global.** They apply to the whole process, and to every library in it that
  uses PdfPinata. In a web application, set them once in `Program.cs`, not per request.
- **`ImageSource` is in the `PinataLayout.DocumentObjectModel.Shapes` namespace**, although it ships
  in the `PdfPinata` package. You need that `using` even if you do not use PinataLayout. If you
  upgrade from an earlier namespace, see [Migrating](./reference/migrating.md).
- **The resolvers are in `PdfPinata.Utils`, but `SkiaImageSource` is in `PdfPinata.Skia`.** The
  Skia registration needs both `using` lines.
- **A shaper changes measurements.** Shaped text can be narrower or wider than unshaped text, so
  lines can wrap at different words. Register the shaper before you lay out anything whose output
  must stay the same between runs.
- **If you use the ImageSharp backend, treat warning `NU1608` as an error.** NuGet reports it when
  your project references ImageSharp 3 directly. Without the error, the build succeeds and the
  backend throws an `InvalidOperationException` when it decodes an image. Add this to your project file:

  ```xml
  <PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);NU1608</WarningsAsErrors>
  </PropertyGroup>
  ```

  If ImageSharp 3 arrives through another package, NuGet reports `NU1107` and the restore fails.
