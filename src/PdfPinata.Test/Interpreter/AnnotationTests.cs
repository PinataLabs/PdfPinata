using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// The normal appearances of a page's annotations (ISO 32000-1 12.5.5), drawn after its content.
/// </summary>
/// <remarks>
/// A hand-made page is 200 points wide and 100 high, so a point of user space (x, y) lands on the
/// device at (x, 100 - y).
/// </remarks>
public class AnnotationTests
{
    private static PdfDictionary Annotation(PdfDocument document, PdfPage page, string subtype, PdfItem normal,
        double[] rect = null, int? flags = null, string state = null)
    {
        var annotation = new PdfDictionary(document);
        document.Internals.AddObject(annotation);
        annotation.Elements.SetName("/Type", "/Annot");
        annotation.Elements.SetName("/Subtype", subtype);
        rect ??= [50, 10, 150, 60];
        annotation.Elements["/Rect"] = new PdfRectangle(new XPoint(rect[0], rect[1]), new XPoint(rect[2], rect[3]));
        if (normal != null)
        {
            var appearance = new PdfDictionary(document);
            appearance.Elements["/N"] = normal;
            annotation.Elements["/AP"] = appearance;
        }

        if (flags != null)
            annotation.Elements.SetInteger("/F", flags.Value);
        if (state != null)
            annotation.Elements.SetName("/AS", state);

        var annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
            page.Elements["/Annots"] = annotations = new PdfArray(document);
        annotations.Elements.Add(annotation.Reference);
        return annotation;
    }

    private static PdfItem Reference(PdfDictionary form) => form.Reference;

    private static (RecordingDevice Device, PageRenderResult Result) Render(PdfPage page, bool annotations = true)
    {
        var device = new RecordingDevice();
        return (device, PageRendering.Render(page, device, annotations: annotations));
    }

    private static XPoint[] Corners(RecordingDevice.Call call) =>
        call.Path.Points.ToArray().Select(point =>
        {
            var mapped = call.Transform.Transform(point);
            return new XPoint(Math.Round(mapped.X, 6), Math.Round(mapped.Y, 6));
        }).ToArray();

    private static PdfPage PageWithSquare(string pageContent, string formContent, out PdfDocument document,
        Action<PdfDocument, PdfPage, PdfDictionary> annotate = null)
    {
        var page = Drawn.PageOf(pageContent);
        document = page.Owner;
        var form = Drawn.Form(document, formContent);
        form.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(100, 100));
        var annotation = Annotation(document, page, "/Square", Reference(form));
        annotate?.Invoke(document, page, annotation);
        return page;
    }

    [Test]
    public void AnnotationIsDrawnAfterThePageContentWithItsBoxMappedOntoItsRectangle()
    {
        var page = PageWithSquare("0 0 10 10 re f", "0 0 100 100 re f", out _);

        var (device, result) = Render(page);

        // The 100 by 100 box is put on a rectangle of 100 by 50 at (50, 10).
        device.Fills.Should().HaveCount(2);
        Corners(device.Fills.Last()).Should().BeEquivalentTo(
            [new XPoint(50, 90), new XPoint(150, 90), new XPoint(150, 40), new XPoint(50, 40)]);
        result.IsComplete.Should().BeTrue();
        device.Calls.Count(c => c.Kind == "save").Should().Be(device.Calls.Count(c => c.Kind == "restore"));
    }

    [Test]
    public void TheAppearanceStartsFromTheStateThePageStartedInWhateverTheContentLeftSet()
    {
        var page = PageWithSquare("2 0 0 2 0 0 cm 1 0 0 rg 0 0 5 5 re f", "0 0 100 100 re f", out _);

        var (device, _) = Render(page);

        Corners(device.Fills.Last()).Should().BeEquivalentTo(
            [new XPoint(50, 90), new XPoint(150, 90), new XPoint(150, 40), new XPoint(50, 40)]);
        device.Fills.Last().Paint.Should().Be(new PagePaint(0, 0, 0));
    }

    [Test]
    public void TheMatrixIsAppliedBeforeTheBoxIsMapped()
    {
        var page = Drawn.PageOf("");
        var form = Drawn.Form(page.Owner, "0 0 100 50 re f");
        form.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(100, 50));
        form.Elements.SetMatrix("/Matrix", new XMatrix(0, 1, -1, 0, 0, 0));
        Annotation(page.Owner, page, "/Stamp", Reference(form), rect: [20, 20, 70, 120]);

        var (device, _) = Render(page);

        // Turned a quarter, the box is 50 wide and 100 high, as the rectangle is.
        Corners(device.Fills.Single()).Should().BeEquivalentTo(
            [new XPoint(70, 80), new XPoint(70, -20), new XPoint(20, -20), new XPoint(20, 80)],
            options => options.WithoutStrictOrdering());
    }

    [Test]
    public void TheOptionLeavesAnnotationsOut()
    {
        var page = PageWithSquare("", "0 0 100 100 re f", out _);

        var (device, _) = Render(page, annotations: false);

        device.Fills.Should().BeEmpty();
    }

    [Test]
    public void AStateIsChosenByAs()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        var on = Drawn.Form(document, "1 0 0 rg 0 0 100 100 re f");
        var off = Drawn.Form(document, "0 0 1 rg 0 0 100 100 re f");
        var states = new PdfDictionary(document);
        states.Elements["/On"] = on.Reference;
        states.Elements["/Off"] = off.Reference;
        Annotation(document, page, "/Widget", states, state: "/Off");

        Render(page).Device.Fills.Single().Paint.Should().Be(new PagePaint(0, 0, 1));
    }

    [Test]
    public void AStateWithNoAsOrAnAsThatNamesNoStateDrawsNothing()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        var states = new PdfDictionary(document);
        states.Elements["/On"] = Drawn.Form(document, "0 0 100 100 re f").Reference;
        Annotation(document, page, "/Widget", states);
        Annotation(document, page, "/Widget", states, state: "/Missing");

        var (device, result) = Render(page);

        device.Fills.Should().BeEmpty();
        result.IsComplete.Should().BeTrue();
    }

    [Test]
    [Arguments(2, false)]
    [Arguments(32, false)]
    [Arguments(34, false)]
    [Arguments(1, true)]
    [Arguments(4, true)]
    [Arguments(0, true)]
    public void OnlyHiddenAndNoViewKeepAnAppearanceOffTheScreen(int flags, bool drawn)
    {
        var page = PageWithSquare("", "0 0 100 100 re f", out _, (_, _, annotation) =>
            annotation.Elements.SetInteger("/F", flags));

        Render(page).Device.Fills.Any().Should().Be(drawn);
    }

    [Test]
    public void APopupIsNotDrawn()
    {
        var page = Drawn.PageOf("");
        Annotation(page.Owner, page, "/Popup", Drawn.Form(page.Owner, "0 0 100 100 re f").Reference);

        Render(page).Device.Fills.Should().BeEmpty();
    }

    [Test]
    public void AnAnnotationWithNoAppearanceDrawsNothingAndIsNotAProblem()
    {
        var page = Drawn.PageOf("");
        Annotation(page.Owner, page, "/Text", null);
        Annotation(page.Owner, page, "/Link", null);

        var (device, result) = Render(page);

        device.Calls.Count(c => c.Kind is "fill" or "stroke").Should().Be(0);
        result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AnnotationsInTheOrderTheyAreListedDrawOneOverAnother()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        Annotation(document, page, "/Square", Drawn.Form(document, "1 0 0 rg 0 0 100 100 re f").Reference);
        Annotation(document, page, "/Square", Drawn.Form(document, "0 1 0 rg 0 0 100 100 re f").Reference);

        Render(page).Device.Fills.Select(f => f.Paint).Should().Equal(new PagePaint(1, 0, 0), new PagePaint(0, 1, 0));
    }

    [Test]
    public void AnAnnotationIsDrawnOnAPageWhoseContentCannotBeRead()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        Annotation(document, page, "/Square", Drawn.Form(document, "0 0 100 100 re f").Reference);
        page.Elements["/Contents"] = new PdfString("x");

        Render(page).Device.Fills.Should().ContainSingle();
    }

    // ----- malformed ----------------------------------------------------------------------------------------

    [Test]
    public void ABadAppearanceOrRectangleLeavesOutThatAnnotationAndDrawsTheRest()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        var good = Drawn.Form(document, "0 0 100 100 re f");

        Annotation(document, page, "/Square", new PdfString("five"));
        Annotation(document, page, "/Square", new PdfString("not a stream"));
        var noRect = Annotation(document, page, "/Square", good.Reference);
        noRect.Elements.Remove("/Rect");
        var textRect = Annotation(document, page, "/Square", good.Reference);
        textRect.Elements["/Rect"] = new PdfString("x");
        Annotation(document, page, "/Square", good.Reference, rect: [10, 10, 10, 50]);
        Annotation(document, page, "/Square", good.Reference, rect: [10, 10, double.PositiveInfinity, 50]);

        var noBox = Drawn.Form(document, "0 0 100 100 re f");
        noBox.Elements.Remove("/BBox");
        Annotation(document, page, "/Square", noBox.Reference);
        var flatBox = Drawn.Form(document, "0 0 100 100 re f");
        flatBox.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(0, 100));
        Annotation(document, page, "/Square", flatBox.Reference);
        var badMatrix = Drawn.Form(document, "0 0 100 100 re f");
        badMatrix.Elements["/Matrix"] = new PdfString("x");
        Annotation(document, page, "/Square", badMatrix.Reference);

        var annotations = page.Elements.GetArray("/Annots");
        annotations.Elements.Add(new PdfString("three"));
        annotations.Elements.Add(PdfNull.Value);
        annotations.Elements.Add(new PdfDictionary(document));
        Annotation(document, page, "/Square", good.Reference);

        var (device, result) = Render(page);

        device.Fills.Should().ContainSingle("only the last annotation is well formed");
        result.Diagnostics.Select(d => d.Problem).Should().OnlyContain(p => p == PageRenderProblem.InvalidValue);
        device.Calls.Count(c => c.Kind == "save").Should().Be(device.Calls.Count(c => c.Kind == "restore"));
    }

    [Test, Timeout(30_000)]
    public void AnAppearanceThatDrawsItselfIsDrawnOnceAndReported()
    {
        var page = Drawn.PageOf("");
        var document = page.Owner;
        var form = Drawn.Form(document, "0 0 5 5 re f /Fm Do");
        var resources = new PdfDictionary(document);
        form.Elements["/Resources"] = resources;
        Drawn.Add(document, resources, "/XObject", "/Fm", form);
        Annotation(document, page, "/Square", form.Reference);

        var (device, result) = Render(page);

        device.Fills.Should().ContainSingle();
        result.Diagnostics.Should().ContainSingle(d => d.Problem == PageRenderProblem.LimitReached);
    }

    // ----- what this library writes -------------------------------------------------------------------------

    [Test]
    [Arguments("Square")]
    [Arguments("Circle")]
    [Arguments("Line")]
    [Arguments("FreeText")]
    public void AnAnnotationThisLibraryWroteIsDrawnWithinItsRectangleAfterARoundTrip(string kind)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var box = new PdfRectangle(new XPoint(100, 500), new XPoint(220, 600));
        PdfAnnotation annotation = kind switch
        {
            "Square" => new PdfSquareAnnotation { Rectangle = box, Color = XColors.Red, BorderWidth = 4 },
            "Circle" => new PdfCircleAnnotation { Rectangle = box, Color = XColors.Red, BorderWidth = 4 },
            "Line" => LineAnnotation(),
            _ => new PdfFreeTextAnnotation { Rectangle = box, Contents = "Hi", BorderWidth = 2, TextColor = XColors.Red }
        };
        page.Annotations.Add(annotation);

        var reopened = document.Reopened().Pages[0];
        var (device, _) = Render(reopened);

        var drawn = device.Calls.Where(c => c.Kind is "fill" or "stroke").ToArray();
        drawn.Should().NotBeEmpty("the annotation is drawn from its appearance and from nothing else");
        var height = reopened.Height.Point;
        foreach (var point in drawn.SelectMany(Corners))
        {
            point.X.Should().BeInRange(100 - 8, 300 + 8);
            point.Y.Should().BeInRange(height - 600 - 8, height - 500 + 8);
        }

        Render(reopened, annotations: false).Device.Calls.Count(c => c.Kind is "fill" or "stroke").Should().Be(0);

        static PdfLineAnnotation LineAnnotation()
        {
            var line = new PdfLineAnnotation { Color = XColors.Red, BorderWidth = 4 };
            line.SetLine(new XPoint(100, 500), new XPoint(300, 600));
            return line;
        }
    }

    [Test]
    public void AFormFieldThisLibraryWroteIsDrawnAfterARoundTrip()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(field);
        field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        field.BackColor = XColors.Yellow;
        field.BorderColor = XColors.Blue;

        var reopened = document.Reopened().Pages[0];
        var (device, _) = Render(reopened);

        var yellow = device.Fills.Where(f => f.Paint.Equals(new PagePaint(1, 1, 0))).ToArray();
        yellow.Should().NotBeEmpty();
        var corners = Corners(yellow[0]);
        corners.Min(p => p.X).Should().BeApproximately(60, 0.01);
        corners.Max(p => p.X).Should().BeApproximately(260, 0.01);
    }
}
