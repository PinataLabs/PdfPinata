using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A field of variable text draws its value in the font its <c>/DA</c> names, not in whatever
///   the resolver's default is: the key in <c>/DA</c> is looked up in the form's <c>/DR /Font</c>,
///   and the <c>/BaseFont</c> found there says which family to ask the resolver for. Issue #353.
/// </summary>
/// <remarks>
///   The font embedded in <c>/DR</c> is often a subset and cannot draw new glyphs, so only its name
///   is used. What these tests read is the <c>/BaseFont</c> of the font in the widget's appearance
///   stream, which is the name of the face the resolver served. <see cref="PinnedFontResolver"/>
///   serves Liberation Sans for every family it has not been told about, so the families the
///   tests name are registered with a face of their own - Source Code Pro - and a name that
///   reached the resolver is told apart from one that did not.
/// </remarks>
public sealed class DefaultAppearanceFontTests
{
    /// <summary>A family only these tests ask for, served by Source Code Pro.</summary>
    private const string MonoFamily = "PinataDaMono";

    /// <summary>
    ///   A family served by Liberation Sans Bold, whose own name - Liberation Sans - is one the
    ///   resolver serves as a regular face. Asking it for the family the face calls itself instead
    ///   of the one it was asked for would draw this in the wrong weight.
    /// </summary>
    private const string BoldFamily = "PinataDaBoldFace";

    static DefaultAppearanceFontTests()
    {
        // Nothing else in the suite draws in Times New Roman or Courier New - the document object
        // model's tests name them but resolve no face - which is what makes it safe to register
        // them: the resolver is shared by everything running beside these tests.
        var mono = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", "SourceCodePro-Regular.otf"));
        PinnedFontResolver.Register(MonoFamily, mono);
        PinnedFontResolver.Register(BoldFamily,
            File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Bold.ttf")));
        PinnedFontResolver.Register("Times New Roman", mono);
        PinnedFontResolver.Register("Courier New", mono);
    }

    /// <summary>
    ///   A field of the given kind on a page, whose <c>/DA</c> names <c>/F1</c> and whose form's
    ///   <c>/DR</c> gives <c>/F1</c> the <paramref name="baseFont"/> - and, drawn, the face the
    ///   widget's appearance names.
    /// </summary>
    private static T FieldDrawnIn<T>(string baseFont, T field, string defaultAppearance = "/F1 12 Tf 0 g")
        where T : PdfAcroField
    {
        var document = field.Owner;
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        form.AddStandardFont("F1", baseFont);
        form.Fields.Add(field);
        field.DefaultAppearance = defaultAppearance;

        var gfx = XGraphics.FromPdfPage(page);
        field.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(new XRect(60, 60, 200, 30))));
        gfx.Dispose();
        return field;
    }

    private static PdfTextField Text(string baseFont, string defaultAppearance = "/F1 12 Tf 0 g") =>
        FieldDrawnIn(baseFont, new PdfTextField(new PdfDocument()) { Name = "text" }, defaultAppearance);

    /// <summary>
    ///   What tells the faces a widget's appearance stream draws in apart: each font's name without
    ///   the six letters of its subset tag, and the measurements its descriptor gives. The tag is
    ///   different in every document, and Liberation Sans is the name of all four of its styles,
    ///   so the name alone says neither which family was asked for nor which style.
    /// </summary>
    private static string[] FacesDrawnIn(PdfAcroField field)
    {
        var appearance = field.Widgets[0].Elements.GetDictionary("/AP").Elements.GetDictionary("/N");
        var fonts = appearance.Elements.GetDictionary("/Resources").Elements.GetDictionary("/Font");

        return fonts.Elements.Keys.Select(key => FaceOf(fonts.Elements.GetDictionary(key))).ToArray();
    }

    private static string FaceOf(PdfDictionary font)
    {
        var simple = font.Elements.GetArray("/DescendantFonts")?.Elements.GetDictionary(0) ?? font;
        var descriptor = simple.Elements.GetDictionary("/FontDescriptor");

        var name = Regex.Replace(simple.Elements.GetName("/BaseFont"), @"^/[A-Z]{6}\+", "/");
        var measurements = new[] { "/FontBBox", "/ItalicAngle", "/Ascent", "/Descent", "/CapHeight", "/XHeight" }
            .Select(key => descriptor.Elements[key]?.ToString());

        return name + " " + string.Join(" ", measurements);
    }

    /// <summary>
    ///   Asserts the field is drawn in the face <paramref name="expected"/> is. A face is told by
    ///   what <see cref="FaceOf"/> reads, and that is read off a field that sets that font itself
    ///   rather than written out here.
    /// </summary>
    private static void ShouldBeDrawnIn(PdfAcroField field, XFont expected)
    {
        var reference = Reference(expected);

        FacesDrawnIn(field).Should().ContainSingle().Which.Should().Be(reference);
    }

    private static string Reference(XFont font)
    {
        var field = FieldDrawnIn("Helvetica", new PdfTextField(new PdfDocument()) { Name = "reference" });
        field.Font = font;
        field.Text = "Hello";
        return FacesDrawnIn(field).Single();
    }

    private static XFont Mono => new(MonoFamily, 12);

    private static XFont Sans(XFontStyle style = XFontStyle.Regular) => new("Arial", 12, style);

    [Test]
    public void ASubsetTaggedNameAsksTheResolverForItsFamily()
    {
        var field = Text("/ABCDEF+" + MonoFamily + "MT");

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    [Arguments("Arial,Bold", XFontStyle.Bold)]
    [Arguments("Arial,Italic", XFontStyle.Italic)]
    [Arguments("Arial,BoldItalic", XFontStyle.BoldItalic)]
    [Arguments("ArialMT", XFontStyle.Regular)]
    [Arguments("Arial-BoldMT", XFontStyle.Bold)]
    [Arguments("Arial-ItalicMT", XFontStyle.Italic)]
    [Arguments("ABCDEF+Arial-BoldItalicMT", XFontStyle.BoldItalic)]
    [Arguments("Arial-Bold", XFontStyle.Bold)]
    [Arguments("Arial,Bold Italic", XFontStyle.BoldItalic)]
    [Arguments("Arial,Semibold", XFontStyle.Bold)]
    [Arguments("Helvetica-Oblique", XFontStyle.Italic)]
    [Arguments("Helvetica-BoldOblique", XFontStyle.BoldItalic)]
    public void TheStyleInTheNameIsAskedForAlongWithTheFamily(string baseFont, XFontStyle style)
    {
        var field = Text(baseFont);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans(style));
    }

    [Test]
    [Arguments("Times-Roman")]
    [Arguments("TimesPSMT")]
    [Arguments("ABCDEF+TimesMT")]
    [Arguments("TimesNewRomanPSMT")]
    [Arguments("TimesNewRomanPS-BoldMT")]
    public void TheStandardTimesNamesAreAskedForAsTimes(string baseFont)
    {
        var field = Text(baseFont);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    [Arguments("Courier")]
    [Arguments("Cour")]
    [Arguments("ABCDEF+Courier")]
    [Arguments("CourierNewPSMT")]
    public void TheStandardCourierNamesAreAskedForAsCourier(string baseFont)
    {
        var field = Text(baseFont);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    [Arguments("Helvetica")]
    [Arguments("Helv")]
    public void TheStandardHelveticaNamesAreDrawnInASansSerifFace(string baseFont)
    {
        var field = Text(baseFont);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    [Arguments("ZapfDingbats")]
    [Arguments("ZaDb")]
    [Arguments("Symbol")]
    public void ASymbolicFaceIsLeftToTheExistingRulesAndDrawnInTheDefaultFont(string baseFont)
    {
        var field = Text(baseFont);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    public void ANameTheResolverDoesNotServeFallsBackToTheDefaultFontWithoutThrowing()
    {
        var field = Text("ABCDEF+" + PinnedFontResolver.UnservedFamilyName + ",Bold");

        var draw = () => field.Text = "Hello";

        draw.Should().NotThrow();
        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    public void AFamilyThatEndsInTheLettersOfASuffixKeepsThem()
    {
        // "Pinata MT" is a family as the platform spells it; stripping "MT" would ask for "Pinata ".
        PinnedFontResolver.Register("Pinata MT", File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "SourceCodePro-Regular.otf")));
        var field = Text("Pinata MT");

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    public void AKeyWrittenWithAnEscapeIsFoundInTheDefaultResources()
    {
        // /F#20A in a content stream is the name "F A", which is how the lexer stores it as a key.
        var field = Text(MonoFamily, "/F#20A 12 Tf 0 g");
        field.Owner.AcroForm.AddStandardFont("F A", MonoFamily);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    public void AFontSetOnTheFieldWinsOverTheOneDefaultAppearanceNames()
    {
        var field = Text(MonoFamily);
        field.Font = new XFont("Arial", 14);

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    public void AKeyThatDefaultResourcesDoNotHaveIsDrawnInTheDefaultFont()
    {
        var field = Text(MonoFamily, "/Missing 12 Tf 0 g");

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    public void ADefaultAppearanceWithNoFontIsDrawnInTheDefaultFont()
    {
        var field = Text(MonoFamily, "0 g");

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Sans());
    }

    [Test]
    public void TheFormsDefaultAppearanceNamesTheFontOfAFieldWithNoneOfItsOwn()
    {
        var field = Text(MonoFamily);
        field.Elements.Remove("/DA");
        field.Owner.AcroForm.DefaultAppearance = "/F1 12 Tf 0 g";

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    public void AnAncestorsDefaultAppearanceNamesTheFontOfAFieldWithNoneOfItsOwn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        form.AddStandardFont("F1", MonoFamily);
        var parent = new PdfTextField(document) { Name = "group" };
        form.Fields.Add(parent);
        parent.DefaultAppearance = "/F1 12 Tf 0 g";
        var child = new PdfTextField(document) { Name = "text" };
        parent.Fields.Add(child);

        var gfx = XGraphics.FromPdfPage(page);
        child.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(new XRect(60, 60, 200, 30))));
        gfx.Dispose();
        child.Text = "Hello";

        ShouldBeDrawnIn(child, Mono);
    }

    [Test]
    public void ADescendantFontOfATypeZeroFontIsReadForItsName()
    {
        var field = Text("Arial");
        var fonts = field.Owner.AcroForm.DefaultResources.Elements.GetDictionary("/Font");
        var type0 = fonts.Elements.GetDictionary("/F1");
        var descendant = new PdfDictionary(field.Owner);
        field.Owner.Internals.AddObject(descendant);
        descendant.Elements.SetName("/BaseFont", "/ABCDEF+" + MonoFamily);
        var descendants = new PdfArray(field.Owner);
        descendants.Elements.Add(descendant);
        type0.Elements.SetName("/Subtype", "/Type0");
        type0.Elements.SetName("/BaseFont", "/ABCDEF+" + MonoFamily + "-Identity-H");
        type0.Elements["/DescendantFonts"] = descendants;

        field.Text = "Hello";

        ShouldBeDrawnIn(field, Mono);
    }

    [Test]
    public void AComboBoxIsDrawnInTheFontDefaultAppearanceNames()
    {
        var combo = FieldDrawnIn("ABCDEF+" + MonoFamily,
            new PdfComboBoxField(new PdfDocument()) { Name = "combo", Options = ["Hello", "Two"] });

        combo.SelectedIndex = 0;

        ShouldBeDrawnIn(combo, Mono);
    }

    [Test]
    public void AListBoxIsDrawnInTheFontDefaultAppearanceNames()
    {
        var list = FieldDrawnIn("ABCDEF+" + MonoFamily,
            new PdfListBoxField(new PdfDocument()) { Name = "list", Options = ["Hello", "Two"] });

        list.SelectedIndex = 0;

        ShouldBeDrawnIn(list, Mono);
    }

    [Test]
    public void AutoSizingStillWorksInTheFontDefaultAppearanceNames()
    {
        var field = Text(BoldFamily, "/F1 0 Tf 0 g");

        field.Text = "Hello";

        ShouldBeDrawnIn(field, new XFont(BoldFamily, 12));
        field.Font.Size.Should().Be(10, "the size of an auto-sizing field is the appearance's, not the field's");
    }

    [Test]
    public void TheFontPropertyAnswersTheFontDefaultAppearanceNamesAtTheSizeItNames()
    {
        var field = Text("ABCDEF+" + MonoFamily, "/F1 9 Tf 0 g");

        field.Font.Size.Should().Be(9);
        field.Font.Name.Should().Be(PinnedFontResolver.CffFamilyName);
    }
}
