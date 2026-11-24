using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using PdfPinata.Test.IO.Filters;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Image XObjects and inline images, drawn onto a <see cref="RecordingDevice"/>, and the exact
/// samples of the <see cref="PageImage"/> that reached it.
/// </summary>
public class ImageTests
{
    private const string DrawIm1 = "q 10 0 0 10 0 0 cm /Im1 Do Q";

    // ----- Bit depths ----------------------------------------------------------------------------

    [Test]
    public void AnEightBitRgbImageIsItsSamplesWithAnOpaqueAlpha()
    {
        var image = DrawnImage([255, 0, 0, 0, 0, 255, 10, 20, 30], ("/Width", 3), ("/Height", 1),
            ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8));

        image.Format.Should().Be(PageImageFormat.Rgba32);
        image.Data.ToArray().Should().Equal(255, 0, 0, 255, 0, 0, 255, 255, 10, 20, 30, 255);
    }

    [Test]
    public void AOneBitImageReadsEachRowFromAByteOfItsOwn()
    {
        // Three samples a row, so each row is one byte and its last five bits are padding.
        var image = DrawnImage([0b101_11111, 0b010_00000], ("/Width", 3), ("/Height", 2),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 1));

        Gray(image).Should().Equal(255, 0, 255, 0, 255, 0);
    }

    [Test]
    public void ATwoBitImageSpreadsItsFourLevelsOverTheRange()
    {
        var image = DrawnImage([0b00_01_10_11], ("/Width", 4), ("/Height", 1),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 2));

        Gray(image).Should().Equal(0, 85, 170, 255);
    }

    [Test]
    public void AFourBitImageReadsTheHighNibbleFirst()
    {
        var image = DrawnImage([0x0F, 0x8A, 0x3C, 0x00], ("/Width", 3), ("/Height", 2),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 4));

        Gray(image).Should().Equal(0, 255, 136, 51, 204, 0);
    }

    [Test]
    public void ASixteenBitImageReadsEachSampleBigEndian()
    {
        var image = DrawnImage([0x00, 0x00, 0xFF, 0xFF, 0x80, 0x00, 0x01, 0x01], ("/Width", 4), ("/Height", 1),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 16));

        Gray(image).Should().Equal(0, 255, 128, 1);
    }

    [Test]
    public void ACmykImageIsTurnedToRgbAsCmykColoursAre()
    {
        var image = DrawnImage([0, 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 255], ("/Width", 3), ("/Height", 1),
            ("/ColorSpace", "/DeviceCMYK"), ("/BitsPerComponent", 8));

        image.Data.ToArray().Should().Equal(255, 255, 255, 255, 0, 255, 255, 255, 0, 0, 0, 255);
    }

    // ----- Decode ----------------------------------------------------------------------------------

    [Test]
    public void ADecodeOfOneToZeroInvertsAnImage()
    {
        var image = DrawnImage([0, 64, 255], ("/Width", 3), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Decode", new[] { 1, 0 }));

        Gray(image).Should().Equal(255, 191, 0);
    }

    [Test]
    public void ADecodeOfOneToZeroInvertsAOneBitImage()
    {
        var image = DrawnImage([0b1100_0000], ("/Width", 4), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 1), ("/Decode", new[] { 1, 0 }));

        Gray(image).Should().Equal(0, 0, 255, 255);
    }

    [Test]
    public async Task ADecodeOfTheWrongLengthIsReportedAndTheDefaultUsed()
    {
        var drawn = await Draw([0, 255], ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Decode", new[] { 1 }));

        Gray(drawn.Device.Images.Single().Image).Should().Equal(0, 255);
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test]
    public async Task ADecodeThatFailsPartWayLeavesNothingOfItBehind()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page(DrawIm1, Resources([0, 255], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/Decode"] = new PdfArray(document, new global::PdfPinata.Pdf.PdfInteger(1), new PdfName("/Bad"));
        })));

        Gray(drawn.Device.Images.Single().Image).Should().Equal(0, 255);
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    // ----- Stencil masks ---------------------------------------------------------------------------

    [Test]
    public void AnImageMaskIsAStencilPaintedInTheFillColour()
    {
        var drawn = Drawn.Page("1 0 0 rg " + DrawIm1, Resources([0b0110_0000], ("/Width", 4), ("/Height", 1),
            ("/ImageMask", true), ("/BitsPerComponent", 1)));

        var call = drawn.Device.Images.Single();
        call.Image.Format.Should().Be(PageImageFormat.Alpha8);
        call.Image.Data.ToArray().Should().Equal(255, 0, 0, 255);
        call.Paint.Should().Be(new PagePaint(1, 0, 0));
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void AnImageMaskWithADecodeOfOneToZeroPaintsWhereItsSamplesAreOne()
    {
        var image = DrawnImage([0b0110_0000], ("/Width", 4), ("/Height", 1), ("/ImageMask", true),
            ("/Decode", new[] { 1, 0 }));

        image.Data.ToArray().Should().Equal(0, 255, 255, 0);
    }

    [Test]
    public void AMaskThatIsAStencilHidesWhereItsSamplesAreOne()
    {
        var image = DrawnImage([10, 20, 30, 40], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 2), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/Mask"] = Stream(document, [0b0100_0000, 0b1000_0000], ("/Width", 2), ("/Height", 2),
                ("/ImageMask", true), ("/BitsPerComponent", 1)).Reference;
        });

        Alpha(image).Should().Equal(255, 0, 0, 255);
        Gray(image).Should().Equal(10, 20, 30, 40);
    }

    [Test]
    public void AStencilMaskOfAnotherSizeIsStretchedOverTheImage()
    {
        // A mask of 2 by 1 over an image of 4 by 2: its left half covers, its right half hides.
        var image = DrawnImage(new byte[8], (document, entries) =>
        {
            Set(entries, ("/Width", 4), ("/Height", 2), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/Mask"] = Stream(document, [0b0100_0000], ("/Width", 2), ("/Height", 1),
                ("/ImageMask", true), ("/BitsPerComponent", 1)).Reference;
        });

        Alpha(image).Should().Equal(255, 255, 0, 0, 255, 255, 0, 0);
    }

    // A scan split into layers: a coarse colour image under a fine mask of the text.
    [Test]
    public void AStencilMaskFinerThanItsImageKeepsItsDetail()
    {
        var image = DrawnImage([100, 200], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/Mask"] = Stream(document, [0b0101_0101], ("/Width", 8), ("/Height", 1),
                ("/ImageMask", true), ("/BitsPerComponent", 1)).Reference;
        });

        image.Width.Should().Be(8);
        image.Height.Should().Be(1);
        Alpha(image).Should().Equal(255, 0, 255, 0, 255, 0, 255, 0);
        Gray(image).Should().Equal(100, 100, 100, 100, 200, 200, 200, 200);
    }

    [Test]
    public void ASoftMaskFinerThanItsImageKeepsItsDetail()
    {
        var image = DrawnImage([10, 20, 30, 40], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 2), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/SMask"] = Stream(document, Enumerable.Range(1, 16).Select(alpha => (byte)alpha).ToArray(),
                ("/Width", 4), ("/Height", 4), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        });

        image.Width.Should().Be(4);
        image.Height.Should().Be(4);
        Alpha(image).Should().Equal(Enumerable.Range(1, 16).Select(alpha => (byte)alpha));
        Gray(image).Should().Equal(10, 10, 20, 20, 10, 10, 20, 20, 30, 30, 40, 40, 30, 30, 40, 40);
    }

    // The image is 4 by 1 and the mask 1 by 2: finer across and coarser down, so each keeps what it
    // has more of.
    [Test]
    public void AMaskFinerOneWayAndCoarserTheOtherKeepsTheDetailOfBoth()
    {
        var image = DrawnImage([10, 20, 30, 40], (document, entries) =>
        {
            Set(entries, ("/Width", 4), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/Mask"] = Stream(document, [0b0000_0000, 0b1000_0000], ("/Width", 1), ("/Height", 2),
                ("/ImageMask", true), ("/BitsPerComponent", 1)).Reference;
        });

        image.Width.Should().Be(4);
        image.Height.Should().Be(2);
        Alpha(image).Should().Equal(255, 255, 255, 255, 0, 0, 0, 0);
        Gray(image).Should().Equal(10, 20, 30, 40, 10, 20, 30, 40);
    }

    [Test]
    public void AColourKeyHidesTheSamplesInsideItsRanges()
    {
        var image = DrawnImage([255, 0, 0, 250, 10, 5, 0, 0, 255], ("/Width", 3), ("/Height", 1),
            ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8), ("/Mask", new[] { 240, 255, 0, 20, 0, 20 }));

        Alpha(image).Should().Equal(0, 0, 255);
    }

    [Test]
    public void AColourKeyIsComparedWithTheSamplesBeforeTheyAreDecoded()
    {
        var image = DrawnImage([0b0001_1011], ("/Width", 4), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 2), ("/Decode", new[] { 1, 0 }), ("/Mask", new[] { 1, 2 }));

        Alpha(image).Should().Equal(255, 0, 0, 255);
        Gray(image).Should().Equal(255, 170, 85, 0);
    }

    // ----- Scaling an image up to its mask ----------------------------------------------------------

    [Test, Timeout(30_000)]
    public async Task AThinImageUnderAThinMaskAcrossItIsNotScaledToTheSquareOfBoth()
    {
        var drawn = await DrawWith(new byte[16384], (document, image) =>
        {
            Set(image, ("/Width", 16384), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            image.Elements["/SMask"] = Stream(document, new byte[16384], ("/Width", 1), ("/Height", 16384),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        });

        var image = drawn.Device.Images.Single().Image;
        image.Width.Should().Be(16384);
        image.Height.Should().Be(1);
        drawn.Problems.Should().Equal("LimitReached /Im1");
    }

    // 64 samples of an image 1 by 64 and of a mask 64 by 1 draw at 64 by 64, which is 64 times
    // what either has; one more of each is past it.
    [Test]
    [Arguments(64, true)]
    [Arguments(65, false)]
    public void AnImageIsScaledToItsMaskUpToSixtyFourTimesTheSamplesOfEither(int length, bool scaled)
    {
        var drawn = Drawn.Page(DrawIm1, Resources(new byte[length], (document, image) =>
        {
            Set(image, ("/Width", 1), ("/Height", length), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            image.Elements["/SMask"] = Stream(document, new byte[length], ("/Width", length), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        }));

        var image = drawn.Device.Images.Single().Image;
        image.Width.Should().Be(scaled ? length : 1);
        image.Height.Should().Be(length);
        drawn.Problems.Should().Equal(scaled ? [] : ["LimitReached /Im1"]);
    }

    // 20000 by 500 under 500 by 20000 is 400 million samples, over the 2^28 an image may have, and
    // is 40 times what either has, so it is the limit and not the multiple that refuses it.
    [Test, Timeout(60_000)]
    public async Task AnImageScaledToItsMaskPastTheMostSamplesAnImageMayHaveIsReported()
    {
        var drawn = await DrawWith(new byte[10_000_000], (document, image) =>
        {
            Set(image, ("/Width", 20000), ("/Height", 500), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            image.Elements["/SMask"] = Stream(document, new byte[10_000_000], ("/Width", 500), ("/Height", 20000),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        });

        var image = drawn.Device.Images.Single().Image;
        image.Width.Should().Be(20000);
        image.Height.Should().Be(500);
        drawn.Problems.Should().Equal("LimitReached /Im1");
    }

    // ----- Soft masks ------------------------------------------------------------------------------

    [Test]
    public void ASoftMaskIsTheImagesAlpha()
    {
        var image = DrawnImage([10, 20, 30], (document, entries) =>
        {
            Set(entries, ("/Width", 3), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/SMask"] = Stream(document, [0, 128, 255], ("/Width", 3), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        });

        Alpha(image).Should().Equal(0, 128, 255);
        Gray(image).Should().Equal(10, 20, 30);
    }

    [Test]
    public void ASoftMaskOfAnotherSizeIsStretchedOverTheImage()
    {
        var image = DrawnImage(new byte[4], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 2), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/SMask"] = Stream(document, [0b0100_0000], ("/Width", 1), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 2)).Reference;
        });

        Alpha(image).Should().Equal(85, 85, 85, 85);
    }

    [Test]
    public void ASoftMaskOverridesAColourKey()
    {
        var image = DrawnImage([0, 0], (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8),
                ("/Mask", new[] { 0, 10 }));
            entries.Elements["/SMask"] = Stream(document, [255, 100], ("/Width", 2), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
        });

        Alpha(image).Should().Equal(255, 100);
    }

    [Test]
    public void AMatteIsTakenBackOutOfTheColours()
    {
        // Blended with white by half: 0 was written as 127, and 201 as 228.
        var image = DrawnImage([127, 228, 77], (document, entries) =>
        {
            Set(entries, ("/Width", 3), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/SMask"] = Stream(document, [128, 128, 0], ("/Width", 3), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Matte", new[] { 1 })).Reference;
        });

        Alpha(image).Should().Equal(128, 128, 0);
        Gray(image).Should().Equal(0, 201, 77);
    }

    // ----- Filters ---------------------------------------------------------------------------------

    [Test]
    public void AFlateImageWithAPngPredictorIsUnpredicted()
    {
        // Two rows of two: the first filtered with None, the second with Up.
        var predicted = new byte[] { 0, 10, 20, 2, 5, 5 };
        var image = DrawnImage(Filtering.FlateDecode.Encode(predicted), (document, entries) =>
        {
            Set(entries, ("/Width", 2), ("/Height", 2), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8),
                ("/Filter", "/FlateDecode"));
            var parameters = new PdfDictionary(document);
            Set(parameters, ("/Predictor", 15), ("/Colors", 1), ("/BitsPerComponent", 8), ("/Columns", 2));
            entries.Elements["/DecodeParms"] = parameters;
        });

        Gray(image).Should().Equal(10, 20, 15, 25);
    }

    [Test]
    public void AChainOfFiltersIsUndoneInOrder()
    {
        var hex = Encoding.ASCII.GetBytes(Convert.ToHexString(Filtering.FlateDecode.Encode([7, 8, 9])) + ">");
        var image = DrawnImage(hex, ("/Width", 3), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Filter", new[] { "/ASCIIHexDecode", "/FlateDecode" }));

        Gray(image).Should().Equal(7, 8, 9);
    }

    // ----- CCITTFax ----------------------------------------------------------------------------

    [Test]
    public void AOneBitImageCodedInGroup4IsDrawn()
    {
        var image = DrawnImage(CcittFaxDecodeTests.Asset("g4.bin"), FaxImage(("/K", -1)));

        Gray(image).Should().Equal(TextPixels(black: 0, white: 255));
    }

    [Test]
    public void AnImageMaskCodedInGroup4PaintsWhereTheScanIsBlack()
    {
        var image = DrawnImage(CcittFaxDecodeTests.Asset("g4.bin"), (document, entries) =>
        {
            Set(entries, ("/Width", Text.Width), ("/Height", Text.Height), ("/ImageMask", true),
                ("/Filter", "/CCITTFaxDecode"), ("/DecodeParms", FaxParameters(("/K", -1))));
        });

        image.Format.Should().Be(PageImageFormat.Alpha8);
        image.Data.ToArray().Should().Equal(TextPixels(black: 255, white: 0));
    }

    [Test]
    [Arguments(false, 255, 0)]
    [Arguments(true, 0, 255)]
    public void BlackIs1SaysWhatTheFilterAnswersAndTheDecodeIsAppliedToThatAfterwards(bool inverted, int black, int white)
    {
        // /BlackIs1 belongs to the filter: its samples are 1 where the scan is black. A /DeviceGray
        // image reads 1 as white, as it reads any other samples, so a file coded that way says
        // /Decode [1 0] to draw its black as black.
        var image = DrawnImage(CcittFaxDecodeTests.Asset("g4-blackis1.bin"), (document, entries) =>
        {
            FaxImage(("/K", -1), ("/BlackIs1", true))(document, entries);
            if (inverted)
                Set(entries, ("/Decode", new[] { 1, 0 }));
        });

        Gray(image).Should().Equal(TextPixels((byte)black, (byte)white));
    }

    [Test]
    public void FaxDataCutShortIsDrawnWithTheRowsItLacksWhiteAndReported()
    {
        var data = CcittFaxDecodeTests.Asset("g4.bin");
        var drawn = Drawn.Page(DrawIm1, Resources(data.AsSpan(0, data.Length / 2).ToArray(), FaxImage(("/K", -1))));

        drawn.Problems.Should().Equal("InvalidValue /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("/CCITTFaxDecode").And.Contain("white");
        var gray = Gray(drawn.Device.Images.Single().Image);
        gray.Should().HaveCount(Text.Width * Text.Height);
        gray.Skip(Text.Width * (Text.Height - 1)).Should().OnlyContain(value => value == 255);
    }

    [Test]
    public void FaxDataWithARowDamagedAndSteppedOverIsDrawnAndReported()
    {
        var data = CcittFaxDecodeTests.Asset("g3-1d-eol.bin");
        data[data.Length / 2] = 0;
        data[data.Length / 2 + 1] = 0;
        var drawn = Drawn.Page(DrawIm1, Resources(data,
            FaxImage(("/K", 0), ("/EndOfLine", true), ("/DamagedRowsBeforeError", 5))));

        drawn.Problems.Should().Equal("InvalidValue /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("damaged rows").And.NotContain("white");
        Gray(drawn.Device.Images.Single().Image).Skip(Text.Width * (Text.Height - 1))
            .Should().Equal(TextPixels(black: 0, white: 255).Skip(Text.Width * (Text.Height - 1)));
    }

    [Test]
    public void FaxParametersOutOfRangeAreReportedAndTheImageIsNotDrawn()
    {
        var drawn = Drawn.Page(DrawIm1, Resources(CcittFaxDecodeTests.Asset("g4.bin"), FaxImage(("/K", -1), ("/Columns", 0))));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test]
    public void ColumnsFarWiderThanTheImageAreReportedAndTheImageIsNotDrawn()
    {
        var drawn = Drawn.Page(DrawIm1, Resources(CcittFaxDecodeTests.Asset("g4.bin"), FaxImage(("/K", -1), ("/Columns", 2147483640))));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test]
    [Arguments("/JBIG2Decode")]
    [Arguments("/JPXDecode")]
    public void AFilterNotDecodedYetIsReported(string filter)
    {
        var drawn = DrawnPage([0], ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 1), ("/Filter", filter));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain(filter);
    }

    // ----- JPEG ------------------------------------------------------------------------------------

    [Test]
    public void AJpegIsDecodedByTheDeviceFromTheBytesItsOtherFiltersLeave()
    {
        byte[] seen = null;
        var device = new RecordingDevice
        {
            Jpeg = data =>
            {
                seen = data.ToArray();
                return PageImage.FromRgba32(2, 1, new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 });
            }
        };
        var hex = Encoding.ASCII.GetBytes("FFD8FFD9>");

        var result = PageRendering.Render(Drawn.PageOf(DrawIm1, Resources(hex, ("/Width", 2), ("/Height", 1),
            ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8), ("/Filter", new[] { "/AHx", "/DCTDecode" }))), device);

        seen.Should().Equal(0xFF, 0xD8, 0xFF, 0xD9);
        device.Images.Single().Image.Data.ToArray().Should().Equal(1, 2, 3, 255, 4, 5, 6, 255);
        result.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void AJpegHasItsDecodeAndSoftMaskAppliedToWhatTheDeviceAnswers()
    {
        var device = new RecordingDevice
        {
            Jpeg = data => data.Span[0] == 1
                ? PageImage.FromRgba32(2, 1, new byte[] { 0, 100, 255, 255, 255, 0, 0, 255 })
                : PageImage.FromRgba32(2, 1, new byte[] { 50, 50, 50, 255, 200, 200, 200, 255 })
        };

        var result = PageRendering.Render(Drawn.PageOf(DrawIm1, (document, resources) =>
        {
            var image = Stream(document, [1], ("/Subtype", "/Image"), ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"),
                ("/BitsPerComponent", 8), ("/Filter", "/DCTDecode"), ("/Decode", new[] { 1, 0, 1, 0, 1, 0 }));
            image.Elements["/SMask"] = Stream(document, [2], ("/Width", 2), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", "/DCTDecode")).Reference;
            Drawn.Add(document, resources, "/XObject", "/Im1", image);
        }), device);

        device.Images.Single().Image.Data.ToArray().Should().Equal(255, 155, 0, 50, 0, 255, 255, 200);
        result.Diagnostics.Should().BeEmpty();
    }

    [Test]
    public void AJpegsColorTransformIsReportedBecauseTheDeviceIsHandedTheJpegAlone()
    {
        var device = new RecordingDevice { Jpeg = _ => PageImage.FromRgba32(1, 1, new byte[] { 1, 2, 3, 255 }) };

        var result = PageRendering.Render(Drawn.PageOf(DrawIm1, Resources([0xFF], (document, entries) =>
        {
            Set(entries, ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8),
                ("/Filter", "/DCTDecode"));
            var parameters = new PdfDictionary(document);
            Set(parameters, ("/ColorTransform", 0));
            entries.Elements["/DecodeParms"] = parameters;
        })), device);

        device.Images.Single().Image.Data.ToArray().Should().Equal(1, 2, 3, 255);
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("UnsupportedFeature /Im1");
    }

    // The device's convention: a JPEG of four components is answered with any Adobe inversion
    // undone, so Photoshop's CMYK JPEG, with the /Decode a PDF needs to invert it back, is exact.
    [Test]
    public void ACmykJpegWithAnAdobeMarkerAndAnInvertingDecodeIsDrawnAsTheDeviceAnswersIt()
    {
        var drawn = DrawnCmykJpeg(adobeMarker: true, decode: [1, 0, 1, 0, 1, 0, 1, 0]);

        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(0, 255, 255, 255);
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void ACmykJpegWithNeitherAnAdobeMarkerNorAnInvertingDecodeIsDrawnAsTheDeviceAnswersIt()
    {
        var drawn = DrawnCmykJpeg(adobeMarker: false, decode: null);

        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(0, 255, 255, 255);
        drawn.Problems.Should().BeEmpty();
    }

    // Ghostscript draws this file near-black, reading the inverted inks as they are stored, and
    // the device's codec answers cyan.
    [Test]
    [Arguments(null)]
    [Arguments(new[] { 0, 1, 0, 1, 0, 1, 0, 1 })]
    public void ACmykJpegWithAnAdobeMarkerAndTheDefaultDecodeIsReported(int[] decode)
    {
        var drawn = DrawnCmykJpeg(adobeMarker: true, decode);

        drawn.Device.Images.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedFeature /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("Adobe marker");
    }

    [Test]
    public void ACmykJpegWithAnInvertingDecodeAndNoAdobeMarkerIsReported()
    {
        var drawn = DrawnCmykJpeg(adobeMarker: false, decode: [1, 0, 1, 0, 1, 0, 1, 0]);

        drawn.Device.Images.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedFeature /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("no Adobe marker");
    }

    [Test]
    public void ACmykJpegWithAnyOtherDecodeIsReportedAndNotApplied()
    {
        var drawn = DrawnCmykJpeg(adobeMarker: true, decode: [0, 1, 1, 0, 0, 1, 0, 1]);

        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(0, 255, 255, 255);
        drawn.Problems.Should().Equal("UnsupportedFeature /Im1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("is not applied");
    }

    [Test]
    public void AJpegTheDeviceCannotDecodeIsReported()
    {
        var drawn = DrawnPage([0xFF, 0xD8], ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"),
            ("/BitsPerComponent", 8), ("/Filter", "/DCTDecode"));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
    }

    [Test]
    public void AJpegFilteredAgainAfterItsDecodingIsReported()
    {
        var device = new RecordingDevice { Jpeg = _ => throw new InvalidOperationException("Not to be asked.") };

        var result = PageRendering.Render(Drawn.PageOf(DrawIm1, Resources(Filtering.FlateDecode.Encode([0xFF, 0xD8]),
            ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8),
            ("/Filter", new[] { "/DCTDecode", "/FlateDecode" }))), device);

        device.Images.Should().BeEmpty();
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("UnsupportedImage /Im1");
    }

    // ----- Inline images ---------------------------------------------------------------------------

    [Test]
    public void AnInlineImageWrittenWithAbbreviationsIsDrawn()
    {
        var drawn = Drawn.Page("q 10 0 0 10 0 0 cm BI /W 2 /H 1 /CS /RGB /BPC 8 /F /AHx ID FF00000000FF> EI Q");

        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(255, 0, 0, 255, 0, 0, 255, 255);
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void AnInlineImageWrittenInFullIsDrawn()
    {
        var drawn = Drawn.Page("BI /Width 2 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Decode [1 0] ID ÿ\u0000\nEI");

        Gray(drawn.Device.Images.Single().Image).Should().Equal(0, 255);
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void AnInlineImageMaskIsAStencil()
    {
        var drawn = Drawn.Page("BI /W 4 /H 1 /IM true ID P\nEI");

        var image = drawn.Device.Images.Single().Image;
        image.Format.Should().Be(PageImageFormat.Alpha8);
        image.Data.ToArray().Should().Equal(255, 0, 255, 0);
    }

    [Test]
    public void AnInlineImageMayNameAColourSpaceOfTheResources()
    {
        var drawn = Drawn.Page("BI /W 1 /H 1 /CS /Cs1 /BPC 8 ID \u0001\u0002\u0003\nEI", (document, resources) =>
        {
            var spaces = new PdfDictionary(document);
            spaces.Elements["/Cs1"] = new PdfName("/DeviceRGB");
            resources.Elements["/ColorSpace"] = spaces;
        });

        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(1, 2, 3, 255);
    }

    [Test]
    public void AnInlineImageWithFlateAndItsParametersAbbreviatedIsUnpredicted()
    {
        var flate = Filtering.FlateDecode.Encode([0, 10, 20, 2, 5, 5]);
        var content = Encoding.Latin1.GetBytes("BI /W 2 /H 2 /CS /G /BPC 8 /F /Fl /DP << /Predictor 15 /Columns 2 >> ID ")
            .Concat(flate).Concat(Encoding.Latin1.GetBytes("\nEI")).ToArray();

        Gray(Drawn.Page(content).Device.Images.Single().Image).Should().Equal(10, 20, 15, 25);
    }

    private static readonly byte[] Palette = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0];

    private static readonly byte[][] PaletteColours =
    [
        [255, 0, 0, 255], [0, 255, 0, 255], [0, 0, 255, 255], [255, 255, 0, 255]
    ];

    [Test]
    public void AnAbbreviatedIndexedSpaceIsResolvedAsIndexed()
    {
        var drawn = Drawn.Page("BI /W 2 /H 1 /CS [/I /RGB 1 <FF000000FF00>] /BPC 8 ID \u0001\u0000\nEI");

        drawn.Problems.Should().BeEmpty();
        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(0, 255, 0, 255, 255, 0, 0, 255);
    }

    // The samples are indexes, and the default /Decode keeps them whole: 0 to 2^bits - 1.
    [Test]
    [Arguments(1, new byte[] { 0x60 }, new[] { 0, 1, 1, 0 })]
    [Arguments(2, new byte[] { 0x1B }, new[] { 0, 1, 2, 3 })]
    [Arguments(4, new byte[] { 0x30, 0x20 }, new[] { 3, 0, 2 })]
    [Arguments(8, new byte[] { 3, 2, 1, 0 }, new[] { 3, 2, 1, 0 })]
    public void AnIndexedInlineImageDrawsItsPaletteAtEveryBitDepth(int bits, byte[] row, int[] indexes)
    {
        var content = Encoding.Latin1.GetBytes($"BI /W {indexes.Length} /H 1 /CS [/I /RGB 3 <FF000000FF000000FFFFFF00>] /BPC {bits} ID ")
            .Concat(row).Concat(Encoding.Latin1.GetBytes("\nEI")).ToArray();

        var drawn = Drawn.Page(content);

        drawn.Problems.Should().BeEmpty();
        drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(indexes.SelectMany(i => PaletteColours[i]));
    }

    [Test]
    [Arguments(1, new byte[] { 0x60 }, new[] { 0, 1, 1, 0 })]
    [Arguments(2, new byte[] { 0x1B }, new[] { 0, 1, 2, 3 })]
    [Arguments(4, new byte[] { 0x30, 0x20 }, new[] { 3, 0, 2 })]
    [Arguments(8, new byte[] { 3, 2, 1, 0 }, new[] { 3, 2, 1, 0 })]
    public void AnIndexedImageXObjectDrawsItsPaletteAtEveryBitDepth(int bits, byte[] row, int[] indexes)
    {
        var image = DrawnImage(row, (document, dictionary) =>
        {
            Set(dictionary, ("/Width", indexes.Length), ("/Height", 1), ("/BitsPerComponent", bits));
            dictionary.Elements["/ColorSpace"] = new PdfArray(document,
                new PdfName("/Indexed"), new PdfName("/DeviceRGB"), new global::PdfPinata.Pdf.PdfInteger(3),
                new PdfString(Encoding.Latin1.GetString(Palette) + "ÿÿ ", PdfStringEncoding.RawEncoding));
        });

        image.Data.ToArray().Should().Equal(indexes.SelectMany(i => PaletteColours[i]));
    }

    // ----- Drawn again ------------------------------------------------------------------------------

    [Test]
    public void AnImageDrawnManyTimesIsDecodedOnce()
    {
        var decoded = 0;
        var device = new RecordingDevice
        {
            Jpeg = _ =>
            {
                decoded++;
                return PageImage.FromRgba32(1, 1, new byte[] { 1, 2, 3, 255 });
            }
        };

        PageRendering.Render(Drawn.PageOf(string.Concat(Enumerable.Repeat("/Im1 Do ", 5)), Resources([0xFF],
            ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8),
            ("/Filter", "/DCTDecode"))), device);

        decoded.Should().Be(1);
        device.Images.Should().HaveCount(5);
    }

    // The first image fills the budget for large ones, being a whole 2^28 samples.
    [Test]
    public void ASmallImageIsKeptEvenWhenLargeOnesHaveSpentTheirBudget()
    {
        var decoded = new int[3];
        var largest = new byte[1 << 30];
        var device = new RecordingDevice
        {
            Jpeg = data =>
            {
                var which = data.Span[1];
                decoded[which]++;
                return which == 1
                    ? PageImage.FromRgba32(1 << 14, 1 << 14, largest)
                    : PageImage.FromRgba32(1, 1, new byte[] { 1, 2, 3, 255 });
            }
        };

        PageRendering.Render(Drawn.PageOf("/Im1 Do /Im2 Do /Im2 Do /Im2 Do /Im1 Do", (document, resources) =>
        {
            foreach (var which in new byte[] { 1, 2 })
            {
                var image = Stream(document, [0xFF, which], ("/Subtype", "/Image"), ("/Width", 1), ("/Height", 1),
                    ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8), ("/Filter", "/DCTDecode"));
                Drawn.Add(document, resources, "/XObject", "/Im" + which, image);
            }
        }), device);

        decoded[2].Should().Be(1);
        decoded[1].Should().Be(1);
        device.Images.Should().HaveCount(5);
    }

    [Test]
    public void AnImageTheDeviceCannotDecodeIsAskedAboutOnce()
    {
        var asked = 0;
        var device = new RecordingDevice
        {
            Jpeg = _ =>
            {
                asked++;
                return null;
            }
        };

        var result = PageRendering.Render(Drawn.PageOf("/Im1 Do /Im1 Do /Im1 Do", Resources([0xFF],
            ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8),
            ("/Filter", "/DCTDecode"))), device);

        asked.Should().Be(1);
        device.Images.Should().BeEmpty();
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("UnsupportedImage /Im1");
    }

    // What is kept is the stencil's coverage. The colour is the fill colour of each draw.
    [Test]
    public void AStencilMaskDrawnInTwoColoursIsPaintedInEach()
    {
        var drawn = Drawn.Page("1 0 0 rg /Im1 Do 0 0 1 rg /Im1 Do", Resources([0b1010_0000],
            ("/Width", 4), ("/Height", 1), ("/ImageMask", true)));

        var images = drawn.Device.Images.ToArray();
        images.Should().HaveCount(2);
        images[0].Image.Data.ToArray().Should().Equal(0, 255, 0, 255);
        images[1].Image.Data.ToArray().Should().Equal(0, 255, 0, 255);
        (images[0].Paint.Red, images[0].Paint.Blue).Should().Be((1, 0));
        (images[1].Paint.Red, images[1].Paint.Blue).Should().Be((0, 1));
    }

    // One image drawn by two forms, each naming /Cs1 a space of its own: gray reads the first byte
    // as white, and CMYK reads all four as cyan.
    [Test]
    public void AnImageNamingASpaceOfTheResourcesIsDrawnInTheSpaceEachFormGivesIt()
    {
        var drawn = Drawn.Page("/Fm1 Do /Fm2 Do", (document, resources) =>
        {
            var image = Stream(document, [255, 0, 0, 0], ("/Subtype", "/Image"), ("/Width", 1), ("/Height", 1),
                ("/ColorSpace", "/Cs1"), ("/BitsPerComponent", 8));
            Drawn.Add(document, resources, "/XObject", "/Fm1", FormDrawing(document, image, "/DeviceGray"));
            Drawn.Add(document, resources, "/XObject", "/Fm2", FormDrawing(document, image, "/DeviceCMYK"));
        });

        var images = drawn.Device.Images.ToArray();
        images[0].Image.Data.ToArray().Should().Equal(255, 255, 255, 255);
        images[1].Image.Data.ToArray().Should().Equal(0, 255, 255, 255);
        drawn.Problems.Should().BeEmpty();
    }

    // /ASCII85Decode squeezes the white space out of the array it is handed, in place, so decoding
    // the stream's own bytes changed them to "zz~>", and the next render read eight zeros for four.
    [Test]
    public void DrawingAnImageLeavesItsEncodedBytesAsTheyWere()
    {
        var encoded = Encoding.ASCII.GetBytes(" z~>");
        PdfDictionary image = null;
        var page = Drawn.PageOf(DrawIm1, (document, resources) =>
        {
            image = Stream(document, encoded, ("/Subtype", "/Image"), ("/Width", 2), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", "/ASCII85Decode"));
            Drawn.Add(document, resources, "/XObject", "/Im1", image);
        });

        var first = new RecordingDevice();
        var second = new RecordingDevice();
        PageRendering.Render(page, first);
        PageRendering.Render(page, second);

        image.Stream.Value.Should().Equal(Encoding.ASCII.GetBytes(" z~>"));
        second.Images.Single().Image.Data.ToArray().Should().Equal(first.Images.Single().Image.Data.ToArray());
    }

    // ----- The dictionary --------------------------------------------------------------------------

    [Test]
    public void AWholeSizeWrittenAsARealIsRead()
    {
        var image = DrawnImage([10, 20], ("/Width", 2.0), ("/Height", 1.0), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8));

        Gray(image).Should().Equal(10, 20);
    }

    [Test, Timeout(30_000)]
    public async Task ASizeThatIsNotWholeIsReported()
    {
        var drawn = await Draw([10, 20], ("/Width", 2.5), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    // A palette written as a binary literal string is any bytes, and [ and < are two of them. The
    // dictionary is read and the image is drawn through the palette; the same string under a key of
    // no meaning is read and the image drawn.
    [Test]
    [Arguments("/CS [/I /RGB 255 (", ")]")]
    [Arguments("/CS /G /X (", ")")]
    public void AnInlineStringFullOfBracketBytesIsRead(string before, string after)
    {
        var palette = Enumerable.Range(0, 256).SelectMany(index => new[] { (byte)'<', (byte)'[', (byte)index }).ToArray();
        var content = Encoding.Latin1.GetBytes("BI /W 2 /H 1 /BPC 8 " + before).Concat(Escaped(palette))
            .Concat(Encoding.Latin1.GetBytes(after + " ID ")).Concat(new byte[] { 0, 255 })
            .Concat(Encoding.Latin1.GetBytes("\nEI")).ToArray();

        var drawn = Drawn.Page(content);

        if (before.Contains("/I "))
        {
            drawn.Problems.Should().BeEmpty();
            drawn.Device.Images.Single().Image.Data.ToArray().Should().Equal(60, 91, 0, 255, 60, 91, 255, 255);
        }
        else
            Gray(drawn.Device.Images.Single().Image).Should().Equal(0, 255);
    }

    [Test]
    public void AnInlineImageNestedAsDeepAsItMayBeIsDrawn()
    {
        var drawn = Drawn.Page("BI /W 1 /H 1 /CS /G /BPC 8 /X " + new string('[', 32) + new string(']', 32) + " ID \u0007\nEI");

        Gray(drawn.Device.Images.Single().Image).Should().Equal(7);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task AnInlineImageNestedDeeperThanItMayBeIsRefused()
    {
        var content = "BI /W 1 /H 1 /CS /G /BPC 8 /X " + new string('[', 33) + new string(']', 33) + " ID \u0007\nEI";

        var drawn = await Interruptibly.Run(() => Drawn.Page(content));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue BI");
    }

    // Many strings side by side are not nested, however many there are.
    [Test]
    public void AnInlineImageWithManyHexStringsInOneArrayIsDrawn()
    {
        var drawn = Drawn.Page("BI /W 1 /H 1 /BPC 8 /CS /G /X [" + string.Concat(Enumerable.Repeat("<00>", 1000)) + "] ID \u0007\nEI");

        Gray(drawn.Device.Images.Single().Image).Should().Equal(7);
    }

    // ----- Malformed and hostile images --------------------------------------------------------

    [Test, Timeout(30_000)]
    public async Task DataThatStopsShortIsDrawnAsFarAsItGoesAndReported()
    {
        var drawn = await Draw([1, 2, 3], ("/Width", 2), ("/Height", 2), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8));

        Gray(drawn.Device.Images.Single().Image).Should().Equal(1, 2, 3, 0);
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(120_000), NotInParallel]
    public async Task AnImageWhoseFlateDataInflatesToGigabytesIsDrawnAtItsSizeWithoutHoldingTheRest()
    {
        const long inflated = 2L * 1024 * 1024 * 1024;
        var bomb = Deflated(inflated, 7);
        bomb.Length.Should().BeLessThan(4 * 1024 * 1024, "the stream is small and what it expands to is not");

        var (drawn, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var page = DrawnPage(bomb, ("/Width", 256), ("/Height", 256), ("/ColorSpace", "/DeviceGray"),
                ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"));
            return (page, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        var image = drawn.Device.Images.Single().Image;
        (image.Width, image.Height).Should().Be((256, 256));
        Gray(image).Should().OnlyContain(b => b == 7);
        drawn.Problems.Should().BeEmpty("what a stream holds past the image is not drawn, as viewers do not draw it");
        allocated.Should().BeLessThan(32L * 1024 * 1024, "the samples are inflated to the image's size and no further");
    }

    [Test]
    public void RunLengthDataLongerThanTheSamplesItDecodesToIsDrawnWhole()
    {
        // 128 samples written as 64 runs of two literals, each behind its own length byte: 192
        // bytes for 128, the most RunLength spends.
        var samples = Enumerable.Range(0, 128).Select(i => (byte)i).ToArray();
        var runs = samples.Chunk(2).SelectMany(pair => new byte[] { 1, pair[0], pair[1] }).Append((byte)128).ToArray();

        var image = DrawnImage(Filtering.FlateDecode.Encode(runs), ("/Width", 128), ("/Height", 1),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", new[] { "/FlateDecode", "/RunLengthDecode" }));

        Gray(image).Should().Equal(samples);
    }

    // A 1-bit image three samples wide needs one byte, and a predictor whose /DecodeParms leave
    // out /BitsPerComponent works in rows of three, eight bits to a sample. The filter is held to
    // one whole row of those, or the predictor would find none and leave nothing to draw.
    [Test]
    [Arguments(2, new byte[] { 0b1010_0000, 0, 0 })]
    [Arguments(12, new byte[] { 2, 0b1010_0000, 0, 0 })]
    public void AnImageWhosePredictorRowIsWiderThanTheWholeImageIsDrawn(int predictor, byte[] rows)
    {
        var image = DrawnImage(Filtering.FlateDecode.Encode(rows), (document, image) =>
        {
            Set(image, ("/Width", 3), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 1),
                ("/Filter", "/FlateDecode"));
            image.Elements["/DecodeParms"] = Dictionary(document, ("/Predictor", predictor), ("/Columns", 3));
        });

        Gray(image).Should().Equal(255, 0, 255);
    }

    // A /DecodeParms entry of the wrong type is the filter's to refuse, as it always was: an
    // image's mask is then left off and the image drawn, and a filter that reads no predictor
    // does not look at it.
    [Test]
    [Arguments("/Columns", 2.5)]
    [Arguments("/Predictor", "/Foo")]
    public void AMaskWhosePredictorCannotBeReadIsLeftOffAndTheImageDrawn(string key, object value)
    {
        var drawn = Drawn.Page(DrawIm1, Resources([10, 20, 30, 40, 50, 60], (document, image) =>
        {
            Set(image, ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerComponent", 8));
            var mask = Stream(document, Filtering.FlateDecode.Encode([0, 100, 200]), ("/Width", 2), ("/Height", 1),
                ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"));
            mask.Elements["/DecodeParms"] = Dictionary(document, ("/Predictor", 12), (key, value));
            image.Elements["/SMask"] = mask.Reference;
        }));

        var image = drawn.Device.Images.Single().Image;
        Gray(image).Should().Equal(10, 40);
        Alpha(image).Should().Equal(255, 255);
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
    }

    [Test]
    public void APredictorThatCannotBeReadIsIgnoredByAFilterThatReadsNone()
    {
        var image = DrawnImage(Filtering.ASCIIHexDecode.Encode([1, 2]), (document, image) =>
        {
            Set(image, ("/Width", 2), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8),
                ("/Filter", "/ASCIIHexDecode"));
            image.Elements["/DecodeParms"] = Dictionary(document, ("/Predictor", "/Foo"));
        });

        Gray(image).Should().Equal(1, 2);
    }

    // /JPXDecode may leave the colour space to the codestream (ISO 32000-1 8.9.5), so a JPX image
    // with none is reported as not decoded rather than as missing one.
    [Test]
    public void AJpxImageWithNoColourSpaceIsReportedAsNotDecodedYet()
    {
        var drawn = DrawnPage([1, 2, 3], ("/Width", 1), ("/Height", 1), ("/BitsPerComponent", 8), ("/Filter", "/JPXDecode"));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Result.Diagnostics.Single().Message.Should().Contain("/JPXDecode, which is not decoded yet");
    }

    [Test, Timeout(30_000)]
    public async Task AnImageOfAbsurdSizeIsRefusedBeforeAnythingIsDecoded()
    {
        var drawn = await Draw([0], ("/Width", 100_000), ("/Height", 100_000), ("/ColorSpace", "/DeviceRGB"),
            ("/BitsPerComponent", 8));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("LimitReached /Im1");
    }

    [Test, Timeout(30_000)]
    [Arguments("/Width")]
    [Arguments("/Height")]
    public async Task AnImageWithoutASizeIsReported(string missing)
    {
        var entries = new (string, object)[] { ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8) }
            .Where(entry => entry.Item1 != missing).ToArray();

        var drawn = await Draw([0], entries);

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task ABitDepthThatIsNoneOfTheFiveIsReported()
    {
        var drawn = await Draw([0], ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 3));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task FlateDataThatIsNotFlateIsReported()
    {
        var drawn = await Draw([1, 2, 3, 4, 5, 6], ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
    }

    [Test, Timeout(30_000)]
    [Arguments(4, false)]
    [Arguments(8, true)]
    public async Task FlateDataCutOffPartWayIsDrawnAsFarAsItInflatedAndReported(int eighthsKept, bool drawsTheImage)
    {
        var samples = Enumerable.Range(0, 64).Select(value => (byte)(value + 1)).ToArray();
        var flate = Filtering.FlateDecode.Encode(samples);
        var kept = flate.Length * eighthsKept / 8 - (eighthsKept == 8 ? 8 : 0);

        var drawn = await Interruptibly.Run(() => Drawn.Page(DrawIm1 + " 0 0 5 5 re f", Resources(
            flate.AsSpan(0, kept).ToArray(), ("/Width", 8), ("/Height", 8),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"))));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
        var message = drawn.Result.Diagnostics.Single().Message;
        message.Should().Contain("damaged", "the one report on the image says why its data is short");
        if (!drawsTheImage)
        {
            message.Should().Contain("not drawn", "what came before the damage is too little to draw");
            drawn.Device.Images.Should().BeEmpty("less than half of the samples inflated");
            return;
        }

        message.Should().Contain("as if every sample were 0", "the rest of the image is drawn as zeros");

        var gray = Gray(drawn.Device.Images.Single().Image);
        var inflated = gray.TakeWhile(sample => sample != 0).Count();
        inflated.Should().BeInRange(32, 63);
        gray.Should().Equal(samples.Take(inflated).Concat(new byte[64 - inflated]), "what inflated is drawn and the rest is 0");
    }

    [Test, Timeout(30_000)]
    [Arguments(true)]
    [Arguments(false)]
    public async Task FlateDataWithItsChecksumMissingOrWrongIsDrawnWholeAndReported(bool cut)
    {
        var samples = Enumerable.Range(0, 100 * 100).Select(i => (byte)(i % 100 + i / 100)).ToArray();
        var flate = Filtering.FlateDecode.Encode(samples);
        if (cut)
            flate = flate[..^4];
        else
            flate[^1] ^= 0xFF;

        var drawn = await Draw(flate, ("/Width", 100), ("/Height", 100), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"));

        Gray(drawn.Device.Images.Single().Image).Should().Equal(samples);
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task AnUnknownFilterIsReported()
    {
        var drawn = await Draw([0], ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 8), ("/Filter", "/NoSuchDecode"));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task AnImageWithNoColourSpaceIsReported()
    {
        var drawn = await Draw([0], ("/Width", 1), ("/Height", 1), ("/BitsPerComponent", 8));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task AnImageWithNoStreamIsReportedAndThePageDrawn()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("/Im1 Do 0 0 5 5 re f", (document, resources) =>
        {
            var image = new PdfDictionary(document);
            image.Elements.SetName("/Subtype", "/Image");
            Drawn.Add(document, resources, "/XObject", "/Im1", image);
        }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task AMaskThatCannotBeReadLeavesTheImageUnmasked()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page(DrawIm1, Resources([10], (document, entries) =>
        {
            Set(entries, ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            entries.Elements["/SMask"] = Stream(document, [0], ("/Width", 1), ("/ColorSpace", "/DeviceGray")).Reference;
        })));

        Alpha(drawn.Device.Images.Single().Image).Should().Equal(255);
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    [Arguments("BI /W 1 /H 1 /CS /G /BPC 8 ID \u0000\nEI 0 0 5 5 re f")]
    [Arguments("BI /W 1 /H ID \u0000\nEI 0 0 5 5 re f")]
    [Arguments("BI /W 1 0 R /H 1 /CS /G /BPC 8 ID \u0000\nEI 0 0 5 5 re f")]
    [Arguments("BI /W 1 /H 1 /CS /G /BPC 8 /D [[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[[ ID \u0000\nEI 0 0 5 5 re f")]
    [Arguments("BI /W 1 /H 1 /CS /G /BPC 8 /D ] ] >> >> ID X\nEI 0 0 5 5 re f")]
    [Arguments("BI ) ] >> ID \u0000\nEI 0 0 5 5 re f")]
    public async Task AnInlineImageOfAnyDictionaryLeavesThePageDrawn(string content)
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page(content));

        drawn.Device.Fills.Should().ContainSingle();
    }

    [Test, Timeout(30_000)]
    public async Task AnInlineImageNestedDeeperThanItCouldBeIsRefusedUnread()
    {
        var content = "BI /W 1 /H 1 /CS /G /BPC 8 /DP " + new string('[', 100_000) + " ID \u0000\nEI";

        var drawn = await Interruptibly.Run(() => Drawn.Page(content));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue BI");
    }

    [Test, Timeout(30_000)]
    [Arguments("/X <(> ")]
    [Arguments("/X <%> ")]
    [Arguments("/X <]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]]> ")]
    public async Task NestingAfterTextTheLexerMightReadAsAStringIsRefusedAllTheSame(string hiding)
    {
        var content = "BI /W 1 /H 1 /CS /G /BPC 8 " + hiding + "/DP " + new string('[', 100_000) + " ID \u0000\nEI";

        var drawn = await Interruptibly.Run(() => Drawn.Page(content));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue BI");
    }

    [Test, Timeout(30_000)]
    public async Task DataOfLessThanHalfAnImageIsRefused()
    {
        var drawn = await Draw([1], ("/Width", 100), ("/Height", 100), ("/ColorSpace", "/DeviceRGB"),
            ("/BitsPerComponent", 8));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Im1");
    }

    [Test, Timeout(30_000)]
    public async Task DecodeParmsWithoutAFilterAreIgnored()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page(DrawIm1, Resources([9], (document, entries) =>
        {
            Set(entries, ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8));
            var parameters = new PdfDictionary(document);
            Set(parameters, ("/Predictor", 15));
            entries.Elements["/DecodeParms"] = parameters;
        })));

        Gray(drawn.Device.Images.Single().Image).Should().Equal(9);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task AnInlineImageNamingAColourSpaceTheResourcesLackIsReported()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("BI /W 1 /H 1 /CS /Cs9 /BPC 8 ID \u0001\nEI"));

        drawn.Device.Images.Should().BeEmpty();
        drawn.Problems.Should().Equal("MissingResource /Cs9");
    }


    // ----- Scaling an image to a mask of another shape ---------------------------------------

    private static Drawn Crossed(string content, int imageWidth, int imageHeight, int maskWidth, int maskHeight, string space = "/DeviceGray")
    {
        return Drawn.Page(content, (document, resources) =>
        {
            if (space != "/DeviceGray")
            {
                var spaces = new PdfDictionary(document);
                spaces.Elements[space] = new PdfName("/DeviceGray");
                resources.Elements["/ColorSpace"] = spaces;
            }
            Resources(new byte[imageWidth * imageHeight], (doc, image) =>
            {
                Set(image, ("/Width", imageWidth), ("/Height", imageHeight), ("/ColorSpace", space), ("/BitsPerComponent", 8));
                image.Elements["/SMask"] = Stream(doc, new byte[maskWidth * maskHeight], ("/Width", maskWidth), ("/Height", maskHeight),
                    ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8)).Reference;
            })(document, resources);
        });
    }

    [Test]
    [Arguments(2, 2, 200, 200, 200, 200, 0)]      // both axes, a hundred times each way: the mask's own size
    [Arguments(1, 4, 400, 4, 400, 4, 0)]          // one axis only
    [Arguments(4, 1, 4, 400, 4, 400, 0)]          // the other axis only
    [Arguments(2, 128, 128, 2, 128, 128, 0)]      // crossed, exactly 64 times
    [Arguments(2, 130, 130, 2, 2, 130, 1)]        // crossed, 65 times
    [Arguments(1, 64, 64, 1, 64, 64, 0)]
    [Arguments(1, 65, 65, 1, 1, 65, 1)]
    [Arguments(3, 300, 100, 5, 100, 300, 0)]      // crossed and uneven: 30000 against 64 * 900
    [Arguments(1, 1000, 1000, 1, 1, 1000, 1)]
    [Arguments(200, 200, 2, 2, 200, 200, 0)]      // a mask coarser than the image
    public void AnImageUnderAMaskIsScaledUpOnlyWithinTheCap(int iw, int ih, int mw, int mh, int dw, int dh, int problems)
    {
        var drawn = Crossed(DrawIm1, iw, ih, mw, mh);
        var image = drawn.Device.Images.Single().Image;
        (image.Width, image.Height).Should().Be((dw, dh));
        drawn.Problems.Should().HaveCount(problems);
        if (problems > 0) drawn.Problems.Should().OnlyContain(p => p == "LimitReached /Im1");
    }

    [Test]
    public void ACappedScaleUpIsReportedOncePerImageKept()
    {
        var drawn = Crossed("/Im1 Do /Im1 Do /Im1 Do /Im1 Do", 1, 65, 65, 1);
        drawn.Device.Images.Should().HaveCount(4);
        drawn.Problems.Should().Equal("LimitReached /Im1");
    }

    [Test]
    public void ACappedScaleUpOfAnImageNotKeptIsReportedOnce()
    {
        var drawn = Crossed("/Im1 Do /Im1 Do /Im1 Do /Im1 Do", 1, 65, 65, 1, "/Cs1");
        drawn.Device.Images.Should().HaveCount(4);
        drawn.Problems.Should().Equal("LimitReached /Im1");
    }

    // ----- Helpers ---------------------------------------------------------------------------------

    private static Task<Drawn> Draw(byte[] data, params (string Key, object Value)[] entries) =>
        Interruptibly.Run(() => DrawnPage(data, entries));

    private static Task<Drawn> DrawWith(byte[] data, Action<PdfDocument, PdfDictionary> entries) =>
        Interruptibly.Run(() => Drawn.Page(DrawIm1, Resources(data, entries)));

    private static Drawn DrawnPage(byte[] data, params (string Key, object Value)[] entries) =>
        Drawn.Page(DrawIm1, Resources(data, entries));

    /// <summary>The one image an image XObject drew, having drawn with nothing reported.</summary>
    private static PageImage DrawnImage(byte[] data, params (string Key, object Value)[] entries) =>
        DrawnImage(data, (_, image) => Set(image, entries));

    private static PageImage DrawnImage(byte[] data, Action<PdfDocument, PdfDictionary> entries)
    {
        var drawn = Drawn.Page(DrawIm1, Resources(data, entries));
        drawn.Problems.Should().BeEmpty();
        return drawn.Device.Images.Single().Image;
    }

    private static Action<PdfDocument, PdfDictionary> Resources(byte[] data, params (string Key, object Value)[] entries) =>
        Resources(data, (_, image) => Set(image, entries));

    private static Action<PdfDocument, PdfDictionary> Resources(byte[] data, Action<PdfDocument, PdfDictionary> entries) =>
        (document, resources) =>
        {
            var image = Stream(document, data, ("/Subtype", "/Image"));
            entries(document, image);
            Drawn.Add(document, resources, "/XObject", "/Im1", image);
        };

    private static PdfDictionary Dictionary(PdfDocument document, params (string Key, object Value)[] entries)
    {
        var dictionary = new PdfDictionary(document);
        Set(dictionary, entries);
        return dictionary;
    }

    /// <summary>A stream object holding <paramref name="data"/> as it is, with the entries given.</summary>
    private static PdfDictionary Stream(PdfDocument document, byte[] data, params (string Key, object Value)[] entries)
    {
        var stream = new PdfDictionary(document);
        document.Internals.AddObject(stream);
        stream.CreateStream(data);
        Set(stream, entries);
        return stream;
    }

    private static void Set(PdfDictionary dictionary, params (string Key, object Value)[] entries)
    {
        foreach (var (key, value) in entries)
            dictionary.Elements[key] = Item(value);
    }

    private static PdfItem Item(object value) => value switch
    {
        PdfDictionary dictionary => dictionary,
        int integer => new global::PdfPinata.Pdf.PdfInteger(integer),
        double real => new PdfReal(real),
        bool boolean => new PdfBoolean(boolean),
        string name => new PdfName(name),
        int[] numbers => new PdfArray(null, numbers.Select(number => (PdfItem)new global::PdfPinata.Pdf.PdfInteger(number)).ToArray()),
        string[] names => new PdfArray(null, names.Select(name => (PdfItem)new PdfName(name)).ToArray()),
        _ => throw new ArgumentException("No PDF item for " + value)
    };

    /// <summary>
    /// A CMYK JPEG drawn by a device that answers it as cyan. The JPEG is the markers up to its
    /// first scan and no data, which is all the core reads of it.
    /// </summary>
    private static Drawn DrawnCmykJpeg(bool adobeMarker, int[] decode)
    {
        byte[] app0 = [0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0];
        byte[] app14 = adobeMarker ? [0xFF, 0xEE, 0x00, 0x0E, .. "Adobe"u8, 0, 100, 0, 0, 0, 0, 2] : [];
        byte[] jpeg = [0xFF, 0xD8, .. app0, .. app14, 0xFF, 0xDA, 0x00, 0x02, 0xFF, 0xD9];
        var device = new RecordingDevice { Jpeg = _ => PageImage.FromRgba32(1, 1, new byte[] { 0, 255, 255, 255 }) };

        var result = PageRendering.Render(Drawn.PageOf(DrawIm1, Resources(jpeg, (_, image) =>
        {
            Set(image, ("/Width", 1), ("/Height", 1), ("/ColorSpace", "/DeviceCMYK"), ("/BitsPerComponent", 8),
                ("/Filter", "/DCTDecode"));
            if (decode != null)
                Set(image, ("/Decode", decode));
        })), device);
        return new Drawn(result, device);
    }

    /// <summary>A form that draws <paramref name="image"/> with <c>/Cs1</c> in its resources naming <paramref name="space"/>.</summary>
    private static PdfDictionary FormDrawing(PdfDocument document, PdfDictionary image, string space)
    {
        var form = Drawn.Form(document, "q 10 0 0 10 0 0 cm /Im1 Do Q");
        var resources = new PdfDictionary(document);
        form.Elements["/Resources"] = resources;
        Drawn.Add(document, resources, "/XObject", "/Im1", image);
        var spaces = new PdfDictionary(document);
        spaces.Elements["/Cs1"] = new PdfName(space);
        resources.Elements["/ColorSpace"] = spaces;
        return form;
    }

    /// <summary>Bytes as the inside of a literal string: each as it is, but for the three a string escapes.</summary>
    private static byte[] Escaped(byte[] bytes) =>
        bytes.SelectMany(b => b is (byte)'(' or (byte)')' or (byte)'\\' ? new[] { (byte)'\\', b } : new[] { b }).ToArray();

    private static CcittFaxDecodeTests.Pbm Text => CcittFaxDecodeTests.Text;

    private static Action<PdfDocument, PdfDictionary> FaxImage(params (string Key, object Value)[] parameters) =>
        (_, image) => Set(image, ("/Width", Text.Width), ("/Height", Text.Height), ("/ColorSpace", "/DeviceGray"),
            ("/BitsPerComponent", 1), ("/Filter", "/CCITTFaxDecode"), ("/DecodeParms", FaxParameters(parameters)));

    private static PdfDictionary FaxParameters(params (string Key, object Value)[] parameters)
    {
        var dictionary = new PdfDictionary();
        Set(dictionary, [("/Columns", Text.Width), ("/Rows", Text.Height), .. parameters]);
        return dictionary;
    }

    private static byte[] TextPixels(byte black, byte white) =>
        Enumerable.Range(0, Text.Width * Text.Height)
            .Select(at => Text.IsBlack(at % Text.Width, at / Text.Width) ? black : white).ToArray();

    /// <summary>A zlib stream of <paramref name="length"/> bytes, each <paramref name="value"/>.</summary>
    private static byte[] Deflated(long length, byte value)
    {
        using var output = new System.IO.MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            var chunk = new byte[1024 * 1024];
            Array.Fill(chunk, value);
            for (long done = 0; done < length; done += chunk.Length)
                zlib.Write(chunk, 0, (int)Math.Min(chunk.Length, length - done));
        }

        return output.ToArray();
    }

    /// <summary>The red of every sample, which is the gray of a gray image.</summary>
    private static byte[] Gray(PageImage image) => Channel(image, 0);

    private static byte[] Alpha(PageImage image) => Channel(image, 3);

    private static byte[] Channel(PageImage image, int channel) =>
        image.Data.ToArray().Where((_, index) => index % 4 == channel).ToArray();
}
