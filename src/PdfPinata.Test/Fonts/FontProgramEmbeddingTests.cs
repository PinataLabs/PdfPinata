using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   Embedding a font program a caller supplies, under a name the caller chooses, rather than a face
///   the resolver found.
/// </summary>
/// <remarks>
///   The document holds one of these per name, in <c>PdfFontTable</c>, so a program asked for twice
///   is embedded once. Both of the table's methods for it used to look the font up under a hard-coded
///   null key: <c>GetFont</c> threw <see cref="ArgumentNullException"/> out of the dictionary, and
///   <c>TryGetFont</c> - which is documented to answer null when there is no such font - could not
///   reach the lookup at all in a Debug build, because it opened with <c>Debug.Assert(false)</c>.
///   Neither was reachable from outside the assembly, which is why neither was noticed;
///   <see cref="PdfPage.AddFontProgram"/> and <see cref="PdfPage.TryGetFontProgramName"/> are the
///   route in, and these are the tests that take it.
/// </remarks>
public class FontProgramEmbeddingTests
{
    [Fact]
    public void AFontProgramIsEmbeddedAsACompositeFont()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var name = page.AddFontProgram("Liberation", Program());

        name.Should().StartWith("/", "a resource name is a PDF name");

        var font = FontResourcesOf(page).Single();
        font.Elements.GetName("/Subtype").Should().Be("/Type0");
        font.Elements.GetName("/Encoding").Should().Be("/Identity-H");
    }

    /// <summary>
    ///   The program itself reaches the file. Read back rather than asserted on the object in
    ///   memory, so that what is checked is what a reader is given.
    /// </summary>
    [Fact]
    public void TheProgramItselfIsWrittenToTheFile()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.AddFontProgram("Liberation", Program());

        var reopened = document.Reopened();
        var descriptor = FontResourcesOf(reopened.Pages[0])
            .Select(font => DescendantOf(font).Elements.GetDictionary("/FontDescriptor"))
            .Single();

        descriptor.Should().NotBeNull();
        descriptor.Elements.GetDictionary("/FontFile2").Stream.Value.Should().NotBeEmpty();
    }

    /// <summary>
    ///   The name is what identifies the program, so asking twice embeds it once. This is the half of
    ///   the defect that a null key hid rather than announced: every program shared one entry.
    /// </summary>
    [Fact]
    public void AskingTwiceUnderTheSameNameEmbedsItOnce()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var first = page.AddFontProgram("Liberation", Program());
        var second = page.AddFontProgram("Liberation", Program());

        second.Should().Be(first);
        FontResourcesOf(page).Should().HaveCount(1);
    }

    /// <summary>
    ///   And two names are two fonts. Under the null key the second name was answered with the first
    ///   name's font, so a page naming both drew both in the same face.
    /// </summary>
    [Fact]
    public void TwoNamesAreTwoFonts()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var sans = page.AddFontProgram("Liberation Sans", Program());
        var mono = page.AddFontProgram("Source Code Pro", Program("SourceCodePro-Regular.otf"));

        mono.Should().NotBe(sans);
        FontResourcesOf(page).Should().HaveCount(2);
    }

    [Fact]
    public void AProgramNobodyEmbeddedIsNotFound()
    {
        var page = new PdfDocument().AddPage();

        page.TryGetFontProgramName("Liberation").Should().BeNull();
    }

    [Fact]
    public void AProgramAlreadyEmbeddedIsFoundUnderItsName()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var name = page.AddFontProgram("Liberation", Program());

        page.TryGetFontProgramName("Liberation").Should().Be(name);
        page.TryGetFontProgramName("Something else").Should().BeNull();
    }

    /// <summary>
    ///   A second page reaches the same embedded program: the font table belongs to the document, and
    ///   only the resource entry is per page.
    /// </summary>
    [Fact]
    public void ASecondPageReachesTheProgramTheFirstEmbedded()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();

        first.AddFontProgram("Liberation", Program());

        second.TryGetFontProgramName("Liberation").Should().NotBeNull();
        FontResourcesOf(second).Single().Should().BeSameAs(FontResourcesOf(first).Single());
    }

    /// <summary>
    ///   A program and a face the resolver found are held in the one dictionary, so the two sets of
    ///   keys have to stay apart. The name asked for here is the very key the drawn face is held
    ///   under, so were the two key spaces one this would answer with that face.
    /// </summary>
    [Fact]
    public void AProgramCannotBeNamedSoAsToAnswerForAFaceThePageDrewWith()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var font = new XFont("Arial", 20);
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Arial", font, XBrushes.Black, 20, 40);

        page.TryGetFontProgramName(KeyOfFace(font)).Should().BeNull(
            "the face the page drew with is not a font program anybody supplied");
    }

    [Fact]
    public void ANameIsRequiredAndSoAreTheBytes()
    {
        var page = new PdfDocument().AddPage();

        page.Invoking(p => p.AddFontProgram(null, Program()))
            .Should().Throw<ArgumentNullException>();
        page.Invoking(p => p.AddFontProgram("", Program()))
            .Should().Throw<ArgumentException>();
        page.Invoking(p => p.AddFontProgram("Liberation", null))
            .Should().Throw<ArgumentNullException>();
        page.Invoking(p => p.TryGetFontProgramName(null))
            .Should().Throw<ArgumentNullException>();
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///   The key <c>PdfFontTable</c> holds a resolved face under, computed by the table's own method
    ///   so that the name a test hands over is exactly the one that would collide. Reached by
    ///   reflection because the table is internal and this repository carries no
    ///   <c>InternalsVisibleTo</c>, the same way the probes in <c>FontPlumbingTests</c> do.
    /// </summary>
    private static string KeyOfFace(XFont font) => (string)typeof(XPoint).Assembly
        .GetType("PdfPinata.Pdf.Advanced.PdfFontTable", throwOnError: true)!
        .GetMethod("ComputeKey", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(XFont)], null)!
        .Invoke(null, [font]);

    /// <summary>A font program, as a caller who read one off disk would hand it over.</summary>
    private static byte[] Program(string file = "LiberationSans-Regular.ttf") =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", file));

    /// <summary>The fonts a page's resource dictionary names.</summary>
    private static PdfDictionary[] FontResourcesOf(PdfPage page)
    {
        var fonts = page.Resources.Elements.GetDictionary("/Font");
        if (fonts == null)
            return [];

        return [..fonts.Elements.Values
            .Select(item => item is PdfReference reference ? reference.Value : item)
            .Cast<PdfDictionary>()];
    }

    private static PdfDictionary DescendantOf(PdfDictionary type0) =>
        (PdfDictionary)((PdfReference)type0.Elements.GetArray("/DescendantFonts").Elements[0]).Value;
}
