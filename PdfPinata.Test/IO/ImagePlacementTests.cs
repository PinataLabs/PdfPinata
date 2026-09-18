using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.ImagePlacementFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   The stream of an image XObject is the image the way it is stored, which is not always the
///   way the page shows it: a writer of PDF may store an image upside down and turn it back
///   over with a negative vertical scale as it draws it. Pulling the stream out and saving it
///   then gives an image the wrong way up.
///   See https://github.com/ststeiger/PdfSharpCore/issues/448.
/// </summary>
public class ImagePlacementTests
{
    [Fact]
    public void AnImageDrawnUprightIsStoredTheWayItIsShown()
    {
        var placements = PlacementsOf(PageDrawingAnImage("100 0 0 100 10 10"));

        placements.Should().ContainSingle();
        placements[0].Name.Should().Be("/Im0");
        placements[0].Orientation.Should().Be(PdfImageOrientation.Normal);
        placements[0].IsMirrored.Should().BeFalse();
    }

    [Fact]
    public void AnImageDrawnWithANegativeVerticalScaleIsStoredUpsideDown()
    {
        // What the file on the issue does, and what makes the extracted image come out flipped.
        var placements = PlacementsOf(PageDrawingAnImage("100 0 0 -100 10 110"));

        placements.Should().ContainSingle();
        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
        placements[0].IsMirrored.Should().BeTrue();
    }

    [Fact]
    public void AnImageDrawnWithANegativeHorizontalScaleIsStoredBackToFront()
    {
        var placements = PlacementsOf(PageDrawingAnImage("-100 0 0 100 110 10"));

        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipHorizontal);
        placements[0].IsMirrored.Should().BeTrue();
    }

    [Fact]
    public void AnImageDrawnWithBothScalesNegativeIsStoredTurnedHalfWayRound()
    {
        var placements = PlacementsOf(PageDrawingAnImage("-100 0 0 -100 110 110"));

        // Both flips at once is a turn, and a turn is no reflection.
        placements[0].Orientation.Should().Be(PdfImageOrientation.Rotate180);
        placements[0].IsMirrored.Should().BeFalse();
    }

    [Fact]
    public void AnImageDrawnTurnedAQuarterIsNeitherWayUp()
    {
        var placements = PlacementsOf(PageDrawingAnImage("0 100 -100 0 110 10"));

        placements[0].Orientation.Should().Be(PdfImageOrientation.Other);
    }

    [Fact]
    public void AMatrixOffSquareOnlyByARoundingErrorIsStillSquare()
    {
        // Judging the off-diagonal against the size of the transform rather than against a
        // fixed figure keeps a matrix written out to a few decimal places from reading as
        // turned when nothing about it is.
        var placements = PlacementsOf(PageDrawingAnImage("100 0.0000001 -0.0000001 -100 10 110"));

        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
    }

    [Fact]
    public void TheSizeOfTheImageIsTakenFromTheImageAndNotFromTheTransform()
    {
        var placements = PlacementsOf(PageDrawingAnImage("100 0 0 -100 10 110"));

        placements[0].PixelWidth.Should().Be(40);
        placements[0].PixelHeight.Should().Be(30);
        placements[0].GetRawStream().Length.Should().Be(1200);
    }

    [Fact]
    public void TheTransformIsTheOneTheImageWasDrawnUnder()
    {
        var placements = PlacementsOf(PageDrawingAnImage("100 0 0 -100 10 110"));

        var transform = placements[0].Transform;
        transform.M11.Should().Be(100);
        transform.M22.Should().Be(-100);
        transform.OffsetX.Should().Be(10);
        transform.OffsetY.Should().Be(110);
    }

    [Fact]
    public void AFormStoringAnImageUpsideDownTurnsOverAnImageDrawnUprightWithinIt()
    {
        var placements = PlacementsOf(
            PageDrawingAnImageInsideAForm("1 0 0 -1 0 200", "100 0 0 100 10 10"));

        // The image is drawn upright, so only by reading the form's matrix as well does the
        // way the page shows it come out.
        placements.Should().ContainSingle();
        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
    }

    [Fact]
    public void TwoFlipsOneInTheFormAndOneInTheImageCancelOut()
    {
        var placements = PlacementsOf(
            PageDrawingAnImageInsideAForm("1 0 0 -1 0 200", "100 0 0 -100 10 110"));

        placements[0].Orientation.Should().Be(PdfImageOrientation.Normal);
    }

    [Fact]
    public void TheTransformOfAFormIsCarriedIntoTheSpaceThePageDrawsIn()
    {
        var placements = PlacementsOf(
            PageDrawingAnImageInsideAForm("1 0 0 -1 0 200", "100 0 0 100 10 10"));

        // The image sits at y 10 in the form, and the form turns that over about y 100.
        placements[0].Transform.OffsetX.Should().Be(10);
        placements[0].Transform.OffsetY.Should().Be(190);
    }

    [Fact]
    public void RestoringTheStatePutsBackTheTransformThatWasSavedWithIt()
    {
        var placements = PlacementsOf(PageRestoringTheStateBeforeDrawing());

        // The flip was set inside a q Q that closed before the image was drawn.
        placements[0].Orientation.Should().Be(PdfImageOrientation.Normal);
    }

    [Fact]
    public void ContentSplitAcrossStreamsIsReadAsOne()
    {
        var placements = PlacementsOf(PageWhoseContentIsSplitAcrossStreams());

        // The matrix is broken across two of the three streams and the drawing across the third.
        placements.Should().ContainSingle();
        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
    }

    [Fact]
    public void AFormDrawingItselfIsReadOnce()
    {
        var placements = PlacementsOf(PageWithAFormDrawingItself());

        placements.Should().ContainSingle();
        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
    }

    [Fact]
    public void TheSameImageDrawnTwiceIsReportedOnceForEachDrawing()
    {
        var placements = PlacementsOf(PageDrawingOneImageTwice());

        // One image, two drawings, and the way up is a property of the drawing.
        placements.Select(placement => placement.Orientation)
            .Should().Equal(PdfImageOrientation.Normal, PdfImageOrientation.FlipVertical);
        placements[0].XObject.Should().BeSameAs(placements[1].XObject);
    }

    [Fact]
    public void APageHoldingAnInlineImageIsReadNoFurtherThanTheInlineImage()
    {
        var placements = PlacementsOf(PageWithAnInlineImage());

        // Reading over an inline image is guesswork, and a transform picked up from the middle
        // of image data would report the images after it the wrong way up.
        placements.Should().BeEmpty();
    }

    [Fact]
    public void APageWhoseContentCannotBeReadDrawsNothingThatCanBeTold()
    {
        var placements = PlacementsOf(PageWhoseContentCannotBeRead());

        placements.Should().BeEmpty();
    }

    [Fact]
    public void AnOperatorGivenTooFewOperandsDoesNotStopTheReading()
    {
        // Documents in the wild carry these, and a debug build used to assert on one.
        var placements = PlacementsOf(PageWithATruncatedOperator());

        placements.Should().ContainSingle();
        placements[0].Orientation.Should().Be(PdfImageOrientation.FlipVertical);
    }

    [Fact]
    public void ANumberWithMoreThanNineDecimalPlacesIsReadToTheEnd()
    {
        // The rotation matrices a writer of PDF puts out carry as many decimal places as a
        // double will hold. Reading one used to run off the end of a table of powers of ten,
        // and the ten decimal places it did read were all it kept.
        var content = ContentReader.ReadContent(
            Encoding.Latin1.GetBytes("0.819152044288992 0.573576436351046 0 1 0 0 cm"));

        var operands = content.OfType<COperator>().Single().Operands;
        ((CReal)operands[0]).Value.Should().Be(0.819152044288992);
        ((CReal)operands[1]).Value.Should().Be(0.573576436351046);
    }

    [Fact]
    public void ANumberIsReadTheSameWayWhicheverSideOfThePointItFallsOn()
    {
        var content = ContentReader.ReadContent(Encoding.Latin1.GetBytes("-12.5 .25 4. 0 0 0 cm"));

        var operands = content.OfType<COperator>().Single().Operands;
        ((CReal)operands[0]).Value.Should().Be(-12.5);
        ((CReal)operands[1]).Value.Should().Be(0.25);
        ((CReal)operands[2]).Value.Should().Be(4);
    }

    private static PdfImagePlacement[] PlacementsOf(byte[] document)
    {
        var opened = Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);
        return opened.Pages[0].GetImagePlacements().ToArray();
    }
}
