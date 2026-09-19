using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   A shading dictionary carries colour and no alpha, so a gradient between translucent colours
///   used to paint a flat opaque band over whatever it was meant to veil. It is now painted under
///   a luminosity soft mask built from the same geometry, which is the mechanism the PDF
///   specification provides and the one every other producer uses. These tests read the structure
///   the library writes; <see cref="GradientTransparencyRenderingTests" /> looks at the pixels.
/// </summary>
public class GradientTransparencyTests
{
    [Fact]
    public void AGradientWithAlphaIsPaintedUnderALuminosityMask()
    {
        var page = SavedPageWith(FadingBrush());

        var mask = SoftMaskOf(page);
        mask.Should().NotBeNull("the gradient asked for alpha and a shading cannot carry any");
        mask.Elements.GetName("/S").Should().Be("/Luminosity");
        mask.Elements.GetName("/Type").Should().Be("/Mask");
        mask.Elements.ContainsKey("/G").Should().BeTrue();
    }

    [Fact]
    public void TheMasksGroupIsAGreyTransparencyGroup()
    {
        var form = MaskFormOf(SavedPageWith(FadingBrush()));

        form.Should().NotBeNull();
        form.Elements.GetName("/Subtype").Should().Be("/Form");

        var group = form.Elements.GetDictionary("/Group");
        group.Should().NotBeNull();
        group.Elements.GetName("/S").Should().Be("/Transparency");
        group.Elements.GetName("/CS").Should().Be("/DeviceGray");
    }

    [Fact]
    public void TheMaskPaintsTheAlphaOfTheGradientsColours()
    {
        var form = MaskFormOf(SavedPageWith(FadingBrush()));
        var shading = OnlyShadingIn(form);

        shading.Elements.GetName("/ColorSpace").Should().Be("/DeviceGray");

        // Fully transparent to fully opaque, as one grey component each: black lets nothing
        // through, white lets everything through.
        RampEnd(shading, "/C0").Should().Be("[0]");
        RampEnd(shading, "/C1").Should().Be("[1]");
    }

    [Fact]
    public void TheMaskFollowsTheSameGeometryAsTheColour()
    {
        var page = SavedPageWith(FadingBrush());

        var colour = OnlyShadingIn(ColourPatternOf(page));
        var alpha = OnlyShadingIn(MaskFormOf(page));

        // Same axis, same shading type, same interpolation - everything but the colour space and
        // the two values the ramp runs between.
        alpha.Elements["/Coords"].ToString().Should().Be(colour.Elements["/Coords"].ToString());
        alpha.Elements["/ShadingType"].ToString().Should().Be(colour.Elements["/ShadingType"].ToString());
        alpha.Elements.GetDictionary("/Function").Elements["/Domain"].ToString()
            .Should().Be(colour.Elements.GetDictionary("/Function").Elements["/Domain"].ToString());
    }

    [Fact]
    public void ARadialGradientIsMaskedTheSameWay()
    {
        var page = SavedPageWith(new XRadialGradientBrush(
            new XPoint(120, 120), new XPoint(120, 120), 0, 100,
            XColors.Black, XColor.FromArgb(0, 0, 0, 0)));

        OnlyShadingIn(MaskFormOf(page)).Elements["/ShadingType"].ToString().Should().Be("3");
    }

    [Fact]
    public void HalfAnAlphaIsHalfAGrey()
    {
        var half = XColor.FromArgb(128, 0, 0, 0);
        var page = SavedPageWith(new XLinearGradientBrush(Box, half, half, XLinearGradientMode.Horizontal));

        var shading = OnlyShadingIn(MaskFormOf(page));

        // 128/255 to three figures, at both ends: a ramp that does not ramp.
        RampEnd(shading, "/C0").Should().Be("[0.502]");
        RampEnd(shading, "/C1").Should().Be("[0.502]");
    }

    [Fact]
    public void TheMaskIsTakenOffAgainBeforeAnythingElseIsDrawn()
    {
        var page = SavedPageWith(gfx =>
        {
            gfx.DrawRectangle(FadingBrush(), Box);
            gfx.DrawRectangle(XBrushes.Black, new XRect(20, 240, 200, 100));
        });

        // The mask goes on for the gradient, and comes off before the opaque rectangle is filled.
        // What is applied after that is the alpha constant the black brush asks for, which says
        // nothing about soft masks and leaves the /None in force.
        var applied = SoftMasksAppliedBy(page);

        applied.Should().ContainInOrder("mask", "none");
        applied.Should().NotContainInOrder("none", "mask");
    }

    [Fact]
    public void TwoGradientsOnOnePageCarryOneMaskEach()
    {
        var fading = new XLinearGradientBrush(Box, XColor.FromArgb(0, 0, 0, 0), XColors.Black,
            XLinearGradientMode.Horizontal);
        var other = new XRect(20, 240, 200, 100);
        var thinning = new XLinearGradientBrush(other, XColors.Red, XColor.FromArgb(64, 255, 0, 0),
            XLinearGradientMode.Vertical);

        var page = SavedPageWith(gfx =>
        {
            gfx.DrawRectangle(fading, Box);
            gfx.DrawRectangle(thinning, other);
        });

        var masks = FormsIn(page).Select(form => RampEnd(OnlyShadingIn(form), "/C1")).ToList();

        // One ramp each, and neither is the other's.
        masks.Should().HaveCount(2);
        masks.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AnOpaqueGradientAfterATranslucentOneIsNotMasked()
    {
        var other = new XRect(20, 240, 200, 100);
        var page = SavedPageWith(gfx =>
        {
            gfx.DrawRectangle(FadingBrush(), Box);
            gfx.DrawRectangle(new XLinearGradientBrush(other, XColors.Red, XColors.Blue,
                XLinearGradientMode.Horizontal), other);
        });

        // One mask for the one gradient that asked for one, taken off before the other is drawn.
        FormsIn(page).Should().ContainSingle();
        SoftMasksAppliedBy(page).Should().ContainInOrder("mask", "none");
    }

    // ----- the page under test -------------------------------------------------------------------

    static readonly XRect Box = new XRect(20, 20, 200, 100);

    /// <summary>Fully transparent black to fully opaque black, across the box.</summary>
    static XLinearGradientBrush FadingBrush()
    {
        return new XLinearGradientBrush(Box, XColor.FromArgb(0, 0, 0, 0), XColors.Black,
            XLinearGradientMode.Horizontal);
    }

    static PdfPage SavedPageWith(XBrush brush) => SavedPageWith(gfx => gfx.DrawRectangle(brush, Box));

    static PdfPage SavedPageWith(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }

    // ----- reading the structure back ------------------------------------------------------------

    static string ContentOf(PdfPage page) => System.Text.Encoding.ASCII.GetString(PageContent.Of(page));

    /// <summary>The two values of a shading's ramp, with the spacing a round trip adds removed.</summary>
    static string RampEnd(PdfDictionary shading, string key)
    {
        return shading.Elements.GetDictionary("/Function").Elements[key].ToString().Replace(" ", "");
    }

    /// <summary>
    ///   What each extended graphics state the page applies says about soft masks, in the order
    ///   they are applied: "mask" for one that sets a mask, "none" for one that takes it off, and
    ///   nothing at all for one that has no opinion.
    /// </summary>
    static System.Collections.Generic.IReadOnlyList<string> SoftMasksAppliedBy(PdfPage page)
    {
        var states = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/ExtGState");

        return ContentOf(page).Split('\n')
            .Where(line => line.EndsWith(" gs"))
            .Select(line => states.Elements.GetDictionary(line[..^3]))
            .Select(state => state.Elements.GetDictionary("/SMask") != null ? "mask"
                : state.Elements.GetName("/SMask") == "/None" ? "none"
                : "quiet")
            .ToList();
    }

    /// <summary>The soft mask the page's one masking graphics state names.</summary>
    static PdfDictionary SoftMaskOf(PdfPage page)
    {
        var states = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/ExtGState");
        return states?.Elements.KeyNames
            .Select(key => states.Elements.GetDictionary(key.Value).Elements.GetDictionary("/SMask"))
            .FirstOrDefault(mask => mask != null);
    }

    static PdfDictionary MaskFormOf(PdfPage page) => SoftMaskOf(page)?.Elements.GetDictionary("/G");

    /// <summary>Every mask form the page reaches, one per masked gradient.</summary>
    static System.Collections.Generic.IReadOnlyList<PdfDictionary> FormsIn(PdfPage page)
    {
        var states = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/ExtGState");
        if (states == null)
            return Array.Empty<PdfDictionary>();

        return states.Elements.KeyNames
            .Select(key => states.Elements.GetDictionary(key.Value).Elements.GetDictionary("/SMask"))
            .Where(mask => mask != null)
            .Select(mask => mask.Elements.GetDictionary("/G"))
            .ToList();
    }

    /// <summary>The colour shading pattern the page fills with.</summary>
    static PdfDictionary ColourPatternOf(PdfPage page)
    {
        var patterns = page.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Pattern");
        return patterns.Elements.GetDictionary(patterns.Elements.KeyNames[0].Value);
    }

    /// <summary>The one shading a pattern or a mask form paints with.</summary>
    static PdfDictionary OnlyShadingIn(PdfDictionary patternOrForm)
    {
        var shading = patternOrForm.Elements.GetDictionary("/Shading");
        if (shading != null)
            return shading;

        var patterns = patternOrForm.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Pattern");
        return patterns.Elements.GetDictionary(patterns.Elements.KeyNames[0].Value)
            .Elements.GetDictionary("/Shading");
    }
}
