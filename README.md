# PdfPinata

**PdfPinata is a .NET library for creating, drawing, and manipulating PDF documents.**

It provides a PDF document model and drawing API for working with pages, text, fonts, images, shapes, and existing PDF files. It can be used directly for low-level PDF drawing, or together with **PinataLayout** for higher-level document layouts such as paragraphs, tables, headers, footers, and flowing content.

Typical uses include:

* generating invoices, reports, statements, and other PDFs
* drawing text, images, lines, shapes, and graphics onto pages
* loading and modifying existing PDF documents
* embedding and subsetting fonts
* rendering multilingual and right-to-left text
* building reusable document layouts with PinataLayout
* generating PDFs on Windows, Linux, macOS, and Unity

PdfPinata is a fork of [PdfSharpCore](https://github.com/ststeiger/PdfSharpCore), itself based on [PdfSharp.Xamarin](https://github.com/roceh/PdfSharp.Xamarin/). The fork continues that API while separating font and image handling into interchangeable backends and extending text support.

The core `PdfPinata` package has no imaging or font-rendering dependency of its own. Choose a backend package and register it once when your application starts.

## Badges

[![NuGet Version](https://img.shields.io/nuget/v/PdfPinata.svg)](https://www.nuget.org/packages/PdfPinata/)
[![CI](https://github.com/PinataLabs/PdfPinata/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/PinataLabs/PdfPinata/actions/workflows/build-and-test.yml)
[![codecov](https://codecov.io/gh/PinataLabs/PdfPinata/graph/badge.svg)](https://codecov.io/gh/PinataLabs/PdfPinata)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=PinataLabs_PdfPinata\&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=PinataLabs_PdfPinata)


## Packages

### Rendering backends

| Package                | Backend                                                                                                       | License    | Notes                                                                           |
| ---------------------- | ------------------------------------------------------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------- |
| `PdfPinata.Skia`       | [SkiaSharp](https://github.com/mono/SkiaSharp)                                                                | MIT        | Recommended backend. Requires native assets for the target platform.            |
| `PdfPinata.ImageSharp` | [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) / [Fonts](https://github.com/SixLabors/Fonts) | Apache-2.0 | Fully managed. Uses the final Apache-2.0 ImageSharp 2.1.x / Fonts 1.x releases. |

Register the backend before creating fonts or loading images:

```csharp
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Utils;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
```

If a font or image API is used before its backend has been registered, PdfPinata throws a descriptive `InvalidOperationException`.

### Text shaping

Complex text shaping is available through the optional `PdfPinata.HarfBuzz` package.

| Package              | Backend                                            | License | Notes                                                                  |
| -------------------- | -------------------------------------------------- | ------- | ---------------------------------------------------------------------- |
| `PdfPinata.HarfBuzz` | [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | MIT     | Optional. Works with either rendering backend. Requires native assets. |

Without HarfBuzz, PdfPinata maps Unicode characters directly to glyphs using the font's `cmap`. This is sufficient for basic Latin text, but does not provide features such as kerning, ligatures, or contextual Arabic letter forms.

Registering HarfBuzz enables the font's OpenType `GSUB` and `GPOS` shaping rules:

```csharp
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;

GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();
```

Text shaping affects measured text width and therefore can change line wrapping. Register it before creating layouts whose output needs to remain stable.

HarfBuzz is kept separate from the rendering backend so shaping can be used with either SkiaSharp or ImageSharp.

Applications must reference the appropriate native assets:

```xml
<PackageReference Include="HarfBuzzSharp.NativeAssets.Win32" Version="14.2.1.2" />
<PackageReference Include="HarfBuzzSharp.NativeAssets.Linux" Version="14.2.1.2" />
<PackageReference Include="HarfBuzzSharp.NativeAssets.macOS" Version="14.2.1.2" />
```

## International text

### Bidirectional text

Unicode bidirectional text handling is built into PdfPinata and does **not** require HarfBuzz.

`DrawString` and `MeasureString` apply the Unicode Bidirectional Algorithm, separating text into directional and script runs before measuring or drawing them.

This means right-to-left text is presented in reading order even when text shaping is not installed. HarfBuzz is still required where characters need contextual shaping, such as joined Arabic forms.

Strings containing only characters below `U+02B0` use a fast path and skip bidirectional processing.

### Font fallback

PdfPinata can use another font when the selected font does not contain a required glyph.

Configure fallback families in preference order:

```csharp
GlobalFontSettings.FontFallback =
    new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

Fallback is optional. Without it, missing glyphs are rendered using the font's `.notdef` glyph.

Font families are configured explicitly rather than automatically scanning every installed font. Applications that require different behaviour can implement `IFontFallback`.

### Right-to-left paragraphs

`XTextFormatter` and PinataLayout can lay out complete right-to-left paragraphs, including justified text.

Direction can either be detected from the first strong character or specified explicitly:

```csharp
formatter.TextDirection = BidiParagraphDirection.RightToLeft;
paragraph.Format.TextDirection = BidiParagraphDirection.RightToLeft;
```

Bidirectional tab-stop layout in PinataLayout is currently left unchanged because the expected placement of mirrored tab stops is application-dependent.

See `docs/specs/text-shaping-and-bidi.md` for more detail.

## Fonts

The font resolver supplied by each backend discovers:

* `.ttf`
* `.otf`
* `.ttc`
* `.otc`

Each face in a TrueType or OpenType collection is exposed separately.

Fonts used by a document are always embedded in the PDF.

### Font subsetting

TrueType fonts are subsetted so the PDF contains only the glyphs actually used by the document.

OpenType fonts containing PostScript/CFF outlines are currently embedded in full. Large CJK CFF fonts can therefore add several megabytes to a PDF.

### Synthetic bold and italic

If a requested family does not contain a real bold or italic face, PdfPinata can simulate it by stroking or skewing the glyphs.

This is useful as a fallback, but a genuine bold or italic font produces better output. Applications where typography matters should ship the required font faces.

## SkiaSharp native assets

SkiaSharp uses native libraries. Applications using `PdfPinata.Skia` must reference the native asset package for each target platform.

PdfPinata intentionally does not choose these packages automatically because Linux applications may require different variants.

For example:

```xml
<PackageReference Include="SkiaSharp.NativeAssets.Win32" Version="4.150.1" />
<PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.150.1" />
<PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="4.150.1" />
```

Use `SkiaSharp.NativeAssets.Linux` instead of `SkiaSharp.NativeAssets.Linux.NoDependencies` when `libfontconfig1` is available in the target Linux environment.

`PdfPinata.ImageSharp` is fully managed and does not require native rendering assets.

## ImageSharp version support

`PdfPinata.ImageSharp` requires `SixLabors.ImageSharp` **2.1.x**.

ImageSharp 3.0 moved from Apache-2.0 to the [Six Labors Split License](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE). PdfPinata therefore remains on the final Apache-2.0 ImageSharp release rather than automatically introducing the newer licence into applications using PdfPinata.

ImageSharp 3.x is also not binary compatible with ImageSharp 2.1.x. APIs used by the backend changed between the releases, so loading ImageSharp 3.x with a backend compiled against 2.1.x can result in runtime failures.

The dependency is therefore declared as:

```text
[2.1.13,3.0.0)
```

This normally exposes the conflict during package restore instead of at runtime.

If ImageSharp 3.x arrives transitively, NuGet reports `NU1107`.

If the application directly references ImageSharp 3.x, NuGet's direct-dependency-wins rule can instead produce `NU1608`. Applications using the ImageSharp backend should treat that warning as an error:

```xml
<WarningsAsErrors>$(WarningsAsErrors);NU1608</WarningsAsErrors>
```

If an incompatible version is loaded anyway, PdfPinata reports a descriptive `InvalidOperationException` including the detected ImageSharp version.

`SixLabors.Fonts` is referenced at `1.0.1` for the same licensing reason, but the API used by `ImageSharpFontResolver` remains compatible with Fonts 2.x. Resolving a newer Fonts version is therefore a licensing choice rather than a known runtime incompatibility.

**Applications that require ImageSharp 3.x or later should use `PdfPinata.Skia`.** The Skia backend has no ImageSharp dependency, so ImageSharp 3.x can be used independently in the same application.

## Target frameworks

All PdfPinata packages target:

```text
netstandard2.1
net8.0
net10.0
```

`netstandard2.1` is retained primarily for **Unity**, whose scripting runtime supports the .NET Standard 2.1 API surface but cannot consume `net8.0` or `net10.0` assemblies directly.

Supporting `netstandard2.1` requires small compatibility shims for APIs introduced in later .NET versions. Missing types, such as trimming annotations and `IsExternalInit`, are generated by the [PolySharp](https://github.com/Sergio0694/PolySharp) source generator. Missing members, such as throw helpers and span overloads, live in `Polyfills/`. Both are `internal`, apply to the `netstandard` legs only, and add no package dependency.

When `netstandard2.1` support is eventually removed, the PolySharp reference and `Polyfills/` can be removed with it.

## Table of Contents

* [Documentation](docs/index.md)
* [Packages](#packages)
* [International text](#international-text)
* [Fonts](#fonts)
* [Target frameworks](#target-frameworks)
* [Example](#example)
* [Running the demos](#running-the-demos)
* [Running the tests](#running-the-tests)
* [Contributing](#contributing)
* [License](#license)

## Example

This example creates a PDF containing `Hello World!` using the Skia backend:

```csharp
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Utils;

GlobalFontSettings.FontResolver = new SkiaFontResolver();

var document = new PdfDocument();
var page = document.AddPage();

var gfx = XGraphics.FromPdfPage(page);
var font = new XFont("Arial", 20, XFontStyle.Bold);

var textColor = XBrushes.Black;
var layout = new XRect(20, 20, page.Width, page.Height);
var format = XStringFormats.Center;

gfx.DrawString("Hello World!", font, textColor, layout, format);

document.Save("helloworld.pdf");
```

## Running the demos

`SampleApp` contains examples covering the main PdfPinata features.

```powershell
dotnet run --project SampleApp -- list
dotnet run --project SampleApp -- run
dotnet run --project SampleApp -- run --example Fonts Text
dotnet run --project SampleApp -- run --no-code
```

The demos cover:

* Hello World
* fonts
* page orientation
* images
* text
* layout
* tables
* page resizing
* invoices
* newspapers
* magazines

Each example writes:

```text
output/<Name>.pdf
```

The sample application includes its own Liberation Sans, Liberation Serif, and Source Code Pro fonts so generated PDFs remain consistent across platforms, including machines with no suitable fonts installed.

See `docs/specs/demonstration-app.md` for the complete demo specification.

## Running the tests

Run the test suite with:

```powershell
dotnet test
```

No additional setup is required on Windows. Ghostscript used by the PDF visual comparison tests is supplied through `Ghostscript.NativeAssets`.

On Linux and macOS, ImageMagick uses the system Ghostscript installation, so install it through the operating system package manager when running visual tests:

```bash
apt-get install ghostscript
```

or:

```bash
brew install ghostscript
```

Some `XTextFormatterTest` visual tests compare generated output with reference images rendered on Linux.

Font rasterisation varies between operating systems and installed fonts, so these tests are skipped on platforms where the reference output cannot be compared reliably. CI runs them on Linux and is the reference environment for rendering changes.

## Contributing

Feedback, bug reports, pull requests, and other contributions are welcome.

## License

PdfPinata is released under the MIT License. See [LICENCE.md](LICENCE.md).

PdfPinata can optionally use projects distributed under other licences.

### ImageSharp and SixLabors.Fonts

`PdfPinata.ImageSharp` uses the Apache-2.0 licensed releases of:

* SixLabors.ImageSharp
* SixLabors.Fonts

Later versions of those projects use the Six Labors Split License. See the [ImageSharp licence](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE) for details.
