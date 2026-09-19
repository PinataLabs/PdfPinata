using System.Collections.Concurrent;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   The page setup a section starts out from is one object shared by the whole process, and it
///   used to be filled in on first use: the thread that got there first published it before it
///   had written the page size and the margins onto it, so a thread coming in behind could find
///   the field set and carry off a page setup that was still half empty.
/// </summary>
public class ConcurrentRenderingTests
{
    // A4 is 21cm by 29.7cm, which is this many points.
    const double A4Width = 595.276;
    const double A4Height = 841.89;

    const int Renderers = 32;

    [Fact]
    public void SeveralThreadsRenderingAtOnceAllGetTheSamePageSize()
    {
        var sizes = new ConcurrentBag<(double Width, double Height)>();

        Parallel.For(0, Renderers, _ =>
        {
            var document = new Document();
            document.AddSection().AddParagraph("Hello world");

            var renderer = new PdfDocumentRenderer(true) { Document = document };
            renderer.RenderDocument();

            var page = renderer.PdfDocument.Pages[0];
            sizes.Add((page.Width.Point, page.Height.Point));
        });

        // Whichever thread reached the shared defaults first, every document is still A4. This
        // can only catch the race when it is the first thing in the process to render, so it
        // is a guard rather than a proof.
        sizes.Should().HaveCount(Renderers);
        sizes.Should().AllSatisfy(size =>
        {
            size.Width.Should().BeApproximately(A4Width, 0.01);
            size.Height.Should().BeApproximately(A4Height, 0.01);
        });
    }

    [Fact]
    public void TheSharedDefaultsAreFullyFilledInBeforeAnyoneSeesThem()
    {
        var defaults = new Document().DefaultPageSetup;

        defaults.PageFormat.Should().Be(PageFormat.A4);
        defaults.Orientation.Should().Be(Orientation.Portrait);
        defaults.PageWidth.Centimeter.Should().BeApproximately(21, 0.001);
        defaults.PageHeight.Centimeter.Should().BeApproximately(29.7, 0.001);
        defaults.TopMargin.Centimeter.Should().BeApproximately(2.5, 0.001);
        defaults.BottomMargin.Centimeter.Should().BeApproximately(2, 0.001);
        defaults.LeftMargin.Centimeter.Should().BeApproximately(2.5, 0.001);
        defaults.RightMargin.Centimeter.Should().BeApproximately(2.5, 0.001);
    }
}
