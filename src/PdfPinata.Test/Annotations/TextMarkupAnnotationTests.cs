using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   What the text markup annotations put in the file. The drawing they produce is checked by
///   <see cref="TextMarkupRenderingTests"/>; these are about the dictionary, which is where the
///   defect behind issue #342 lived — an annotation of the right subtype that no viewer draws,
///   because the quadrilaterals it is required to carry were not there.
/// </summary>
public class TextMarkupAnnotationTests
{
    [Theory]
    [InlineData(typeof(PdfHighlightAnnotation), "/Highlight")]
    [InlineData(typeof(PdfUnderlineAnnotation), "/Underline")]
    [InlineData(typeof(PdfStrikeOutAnnotation), "/StrikeOut")]
    [InlineData(typeof(PdfSquigglyAnnotation), "/Squiggly")]
    public void EachSubtypeNamesItself(System.Type type, string subtype)
    {
        var annotation = (PdfTextMarkupAnnotation)System.Activator.CreateInstance(type);

        // ReSharper disable once PossibleNullReferenceException
        annotation.Elements.GetName("/Subtype").Should().Be(subtype);
    }

    [Fact]
    public void AQuadIsWrittenAsTheFourCornersEveryProducerWrites()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 16));

        // Upper-left, upper-right, lower-left, lower-right — the order viewers read, rather
        // than the counterclockwise order the specification's prose asks for.
        Numbers(annotation, "/QuadPoints").Should().Equal(
            30, 716, 100, 716,
            30, 700, 100, 700);
    }

    [Fact]
    public void TheRectangleBecomesTheBoxAroundEveryQuad()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 16));
        annotation.AddQuad(new XRect(120, 660, 40, 12));

        Numbers(annotation, "/Rect").Should().Equal(30, 660, 160, 716);
    }

    [Fact]
    public void QuadsReadBackAsTheyWereGiven()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 16));
        annotation.AddQuad(new XRect(120, 660, 40, 12));

        annotation.Quads.Select(q => new[] { q.X1, q.Y1, q.X2, q.Y2 })
            .Should().BeEquivalentTo([
                new[] { 30d, 700d, 100d, 716d },
                new[] { 120d, 660d, 160d, 672d }
            ]);
    }

    [Fact]
    public void ClearingTheQuadsLeavesNone()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());
        annotation.AddQuad(new XRect(30, 700, 70, 16));

        annotation.ClearQuads();

        annotation.Quads.Should().BeEmpty();
        annotation.Elements.ContainsKey("/QuadPoints").Should().BeFalse();
    }

    /// <summary>
    ///   The code in issue #342 sets a rectangle and never mentions a quadrilateral. An
    ///   annotation that took that literally would carry no quads and draw nothing, which is
    ///   the complaint the issue was raised about.
    /// </summary>
    [Fact]
    public void AnAnnotationGivenOnlyARectangleIsStillDrawn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var annotation = new PdfHighlightAnnotation { Rectangle = new PdfRectangle(new XRect(30, 700, 100, 20)) };

        page.Annotations.Add(annotation);

        Appearance(annotation).Should().NotBeNull();
        Content(annotation).Should().Contain("30 700 100 20 re f");
    }

    [Fact]
    public void TheAppearanceIsAFormCoveringTheAnnotationRectangle()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());
        annotation.AddQuad(new XRect(30, 700, 70, 16));

        var form = Appearance(annotation);

        form.Elements.GetName("/Type").Should().Be("/XObject");
        form.Elements.GetName("/Subtype").Should().Be("/Form");
        Numbers(form, "/BBox").Should().Equal(30, 700, 100, 716);
    }

    /// <summary>
    ///   An annotation has no owning document until it is added to a page, and an appearance
    ///   stream is an object in the document. Whichever side of the Add a property is set,
    ///   it has to reach the appearance.
    /// </summary>
    [Fact]
    public void PropertiesSetBeforeTheAnnotationIsAddedReachTheAppearance()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var annotation = new PdfHighlightAnnotation();
        annotation.AddQuad(new XRect(30, 700, 70, 16));
        annotation.Color = XColors.Lime;

        page.Annotations.Add(annotation);

        Content(annotation).Should().Contain("0 1 0 rg");
    }

    [Fact]
    public void PropertiesSetAfterTheAnnotationIsAddedReachTheAppearance()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 16));
        annotation.Color = XColors.Lime;

        Content(annotation).Should().Contain("0 1 0 rg");
    }

    /// <summary>
    ///   Opacity is carried in the graphics state of the appearance rather than left in /CA,
    ///   which a viewer ignores once there is an appearance for it to apply.
    /// </summary>
    [Fact]
    public void OpacityReachesTheGraphicsStateOfTheAppearance()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());
        annotation.AddQuad(new XRect(30, 700, 70, 16));

        annotation.Opacity = 0.4;

        var state = Appearance(annotation).Elements.GetDictionary("/Resources")
            .Elements.GetDictionary("/ExtGState").Elements.GetDictionary("/GS0");
        state.Elements.GetReal("/ca").Should().Be(0.4);
        state.Elements.GetReal("/CA").Should().Be(0.4);
        state.Elements.GetName("/BM").Should().Be("/Multiply");
    }

    /// <summary>
    ///   The appearance is rewritten in place, so changing a colour twenty times does not leave
    ///   twenty streams behind in the document.
    /// </summary>
    [Fact]
    public void RewritingTheAppearanceDoesNotLeaveTheOldOneBehind()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var annotation = new PdfHighlightAnnotation();
        page.Annotations.Add(annotation);
        annotation.AddQuad(new XRect(30, 700, 70, 16));

        var form = Appearance(annotation);
        var countBefore = document.Internals.GetAllObjects().Length;

        for (var i = 0; i < 20; i++)
            annotation.Color = i % 2 == 0 ? XColors.Lime : XColors.Yellow;

        document.Internals.GetAllObjects().Length.Should().Be(countBefore);
        Appearance(annotation).Should().BeSameAs(form);
        Content(annotation).Should().Contain("1 1 0 rg");
    }

    [Fact]
    public void AnAnnotationNeverAddedToAPageHasNoAppearance()
    {
        var annotation = new PdfHighlightAnnotation();

        annotation.AddQuad(new XRect(30, 700, 70, 16));

        annotation.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    [Fact]
    public void TheUnderlineIsRuledAlongTheFootOfTheQuad()
    {
        var annotation = OnAPage(new PdfUnderlineAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 14));

        // A fourteenth of the height, that far above the bottom.
        Content(annotation).Should().Contain("30 701 70 1 re f");
    }

    [Fact]
    public void TheStrikeOutIsRuledThroughTheQuad()
    {
        var annotation = OnAPage(new PdfStrikeOutAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 14));

        Content(annotation).Should().Contain("30 706 70 1 re f");
    }

    [Fact]
    public void TheSquigglyIsStrokedAsAZigzagThatStopsAtTheEndOfTheQuad()
    {
        var annotation = OnAPage(new PdfSquigglyAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 14));

        var content = Content(annotation);
        content.Should().Contain(" m\n").And.Contain(" l\n").And.EndWith("S\n");
        // The wave is clipped to the quad rather than allowed to overhang it.
        content.Should().Contain("100 ");
        Numbers(annotation, "/Rect").Should().Equal(30, 700, 100, 714);
    }

    [Fact]
    public void EveryQuadIsDrawn()
    {
        var annotation = OnAPage(new PdfHighlightAnnotation());

        annotation.AddQuad(new XRect(30, 700, 70, 16));
        annotation.AddQuad(new XRect(120, 660, 40, 12));

        var content = Content(annotation);
        content.Should().Contain("30 700 70 16 re f");
        content.Should().Contain("120 660 40 12 re f");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheAnnotationSurvivesBeingSavedAndRead(bool quadsBeforeTheAnnotationIsAdded)
    {
        // The quad array is built before the annotation has an owning document in one of these
        // orders and after it in the other, and has to be written out either way.
        var document = new PdfDocument();
        var page = document.AddPage();
        var annotation = new PdfHighlightAnnotation();
        if (quadsBeforeTheAnnotationIsAdded)
        {
            annotation.AddQuad(new XRect(30, 700, 70, 16));
            page.Annotations.Add(annotation);
        }
        else
        {
            page.Annotations.Add(annotation);
            annotation.AddQuad(new XRect(30, 700, 70, 16));
        }

        using var stream = new System.IO.MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var reread = Pdf.IO.PdfReader.Open(stream, Pdf.IO.PdfDocumentOpenMode.Import);

        var annots = reread.Pages[0].Elements.GetArray("/Annots");
        var dict = annots.Elements.GetDictionary(0);
        dict.Elements.GetName("/Subtype").Should().Be("/Highlight");
        dict.Elements.GetArray("/QuadPoints").Elements.Count.Should().Be(8);
        dict.Elements.GetArray("/QuadPoints").Elements.GetReal(0).Should().Be(30);
        dict.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    private static T OnAPage<T>(T annotation) where T : PdfTextMarkupAnnotation
    {
        var document = new PdfDocument();
        document.AddPage().Annotations.Add(annotation);
        return annotation;
    }

    private static PdfDictionary Appearance(PdfAnnotation annotation)
    {
        var ap = annotation.Elements.GetDictionary("/AP");
        return ap == null ? null : (PdfDictionary)ap.Elements.GetObject("/N");
    }

    private static string Content(PdfAnnotation annotation)
    {
        return System.Text.Encoding.ASCII.GetString(Appearance(annotation).Stream.UnfilteredValue);
    }

    private static double[] Numbers(PdfDictionary dictionary, string key)
    {
        var item = dictionary.Elements[key];
        if (item is PdfRectangle rectangle)
            return [rectangle.X1, rectangle.Y1, rectangle.X2, rectangle.Y2];

        var array = dictionary.Elements.GetArray(key);
        return [..Enumerable.Range(0, array.Elements.Count).Select(array.Elements.GetReal)];
    }
}
