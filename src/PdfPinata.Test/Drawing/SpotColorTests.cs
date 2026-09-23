using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   Spot colours (empira/PDFsharp#201): a named colorant painted through <see cref="XGraphics"/>
///   is written as a <c>/Separation</c> colour space with a Type 2 tint transform, selected with
///   <c>cs</c>/<c>CS</c> and set with <c>scn</c>/<c>SCN</c>. Everything here is read back out of a
///   saved and reopened file, because what matters is what a RIP would find there.
/// </summary>
public class SpotColorTests
{
    private static readonly XSpotColor Pantone185 = new("PANTONE 185 C", XColor.FromCmyk(0, 0.93, 0.79, 0));
    private static readonly XSpotColor WhiteInk = new("White", XColor.FromArgb(240, 240, 240));

    // ----- what is written -----------------------------------------------------------------------

    [Fact]
    public void AFillSelectsTheSeparationSpaceAndSetsTheTint()
    {
        var page = Reopened(gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185, 0.5)), 10, 10, 100, 100))[0];

        Content(page).Should().MatchRegex(@"/CS0 cs\s+0\.5 scn\b");
    }

    [Fact]
    public void AStrokeSelectsTheSeparationSpaceWithTheStrokingOperators()
    {
        var page = Reopened(gfx =>
            gfx.DrawLine(new XPen(XColor.FromSpot(Pantone185), 3), 10, 10, 200, 200))[0];

        var content = Content(page);
        content.Should().MatchRegex(@"/CS0 CS\s+1 SCN\b");
        content.Should().NotMatchRegex(@"\bRG\b", "the line is stroked in the ink, not in its alternate");
    }

    [Fact]
    public void TheColourSpaceIsASeparationWithAType2TintTransformFromWhiteToTheAlternate()
    {
        var page = Reopened(gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185)), 10, 10, 100, 100))[0];

        var space = ColorSpace(page, "/CS0");
        space.Elements.GetName(0).Should().Be("/Separation");
        space.Elements.GetName(1).Should().Be("/PANTONE 185 C", "the writer escapes the spaces and the reader takes them back");
        space.Elements.GetName(2).Should().Be("/DeviceCMYK");

        var function = (PdfDictionary)Resolve(space.Elements[3]);
        function.Elements.GetInteger("/FunctionType").Should().Be(2);
        function.Elements.GetReal("/N").Should().Be(1);
        Numbers(function.Elements.GetArray("/Domain")).Should().Equal(0, 1);
        Numbers(function.Elements.GetArray("/C0")).Should().Equal(0, 0, 0, 0);
        Numbers(function.Elements.GetArray("/C1")).Should().Equal(0, 0.93, 0.79, 0);
    }

    [Fact]
    public void AnRgbAlternateFadesToRgbWhite()
    {
        var page = Reopened(gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(WhiteInk)), 10, 10, 100, 100))[0];

        var space = ColorSpace(page, "/CS0");
        space.Elements.GetName(2).Should().Be("/DeviceRGB");
        var function = (PdfDictionary)Resolve(space.Elements[3]);
        Numbers(function.Elements.GetArray("/C0")).Should().Equal(1, 1, 1);
        Numbers(function.Elements.GetArray("/C1")).Should().Equal(0.9412, 0.9412, 0.9412);
    }

    [Fact]
    public void AGreyAlternateIsWrittenInDeviceGray()
    {
        var varnish = new XSpotColor("Varnish", XColor.FromGrayScale(0.25));
        var page = Reopened(gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(varnish)), 10, 10, 100, 100))[0];

        var space = ColorSpace(page, "/CS0");
        space.Elements.GetName(2).Should().Be("/DeviceGray");
        var function = (PdfDictionary)Resolve(space.Elements[3]);
        Numbers(function.Elements.GetArray("/C0")).Should().Equal(1);
        Numbers(function.Elements.GetArray("/C1")).Should().Equal(0.25);
    }

    [Fact]
    public void AnRgbColourDeclaredGreyIsNotWrittenInverted()
    {
        // An RGB colour's GS is how dark it is - black carries 1 - while the tint transform reads
        // a grey alternate as how light it is. Taken as it stood, this black alternate was white.
        var black = XColor.FromArgb(0, 0, 0);
        black.ColorSpace = XColorSpace.GrayScale;
        var ink = new XSpotColor("Black ink", black);

        var page = Reopened(gfx =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(ink)), 10, 10, 100, 100))[0];

        var function = (PdfDictionary)Resolve(ColorSpace(page, "/CS0").Elements[3]);
        Numbers(function.Elements.GetArray("/C1")).Should().Equal(0);
        XColor.FromSpot(ink).R.Should().Be(0);
    }

    [Fact]
    public void ANameOutsideAsciiIsWrittenAsUtf8()
    {
        var ink = new XSpotColor("Weiß", XColor.FromCmyk(0, 0, 0, 0.1));

        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(ink)), 10, 10, 100, 100);

        using var stream = new MemoryStream();
        document.Save(stream, false);

        // ß is C3 9F in UTF-8, and a name writes every byte outside '!'..'~' as #xx.
        Encoding.ASCII.GetString(stream.ToArray()).Should().Contain("/Wei#C3#9F");
    }

    [Fact]
    public void TextIsPaintedInTheInk()
    {
        var page = Reopened(gfx =>
            gfx.DrawString("Spot", new XFont("Arial", 20), new XSolidBrush(XColor.FromSpot(Pantone185, 0.8)), 50, 50))[0];

        Content(page).Should().MatchRegex(@"/CS0 cs\s+0\.8 scn\b[\s\S]*\bTj\b");
    }

    [Fact]
    public void APenMadeFromASpotBrushStrokesInTheInk()
    {
        var page = Reopened(gfx =>
            gfx.DrawLine(new XPen(new XSolidBrush(XColor.FromSpot(Pantone185, 0.25)), 2), 10, 10, 200, 10))[0];

        Content(page).Should().MatchRegex(@"/CS0 CS\s+0\.25 SCN\b");
    }

    [Fact]
    public void ADrawingInAFormNamesTheColourSpaceInTheFormsOwnResources()
    {
        var document = new PdfDocument();
        var form = new XForm(document, 100, 100);
        using (var formGfx = XGraphics.FromForm(form))
            formGfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185)), 0, 0, 50, 50);

        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawImage(form, 10, 10);

        var page = Reopen(document)[0];
        var xObjects = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/XObject")!;
        PdfDictionary formXObject = null;
        foreach (var key in xObjects.Elements.Keys)
            formXObject = (PdfDictionary)Resolve(xObjects.Elements[key]);

        var formSpaces = formXObject!.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/ColorSpace");
        formSpaces.Should().NotBeNull();
        ((PdfArray)Resolve(formSpaces!.Elements["/CS0"])).Elements.GetName(0).Should().Be("/Separation");
    }

    // ----- sharing and reselecting ----------------------------------------------------------------

    [Fact]
    public void EveryPageAndEveryEqualDefinitionShareOneColourSpaceObject()
    {
        var sameInkAgain = new XSpotColor("PANTONE 185 C", XColor.FromCmyk(0, 0.93, 0.79, 0));

        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185)), 10, 10, 100, 100);
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(sameInkAgain, 0.3)), 10, 10, 100, 100);

        var pages = Reopen(document);
        var first = SpaceReference(pages[0], "/CS0");
        var second = SpaceReference(pages[1], "/CS0");

        first.ObjectNumber.Should().Be(second.ObjectNumber);
    }

    [Fact]
    public void TwoDefinitionsOfOneNameThatDisagreeAreRefused()
    {
        var impostor = new XSpotColor("PANTONE 185 C", XColor.FromCmyk(0.5, 0, 0, 0));

        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185)), 10, 10, 100, 100);

        var drawing = () => gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(impostor)), 10, 120, 100, 100);

        drawing.Should().Throw<InvalidOperationException>().WithMessage("*PANTONE 185 C*one plate*");
    }

    [Fact]
    public void ChangingOnlyTheTintDoesNotSelectTheSpaceAgain()
    {
        var page = Reopened(gfx =>
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185)), 10, 10, 100, 100);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185, 0.4)), 10, 120, 100, 100);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(Pantone185, 0.4)), 10, 230, 100, 100);
        })[0];

        var content = Content(page);
        Regex.Matches(content, @"\bcs\b").Count.Should().Be(1);
        Regex.Matches(content, @"\bscn\b").Count.Should().Be(2, "the third rectangle is the tint already set");
    }

    [Fact]
    public void AProcessFillAfterASpotSelectsTheDeviceSpaceAgainEvenWhenTheNumbersMatch()
    {
        // The spot's alternate at full tint is exactly red, so its RGB components match the red
        // that follows - which must still be written, or the red is painted in the ink.
        var red = new XSpotColor("Red ink", XColor.FromArgb(255, 0, 0));
        var page = Reopened(gfx =>
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(red)), 10, 10, 100, 100);
            gfx.DrawRectangle(XBrushes.Red, 10, 120, 100, 100);
        })[0];

        Content(page).Should().MatchRegex(@"scn\b[\s\S]*1 0 0 rg\b");
    }

    [Fact]
    public void AProcessStrokeAfterASpotSelectsTheDeviceSpaceAgainEvenWhenTheNumbersMatch()
    {
        var red = new XSpotColor("Red ink", XColor.FromArgb(255, 0, 0));
        var page = Reopened(gfx =>
        {
            gfx.DrawLine(new XPen(XColor.FromSpot(red), 2), 10, 10, 200, 10);
            gfx.DrawLine(new XPen(XColors.Red, 2), 10, 20, 200, 20);
        })[0];

        Content(page).Should().MatchRegex(@"SCN\b[\s\S]*1 0 0 RG\b");
    }

    [Fact]
    public void ACmykProcessFillAfterACmykSpotSelectsDeviceCmykAgain()
    {
        var ink = new XSpotColor("Process-looking ink", XColor.FromCmyk(0, 0, 0, 1));
        var document = new PdfDocument();
        document.Options.ColorMode = PdfColorMode.Cmyk;
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(ink)), 10, 10, 100, 100);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromCmyk(0, 0, 0, 1)), 10, 120, 100, 100);
        }

        Content(Reopen(document)[0]).Should().MatchRegex(@"scn\b[\s\S]*0 0 0 1 k\b");
    }

    [Fact]
    public void AnAlphaIsAppliedThroughAGraphicsStateAsForAnyOtherColour()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(0.5, Pantone185, 1)), 10, 10, 100, 100);

        var page = Reopen(document)[0];
        var content = Content(page);
        var gs = Regex.Match(content, @"(/\S+)\s+gs\b").Groups[1].Value;
        var state = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/ExtGState")!.Elements.GetDictionary(gs)!;

        state.Elements.GetReal("/ca").Should().Be(0.5);
        content.Should().MatchRegex(@"/CS0 cs\s+1 scn\b");
    }

    // ----- the colour value itself ----------------------------------------------------------------

    [Fact]
    public void ASpotColoursProcessComponentsAreTheAlternateAtItsTint()
    {
        var half = XColor.FromSpot(Pantone185, 0.5);

        half.Spot.Should().BeSameAs(Pantone185);
        half.Tint.Should().Be(0.5);
        half.ColorSpace.Should().Be(XColorSpace.Cmyk);
        half.M.Should().BeApproximately(0.465, 1e-6);
        half.Y.Should().BeApproximately(0.395, 1e-6);
        half.A.Should().Be(1);

        var paleWhite = XColor.FromSpot(WhiteInk, 0.5);
        paleWhite.R.Should().Be(248, "halfway from paper white to 240");
    }

    [Fact]
    public void SettingAComponentMakesAnOrdinaryProcessColourButSettingAlphaDoesNot()
    {
        var spot = XColor.FromSpot(Pantone185);

        var translucent = spot;
        translucent.A = 0.3;
        translucent.Spot.Should().BeSameAs(Pantone185);

        var changed = spot;
        changed.C = 0.2;
        changed.Spot.Should().BeNull();
        changed.Tint.Should().Be(0);
    }

    [Fact]
    public void ASpotColourIsNotEqualToTheProcessColourItFallsBackOn()
    {
        var spot = XColor.FromSpot(Pantone185);
        var process = XColor.FromCmyk(0, 0.93, 0.79, 0);

        (spot == process).Should().BeFalse();
        spot.Should().Be(XColor.FromSpot(new XSpotColor("PANTONE 185 C", process)));
        spot.Should().NotBe(XColor.FromSpot(Pantone185, 0.5));
    }

    [Fact]
    public void ATintIsClampedAndMustBeANumber()
    {
        XColor.FromSpot(Pantone185, 3).Tint.Should().Be(1);
        XColor.FromSpot(Pantone185, -1).Tint.Should().Be(0);

        var nan = () => XColor.FromSpot(Pantone185, double.NaN);
        nan.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ASpotColourNeedsAName()
    {
        var empty = () => new XSpotColor("", XColors.Red);
        var none = () => new XSpotColor(null, XColors.Red);

        empty.Should().Throw<ArgumentException>();
        none.Should().Throw<ArgumentNullException>();
    }

    // ----- PDF/A ----------------------------------------------------------------------------------

    [Fact]
    public void ACmykAlternateBreaksAnRgbArchivalDocument()
    {
        // The default output intent is sRGB, and a reader without the ink paints the CMYK
        // alternate for real - so the same rule that refuses "k" refuses this.
        var saving = SavingArchival(PdfColorMode.Rgb, profile: null, Pantone185);

        saving.Should().Throw<InvalidOperationException>()
            .WithMessage("*output intent*").WithMessage("*4-component*");
    }

    [Fact]
    public void AnRgbAlternateConformsInAnRgbArchivalDocument()
    {
        SavingArchival(PdfColorMode.Rgb, profile: null, WhiteInk).Should().NotThrow();
    }

    [Fact]
    public void ACmykAlternateConformsWhenTheOutputIntentIsCmyk()
    {
        SavingArchival(PdfColorMode.Cmyk, CmykProfile(), Pantone185).Should().NotThrow();
    }

    // ----- helpers --------------------------------------------------------------------------------

    private static Action SavingArchival(PdfColorMode mode, byte[] profile, XSpotColor ink) => () =>
    {
        var document = new PdfDocument();
        document.Info.Title = "Spot colour";
        document.Options.ColorMode = mode;
        if (profile != null)
        {
            document.Options.OutputIntentIccProfile = profile;
            document.Options.OutputIntentIdentifier = "Some press";
        }
        document.Options.Conformance = PdfAConformance.PdfA2B;

        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromSpot(ink)), 10, 10, 100, 100);

        using var output = new MemoryStream();
        document.Save(output, false);
    };

    /// <summary>
    ///   Not a usable profile: only the colour-space signature at offset 16 is read, which is what
    ///   says the output intent is four-component.
    /// </summary>
    private static byte[] CmykProfile()
    {
        var profile = new byte[128];
        Encoding.ASCII.GetBytes("CMYK").CopyTo(profile, 16);
        return profile;
    }

    private static PdfPages Reopened(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            draw(gfx);
        return Reopen(document);
    }

    private static PdfPages Reopen(PdfDocument document)
    {
        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages;
    }

    private static string Content(PdfPage page) => Encoding.ASCII.GetString(PageContent.Of(page));

    private static PdfArray ColorSpace(PdfPage page, string name) => (PdfArray)Resolve(SpaceReference(page, name));

    private static PdfReference SpaceReference(PdfPage page, string name)
    {
        var spaces = page.Elements.GetDictionary("/Resources")!.Elements.GetDictionary("/ColorSpace");
        spaces.Should().NotBeNull("a page painting a spot colour names its colour space");
        return (PdfReference)spaces!.Elements[name];
    }

    private static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    private static double[] Numbers(PdfArray array)
    {
        var numbers = new double[array.Elements.Count];
        for (var idx = 0; idx < numbers.Length; idx++)
            numbers[idx] = Math.Round(array.Elements.GetReal(idx), 4);
        return numbers;
    }
}
