using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   What <see cref="PdfDocumentOptions.DeduplicateResources" /> merges and what it leaves alone.
///   Merging a hundred files by importing their pages writes each file's fonts and images once per
///   file; the option collapses the copies that are identical. See empira/PDFsharp#275.
///   <para>
///     Every document here is written by hand, read, saved with the option and read back, so what
///     is asserted is what a reader of the file would find rather than what the object model holds.
///   </para>
/// </summary>
public class ResourceDeduplicationTests
{
    [Fact]
    public void IdenticalImagesOnTwoPagesAreWrittenOnce()
    {
        var document = Saved(TwoPagesDrawing(Image("same bytes"), Image("same bytes")), deduplicate: true);

        ResourceOf(document.Pages[0], "/XObject", "/Im0").Should()
            .Be(ResourceOf(document.Pages[1], "/XObject", "/Im0"));
    }

    [Fact]
    public void WithoutTheOptionNothingIsMerged()
    {
        // The state of affairs the issue reports, and what makes the test above worth having.
        var document = Saved(TwoPagesDrawing(Image("same bytes"), Image("same bytes")), deduplicate: false);

        ResourceOf(document.Pages[0], "/XObject", "/Im0").Should()
            .NotBe(ResourceOf(document.Pages[1], "/XObject", "/Im0"));
    }

    [Fact]
    public void ImagesDifferingByOneByteAreKeptApart()
    {
        var document = Saved(TwoPagesDrawing(Image("bytes A"), Image("bytes B")), deduplicate: true);

        ResourceOf(document.Pages[0], "/XObject", "/Im0").Should()
            .NotBe(ResourceOf(document.Pages[1], "/XObject", "/Im0"));
    }

    [Fact]
    public void ImagesDifferingOnlyInTheirDictionaryAreKeptApart()
    {
        var document = Saved(TwoPagesDrawing(Image("same bytes"), Image("same bytes", "/Interpolate true")),
            deduplicate: true);

        ResourceOf(document.Pages[0], "/XObject", "/Im0").Should()
            .NotBe(ResourceOf(document.Pages[1], "/XObject", "/Im0"));
    }

    [Fact]
    public void AFontCollapsesTogetherWithEverythingItRefersTo()
    {
        var document = Saved(TwoPagesWithFonts(widthsOfSecond: "[500 600]"), deduplicate: true);

        var first = FontOf(document.Pages[0]);
        var second = FontOf(document.Pages[1]);

        first.Reference.Should().BeSameAs(second.Reference, "the fonts are identical all the way down");
        document.Pages[0].Owner.Internals.GetAllObjects()
            .Count(o => o is PdfDictionary d && d.Elements.GetName("/Type") == "/FontDescriptor")
            .Should().Be(1);
    }

    [Fact]
    public void FontsDifferingInTheirWidthsShareTheirProgramAndNothingAbove()
    {
        var document = Saved(TwoPagesWithFonts(widthsOfSecond: "[500 700]"), deduplicate: true);

        var first = FontOf(document.Pages[0]);
        var second = FontOf(document.Pages[1]);

        first.Reference.Should().NotBeSameAs(second.Reference);
        FontFileOf(first).Should().BeSameAs(FontFileOf(second),
            "the programs are the same bytes, and only what refers to them differs");
    }

    [Fact]
    public void FormsThatDrawThemselvesAreMergedWhenEqual()
    {
        // A reference cycle: each form names itself in its own resources. Deciding the two are
        // equal needs knowing they are equal, which is what refining classes rather than
        // comparing depth first is for.
        var document = Saved(Build(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Resources<</XObject<</Fm0 5 0 R>>>>/Contents 7 0 R"),
            Page("/Resources<</XObject<</Fm0 6 0 R>>>>/Contents 7 0 R"),
            Form("/Resources<</XObject<</Fm0 5 0 R>>>>", "0 0 m 10 10 l S"),
            Form("/Resources<</XObject<</Fm0 6 0 R>>>>", "0 0 m 10 10 l S"),
            RawPdf.Stream("", "/Fm0 Do")), deduplicate: true);

        ResourceOf(document.Pages[0], "/XObject", "/Fm0").Should()
            .Be(ResourceOf(document.Pages[1], "/XObject", "/Fm0"));
    }

    [Fact]
    public void OptionalContentGroupsAreNotMergedAndNeitherIsWhatNamesThem()
    {
        // Two layers called the same thing are still two layers, each shown or hidden on its own,
        // and a form belonging to one is not the form belonging to the other.
        var document = Saved(Build(
            "<</Type/Catalog/Pages 2 0 R/OCProperties<</OCGs[7 0 R 8 0 R]/D<</Order[7 0 R 8 0 R]>>>>>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Resources<</XObject<</Fm0 5 0 R>>>>/Contents 9 0 R"),
            Page("/Resources<</XObject<</Fm0 6 0 R>>>>/Contents 9 0 R"),
            Form("/OC 7 0 R", "0 0 m 10 10 l S"),
            Form("/OC 8 0 R", "0 0 m 10 10 l S"),
            "<</Type/OCG/Name(Layer)>>",
            "<</Type/OCG/Name(Layer)>>",
            RawPdf.Stream("", "/Fm0 Do")), deduplicate: true);

        var first = ResolvedResource(document.Pages[0], "/XObject", "/Fm0");
        var second = ResolvedResource(document.Pages[1], "/XObject", "/Fm0");

        first.Reference.Should().NotBeSameAs(second.Reference);
        first.Elements["/OC"].Should().NotBeSameAs(second.Elements["/OC"]);
    }

    [Fact]
    public void PagesAndAnnotationsStayApartWhileWhatTheyDrawIsMerged()
    {
        // Two pages alike in every byte, each with an annotation alike in every byte. The pages
        // and annotations are referred to for which one they are - a link goes to a page, an
        // annotation belongs to one - so neither is merged. What they draw is.
        var document = Saved(Build(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Resources<</Font<</F1 7 0 R>>>>/Contents 5 0 R/Annots[9 0 R]"),
            Page("/Resources<</Font<</F1 8 0 R>>>>/Contents 6 0 R/Annots[10 0 R]"),
            RawPdf.Stream("", "BT /F1 12 Tf (hi) Tj ET"),
            RawPdf.Stream("", "BT /F1 12 Tf (hi) Tj ET"),
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            "<</Type/Annot/Subtype/Square/Rect[10 10 50 50]/AP<</N 11 0 R>>>>",
            "<</Type/Annot/Subtype/Square/Rect[10 10 50 50]/AP<</N 12 0 R>>>>",
            Form("/Resources<</Font<</F1 7 0 R>>>>", "BT /F1 12 Tf (a) Tj ET"),
            Form("/Resources<</Font<</F1 8 0 R>>>>", "BT /F1 12 Tf (a) Tj ET")), deduplicate: true);

        document.PageCount.Should().Be(2);
        var pageOne = document.Pages[0];
        var pageTwo = document.Pages[1];
        pageOne.Reference.Should().NotBeSameAs(pageTwo.Reference);

        var annotationOne = (PdfReference)pageOne.Elements.GetArray("/Annots").Elements[0];
        var annotationTwo = (PdfReference)pageTwo.Elements.GetArray("/Annots").Elements[0];
        annotationOne.Should().NotBeSameAs(annotationTwo);

        // An appearance is redrawn in place when a field's value changes, so the streams stay
        // apart and only the font they draw with is shared.
        var appearanceOne = AppearanceOf(annotationOne);
        var appearanceTwo = AppearanceOf(annotationTwo);
        appearanceOne.Reference.Should().NotBeSameAs(appearanceTwo.Reference);
        ResourceIn(appearanceOne, "/Font", "/F1").Should().BeSameAs(ResourceIn(appearanceTwo, "/Font", "/F1"));

        pageOne.Elements["/Contents"].Should().BeSameAs(pageTwo.Elements["/Contents"]);
        ResourceOf(pageOne, "/Font", "/F1").Should().Be(ResourceOf(pageTwo, "/Font", "/F1"));
    }

    [Fact]
    public void ADictionaryWithAParentIsNotMerged()
    {
        // Nothing a resource names should have a parent, but if something does, it is a node of a
        // tree and stands for its place in it.
        var document = Saved(Build(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Resources<</ExtGState<</GS0 5 0 R>>>>/Contents 7 0 R"),
            Page("/Resources<</ExtGState<</GS0 6 0 R>>>>/Contents 7 0 R"),
            "<</Type/ExtGState/CA 0.5/Parent 2 0 R>>",
            "<</Type/ExtGState/CA 0.5/Parent 2 0 R>>",
            RawPdf.Stream("", "/GS0 gs")), deduplicate: true);

        ResourceOf(document.Pages[0], "/ExtGState", "/GS0").Should()
            .NotBe(ResourceOf(document.Pages[1], "/ExtGState", "/GS0"));
    }

    [Fact]
    public void AnAppendedRevisionRefusesTheOption()
    {
        using var source = new MemoryStream(TwoPagesDrawing(Image("same bytes"), Image("same bytes")));
        var document = Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Append);
        document.Options.DeduplicateResources = true;

        var incremental = () => document.SaveIncremental(new MemoryStream());
        var whole = () => document.Save(new MemoryStream());

        incremental.Should().Throw<InvalidOperationException>().WithMessage("*DeduplicateResources*Append*");
        whole.Should().Throw<InvalidOperationException>().WithMessage("*DeduplicateResources*Append*");
    }

    [Fact]
    public void ARefusedSaveLeavesACrossReferenceStreamTrailerAlone()
    {
        // Save replaces a cross-reference stream trailer with a classic one before it prepares
        // anything, and an incremental save afterwards would then write a classic trailer whose
        // /Prev names a cross-reference stream. The refusal has to come before that.
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        using var original = new MemoryStream();
        document.Save(original, false);

        original.Position = 0;
        var appended = Pdf.IO.PdfReader.Open(original, PdfDocumentOpenMode.Append);
        appended.Options.DeduplicateResources = true;

        var save = () => appended.Save(new MemoryStream());
        save.Should().Throw<InvalidOperationException>();

        appended.Options.DeduplicateResources = false;
        using var revised = new MemoryStream();
        appended.SaveIncremental(revised);

        var revision = System.Text.Encoding.Latin1.GetString(revised.ToArray(), (int)original.Length,
            (int)(revised.Length - original.Length));
        revision.Should().NotContain("trailer", "the revision continues the cross-reference streams before it");
        revision.Should().Contain("/XRef");
    }

    [Fact]
    public void AnEncryptedDocumentIsDeduplicatedAndReadsBack()
    {
        const string password = "owner";
        using var source = new MemoryStream(TwoPagesDrawing(Image("same bytes"), Image("same bytes")));
        var document = Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        document.Options.DeduplicateResources = true;
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.OwnerPassword = password;
        document.SecuritySettings.UserPassword = "";

        using var saved = new MemoryStream();
        document.Save(saved, false);
        saved.Position = 0;
        var reread = Pdf.IO.PdfReader.Open(saved, password, PdfDocumentOpenMode.Modify);

        var first = ResolvedResource(reread.Pages[0], "/XObject", "/Im0");
        first.Reference.Should().BeSameAs(ResolvedResource(reread.Pages[1], "/XObject", "/Im0").Reference);
        System.Text.Encoding.ASCII.GetString(first.Stream.Value).Should().Be("same bytes");
    }

    [Fact]
    public void SavingTwiceWritesWhatSavingOnceDoes()
    {
        using var source = new MemoryStream(TwoPagesWithFonts(widthsOfSecond: "[500 600]"));
        var document = Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        document.Options.DeduplicateResources = true;

        using var once = new MemoryStream();
        document.Save(once, false);
        using var twice = new MemoryStream();
        document.Save(twice, false);

        twice.Length.Should().Be(once.Length);
    }

    [Fact]
    public void APageImportedAfterSavingGetsTheCopyThatWasKept()
    {
        // Importing remembers which copy each object of the source became. The copies dropped by
        // the save have to be forgotten, or importing again from the same source hands out an
        // object that is no longer in the document. With the option still on, the next save would
        // merge it away again and hide that; turned off, nothing does.
        using var input = new MemoryStream(TwoPagesDrawing(Image("same bytes"), Image("same bytes")));
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        target.Options.DeduplicateResources = true;
        _ = target.AddPage(source.Pages[0]);
        _ = target.AddPage(source.Pages[1]);
        target.Save(new MemoryStream(), true);

        target.Options.DeduplicateResources = false;
        _ = target.AddPage(source.Pages[1]);
        using var saved = new MemoryStream();
        target.Save(saved, false);
        saved.Position = 0;
        var reread = Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Import);

        reread.PageCount.Should().Be(3);
        var images = Enumerable.Range(0, 3).Select(i => ResolvedResource(reread.Pages[i], "/XObject", "/Im0")).ToList();
        images.Should().OnlyContain(image => image != null && image.Stream != null);
        images.Select(image => image.Reference).Distinct().Should().HaveCount(1);
    }

    // ----- documents -----

    /// <summary>
    ///   Two pages, each drawing an image of its own as /Im0. The images are objects 5 and 6.
    /// </summary>
    private static byte[] TwoPagesDrawing(string firstImage, string secondImage) => Build(
        "<</Type/Catalog/Pages 2 0 R>>",
        "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
        Page("/Resources<</XObject<</Im0 5 0 R>>>>/Contents 7 0 R"),
        Page("/Resources<</XObject<</Im0 6 0 R>>>>/Contents 8 0 R"),
        firstImage,
        secondImage,
        RawPdf.Stream("", "q 100 0 0 100 0 0 cm /Im0 Do Q"),
        RawPdf.Stream("", "q 100 0 0 100 0 0 cm /Im0 Do Q"));

    /// <summary>
    ///   Two pages, each drawing with a TrueType font of its own: the font, its widths, its
    ///   descriptor and its program are each a separate object, and only the second font's widths
    ///   are given.
    /// </summary>
    private static byte[] TwoPagesWithFonts(string widthsOfSecond) => Build(
        "<</Type/Catalog/Pages 2 0 R>>",
        "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
        Page("/Resources<</Font<</F1 5 0 R>>>>/Contents 13 0 R"),
        Page("/Resources<</Font<</F1 6 0 R>>>>/Contents 13 0 R"),
        TrueTypeFont(widths: 7, descriptor: 9),
        TrueTypeFont(widths: 8, descriptor: 10),
        "[500 600]",
        widthsOfSecond,
        "<</Type/FontDescriptor/FontName/ABCDEF+Sample/Flags 32/FontBBox[0 0 1000 1000]"
        + "/ItalicAngle 0/Ascent 800/Descent -200/CapHeight 700/StemV 80/FontFile2 11 0 R>>",
        "<</Type/FontDescriptor/FontName/ABCDEF+Sample/Flags 32/FontBBox[0 0 1000 1000]"
        + "/ItalicAngle 0/Ascent 800/Descent -200/CapHeight 700/StemV 80/FontFile2 12 0 R>>",
        RawPdf.Stream("", "a font program, byte for byte"),
        RawPdf.Stream("", "a font program, byte for byte"),
        RawPdf.Stream("", "BT /F1 12 Tf (AB) Tj ET"));

    private static string TrueTypeFont(int widths, int descriptor) =>
        "<</Type/Font/Subtype/TrueType/BaseFont/ABCDEF+Sample/FirstChar 65/LastChar 66"
        + "/Widths " + widths + " 0 R/FontDescriptor " + descriptor + " 0 R/Encoding/WinAnsiEncoding>>";

    private static string Page(string entries) =>
        "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + ">>";

    private static string Form(string entries, string content) =>
        RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" + entries, content);

    private static string Image(string bytes, string entries = "") =>
        RawPdf.Stream("/Type/XObject/Subtype/Image/Width " + bytes.Length
            + "/Height 1/BitsPerComponent 8/ColorSpace/DeviceGray" + entries, bytes);

    private static byte[] Build(params string[] objects) => RawPdf.Build(new List<string>(objects));

    // ----- reading back -----

    /// <summary>
    ///   Reads the document, saves it, with or without the option, and reads what was saved.
    /// </summary>
    private static PdfDocument Saved(byte[] pdf, bool deduplicate)
    {
        using var source = new MemoryStream(pdf);
        var document = Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Modify);
        document.Options.DeduplicateResources = deduplicate;

        return document.Reopened();
    }

    private static PdfObjectID ResourceOf(PdfPage page, string category, string name) =>
        ((PdfReference)page.Elements.GetDictionary("/Resources").Elements.GetDictionary(category)
            .Elements[name]).ObjectID;

    private static PdfDictionary ResolvedResource(PdfPage page, string category, string name) =>
        ResourceIn(page.Elements.GetDictionary("/Resources").Elements.GetDictionary(category)
            .Elements[name]);

    private static PdfDictionary ResourceIn(PdfDictionary stream, string category, string name) =>
        ResourceIn(stream.Elements.GetDictionary("/Resources").Elements.GetDictionary(category).Elements[name]);

    private static PdfDictionary ResourceIn(PdfItem item) => (PdfDictionary)((PdfReference)item).Value;

    private static PdfDictionary FontOf(PdfPage page) => ResolvedResource(page, "/Font", "/F1");

    private static PdfReference FontFileOf(PdfDictionary font) =>
        (PdfReference)font.Elements.GetDictionary("/FontDescriptor").Elements["/FontFile2"];

    private static PdfDictionary AppearanceOf(PdfReference annotation) =>
        (PdfDictionary)((PdfReference)((PdfDictionary)annotation.Value).Elements.GetDictionary("/AP")
            .Elements["/N"]).Value;
}
