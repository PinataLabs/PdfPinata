using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using TUnit.Core;

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
[Rasterizing]
public sealed class AppearanceLifecycleTests : IDisposable
{
    private const string OutDir = "Out/AppearanceLifecycle";

    private static readonly DateTime LongAgo = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    /// <summary>
    ///   Every class that draws itself. The text markup annotations share one implementation, so
    ///   <c>/Highlight</c> stands for all four.
    /// </summary>
    public static IEnumerable<string> Drawing =>
        ["Square", "Circle", "Line", "FreeText", "Ink", "Polygon", "PolyLine", "Caret", "Redact", "Highlight"];

    /// <summary>
    ///   Asked to draw nothing, every one of them takes its appearance away.
    /// </summary>
    public static IEnumerable<string> Removing => Drawing;

    // ----- when the appearance is built -----------------------------------------------------------------

    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void NothingIsBuiltUntilTheAnnotationReachesAPageAndThenItIs(string kind)
    {
        var annotation = Configured(kind);

        annotation.Elements.ContainsKey("/AP").Should().BeFalse("there is no document to make a form in yet");

        new PdfDocument().AddPage().Annotations.Add(annotation);

        annotation.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void WhatItWasAskedForBeforeReachingAPageIsPainted(string kind)
    {
        var page = Rasterize(kind + "-painted", document => document.Pages[0].Annotations.Add(Configured(kind)));

        PageInk.Count(page, IsInk).Should().BeGreaterThan(20);
    }

    // ----- what rebuilds it -----------------------------------------------------------------------------

    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void AColourChangeRedrawsAndStampsTheModificationDate(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = NormalStream(annotation);
        annotation.Elements.SetDateTime("/M", LongAgo);

        annotation.Color = XColors.Blue;

        NormalStream(annotation).Should().NotEqual(before);
        annotation.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void AnOpacityChangeRedraws(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = Snapshot(annotation);

        annotation.Opacity = 0.5;

        Snapshot(annotation).Should().NotBe(before);
    }

    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ItsOwnChangeRedrawsAndStampsTheModificationDate(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var before = Snapshot(annotation);
        annotation.Elements.SetDateTime("/M", LongAgo);

        OwnChange(annotation);

        Snapshot(annotation).Should().NotBe(before);
        annotation.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    // ----- after a caller's own appearance --------------------------------------------------------------

    /// <summary>
    ///   A caller may hand any of them a drawing of its own, and it shows - until something the
    ///   annotation is drawn from changes. Then the annotation's redraw wins, as it would have
    ///   without the caller's drawing, rather than the change being made and never seen.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ACallersAppearanceGivesWayToTheNextRedraw(string kind)
    {
        PdfAnnotation annotation = null;
        var page = Rasterize(kind + "-own-then-redrawn", document =>
        {
            annotation = Configured(kind);
            document.Pages[0].Annotations.Add(annotation);
            annotation.SetAppearance(GreenBlock(annotation));

            annotation.Color = XColors.Blue;
        });

        PageInk.Count(page, IsGreen).Should().Be(0, "the caller's drawing has been replaced");
        PageInk.Count(page, PageInk.IsBlue).Should().BeGreaterThan(20, "the redraw is what shows");
    }

    /// <summary>
    ///   The same after a set of named appearances, which a redraw replaces by a single one - so
    ///   <c>/AS</c> goes with the set, as <see cref="PdfAnnotation.SetAppearance(XForm)"/> takes it.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ACallersSetOfAppearancesGivesWayToTheNextRedraw(string kind)
    {
        PdfAnnotation annotation = null;
        var page = Rasterize(kind + "-states-then-redrawn", document =>
        {
            annotation = Configured(kind);
            document.Pages[0].Annotations.Add(annotation);
            annotation.SetAppearance("/On", GreenBlock(annotation));

            annotation.Color = XColors.Blue;
        });

        annotation.Elements.ContainsKey("/AS").Should().BeFalse("no set of appearances is left for it to name");
        annotation.Elements.GetDictionary("/AP").Elements.GetDictionary("/N").Elements.ContainsKey("/BBox")
            .Should().BeTrue("/N is a single form again");
        PageInk.Count(page, IsGreen).Should().Be(0, "the caller's drawing has been replaced");
        PageInk.Count(page, PageInk.IsBlue).Should().BeGreaterThan(20, "the redraw is what shows");
    }

    /// <summary>
    ///   And the drawing given way to is not left in the file, nor any form the annotation drew
    ///   before it: one appearance, one form XObject.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void TheAppearanceGivenWayToIsNotWritten(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        annotation.SetAppearance(GreenBlock(annotation));
        annotation.Color = XColors.Blue;

        var reopened = annotation.Owner.Reopened();

        var forms = reopened.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Count(dict => dict.Elements.GetName("/Subtype") == "/Form");
        forms.Should().Be(1);
    }

    /// <summary>
    ///   A redraw replaces the normal appearance and nothing else: the library never draws a
    ///   rollover or a down appearance, so one there is the caller's, and survives. <c>/AS</c> stays
    ///   while the down appearance is a set of states it picks from.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ARedrawKeepsTheCallersRolloverAndDownAppearances(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var rollover = CallersForm(annotation.Owner);
        var pressed = CallersForm(annotation.Owner);
        var appearance = annotation.Elements.GetDictionary("/AP");
        var normal = appearance.Elements["/N"];
        appearance.Elements["/R"] = rollover.Reference;
        appearance.Elements["/D"] = new PdfDictionary(annotation.Owner) { Elements = { ["/On"] = pressed.Reference } };
        annotation.Elements.SetName("/AS", "/On");

        annotation.Color = XColors.Blue;

        var redrawn = annotation.Elements.GetDictionary("/AP");
        redrawn.Elements["/R"].Should().BeSameAs(rollover.Reference);
        redrawn.Elements.GetDictionary("/D").Elements["/On"].Should().BeSameAs(pressed.Reference);
        redrawn.Elements.GetDictionary("/N").Elements.ContainsKey("/BBox").Should().BeTrue();
        annotation.Elements.GetName("/AS").Should().Be("/On", "the down appearance is still a set it names one of");
        if (annotation is not PdfTextMarkupAnnotation)
            redrawn.Elements["/N"].Should().NotBeSameAs(normal, "a fresh form is drawn");
    }

    /// <summary>
    ///   With no set of states left anywhere in <c>/AP</c>, <c>/AS</c> names nothing and goes.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ARedrawWithOnlySingleAppearancesLeftTakesTheStateAway(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var rollover = CallersForm(annotation.Owner);
        annotation.Elements.GetDictionary("/AP").Elements["/R"] = rollover.Reference;
        annotation.Elements.SetName("/AS", "/Stale");

        annotation.Color = XColors.Blue;

        annotation.Elements.GetDictionary("/AP").Elements["/R"].Should().BeSameAs(rollover.Reference);
        annotation.Elements.ContainsKey("/AS").Should().BeFalse();
    }

    /// <summary>
    ///   While a kept set of states is left, <c>/AS</c> is required, and which state it names is the
    ///   caller's: the redraw leaves it as it is, even naming a state the set lacks - and is not put
    ///   off by one that is not a name at all, which a loosely written file may carry.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void ARedrawLeavesTheStateToTheCallerWhileASetOfStatesIsKept(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        annotation.SetAppearance("/On", GreenBlock(annotation));
        var pressed = CallersForm(annotation.Owner);
        annotation.Elements.GetDictionary("/AP").Elements["/D"] =
            new PdfDictionary(annotation.Owner) { Elements = { ["/Off"] = pressed.Reference } };

        annotation.Color = XColors.Blue;

        annotation.Elements.GetDictionary("/AP").Elements.GetDictionary("/D").Elements["/Off"]
            .Should().BeSameAs(pressed.Reference);
        annotation.Elements.GetName("/AS").Should().Be("/On");

        annotation.Elements["/AS"] = new PdfString("On");

        annotation.Opacity = 0.5;

        annotation.Elements["/AS"].Should().BeOfType<PdfString>();
    }

    /// <summary>
    ///   And what shows, with a rollover kept beside it, is the redraw.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(Drawing))]
    public void TheRedrawShowsBesideAKeptRollover(string kind)
    {
        PdfAnnotation annotation = null;
        var page = Rasterize(kind + "-rollover-kept", document =>
        {
            annotation = Configured(kind);
            document.Pages[0].Annotations.Add(annotation);
            annotation.SetAppearance(GreenBlock(annotation));
            annotation.Elements.GetDictionary("/AP").Elements["/R"] = CallersForm(document).Reference;

            annotation.Color = XColors.Blue;
        });

        annotation.Elements.GetDictionary("/AP").Elements.ContainsKey("/R").Should().BeTrue();
        PageInk.Count(page, IsGreen).Should().Be(0);
        PageInk.Count(page, PageInk.IsBlue).Should().BeGreaterThan(20);
    }

    /// <summary>
    ///   A form XObject of the caller's, made by hand: one drawing nothing in particular.
    /// </summary>
    private static PdfDictionary CallersForm(PdfDocument document)
    {
        var form = new PdfDictionary(document);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = new PdfArray(document, new PdfReal(0), new PdfReal(0), new PdfReal(10), new PdfReal(10));
        document.Internals.AddObject(form);
        form.CreateStream([]);
        return form;
    }

    /// <summary>
    ///   A drawing of the caller's: a green block over the whole of the annotation.
    /// </summary>
    private static XForm GreenBlock(PdfAnnotation annotation)
    {
        var rect = annotation.Rectangle;
        var form = new XForm(annotation.Owner, rect.Width, rect.Height);
        using (var gfx = XGraphics.FromForm(form))
            gfx.DrawRectangle(XBrushes.Lime, 0, 0, rect.Width, rect.Height);
        return form;
    }

    private static bool IsGreen(IMagickColor<byte> c) => c.G > 150 && c.R < 120 && c.B < 120;

    // ----- asked for nothing ----------------------------------------------------------------------------

    [Test]
    [MethodDataSource(nameof(Removing))]
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
        PageInk.Count(page, IsInk).Should().Be(0);
    }

    [Test]
    [MethodDataSource(nameof(Removing))]
    public void AskedForSomethingAgainItDrawsAgain(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        AskForNothing(annotation);

        AskForSomethingAgain(annotation);

        annotation.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Test]
    public void AddingAQuadToAHighlightStampsTheModificationDate()
    {
        var highlight = OnAPage((PdfHighlightAnnotation)Configured("Highlight"));
        highlight.Elements.SetDateTime("/M", LongAgo);

        highlight.AddQuad(new PdfRectangle(new XPoint(100, 400), new XPoint(200, 420)));

        highlight.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    [Test]
    public void ClearingAHighlightsQuadsStampsTheModificationDate()
    {
        var highlight = OnAPage((PdfHighlightAnnotation)Configured("Highlight"));
        highlight.Elements.SetDateTime("/M", LongAgo);

        highlight.ClearQuads();

        highlight.Elements.GetDateTime("/M", DateTime.MinValue).Should().BeAfter(LongAgo);
    }

    [Test]
    public void AHighlightReadFromAFileShowsWhatItIsRedrawnAs()
    {
        // Read back, it carries the appearance the file gave it and none of its own - which the
        // redraw has to replace rather than leave showing.
        var written = OnAPage((PdfHighlightAnnotation)Configured("Highlight"));
        var read = (PdfHighlightAnnotation)ReadBack(written.Owner).Pages[0].Annotations[0];
        var before = NormalStream(read);

        read.Color = XColors.Blue;

        var form = (PdfDictionary)read.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        form.Stream.Value.Should().NotEqual(before);
    }

    // ----- what /RD and /BS say -------------------------------------------------------------------------

    [Test]
    [Arguments("Square", 2)]
    [Arguments("Circle", 2)]
    [Arguments("FreeText", 4)]
    public void TheRectangleDifferencesAreWrittenAlongsideTheAppearance(string kind, double inset)
    {
        var annotation = OnAPage(Configured(kind));

        var differences = annotation.Elements.GetArray("/RD");
        differences.Elements.Count.Should().Be(4);
        for (var index = 0; index < 4; index++)
            differences.Elements.GetReal(index).Should().Be(inset);
    }

    [Test]
    [Arguments("Square")]
    [Arguments("Circle")]
    [Arguments("Line")]
    [Arguments("FreeText")]
    [Arguments("Ink")]
    [Arguments("Polygon")]
    [Arguments("PolyLine")]
    public void TheWidthIsWrittenAsASolidBorder(string kind)
    {
        var annotation = Configured(kind);

        var border = annotation.Elements.GetDictionary("/BS");
        border.Elements.GetName("/Type").Should().Be("/Border");
        border.Elements.GetName("/S").Should().Be("/S");
        border.Elements.GetReal("/W").Should().BeGreaterThan(0);
        border.IsIndirect.Should().BeFalse("it is written before the annotation has a document");
    }

    [Test]
    [Arguments("Square")]
    [Arguments("Circle")]
    [Arguments("FreeText")]
    [Arguments("Line")]
    [Arguments("Ink")]
    [Arguments("Polygon")]
    [Arguments("PolyLine")]
    public void ANegativeWidthIsRefusedTheSameWayByEveryOne(string kind)
    {
        var annotation = Configured(kind);

        Action act = () => SetBorderWidth(annotation, -1);

        // "value", because it is a property setter that refuses it.
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("A border cannot be narrower than nothing.*")
            .Which.ParamName.Should().Be("value");
        BorderWidthOf(annotation).Should().BeGreaterThan(0, "nothing is written when the width is refused");
    }

    // ----- who owns /Rect -------------------------------------------------------------------------------

    [Test]
    [Arguments("Line")]
    [Arguments("Ink")]
    [Arguments("Polygon")]
    [Arguments("PolyLine")]
    public void ARectangleAssignedToOneThatWorksItOutIsOverwrittenAtOnce(string kind)
    {
        var annotation = OnAPage(Configured(kind));
        var computed = annotation.Rectangle;

        annotation.Rectangle = new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10));

        annotation.Rectangle.Should().Be(computed);
    }

    [Test]
    [Arguments("Square")]
    [Arguments("Circle")]
    [Arguments("FreeText")]
    [Arguments("Caret")]
    [Arguments("Redact")]
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
                markup.AddQuad(new PdfRectangle(new XPoint(100, 400), new XPoint(300, 440)));
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
            case PdfTextMarkupAnnotation markup:
                // No quadrilaterals, and no rectangle to stand for one.
                markup.ClearQuads();
                markup.Rectangle = new PdfRectangle();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(annotation), annotation.GetType().Name, null);
        }
    }

    private static void AskForSomethingAgain(PdfAnnotation annotation)
    {
        switch (annotation)
        {
            case PdfCaretAnnotation or PdfRedactAnnotation or PdfTextMarkupAnnotation:
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

    private static PdfDocument ReadBack(PdfDocument document)
    {
        using var output = new System.IO.MemoryStream();
        document.Save(output, false);
        return Pdf.IO.PdfReader.Open(new System.IO.MemoryStream(output.ToArray()),
            Pdf.IO.PdfDocumentOpenMode.Modify);
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

        return _rasterized.FirstPageOf(document, name);
    }

    /// <summary>
    ///   A pixel that is not the white of the page. The threshold is this class's own, 230 where the
    ///   single-annotation tests use 240, so it stays here rather than in <see cref="PageInk"/>.
    /// </summary>
    private static bool IsInk(IMagickColor<byte> c) => c.R < 230 || c.G < 230 || c.B < 230;
}
