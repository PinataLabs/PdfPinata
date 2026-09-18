using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   An annotation sits on the page in coordinates of its own, so a resize that moved only the
///   drawing would leave every link, highlight and note pointing at empty paper.
///   <para>
///   Every test here resizes an A4 page to exactly half its size in each direction - stretched, so
///   that the transform is a plain halving with no offset and every expected number can be read
///   off by eye.
///   </para>
/// </summary>
public class PageResizeAnnotationTests
{
    const double A4Width = 595;
    const double A4Height = 842;
    const double Tolerance = 0.01;

    static PdfDocument DocumentWithAnAnnotation(PdfDictionary annotation)
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));

        document.Internals.AddObject(annotation);
        PdfArray annotations = new PdfArray(document);
        annotations.Elements.Add(annotation.Reference);
        page.Elements["/Annots"] = annotations;

        return document;
    }

    /// <summary>Halves the page in each direction, exactly.</summary>
    static void HalveThePage(PdfPage page)
    {
        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(new XSize(A4Width / 2, A4Height / 2), options);
    }

    static PdfDictionary AnnotationOfSubtype(string subtype)
    {
        PdfDictionary annotation = new PdfDictionary();
        annotation.Elements.SetName("/Type", "/Annot");
        annotation.Elements.SetName("/Subtype", subtype);
        annotation.Elements.SetRectangle("/Rect",
            new PdfRectangle(new XPoint(100, 200), new XPoint(300, 400)));
        return annotation;
    }

    static PdfArray NumbersOf(PdfDictionary annotation, string key)
    {
        return annotation.Elements.GetArray(key);
    }

    static double[] Values(PdfArray array)
    {
        return Enumerable.Range(0, array.Elements.Count).Select(array.Elements.GetReal).ToArray();
    }

    static PdfDictionary TheAnnotationOf(PdfPage page)
    {
        PdfArray annotations = page.Elements.GetArray("/Annots");
        return annotations.Elements.GetDictionary(0);
    }

    [Fact]
    public void ALinkRectangleIsHalvedWithThePage()
    {
        PdfDocument document = DocumentWithAnAnnotation(AnnotationOfSubtype("/Link"));
        HalveThePage(document.Pages[0]);

        PdfRectangle rect = TheAnnotationOf(document.Pages[0]).Elements.GetRectangle("/Rect");

        rect.X1.Should().BeApproximately(50, Tolerance);
        rect.Y1.Should().BeApproximately(100, Tolerance);
        rect.X2.Should().BeApproximately(150, Tolerance);
        rect.Y2.Should().BeApproximately(200, Tolerance);
    }

    [Fact]
    public void AHighlightKeepsCoveringItsText()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Highlight");
        PdfArray quads = new PdfArray();
        foreach (double value in new double[] { 100, 400, 300, 400, 100, 200, 300, 200 })
            quads.Elements.Add(new PdfReal(value));
        annotation.Elements["/QuadPoints"] = quads;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        Values(NumbersOf(TheAnnotationOf(document.Pages[0]), "/QuadPoints"))
            .Should().Equal(50, 200, 150, 200, 50, 100, 150, 100);
    }

    [Fact]
    public void EveryStrokeOfAnInkAnnotationMoves()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Ink");
        PdfArray inkList = new PdfArray();
        foreach (double[] stroke in new[] { new double[] { 10, 20, 30, 40 }, new double[] { 50, 60 } })
        {
            PdfArray points = new PdfArray();
            foreach (double value in stroke)
                points.Elements.Add(new PdfReal(value));
            inkList.Elements.Add(points);
        }
        annotation.Elements["/InkList"] = inkList;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        PdfArray moved = NumbersOf(TheAnnotationOf(document.Pages[0]), "/InkList");
        Values((PdfArray)moved.Elements[0]).Should().Equal(5, 10, 15, 20);
        Values((PdfArray)moved.Elements[1]).Should().Equal(25, 30);
    }

    [Fact]
    public void ThePointsOfAPolygonMove()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Polygon");
        PdfArray vertices = new PdfArray();
        foreach (double value in new double[] { 10, 20, 30, 40, 50, 60 })
            vertices.Elements.Add(new PdfReal(value));
        annotation.Elements["/Vertices"] = vertices;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        Values(NumbersOf(TheAnnotationOf(document.Pages[0]), "/Vertices"))
            .Should().Equal(5, 10, 15, 20, 25, 30);
    }

    [Fact]
    public void TheEndsOfALineMove()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Line");
        PdfArray line = new PdfArray();
        foreach (double value in new double[] { 100, 200, 300, 400 })
            line.Elements.Add(new PdfReal(value));
        annotation.Elements["/L"] = line;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        Values(NumbersOf(TheAnnotationOf(document.Pages[0]), "/L")).Should().Equal(50, 100, 150, 200);
    }

    [Fact]
    public void TheInsetsOfASquareAreScaled()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Square");
        PdfArray differences = new PdfArray();
        foreach (double value in new double[] { 10, 20, 30, 40 })
            differences.Elements.Add(new PdfReal(value));
        annotation.Elements["/RD"] = differences;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        // Distances rather than points, so they scale and are not offset.
        Values(NumbersOf(TheAnnotationOf(document.Pages[0]), "/RD")).Should().Equal(5, 10, 15, 20);
    }

    [Fact]
    public void AnAppearanceStreamIsLeftExactlyAsItWas()
    {
        // The reader maps an appearance onto the /Rect through the appearance's own bounding box
        // and matrix, so moving the rectangle moves what is drawn in it. Moving both would apply
        // the resize twice over.
        PdfDictionary annotation = AnnotationOfSubtype("/Square");

        PdfDictionary appearance = new PdfDictionary();
        appearance.Elements.SetName("/Type", "/XObject");
        appearance.Elements.SetName("/Subtype", "/Form");
        appearance.Elements.SetRectangle("/BBox", new PdfRectangle(new XPoint(0, 0), new XPoint(200, 200)));
        appearance.Elements.SetMatrix("/Matrix", XMatrix.Identity);

        PdfDictionary normal = new PdfDictionary();
        normal.Elements["/N"] = appearance;
        annotation.Elements["/AP"] = normal;

        PdfDocument document = DocumentWithAnAnnotation(annotation);

        // Read as written rather than through GetMatrix, which cannot parse back what SetMatrix
        // writes - it stores a literal and throws "Parsing matrix from literal" on the way in.
        // Comparing the written form is the stronger check anyway: it says nothing at all
        // changed, not merely that it still means the same thing.
        string matrixBefore = appearance.Elements["/Matrix"].ToString();

        HalveThePage(document.Pages[0]);

        PdfDictionary movedAppearance = TheAnnotationOf(document.Pages[0])
            .Elements.GetDictionary("/AP").Elements.GetDictionary("/N");

        PdfRectangle bbox = movedAppearance.Elements.GetRectangle("/BBox");
        bbox.X2.Should().BeApproximately(200, Tolerance, "the appearance box is not touched");
        bbox.Y2.Should().BeApproximately(200, Tolerance);
        movedAppearance.Elements["/Matrix"].ToString().Should().Be(matrixBefore);
    }

    [Fact]
    public void AnAnnotationOfAnUnknownSubtypeKeepsEverythingButItsRectangle()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/SomethingNobodyModels");
        annotation.Elements.SetString("/Contents", "a note");

        PdfArray mystery = new PdfArray();
        foreach (double value in new double[] { 1, 2, 3, 4 })
            mystery.Elements.Add(new PdfReal(value));
        annotation.Elements["/SomeGeometry"] = mystery;

        PdfDocument document = DocumentWithAnAnnotation(annotation);
        HalveThePage(document.Pages[0]);

        PdfDictionary moved = TheAnnotationOf(document.Pages[0]);

        moved.Elements.GetRectangle("/Rect").X1.Should().BeApproximately(50, Tolerance);
        moved.Elements.GetString("/Contents").Should().Be("a note");
        Values(NumbersOf(moved, "/SomeGeometry")).Should().Equal(new double[] { 1, 2, 3, 4 },
            "an entry nobody models is left alone rather than guessed at");
    }

    [Fact]
    public void EveryAnnotationOfAPageWithSeveralIsMoved()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;
        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));

        PdfArray annotations = new PdfArray(document);
        for (int index = 0; index < 3; index++)
        {
            PdfDictionary annotation = AnnotationOfSubtype("/Link");
            document.Internals.AddObject(annotation);
            annotations.Elements.Add(annotation.Reference);
        }
        page.Elements["/Annots"] = annotations;

        HalveThePage(page);

        for (int index = 0; index < 3; index++)
        {
            annotations.Elements.GetDictionary(index).Elements.GetRectangle("/Rect")
                .X1.Should().BeApproximately(50, Tolerance);
        }
    }

    [Fact]
    public void TurningOffTheAnnotationPassLeavesThemWhereTheyWere()
    {
        PdfDocument document = DocumentWithAnAnnotation(AnnotationOfSubtype("/Link"));

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        options.ScaleAnnotations = false;
        document.Pages[0].Resize(new XSize(A4Width / 2, A4Height / 2), options);

        TheAnnotationOf(document.Pages[0]).Elements.GetRectangle("/Rect")
            .X1.Should().BeApproximately(100, Tolerance);
    }

    [Fact]
    public void AnnotationsFollowThePageWhenItIsTurnedByAutoRotate()
    {
        PdfDictionary annotation = AnnotationOfSubtype("/Link");
        PdfDocument document = DocumentWithAnAnnotation(annotation);

        PageResizeOptions options = PageResizeOptions.Default;
        options.AutoRotate = true;
        document.Pages[0].Resize(PageSize.A4, PageOrientation.Landscape, options);

        // Turned a quarter clockwise with no scaling, so a point at (x, y) lands at (y, W - x)
        // where W is the width the page had. The rectangle's corners swap roles accordingly.
        PdfRectangle rect = TheAnnotationOf(document.Pages[0]).Elements.GetRectangle("/Rect");

        rect.X1.Should().BeApproximately(200, Tolerance);
        rect.Y1.Should().BeApproximately(A4Width - 300, Tolerance);
        rect.X2.Should().BeApproximately(400, Tolerance);
        rect.Y2.Should().BeApproximately(A4Width - 100, Tolerance);
    }

    // ------------------------------------------------- geometry that is not plain numbers

    [Fact]
    public void ACoordinateHeldIndirectlyIsStillMoved()
    {
        // Any object in a PDF may be indirect, a coordinate included. Reading one with GetReal
        // throws rather than following the reference, which used to abort the whole resize.
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;
        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));

        // Constructing with a document does not put the object in the cross reference table, so
        // it has no reference to point at until it is added.
        PdfRealObject indirect = new PdfRealObject(document, 300);
        document.Internals.AddObject(indirect);

        PdfDictionary annotation = AnnotationOfSubtype("/Polygon");
        PdfArray vertices = new PdfArray(document);
        vertices.Elements.Add(new PdfReal(100));
        vertices.Elements.Add(indirect.Reference);
        annotation.Elements["/Vertices"] = vertices;

        document.Internals.AddObject(annotation);
        PdfArray annotations = new PdfArray(document);
        annotations.Elements.Add(annotation.Reference);
        page.Elements["/Annots"] = annotations;

        HalveThePage(page);

        PdfArray moved = TheAnnotationOf(page).Elements.GetArray("/Vertices");
        moved.Elements.GetReal(0).Should().BeApproximately(50, Tolerance);
        moved.Elements.GetReal(1).Should().BeApproximately(150, Tolerance);
    }

    [Fact]
    public void GeometryThatIsNotNumbersIsLeftWholeRatherThanHalfMoved()
    {
        // Writing point by point would leave a malformed array partly moved, and would do it
        // after the content had been wrapped and the boxes set, with no way back. Nothing is
        // written unless all of it can be read.
        PdfDictionary annotation = AnnotationOfSubtype("/Polygon");
        PdfArray vertices = new PdfArray();
        vertices.Elements.Add(new PdfReal(100));
        vertices.Elements.Add(new PdfReal(200));
        vertices.Elements.Add(new PdfReal(300));
        vertices.Elements.Add(new PdfName("/NotANumber"));
        annotation.Elements["/Vertices"] = vertices;

        PdfDocument document = DocumentWithAnAnnotation(annotation);

        Action act = () => HalveThePage(document.Pages[0]);

        act.Should().NotThrow();

        PdfArray after = TheAnnotationOf(document.Pages[0]).Elements.GetArray("/Vertices");
        after.Elements.GetReal(0).Should().Be(100, "not one point may be moved if they cannot all be");
        after.Elements.GetReal(1).Should().Be(200);
        after.Elements.GetReal(2).Should().Be(300);
        after.Elements[3].Should().BeOfType<PdfName>();

        // The rectangle is a separate entry and does move, which is what the fallback for an
        // unmodelled subtype does too.
        TheAnnotationOf(document.Pages[0]).Elements.GetRectangle("/Rect")
            .X1.Should().BeApproximately(50, Tolerance);
    }

    [Fact]
    public void ARectangleHeldAsIndirectNumbersIsStillMoved()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;
        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));

        PdfDictionary annotation = new PdfDictionary(document);
        annotation.Elements.SetName("/Type", "/Annot");
        annotation.Elements.SetName("/Subtype", "/Link");

        PdfArray rect = new PdfArray(document);
        PdfRealObject indirectLeft = new PdfRealObject(document, 100);
        document.Internals.AddObject(indirectLeft);
        rect.Elements.Add(indirectLeft.Reference);
        rect.Elements.Add(new PdfReal(200));
        rect.Elements.Add(new PdfReal(300));
        rect.Elements.Add(new PdfReal(400));
        annotation.Elements["/Rect"] = rect;

        document.Internals.AddObject(annotation);
        PdfArray annotations = new PdfArray(document);
        annotations.Elements.Add(annotation.Reference);
        page.Elements["/Annots"] = annotations;

        HalveThePage(page);

        TheAnnotationOf(page).Elements.GetRectangle("/Rect").X1.Should().BeApproximately(50, Tolerance);
    }
}
