---
title: Licensing
description: The licence of PdfPinata, the licences of the packages it depends on, and the licences of the fonts in the repository.
---

This page lists the licences that apply to PdfPinata and to the packages it brings into your
application. It states facts from the packages and the repository. It is not legal advice. If you
need to know what a licence allows in your situation, read the licence text or ask a lawyer.

## PdfPinata

PdfPinata and PinataLayout are released under the **MIT License**. The licence text is in
[LICENSE.md](https://github.com/PinataLabs/PdfPinata/blob/main/LICENSE.md), and every one of the ten
NuGet packages includes that file.

The licence carries three copyright lines, because PdfPinata continues earlier work:

- Copyright (c) 2026 PdfPinata
- Copyright (c) 2005-2007 empira Software GmbH, Cologne (Germany), the authors of PDFsharp and
  MigraDoc
- Modified work Copyright (c) 2016 David Dunscombe

Many source files also carry empira's copyright notice in their header. The MIT License requires
that the copyright notice and the permission notice are included in all copies or substantial
portions of the software.

## Dependencies of each package

This table lists the third-party packages that each PdfPinata package depends on at run time, and
their licences as their NuGet packages declare them.

| PdfPinata package | Depends on | Licence |
|---|---|---|
| `PdfPinata` | SharpZipLib 1.4.2 | MIT |
| `PdfPinata.Skia` | SkiaSharp, and its native asset packages | MIT |
| `PdfPinata.ImageSharp4` | SixLabors.ImageSharp 4.1.3 or later 4.x versions | Six Labors Split License |
| | SixLabors.Fonts 3.2.0 or later 3.x versions | Six Labors Split License |
| `PdfPinata.ImageSharp` | SixLabors.ImageSharp 2.1.13 or later 2.1 versions | Apache-2.0 |
| | SixLabors.Fonts 1.0.1 | Apache-2.0 |
| `PdfPinata.HarfBuzz` | HarfBuzzSharp, and its native asset packages | MIT |
| `PdfPinata.Signing` | System.Security.Cryptography.Pkcs | MIT |
| `PdfPinata.Charting` | None | |
| `PdfPinata.EInvoice` | None | |
| `PinataLayout.DocumentObjectModel` | None | |
| `PinataLayout.Rendering` | None | |

Every package in the table also depends on `PdfPinata`, and some depend on other PdfPinata packages.
All of those are MIT.

The SkiaSharp, HarfBuzzSharp, SixLabors.ImageSharp and SixLabors.Fonts packages ask you to accept
their licence when you install them.

The `PdfPinata` package also contains a 456-byte sRGB colour profile, which it gives to PDF/A
documents that name no profile of their own. The profile comes from the Compact ICC Profiles
collection by Clinton Ingram and is released to the public domain under Creative Commons CC0 1.0.

## The two ImageSharp packages

There are two ImageSharp backends, and their dependencies have different licences.
[Choose an ImageSharp package](../installation.md#choose-an-imagesharp-package) says which one to
use. This section gives the licence facts only.

**`PdfPinata.ImageSharp4`** depends on SixLabors.ImageSharp 4 and SixLabors.Fonts 3. Since version
3.0 of ImageSharp and version 2.0 of Fonts, Six Labors releases them under the
[Six Labors Split License](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE), not
Apache-2.0.

An application that uses ImageSharp only through PdfPinata needs no Six Labors licence. Six Labors
[says](https://sixlabors.com/posts/licence-enforcement-changes/): "If you use a third party package
that depends on our libraries, you do not need a license." Six Labors checks a licence key when a
project that references its packages directly is built. PdfPinata's build has a key, so the package
can be built. Your application's build does not run that check, because the check does not pass to
the packages that depend on ImageSharp. If your application also references ImageSharp or Fonts
directly, the Split License applies to that use, as it would without PdfPinata.

**`PdfPinata.ImageSharp`** depends on SixLabors.ImageSharp 2.1 and SixLabors.Fonts 1.0.1, the last
releases under Apache-2.0. Its version range stops below ImageSharp 3.0, so installing it never
brings the Split License into your application. ImageSharp 2.1 is end of life and has five security
advisories that will never be fixed. Use this package if every dependency must be under Apache-2.0,
or if you build for Unity.

Each package keeps the ImageSharp major version it was made for. A new ImageSharp major gets a new
package, so a routine update of a PdfPinata package never changes the licence of its dependencies.

`PdfPinata.Skia` has no ImageSharp dependency. With it, your application can use any version of
ImageSharp for its own work, under that version's licence.

## Build-time tools

Some packages take part in the build of PdfPinata but are not dependencies of the published
packages: MinVer sets version numbers, Microsoft.Sbom.Targets writes a software bill of materials,
and PolySharp generates internal compatibility code for the `netstandard2.1` builds. PolySharp is
MIT-licensed.

Each published package contains an SPDX software bill of materials under `_manifest/`, which lists
the dependencies it was built with.

## Fonts

**The PdfPinata packages contain no font files.** Your application supplies fonts, either from the
operating system or from files you ship. The licence of each font is a matter between you and the
font's owner. PdfPinata embeds every font it uses in the PDF it writes, and some font licences
restrict embedding.

The repository contains font files in two places, and neither is published as a package:

- The demo app, `src/SampleApp/Assets/Fonts`: Liberation Sans, Liberation Serif, Source Code Pro
  and Noto Sans Arabic.
- The tests, `src/PdfPinata.Test/Assets/Fonts`: Liberation Sans, Source Code Pro, Noto Sans Arabic
  and Noto Sans Devanagari.

All of these fonts are released under the **SIL Open Font License 1.1**. The licence text is in a
`LICENSE.txt` file beside the fonts, with the copyright notice of each family. The OFL requires that
its text travels with the fonts, so the demo app embeds `LICENSE.txt` beside the font files. If you
copy these fonts into your own application, include the licence file too.
