using System;
using System.Text;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A flattened form looks like the form did. Each test rasterizes a document with a form in it,
///   flattens it, rasterizes it again, and compares the two pages: before, the widgets are
///   annotations that Ghostscript draws from their appearance streams; after, they are page
///   content. Issue #355.
/// </summary>
/// <remarks>
///   The assertion that the page has ink on it comes first and matters as much as the comparison:
///   two blank pages are the same picture, and a widget whose appearance names the right keys and
///   draws nothing looks, to a test of the keys, exactly like one that works.
/// </remarks>
[Rasterizing]
public sealed class FormFlatteningRasterTests : IDisposable
{
    private const string OutDir = "Out/FormFlattening";

    /// <summary>
    ///   How far apart two pages may be, as a share of how far apart two pages can be at all, once
    ///   both are shrunk to a quarter. Glyph edges are the only thing a widget drawn as an annotation
    ///   and as content can disagree about; a widget a few points out of place is well above this.
    /// </summary>
    private const double Tolerance = 0.005;

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    private static PdfRectangle At(double x, double y, double width, double height) => new(new XRect(x, y, width, height));

    private static bool IsInk(IMagickColor<byte> c) => c.R < 200 || c.G < 200 || c.B < 200;

    /// <summary>
    ///   Rasterizes the document, flattens its form, rasterizes it again, and says the two are the
    ///   same picture and not a blank one.
    /// </summary>
    private void FlatteningChangesNothingYouCanSee(string name, PdfDocument document, bool redraw = false)
    {
        var before = _rasterized.FirstPageOf(document, name + "-before");
        PageInk.Count(before, IsInk).Should().BeGreaterThan(200, "the form has to show something for the comparison to mean anything");

        document.AcroForm.Flatten(redraw);

        var after = _rasterized.FirstPageOf(document, name + "-after");
        PageInk.Count(after, IsInk).Should().BeGreaterThan(200);
        Difference(before, after).Should().BeLessThan(Tolerance);
    }

    private static double Difference(IMagickImage<byte> first, IMagickImage<byte> second)
    {
        using var small = first.Clone();
        using var other = second.Clone();
        small.Resize(new Percentage(25));
        other.Resize(new Percentage(25));
        return small.Compare(other, ErrorMetric.RootMeanSquared);
    }

    private static (PdfDocument Document, PdfPage Page, PdfAcroForm Form) AForm()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        return (document, page, document.GetOrCreateAcroForm());
    }

    [Test, GoldenImage]
    public void ATextFieldLooksTheSame()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name", BorderColor = XColors.Black, BackColor = XColors.LightYellow };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 14 Tf 0 0 1 rg";
        field.AddWidget(page, At(60, 700, 240, 30));
        field.Text = "Ada Lovelace";

        FlatteningChangesNothingYouCanSee("text", document);
    }

    [Test, GoldenImage]
    public void ARedrawnTextFieldLooksTheSameAsTheOneItReplaces()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name", BorderColor = XColors.Black };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 14 Tf 0 g";
        field.AddWidget(page, At(60, 700, 240, 30));
        field.Text = "Ada Lovelace";

        FlatteningChangesNothingYouCanSee("text-redrawn", document, redraw: true);
    }

    [Test, GoldenImage]
    public void AComboBoxLooksTheSame()
    {
        var (document, page, form) = AForm();
        var field = new PdfComboBoxField(document) { Name = "country", Options = ["Australia", "Canada", "Ireland"] };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 14 Tf 0 g";
        field.BorderColor = XColors.Black;
        field.AddWidget(page, At(60, 700, 240, 30));
        field.SelectedIndex = 1;

        FlatteningChangesNothingYouCanSee("combo", document);
    }

    [Test, GoldenImage]
    public void AListBoxLooksTheSame()
    {
        var (document, page, form) = AForm();
        var field = new PdfListBoxField(document) { Name = "pets", Flags = PdfAcroFieldFlags.MultiSelect, Options = ["Cat", "Dog", "Emu", "Fox"] };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 14 Tf 0 g";
        field.BorderColor = XColors.Black;
        field.AddWidget(page, At(60, 600, 240, 90));
        field.SelectedIndices = [1, 2];

        FlatteningChangesNothingYouCanSee("list", document);
    }

    [Test, GoldenImage]
    public void ATickedCheckBoxLooksTheSame()
    {
        var (document, page, form) = AForm();
        var box = new PdfCheckBoxField(document) { Name = "tick" };
        form.Fields.Add(box);
        var widget = box.AddWidget(page, At(60, 700, 40, 40));
        var yes = new XForm(document, new XSize(40, 40));
        using (var gfx = XGraphics.FromForm(yes))
        {
            gfx.DrawRectangle(new XPen(XColors.Black, 2), 2, 2, 36, 36);
            gfx.DrawLine(new XPen(XColors.Red, 4), 8, 8, 32, 32);
            gfx.DrawLine(new XPen(XColors.Red, 4), 8, 32, 32, 8);
        }
        widget.SetAppearance("/Yes", yes);
        widget.SetAppearance("/Off", new XForm(document, new XSize(40, 40)));
        box.Checked = true;

        FlatteningChangesNothingYouCanSee("checkbox", document);
    }

    [Test, GoldenImage]
    public void ARadioGroupLooksTheSame()
    {
        var (document, page, form) = AForm();
        var group = new PdfRadioButtonField(document) { Name = "delivery", Options = ["Post", "Courier"] };
        form.Fields.Add(group);
        var buttons = new[] { group.AddWidget(page, At(60, 700, 30, 30)), group.AddWidget(page, At(60, 650, 30, 30)) };
        var states = new[] { "/Post", "/Courier" };
        for (var idx = 0; idx < buttons.Length; idx++)
        {
            var on = new XForm(document, new XSize(30, 30));
            using (var gfx = XGraphics.FromForm(on))
            {
                gfx.DrawEllipse(new XPen(XColors.Black, 2), 2, 2, 26, 26);
                gfx.DrawEllipse(XBrushes.DarkBlue, 8, 8, 14, 14);
            }
            var off = new XForm(document, new XSize(30, 30));
            using (var gfx = XGraphics.FromForm(off))
                gfx.DrawEllipse(new XPen(XColors.Black, 2), 2, 2, 26, 26);

            buttons[idx].SetAppearance(states[idx], on);
            buttons[idx].SetAppearance("/Off", off);
        }
        group.SelectedIndex = 1;
        buttons[0].AppearanceState = "Off";
        buttons[1].AppearanceState = "Courier";

        FlatteningChangesNothingYouCanSee("radio", document);
    }

    [Test, GoldenImage]
    public void AWidgetWhoseAppearanceHasARotatedMatrixLooksTheSame()
    {
        // A box 100 wide and 50 tall, red on its left half and blue on the lower right, turned a
        // quarter by the form's own /Matrix and mapped onto a rectangle that is not its shape.
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "turned" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, At(100, 600, 120, 200));

        var appearance = new PdfDictionary(document);
        document.Internals.AddObject(appearance);
        appearance.Elements.SetName("/Type", "/XObject");
        appearance.Elements.SetName("/Subtype", "/Form");
        appearance.Elements.SetRectangle("/BBox", At(0, 0, 100, 50));
        appearance.Elements.SetMatrix("/Matrix", new XMatrix(0, 1, -1, 0, 0, 0));
        appearance.CreateStream(Encoding.ASCII.GetBytes("1 0 0 rg 0 0 50 50 re f 0 0 1 rg 50 0 50 25 re f"));
        var ap = new PdfDictionary(document);
        ap.Elements["/N"] = appearance.Reference;
        widget.Elements["/AP"] = ap;

        FlatteningChangesNothingYouCanSee("matrix", document);
    }

    [Test, GoldenImage]
    public void AFormOnARotatedPageLooksTheSame()
    {
        var (document, page, form) = AForm();
        page.Rotate = 90;
        var field = new PdfTextField(document) { Name = "name", BorderColor = XColors.Black };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 14 Tf 0 g";
        field.AddWidget(page, At(60, 700, 240, 30));
        field.Text = "Ada Lovelace";

        FlatteningChangesNothingYouCanSee("rotated-page", document);
    }
}
