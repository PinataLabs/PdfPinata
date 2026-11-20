using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
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

    [Test]
    [Arguments("/CCITTFaxDecode")]
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

    [Test]
    public void AnAbbreviatedIndexedSpaceIsResolvedAsIndexed()
    {
        // Indexed is resolved by #372. Until then the image is reported under the family's full name.
        var drawn = Drawn.Page("BI /W 1 /H 1 /CS [/I /RGB 1 <FF000000FF00>] /BPC 8 ID \u0001\nEI");

        drawn.Problems.Should().Contain("UnsupportedColorSpace /Indexed");
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
    public async Task FlateDataCutOffPartWayIsReportedAndThePageDrawn()
    {
        var flate = Filtering.FlateDecode.Encode(Enumerable.Range(0, 64).Select(value => (byte)value).ToArray());

        var drawn = await Interruptibly.Run(() => Drawn.Page(DrawIm1 + " 0 0 5 5 re f", Resources(
            flate.AsSpan(0, flate.Length / 2).ToArray(), ("/Width", 8), ("/Height", 8),
            ("/ColorSpace", "/DeviceGray"), ("/BitsPerComponent", 8), ("/Filter", "/FlateDecode"))));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedImage /Im1");
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
    public async Task OpeningsHiddenBehindAStringAreCountedAllTheSame(string hiding)
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

    // ----- Helpers ---------------------------------------------------------------------------------

    private static Task<Drawn> Draw(byte[] data, params (string Key, object Value)[] entries) =>
        Interruptibly.Run(() => DrawnPage(data, entries));

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
        int integer => new global::PdfPinata.Pdf.PdfInteger(integer),
        bool boolean => new PdfBoolean(boolean),
        string name => new PdfName(name),
        int[] numbers => new PdfArray(null, numbers.Select(number => (PdfItem)new global::PdfPinata.Pdf.PdfInteger(number)).ToArray()),
        string[] names => new PdfArray(null, names.Select(name => (PdfItem)new PdfName(name)).ToArray()),
        _ => throw new ArgumentException("No PDF item for " + value)
    };

    /// <summary>The red of every sample, which is the gray of a gray image.</summary>
    private static byte[] Gray(PageImage image) => Channel(image, 0);

    private static byte[] Alpha(PageImage image) => Channel(image, 3);

    private static byte[] Channel(PageImage image, int channel) =>
        image.Data.ToArray().Where((_, index) => index % 4 == channel).ToArray();
}
