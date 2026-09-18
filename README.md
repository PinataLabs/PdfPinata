# PdfPinata

[![NuGet Version](https://img.shields.io/nuget/v/PdfPinata.svg)](https://www.nuget.org/packages/PdfPinata/)
[![CI](https://github.com/PinataLabs/PdfPinata/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/PinataLabs/PdfPinata/actions/workflows/build-and-test.yml)
[![codecov](https://codecov.io/gh/PinataLabs/PdfPinata/graph/badge.svg)](https://codecov.io/gh/PinataLabs/PdfPinata)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=PinataLabs_PdfPinata&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=PinataLabs_PdfPinata)

**PdfPinata** is a fork of [PdfSharpCore](https://github.com/ststeiger/PdfSharpCore), which is a partial port of [PdfSharp.Xamarin](https://github.com/roceh/PdfSharp.Xamarin/) for .NET Standard.
The core `PdfPinata` package carries no imaging or font dependency of its own. Pick a backend package and register it once at startup.

## Backends

| Package | Backend | License | Notes |
| --- | --- | --- | --- |
| `PdfPinata.Skia` | [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | Default. Native library — see below. |
| `PdfPinata.ImageSharp` | [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) / [Fonts](https://github.com/SixLabors/Fonts) | Apache-2.0 | Pinned to the Apache-2.0 licensed 2.1.x / 1.0.x lines — see below. |

Register the backend before creating any font or loading any image:

```csharp
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Utils;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
```

Both throw a descriptive `InvalidOperationException` if you use them without registering a backend first.

### Text shaping

| Package | Backend | License | Notes |
| --- | --- | --- | --- |
| `PdfPinata.HarfBuzz` | [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | MIT | Optional. Works with either imaging backend. Native library — see below. |

Without it, a string goes to the page one character at a time, each mapped to the glyph the font's
`cmap` gives for that code point. For Latin that is nearly right — it loses kerning and ligatures.
For Arabic it is wrong, because the alphabet has initial, medial, final and isolated forms and
Unicode stores the letter rather than the form, so the letters never join. Registering a shaper runs
the font's own `GSUB` and `GPOS` tables instead:

```csharp
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;

GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();
```

Unlike the other seams this one is optional and reads as `null` until you set it, so nothing changes
by leaving it alone. Setting it **will** change where lines wrap, because kerning and ligatures
change how wide text measures — register it before you pin any layout you care about.

It is a package of its own rather than part of a backend so that shaping does not oblige you to pick
one, and it needs the same kind of native asset reference SkiaSharp does:

```xml
<PackageReference Include="HarfBuzzSharp.NativeAssets.Win32" Version="14.2.1.2" />
<PackageReference Include="HarfBuzzSharp.NativeAssets.Linux" Version="14.2.1.2" />
<PackageReference Include="HarfBuzzSharp.NativeAssets.macOS" Version="14.2.1.2" />
```

**Bidirectional reordering does not need the shaper and is on already.** Every `DrawString` and every
`MeasureString` runs the Unicode Bidirectional Algorithm over the string, cuts it into runs of one
direction and one script, and draws them in the order they are read — so `"سلام"` comes out as
`"سلام"` and not as `"م ا ل س"`, with or without `PdfPinata.HarfBuzz`. Without the shaper the
letters do not *join*, which needs the font's `GSUB`; with it, they do. Text made only of characters
below `U+02B0` skips the whole thing, so plain Latin costs nothing.

**A character the chosen face has no glyph for can be drawn by one that does.** Name the families to
fall back to, in order of preference, and a run is cut wherever coverage runs out:

```csharp
GlobalFontSettings.FontFallback = new FontFallbackList("Noto Sans Arabic", "Noto Sans Devanagari");
```

Optional, like the shaper: with nothing registered, a missing glyph is `.notdef` — an empty box —
exactly as before, and nothing is asked about coverage at all. The families are named rather than
discovered because working out unprompted which of a machine's installed faces can draw a character
means reading the `cmap` of every one of them; implement `IFontFallback` yourself if you want that.

**Whole paragraphs are laid out in the order they are read**, by `XTextFormatter` (justified lines
included) and by MigraDoc. Either can be told which way a paragraph runs instead of leaving it to be
guessed from the first strong character:

```csharp
formatter.TextDirection = BidiParagraphDirection.RightToLeft;
paragraph.Format.TextDirection = BidiParagraphDirection.RightToLeft;
```

One case is left as written: a MigraDoc line containing a **tab**. Where a tab stop belongs when the
text runs the other way is a design question this does not answer.
`docs/specs/text-shaping-and-bidi.md` has the rest.

### Fonts

The resolver shipped with each backend discovers `.ttf`, `.otf`, `.ttc` and `.otc` in the platform's
font directories, and every face of a collection separately. Fonts are always embedded — there is
no setting for it and never has been, on any platform.

Two limits are worth knowing before you pick a font:

* **OpenType fonts with PostScript (CFF) outlines embed whole.** TrueType fonts are subsetted down
  to the glyphs a document actually draws; CFF outlines cannot be, so an `.otf` goes in at its full
  size. For a CJK face that is megabytes per document.
* **A weight or slant a family ships no file for is drawn on**, by stroking the glyphs and skewing
  them. It looks like what a word processor does in the same situation, which is to say noticeably
  worse than a real bold or italic. Ship the real faces where output quality matters.

### SkiaSharp native assets

SkiaSharp is a native library, so an application using `PdfPinata.Skia` must also reference the
native asset package for each platform it runs on. PdfPinata deliberately does not pull these in,
so that you can choose the right Linux variant:

```xml
<PackageReference Include="SkiaSharp.NativeAssets.Win32" Version="4.150.1" />
<PackageReference Include="SkiaSharp.NativeAssets.Linux.NoDependencies" Version="4.150.1" />
<PackageReference Include="SkiaSharp.NativeAssets.macOS" Version="4.150.1" />
```

Use `SkiaSharp.NativeAssets.Linux` instead of `...NoDependencies` if `libfontconfig1` is available
on your Linux image. The `PdfPinata.ImageSharp` backend is fully managed and needs none of this.

### ImageSharp 3.x and later are not supported

`PdfPinata.ImageSharp` requires `SixLabors.ImageSharp` **2.1.x**. SixLabors relicensed from
Apache-2.0 to the [Six Labors Split License](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE)
in ImageSharp 3.0 and Fonts 2.0, so this package stays on the last Apache-2.0 versions rather than
pushing that licence onto everyone who installs PdfPinata.

ImageSharp 3.0 is also not binary compatible with 2.1.x — it removed the
`Image.Load(..., out IImageFormat)` overloads and made every encoder property `init`-only, which
changes the setter signature. A build compiled against 2.1.x therefore fails at runtime with
`MissingMethodException` if 3.x is loaded instead ([#348](https://github.com/ststeiger/PdfSharpCore/issues/348)).

The dependency is declared as the range `[2.1.13,3.0.0)`, so a mismatch surfaces at restore rather
than at runtime:

* If ImageSharp 3.x arrives **transitively**, restore fails with `NU1107` (version conflict).
* If your project references ImageSharp 3.x **directly**, a direct reference wins and you get only a
  `NU1608` warning. Treat it as an error, because the build will still crash at runtime:

  ```xml
  <WarningsAsErrors>$(WarningsAsErrors);NU1608</WarningsAsErrors>
  ```

Should it get through anyway, the backend reports the mismatch as a descriptive
`InvalidOperationException` naming the loaded version instead of a bare `MissingMethodException`.

Only ImageSharp carries an upper bound. `SixLabors.Fonts` is referenced at `1.0.1` for the same
licensing reason, but `ImageSharpFontResolver` uses the small part of its API that 2.x keeps
unchanged, so resolving Fonts to 2.x is a licence decision for you to make rather than something
that breaks at runtime.

**If your application needs ImageSharp 3.x or later, use the `PdfPinata.Skia` backend instead** —
it has no ImageSharp dependency, so both can coexist in one project.


## Target frameworks

Every shipped package targets `netstandard2.1;net8.0;net10.0`.

`netstandard2.1` is kept for **Unity**. Unity's scripting runtime tops out at the .NET Standard 2.1
API compatibility level — it cannot consume a `net8.0` or `net10.0` assembly — so dropping the
netstandard2.1 target would drop Unity as a consumer entirely. Please don't remove it without
checking that first.

It is not free. `netstandard2.1` predates the trimming annotations in
`System.Diagnostics.CodeAnalysis`, so `DynamicallyAccessedMembersAttribute` and
`DynamicallyAccessedMemberTypes` are polyfilled in-repo, guarded by `#if !NET5_0_OR_GREATER` so they
compile to nothing on the modern targets:

* `PdfPinata/!internal/`
* `PinataLayout.DocumentObjectModel/CompileFixes/`

Both copies are `internal` and there is no `InternalsVisibleTo`, which is why each assembly needs its
own. They look like dead code on a `net10.0`-only glance; they are not. When `netstandard2.1` is
eventually dropped, both directories can be deleted together.


## Table of Contents

- [Documentation](docs/index.md)
- [Backends](#backends)
- [Target frameworks](#target-frameworks)
- [Example](#example)
- [Running the demos](#running-the-demos)
- [Contributing](#contributing)
- [License](#license)


## Example

The following code snippet creates a simple PDF-file with the text 'Hello World!'.
The code is written for a .NET 8 console app with top level statements.

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

`SampleApp` builds one PDF per feature area and prints the code that drew it.

```powershell
dotnet run --project SampleApp -- list                       # what each demo shows
dotnet run --project SampleApp -- run                        # all of them, into SampleApp/output
dotnet run --project SampleApp -- run --example Fonts Text   # just these two
dotnet run --project SampleApp -- run --no-code              # PDFs only, no source printed
```

There are eleven demos, from `HelloWorld` through `Fonts`, `Orientation`, `Images`, `Text`,
`Layout`, `Tables` and `PageResize` to three real layouts — `Invoice`, `Newspaper` and `Magazine`.
Each writes `output/<Name>.pdf`, deleting what a previous run left rather than writing over it.

The app carries its own fonts — Liberation Sans and Serif, and Source Code Pro for its PostScript
outlines — so the PDFs are identical wherever they are built, including a machine with no fonts
installed. `docs/specs/demonstration-app.md` records what it covers and what it deliberately does
not.


## Running the tests

`dotnet test` needs no setup. Ghostscript, used to rasterize PDFs for the visual comparison tests,
comes from the `Ghostscript.NativeAssets` package, so there is nothing to install on Windows.
On Linux and macOS ImageMagick invokes the system `gs` delegate instead, so install Ghostscript
through your package manager (`apt-get install ghostscript`, `brew install ghostscript`) if you
want those tests to run.

The visual comparison tests in `XTextFormatterTest` compare against reference images rendered on
Linux. Text rasterizes differently elsewhere because a different set of system fonts is installed,
so on other platforms they report as skipped with the reason rather than failing. CI runs on Linux
and remains the authority on rendering; if you change text layout, check its result.


## Contributing

We appreciate feedback and contribution to this repo!


## License

This software is released under the MIT License. See the [LICENSE](LICENCE.md) file for more info.

PdfPinata relies on the following projects, that are not under the MIT license:

* *SixLabors.ImageSharp* and *SixLabors.Fonts*
  * SixLabors.ImageSharp and SixLabors.Fonts, libraries which PdfPinata relies upon, are licensed under Apache 2.0 when distributed as part of PdfPinata. The SixLabors.ImageSharp license covers all other usage, see https://github.com/SixLabors/ImageSharp/blob/master/LICENSE
