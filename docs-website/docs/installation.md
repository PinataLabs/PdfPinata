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
- **.NET Standard 2.1.** Every package except `PdfPinata.Signing` and `PdfPinata.ImageSharp4` also
  targets `netstandard2.1`. This target exists for Unity, whose scripting runtime cannot load
  `net8.0` assemblies.
- **Native libraries for SkiaSharp and HarfBuzzSharp**, if you use `PdfPinata.Skia` or
  `PdfPinata.HarfBuzz`. See [Add the native libraries](#add-the-native-libraries).

## Choose your packages

| To do this | Install |
|---|---|
| Draw pages with `XGraphics`, or change existing PDFs | A backend: `PdfPinata.Skia`, `PdfPinata.ImageSharp4` or `PdfPinata.ImageSharp` (each one brings in `PdfPinata`) |
| Lay out documents with paragraphs, tables and styles | `PinataLayout.Rendering` and a backend |
| Draw charts on an `XGraphics` page | `PdfPinata.Charting` and a backend |
| Shape Arabic, Indic scripts, ligatures and kerning | `PdfPinata.HarfBuzz`, in addition to a backend |
| Sign documents | `PdfPinata.Signing` (.NET 8 and later) |
| Attach a Factur-X or ZUGFeRD invoice | `PdfPinata.EInvoice` |

`PinataLayout.Rendering` brings in `PinataLayout.DocumentObjectModel` and `PdfPinata.Charting`. All
ten packages have the same version number, so install the same version of each.

## Choose a backend

The backend reads font files and decodes images for PdfPinata. Install one of the three.

| | `PdfPinata.Skia` | `PdfPinata.ImageSharp4` | `PdfPinata.ImageSharp` |
|---|---|---|---|
| Built on | SkiaSharp | SixLabors.ImageSharp 4 and SixLabors.Fonts 3 | SixLabors.ImageSharp 2.1 and SixLabors.Fonts 1.0 |
| Native libraries | Yes, one package for each operating system | No, fully managed | No, fully managed |
| Licence of the dependency | MIT | Six Labors Split License | Apache-2.0 |
| Target frameworks | `netstandard2.1`, `net8.0`, `net10.0` | `net8.0`, `net10.0` | `netstandard2.1`, `net8.0`, `net10.0` |
| ImageSharp your app can use beside it | Any version | 4.x only | 2.1.x only |
| Open ImageSharp security advisories | Not applicable | None | Five, never to be fixed |

**`PdfPinata.Skia` is the recommended backend.** Choose an ImageSharp backend when you cannot ship
native libraries. The next section says which of the two to use.

## Choose an ImageSharp package

There are two ImageSharp backends. They have the same types in the same namespaces, and they give
the same results. The difference is the version of ImageSharp that each one uses.

**Use `PdfPinata.ImageSharp4` unless one of these applies to you:**

- **You build for Unity.** ImageSharp 4 targets `net8.0` only, so `PdfPinata.ImageSharp4` has no
  `netstandard2.1` build. Unity needs one, and only `PdfPinata.ImageSharp` has it.
- **Every dependency must be under Apache-2.0.** ImageSharp 4 and SixLabors.Fonts 3 are under the
  Six Labors Split License. `PdfPinata.ImageSharp` uses ImageSharp 2.1 and SixLabors.Fonts 1.0, the
  last releases under Apache-2.0.

If you use `PdfPinata.ImageSharp`, know these limits:

- **ImageSharp 2.1 is end of life.** It has five security advisories that will never be fixed:
  [GHSA-gwg2-r3hj-4w44](https://github.com/advisories/GHSA-gwg2-r3hj-4w44),
  [GHSA-j3p4-wp97-rph4](https://github.com/advisories/GHSA-j3p4-wp97-rph4),
  [GHSA-wmxv-xphr-5c9g](https://github.com/advisories/GHSA-wmxv-xphr-5c9g),
  [GHSA-jjfr-hcj7-qf5w](https://github.com/advisories/GHSA-jjfr-hcj7-qf5w) and
  [GHSA-j9gm-c75j-xc9q](https://github.com/advisories/GHSA-j9gm-c75j-xc9q). Two of them are in the
  decoders, for ICC colour profiles and for BigTIFF files. The other three are in histogram
  equalization and in the TIFF encoder, which the backend does not call. If your application loads
  images that you do not control, use `PdfPinata.ImageSharp4` or `PdfPinata.Skia`.
- **Your application cannot use a later ImageSharp.** NuGet gives an application one version of
  ImageSharp. If your application already uses ImageSharp 3 or later for its own work, use
  `PdfPinata.ImageSharp4` (for ImageSharp 4) or `PdfPinata.Skia` (for any version).

**You need no Six Labors licence key to use `PdfPinata.ImageSharp4`.** Six Labors checks its key
when a project that references ImageSharp directly is built. That check runs when PdfPinata builds
the package, not when your application builds. Six Labors says: "If you use a third party package
that depends on our libraries, you do not need a license." If your application also references
ImageSharp 4 directly, the Six Labors licence applies to that use. See
[Licensing](./reference/licensing.md).

**To switch between the two, change the package reference only.** The code that registers the
backend is the same for both. Replace one line in your project file:

```xml
<!-- Before -->
<PackageReference Include="PdfPinata.ImageSharp" Version="..." />
<!-- After -->
<PackageReference Include="PdfPinata.ImageSharp4" Version="..." />
```

Images decode to the same pixels in both packages, with one exception. A JPEG image can differ by up
to 2 levels in 255 in each colour channel, because ImageSharp 3 has a new JPEG decoder. So a PDF made
from a JPEG image is not always byte for byte the same after a switch. PNG images decode to the same
pixels. Both packages keep an embedded ICC colour profile and do not apply it.

Each package accepts one major version of ImageSharp. `PdfPinata.ImageSharp4` accepts 4.1.3 up to,
but not including, 5.0. `PdfPinata.ImageSharp` accepts 2.1.13 up to, but not including, 3.0. The
majors are not binary compatible, so NuGet reports a conflict when your application asks for another
one. See [Things to know](#things-to-know).

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

`PdfPinata.ImageSharp4` and `PdfPinata.ImageSharp` need no native libraries.
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

For `PdfPinata.ImageSharp4` or `PdfPinata.ImageSharp` (the code is the same for both):

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
- **If you use an ImageSharp backend, treat warning `NU1608` as an error.** NuGet reports it when
  your project references a major version of ImageSharp that the backend does not accept: anything
  but 4.x for `PdfPinata.ImageSharp4`, anything but 2.1.x for `PdfPinata.ImageSharp`. Without the
  error, the build succeeds and the backend throws an `InvalidOperationException` when it decodes an
  image. Add this to your project file:

  ```xml
  <PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);NU1608</WarningsAsErrors>
  </PropertyGroup>
  ```

  If the other version arrives through another package, NuGet reports `NU1107` and the restore fails.
