using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A combo box and a list box draw their own appearance, as a text field does. They used to
///   write their value and leave the drawing to the reader through <c>/NeedAppearances</c>, which
///   Ghostscript and most print pipelines ignore - so the value was set and invisible. Issue #151.
/// </summary>
/// <remarks>
///   The tests that matter count pixels. A widget whose appearance names the right keys and draws
///   nothing looks, to an assertion on the keys, exactly like one that works.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class ChoiceFieldAppearanceTests : IDisposable
{
    private const string OutDir = "Out/ChoiceFieldAppearances";

    /// <summary>Where each field is placed, in world space from the top left of an A4 page.</summary>
    private static readonly XRect Box = new(60, 60, 240, 72);

    private static readonly string[] Countries = ["Australia", "Canada", "Ireland", "New Zealand", "United Kingdom"];

    private readonly Rasterizations _rasterized = new(OutDir);

    static ChoiceFieldAppearanceTests()
    {
        GhostscriptSetup.Configure();
    }

    public void Dispose() => _rasterized.Dispose();

    private static (PdfDocument Document, T Field) OnAPage<T>(Func<PdfDocument, T> make, XRect? box = null)
        where T : PdfChoiceField
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = make(document);
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 12 Tf 0 g";

        var gfx = XGraphics.FromPdfPage(page);
        field.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box ?? Box)));
        gfx.Dispose();
        return (document, field);
    }

    private static PdfComboBoxField ACountryCombo(PdfDocument document) =>
        new(document) { Name = "country", Options = Countries };

    private static PdfListBoxField ACountryList(PdfDocument document) =>
        new(document) { Name = "countries", Flags = PdfAcroFieldFlags.MultiSelect, Options = Countries };

    [Fact]
    public void AComboBoxWithAValueHasAnAppearance()
    {
        var (_, combo) = OnAPage(ACountryCombo);

        combo.SelectedIndex = 4;

        combo.Widgets[0].Elements.GetDictionary(PdfAnnotation.Keys.AP).Should().NotBeNull();
    }

    [Fact]
    public void AComboBoxWithNothingToDrawHasNoAppearance()
    {
        // No value, no background and no border: an empty appearance would stop a reader
        // building one from /MK, so none is written.
        var (_, combo) = OnAPage(ACountryCombo);

        combo.Widgets[0].Elements.ContainsKey(PdfAnnotation.Keys.AP).Should().BeFalse();
    }

    [Fact]
    public void TheFontSizeAndColourComeFromTheDefaultAppearance()
    {
        var (_, combo) = OnAPage(ACountryCombo);

        combo.DefaultAppearance = "/Helv 9 Tf 1 0 0 rg";

        combo.Font.Size.Should().Be(9);
        combo.ForeColor.R.Should().Be(255);
        combo.ForeColor.G.Should().Be(0);
    }

    [Fact]
    public void AnAutoSizedDefaultAppearanceDrawsAtTenPoints()
    {
        var (_, combo) = OnAPage(ACountryCombo);

        combo.DefaultAppearance = "/Helv 0 Tf 0 g";

        combo.Font.Size.Should().Be(10);
    }

    [GoldenImageFact]
    public void AComboBoxShowsItsValue()
    {
        var page = Rasterize("combo-value", OnAPage(ACountryCombo), combo => combo.SelectedIndex = 4);

        Count(page, Box, IsInk).Should().BeGreaterThan(100, "the value is drawn in the box");
    }

    [GoldenImageFact]
    public void AComboBoxShowsATypedValueThatIsNotAnOption()
    {
        var (document, combo) = OnAPage(document => new PdfComboBoxField(document)
        {
            Name = "typed",
            Flags = PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.Edit
        });

        var page = Rasterize("combo-typed", (document, combo), field => field.Value = new PdfString("Iceland"));

        Count(page, Box, IsInk).Should().BeGreaterThan(100);
    }

    [GoldenImageFact]
    public void TheValuesColourIsTheOneTheDefaultAppearanceNames()
    {
        var page = Rasterize("combo-red", OnAPage(ACountryCombo), combo =>
        {
            combo.DefaultAppearance = "/Helv 12 Tf 1 0 0 rg";
            combo.SelectedIndex = 1;
        });

        Count(page, Box, IsRed).Should().BeGreaterThan(50);
        Count(page, Box, IsDark).Should().Be(0, "no black text is drawn");
    }

    [GoldenImageFact]
    public void AListBoxHighlightsEachChosenRow()
    {
        var page = Rasterize("list-two", OnAPage(ACountryList), list => list.SelectedIndices = [0, 3]);

        var highlighted = Count(page, Box, IsHighlight);
        highlighted.Should().BeGreaterThan(500, "two rows are highlighted");
        Count(page, Box, IsInk).Should().BeGreaterThan(200, "and every visible option is written");
    }

    [GoldenImageFact]
    public void AListBoxHighlightsNothingWhenNothingIsChosen()
    {
        var page = Rasterize("list-none", OnAPage(ACountryList), _ => { });

        Count(page, Box, IsHighlight).Should().Be(0);
        Count(page, Box, IsInk).Should().BeGreaterThan(200, "the options are still listed");
    }

    [GoldenImageFact]
    public void AListBoxHighlightsTwiceAsMuchForTwoRowsAsForOne()
    {
        var one = Count(Rasterize("list-one", OnAPage(ACountryList), list => list.SelectedIndices = [2]), Box, IsHighlight);
        var two = Count(Rasterize("list-pair", OnAPage(ACountryList), list => list.SelectedIndices = [1, 2]), Box, IsHighlight);

        two.Should().BeInRange((int)(one * 1.7), (int)(one * 2.3));
    }

    [GoldenImageFact]
    public void TheTopIndexScrollsTheList()
    {
        // The chosen option is the third. Shown from the top, its highlight is the third row;
        // scrolled so that it is the first option shown, its highlight is the top row.
        var topRow = new XRect(Box.X, Box.Y, Box.Width, 14);

        var unscrolled = Rasterize("list-unscrolled", OnAPage(ACountryList), list => list.SelectedIndices = [2]);
        var scrolled = Rasterize("list-scrolled", OnAPage(ACountryList), list =>
        {
            list.SelectedIndices = [2];
            list.TopIndex = 2;
        });

        Count(unscrolled, topRow, IsHighlight).Should().Be(0);
        Count(scrolled, topRow, IsHighlight).Should().BeGreaterThan(200);
    }

    [GoldenImageFact]
    public void TheBackgroundAndBorderComeFromTheWidgetsCharacteristics()
    {
        // A widget decorated through /MK alone - as the demo is, and as files from other
        // software are - keeps its box once the field draws itself.
        var page = Rasterize("mk", OnAPage(ACountryCombo), combo =>
        {
            combo.Widgets[0].Elements["/MK"] = new PdfDictionary(combo.Owner)
            {
                Elements = { ["/BG"] = new PdfArray(combo.Owner, new PdfReal(0), new PdfReal(0), new PdfReal(1)) }
            };
            combo.SelectedIndex = 0;
        });

        Count(page, Box, IsBlue).Should().BeGreaterThan(5000);
    }

    [GoldenImageFact]
    public void AColourSetOnTheFieldWinsOverTheCharacteristics()
    {
        var page = Rasterize("mk-overridden", OnAPage(ACountryCombo), combo =>
        {
            combo.Widgets[0].Elements["/MK"] = new PdfDictionary(combo.Owner)
            {
                Elements = { ["/BG"] = new PdfArray(combo.Owner, new PdfReal(0), new PdfReal(0), new PdfReal(1)) }
            };
            combo.BackColor = XColors.Gold;
        });

        Count(page, Box, IsBlue).Should().Be(0);
        Count(page, Box, IsGold).Should().BeGreaterThan(5000);
    }

    [GoldenImageFact]
    public void AFieldMergedWithItsWidgetDrawsIntoItself()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var combo = ACountryCombo(document);
        form.Fields.Add(combo);
        combo.DefaultAppearance = "/Helv 12 Tf 0 g";
        var gfx = XGraphics.FromPdfPage(page);
        combo.Elements.SetName("/Type", "/Annot");
        combo.Elements.SetName("/Subtype", "/Widget");
        combo.Elements.SetRectangle("/Rect", new PdfRectangle(gfx.Transformer.WorldToDefaultPage(Box)));
        gfx.Dispose();
        page.Annotations.Elements.Add(combo.Reference);

        var image = Rasterize("merged", (document, combo), field => field.SelectedIndex = 2);

        combo.Elements.GetDictionary(PdfAnnotation.Keys.AP).Should().NotBeNull();
        Count(image, Box, IsInk).Should().BeGreaterThan(100);
    }

    [Fact]
    public void ChangingTheSelectionRedrawsTheList()
    {
        var (_, list) = OnAPage(ACountryList);
        list.SelectedIndices = [0];
        var before = NormalAppearance(list).Stream.Value;

        list.SelectedIndices = [4];

        NormalAppearance(list).Stream.Value.Should().NotEqual(before);
    }

    [Fact]
    public void SettingAListBoxsValueRedrawsItAndReplacesItsIndices()
    {
        // Filling a form through Value - AcroForm.Fields[name].Value - is the ordinary way to fill
        // one read from a file, and it redrew nothing and left /I naming the old rows.
        var (_, list) = OnAPage(ACountryList);
        list.SelectedIndices = [0, 3];
        var before = NormalAppearance(list).Stream.Value;

        list.Value = new PdfString("Canada");

        list.SelectedIndices.Should().Equal(1);
        list.Elements.GetArray(PdfChoiceField.Keys.I).Elements.GetInteger(0).Should().Be(1);
        NormalAppearance(list).Stream.Value.Should().NotEqual(before);
    }

    [Fact]
    public void SettingATextFieldsValueRedrawsIt()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var text = new PdfTextField(document) { Name = "name", BorderColor = XColors.Gray };
        form.Fields.Add(text);
        text.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        var appearances = text.Widgets[0].Elements.GetDictionary(PdfAnnotation.Keys.AP);
        var before = ((PdfDictionary)appearances.Elements.GetObject("/N")).Stream.Value;

        text.Value = new PdfString("Ada Lovelace");

        appearances = text.Widgets[0].Elements.GetDictionary(PdfAnnotation.Keys.AP);
        ((PdfDictionary)appearances.Elements.GetObject("/N")).Stream.Value.Should().NotEqual(before);
    }

    [Theory]
    [InlineData("/Helv 9 Tf 1 rg")]
    [InlineData("/Helv 9 Tf 0 0 0 k")]
    [InlineData("/Helv 9 Tf 0 0 1 0 rg")]
    public void AColourOperatorWithTheWrongNumberOfOperandsIsReadAsBlack(string appearance)
    {
        // /DA comes from files, and this used to throw FormatException out of every setter that
        // redraws the field.
        var (_, combo) = OnAPage(ACountryCombo);
        combo.DefaultAppearance = appearance;

        combo.ForeColor.Should().Be(XColors.Black);
        var act = () => combo.SelectedIndex = 2;
        act.Should().NotThrow();
    }

    [Fact]
    public void AFieldReadFromAFileKeepsItsAppearanceUntilItIsChanged()
    {
        // Saving does not redraw: a form somebody else wrote keeps its own drawing unless the
        // caller changes the field.
        var (document, combo) = OnAPage(ACountryCombo);
        combo.SelectedIndex = 1;
        var drawn = NormalAppearance(combo).Stream.UnfilteredValue;

        var read = (PdfComboBoxField)Reopened(document).AcroForm.Fields["country"];
        NormalAppearance(read).Stream.UnfilteredValue.Should().Equal(drawn);

        var again = (PdfComboBoxField)Reopened(read.Owner).AcroForm.Fields["country"];
        NormalAppearance(again).Stream.UnfilteredValue.Should().Equal(drawn, "saving a read field does not redraw it");
    }

    private static PdfDocument Reopened(PdfDocument document)
    {
        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);
    }

    private static PdfDictionary NormalAppearance(PdfChoiceField field)
    {
        var appearances = field.Widgets[0].Elements.GetDictionary(PdfAnnotation.Keys.AP);
        return (PdfDictionary)appearances.Elements.GetObject("/N");
    }

    private IMagickImage<byte> Rasterize<T>(string name, (PdfDocument Document, T Field) placed, Action<T> arrange)
        where T : PdfChoiceField
    {
        arrange(placed.Field);

        return _rasterized.FirstPageOf(placed.Document, name);
    }

    /// <summary>The pixels inside a box given in world space, matching a test.</summary>
    private static int Count(IMagickImage<byte> image, XRect box, Func<IMagickColor<byte>, bool> match)
    {
        var scale = image.Width / PageSizeConverter.ToSize(PageSize.A4).Width;
        var left = (int)(box.X * scale);
        var top = (int)(box.Y * scale);
        var right = (int)Math.Ceiling(box.Right * scale);
        var bottom = (int)Math.Ceiling(box.Bottom * scale);

        using var pixels = image.GetPixels();
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var c = pixels.GetPixel(x, y).ToColor();
                if (c != null && match(c))
                    count++;
            }
        }
        return count;
    }

    private static bool IsInk(IMagickColor<byte> c) => c.R < 110 && c.G < 110 && c.B < 110;

    private static bool IsDark(IMagickColor<byte> c) => c.R < 60 && c.G < 60 && c.B < 60;

    private static bool IsRed(IMagickColor<byte> c) => c.R > 150 && c.G < 90 && c.B < 90;

    private static bool IsBlue(IMagickColor<byte> c) => c.B > 180 && c.R < 60 && c.G < 60;

    private static bool IsGold(IMagickColor<byte> c) => c.R > 200 && c.G > 170 && c.B < 60;

    /// <summary>Acrobat's list highlight, RGB 153 193 218, give or take antialiasing.</summary>
    private static bool IsHighlight(IMagickColor<byte> c) =>
        Math.Abs(c.R - 153) < 12 && Math.Abs(c.G - 193) < 12 && Math.Abs(c.B - 218) < 12;
}
