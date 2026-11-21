using System;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Rendering.Skia.Tests.Helpers;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// Hand-written pages that each draw one feature, rendered here and by Ghostscript and compared.
/// </summary>
/// <remarks>
/// Each tolerance is set a little above what the comparison measured when it was written, so a
/// change that moves a page is seen. Most of what remains is the two rasterizers' anti-aliasing:
/// Ghostscript's comes from sampling each pixel 4 by 4, and Skia's from the coverage it computes.
/// Where more remains, the test says what it is.
///
/// DeviceCMYK fills are not compared here. Ghostscript converts them to RGB through an ICC profile,
/// and page rendering converts them by the formula ISO 32000-1 10.3.5 gives, with no colour
/// management, so the two differ by design; <c>SkiaPageRenderingTests</c> checks the colours it
/// paints instead. A CMYK JPEG is compared, with a tolerance wide enough for that difference and
/// narrow enough that an inversion of its inks is seen.
/// Text is not here yet, because the glyph sources it needs are other issues. Shadings in DeviceCMYK
/// are left out for the reason fills in it are.
/// </remarks>
[Rasterizing]
public class GhostscriptFidelityTests
{
    private const string Circle =
        "150 75 m 150 102.6 127.6 125 100 125 c 72.4 125 50 102.6 50 75 c " +
        "50 47.4 72.4 25 100 25 c 127.6 25 150 47.4 150 75 c h ";

    [Test]
    public void FillsInDeviceRgbAndDeviceGray() =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 10 10 80 60 re f " +
                "0 0.5 0.25 rg 110 10 80 60 re f " +
                "0.5 g 10 80 80 60 re f " +
                "0.2 0.4 0.9 rg 110 80 80 60 re f"),
            nameof(FillsInDeviceRgbAndDeviceGray), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    // The hairline, width 0, lies on a boundary between two rows of pixels. Ghostscript paints one
    // of them a light grey and Skia paints both a middle grey, which is most of the share over the
    // threshold.
    [Test]
    public void StrokesWithEachCapAndJoin() =>
        Fidelity.Of(HandWritten.Page(
                "12 w 0 0 1 RG " +
                "0 J 20 130 m 60 130 l S 1 J 80 130 m 120 130 l S 2 J 140 130 m 180 130 l S " +
                "0 J 0 j 20 20 m 40 90 l 60 20 l S " +
                "1 j 80 20 m 100 90 l 120 20 l S " +
                "2 j 140 20 m 160 90 l 180 20 l S " +
                "0 w 0 0 0 RG 10 105 m 190 105 l S"),
            nameof(StrokesWithEachCapAndJoin), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.5, shareOverThreshold: 0.01);

    // A dash of no length that falls exactly on the end of a path is a dot to Ghostscript and
    // nothing to Skia, and the dashes round the closed rectangle's corners join differently.
    [Test]
    public void DashedLines() =>
        Fidelity.Of(HandWritten.Page(
                "4 w 0.6 0 0 RG " +
                "[12 6] 0 d 10 130 m 190 130 l S " +
                "[12 6] 9 d 10 110 m 190 110 l S " +
                "[9] 0 d 10 90 m 190 90 l S " +
                "1 J [0 10] 0 d 10 70 m 190 70 l S " +
                "0 J [20 5 5 5] 0 d 20 15 m 20 50 l 180 50 l 180 15 l h S"),
            nameof(DashedLines), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.5, shareOverThreshold: 0.02);

    [Test]
    public void CurvesUnderATransformThatScalesUnevenly() =>
        Fidelity.Of(HandWritten.Page(
                "q 1 0 0 0.5 0 37.5 cm 6 w 0 0.5 0 RG " + Circle + "S Q " +
                "q 0.3 0 0 0.3 10 10 cm 0.8 0.2 0 rg " + Circle + "f Q"),
            nameof(CurvesUnderATransformThatScalesUnevenly), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.75, shareOverThreshold: 0.004);

    [Test]
    public void NonzeroAndEvenOddFills() =>
        Fidelity.Of(HandWritten.Page(
                "0 0 0.7 rg " +
                "10 20 80 110 re 25 40 50 70 re f " +
                "110 20 80 110 re 125 40 50 70 re f*"),
            nameof(NonzeroAndEvenOddFills), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    [Test]
    public void ClipsNestedAndRestored() =>
        Fidelity.Of(HandWritten.Page(
                "q " + Circle + "W n " +
                "q 0 0 100 150 re W n 1 0 0 rg 0 0 200 150 re f Q " +
                "0 0 1 rg 100 0 100 150 re f Q " +
                "q 10 10 40 40 re 20 20 20 20 re W* n 0 0.6 0 rg 0 0 60 60 re f Q " +
                "0 g 160 10 30 30 re f"),
            nameof(ClipsNestedAndRestored), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1, shareOverThreshold: 0.008);

    [Test]
    public void ConstantAlpha() =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 20 20 100 80 re f /Half gs 0 0 1 rg 80 50 100 80 re f " +
                "0 1 0 RG 10 w 20 130 m 180 20 l S",
                page: (document, page) => HandWritten.ExtGState(document, page, "/Half", state =>
                {
                    state.Elements.SetReal("/ca", 0.5);
                    state.Elements.SetReal("/CA", 0.5);
                })),
            nameof(ConstantAlpha), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);

    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public void APageTurnedByRotate(int rotate) =>
        Fidelity.Of(HandWritten.Page(
                "1 0 0 rg 10 10 60 30 re f 0 0 1 rg 150 100 40 40 re f " +
                "3 w 0 g 10 140 m 100 75 l 190 140 l S",
                page: (_, page) => page.Rotate = rotate),
            nameof(APageTurnedByRotate) + rotate, new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.6, shareOverThreshold: 0.001);

    [Test]
    public void ARegionAtAScale() =>
        Fidelity.Of(HandWritten.Page(
                "0.8 0.4 0 rg " + Circle + "f 4 w 0 0 0.5 RG 0 0 m 200 150 l S"),
            nameof(ARegionAtAScale), new PageRenderOptions { Scale = 3, Region = new XRect(40, 30, 90, 60) })
            .ShouldBeWithin(meanChannelDifference: 0.6, shareOverThreshold: 0.001);

    private const string RedToBlue = "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    // Red, a hard edge to green at a third, a hard edge to a blue ramp at two thirds.
    private const string ThreeBands =
        "<< /FunctionType 3 /Domain [0 1] /Bounds [0.333 0.667] /Encode [0 1 0 1 0 1] /Functions [" +
        "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [1 0.8 0] /N 1 >> " +
        "<< /FunctionType 2 /Domain [0 1] /C0 [0 0.6 0] /C1 [0 0.6 0] /N 1 >> " +
        "<< /FunctionType 2 /Domain [0 1] /C0 [0 0 1] /C1 [1 1 1] /N 2 >>] >>";

    private static PdfDocument PageWith(string content, params (string Category, string Name, Func<PdfDocument, PdfItem> Make)[] resources) =>
        HandWritten.Page(content, page: (document, page) =>
        {
            foreach (var (category, name, make) in resources)
                HandWritten.Resource(document, page, category, name, make(document));
        });

    private static Func<PdfDocument, PdfDictionary> Shading(params (string Key, object Value)[] entries) =>
        document => HandWritten.Object(document, entries);

    private static Func<PdfDocument, PdfDictionary> ShadingPattern(string matrix, params (string Key, object Value)[] shading) =>
        document => HandWritten.Object(document, ("/PatternType", "2"), ("/Matrix", matrix),
            ("/Shading", HandWritten.Object(document, shading)));

    // Shadings. Ghostscript paints a shading without smoothing its edge, and that edge is what
    // remains in each of these: the colours inside agree.

    // sh under a clip, the axis at a slant, both ends carried on to the clip's edges.
    [Test]
    public void AnAxialShadingExtendedPastBothEnds() =>
        Fidelity.Of(PageWith(
                "q 10 10 180 130 re W n /Sh0 sh Q",
                ("/Shading", "/Sh0", Shading(("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[60 40 140 110]"), ("/Function", RedToBlue), ("/Extend", "[true true]")))),
            nameof(AnAxialShadingExtendedPastBothEnds), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.3, shareOverThreshold: 0.001);

    // One end carried on and the other not, each way round, through a function with two hard edges.
    [Test]
    public void AnAxialShadingExtendedPastOneEnd() =>
        Fidelity.Of(PageWith(
                "q 10 80 180 60 re W n /Start sh Q q 10 10 180 60 re W n /End sh Q",
                ("/Shading", "/Start", Shading(("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[50 0 150 0]"), ("/Function", ThreeBands), ("/Extend", "[true false]"))),
                ("/Shading", "/End", Shading(("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[50 0 150 0]"), ("/Function", ThreeBands), ("/Extend", "[false true]")))),
            nameof(AnAxialShadingExtendedPastOneEnd), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.4, shareOverThreshold: 0.002);

    // Two circles that are not one inside the other, so the extension is a cone; and beside it the
    // usual case of one inside the other, extended past the outer.
    [Test]
    public void ARadialShadingExtended() =>
        Fidelity.Of(PageWith(
                "q 0 0 100 150 re W n /Cone sh Q q 100 0 100 150 re W n /Spot sh Q",
                ("/Shading", "/Cone", Shading(("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[30 110 8 60 50 25]"), ("/Function", RedToBlue), ("/Extend", "[true true]"))),
                ("/Shading", "/Spot", Shading(("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[140 85 5 150 75 40]"), ("/Function", RedToBlue), ("/Extend", "[true true]")))),
            nameof(ARadialShadingExtended), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.3, shareOverThreshold: 0.001);

    [Test]
    public void ARadialShadingExtendedPastOneEndOrNeither() =>
        Fidelity.Of(PageWith(
                "0.9 g 0 0 200 150 re f /Inner sh /Outer sh /Neither sh",
                ("/Shading", "/Inner", Shading(("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[40 100 15 40 100 35]"), ("/Function", RedToBlue), ("/Extend", "[true false]"))),
                ("/Shading", "/Outer", Shading(("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[40 40 15 40 40 35]"), ("/Function", RedToBlue), ("/Extend", "[false false]"))),
                ("/Shading", "/Neither", Shading(("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[130 110 6 160 50 30]"), ("/Function", RedToBlue), ("/Extend", "[false false]")))),
            nameof(ARadialShadingExtendedPastOneEndOrNeither), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.6, shareOverThreshold: 0.006);

    // A shading pattern lies on the page by its own matrix: the cm before each path moves the path
    // and not the pattern. It is filled, stroked, and filled at half alpha over a background.
    [Test]
    public void AShadingPatternFilledAndStroked() =>
        Fidelity.Of(PageWith(
                "/Pattern cs /P0 scn /Pattern CS /P0 SCN " +
                "10 10 80 60 re f q 1 0 0 1 100 0 cm 10 10 80 60 re f Q " +
                "12 w 20 90 m 100 135 l 180 90 l S " +
                "/Half gs /Boxed scn 60 30 80 90 re f",
                ("/Pattern", "/P0", ShadingPattern("[1 0 0 1 0 0]", ("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[10 0 190 0]"), ("/Function", RedToBlue), ("/Extend", "[true true]"))),
                ("/Pattern", "/Boxed", ShadingPattern("[1 0 0 1 60 30]", ("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"),
                    ("/Coords", "[40 45 0 40 45 30]"), ("/Function", RedToBlue), ("/Background", "[1 1 0]"),
                    ("/BBox", "[10 10 70 80]"))),
                ("/ExtGState", "/Half", document => HandWritten.Object(document, ("/ca", "0.5")))),
            nameof(AShadingPatternFilledAndStroked), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.3, shareOverThreshold: 0.008);

    private static Func<PdfDocument, PdfDictionary> Tiling(string content, params (string Key, object Value)[] entries) =>
        document =>
        {
            var pattern = HandWritten.Stream(document, content, entries);
            pattern.Elements["/PatternType"] = new PdfLiteral("1");
            pattern.Elements["/TilingType"] = new PdfLiteral("1");
            pattern.Elements["/Resources"] = new PdfLiteral("<< >>");
            return pattern;
        };

    // Tiling patterns have the widest tolerances here, and most of it is Ghostscript's. It draws
    // a tile without smoothing, and widens a line in it to every pixel the line touches, so each
    // edge in a tile differs by a pixel. Skia repeats a picture of one tile, and where the
    // pattern's matrix turns it, as in the first of these, it is resampled and its edges are soft.

    // A cell of two colours, turned by the pattern's matrix, filling a shape and stroking a line.
    [Test]
    public void AColouredTilingPattern() =>
        Fidelity.Of(PageWith(
                "/Pattern cs /T0 scn " + Circle + "f /Pattern CS /T0 SCN 14 w 10 10 m 190 10 l S",
                ("/Pattern", "/T0", Tiling(
                    "1 0 0 rg 0 0 10 10 re f 0 0 1 rg 10 10 10 10 re f 0 0.6 0 RG 2 w 0 20 m 20 0 l S",
                    ("/PaintType", "1"), ("/BBox", "[0 0 20 20]"), ("/XStep", "20"), ("/YStep", "20"),
                    ("/Matrix", "[0.966 0.259 -0.259 0.966 5 5]")))),
            nameof(AColouredTilingPattern), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 5.5, shareOverThreshold: 0.05);

    // One cell painted in two colours, with a step wider than the cell one way and narrower the
    // other, so tiles stand apart across and overlap up.
    [Test]
    public void AnUncolouredTilingPattern() =>
        Fidelity.Of(PageWith(
                "/Tint cs 0.8 0.1 0.1 /T0 scn 10 10 85 130 re f 0.1 0.3 0.8 /T0 scn 105 10 85 130 re f",
                ("/ColorSpace", "/Tint", _ => new PdfLiteral("[/Pattern /DeviceRGB]")),
                ("/Pattern", "/T0", Tiling(
                    "8 8 m 16 8 l 8 16 l f 2 w 0 0 16 16 re S",
                    ("/PaintType", "2"), ("/BBox", "[0 0 16 16]"), ("/XStep", "22"), ("/YStep", "12")))),
            nameof(AnUncolouredTilingPattern), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 6.3, shareOverThreshold: 0.045);

    private const string Rgb16 = "[0 65535 0 65535 0 1 0 1 0 1]";

    private static Func<PdfDocument, PdfDictionary> Mesh(int type, MeshData data, params (string Key, object Value)[] entries) =>
        document =>
        {
            var shading = HandWritten.Stream(document, data.ToString(), entries);
            shading.Elements["/ShadingType"] = new PdfLiteral(type.ToString());
            shading.Elements["/ColorSpace"] = new PdfLiteral("/DeviceRGB");
            shading.Elements["/BitsPerCoordinate"] = new PdfLiteral("16");
            shading.Elements["/BitsPerComponent"] = new PdfLiteral("8");
            if (type != 5)
                shading.Elements["/BitsPerFlag"] = new PdfLiteral("8");
            return shading;
        };

    // Meshes. Neither rasterizer smooths the outline of a mesh, and the two put it on different
    // pixels. Inside a patch a little more remains: it is drawn here as a grid of flat triangles.

    // A colour that is a function of both coordinates, over a square that the shading's matrix
    // makes a slanted box.
    [Test]
    public void AFunctionBasedShading() =>
        Fidelity.Of(PageWith(
                "0.9 g 0 0 200 150 re f /Sh0 sh",
                ("/Shading", "/Sh0", document => HandWritten.Object(document, ("/ShadingType", "1"),
                    ("/ColorSpace", "/DeviceRGB"), ("/Domain", "[0 1 0 1]"), ("/Matrix", "[120 20 30 100 25 15]"),
                    ("/Function", HandWritten.Stream(document, "{ 2 copy mul 3 1 roll }",
                        ("/FunctionType", "4"), ("/Domain", "[0 1 0 1]"), ("/Range", "[0 1 0 1 0 1]")))))),
            nameof(AFunctionBasedShading), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.4, shareOverThreshold: 0.005);

    // Triangles joined by their flags, and a lattice, each corner its own colour.
    [Test]
    public void TriangleMeshes() =>
        Fidelity.Of(PageWith(
                "/Free sh /Lattice sh",
                ("/Shading", "/Free", Mesh(4, new MeshData()
                    .Flag(0).Point(10, 20).Color(255, 0, 0)
                    .Flag(0).Point(90, 30).Color(0, 255, 0)
                    .Flag(0).Point(30, 130).Color(0, 0, 255)
                    .Flag(1).Point(95, 140).Color(255, 255, 0)
                    .Flag(2).Point(60, 75).Color(255, 255, 255), ("/Decode", Rgb16))),
                ("/Shading", "/Lattice", Mesh(5, new MeshData()
                    .Point(110, 20).Color(255, 0, 0).Point(150, 10).Color(0, 255, 0).Point(190, 25).Color(0, 0, 255)
                    .Point(105, 75).Color(0, 0, 0).Point(150, 80).Color(255, 255, 255).Point(195, 70).Color(0, 0, 0)
                    .Point(115, 135).Color(0, 255, 255).Point(150, 140).Color(255, 0, 255).Point(185, 130).Color(255, 255, 0),
                    ("/Decode", Rgb16), ("/VerticesPerRow", "3")))),
            nameof(TriangleMeshes), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.6, shareOverThreshold: 0.009);

    // The same two meshes in values that do not fill a byte: 12-bit coordinates, 4-bit
    // components and, in the free-form mesh, a 2-bit flag. A vertex is then 38 bits, or 36 in the
    // lattice, and each is padded to a whole byte. Read without the padding, every vertex after
    // the first is wrong.
    [Test]
    public void AFreeFormMeshOfValuesNarrowerThanAByte() =>
        Fidelity.Of(PageWith(
                "/Free sh",
                ("/Shading", "/Free", NarrowMesh(4, new PackedMesh(2, 12, 4)
                    .Vertex(0, 10, 20, 15, 0, 0)
                    .Vertex(0, 90, 30, 0, 15, 0)
                    .Vertex(0, 30, 130, 0, 0, 15)
                    .Vertex(1, 95, 140, 15, 15, 0)
                    .Vertex(2, 60, 75, 15, 15, 15)))),
            nameof(AFreeFormMeshOfValuesNarrowerThanAByte), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);

    [Test]
    public void ALatticeMeshOfValuesNarrowerThanAByte() =>
        Fidelity.Of(PageWith(
                "/Lattice sh",
                ("/Shading", "/Lattice", NarrowMesh(5, new PackedMesh(0, 12, 4)
                    .Vertex(0, 110, 20, 15, 0, 0).Vertex(0, 150, 10, 0, 15, 0).Vertex(0, 190, 25, 0, 0, 15)
                    .Vertex(0, 105, 75, 0, 0, 0).Vertex(0, 150, 80, 15, 15, 15).Vertex(0, 195, 70, 0, 0, 0)
                    .Vertex(0, 115, 135, 0, 15, 15).Vertex(0, 150, 140, 15, 0, 15).Vertex(0, 185, 130, 15, 15, 0),
                    ("/VerticesPerRow", "3")))),
            nameof(ALatticeMeshOfValuesNarrowerThanAByte), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);

    private static Func<PdfDocument, PdfDictionary> NarrowMesh(int type, PackedMesh data,
        params (string Key, object Value)[] entries) =>
        document =>
        {
            var shading = HandWritten.Stream(document, data.ToString(), entries);
            shading.Elements["/ShadingType"] = new PdfLiteral(type.ToString());
            shading.Elements["/ColorSpace"] = new PdfLiteral("/DeviceRGB");
            shading.Elements["/BitsPerCoordinate"] = new PdfLiteral("12");
            shading.Elements["/BitsPerComponent"] = new PdfLiteral("4");
            shading.Elements["/Decode"] = new PdfLiteral("[0 4095 0 4095 0 1 0 1 0 1]");
            if (type == 4)
                shading.Elements["/BitsPerFlag"] = new PdfLiteral("2");
            return shading;
        };

    // A Coons patch with a second sharing its side, and a tensor-product patch whose inner
    // control points pull the colours towards one corner.
    [Test]
    public void PatchMeshes() =>
        Fidelity.Of(PageWith(
                "/Coons sh /Tensor sh",
                ("/Shading", "/Coons", Mesh(6, new MeshData()
                    .Flag(0)
                    .Point(10, 20).Point(0, 40).Point(20, 55).Point(10, 75)
                    .Point(30, 85).Point(60, 65).Point(80, 75)
                    .Point(90, 55).Point(70, 40).Point(80, 20)
                    .Point(60, 10).Point(30, 30)
                    .Color(255, 0, 0).Color(0, 255, 0).Color(0, 0, 255).Color(255, 255, 0)
                    .Flag(1)
                    .Point(100, 95).Point(70, 115).Point(80, 135)
                    .Point(60, 145).Point(30, 125).Point(10, 135)
                    .Point(20, 115).Point(0, 95)
                    .Color(255, 0, 255).Color(0, 255, 255), ("/Decode", Rgb16))),
                ("/Shading", "/Tensor", Mesh(7, new MeshData()
                    .Flag(0)
                    .Point(110, 20).Point(110, 60).Point(110, 100).Point(110, 140)
                    .Point(135, 140).Point(165, 140).Point(190, 140)
                    .Point(190, 100).Point(190, 60).Point(190, 20)
                    .Point(165, 20).Point(135, 20)
                    .Point(170, 110).Point(170, 120).Point(180, 120).Point(180, 110)
                    .Color(255, 0, 0).Color(0, 255, 0).Color(0, 0, 255).Color(255, 255, 0), ("/Decode", Rgb16)))),
            nameof(PatchMeshes), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.9, shareOverThreshold: 0.013);

    // What is blended across a triangle is the function's input, so its hard edges cross the
    // triangle as lines.
    [Test]
    public void ATriangleMeshWithAFunction() =>
        Fidelity.Of(PageWith(
                "/Sh0 sh",
                ("/Shading", "/Sh0", Mesh(4, new MeshData()
                    .Flag(0).Point(20, 20).Color(0)
                    .Flag(0).Point(180, 40).Color(255)
                    .Flag(0).Point(60, 135).Color(128), ("/Decode", "[0 65535 0 65535 0 1]"), ("/Function", ThreeBands)))),
            nameof(ATriangleMeshWithAFunction), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);

    // Every /Rotate matrix is symmetric, so only a cm that turns or skews content tells a matrix
    // from its transpose.
    [Test]
    public void ContentTurnedAndSkewedByCm() =>
        Fidelity.Of(HandWritten.Page(
                "q 0.866 0.5 -0.5 0.866 100 20 cm 1 0 0 rg 0 0 80 30 re f 4 w 0 0 1 RG 0 40 m 70 40 l S Q " +
                "q 1 0 0.4 1 10 90 cm 0 0.6 0 rg 0 0 50 40 re f Q"),
            nameof(ContentTurnedAndSkewedByCm), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);

    // An appearance whose matrix turns its box a quarter, mapped onto a rectangle that is neither
    // its size nor its shape, on a page that is itself turned.
    [Test]
    [Arguments(0)]
    [Arguments(90)]
    public void AnAnnotationAppearanceMappedOntoItsRectangle(int rotate) =>
        Fidelity.Of(HandWritten.Page("0.9 g 0 0 200 150 re f",
                page: (document, page) =>
                {
                    page.Rotate = rotate;
                    var form = new PdfDictionary(document);
                    document.Internals.AddObject(form);
                    form.Elements.SetName("/Type", "/XObject");
                    form.Elements.SetName("/Subtype", "/Form");
                    form.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(100, 40));
                    form.Elements.SetMatrix("/Matrix", new XMatrix(0, 1, -1, 0, 0, 0));
                    form.CreateStream(System.Text.Encoding.Latin1.GetBytes(
                        "1 0 0 rg 0 0 100 40 re f 0 0 1 rg 0 0 30 40 re f 4 w 0 g 0 0 m 100 40 l S"));

                    var annotation = new PdfDictionary(document);
                    document.Internals.AddObject(annotation);
                    annotation.Elements.SetName("/Subtype", "/Stamp");
                    annotation.Elements["/Rect"] = new PdfRectangle(new XPoint(40, 20), new XPoint(100, 130));
                    var appearance = new PdfDictionary(document);
                    appearance.Elements["/N"] = form.Reference;
                    annotation.Elements["/AP"] = appearance;
                    var annotations = new PdfArray(document);
                    annotations.Elements.Add(annotation.Reference);
                    page.Elements["/Annots"] = annotations;
                }),
            nameof(AnAnnotationAppearanceMappedOntoItsRectangle) + rotate, new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.8, shareOverThreshold: 0.005);

    // Ghostscript honours the default configuration, /D, unless told not to. The red square is in
    // marked content of the group that is off and the green one is a form of it, so each way a
    // page names a group is compared; the blue square is in the group that is on. Drawn all, the
    // page differs from Ghostscript's by the two squares.
    [Test]
    public void ContentOfAGroupTheDefaultConfigurationSwitchesOff()
    {
        var document = HandWritten.Page(
            "/OC /Off BDC 1 0 0 rg 10 10 80 60 re f EMC " +
            "/OC /On BDC 0 0 1 rg 110 10 80 60 re f EMC " +
            "/Fm1 Do",
            page: (document, page) =>
            {
                var off = new PdfDictionary(document);
                document.Internals.AddObject(off);
                off.Elements.SetName("/Type", "/OCG");
                off.Elements.SetString("/Name", "Off");
                var on = new PdfDictionary(document);
                document.Internals.AddObject(on);
                on.Elements.SetName("/Type", "/OCG");
                on.Elements.SetString("/Name", "On");

                var configuration = new PdfDictionary(document);
                configuration.Elements["/OFF"] = new PdfArray(document, off.Reference);
                var properties = new PdfDictionary(document);
                properties.Elements["/OCGs"] = new PdfArray(document, off.Reference, on.Reference);
                properties.Elements["/D"] = configuration;
                document.Internals.Catalog.Elements["/OCProperties"] = properties;

                var form = new PdfDictionary(document);
                document.Internals.AddObject(form);
                form.Elements.SetName("/Type", "/XObject");
                form.Elements.SetName("/Subtype", "/Form");
                form.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 150));
                form.Elements["/OC"] = off.Reference;
                form.CreateStream(System.Text.Encoding.Latin1.GetBytes("0 1 0 rg 10 80 80 60 re f"));

                var resources = new PdfDictionary(document);
                var properties2 = new PdfDictionary(document);
                properties2.Elements["/Off"] = off.Reference;
                properties2.Elements["/On"] = on.Reference;
                resources.Elements["/Properties"] = properties2;
                var xObjects = new PdfDictionary(document);
                xObjects.Elements["/Fm1"] = form.Reference;
                resources.Elements["/XObject"] = xObjects;
                page.Elements["/Resources"] = resources;
            });

        Fidelity.Of(document, nameof(ContentOfAGroupTheDefaultConfigurationSwitchesOff), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 0.5, shareOverThreshold: 0.001);
    }

    // What this library writes for its shape annotations, and so what a reader of its files sees. A field
    // of variable text is not here: its appearance is text, and this project registers no font resolver.
    [Test]
    public void ShapeAnnotationsThisLibraryWrote()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 150));
        page.Annotations.Add(new PdfPinata.Pdf.Annotations.PdfSquareAnnotation
        {
            Rectangle = new PdfRectangle(new XPoint(10, 80), new XPoint(90, 140)),
            Color = XColors.Red, BorderWidth = 4
        });
        page.Annotations.Add(new PdfPinata.Pdf.Annotations.PdfCircleAnnotation
        {
            Rectangle = new PdfRectangle(new XPoint(110, 80), new XPoint(190, 140)),
            Color = XColors.Blue, BorderWidth = 3
        });
        var line = new PdfPinata.Pdf.Annotations.PdfLineAnnotation { Color = XColors.Green, BorderWidth = 3 };
        line.SetLine(new XPoint(10, 70), new XPoint(190, 50));
        page.Annotations.Add(line);
        // Measured at 0.56 and 0.0049, all of it one pixel along the edges of strokes, where Ghostscript
        // snaps a stroke to the pixel grid and Skia anti-aliases it. Turning the annotations off
        // measures 13.9, so the limits sit at about twice the noise and a fifteenth of that.
        Fidelity.Of(document, nameof(ShapeAnnotationsThisLibraryWrote), new PageRenderOptions { Scale = 2 })
            .ShouldBeWithin(meanChannelDifference: 1.0, shareOverThreshold: 0.01);
    }

    // Red as Photoshop writes a CMYK JPEG: an Adobe marker and every ink stored inverted, with the
    // /Decode a PDF needs to invert them back. SkiaSharp's codec undoes the inversion itself, which
    // is the convention DecodeImage states. What remains is the conversion to RGB: Ghostscript's
    // goes through an ICC profile and draws (237, 28, 36), and the codec's is a formula and draws
    // (255, 0, 0), a mean of 27 and nothing over the threshold. Drawn the wrong way round, the page
    // would be near black to one of them and every pixel over it.
    [Test]
    public void AnAdobeCmykJpegWithTheDecodeThatInvertsIt()
    {
        var document = HandWritten.Page("q 40 0 0 40 0 0 cm /Im1 Do Q", 40, 40, (document, page) =>
            HandWritten.Image(document, page, "/Im1", Asset("adobe-cmyk-red.jpg"), image =>
            {
                image.Elements.SetInteger("/Width", 8);
                image.Elements.SetInteger("/Height", 8);
                image.Elements.SetName("/ColorSpace", "/DeviceCMYK");
                image.Elements.SetInteger("/BitsPerComponent", 8);
                image.Elements.SetName("/Filter", "/DCTDecode");
                image.Elements["/Decode"] = new PdfArray(document, new PdfInteger(1), new PdfInteger(0),
                    new PdfInteger(1), new PdfInteger(0), new PdfInteger(1), new PdfInteger(0),
                    new PdfInteger(1), new PdfInteger(0));
            }));

        Fidelity.Of(document, nameof(AnAdobeCmykJpegWithTheDecodeThatInvertsIt))
            .ShouldBeWithin(meanChannelDifference: 28, shareOverThreshold: 0);
    }

    // The four JPEGs have four components and no Adobe marker, so their samples are the inks as
    // they are, which is what a PDF reads when it has no /Decode. They were made by taking the
    // marker out of a file Photoshop-style, so the inks are strange ones, but Ghostscript and the
    // device read the same bytes. SkiaSharp alone inverts every one, and draws red as (179, 171, 0)
    // where Ghostscript draws (0, 0, 11).
    [Test]
    [Arguments("red")]
    [Arguments("cyan")]
    [Arguments("black")]
    [Arguments("white")]
    public void AMarkerlessCmykJpegWithNoDecode(string name)
    {
        var document = HandWritten.Page("q 40 0 0 40 0 0 cm /Im1 Do Q", 40, 40, (document, page) =>
            HandWritten.Image(document, page, "/Im1", Asset($"cmyk-{name}-nomarker.jpg"), image =>
            {
                image.Elements.SetInteger("/Width", 8);
                image.Elements.SetInteger("/Height", 8);
                image.Elements.SetName("/ColorSpace", "/DeviceCMYK");
                image.Elements.SetInteger("/BitsPerComponent", 8);
                image.Elements.SetName("/Filter", "/DCTDecode");
            }));

        Fidelity.Of(document, nameof(AMarkerlessCmykJpegWithNoDecode) + "-" + name)
            .ShouldBeWithin(meanChannelDifference: 30, shareOverThreshold: 0);
    }

    // The PDF ImageMagick writes for a page scanned at 150 dots to the inch: one Group 4 image
    // filling the page, drawn here a pixel of the page to a pixel of the scan. Both draw every
    // pixel the same, measured at 0 and 0, so any difference at all is a pixel decoded wrongly.
    [Test]
    public void AGroup4ScanImageMagickWrote()
    {
        using var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(Asset("group4-scan.pdf")),
            PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);

        Fidelity.Of(document, nameof(AGroup4ScanImageMagickWrote), new PageRenderOptions { Scale = 150 / 72.0 })
            .ShouldBeWithin(meanChannelDifference: 0, shareOverThreshold: 0);
    }

    // /BlackIs1 makes the filter answer 1 for black, and a /DeviceGray image with no /Decode draws
    // 1 as white, so the text is drawn white on black. Ghostscript draws it so too.
    [Test]
    public void AGroup4ImageWithBlackIs1AndNoDecodeIsDrawnInverted()
    {
        var document = HandWritten.Page("q 301 0 0 61 0 0 cm /Im1 Do Q", 301, 61, (document, page) =>
            HandWritten.Image(document, page, "/Im1", Asset("g4-blackis1.bin"), image =>
            {
                image.Elements.SetInteger("/Width", 301);
                image.Elements.SetInteger("/Height", 61);
                image.Elements.SetName("/ColorSpace", "/DeviceGray");
                image.Elements.SetInteger("/BitsPerComponent", 1);
                image.Elements.SetName("/Filter", "/CCITTFaxDecode");
                var parameters = new PdfDictionary(document);
                parameters.Elements.SetInteger("/K", -1);
                parameters.Elements.SetInteger("/Columns", 301);
                parameters.Elements.SetInteger("/Rows", 61);
                parameters.Elements.SetBoolean("/BlackIs1", true);
                image.Elements["/DecodeParms"] = parameters;
            }));

        Fidelity.Of(document, nameof(AGroup4ImageWithBlackIs1AndNoDecodeIsDrawnInverted))
            .ShouldBeWithin(meanChannelDifference: 0, shareOverThreshold: 0);
    }

    private static byte[] Asset(string name)
    {
        using var stream = typeof(GhostscriptFidelityTests).Assembly.GetManifestResourceStream("Assets/" + name)
                           ?? throw new InvalidOperationException("No embedded asset " + name + ".");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
