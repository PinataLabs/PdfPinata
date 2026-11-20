using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Shadings: what <c>sh</c> and a shading pattern hand a device, and what a device that draws no
/// shaders is handed in their place.
/// </summary>
/// <remarks>
/// The page is 200 by 100, so its transform to the device is <c>[1 0 0 -1 0 100]</c>.
/// </remarks>
public class ShadingTests
{
    internal const string RedToBlue = "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    private const string PageBox = "M0,100 L200,100 L200,0 L0,0 Z";

    internal static PdfDictionary Axial(PdfDocument document, params (string Key, object Value)[] more) =>
        Drawn.Object(document, [
            ("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[0 0 100 0]"), ("/Function", RedToBlue),
            .. more
        ]);

    internal static PdfDictionary PatternOf(PdfDocument document, PdfDictionary shading, string matrix = null)
    {
        var pattern = Drawn.Object(document, ("/PatternType", "2"), ("/Shading", shading));
        if (matrix != null)
            pattern.Elements["/Matrix"] = new PdfLiteral(matrix);
        return pattern;
    }

    private static Drawn WithShading(string content, bool drawsShaders = true, params (string Key, object Value)[] more) =>
        Drawn.Saved(content, (document, resources) =>
        {
            var shading = Axial(document, more);
            Drawn.Add(document, resources, "/Shading", "/Sh0", shading);
            Drawn.Add(document, resources, "/Pattern", "/P0", PatternOf(document, shading, "[1 0 0 1 20 0]"));
            Drawn.Add(document, resources, "/ExtGState", "/Half", Drawn.Object(document, ("/ca", "0.5"), ("/CA", "0.25")));
        }, drawsShaders);

    [Test]
    public void ShFillsThePageBoxWithTheShadingUnderTheTransformInForce()
    {
        var drawn = WithShading("2 0 0 2 10 10 cm /Sh0 sh", more: ("/Extend", "[true false]"));

        drawn.Device.Body.Should().Equal("fill nonzero " + PageBox + " axial");
        var fill = drawn.Device.Fills.Single();
        fill.Shader!.Value.Transform.Should().Be(new XMatrix(2, 0, 0, -2, 10, 90));
        fill.Stops.Should().Equal("0 rgb(1,0,0)", "1 rgb(0,0,1)");

        var axial = fill.ShaderAs<PageAxialShading>();
        axial.Start.Should().Be(new XPoint(0, 0));
        axial.End.Should().Be(new XPoint(100, 0));
        axial.ExtendStart.Should().BeTrue();
        axial.ExtendEnd.Should().BeFalse();
        axial.IsShared.Should().BeTrue();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void ARadialShadingHandsOverBothCircles()
    {
        var drawn = Drawn.Saved("/Sh0 sh", (document, resources) =>
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Object(document,
                ("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[10 20 5 30 40 25]"),
                ("/Function", RedToBlue), ("/Extend", "[false true]"))));

        var radial = drawn.Device.Fills.Single().ShaderAs<PageRadialShading>();
        (radial.StartCenter, radial.StartRadius).Should().Be((new XPoint(10, 20), 5));
        (radial.EndCenter, radial.EndRadius).Should().Be((new XPoint(30, 40), 25));
        (radial.ExtendStart, radial.ExtendEnd).Should().Be((false, true));
    }

    [Test]
    public void AShadingPatternLiesInTheSpaceThePageBeganInAndNotTheOneCmMade()
    {
        var drawn = WithShading("/Pattern cs /P0 scn 2 0 0 2 0 0 cm 0 0 50 50 re f");

        // The cm doubled the path. The pattern is still its own matrix over the page's space.
        var fill = drawn.Device.Fills.Single();
        fill.DevicePath.Should().Be("M0,100 L100,100 L100,0 L0,0 Z");
        fill.Shader!.Value.Transform.Should().Be(new XMatrix(1, 0, 0, -1, 20, 100));
    }

    [Test]
    public void AShadingPatternInAFormLiesInTheSpaceTheFormBeganIn()
    {
        var drawn = Drawn.Saved("1 0 0 1 10 0 cm /Fm1 Do", (document, resources) =>
        {
            Drawn.Add(document, resources, "/Pattern", "/P0", PatternOf(document, Axial(document), "[1 0 0 1 20 0]"));
            var form = Drawn.Form(document, "/Pattern cs /P0 scn 3 0 0 3 0 0 cm 0 0 5 5 re f");
            form.Elements["/Matrix"] = new PdfLiteral("[2 0 0 2 0 20]");
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        // The pattern's matrix, then the form's, then the transform the form was drawn under.
        drawn.Device.Fills.Single().Shader!.Value.Transform.Should().Be(new XMatrix(2, 0, 0, -2, 50, 80));
    }

    [Test]
    public void OneShaderIsHandedOverEachTimeItsShadingIsPaintedWith()
    {
        var drawn = WithShading("/Sh0 sh 2 0 0 2 0 0 cm /Sh0 sh /Pattern cs /P0 scn 0 0 5 5 re f");

        var shaders = drawn.Device.Fills.Select(fill => fill.Shader!.Value.Shader).ToList();
        shaders[1].Should().BeSameAs(shaders[0]);
        shaders[2].Should().BeSameAs(shaders[0], "a pattern with no background paints with the shading itself");
    }

    [Test]
    public void AStrokeInAShadingPatternIsAStrokeWithTheShader()
    {
        var drawn = WithShading("/Pattern CS /P0 SCN 4 w 0 0 m 50 0 l S");

        var stroke = drawn.Device.Strokes.Single();
        stroke.ToString().Should().Be("stroke M0,100 L50,100 axial");
        stroke.Stroke.Width.Should().Be(4);
        stroke.Shader!.Value.Transform.Should().Be(new XMatrix(1, 0, 0, -1, 20, 100));
    }

    [Test]
    public void AGlyphFilledAndStrokedInAShadingPatternKeepsItsSharedOutline()
    {
        var drawn = Drawn.Saved("/Pattern cs /P0 scn /Pattern CS /P0 SCN BT /F1 10 Tf 2 Tr 30 40 Td (A) Tj ET",
            (document, resources) =>
            {
                Drawn.Add(document, resources, "/Pattern", "/P0", PatternOf(document, Axial(document), "[1 0 0 1 20 0]"));
                Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 500));
            }, glyphSources: _ => new SquareGlyphs());

        drawn.Device.Body.Should().HaveCount(2);
        foreach (var call in new[] { drawn.Device.Fills.Single(), drawn.Device.Strokes.Single() })
        {
            call.Path.IsShared.Should().BeTrue();
            call.Shader!.Value.Transform.Should().Be(new XMatrix(1, 0, 0, -1, 20, 100),
                "the glyph has its own transform and the pattern is not under it");
        }
    }

    [Test]
    public void TheAlphaOfTheGraphicsStateIsTheShaderPaints()
    {
        var drawn = WithShading("/Half gs /Sh0 sh /Pattern cs /P0 scn /Pattern CS /P0 SCN 0 0 5 5 re B");

        drawn.Device.Calls.Where(call => call.Shader != null).Select(call => call.Shader!.Value.Alpha)
            .Should().Equal(0.5, 0.5, 0.25);
    }

    [Test]
    public void ABBoxIsAClipRoundTheCallAndNeverReachesTheDevice()
    {
        var drawn = WithShading("/Sh0 sh /Pattern cs /P0 scn 0 0 5 5 re f", more: ("/BBox", "[0 0 50 40]"));

        // Under the transform in force for sh, and under the pattern's own, moved right by 20, for the fill.
        drawn.Device.Body.Should().Equal(
            "save",
            "clip nonzero M0,100 L50,100 L50,60 L0,60 Z",
            "fill nonzero " + PageBox + " axial",
            "restore",
            "save",
            "clip nonzero M20,100 L70,100 L70,60 L20,60 Z",
            "fill nonzero M0,100 L5,100 L5,95 L0,95 Z axial",
            "restore");
    }

    [Test]
    public void ABackgroundIsPartOfAPatternsShaderAndNoPartOfWhatShPaints()
    {
        var drawn = WithShading("/Sh0 sh /Pattern cs /P0 scn 0 0 5 5 re f", more: ("/Background", "[0 1 0]"));

        var shaders = drawn.Device.Fills.Select(fill => fill.ShaderAs<PageAxialShading>()).ToList();
        shaders[0].Background.Should().BeNull();
        shaders[1].Background.Should().Be(new PagePaint(0, 1, 0));
    }

    [Test]
    public void APatternSpaceStartsAtThePatternThatPaintsNothing()
    {
        var drawn = WithShading("/Pattern cs /Pattern CS 0 0 5 5 re B 1 0 0 rg 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(1,0,0)");
        drawn.Problems.Should().BeEmpty();
    }

    [Test]
    public void APatternTheResourcesLackPaintsNothingAndIsReported()
    {
        var drawn = WithShading("/Pattern cs /Nope scn 0 0 5 5 re f /Nope sh");

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("MissingResource /Nope");
        drawn.Result.Diagnostics.Single().Occurrences.Should().Be(2);
    }

    [Test]
    public void ADeviceThatDrawsNoShadersIsHandedTheMeanColourOfAShadingAndTheRenderSaysSo()
    {
        var drawn = WithShading("/Half gs /Sh0 sh /Pattern cs /P0 scn /Pattern CS /P0 SCN 0 0 5 5 re B", drawsShaders: false);

        drawn.Device.Body.Should().Equal(
            "fill nonzero " + PageBox + " rgb(0.5,0,0.5) a0.5",
            "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0.5,0,0.5) a0.5",
            "stroke M0,100 L5,100 L5,95 L0,95 Z rgb(0.5,0,0.5) a0.25");
        drawn.Problems.Should().Equal("UnsupportedFeature sh", "UnsupportedFeature /Pattern");
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test]
    public void AShadingOfATypeThereIsNotIsInvalid()
    {
        var drawn = Drawn.Saved("/Sh0 sh 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Object(document,
                ("/ShadingType", "9"), ("/ColorSpace", "/DeviceRGB"))));

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test]
    [Arguments("/Coords [0 0 0 0]", "an axis of no length")]
    [Arguments("/Coords [0 0 1]", "too few coordinates")]
    [Arguments("/ColorSpace /Pattern", "a pattern space")]
    [Arguments("/Function << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>", "one output for three components")]
    public void AShadingThatCannotBeReadPaintsNothingAndIsReported(string entry, string because)
    {
        var parts = entry.Split(' ', 2);
        var drawn = WithShading("/Sh0 sh /Pattern cs /P0 scn 0 0 5 5 re f", more: (parts[0], parts[1]));

        drawn.Device.Body.Should().BeEmpty(because);
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test]
    public void AShadingUnderATransformWithNoAreaPaintsNothingAndIsReported()
    {
        var drawn = WithShading("1 0 0 0 0 0 cm /Sh0 sh");

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }
}
