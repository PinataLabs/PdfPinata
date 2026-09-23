using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   <see cref="PdfLineAnnotation"/>: the dictionary it writes, the rectangle it works out for
///   itself, and whether a reader paints it.
/// </summary>
/// <remarks>
///   A <c>/Line</c> is drawn from its appearance stream and from nothing else, so the tests that
///   matter are the ones that count pixels. The rest pin the two things a caller cannot see from
///   the outside: that <c>/Rect</c> is derived from <c>/L</c> rather than set, and that it is made
///   wide enough for whatever sits at the ends.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class LineAnnotationTests : IDisposable
{
    private const string OutDir = "Out/LineAnnotations";

    private readonly List<MagickImageCollection> _rasterized = new List<MagickImageCollection>();

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static LineAnnotationTests()
    {
        GhostscriptSetup.Configure();
    }

    private static readonly XPoint From = new XPoint(100, 400);
    private static readonly XPoint To = new XPoint(300, 400);

    [Fact]
    public void ALineNamesItsSubtypeAndCarriesADefaultWidth()
    {
        var line = OnAPage();

        line.Elements.GetName("/Subtype").Should().Be("/Line");
        line.BorderWidth.Should().Be(1);
        line.Elements.GetDictionary("/BS").Elements.GetReal("/W").Should().Be(1);
    }

    [Fact]
    public void TheEndpointsAreWrittenToLInTheOrderTheyWereGiven()
    {
        var line = OnAPage();

        var l = line.Elements.GetArray("/L");
        l.Elements.Count.Should().Be(4);
        l.Elements.GetReal(0).Should().Be(100);
        l.Elements.GetReal(1).Should().Be(400);
        l.Elements.GetReal(2).Should().Be(300);
        l.Elements.GetReal(3).Should().Be(400);

        line.Start.Should().Be(From);
        line.End.Should().Be(To);
    }

    [Fact]
    public void TheRectangleIsWorkedOutFromTheLineRatherThanSet()
    {
        var line = OnAPage();

        // Half the width on each side, because a stroke straddles the path it follows. Without
        // it the outer half of a wide line falls outside the annotation and is clipped.
        line.BorderWidth = 8;

        var rect = line.Elements.GetRectangle("/Rect");
        rect.X1.Should().Be(96);
        rect.X2.Should().Be(304);
        rect.Y1.Should().Be(396);
        rect.Y2.Should().Be(404);
    }

    [Fact]
    public void ARectangleAssignedByHandIsOverwrittenByTheNextChange()
    {
        var line = OnAPage();

        line.Rectangle = new PdfRectangle(new XRect(0, 0, 10, 10));
        line.BorderWidth = 2;

        // The line is what the rectangle means, so the class has the last word on it. Documented
        // on the class, and asserted here so that it is a decision rather than a surprise.
        line.Elements.GetRectangle("/Rect").X1.Should().Be(99);
    }

    [Fact]
    public void AnEndingMakesRoomForItselfInTheRectangle()
    {
        var line = OnAPage();

        var withoutHead = line.Elements.GetRectangle("/Rect").Y2;

        line.EndEnding = PdfLineEnding.ClosedArrow;

        // An arrowhead is wider than the line it finishes, and /Rect has to enclose everything
        // drawn or a reader clips the head off.
        line.Elements.GetRectangle("/Rect").Y2.Should().BeGreaterThan(withoutHead);
    }

    [Fact]
    public void BothEndingsAreWrittenAsTwoNames()
    {
        var line = OnAPage();

        line.StartEnding = PdfLineEnding.Circle;
        line.EndEnding = PdfLineEnding.OpenArrow;

        var endings = line.Elements.GetArray("/LE");
        endings.Elements.Count.Should().Be(2);
        endings.Elements.GetName(0).Should().Be("/Circle");
        endings.Elements.GetName(1).Should().Be("/OpenArrow");

        line.StartEnding.Should().Be(PdfLineEnding.Circle);
        line.EndEnding.Should().Be(PdfLineEnding.OpenArrow);
    }

    [Fact]
    public void AnEndingNamingSomethingUnknownReadsBackAsNone()
    {
        var line = OnAPage();

        line.Elements["/LE"] = new PdfArray(line.Owner, new PdfName("/Trumpet"), new PdfName("/None"));

        // Rather than throwing on a document somebody else wrote, which is what Enum.Parse on
        // the raw name would do.
        line.StartEnding.Should().Be(PdfLineEnding.None);
    }

    [Fact]
    public void AnUnfilledEndingSaysSoWithAnEmptyArray()
    {
        var line = OnAPage();

        line.Interior.Should().Be(XColor.Empty);
        line.Elements.GetArray("/IC").Should().BeNull();

        line.Interior = XColor.Empty;
        line.Elements.GetArray("/IC").Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AFilledEndingWritesItsInteriorColour()
    {
        var line = OnAPage();

        line.Interior = XColors.RoyalBlue;

        var colour = line.Elements.GetArray("/IC");
        colour.Elements.Count.Should().Be(3);
        colour.Elements.GetReal(0).Should().BeApproximately(65 / 255.0, 0.01);
        colour.Elements.GetReal(2).Should().BeApproximately(225 / 255.0, 0.01);

        line.Interior.R.Should().Be(65);
    }

    [Fact]
    public void AnInteriorColourSurvivesTheFile()
    {
        var line = OnAPage();
        line.Interior = XColor.FromArgb(127, 127, 127);

        // What the file carries. A component goes out as a fraction of 255 to the seven decimal
        // places PdfWriter gives a real, so 127 becomes 0.4980392.
        var reopened = SaveAndReopen(line.Owner);
        var written = reopened.Pages[0].Annotations[0].Elements.GetArray("/IC");
        written.Elements.GetReal(0).Should().BeApproximately(0.4980392, 1e-9);

        // And what this class makes of it, reading the annotation back as itself. 0.4980392 times
        // 255 is 126.999996, so truncating loses a value the file all but said, and the grey a
        // caller asked for comes back a shade darker every time it is saved.
        var read = (PdfLineAnnotation)reopened.Pages[0].Annotations[0];

        read.Interior.R.Should().Be(127);
        read.Interior.G.Should().Be(127);
        read.Interior.B.Should().Be(127);
    }

    [Fact]
    public void ALineTooThinToMakeAFormOfIsLeftUndrawnRatherThanRefused()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var line = new PdfLineAnnotation
        {
            Start = new XPoint(100, 700),
            End = new XPoint(300, 700)
        };
        page.Annotations.Add(line);

        // A hairline lying flat is half its width either side of the line and no more, which is
        // under XForm's floor of a point. The guard used to be against zero, so this got past it
        // and threw out of a property setter.
        Action thinning = () => line.BorderWidth = 0.5;

        thinning.Should().NotThrow();
        line.Elements.ContainsKey("/AP").Should().BeFalse();

        // A point of height is enough, and then it does draw.
        line.BorderWidth = 1;

        line.Elements.ContainsKey("/AP").Should().BeTrue();
    }

    [Fact]
    public void MovingTheLineStampsTheModificationDate()
    {
        var line = OnAPage();

        // Rewound rather than read, so that the assertion does not turn on the clock ticking
        // between two statements.
        var before = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        line.Elements.SetDateTime("/M", before);
        line.End = new XPoint(320, 420);
        line.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(before);
    }

    [Fact]
    public void ANegativeWidthIsRefused()
    {
        var line = OnAPage();

        Action act = () => line.BorderWidth = -1;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheAppearanceIsBuiltWhenTheAnnotationReachesAPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var line = new PdfLineAnnotation();
        line.SetLine(From, To);

        // Everything above was set with no document to build a form in. Adding it to the page is
        // what gives it one, and the appearance has to appear then rather than be lost.
        line.Elements.ContainsKey("/AP").Should().BeFalse();

        page.Annotations.Add(line);

        line.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void ChangingWhatItIsDrawnFromRebuildsTheAppearance()
    {
        var line = OnAPage();

        var before = NormalStream(line);

        line.EndEnding = PdfLineEnding.ClosedArrow;

        NormalStream(line).Should().NotEqual(before);
    }

    [Fact]
    public void ALineOfNoWidthDrawsNothingAndKeepsNoAppearance()
    {
        var line = OnAPage();

        line.BorderWidth = 0;

        // The appearance already there has to go, or a line set back to nothing stays on the page.
        line.Elements.ContainsKey("/AP").Should().BeFalse();

        // /Rect is required whether anything is drawn or not.
        line.Elements.ContainsKey("/Rect").Should().BeTrue();
    }

    [Fact]
    public void ALineGoingNowhereDrawsNothing()
    {
        var line = OnAPage();

        line.SetLine(From, From);

        line.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    [Fact]
    public void ALineSurvivesBeingWrittenAndReadBack()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var line = new PdfLineAnnotation();
        page.Annotations.Add(line);
        line.SetLine(From, To);
        line.EndEnding = PdfLineEnding.ClosedArrow;

        var reopened = SaveAndReopen(document);

        var read = reopened.Pages[0].Annotations[0];
        read.Elements.GetName("/Subtype").Should().Be("/Line");
        read.Elements.GetArray("/L").Elements.GetReal(2).Should().Be(300);
        read.Elements.GetArray("/LE").Elements.GetName(1).Should().Be("/ClosedArrow");
        read.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [GoldenImageFact]
    public void ALineIsPainted()
    {
        var page = Rasterize("plain", line =>
        {
            line.Color = XColors.Firebrick;
            line.BorderWidth = 6;
        });

        Count(page, IsRed).Should().BeGreaterThan(200);
    }

    [GoldenImageFact]
    public void AnArrowheadPutsMoreInkOnThePageThanThePlainLineDoes()
    {
        var plain = Count(Rasterize("bare", line =>
        {
            line.Color = XColors.Firebrick;
            line.BorderWidth = 4;
        }), IsRed);

        var arrowed = Count(Rasterize("arrow", line =>
        {
            line.Color = XColors.Firebrick;
            line.BorderWidth = 4;
            line.Interior = XColors.Firebrick;
            line.EndEnding = PdfLineEnding.ClosedArrow;
        }), IsRed);

        // The one thing an arrowhead cannot fail to do. Counted rather than sampled, because
        // where the head lands depends on the line's direction and this does not.
        arrowed.Should().BeGreaterThan(plain);
    }

    [GoldenImageFact]
    public void ALineOfNoWidthRasterizesToNothing()
    {
        var page = Rasterize("empty", line => line.BorderWidth = 0);

        Count(page, IsAnythingButWhite).Should().Be(0);
    }

    private IMagickImage<byte> Rasterize(string name, Action<PdfLineAnnotation> arrange)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var line = new PdfLineAnnotation();
        page.Annotations.Add(line);
        line.SetLine(From, To);

        arrange(line);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    private static PdfLineAnnotation OnAPage()
    {
        var document = new PdfDocument();
        var line = new PdfLineAnnotation();
        document.AddPage().Annotations.Add(line);

        line.SetLine(From, To);
        return line;
    }

    private static byte[] NormalStream(PdfLineAnnotation line)
    {
        var form =
            (PdfDictionary)line.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        return form.Stream.Value;
    }

    private static PdfDocument SaveAndReopen(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        // Named in full: this assembly has a test class called PdfReader too, and it wins.
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static bool IsRed(IMagickColor<byte> c) => c.R > 130 && c.G < 100 && c.B < 100;

    private static bool IsAnythingButWhite(IMagickColor<byte> c) => c.R < 240 || c.G < 240 || c.B < 240;

    private static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            var c = p.ToColor();
            return c != null && match(c);
        });
    }
}
