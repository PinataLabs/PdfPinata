using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using Xunit;

namespace PdfPinata.Test.Forms;

/// <summary>
///   <see cref="PdfAcroField.BackColor"/>, <see cref="PdfAcroField.BorderColor"/> and
///   <see cref="PdfPushButtonField.Caption"/> are written to each widget's <c>/MK</c> as well as
///   drawn. <c>/MK</c> is what a viewer builds a field from when it does not show the appearance
///   stream - pdf.js always, for text and choice fields; Acrobat while a field has the focus; and
///   every viewer honouring <c>/NeedAppearances</c> - so colours drawn and not written there
///   disappeared in all of them. Issue #153.
/// </summary>
public class AppearanceCharacteristicsTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    private static (PdfDocument Document, PdfPage Page, PdfAcroForm Form) AForm()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        return (document, page, document.GetOrCreateAcroForm());
    }

    private static PdfDictionary Characteristics(PdfDictionary widget) =>
        widget.Elements.GetDictionary("/MK");

    private static double[] Components(PdfDictionary widget, string key)
    {
        var array = Characteristics(widget).Elements.GetArray(key);
        var components = new double[array.Elements.Count];
        for (var index = 0; index < components.Length; index++)
            components[index] = array.Elements.GetReal(index);
        return components;
    }

    [Fact]
    public void ABackgroundIsWrittenToTheWidgetsCharacteristics()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);

        field.BackColor = XColor.FromArgb(255, 0, 0);

        Components(widget, "/BG").Should().Equal(1, 0, 0);
    }

    [Fact]
    public void EachColourSpaceIsWrittenWithItsOwnNumberOfComponents()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);

        field.BackColor = XColor.FromGrayScale(0.25);
        Components(widget, "/BG").Should().Equal(0.25);

        field.BackColor = XColor.FromCmyk(0.1, 0.2, 0.3, 0.4);
        Components(widget, "/BG").Should().Equal(0.1, 0.2, 0.3, 0.4);
    }

    [Fact]
    public void ABorderIsWrittenWithTheWidthItIsDrawnAt()
    {
        var (document, page, form) = AForm();
        var field = new PdfComboBoxField(document) { Name = "choice", Options = ["a", "b"] };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);

        field.BorderColor = XColors.Gray;

        Components(widget, "/BC").Should().HaveCount(3);
        var style = widget.Elements.GetDictionary("/BS");
        style.Elements.GetInteger("/W").Should().Be(1);
        style.Elements.GetName("/S").Should().Be("/S");
    }

    [Fact]
    public void AColourSetBeforeTheFieldIsPlacedIsGivenToItsWidget()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        field.BackColor = XColors.White;
        field.BorderColor = XColors.Black;

        var widget = field.AddWidget(page, Box);

        Components(widget, "/BG").Should().Equal(1, 1, 1);
        Components(widget, "/BC").Should().Equal(0, 0, 0);
    }

    [Fact]
    public void EveryWidgetOfARadioGroupIsGivenTheColours()
    {
        var (document, page, form) = AForm();
        var group = new PdfRadioButtonField(document) { Name = "delivery", Flags = PdfAcroFieldFlags.Radio };
        form.Fields.Add(group);
        var first = group.AddWidget(page, new PdfRectangle(new XRect(60, 700, 14, 14)));
        group.BorderColor = XColors.Gray;
        var second = group.AddWidget(page, new PdfRectangle(new XRect(160, 700, 14, 14)));

        Components(first, "/BC").Should().HaveCount(3);
        Components(second, "/BC").Should().HaveCount(3, "a widget added afterwards is given it too");
    }

    [Fact]
    public void ClearingAColourRemovesItAndAnEmptyCharacteristicsDictionary()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        field.BackColor = XColors.White;

        field.BackColor = XColor.Empty;

        widget.Elements.ContainsKey("/MK").Should().BeFalse();
    }

    [Fact]
    public void SettingOneColourLeavesTheRestOfTheCharacteristicsAlone()
    {
        // A widget read from a file carries /MK entries of its own, and only what is set is
        // written.
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        var existing = new PdfDictionary(document);
        existing.Elements["/BC"] = new PdfArray(document, new PdfReal(0.5));
        existing.Elements.SetInteger("/R", 90);
        widget.Elements["/MK"] = existing;

        field.BackColor = XColors.White;

        Components(widget, "/BC").Should().Equal(0.5);
        Characteristics(widget).Elements.GetInteger("/R").Should().Be(90);
        Components(widget, "/BG").Should().Equal(1, 1, 1);
    }

    [Fact]
    public void AFieldMergedWithItsWidgetCarriesTheCharacteristicsItself()
    {
        var (document, page, form) = AForm();
        var field = new PdfTextField(document) { Name = "merged" };
        form.Fields.Add(field);
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetRectangle("/Rect", Box);
        page.Annotations.Elements.Add(field.Reference);

        field.BorderColor = XColors.Black;

        Components(field, "/BC").Should().Equal(0, 0, 0);
    }

    [Fact]
    public void APushButtonsCaptionIsWrittenToEachWidget()
    {
        var (document, page, form) = AForm();
        var button = new PdfPushButtonField(document) { Name = "help", Caption = "Read the manual" };
        form.Fields.Add(button);

        var widget = button.AddWidget(page, Box);

        Characteristics(widget).Elements.GetString("/CA").Should().Be("Read the manual");
        button.Caption.Should().Be("Read the manual");
    }

    [Fact]
    public void AnEmptyCaptionRemovesIt()
    {
        var (document, page, form) = AForm();
        var button = new PdfPushButtonField(document) { Name = "help" };
        form.Fields.Add(button);
        var widget = button.AddWidget(page, Box);
        button.Caption = "Go";

        button.Caption = "";

        widget.Elements.ContainsKey("/MK").Should().BeFalse();
        button.Caption.Should().BeEmpty();
    }

    [Fact]
    public void TheCharacteristicsSurviveBeingWrittenOutAndReadBack()
    {
        var (document, page, form) = AForm();
        var button = new PdfPushButtonField(document)
        {
            Name = "help",
            Caption = "Read the manual",
            BackColor = XColor.FromArgb(217, 227, 240),
            BorderColor = XColor.FromArgb(89, 115, 153)
        };
        form.Fields.Add(button);
        button.AddWidget(page, Box);

        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var read = (PdfPushButtonField)PdfPinata.Pdf.IO.PdfReader
            .Open(stream, PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify).AcroForm.Fields["help"];

        read.Caption.Should().Be("Read the manual", "a caption read from a file is the widget's");
        var widget = read.Widgets[0];
        Components(widget, "/BG").Should().HaveCount(3);
        Components(widget, "/BC").Should().HaveCount(3);
        widget.Elements.GetDictionary("/BS").Elements.GetInteger("/W").Should().Be(1);
    }
}
