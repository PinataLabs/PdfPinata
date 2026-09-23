using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A page has five boxes (ISO 32000-1 14.11.2). Only the media box is required; the crop box
///   defaults to it, and the bleed, trim and art boxes default to the crop box.
///
///   <para>
///   Reading one of the four optional boxes used to write it: the getters asked for the entry
///   with <c>create: true</c>, so a page that had no trim box was given <c>/TrimBox [0 0 0 0]</c>
///   by being asked about it, and a crop box written that way crops the page to nothing. Reading
///   a box now leaves the page as it was, answers the empty rectangle for a box the page does not
///   state, and <c>Has…Box</c> and <c>Effective…Box</c> say whether it does and what the reader
///   will use.
///   </para>
/// </summary>
public class PageBoxTests
{
    public static TheoryData<string> OptionalBoxes => ["/CropBox", "/BleedBox", "/TrimBox", "/ArtBox"];

    [Theory]
    [MemberData(nameof(OptionalBoxes))]
    public void ReadingABoxThePageDoesNotStateDoesNotGiveThePageOne(string key)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var box = Read(page, key);

        box.IsEmpty.Should().BeTrue();
        page.Elements.ContainsKey(key).Should().BeFalse();

        var reread = SaveAndReopen(document);
        reread.Pages[0].Elements.ContainsKey(key).Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(OptionalBoxes))]
    public void ReadingABoxOnAPageReadFromAFileDoesNotGiveThePageOne(string key)
    {
        var document = OpenOnePage("/MediaBox[0 0 300 400]");

        Read(document.Pages[0], key).IsEmpty.Should().BeTrue();

        document.Pages[0].Elements.ContainsKey(key).Should().BeFalse();
        SaveAndReopen(document).Pages[0].Elements.ContainsKey(key).Should().BeFalse();
    }

    [Fact]
    public void ReadingTheMediaBoxOfAPageThatStatesNoneDoesNotGiveItAnEmptyOne()
    {
        // The media box is required, but a file can still leave it out, and a reader then falls
        // back on a size of its own choosing. A [0 0 0 0] written in its place is a page of no
        // size at all, which is worse than the omission it replaced.
        var document = OpenOnePage("");

        document.Pages[0].MediaBox.IsEmpty.Should().BeTrue();

        document.Pages[0].Elements.ContainsKey("/MediaBox").Should().BeFalse();
    }

    [Fact]
    public void ANewPageHasAMediaBoxAndNoneOfTheOthers()
    {
        var page = new PdfDocument().AddPage();

        page.HasMediaBox.Should().BeTrue();
        page.HasCropBox.Should().BeFalse();
        page.HasBleedBox.Should().BeFalse();
        page.HasTrimBox.Should().BeFalse();
        page.HasArtBox.Should().BeFalse();
    }

    [Fact]
    public void EachHasSaysWhetherThatBoxIsStated()
    {
        var page = new PdfDocument().AddPage();
        var box = Box(10, 10, 200, 300);

        page.CropBox = box;
        page.BleedBox = box;
        page.TrimBox = box;
        page.ArtBox = box;

        page.HasCropBox.Should().BeTrue();
        page.HasBleedBox.Should().BeTrue();
        page.HasTrimBox.Should().BeTrue();
        page.HasArtBox.Should().BeTrue();
    }

    [Fact]
    public void ABoxInheritedFromThePageTreeCountsAsStated()
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 300 400]/CropBox[5 5 295 395]>>",
            "<</Type/Page/Parent 2 0 R>>"
        };
        var page = Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build(objects)), PdfDocumentOpenMode.Modify).Pages[0];

        page.HasMediaBox.Should().BeTrue();
        page.HasCropBox.Should().BeTrue();
        page.EffectiveTrimBox.Should().Be(Box(5, 5, 295, 395));
    }

    [Theory]
    [InlineData("/CropBox null")]
    [InlineData("/CropBox 42")]
    [InlineData("/CropBox[1 2 3]")]
    public void AnEntryThatIsNotARectangleIsNoBox(string entry)
    {
        var page = OpenOnePage("/MediaBox[0 0 300 400]" + entry).Pages[0];

        page.HasCropBox.Should().BeFalse();
        page.CropBox.IsEmpty.Should().BeTrue();
        page.EffectiveCropBox.Should().Be(Box(0, 0, 300, 400));
    }

    [Fact]
    public void WithNoBoxesStatedEveryEffectiveBoxIsTheMediaBox()
    {
        var page = OpenOnePage("/MediaBox[0 0 300 400]").Pages[0];

        page.EffectiveCropBox.Should().Be(Box(0, 0, 300, 400));
        page.EffectiveBleedBox.Should().Be(Box(0, 0, 300, 400));
        page.EffectiveTrimBox.Should().Be(Box(0, 0, 300, 400));
        page.EffectiveArtBox.Should().Be(Box(0, 0, 300, 400));
    }

    [Fact]
    public void TheBleedTrimAndArtBoxesDefaultToTheCropBox()
    {
        var page = OpenOnePage("/MediaBox[0 0 300 400]/CropBox[10 20 290 380]").Pages[0];

        page.EffectiveBleedBox.Should().Be(Box(10, 20, 290, 380));
        page.EffectiveTrimBox.Should().Be(Box(10, 20, 290, 380));
        page.EffectiveArtBox.Should().Be(Box(10, 20, 290, 380));
    }

    [Fact]
    public void AStatedBoxIsItsOwnEffectiveBoxWhateverTheCropBoxSays()
    {
        // The standard reduces a box to the media box, not to the crop box.
        var page = OpenOnePage(
            "/MediaBox[0 0 300 400]/CropBox[50 50 250 350]/BleedBox[5 5 295 395]/TrimBox[20 20 280 380]/ArtBox[30 30 270 370]").Pages[0];

        page.EffectiveBleedBox.Should().Be(Box(5, 5, 295, 395));
        page.EffectiveTrimBox.Should().Be(Box(20, 20, 280, 380));
        page.EffectiveArtBox.Should().Be(Box(30, 30, 270, 370));
    }

    [Fact]
    public void ABoxReachingOutsideTheMediaBoxIsReducedToThePartInside()
    {
        var page = OpenOnePage("/MediaBox[0 0 300 400]/CropBox[-10 100 500 900]/TrimBox[250 -50 350 50]").Pages[0];

        page.EffectiveCropBox.Should().Be(Box(0, 100, 300, 400));
        page.EffectiveTrimBox.Should().Be(Box(250, 0, 300, 50));

        // What the page states is untouched: only the effective box is reduced.
        page.CropBox.Should().Be(Box(-10, 100, 500, 900));
    }

    [Fact]
    public void ABoxWhollyOutsideTheMediaBoxHasNoEffectiveArea()
    {
        var page = OpenOnePage("/MediaBox[0 0 300 400]/ArtBox[400 500 600 700]").Pages[0];

        page.EffectiveArtBox.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AnEffectiveBoxIsWrittenLowerLeftCornerFirst()
    {
        var page = OpenOnePage("/MediaBox[300 400 0 0]/CropBox[290 380 10 20]").Pages[0];

        page.EffectiveCropBox.Should().Be(Box(10, 20, 290, 380));
        page.EffectiveTrimBox.Should().Be(Box(10, 20, 290, 380));
    }

    [Fact]
    public void AnEffectiveBoxOfALandscapePageIsMeasuredAgainstTheMediaBoxAsWritten()
    {
        // The media box of a page built as landscape is held upright and turned over as it is
        // written; the other boxes are written as they are. So the effective boxes are in the
        // coordinates of the file, which is where a reader applies them.
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A4;
        page.Orientation = PageOrientation.Landscape;
        var upright = page.MediaBox;

        var turned = Box(0, 0, upright.Height, upright.Width);
        page.EffectiveCropBox.Should().Be(turned);

        page.TrimBox = Box(10, 10, upright.Height - 10, upright.Width - 10);
        page.EffectiveTrimBox.Should().Be(Box(10, 10, upright.Height - 10, upright.Width - 10));

        var reread = SaveAndReopen(document).Pages[0];
        reread.MediaBox.Should().Be(turned);
        reread.EffectiveTrimBox.Should().Be(page.EffectiveTrimBox);
    }

    [Fact]
    public void ReadingTheEffectiveBoxesDoesNotChangeThePage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var keys = page.Elements.Keys.Count;

        _ = page.EffectiveCropBox;
        _ = page.EffectiveBleedBox;
        _ = page.EffectiveTrimBox;
        _ = page.EffectiveArtBox;
        _ = page.HasCropBox;

        page.Elements.Keys.Count.Should().Be(keys);
        foreach (var key in OptionalBoxes)
            page.Elements.ContainsKey(key).Should().BeFalse();
    }

    [Fact]
    public void APageWithNoMediaBoxIsNotClipped()
    {
        var page = OpenOnePage("/CropBox[10 20 290 380]").Pages[0];

        page.HasMediaBox.Should().BeFalse();
        page.EffectiveCropBox.Should().Be(Box(10, 20, 290, 380));
        OpenOnePage("").Pages[0].EffectiveTrimBox.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ALandscapePageWithNoMediaBoxIsNotGivenAnEmptyOneWhenSaved()
    {
        // Turning the empty rectangle over as the page is written used to be the other way a
        // media box of no size reached the file.
        var document = OpenOnePage("/Rotate 90");
        document.Pages[0].Orientation.Should().Be(PageOrientation.Landscape);
        document.Pages[0].Rotate = 0;

        SaveAndReopen(document).Pages[0].HasMediaBox.Should().BeFalse();
    }

    private static PdfRectangle Box(double x1, double y1, double x2, double y2) =>
        new(new XPoint(x1, y1), new XPoint(x2, y2));

    private static PdfRectangle Read(PdfPage page, string key) => key switch
    {
        "/CropBox" => page.CropBox,
        "/BleedBox" => page.BleedBox,
        "/TrimBox" => page.TrimBox,
        "/ArtBox" => page.ArtBox,
        "/MediaBox" => page.MediaBox,
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static PdfDocument OpenOnePage(string pageEntries)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R" + pageEntries + ">>"
        };
        return Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build(objects)), PdfDocumentOpenMode.Modify);
    }

    private static PdfDocument SaveAndReopen(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return Pdf.IO.PdfReader.Open(new MemoryStream(output.ToArray()), PdfDocumentOpenMode.Modify);
    }
}
