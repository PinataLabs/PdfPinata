using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Shading types 1 and 4 to 7, which all reach a device as triangles with a colour at each corner.
/// </summary>
/// <remarks>
/// Mesh data here has flags and colour components of 8 bits and coordinates of 16, under a
/// <c>/Decode</c> that makes a coordinate the number written.
/// </remarks>
public class MeshShadingTests
{
    private const string Rgb = "[0 65535 0 65535 0 1 0 1 0 1]";

    private static byte[] Point(int x, int y) => [(byte)(x >> 8), (byte)x, (byte)(y >> 8), (byte)y];

    /// <summary>A vertex of a triangle mesh: its flag where the type has one, its place and its colour.</summary>
    private static byte[] Vertex(int? flag, int x, int y, params byte[] color) =>
        [.. flag == null ? Array.Empty<byte>() : [(byte)flag.Value], .. Point(x, y), .. color];

    private static Task<Drawn> Mesh(int type, IEnumerable<byte> data, params (string Key, object Value)[] more) =>
        Interruptibly.Run(() => Drawn.Saved("/Sh0 sh", (document, resources) =>
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Stream(document, data.ToArray(),
            [
                ("/ShadingType", type.ToString()), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerCoordinate", "16"),
                ("/BitsPerComponent", "8"), ("/BitsPerFlag", "8"), ("/Decode", Rgb), .. more
            ]))));

    private static PageMeshShading MeshOf(Drawn drawn) => drawn.Device.Fills.Single().ShaderAs<PageMeshShading>();

    /// <summary>A mesh's triangles, each as its three corners and their colours.</summary>
    private static string[] TrianglesOf(PageMeshShading mesh)
    {
        var triangles = new string[mesh.Points.Length / 3];
        for (var index = 0; index < triangles.Length; index++)
        {
            triangles[index] = string.Join(" ", Enumerable.Range(3 * index, 3).Select(corner =>
                string.Create(CultureInfo.InvariantCulture, $"{mesh.Points[corner].X:0.#},{mesh.Points[corner].Y:0.#}#") +
                $"{mesh.Colors[3 * corner]:X2}{mesh.Colors[3 * corner + 1]:X2}{mesh.Colors[3 * corner + 2]:X2}"));
        }

        return triangles;
    }

    [Test, Timeout(30_000)]
    public async Task AFreeFormMeshJoinsEachVertexToTheTriangleBeforeItByItsFlag()
    {
        var drawn = await Mesh(4,
        [
            .. Vertex(0, 10, 10, 255, 0, 0), .. Vertex(0, 50, 10, 0, 255, 0), .. Vertex(0, 10, 50, 0, 0, 255),
            .. Vertex(1, 50, 50, 255, 255, 255),
            .. Vertex(2, 90, 30, 0, 0, 0)
        ]);

        var mesh = MeshOf(drawn);
        TrianglesOf(mesh).Should().Equal(
            "10,10#FF0000 50,10#00FF00 10,50#0000FF",
            "50,10#00FF00 10,50#0000FF 50,50#FFFFFF",
            "50,10#00FF00 50,50#FFFFFF 90,30#000000");
        mesh.Bounds.Should().Be(new XRect(10, 10, 80, 40));
        mesh.IsShared.Should().BeTrue();
        drawn.Device.Fills.Single().ToString().Should().Be("fill nonzero M0,100 L200,100 L200,0 L0,0 Z mesh");
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task ALatticeMeshJoinsEachRowToTheNext()
    {
        var drawn = await Mesh(5,
        [
            .. Vertex(null, 0, 0, 255, 0, 0), .. Vertex(null, 10, 0, 255, 0, 0), .. Vertex(null, 20, 0, 255, 0, 0),
            .. Vertex(null, 0, 10, 0, 0, 255), .. Vertex(null, 10, 10, 0, 0, 255), .. Vertex(null, 20, 10, 0, 0, 255)
        ], ("/VerticesPerRow", "3"));

        TrianglesOf(MeshOf(drawn)).Should().Equal(
            "0,0#FF0000 10,0#FF0000 0,10#0000FF",
            "10,0#FF0000 10,10#0000FF 0,10#0000FF",
            "10,0#FF0000 20,0#FF0000 10,10#0000FF",
            "20,0#FF0000 20,10#0000FF 10,10#0000FF");
    }

    // A square patch from (0, 0) to (30, 30) whose sides are straight, their control points a
    // third and two thirds of the way along. Round from the first side: up the left, along the
    // top, down the right, back along the bottom.
    private static readonly byte[] SquarePatch =
    [
        .. Point(0, 0), .. Point(0, 10), .. Point(0, 20), .. Point(0, 30),
        .. Point(10, 30), .. Point(20, 30), .. Point(30, 30),
        .. Point(30, 20), .. Point(30, 10), .. Point(30, 0),
        .. Point(20, 0), .. Point(10, 0)
    ];

    private static readonly byte[] FourColours = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0];

    [Test, Timeout(30_000)]
    public async Task ACoonsPatchIsCutIntoAGridOfTrianglesBlendedBetweenItsCorners()
    {
        var drawn = await Mesh(6, [0, .. SquarePatch, .. FourColours]);

        var mesh = MeshOf(drawn);
        var triangles = TrianglesOf(mesh);
        triangles.Should().HaveCount(2 * 8 * 8);
        mesh.Bounds.Should().Be(new XRect(0, 0, 30, 30));

        // Straight sides make a flat patch, so the grid is square. The first cell is at the
        // first corner, in its colour, and the last at the corner opposite.
        triangles[0].Should().StartWith("0,0#FF0000 ");
        triangles[^1].Should().Contain("30,30#0000FF");
        triangles.Should().Contain(triangle => triangle.Contains("15,15#808040"), "the middle is the mean of the four corners");
    }

    [Test, Timeout(30_000)]
    public async Task APatchThatSharesASideTakesItAndItsColoursFromThePatchBefore()
    {
        // The second patch states eight points and two colours, and its first side is the first's top.
        var drawn = await Mesh(6,
        [
            0, .. SquarePatch, .. FourColours,
            1, .. Point(30, 40), .. Point(30, 50), .. Point(30, 60), .. Point(20, 60), .. Point(10, 60), .. Point(0, 60),
            .. Point(0, 50), .. Point(0, 40), 0, 0, 0, 255, 255, 255
        ]);

        var mesh = MeshOf(drawn);
        var second = TrianglesOf(mesh).Skip(128).ToArray();
        second.Should().HaveCount(128);
        mesh.Bounds.Should().Be(new XRect(0, 0, 30, 60));
        second[0].Should().StartWith("0,30#00FF00 ", "the side shared begins at the first patch's second corner");
        second.Should().Contain(triangle => triangle.Contains("30,30#0000FF"));
        second.Should().Contain(triangle => triangle.Contains("30,60#000000"));
        second.Should().Contain(triangle => triangle.Contains("0,60#FFFFFF"));
    }

    [Test, Timeout(30_000)]
    public async Task ATensorPatchIsPulledByItsInnerControlPoints()
    {
        byte[] inner = [.. Point(25, 25), .. Point(25, 25), .. Point(25, 25), .. Point(25, 25)];
        var tensor = MeshOf(await Mesh(7, [0, .. SquarePatch, .. inner, .. FourColours]));
        var coons = MeshOf(await Mesh(6, [0, .. SquarePatch, .. FourColours]));

        // The same sides and colours, but the middle of the surface lies nearer the far corner.
        TrianglesOf(tensor).Should().HaveCount(128);
        TrianglesOf(tensor).Should().NotContain(triangle => triangle.Contains("15,15#"));
        TrianglesOf(tensor).Should().Contain(triangle => triangle.Contains("20.6,20.6#808040"));
        TrianglesOf(coons).Should().Contain(triangle => triangle.Contains("15,15#808040"));
    }

    [Test, Timeout(30_000)]
    public async Task AFunctionBasedShadingIsAGridOverItsDomainUnderItsMatrix()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Saved("/Sh0 sh", (document, resources) =>
            Drawn.Add(document, resources, "/Shading", "/Sh0", Drawn.Object(document,
                ("/ShadingType", "1"), ("/ColorSpace", "/DeviceRGB"), ("/Domain", "[0 2 0 4]"),
                ("/Matrix", "[10 0 0 10 5 5]"),
                ("/Function", Drawn.Stream(document, "{ 4 div exch 2 div exch 0 }",
                    ("/FunctionType", "4"), ("/Domain", "[0 2 0 4]"), ("/Range", "[0 1 0 1 0 1]")))))));

        var mesh = MeshOf(drawn);
        var triangles = TrianglesOf(mesh);
        triangles.Should().HaveCount(2 * 64 * 64);
        mesh.Bounds.Should().Be(new XRect(5, 5, 20, 40));
        triangles[0].Should().StartWith("5,5#000000 ");
        triangles[^1].Should().Contain("25,45#FFFF00");
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(30_000)]
    public async Task AMeshWithAFunctionIsCutAlongTheFunctionsHardEdges()
    {
        // The vertices carry the function's input, from 0 on the left to 1 on the right, and the
        // function is red below a half and blue above.
        var drawn = await Mesh(4,
            [.. Vertex(0, 0, 0, 0), .. Vertex(0, 100, 0, 255), .. Vertex(0, 0, 100, 0)],
            ("/Decode", "[0 65535 0 65535 0 1]"),
            ("/Function", "<< /FunctionType 3 /Domain [0 1] /Bounds [0.5] /Encode [0 1 0 1] /Functions [" +
                          "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [1 0 0] /N 1 >> " +
                          "<< /FunctionType 2 /Domain [0 1] /C0 [0 0 1] /C1 [0 0 1] /N 1 >>] >>"));

        var triangles = TrianglesOf(MeshOf(drawn));
        triangles.Should().OnlyContain(triangle => ColoursIn(triangle).Length == 1, "no triangle runs from one colour to the other");
        triangles.Where(triangle => triangle.Contains("#FF0000")).Should().OnlyContain(triangle => Xs(triangle).Max() <= 50.001);
        triangles.Where(triangle => triangle.Contains("#0000FF")).Should().OnlyContain(triangle => Xs(triangle).Min() >= 49.999);
        triangles.SelectMany(ColoursIn).Distinct().Should().BeEquivalentTo("FF0000", "0000FF");
    }

    private static string[] ColoursIn(string triangle) =>
        triangle.Split(' ').Select(corner => corner.Split('#')[1]).Distinct().ToArray();

    private static double[] Xs(string triangle) =>
        triangle.Split(' ').Select(corner => double.Parse(corner.Split(',')[0], CultureInfo.InvariantCulture)).ToArray();

    [Test, Timeout(30_000)]
    public async Task AMeshWhoseDataEndsPartWayThroughAVertexIsDrawnFromWhatCameBefore()
    {
        var drawn = await Mesh(4,
        [
            .. Vertex(0, 10, 10, 255, 0, 0), .. Vertex(0, 50, 10, 0, 255, 0), .. Vertex(0, 10, 50, 0, 0, 255),
            1, 0, 50
        ]);

        TrianglesOf(MeshOf(drawn)).Should().HaveCount(1);
        drawn.Problems.Should().BeEmpty();
    }

    [Test, Timeout(120_000)]
    public async Task AMeshOfMoreTrianglesThanTheLimitIsCutShortAndSaysSo()
    {
        // Three vertices, then three hundred thousand more that each make a triangle with the two before.
        var data = new List<byte>(Vertex(0, 0, 0, 0, 0, 0).Concat(Vertex(0, 1, 0, 0, 0, 0)).Concat(Vertex(0, 0, 1, 0, 0, 0)));
        var next = Vertex(1, 1, 1, 255, 255, 255);
        for (var index = 0; index < 300_000; index++)
            data.AddRange(next);

        var drawn = await Mesh(4, data);

        MeshOf(drawn).Points.Length.Should().Be(3 * (1 << 18));
        drawn.Problems.Should().Equal("LimitReached /Sh0");
    }

    [Test, Timeout(30_000)]
    public async Task ALatticeWhoseRowsAreLongerThanItsDataIsInvalid()
    {
        var drawn = await Mesh(5, Vertex(null, 0, 0, 255, 0, 0), ("/VerticesPerRow", "2000000000"));

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }

    [Test, Timeout(30_000)]
    [Arguments("/BitsPerCoordinate", "7")]
    [Arguments("/BitsPerComponent", "32")]
    [Arguments("/BitsPerFlag", "1")]
    [Arguments("/Decode", "[0 1 0 1]")]
    [Arguments("/ColorSpace", "/DeviceCMYK")]
    public async Task AMeshWhoseLayoutIsNotOneTheStandardAllowsIsInvalid(string key, string value)
    {
        var drawn = await Mesh(4, Vertex(0, 10, 10, 255, 0, 0), (key, value));

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Sh0");
    }
}
