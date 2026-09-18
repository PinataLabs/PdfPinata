using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Test.Helpers;
using Xunit;
using static PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Writing an image back out as MDDDL. This is the one serializer of its batch that cannot live
///   in <c>PinataLayout.DocumentObjectModel.Tests</c>: an image is made by handing
///   <c>AddImage</c> an <c>IImageSource</c>, and that seam is unset in a suite with no backend.
///   Here the module initializer has already registered Skia.
/// </summary>
public class ImageSerializationTests
{
    static string AnImagePath() => PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg");

    static Document ADocumentWithAnImage(out Image image)
    {
        var document = new Document();
        image = document.AddSection().AddImage(FromFile(AnImagePath()));
        return document;
    }

    static string Write(Document document) => DdlWriter.WriteToString(document);

    static Image RoundTrip(Document document) =>
        DdlReader.DocumentFromString(Write(document))
            .LastSection.Elements.OfType<Image>().Single();

    /// <summary>
    ///   The path is what an image mostly is, and it used to be the one thing that did not
    ///   survive being written. <c>Serialize</c> wrote an internal <c>name</c> field that nothing
    ///   in the repository ever assigns - not <c>AddImage</c>, which takes a source, and not the
    ///   parser, which puts <c>\image("path")</c> on <c>Source</c> as well - so every image in
    ///   every document was written as <c>\image("")</c>. See the backlog spec's finding F9.
    /// </summary>
    [Fact]
    public void AnImageIsWrittenWithThePathItWasGiven()
    {
        var document = ADocumentWithAnImage(out _);

        Write(document).Should().Contain("frog-and-toad.jpg");
    }

    [Fact]
    public void AnImagePathSurvivesBeingWrittenAndReadBack()
    {
        var document = ADocumentWithAnImage(out _);

        RoundTrip(document).Source.Name.Should().Be(AnImagePath());
    }

    [Fact]
    public void EverythingAnImageCanSayIsWrittenAndReadBack()
    {
        var document = ADocumentWithAnImage(out var image);
        image.ScaleWidth = 0.5;
        image.ScaleHeight = 0.25;
        image.LockAspectRatio = false;
        image.Resolution = 150;
        image.Width = Unit.FromCentimeter(4);
        image.PictureFormat.CropLeft = Unit.FromCentimeter(1);

        var reread = RoundTrip(document);

        reread.ScaleWidth.Should().BeApproximately(0.5, 1e-4);
        reread.ScaleHeight.Should().BeApproximately(0.25, 1e-4);
        reread.LockAspectRatio.Should().BeFalse();
        reread.Resolution.Should().BeApproximately(150, 1e-4);
        reread.Width.Centimeter.Should().BeApproximately(4, 1e-4);
        reread.PictureFormat.CropLeft.Centimeter.Should().BeApproximately(1, 1e-4);
    }

    [Fact]
    public void AnImageWithNothingSetButItsSourceIsStillWritten()
    {
        var document = ADocumentWithAnImage(out _);

        RoundTrip(document).Should().NotBeNull();
    }

    [Fact]
    public void ABackslashInThePathIsEscapedSoItReadsBackAsItself()
    {
        // The path is written inside a quoted literal, where a backslash would otherwise begin an
        // escape. On Windows every path has them.
        var document = ADocumentWithAnImage(out _);

        var written = Write(document);
        if (AnImagePath().Contains("\\"))
            written.Should().Contain("\\\\", "each separator is doubled on the way out");

        RoundTrip(document).Source.Name.Should().Be(AnImagePath());
    }
}
