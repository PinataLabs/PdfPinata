using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   <see cref="PdfFreeTextAnnotation"/>: the dictionary it writes, and whether a reader paints
///   the text.
/// </summary>
/// <remarks>
///   The one annotation whose <c>/Contents</c> are the thing drawn rather than a description of
///   it, which is what the first two tests here are about; and, like <c>/Square</c> and
///   <c>/Line</c>, drawn from <c>/AP</c> and nothing else, which is what the rasterizing ones are.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public class FreeTextAnnotationTests : IDisposable
{
    const string OutDir = "Out/FreeTextAnnotations";

    readonly List<MagickImageCollection> _rasterized = new List<MagickImageCollection>();

    public void Dispose()
    {
        foreach (MagickImageCollection collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static FreeTextAnnotationTests()
    {
        GhostscriptSetup.Configure();
    }

    static readonly XRect Where = new XRect(60, 60, 220, 90);

    [Fact]
    public void AFreeTextNamesItsSubtypeAndCarriesTheDefaultAppearanceItIsRequiredTo()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        caption.Elements.GetName("/Subtype").Should().Be("/FreeText");

        // /DA is required of this subtype, and is written before anything can have changed so
        // that an annotation nobody configures is still well formed.
        caption.Elements.GetString("/DA").Should().Contain("Tf");
    }

    [Fact]
    public void TheDefaultAppearanceNamesTheSizeAndTheColourTheTextIsDrawnIn()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        caption.Font = new XFont("Arial", 14);
        caption.TextColor = XColors.Red;

        string da = caption.Elements.GetString("/DA");
        da.Should().Contain("14");
        da.Should().Contain("1 0 0 rg");
    }

    [Fact]
    public void SettingTheContentsRedrawsBecauseForThisSubtypeTheyAreWhatIsDrawn()
    {
        PdfFreeTextAnnotation caption = OnAPage();
        caption.Contents = "One";

        byte[] before = NormalStream(caption);

        caption.Contents = "Something altogether different";

        NormalStream(caption).Should().NotEqual(before);
    }

    [Fact]
    public void TheAlignmentIsWrittenAsAQuaddingCode()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        caption.Alignment = XParagraphAlignment.Right;
        caption.Elements.GetInteger("/Q").Should().Be(2);
        caption.Alignment.Should().Be(XParagraphAlignment.Right);

        caption.Alignment = XParagraphAlignment.Center;
        caption.Elements.GetInteger("/Q").Should().Be(1);
    }

    [Fact]
    public void JustifiedTextIsWrittenAsLeftBecauseQuaddingCannotSayJustified()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        caption.Alignment = XParagraphAlignment.Justify;

        // The drawing is ours and the entry is a reader's. Left is what a reader regenerating the
        // appearance would make of it anyway, so that is what the file says.
        caption.Elements.GetInteger("/Q").Should().Be(0);
        caption.Alignment.Should().Be(XParagraphAlignment.Justify);
    }

    [Fact]
    public void WhatTheTextGivesUpToTheBorderIsRecordedInRd()
    {
        PdfFreeTextAnnotation caption = OnAPage();
        caption.Contents = "Hello";

        caption.BorderWidth = 4;

        PdfArray differences = caption.Elements.GetArray("/RD");
        differences.Elements.Count.Should().Be(4);
        foreach (int side in new[] { 0, 1, 2, 3 })
            differences.Elements.GetReal(side).Should().Be(8);
    }

    [Fact]
    public void ANegativeBorderIsRefused()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        Action act = () => caption.BorderWidth = -1;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheAppearanceIsBuiltWhenTheAnnotationReachesAPage()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        PdfFreeTextAnnotation caption = new PdfFreeTextAnnotation();
        caption.Contents = "Written before there was anywhere to draw it";

        // Everything above was set with no document to build a form in. Adding it to the page is
        // what gives it one, and the appearance has to appear then rather than be lost.
        caption.Elements.ContainsKey("/AP").Should().BeFalse();

        page.Annotations.Add(caption);
        caption.Rectangle = new PdfRectangle(Where);

        caption.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void NoTextNoBorderAndNoBackgroundDrawsNothingAndKeepsNoAppearance()
    {
        PdfFreeTextAnnotation caption = OnAPage();
        caption.Contents = "Something";

        caption.Contents = "";
        caption.BorderWidth = 0;

        // Asked for nothing, draws nothing - and the appearance already there has to go, or text
        // cleared away stays on the page.
        caption.Elements.ContainsKey("/AP").Should().BeFalse();
        caption.Elements.ContainsKey("/RD").Should().BeFalse();
    }

    [Fact]
    public void AnnotationWithNoColourEntryHasNoBackgroundRatherThanABlackOne()
    {
        PdfFreeTextAnnotation caption = OnAPage();

        // PdfAnnotation.Color answers black for an annotation carrying no /C at all, so a box
        // nobody gave a background to would be filled black if the appearance read it through
        // that property. It reads the dictionary instead.
        caption.Elements.ContainsKey("/C").Should().BeFalse();
        caption.Color.Should().Be(XColors.Black);
    }

    [GoldenImageFact]
    public void TheTextIsPainted()
    {
        IMagickImage<byte> page = Rasterize("text", caption =>
        {
            caption.Contents = "Free text drawn onto the page";
            caption.TextColor = XColors.Firebrick;
            caption.BorderWidth = 0;
        });

        Count(page, IsRed).Should().BeGreaterThan(200);
    }

    [GoldenImageFact]
    public void ABackgroundColourFillsTheBox()
    {
        IMagickImage<byte> page = Rasterize("background", caption =>
        {
            caption.Contents = "";
            caption.BorderWidth = 0;
            caption.Color = XColors.RoyalBlue;
        });

        Count(page, IsBlue).Should().BeGreaterThan(1000);
    }

    [GoldenImageFact]
    public void AnEmptyAnnotationRasterizesToNothing()
    {
        IMagickImage<byte> page = Rasterize("empty", caption =>
        {
            caption.Contents = "";
            caption.BorderWidth = 0;
        });

        Count(page, IsAnythingButWhite).Should().Be(0);
    }

    IMagickImage<byte> Rasterize(string name, Action<PdfFreeTextAnnotation> arrange)
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        XGraphics gfx = XGraphics.FromPdfPage(page);

        PdfFreeTextAnnotation caption = new PdfFreeTextAnnotation();
        page.Annotations.Add(caption);
        caption.Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(Where));

        arrange(caption);

        MagickImageCollection images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    static PdfFreeTextAnnotation OnAPage()
    {
        PdfDocument document = new PdfDocument();
        PdfFreeTextAnnotation caption = new PdfFreeTextAnnotation();
        document.AddPage().Annotations.Add(caption);

        // Without somewhere to be there is nothing to draw, so nothing derived from the geometry
        // is written.
        caption.Rectangle = new PdfRectangle(Where);
        return caption;
    }

    static byte[] NormalStream(PdfFreeTextAnnotation caption)
    {
        PdfDictionary form =
            (PdfDictionary)caption.Elements.GetDictionary("/AP").Elements.GetObject("/N");
        return form.Stream.Value;
    }

    static bool IsRed(IMagickColor<byte> c) => c.R > 130 && c.G < 100 && c.B < 100;

    static bool IsBlue(IMagickColor<byte> c) => c.B > 150 && c.R < 120 && c.G < 150;

    static bool IsAnythingButWhite(IMagickColor<byte> c) => c.R < 240 || c.G < 240 || c.B < 240;

    static int Count(IMagickImage<byte> image, Func<IMagickColor<byte>, bool> match)
    {
        using IPixelCollection<byte> pixels = image.GetPixels();
        return pixels.Count(p =>
        {
            IMagickColor<byte> c = p.ToColor();
            return c != null && match(c);
        });
    }
}
