---
title: Platforms and deployment
description: What PdfPinata needs on Windows, macOS, Linux, in containers and in Unity, and how to run it where no fonts are installed.
---

PdfPinata's own code is managed .NET and runs wherever .NET 8, .NET 10 or .NET Standard 2.1 runs.
Two things change from one platform to another: the **native libraries** that the SkiaSharp and
HarfBuzzSharp packages need, and the **fonts** that the machine has installed. This page covers
both, for each platform. [Installation](../installation.md) covers the packages themselves.

## Windows

SkiaSharp and HarfBuzzSharp bring in their Windows native libraries, so you add nothing.

The font resolvers in `PdfPinata.Skia` and `PdfPinata.ImageSharp` read two folders:

- `%SystemRoot%\Fonts`
- `%LOCALAPPDATA%\Microsoft\Windows\Fonts`, which holds fonts installed for one user only

If a file with the same name is in both folders, the resolver uses the one in `%SystemRoot%\Fonts`.

## macOS

SkiaSharp and HarfBuzzSharp bring in their macOS native libraries, so you add nothing.

The font resolvers read **`/Library/Fonts/` only**. Fonts in `/System/Library/Fonts` and in
`~/Library/Fonts` are not found. If a family you need is not in `/Library/Fonts/`, install it
there, or serve your own font files (see [Where no fonts are installed](#where-no-fonts-are-installed)).

## Linux

On Linux you must add the native libraries yourself:

- For `PdfPinata.Skia`, add `SkiaSharp.NativeAssets.Linux.NoDependencies`, or
  `SkiaSharp.NativeAssets.Linux` if `libfontconfig1` is installed on every machine you deploy to.
- For `PdfPinata.HarfBuzz`, add `HarfBuzzSharp.NativeAssets.Linux`.

These packages contain builds for x64, Arm64 and other processors, and for musl-based distributions
such as Alpine. Use the same version as the managed SkiaSharp or HarfBuzzSharp package. To see
that version, run `dotnet list package --include-transitive`.

To find fonts, the resolvers ask fontconfig through `libfontconfig.so.1`. If that library is not
there, they read the folders named in `/etc/fonts/fonts.conf`, then `/usr/share/fonts`,
`/usr/local/share/fonts` and `~/.fonts`.

## Containers

A container image is Linux with almost nothing installed. Three things follow:

1. **Use `SkiaSharp.NativeAssets.Linux.NoDependencies`.** It does not need fontconfig, which slim
   images do not have.
2. **Expect no fonts.** A slim base image often contains no font files. The first time PdfPinata
   resolves a font, the resolver throws `FileNotFoundException` with the message "No Fonts installed
   on this device!".
3. **Supply the fonts.** Either install a font package in the image, or ship font files with your
   application and register them. The second way gives the same layout on every machine.

## Where no fonts are installed

Build servers, containers and serverless hosts often have no fonts, or different fonts from your
development machine. Because glyph widths decide where lines wrap, different fonts give different
page breaks, not only a different look.

To make output the same everywhere, ship the font files with your application and give them to the
resolver. `SetupFontsFiles` replaces the system search with the files you pass:

```csharp
using System;
using System.IO;
using PdfPinata.Fonts;
using PdfPinata.Utils;

var resolver = new SkiaFontResolver();
resolver.SetupFontsFiles(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fonts"), "*.ttf"));
GlobalFontSettings.FontResolver = resolver;
```

To copy the files to the output folder, mark them in your project file with
`CopyToOutputDirectory`. You can also write your own `IFontResolver` that reads fonts from embedded
resources, a database or a network share. [Fonts](../fonts-and-text/fonts.md) shows how.

Two settings make a missing font easier to find:

- **`NullIfFontNotFound`.** By default a resolver built on `FontResolverBase`, which includes both
  backend resolvers, answers an unknown family with another font and says nothing. Set
  `resolver.NullIfFontNotFound = true` to make an unknown family throw `InvalidOperationException`
  ("No appropriate font found.") when you create the `XFont`.
- **The PinataLayout default font.** The `Normal` style of a PinataLayout document uses the
  resolver's `DefaultFontName`, which is "Arial" for both backend resolvers. If you do not ship
  Arial, set `document.Styles[StyleNames.Normal].Font.Name` to a family you do ship.

## Unity

Unity's scripting runtime cannot load `net8.0` assemblies, so it uses the `netstandard2.1` build of
each package. Every package has one except `PdfPinata.Signing`, which targets .NET 8 and later only.

`PdfPinata.ImageSharp` is the backend with no native libraries, so it avoids the work of adding
SkiaSharp's native libraries to a Unity build. ImageSharp 2.1 and SixLabors.Fonts 1.0 both target
.NET Standard 2.1.

The `netstandard2.1` builds are not tested inside Unity. Test your document generation in the Unity
player you ship.

## Trimming and Native AOT

These packages are marked as trimmable: `PdfPinata`, `PdfPinata.ImageSharp`, `PdfPinata.Charting`,
`PdfPinata.Signing`, `PdfPinata.EInvoice`, `PinataLayout.DocumentObjectModel` and
`PinataLayout.Rendering`. `PdfPinata.Skia` and `PdfPinata.HarfBuzz` are not marked.

The PinataLayout document model uses code generated at compile time, not reflection. PinataLayout
with the Skia backend is tested under Native AOT on `linux-x64`: a small application builds a
document with styles and a table, writes and reads it as DDL, and renders it to PDF.

## Web applications and services

The backend settings are static and apply to the whole process. Set them once, at startup, before
the host starts to handle requests. Setting the same resolver instance a second time does nothing,
but setting a different one after the first font exists throws.

`HarfBuzzTextShaper` can be used from several threads at once.

## Things to know

- **Different machines can give different pages.** Font files, and the region settings that choose
  the default page size of `PdfPage` (A4 or US Letter), come from the machine. If output must match
  on every machine, ship your fonts and set page sizes explicitly.
- **The ImageSharp backend needs no native libraries**, on any platform.
- **`PdfPinata.Signing` needs .NET 8 or later.** It cannot run in Unity.
