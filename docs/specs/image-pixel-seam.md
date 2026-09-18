# Spec — the image seam widened from an invented BMP to pixels (T14)

`IImageSource.SaveAsPdfBitmap` and `PdfImage.ReadTrueColorMemoryBitmap` are gone.
`IImageSource.GetPixels()` returns a `PixelBuffer` — `Width`, `Height`, and a tightly packed,
top-down, straight-alpha `ReadOnlyMemory<byte>` in B, G, R, A order — and `PdfImage` consumes it
directly. Both backends implement the new member; `PdfPinata.Skia/PdfBitmapWriter.cs` is
deleted outright, and `ImageSharpImageSource<TPixel>` no longer references `BmpEncoder` at all.

## What shipped, against what was proposed

The plan in this document's earlier form proposed exactly this shape — a `PixelBuffer` with no
format tag, one call site, grayscale and CMYK deferred — and that is what landed. The one place
implementation diverged from the plan's own risk list is the ImageSharp channel swap, and it
diverged by confirming the risk rather than avoiding it: the plan flagged that `BmpEncoder` was
performing the R/B reorder invisibly and that removing it would need an explicit replacement, and
`ImageSharpImageSourceImpl<TPixel2>.ReadPixels` (`PdfPinata.ImageSharp/ImageSharpImageSource.cs:180-199`)
is exactly that replacement — `PixelOperations<TPixel2>.Instance.ToBgra32Bytes`, called once per row
inside `Image.ProcessPixelRows`, which the plan named as the option ImageSharp's 2.1.x line already
exposed for this. Nothing had to be hand-rolled.

A second commit landed same-day, folded into this one on the branch rather than split out: Skia's
`GetPixels()` gained a check the plan's Implementation Decisions never mentioned — refusing a
premultiplied bitmap. `SkiaImageSource.GetPixels()` (`PdfPinata.Skia/SkiaImageSource.cs:128-159`)
throws `InvalidOperationException` naming the bitmap's `AlphaType` when it is `Premul`, and passes
`Opaque` through untouched. The gap it closes: the decode path inside `SkiaImageSource.Decode`
always requests `SKAlphaType.Unpremul`, but `FromSkiaBitmap` is a public entry point that takes
whatever bitmap a caller already holds, and a premultiplied one is the wrong answer that produces a
valid-looking, silently-darkened image — `PdfImage` writes colour and alpha into separate streams,
so a premultiplied pixel is darkened in proportion to its own transparency with nothing to say why.
`SkiaImageSourceTest.GetPixelsRefusesAPremultipliedBitmap` and
`GetPixelsAcceptsAnOpaqueBitmap` pin both halves.

## The row flip, actually removed

The Problem Statement's derivation — that the write-side bottom-up flip and the read-side
`height-1-y` flip cancelled exactly, so the whole BMP round trip amounted to a top-down buffer
turned bottom-up and back — is what the new code no longer does at all. `PdfImage.InitializeNonJpeg`
(`PdfPinata/Pdf.Advanced/PdfImage.cs:156-257`) walks `y` from `0` to `height` and writes to
`imageData` at the same `y`, with the comment `// Row r of the source is row r of the output: both
are top-down and neither pads.` right above the loop. `MonochromeMask.StartLine`
(`PdfImage.cs:473-478`) takes the line number directly rather than un-flipping it, for the same
reason — it used to be called with the row already turned upside down by the BMP reader and now
isn't. There is no `height - 1 - y` anywhere in the file any more.

## Grayscale and CMYK: deferred exactly as planned

Both stayed out. `InitializeNonJpeg` writes `/DeviceRGB` unconditionally
(`PdfImage.cs:150-155`, `253`) with no branch on component count — the `components`/`bits`/`hasAlpha`
parameters `ReadTrueColorMemoryBitmap` used to carry, and the unreachable `components == 1`
`NotImplementedException` branch they gated, are gone along with the method itself rather than kept
as dead code with a new call shape. `PixelBuffer` carries no format tag, exactly as Implementation
Decisions proposed: one shape, because one shape is what either backend has ever produced. Neither
backend's decode step was touched to preserve CMYK samples — Skia still decodes unconditionally to
`Bgra8888` and ImageSharp's `TPixel` is still chosen by the caller from its RGB(A) types — so CMYK
support needs the same larger, separate change the plan described, and it wasn't taken up here.

## The asymmetric test image, and where the risk actually lived

The plan's Testing Decisions called for an asymmetric image specifically because a solid fill or a
flip-symmetric pattern would pass against a row flip or a channel swap without noticing either. Both
new test files build one. `SkiaImageSourceTest`'s `GetPixelsPutsEveryCornerWhereItBelongs`
(`PdfPinata.Test/Imaging/SkiaImageSourceTest.cs:58-76`) uses a 2×2 bitmap with four distinct
per-channel corner values and asserts the exact byte sequence, explicit about why: "a vertical flip,
a horizontal flip and a half turn are each told apart from the right answer and from each other."
`ImagePixelRoundTripTests` (`PdfPinata.Test/Imaging/ImagePixelRoundTripTests.cs`) goes one level
further out — a 3×2 image where `Colour(index)` gives every pixel a distinct R, G and B
(`static (byte R, byte G, byte B) Colour(int index) => ((byte)(10 + index * 10), (byte)(100 + index
* 10), (byte)(200 + index * 10))`, lines 36-37) and `Alphas` straddles the monochrome mask's 128
threshold on both sides (`{ 255, 200, 128, 127, 64, 0 }`, line 42) — deliberately choosing values so
that the earlier tests' failure mode (a solid-fill or symmetric image passing regardless of
orientation) cannot recur. Its own doc comment says this in so many words: "A flipped row or a
swapped channel makes a perfectly valid, perfectly wrong document that veraPDF passes and that every
solid-fill test in this repository used to pass too."

That test class is also the true end-to-end round trip the plan asked for, and it goes further than
"draw, save, reread, compare": it draws through `XGraphics.DrawImage`, saves, reopens with
`PdfReader.Open`, reads the XObject stream via `GetImagePlacements().Single()`, and asserts against
`UnfilteredValue` — which is `PdfDictionary`/`PdfStream`'s own FLATE-decoding accessor, so the test
never re-implements decompression itself. It checks all four things the plan's Testing Decisions
separated out: the main RGB stream (`TheImageStreamHoldsTheSourcePixelsRowForRowAndChannelForChannel`),
the absence of any mask when opaque (`AnOpaqueImageIsWrittenWithNoMaskOfEitherKind`), the `/SMask`'s
alpha bytes (`ThePartlyTransparentImageCarriesItsAlphaInTheSoftMask`), and the 1-bit `/Mask`'s packed
bits, with the expected byte (`0x00, 0xE0`) worked out by hand from which of the six alphas fall below
128 (`ThePartlyTransparentImageAlsoCarriesTheOlderOnebitMask`). `TheTwoBackendsWriteTheSameImage` and
`TheTwoBackendsHandBackTheSamePixels` are the cross-backend parity coverage the plan called for,
checked once at the document level and once at the seam itself — the second one exists specifically
so a divergence between backends names which one it started at rather than only the document it
produced.

## What was deliberately not carried forward

`TransparentPngRoundTripsThroughPdfImage` survived from the old `SkiaImageSourceTest` — the plan
said to rewrite it or add beside it to assert on the produced XObject dictionary rather than just
`ms.Length.Should().BeGreaterThan(0)`, but the version in the current file
(`SkiaImageSourceTest.cs:120-137`) still only checks that the saved stream is non-empty. The
dictionary-level assertion the plan wanted is instead covered by `ImagePixelRoundTripTests`, which
checks `/Mask` and `/SMask` presence and content directly — so the coverage exists, just not where
the plan said to put it, and the old test's job narrowed to "the demo path that decodes a real PNG
through `SKCodec` and draws it doesn't throw," which is still worth having since `ImagePixelRoundTripTests`
builds its bitmaps by hand rather than through a real decode.

The three tests that asserted on BMP header byte offsets —
`SaveAsPdfBitmap_WritesHeaderPdfImageAccepts`, `SaveAsPdfBitmap_WritesRowsBottomUp`,
`SaveAsPdfBitmap_KeepsStraightAlpha` — are deleted rather than migrated, as the plan said they
should be: what they pinned no longer exists once `SaveAsPdfBitmap` does not.

The four fake `IImageSource` implementers needed only the mechanical rename the plan predicted.
`ImageFailureReportingTests.cs`, `ImageFailureTests.cs`, `DdlWordWrapTests.cs`, and
`ImageFailuresDemo.cs` all now implement `PixelBuffer GetPixels()` in place of
`void SaveAsPdfBitmap(MemoryStream ms)`; three of the four return `default(PixelBuffer)` after
calling the same failure-injection callback the old method called, and none of their assertions
changed.

## What else moved with it

`XImage.AsBitmap()` became `XImage.GetPixels()` (`PdfPinata/Drawing/XImage.cs:196-202`), and this
is called out as a breaking change in `CHANGELOG.md` alongside the interface change, with a
before/after diff of the interface member and a paragraph on why the BMP shape was never a real
interchange format. `PixelBuffer` itself (`PdfPinata/Drawing/PixelBuffer.cs`) is a
`readonly struct` living in the same MigraDoc-namespaced-but-PdfPinata-assembly location as
`ImageSource` — `PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes` — for the same
reason `ImageSource` is there, and its constructor validates one thing: that the buffer is exactly
`width * height * 4` bytes, throwing `ArgumentException` if not, so a backend's arithmetic mistake is
named at the seam it was made rather than surfacing as an index out of range partway through writing
a PDF. `PdfImage.InitializeNonJpeg` does not otherwise validate the buffer it is handed — no magic
number, no declared length, no compression field — because producer and consumer are two ends of one
call in the same call stack, per Implementation Decisions.

`PdfImage.cs:161`'s `Debug.Assert(!pixels.IsEmpty, "Image decoding produced no pixels.")` is the one
place the new code is looser than a straightforward port might have been: in a `Debug` build an empty
buffer is asserted against, but in `Release` the method simply writes nothing and returns, rather than
throwing. This wasn't called out as a decision anywhere in the plan or the commit message; it reads
as inherited from the original code's own shape (a guarded block rather than a hard failure) rather
than a considered choice made during this change.

## Testing

`ImagePixelRoundTripTests` (new), `SkiaImageSourceTest` (rewritten), and `ImageSharpVersionTest`
(extended) are the three files carrying this change's coverage.
`ImageSharpVersionTest.ImageSharpBackendEncodesWhatItLoaded` now also calls `GetPixels()` on a real
decoded `lenna.png` and checks `Width`, `Height`, and `Pixels.Length` — folding the FLATE path into
the same test that already exists to catch an ImageSharp 3.x binding failure, since `ReadPixels`'s
`ToBgra32Bytes` call is exactly the kind of API a version bump would move. `verapdf-check.ps1` and
the demo smoke tests still run as before, and per the plan's own Testing Decisions their role is
unchanged: neither one is evidence about pixel correctness, only that nothing structural broke around
the change.
