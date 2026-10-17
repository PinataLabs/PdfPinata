# A second ImageSharp backend, on ImageSharp 4

`PdfPinata.ImageSharp4`, published beside `PdfPinata.ImageSharp`, and why the two are shaped the way
they are. PinataLabs/PdfPinata#305; built in #306–#311.

| item | what | status |
|---|---|---|
| 1 | `PdfPinata.ImageSharp4` on ImageSharp 4.x and Fonts 3.x, `net8.0;net10.0`, in lockstep with the other nine packages | done (#308, #309) |
| 2 | One copy of the backend's source, compiled into both packages | done (#307) |
| 3 | The same pixels, glyph outlines and font resolution from both, for the same input | done, JPEG excepted (#308) |
| 4 | A committed Six Labors community key, and a clear failure when it lapses | done (#306) |
| 5 | `PdfPinata.ImageSharp4.Tests`, running the backend's tests against 4.x | done (#308) |
| 6 | The docs, the README and both package descriptions say which package to choose | done (#310) |
| 7 | The Blazor sample on `PdfPinata.ImageSharp4` | done (#311) |
| — | An ImageSharp 3.x package | deliberately not done |
| — | A `netstandard2.1` build of the 4.x package | deliberately not done |
| — | Retiring or deprecating `PdfPinata.ImageSharp` | deliberately not done |
| — | Opting in to ImageSharp 4's colour-profile conversion | deliberately not done |

## Why a second package

`PdfPinata.ImageSharp` is pinned to ImageSharp 2.1.x and Fonts 1.0.1, the last Apache-2.0 releases.
Two things made that pin a problem:

- **2.1.x is end of life and will not be patched.** 2.1.13, the last release, has five open
  advisories: GHSA-gwg2-r3hj-4w44 (ICC CLUT parsing), GHSA-j3p4-wp97-rph4 (histogram equalization),
  GHSA-wmxv-xphr-5c9g (BigTIFF decoding), GHSA-jjfr-hcj7-qf5w and GHSA-j9gm-c75j-xc9q (the TIFF
  CCITT encoders). They are fixed only in 3.2.0 and in 4.1.2 and later. Six Labors patches the
  latest major, and the one before it for 12 months.
- **An application gets one ImageSharp.** NuGet resolves one version per application, so an
  application already on ImageSharp 3 or 4 could not use the ImageSharp backend at all.

Checked on 2026-10-17: no 3.x release is both patched and free of the licence-key check. 3.2.0 and
Fonts 2.2.0 fix the advisories and add the check; 3.1.12 and Fonts 2.1.3, the last without it, carry
the five plus GHSA-jj3q-cwqj-842r and GHSA-v76p-62qx-wwq2. 3.x also reaches end of life on
2027-05-12. So the second package is on 4.x.

## Decisions

**`PdfPinata.ImageSharp` stays on 2.1.x and Apache-2.0.** It is the only ImageSharp backend Unity can
use, and the only one for a consumer who must keep every dependency under Apache-2.0. Its
description, the installation guide and the README say plainly that 2.1.x is end of life with five
advisories that will never be fixed, and recommend `PdfPinata.ImageSharp4` to everyone else. The CI
vulnerable-package check keeps those five on its allow-list, commented as permanent.

**A package is named for the ImageSharp major it binds to.** The existing name never moves to a new
major, because ImageSharp 3.0 changed the licence to the Six Labors Split License, and a licence
change must not reach anyone through a routine update. Each package's range stops below the next
major — `[2.1.13, 3.0.0)` and `[4.1.3, 5.0.0)`, with Fonts `[3.2.0, 4.0.0)` — because the majors are
not binary compatible; a conflict then surfaces at restore as NU1107 or NU1608. If the wrong major
loads anyway, both throw an `InvalidOperationException` that names the version they need.

**The two packages are equal.** Same features, same tests, same fixes, and the same MinVer version as
every other package, which makes ten in `PACKAGES`. They have the same types in the same namespaces
and differ only in assembly name, so switching between them is a project-file change.

**The source is written once.** `src/ImageSharpBackend/` has no project of its own; both csprojs
compile it, and `PdfPinata.ImageSharp4` defines `IMAGESHARP4`. The calls that differ sit behind that
constant:

- `Image.Load<T>(…, out IImageFormat)` is gone in 4.x; the format is
  `image.Metadata.DecodedImageFormat`.
- `GetConfiguration()` is `image.Configuration`.
- In Fonts 3, the renderer types move to `SixLabors.Fonts.Rendering`, `RenderTextTo` is `RenderTo`,
  `TryGetGlyphMetrics` takes a `FontPalette`, and `LoadFontCollectionDescriptions` answers a
  `ReadOnlyMemory<FontDescription>`.
- `IGlyphRenderer` gains `BeginLayer`, `EndLayer`, `BeginGroup`, `EndGroup` and `ArcTo`, and
  `SetDecoration` gains a parameter.

`JpegEncoder { Quality = … }` compiles against both, because an object initialiser calls an `init`
setter and a `set` one alike.

**Images decode the same in both.** 4.x handles colour profiles through `DecoderOptions`, and its
choices can change the pixels an image decodes to. The 4.x build sets `ColorProfileHandling.Preserve`,
which is what 2.x did: an embedded ICC profile is kept, not applied. PNGs (RGB, and palette with
`tRNS`) decode bit-identically. **A JPEG does not**: ImageSharp 3 rewrote the JPEG decoder, and the two
differ by up to 2 levels in 255 per channel, which no decoder option removes. So nothing promises a
byte-identical PDF across a switch for JPEG input. `ImageSharpDecodingParityTests` pins both halves:
the PNGs by the hash of what 2.1.13 decoded them to, and the JPEG within two levels of a square of it
that 2.1.13 decoded, checked in as a PNG.

**Glyph outlines are the same in both, to about 4e-6 pt.** The 4.x provider:

- adds `ArcTo` to the path as cubic Béziers (SVG implementation notes F.6);
- treats layers and groups as no-ops, so a colour glyph contributes its outline in one fill;
- uses the default palette, `ColorFontSupport.ColrV0` and `TextBaseline.Alphabetic`, which were
  Fonts 1's defaults, so SVG and COLR v1 glyphs are drawn from their plain outlines as in 2.x;
- renders at 64 times the resolution and scales back, because Fonts 3 rounds TrueType points to
  1/64 pixel.

Parity is one reason for leaving SVG glyphs to their plain outlines. The other is a bug in Fonts
3.2.0: `PaintedGlyphMetrics` passes an arc's `radiusY` as `RadiusY * M12`, which is 0 for an
unrotated glyph, so an SVG circle collapses to a line. `ArcGlyphOutlineTests` drives `ArcTo` directly.

**The licence key is committed.** ImageSharp 4 and Fonts 3 refuse a Release build without a Six
Labors key. The maintainer obtained an open-source community key, which Six Labors allows to be
committed; a CI secret would not reach a pull request from a fork. The key covers one assembly name,
so only `PdfPinata.ImageSharp4` may reference the packages directly, and everything else reaches them
through a `ProjectReference`. `sixlabors-licence-key.md` has the key, the PPSL001–PPSL004 messages and
the renewal steps.

**A consumer needs no key and no Six Labors licence.** The check is an MSBuild target in the
package's `build/` folder, which does not flow to a project that references `PdfPinata.ImageSharp4`,
and nothing is checked at run time. Six Labors: "If you use a third party package that depends on
our libraries, you do not need a license" ([licence enforcement
post](https://sixlabors.com/posts/licence-enforcement-changes/)). The 4.x description quotes it.

**A test project of its own.** `PdfPinata.Test` stays on 2.x, because one project resolves one
ImageSharp version. `PdfPinata.ImageSharp4.Tests` links the backend's test files out of it — pixel
round trip, image quality defaults, decoding parity, glyph outlines and their parity, font resolver
parity, TrueType collections, and a version test that asserts the major under `IMAGESHARP4` —
registers the 4.x backend in its own `TestBackendSetup`, and runs on `net8.0` and `net10.0` in CI. It
adds two test classes of its own: `ArcTo`, and what a consumer is told when the wrong major loads. It has no
`PackageReference` to either SixLabors package, for the key's sake.

**The Blazor sample uses `PdfPinata.ImageSharp4`**, so the trim check and the Playwright smoke test
cover it end to end.

## Deliberately not done

- **An ImageSharp 3.x package.** No 3.x release is both patched and keyless, and 3.x reaches end of
  life on 2027-05-12, sooner than any package would earn its keep.
- **A `netstandard2.1` build of the 4.x package.** ImageSharp 4 and Fonts 3 target `net8.0` only.
  Unity stays on `PdfPinata.ImageSharp`.
- **Retiring or deprecating `PdfPinata.ImageSharp`.** Unity and Apache-2.0-only consumers have no
  other ImageSharp backend. Its description says it is end of life instead.
- **Opting in to 4.x's colour-profile conversion.** Applying an embedded profile changes the pixels,
  which would make the two packages disagree. If anyone wants it, it can become an opt-in setting.

## Risk

NuGet's advisory data lags Six Labors' own listing; it still flags 3.2.0, which is fixed. If it ever
flags a 4.x release the same way, the vulnerable-package check needs a temporary allow-list entry
with a note saying when to remove it — not a permanent one like the 2.x five.
