using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   The six annotation subtypes that had no class - <c>/Ink</c>, <c>/Polygon</c>,
///   <c>/PolyLine</c>, <c>/Caret</c>, <c>/Redact</c> and <c>/Popup</c> - and the markup entries
///   every markup annotation shares.
/// </summary>
/// <remarks>
///   The five that draw themselves are tested by counting pixels, because an annotation carrying
///   the right entries and no appearance rasterizes to nothing in most readers, and asserting the
///   keys would pass on a page nobody can see.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class MarkupAnnotationTypesTests : IDisposable
{
    private const string OutDir = "Out/MarkupAnnotationTypes";

    private readonly List<MagickImageCollection> _rasterized = new();

    static MarkupAnnotationTypesTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    // ----- ink ------------------------------------------------------------------------------------------

    [Fact]
    public void AnInkStrokeIsPainted()
    {
        var page = Rasterize("ink", document =>
        {
            var ink = new PdfInkAnnotation { Color = XColors.Red, BorderWidth = 4 };
            document.Pages[0].Annotations.Add(ink);
            ink.AddStroke(new XPoint(100, 500), new XPoint(200, 600), new XPoint(300, 500));
        });

        Count(page, IsRed).Should().BeGreaterThan(300);
    }

    [Fact]
    public void AnInkAnnotationEnclosesItsStrokesAndTheWidthOfThePen()
    {
        var ink = OnAPage(new PdfInkAnnotation { BorderWidth = 4 });
        ink.AddStroke(new XPoint(100, 500), new XPoint(200, 600));
        ink.AddStroke(new XPoint(50, 550), new XPoint(60, 560));

        var rect = ink.Rectangle;
        rect.X1.Should().Be(48);
        rect.Y1.Should().Be(498);
        rect.X2.Should().Be(202);
        rect.Y2.Should().Be(602);
    }

    [Fact]
    public void InkStrokesSurviveTheFile()
    {
        var ink = OnAPage(new PdfInkAnnotation());
        ink.AddStroke(new XPoint(10, 20), new XPoint(30, 40), new XPoint(50, 60));

        var read = (PdfInkAnnotation)ReadBack(ink.Owner).Pages[0].Annotations[0];

        read.Strokes.Should().HaveCount(1);
        read.Strokes[0].Should().Equal(new XPoint(10, 20), new XPoint(30, 40), new XPoint(50, 60));
    }

    [Fact]
    public void InkWithNoStrokesOrNoWidthHasNoAppearance()
    {
        var ink = OnAPage(new PdfInkAnnotation());
        ink.AddStroke(new XPoint(10, 20), new XPoint(30, 40));
        ink.Elements.ContainsKey("/AP").Should().BeTrue();

        ink.BorderWidth = 0;
        ink.Elements.ContainsKey("/AP").Should().BeFalse();

        ink.BorderWidth = 1;
        ink.Elements.ContainsKey("/AP").Should().BeTrue();

        ink.ClearStrokes();
        ink.Elements.ContainsKey("/AP").Should().BeFalse();
        ink.Elements.GetArray("/InkList").Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AStrokeOfOnePointIsRefused()
    {
        var ink = new PdfInkAnnotation();

        Action act = () => ink.AddStroke(new XPoint(1, 1));

        act.Should().Throw<ArgumentException>();
    }

    // ----- polygon and polyline -------------------------------------------------------------------------

    [Fact]
    public void AFilledPolygonIsPainted()
    {
        var page = Rasterize("polygon", document =>
        {
            var polygon = new PdfPolygonAnnotation { Interior = XColors.Blue };
            document.Pages[0].Annotations.Add(polygon);
            polygon.SetVertices(new XPoint(100, 500), new XPoint(300, 500), new XPoint(200, 650));
        });

        Count(page, IsBlue).Should().BeGreaterThan(3000);
    }

    [Fact]
    public void APolyLineIsPaintedWithItsEnding()
    {
        var withEnding = Rasterize("polyline-arrow", document =>
        {
            var polyline = new PdfPolyLineAnnotation { Color = XColors.Red, Interior = XColors.Red, BorderWidth = 2 };
            document.Pages[0].Annotations.Add(polyline);
            polyline.SetVertices(new XPoint(100, 500), new XPoint(200, 600), new XPoint(300, 500));
            polyline.EndEnding = PdfLineEnding.ClosedArrow;
        });

        var without = Rasterize("polyline", document =>
        {
            var polyline = new PdfPolyLineAnnotation { Color = XColors.Red, BorderWidth = 2 };
            document.Pages[0].Annotations.Add(polyline);
            polyline.SetVertices(new XPoint(100, 500), new XPoint(200, 600), new XPoint(300, 500));
        });

        Count(without, IsRed).Should().BeGreaterThan(200);
        Count(withEnding, IsRed).Should().BeGreaterThan(Count(without, IsRed));
    }

    [Fact]
    public void AnEndingWidensThePolyLinesRectangle()
    {
        var polyline = OnAPage(new PdfPolyLineAnnotation { BorderWidth = 2 });
        polyline.SetVertices(new XPoint(100, 500), new XPoint(300, 500), new XPoint(300, 600));
        var plain = polyline.Rectangle;

        polyline.StartEnding = PdfLineEnding.Circle;

        polyline.Rectangle.X1.Should().BeLessThan(plain.X1);
        polyline.Elements.GetArray("/LE").Elements.GetName(0).Should().Be("/Circle");
        polyline.Elements.GetArray("/LE").Elements.GetName(1).Should().Be("/None");
    }

    [Fact]
    public void VerticesAndEndingsSurviveTheFile()
    {
        var polyline = OnAPage(new PdfPolyLineAnnotation { Interior = XColors.Green });
        polyline.SetVertices(new XPoint(1, 2), new XPoint(3, 4), new XPoint(50, 60));
        polyline.EndEnding = PdfLineEnding.OpenArrow;

        var read = (PdfPolyLineAnnotation)ReadBack(polyline.Owner).Pages[0].Annotations[0];

        read.Vertices.Should().Equal(new XPoint(1, 2), new XPoint(3, 4), new XPoint(50, 60));
        read.EndEnding.Should().Be(PdfLineEnding.OpenArrow);
        read.StartEnding.Should().Be(PdfLineEnding.None);
        read.Interior.G.Should().Be(XColors.Green.G);
    }

    [Fact]
    public void APolygonOfFewerThanTwoVerticesHasNoAppearance()
    {
        var polygon = OnAPage(new PdfPolygonAnnotation());
        polygon.SetVertices(new XPoint(1, 2), new XPoint(100, 200));
        polygon.Elements.ContainsKey("/AP").Should().BeTrue();

        polygon.SetVertices(new XPoint(1, 2));

        polygon.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    // ----- caret ----------------------------------------------------------------------------------------

    [Fact]
    public void ACaretIsPaintedInItsRectangle()
    {
        var page = Rasterize("caret", document =>
        {
            var caret = new PdfCaretAnnotation();
            document.Pages[0].Annotations.Add(caret);
            caret.Rectangle = new PdfRectangle(new XPoint(100, 500), new XPoint(160, 560));
        });

        Count(page, IsBlue).Should().BeGreaterThan(300);
    }

    [Fact]
    public void ACaretsSymbolIsWrittenOnlyWhenItIsAParagraph()
    {
        var caret = OnAPage(new PdfCaretAnnotation());
        caret.Elements.ContainsKey("/Sy").Should().BeFalse();

        caret.Symbol = PdfCaretSymbol.Paragraph;

        caret.Elements.GetName("/Sy").Should().Be("/P");
        var read = (PdfCaretAnnotation)ReadBack(caret.Owner).Pages[0].Annotations[0];
        read.Symbol.Should().Be(PdfCaretSymbol.Paragraph);
    }

    // ----- redaction ------------------------------------------------------------------------------------

    [Fact]
    public void ARedactionIsOutlinedAndLeavesWhatIsUnderneathVisible()
    {
        var page = Rasterize("redact", document =>
        {
            var redact = new PdfRedactAnnotation();
            document.Pages[0].Annotations.Add(redact);
            redact.AddQuad(new PdfRectangle(new XPoint(100, 500), new XPoint(300, 600)));
        });

        // An outline and not a fill: marking a region is a proposal, and applying it is not
        // something this library does.
        // The region is 200 by 100 points, so filled it would be tens of thousands of pixels more
        // than its outline is at any resolution the rasterizer uses.
        var red = Count(page, IsRed);
        var region = 200.0 * 100.0 * Math.Pow(page.Width / PageSizeConverter.ToSize(PageSize.A4).Width, 2);
        red.Should().BeGreaterThan(300);
        red.Should().BeLessThan((int)(region / 4));
    }

    [Fact]
    public void ARedactionsRegionsAndOverlaySurviveTheFile()
    {
        var redact = OnAPage(new PdfRedactAnnotation { Interior = XColors.Black });
        redact.AddQuad(new PdfRectangle(new XPoint(10, 10), new XPoint(90, 30)));
        redact.AddQuad(new PdfRectangle(new XPoint(10, 40), new XPoint(60, 60)));
        redact.OverlayText = "REDACTED";
        redact.RepeatOverlayText = true;

        redact.Rectangle.Y2.Should().Be(60);
        redact.Elements.GetString("/DA").Should().NotBeNullOrEmpty("overlay text requires one");

        var read = (PdfRedactAnnotation)ReadBack(redact.Owner).Pages[0].Annotations[0];

        read.Quads.Should().HaveCount(2);
        read.OverlayText.Should().Be("REDACTED");
        read.RepeatOverlayText.Should().BeTrue();
        read.Interior.Should().Be(XColor.FromArgb(0, 0, 0));
    }

    // ----- pop-ups and the markup entries ---------------------------------------------------------------

    [Fact]
    public void APopupIsLinkedToItsAnnotationBothWays()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var note = new PdfTextAnnotation { Rectangle = new PdfRectangle(new XPoint(10, 10), new XPoint(30, 30)) };
        var popup = new PdfPopupAnnotation
        {
            Rectangle = new PdfRectangle(new XPoint(40, 10), new XPoint(200, 100)),
            Open = true,
        };
        page.Annotations.Add(note);
        page.Annotations.Add(popup);

        note.Popup = popup;

        note.Elements.GetReference("/Popup").Should().BeSameAs(popup.Reference);
        popup.Elements.GetReference("/Parent").Should().BeSameAs(note.Reference);

        var annotations = ReadBack(document).Pages[0].Annotations;
        var readNote = (PdfTextAnnotation)annotations[0];
        var readPopup = annotations[1].Should().BeOfType<PdfPopupAnnotation>().Subject;

        readNote.Popup.Should().BeSameAs(readPopup);
        readPopup.ParentAnnotation.Should().BeSameAs(readNote);
        readPopup.Open.Should().BeTrue();
    }

    [Fact]
    public void APopupCannotBeLinkedBeforeItIsOnAPage()
    {
        var document = new PdfDocument();
        var note = new PdfTextAnnotation();
        document.AddPage().Annotations.Add(note);

        Action act = () => note.Popup = new PdfPopupAnnotation();

        act.Should().Throw<InvalidOperationException>().WithMessage("*not on a page*");
    }

    [Fact]
    public void ARepliesThreadRichTextAndIntentSurviveTheFile()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var first = new PdfTextAnnotation { Contents = "question" };
        var reply = new PdfTextAnnotation { Contents = "answer" };
        var cloud = new PdfPolygonAnnotation();
        page.Annotations.Add(first);
        page.Annotations.Add(reply);
        page.Annotations.Add(cloud);

        reply.InReplyTo = first;
        reply.ReplyType = PdfReplyType.Group;
        reply.RichText = "<body><p>answer</p></body>";
        cloud.Intent = "PolygonCloud";

        var annotations = ReadBack(document).Pages[0].Annotations;
        var readReply = (PdfMarkupAnnotation)annotations[1];

        readReply.InReplyTo.Should().BeSameAs(annotations[0]);
        readReply.ReplyType.Should().Be(PdfReplyType.Group);
        readReply.RichText.Should().Be("<body><p>answer</p></body>");
        ((PdfMarkupAnnotation)annotations[2]).Intent.Should().Be("/PolygonCloud");
        ((PdfMarkupAnnotation)annotations[0]).ReplyType.Should().Be(PdfReplyType.Reply);
    }

    public static TheoryData<PdfAnnotation, bool> MarkupOrNot => new()
    {
        { new PdfTextAnnotation(), true },
        { new PdfFreeTextAnnotation(), true },
        { new PdfLineAnnotation(), true },
        { new PdfSquareAnnotation(), true },
        { new PdfCircleAnnotation(), true },
        { new PdfHighlightAnnotation(), true },
        { new PdfRubberStampAnnotation(), true },
        { new PdfFileAttachmentAnnotation(), true },
        { new PdfInkAnnotation(), true },
        { new PdfPolygonAnnotation(), true },
        { new PdfPolyLineAnnotation(), true },
        { new PdfCaretAnnotation(), true },
        { new PdfRedactAnnotation(), true },
        { new PdfLinkAnnotation(), false },
        { new PdfWidgetAnnotation(), false },
        { new PdfPopupAnnotation(), false },
    };

    /// <summary>
    ///   ISO 32000-1 Table 170 lists which subtypes are markup annotations; a link, a widget and a
    ///   pop-up are not.
    /// </summary>
    [Theory]
    [MemberData(nameof(MarkupOrNot))]
    public void TheMarkupAnnotationsAreTheOnesTheSpecificationNames(PdfAnnotation annotation, bool isMarkup)
    {
        (annotation is PdfMarkupAnnotation).Should().Be(isMarkup);
    }

    // ----- helpers ------------------------------------------------------------------------------------

    private static T OnAPage<T>(T annotation) where T : PdfAnnotation
    {
        new PdfDocument().AddPage().Annotations.Add(annotation);
        return annotation;
    }

    private IMagickImage<byte> Rasterize(string name, Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        arrange(document);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    private static PdfDocument ReadBack(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return Pdf.IO.PdfReader.Open(new MemoryStream(output.ToArray()), Pdf.IO.PdfDocumentOpenMode.Modify);
    }

    private static bool IsRed(IMagickColor<byte> c) => c.R > 130 && c.G < 100 && c.B < 100;

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

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
