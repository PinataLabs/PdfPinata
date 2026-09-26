using System;
using System.Collections.Generic;
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
///   The lifecycle every annotation that draws its own appearance shares - when the appearance is
///   built, what rebuilds it, what stamps <c>/M</c>, and what "draw nothing" leaves behind - pinned
///   across all of them at once, so that the classes cannot drift apart again.
/// </summary>
/// <remarks>
///   The drawing is counted in pixels as well as read in keys, because an annotation with the right
///   entries and no appearance rasterizes to nothing in most readers.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class AppearanceLifecycleTests : IDisposable
{
    private const string OutDir = "Out/AppearanceLifecycle";

    private static readonly DateTime LongAgo = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly List<MagickImageCollection> _rasterized = [];

    static AppearanceLifecycleTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    /// <summary>
    ///   Every class that draws itself. The text markup annotations share one implementation, so
    ///   <c>/Highlight</c> stands for all four.
    /// </summary>
    public static TheoryData<string> Drawing =>
        ["Square", "Circle", "Line", "FreeText", "Ink", "Polygon", "PolyLine", "Caret", "Redact", "Highlight"];

    /// <summary>
    ///   The ones that can be asked to draw nothing and take their appearance away when they are.
    /// </summary>
    public static TheoryData<string> Removing =>
        ["Square", "Circle", "Line", "FreeText", "Ink", "Polygon", "PolyLine", "Caret", "Redact"];

    // ----- when the appearance is built -----------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Drawing))]
    public void NothingIsBuiltUntilTheAnnotationReachesAPageAndThenItIs(string kind)
    {
        var annotation = Configured(kind);

        annotation.Elements.ContainsKey("/AP").Should().BeFalse("there is no document to make a form in yet");

        new PdfDocument().AddPage().Annotations.Add(annotation);

        annotation.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Theory]
    [MemberData(nameof(Drawing))]
    public void WhatItWasAskedForBeforeReachingAPageIsPainted(string kind)
    {
        var page = Rasterize(kind + "-painted", document => document.Pages[0].Annotations.Add(Configured(kind)));

        Ink(page).Should().BeGreaterThan(20);
    }

    // ----- what rebuilds it -----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Drawing))]
    public void AColourChangeRedrawsAndStampsTheModificationDate(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = NormalStream(annotation);
        annotation.Elements.SetDateTime("/M", LongAgo);

        annotation.Color = XColors.Blue;

        NormalStream(annotation).Should().NotEqual(before);
        annotation.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    [Theory]
    [MemberData(nameof(Drawing))]
    public void AnOpacityChangeRedraws(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = Snapshot(annotation);

        annotation.Opacity = 0.5;

        Snapshot(annotation).Should().NotBe(before);
    }

    [Theory]
    [MemberData(nameof(Drawing))]
    public void ItsOwnChangeRedrawsAndStampsTheModificationDate(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = Snapshot(annotation);
        annotation.Elements.SetDateTime("/M", LongAgo);

        OwnChange(annotation);

        Snapshot(annotation).Should().NotBe(before);
        annotation.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    // ----- asked for nothing ----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Removing))]
    public void AskedForNothingItTakesItsAppearanceAway(string kind)
    {
        PdfAnnotation annotation = null;
        var page = Rasterize(kind + "-nothing", document =>
        {
            annotation = Configured(kind);
            document.Pages[0].Annotations.Add(annotation);
            annotation.Elements.ContainsKey("/AP").Should().BeTrue();

            // A state left over from a set of appearances, which has to go with the appearance.
            annotation.Elements.SetName("/AS", "/Stale");

            AskForNothing(annotation);
        });

        annotation.Elements.ContainsKey("/AP").Should().BeFalse();
        annotation.Elements.ContainsKey("/AS").Should().BeFalse();
        annotation.Elements.ContainsKey("/RD").Should().BeFalse();
        annotation.Elements.ContainsKey("/Rect").Should().BeTrue("/Rect is required whether anything is drawn or not");
        Ink(page).Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(Removing))]
    public void AskedForSomethingAgainItDrawsAgain(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        AskForNothing(annotation);

        AskForSomethingAgain(annotation);

        annotation.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void AHighlightWithNothingLeftToMarkKeepsTheAppearanceItHad()
    {
        // The one class that does not take its appearance away: with no quadrilaterals and no
        // rectangle it returns without drawing, and the stream it drew last stays.
        var highlight = OnAPage((PdfHighlightAnnotation)Configured("Highlight"));
        highlight.ClearQuads();
        highlight.Rectangle = new PdfRectangle();

        highlight.Elements.ContainsKey("/AP").Should().BeTrue();
    }

    [Fact]
    public void AddingAQuadToAHighlightDoesNotStampTheModificationDate()
    {
        var highlight = OnAPage((PdfHighlightAnnotation)Configured("Highlight"));
        highlight.Elements.SetDateTime("/M", LongAgo);

        highlight.AddQuad(new PdfRectangle(new XPoint(100, 400), new XPoint(200, 420)));
        highlight.ClearQuads();

        highlight.Elements.GetDateTime("/M", DateTime.MinValue).Should().Be(LongAgo);
    }

    // ----- what /RD and /BS say -------------------------------------------------------------------------

    [Theory]
    [InlineData("Square", 2)]
    [InlineData("Circle", 2)]
    [InlineData("FreeText", 4)]
    public void TheRectangleDifferencesAreWrittenAlongsideTheAppearance(string kind, double inset)
    {
        var annotation = OnAPage(Configured(kind));

        var differences = annotation.Elements.GetArray("/RD");
        differences.Elements.Count.Should().Be(4);
        for (var index = 0; index < 4; index++)
            differences.Elements.GetReal(index).Should().Be(inset);
    }

    [Theory]
    [InlineData("Square")]
    [InlineData("Circle")]
    [InlineData("Line")]
    [InlineData("FreeText")]
    [InlineData("Ink")]
    [InlineData("Polygon")]
    [InlineData("PolyLine")]
    public void TheWidthIsWrittenAsASolidBorder(string kind)
    {
        var annotation = Configured(kind);

        var border = annotation.Elements.GetDictionary("/BS");
        border.Elements.GetName("/Type").Should().Be("/Border");
        border.Elements.GetName("/S").Should().Be("/S");
        border.Elements.GetReal("/W").Should().BeGreaterThan(0);
        border.IsIndirect.Should().BeFalse("it is written before the annotation has a document");
    }

    [Theory]
    [InlineData("Square", "A border cannot be narrower than nothing.", "value")]
    [InlineData("Circle", "A border cannot be narrower than nothing.", "value")]
    [InlineData("FreeText", "A border cannot be narrower than nothing.", "value")]
    [InlineData("Line", "A line cannot be narrower than nothing.", "value")]
    [InlineData("Ink", "A border cannot be narrower than nothing.", "width")]
    [InlineData("Polygon", "A border cannot be narrower than nothing.", "width")]
    [InlineData("PolyLine", "A border cannot be narrower than nothing.", "width")]
    public void ANegativeWidthIsRefused(string kind, string message, string parameter)
    {
        var annotation = Configured(kind);

        Action act = () => SetBorderWidth(annotation, -1);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage(message + "*")
            .Which.ParamName.Should().Be(parameter);
        BorderWidthOf(annotation).Should().BeGreaterThan(0, "nothing is written when the width is refused");
    }

    // ----- who owns /Rect -------------------------------------------------------------------------------

    [Theory]
    [InlineData("Line")]
    [InlineData("Ink")]
    [InlineData("Polygon")]
    [InlineData("PolyLine")]
    public void ARectangleAssignedToOneThatWorksItOutIsOverwrittenAtOnce(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var computed = annotation.Rectangle;

        annotation.Rectangle = new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10));

        annotation.Rectangle.Should().Be(computed);
    }

    [Theory]
    [InlineData("Square")]
    [InlineData("Circle")]
    [InlineData("FreeText")]
    [InlineData("Caret")]
    [InlineData("Redact")]
    public void ARectangleAssignedToOneItIsTheGeometryOfIsHonoured(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var assigned = new PdfRectangle(new XPoint(50, 50), new XPoint(150, 120));

        annotation.Rectangle = assigned;

        annotation.Rectangle.Should().Be(assigned);
        annotation.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    // ----- the cases --------------------------------------------------------------------------------------

    private static readonly PdfRectangle Box = new(new XPoint(100, 500), new XPoint(220, 600));

    /// <summary>
    ///   An annotation of the kind asked for, configured to draw something plainly visible and not
    ///   yet on a page.
    /// </summary>
    private static PdfAnnotation Configured(string kind)
    {
        switch (kind)
        {
            case "Square":
                return new PdfSquareAnnotation { Rectangle = Box, Color = XColors.Red, BorderWidth = 4 };
            case "Circle":
                return new PdfCircleAnnotation { Rectangle = Box, Color = XColors.Red, BorderWidth = 4 };
            case "Line":
            {
                var line = new PdfLineAnnotation { Color = XColors.Red, BorderWidth = 4 };
                line.SetLine(new XPoint(100, 500), new XPoint(300, 600));
                return line;
            }
            case "FreeText":
                return new PdfFreeTextAnnotation
                {
                    Rectangle = Box, Contents = "Lifecycle", BorderWidth = 2, TextColor = XColors.Red
                };
            case "Ink":
            {
                var ink = new PdfInkAnnotation { Color = XColors.Red, BorderWidth = 4 };
                ink.AddStroke(new XPoint(100, 500), new XPoint(200, 600), new XPoint(300, 500));
                return ink;
            }
            case "Polygon":
            {
                var polygon = new PdfPolygonAnnotation { Color = XColors.Red, BorderWidth = 4 };
                polygon.SetVertices(new XPoint(100, 500), new XPoint(300, 500), new XPoint(200, 600));
                return polygon;
            }
            case "PolyLine":
            {
                var polyline = new PdfPolyLineAnnotation { Color = XColors.Red, BorderWidth = 4 };
                polyline.SetVertices(new XPoint(100, 500), new XPoint(300, 500), new XPoint(300, 600));
                return polyline;
            }
            case "Caret":
                return new PdfCaretAnnotation { Rectangle = Box, Color = XColors.Red };
            case "Redact":
                return new PdfRedactAnnotation { Rectangle = Box, Color = XColors.Red };
            case "Highlight":
            {
                var highlight = new PdfHighlightAnnotation { Color = XColors.Red };
                highlight.AddQuad(Box);
                return highlight;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>
    ///   A change of the class's own - not one inherited from <see cref="PdfAnnotation"/> - to
    ///   something the appearance is drawn from.
    /// </summary>
    private static void OwnChange(PdfAnnotation annotation)
    {
        switch (annotation)
        {
            case PdfSquareCircleAnnotation shape:
                shape.Interior = XColors.Yellow;
                break;
            case PdfLineAnnotation line:
                line.End = new XPoint(320, 420);
                break;
            case PdfFreeTextAnnotation text:
                text.Contents = "Changed";
                break;
            case PdfInkAnnotation ink:
                ink.AddStroke(new XPoint(120, 520), new XPoint(140, 560));
                break;
            case PdfPolyAnnotation poly:
                poly.SetVertices(new XPoint(100, 500), new XPoint(320, 500), new XPoint(200, 640));
                break;
            case PdfCaretAnnotation caret:
                // A caret has nothing of its own that it is drawn from; its rectangle is the change.
                caret.Rectangle = new PdfRectangle(new XPoint(100, 500), new XPoint(160, 540));
                break;
            case PdfRedactAnnotation redact:
                redact.AddQuad(new PdfRectangle(new XPoint(120, 520), new XPoint(180, 560)));
                break;
            case PdfTextMarkupAnnotation markup:
                // Rectangle rather than AddQuad, which stamps nothing: pinned on its own below.
                markup.Rectangle = new PdfRectangle(new XPoint(100, 500), new XPoint(300, 640));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(annotation), annotation.GetType().Name, null);
        }
    }

    private static void AskForNothing(PdfAnnotation annotation)
    {
        switch (annotation)
        {
            case PdfSquareCircleAnnotation shape:
                shape.BorderWidth = 0;
                break;
            case PdfLineAnnotation line:
                line.BorderWidth = 0;
                break;
            case PdfFreeTextAnnotation text:
                text.Contents = "";
                text.BorderWidth = 0;
                break;
            case PdfInkAnnotation ink:
                // Strokes rather than width: Ghostscript builds an /Ink of its own when there is
                // no /AP, and strokes a width of 0 as the thinnest line it can.
                ink.ClearStrokes();
                break;
            case PdfPolyAnnotation poly:
                poly.BorderWidth = 0;
                break;
            case PdfCaretAnnotation or PdfRedactAnnotation:
                // Under XForm's floor of a point.
                annotation.Rectangle = new PdfRectangle(new XPoint(100, 500), new XPoint(100.5, 600));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(annotation), annotation.GetType().Name, null);
        }
    }

    private static void AskForSomethingAgain(PdfAnnotation annotation)
    {
        switch (annotation)
        {
            case PdfCaretAnnotation or PdfRedactAnnotation:
                annotation.Rectangle = Box;
                break;
            case PdfFreeTextAnnotation text:
                text.Contents = "Again";
                break;
            case PdfInkAnnotation ink:
                ink.AddStroke(new XPoint(100, 500), new XPoint(200, 600));
                break;
            default:
                SetBorderWidth(annotation, 3);
                break;
        }
    }

    private static void SetBorderWidth(PdfAnnotation annotation, double width)
    {
        switch (annotation)
        {
            case PdfSquareCircleAnnotation shape: shape.BorderWidth = width; break;
            case PdfLineAnnotation line: line.BorderWidth = width; break;
            case PdfFreeTextAnnotation text: text.BorderWidth = width; break;
            case PdfInkAnnotation ink: ink.BorderWidth = width; break;
            case PdfPolyAnnotation poly: poly.BorderWidth = width; break;
            default: throw new ArgumentOutOfRangeException(nameof(annotation), annotation.GetType().Name, null);
        }
    }

    private static double BorderWidthOf(PdfAnnotation annotation) =>
        annotation.Elements.GetDictionary("/BS").Elements.GetReal("/W");

    // ----- helpers ----------------------------------------------------------------------------------------

    private static T OnAPage<T>(T annotation) where T : PdfAnnotation
    {
        new PdfDocument().AddPage().Annotations.Add(annotation);
        return annotation;
    }

    private static byte[] NormalStream(PdfAnnotation annotation)
    {
        var form = (PdfDictionary)annotation.Elements.GetDictionary("/AP").Elements.GetObject("/N");

        // Copied, because the text markup annotations rewrite their stream in place.
        return [..form.Stream.Value];
    }

    /// <summary>
    ///   What the appearance draws, where it sits, and under what state - enough to tell any
    ///   rebuild worth the name from none.
    /// </summary>
    private static string Snapshot(PdfAnnotation annotation)
    {
        var form = (PdfDictionary)annotation.Elements.GetDictionary("/AP").Elements.GetObject("/N");

        // The text markup annotations carry their opacity in the form's graphics state rather
        // than drawing it, and rewrite the one form in place.
        var opacity = form.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/ExtGState")
            ?.Elements.GetDictionary("/GS0")?.Elements.GetReal("/ca");

        return string.Join("|", form.Reference?.ObjectNumber, Convert.ToBase64String(form.Stream.Value),
            form.Elements.GetArray("/BBox"), annotation.Elements.GetRectangle("/Rect"), opacity);
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

    /// <summary>
    ///   Pixels that are not the white of the page.
    /// </summary>
    private static int Ink(IMagickImage<byte> image)
    {
        using var pixels = image.GetPixels();
        return pixels.Count(pixel =>
        {
            var c = pixel.ToColor();
            return c != null && (c.R < 230 || c.G < 230 || c.B < 230);
        });
    }
}
