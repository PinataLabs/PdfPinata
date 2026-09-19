using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
/// PdfPinata has always been able to draw a bold or an italic that a family ships no file
/// for - stroking the glyphs, and skewing them - and the shipped resolver never once asked it
/// to. A family with only a regular face rendered bold text as regular text, silently.
/// </summary>
/// <remarks>
/// The resolver is driven with an explicit list of files rather than the machine's font
/// directory, so a family here ships exactly the faces the case under test needs.
/// </remarks>
public class StyleSimulationTest
{
    private const string Family = "Liberation Sans";

    private static readonly string Regular = Face("Regular");
    private static readonly string Bold = Face("Bold");
    private static readonly string Italic = Face("Italic");
    private static readonly string BoldItalic = Face("BoldItalic");

    [Theory]
    // A family shipping every face simulates nothing.
    [InlineData(new[] { "Regular", "Bold", "Italic", "BoldItalic" }, false, false, "Regular", XStyleSimulations.None)]
    [InlineData(new[] { "Regular", "Bold", "Italic", "BoldItalic" }, true, false, "Bold", XStyleSimulations.None)]
    [InlineData(new[] { "Regular", "Bold", "Italic", "BoldItalic" }, false, true, "Italic", XStyleSimulations.None)]
    [InlineData(new[] { "Regular", "Bold", "Italic", "BoldItalic" }, true, true, "BoldItalic", XStyleSimulations.None)]
    // A family with a regular face alone has each missing axis drawn on.
    [InlineData(new[] { "Regular" }, true, false, "Regular", XStyleSimulations.BoldSimulation)]
    [InlineData(new[] { "Regular" }, false, true, "Regular", XStyleSimulations.ItalicSimulation)]
    [InlineData(new[] { "Regular" }, true, true, "Regular", XStyleSimulations.BoldItalicSimulation)]
    // Only the missing axis: a real bold with a drawn-on slant beats simulating both.
    [InlineData(new[] { "Regular", "Bold" }, true, true, "Bold", XStyleSimulations.ItalicSimulation)]
    [InlineData(new[] { "Regular", "Italic" }, true, true, "Italic", XStyleSimulations.BoldSimulation)]
    // Weight and slant cannot be taken away, so a plainer request than the family ships gets
    // the nearest face and no simulation at all.
    [InlineData(new[] { "Bold" }, false, false, "Bold", XStyleSimulations.None)]
    [InlineData(new[] { "BoldItalic" }, false, false, "BoldItalic", XStyleSimulations.None)]
    [InlineData(new[] { "Italic" }, true, false, "Italic", XStyleSimulations.BoldSimulation)]
    public void TheNearestFaceIsUsedAndOnlyTheMissingAxisIsSimulated(
        string[] shipped, bool isBold, bool isItalic, string expectedFace, XStyleSimulations expected)
    {
        var resolver = new Probe();
        resolver.SetupFontsFiles(shipped.Select(Face).ToArray());

        var info = resolver.ResolveTypeface(Family, isBold, isItalic);

        info.FaceName.Should().Be("LiberationSans-" + expectedFace + ".ttf");
        info.StyleSimulations.Should().Be(expected);
    }

    /// <summary>
    /// A family shipping one file used to be filed under Regular whatever that file was, so a
    /// bold-only family answered a request for bold with a face it thought was regular. Now
    /// that the missing weight is drawn on, that would have stroked a bold face bolder still.
    /// </summary>
    [Fact]
    public void ASingleFaceIsFiledUnderTheStyleItActuallyIs()
    {
        var resolver = new Probe();
        resolver.SetupFontsFiles(new[] { Bold });

        var info = resolver.ResolveTypeface(Family, true, false);

        info.FaceName.Should().Be("LiberationSans-Bold.ttf");
        info.StyleSimulations.Should().Be(XStyleSimulations.None,
            "the family ships a real bold, so there is nothing to draw on");
    }

    /// <summary>
    /// The flags are of no use if they stop at the resolver, and they used to: the typeface was
    /// built by a constructor that took no simulations, so what the resolver asked for was
    /// dropped before the renderer could read it. Both tests below would pass on a face that
    /// really was bold, so they use the one family the document resolver ships a single regular
    /// face for - the same file answers both requests, and only the simulation differs.
    /// </summary>
    [Fact]
    public void ASimulatedBoldIsStrokedAsWellAsFilledOnThePage()
    {
        // Text render mode 2, fill then stroke, is how the weight is drawn on.
        RenderModesOf(XFontStyle.Bold).Should().Contain(2);
        RenderModesOf(XFontStyle.Regular).Should().NotContain(2);
    }

    [Fact]
    public void ASimulatedItalicIsSkewedOnThePage()
    {
        // The slant is drawn on by shearing the text matrix 20° to the right, which is the
        // third of the six operands of Tm. An upright face is positioned with Td and sets no
        // text matrix at all, so it shears nothing.
        ShearsOf(XFontStyle.Italic).Should().Contain(shear => shear > 0);
        ShearsOf(XFontStyle.Regular).Should().NotContain(shear => shear > 0);
    }

    [Fact]
    public void ASimulatedBoldMeasuresWiderThanTheFaceItIsDrawnOver()
    {
        const string text = "The quick brown fox";

        var regular = new XFont(PinnedFontResolver.CffFamilyName, 12, XFontStyle.Regular);
        var bold = new XFont(PinnedFontResolver.CffFamilyName, 12, XFontStyle.Bold);

        using var gfx = XGraphics.CreateMeasureContext(
            new XSize(600, 800), XGraphicsUnit.Point, XPageDirection.Downwards);

        // Layout has to agree with what is drawn: the stroke widens every glyph, so a line of
        // simulated bold wraps sooner than the same line of the face it is drawn over.
        gfx.MeasureString(text, bold).Width.Should()
            .BeGreaterThan(gfx.MeasureString(text, regular).Width);
    }

    /// <summary>
    /// The operand of every Tr in the page's content, in the order it is set.
    /// </summary>
    private static int[] RenderModesOf(XFontStyle style)
    {
        return OperatorsOf(style)
            .Where(op => op.OpCode.OpCodeName == OpCodeName.Tr)
            .Select(op => (int)((CInteger)op.Operands[0]).Value)
            .ToArray();
    }

    /// <summary>
    /// The horizontal shear of every text matrix the page sets, in the order it sets them.
    /// </summary>
    private static double[] ShearsOf(XFontStyle style)
    {
        return OperatorsOf(style)
            .Where(op => op.OpCode.OpCodeName == OpCodeName.Tm)
            .Select(op => op.Operands[2] is CReal real ? real.Value : ((CInteger)op.Operands[2]).Value)
            .ToArray();
    }

    /// <summary>
    /// Everything a page drawn in the given style asks the viewer to do, read back off the
    /// saved file rather than out of the document that wrote it.
    /// </summary>
    private static COperator[] OperatorsOf(XFontStyle style)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont(PinnedFontResolver.CffFamilyName, 12, style);
            gfx.DrawString("Simulation check", font, XBrushes.Black, new XPoint(20, 40));
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        var reread = Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];

        return ContentReader.ReadContent(ContentOf(reread)).OfType<COperator>().ToArray();
    }

    private static byte[] ContentOf(PdfPage page)
    {
        var item = page.Elements["/Contents"];
        if (item is PdfReference reference)
            item = reference.Value;

        return ((PdfDictionary)item).Stream.UnfilteredValue;
    }

    private sealed class Probe : SkiaFontResolver
    {
        public Probe()
        {
            // Nothing is installed but what the test hands over, so an unknown family must say
            // so rather than answer with the first face it happens to hold.
            NullIfFontNotFound = true;
        }
    }

    private static string Face(string style)
    {
        return PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-" + style + ".ttf");
    }
}
