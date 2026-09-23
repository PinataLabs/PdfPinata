using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using SkiaSharp;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A page-level /Group entry tells a reader to composite the whole page as one unit against the
///   backdrop. Content that paints with transparency needs that; content that does not is changed
///   by it, and a page that arrived without one has to leave without one.
/// </summary>
public class PageTransparencyGroupTests
{
    /// <summary>
    ///   Spelled out rather than referred to: PdfPage.Keys is not visible outside the library.
    /// </summary>
    private const string GroupKey = "/Group";

    /// <summary>
    ///   Writes the document out and reads it back, so that what is asserted on is what was
    ///   written rather than the objects that were built to write it.
    /// </summary>
    private static PdfDocument RoundTripped(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static PdfDictionary GroupOf(PdfPage page)
    {
        return page.Elements.GetDictionary(GroupKey);
    }

    /// <summary>
    ///   A PNG of one colour at the alpha given, which is what puts an /SMask on the image
    ///   PdfPinata writes for it.
    /// </summary>
    private static XImage SquareWithAlpha(byte alpha)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        bitmap.Erase(new SKColor(0, 128, 255, alpha));

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var png = encoded.ToArray();

        return XImage.FromStream(() => new MemoryStream(png));
    }

    // ------------------------------------------------------------------ a page that needs none

    [Fact]
    public void APageDrawnOnOpaquelyIsWrittenWithoutATransparencyGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(10, 10, 100, 100));

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull(
            "nothing on the page paints with transparency, so compositing it as a group would " +
            "say something about the page that is not true of it");
    }

    [Fact]
    public void APageWithNothingOnItIsWrittenWithoutATransparencyGroup()
    {
        var document = new PdfDocument();
        _ = document.AddPage();

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull();
    }

    [Fact]
    public void AnImportedPageThatHadNoTransparencyGroupStillHasNoneAfterARoundTrip()
    {
        // The report this was written for: opening a document and saving it again stamped a
        // /Group onto every page of it, whatever the pages themselves had said.
        var path = PathHelper.GetInstance().GetAssetPath("test.pdf");

        var source = PdfPinata.Pdf.IO.PdfReader.Open(path, PdfDocumentOpenMode.ReadOnly);
        GroupOf(source.Pages[0]).Should().BeNull("the fixture this rests on must have no group");

        var document = PdfPinata.Pdf.IO.PdfReader.Open(path, PdfDocumentOpenMode.Modify);

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull(
            "a document that is only read and written back must come out the way it went in");
    }

    [Fact]
    public void AnImportedPageThatHadATransparencyGroupKeepsTheOneItHad()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var group = new PdfDictionary(document);
        group.Elements.SetName("/S", "/Transparency");
        group.Elements.SetName("/CS", "/DeviceGray");
        page.Elements[GroupKey] = group;

        var written = GroupOf(RoundTripped(document).Pages[0]);

        written.Should().NotBeNull();
        written.Elements.GetName("/CS").Should().Be("/DeviceGray",
            "the group the page carries is its own, and is not to be replaced by a made one");
    }

    // ------------------------------------------------------------------- a page that needs one

    [Fact]
    public void APageDrawnOnWithATranslucentBrushIsGivenATransparencyGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(128, 255, 0, 0)), new XRect(10, 10, 100, 100));

        var group = GroupOf(RoundTripped(document).Pages[0]);

        group.Should().NotBeNull();
        group.Elements.GetName("/S").Should().Be("/Transparency");
        group.Elements.GetName("/CS").Should().Be("/DeviceRGB");
    }

    [Fact]
    public void APageDrawnOnWithATranslucentPenIsGivenATransparencyGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawLine(new XPen(XColor.FromArgb(128, 0, 0, 255), 4), 10, 10, 100, 100);

        GroupOf(RoundTripped(document).Pages[0]).Should().NotBeNull();
    }

    // -------------------------------------------------------------------- what an image brings

    [Fact]
    public void APageCarryingAnImageWithAnAlphaChannelIsGivenATransparencyGroup()
    {
        // The case the old code's "TODO: check XObjects" stood for. No colour the renderer draws
        // with has alpha in it - the transparency is inside the image, as a soft mask - so it is
        // found only by looking at what was placed on the page.
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(SquareWithAlpha(100), new XRect(10, 10, 50, 50));

        GroupOf(RoundTripped(document).Pages[0]).Should().NotBeNull(
            "the image paints through a soft mask even though nothing else on the page does");
    }

    [Fact]
    public void APageCarryingAnOpaqueImageIsWrittenWithoutATransparencyGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(SquareWithAlpha(255), new XRect(10, 10, 50, 50));

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull(
            "every pixel of the image is opaque, so no soft mask is written for it");
    }

    // --------------------------------------------------------------------- what a form brings

    [Fact]
    public void APageCarryingAFormThatWasDrawnOnTranslucentlyIsGivenATransparencyGroup()
    {
        // The transparency is a graphics state within the form. The page is never asked to draw
        // a colour with alpha in it, so the form has to be looked into.
        var document = new PdfDocument();
        var page = document.AddPage();

        var form = new XForm(document, new XSize(100, 100));
        using (var formGfx = XGraphics.FromForm(form))
            formGfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(128, 0, 255, 0)), new XRect(0, 0, 100, 100));

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, new XRect(10, 10, 100, 100));

        GroupOf(RoundTripped(document).Pages[0]).Should().NotBeNull();
    }

    [Fact]
    public void APageCarryingAFormThatWasDrawnOnOpaquelyIsWrittenWithoutATransparencyGroup()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var form = new XForm(document, new XSize(100, 100));
        using (var formGfx = XGraphics.FromForm(form))
            formGfx.DrawRectangle(XBrushes.Green, new XRect(0, 0, 100, 100));

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, new XRect(10, 10, 100, 100));

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull();
    }

    // -------------------------------------------------------------------- what the group says

    [Fact]
    public void ThePageOfADocumentSetToCmykGetsAGroupInThatColourSpace()
    {
        var document = new PdfDocument();
        document.Options.ColorMode = PdfColorMode.Cmyk;
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(128, 255, 0, 0)), new XRect(10, 10, 100, 100));

        var group = GroupOf(RoundTripped(document).Pages[0]);

        group.Should().NotBeNull();
        group.Elements.GetName("/CS").Should().Be("/DeviceCMYK");
    }

    [Fact]
    public void ADocumentWithNoColourModeGetsNoGroupEvenWhereTransparencyIsUsed()
    {
        var document = new PdfDocument();
        document.Options.ColorMode = PdfColorMode.Undefined;
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(128, 255, 0, 0)), new XRect(10, 10, 100, 100));

        GroupOf(RoundTripped(document).Pages[0]).Should().BeNull(
            "a caller asking for no colour space to be imposed is asking for no group either");
    }

    // ------------------------------------------------ reading a page somebody else's tool wrote

    /// <summary>
    ///   Draws a page of a hand written document onto a page of a new one, and answers that page
    ///   as it was written.
    /// <para>
    ///   This is how the resources of an arbitrary page reach the code being tested: an imported
    ///   page becomes a form XObject carrying the whole resource dictionary it had, and the page
    ///   it is drawn on has to work out from that whether it needs a group.
    /// </para>
    /// </summary>
    private static PdfPage PageDrawnOnWith(byte[] source)
    {
        // Not disposed until the drawing is done: the form reads the document out of the stream.
        var stream = new MemoryStream(source);
        var form = XPdfForm.FromStream(stream);

        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(form, new XRect(0, 0, 200, 200));

        return RoundTripped(document).Pages[0];
    }

    /// <summary>
    ///   Whether drawing a page of the hand written document given leaves the page it was drawn
    ///   on with a transparency group.
    /// </summary>
    private static bool PlacingIsTransparent(byte[] source)
    {
        return GroupOf(PageDrawnOnWith(source)) != null;
    }

    /// <summary>
    ///   A one page document whose page carries the entries given beside its media box and draws
    ///   a filled square. Objects after the content stream are numbered from five.
    /// </summary>
    private static byte[] PageWithEntries(string entries, params string[] rest)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + "/Contents 4 0 R>>",
            RawPdf.Stream("", "0 0 200 200 re f")
        };
        objects.AddRange(rest);

        return RawPdf.Build(objects);
    }

    /// <summary>
    ///   A one page document whose page names the resources given and draws nothing in
    ///   particular. Objects after the content stream are numbered from five.
    /// </summary>
    private static byte[] PageWithResources(string resources, params string[] rest)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" +
            "/Resources<<" + resources + ">>/Contents 4 0 R>>",
            RawPdf.Stream("", "q 100 0 0 100 10 10 cm /GS0 gs Q")
        };
        objects.AddRange(rest);

        return RawPdf.Build(objects);
    }

    /// <summary>
    ///   A one page document whose only graphics state is the one given, which its content sets.
    ///   The state is object five, so anything the state points at starts at six.
    /// </summary>
    private static byte[] PageWithGraphicsState(string state, params string[] rest)
    {
        var objects = new List<string> { "<</Type/ExtGState" + state + ">>" };
        objects.AddRange(rest);

        return PageWithResources("/ExtGState<</GS0 5 0 R>>", [..objects]);
    }

    [Theory]
    [InlineData("/ca 0.5", true)]
    [InlineData("/ca 1", false)]
    [InlineData("/CA 0.5", true)]
    [InlineData("/CA 1", false)]
    public void AGraphicsStateCountsAsTransparencyOnlyWhereItsAlphaIsBelowOne(string alpha, bool expected)
    {
        PlacingIsTransparent(PageWithGraphicsState(alpha)).Should().Be(expected);
    }

    [Fact]
    public void AGraphicsStateSayingNothingAboutAlphaDoesNotCountAsTransparency()
    {
        // The trap this guards: GetReal answers 0 for a key that is absent, which would read
        // every graphics state in every document as fully transparent and put a group back on
        // every page that has one at all.
        PlacingIsTransparent(PageWithGraphicsState("/LW 2")).Should().BeFalse();
    }

    [Theory]
    [InlineData("/Normal", false)]
    [InlineData("/Compatible", false)]
    [InlineData("/Multiply", true)]
    [InlineData("/Screen", true)]
    public void ABlendModeCountsAsTransparencyOnlyWhereItReadsWhatIsUnderneath(string mode, bool expected)
    {
        PlacingIsTransparent(PageWithGraphicsState("/BM" + mode)).Should().Be(expected);
    }

    [Fact]
    public void ABlendModeGivenAsAListOfPreferencesIsReadThrough()
    {
        PlacingIsTransparent(PageWithGraphicsState("/BM[/Darken/Normal]")).Should().BeTrue();
    }

    [Fact]
    public void AGraphicsStateTurningTheSoftMaskOffDoesNotCountAsTransparency()
    {
        PlacingIsTransparent(PageWithGraphicsState("/SMask/None")).Should().BeFalse();
    }

    [Fact]
    public void AGraphicsStateWhoseSoftMaskIsNullDoesNotCountAsTransparency()
    {
        // A PDF null is how a writer says a key holds nothing. Reading it as a mask would put a
        // group back on pages whose content is opaque throughout.
        PlacingIsTransparent(PageWithGraphicsState("/SMask null")).Should().BeFalse();
    }

    [Fact]
    public void AGraphicsStateWhoseSoftMaskIsAReferenceToNullDoesNotCountAsTransparency()
    {
        PlacingIsTransparent(PageWithGraphicsState("/SMask 6 0 R", "null")).Should().BeFalse();
    }

    [Fact]
    public void AGraphicsStateSettingASoftMaskCountsAsTransparency()
    {
        PlacingIsTransparent(PageWithGraphicsState("/SMask<</Type/Mask/S/Luminosity/G 6 0 R>>",
                RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]", "0 0 200 200 re f")))
            .Should().BeTrue();
    }

    [Fact]
    public void AnImageWithASoftMaskCountsAsTransparency()
    {
        PlacingIsTransparent(PageWithResources("/XObject<</Im0 5 0 R>>",
                Image("/SMask 6 0 R"),
                Image("/ColorSpace/DeviceGray")))
            .Should().BeTrue();
    }

    [Fact]
    public void AnImageWhoseSoftMaskIsNullDoesNotCountAsTransparency()
    {
        PlacingIsTransparent(PageWithResources("/XObject<</Im0 5 0 R>>", Image("/SMask null")))
            .Should().BeFalse();
    }

    [Fact]
    public void AnImageMaskedByAStencilDoesNotCountAsTransparency()
    {
        // /Mask leaves pixels unpainted rather than blending them. It predates transparency
        // altogether and renders the same with a group or without one.
        PlacingIsTransparent(PageWithResources("/XObject<</Im0 5 0 R>>",
                Image("/Mask 6 0 R"),
                Image("/ImageMask true/BitsPerComponent 1")))
            .Should().BeFalse();
    }

    [Fact]
    public void TransparencyIsFoundInAFormDrawnWithinAForm()
    {
        PlacingIsTransparent(PageWithResources("/XObject<</Fm0 5 0 R>>",
                RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" +
                              "/Resources<</ExtGState<</GS1 6 0 R>>>>", "/GS1 gs"),
                "<</Type/ExtGState/ca 0.25>>"))
            .Should().BeTrue();
    }

    [Fact]
    public void AFormCarryingATransparencyGroupOfItsOwnCountsAsTransparency()
    {
        PlacingIsTransparent(PageWithResources("/XObject<</Fm0 5 0 R>>",
                RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" +
                              "/Group<</S/Transparency/CS/DeviceGray>>", "0 0 200 200 re f")))
            .Should().BeTrue();
    }

    [Fact]
    public void AFormDrawnWithinItselfIsAnsweredRatherThanFollowedForever()
    {
        PlacingIsTransparent(PageWithResources("/XObject<</Fm0 5 0 R>>",
                RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" +
                              "/Resources<</XObject<</Fm0 5 0 R>>>>", "/Fm0 Do")))
            .Should().BeFalse();
    }

    [Fact]
    public void AnImportedPageBringsItsTransparencyGroupWithItsContent()
    {
        // A group describes the content it wraps. Drawing the page into a form moves the content
        // and has to move the group with it, or the content arrives composited against a backdrop
        // that is not the one it was written for.
        var written = PageDrawnOnWith(
            PageWithEntries("/Group<</S/Transparency/CS/DeviceGray>>"));
        var group = FormOn(written).Elements.GetDictionary(GroupKey);

        group.Should().NotBeNull("the group belongs to the content, which is now inside the form");
        group.Elements.GetName("/S").Should().Be("/Transparency");
        group.Elements.GetName("/CS").Should().Be("/DeviceGray",
            "the group is imported as it was written, not made afresh");

        GroupOf(written).Should().NotBeNull(
            "a form that composites as a group is transparent content on the page holding it");
    }

    [Fact]
    public void AnImportedPageWhoseGroupIsNullIsDrawnWithoutOne()
    {
        // /Group null says the page has no group. Reading it as one would throw on the way in
        // rather than draw the page at all.
        FormOn(PageDrawnOnWith(PageWithEntries("/Group null")))
            .Elements.ContainsKey(GroupKey).Should().BeFalse();
    }

    [Fact]
    public void AnImportedPageWhoseGroupIsAReferenceToNullIsDrawnWithoutOne()
    {
        // The same thing said the long way round, which would otherwise be imported as a group
        // that is a null - written out, carried about, and describing nothing.
        FormOn(PageDrawnOnWith(PageWithEntries("/Group 5 0 R", "null")))
            .Elements.ContainsKey(GroupKey).Should().BeFalse();
    }

    /// <summary>The single XObject named by the page's resources.</summary>
    private static PdfDictionary FormOn(PdfPage page)
    {
        var xObjects = page.Elements.GetDictionary("/Resources")
            .Elements.GetDictionary("/XObject");

        return xObjects.Elements.GetDictionary(xObjects.Elements.KeyNames[0].Value);
    }

    private static string Image(string entries)
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Image/Width 4/Height 4" + entries,
            new string('A', 16));
    }
}
