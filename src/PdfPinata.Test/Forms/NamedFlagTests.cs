using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   The field and annotation flags ISO 32000-1 defines and the enums used to leave out, so that
///   setting one meant casting an integer: <c>RadiosInUnison</c>, <c>RichText</c> and
///   <c>CommitOnSelChange</c> on a field, <c>LockedContents</c> on an annotation. Each is written
///   to <c>/Ff</c> or <c>/F</c> and read back from the file. Issue #337.
/// </summary>
public class NamedFlagTests
{
    [Test]
    public void TheFlagBitsAreTheOnesTheStandardNumbers()
    {
        ((int)PdfAcroFieldFlags.RadiosInUnison).Should().Be(1 << 25, "bit 26");
        ((int)PdfAcroFieldFlags.RichText).Should().Be(1 << 25, "bit 26");
        ((int)PdfAcroFieldFlags.CommitOnSelChange).Should().Be(1 << 26, "bit 27");
        ((int)PdfAnnotationFlags.LockedContents).Should().Be(1 << 9, "bit 10");
    }

    [Test]
    public void RadiosInUnisonSurvivesTheFileAndLeavesARadioFieldARadioField()
    {
        var (document, form) = AForm();
        var field = new PdfRadioButtonField(document) { Name = "choice" };
        form.Fields.Add(field);
        field.Flags = PdfAcroFieldFlags.RadiosInUnison;

        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Radio);
        var reread = document.Reopened().AcroForm.Fields["choice"];

        reread.Should().BeOfType<PdfRadioButtonField>();
        reread.Flags.Should().HaveFlag(PdfAcroFieldFlags.RadiosInUnison);
    }

    [Test]
    public void RichTextSurvivesTheFile()
    {
        var (document, form) = AForm();
        var field = new PdfTextField(document) { Name = "rich" };
        form.Fields.Add(field);
        field.Flags = PdfAcroFieldFlags.RichText;

        var reread = document.Reopened().AcroForm.Fields["rich"];

        reread.Should().BeOfType<PdfTextField>();
        reread.Flags.Should().HaveFlag(PdfAcroFieldFlags.RichText);
    }

    [Test]
    public void CommitOnSelChangeSurvivesTheFileOnAComboAndAListBox()
    {
        var (document, form) = AForm();
        var combo = new PdfComboBoxField(document) { Name = "combo" };
        var list = new PdfListBoxField(document) { Name = "list" };
        form.Fields.Add(combo);
        form.Fields.Add(list);
        combo.Flags = PdfAcroFieldFlags.CommitOnSelChange;
        list.Flags = PdfAcroFieldFlags.CommitOnSelChange;

        var reread = document.Reopened().AcroForm;

        reread.Fields["combo"].Should().BeOfType<PdfComboBoxField>()
            .Which.Flags.Should().HaveFlag(PdfAcroFieldFlags.CommitOnSelChange);
        reread.Fields["list"].Should().BeOfType<PdfListBoxField>()
            .Which.Flags.Should().HaveFlag(PdfAcroFieldFlags.CommitOnSelChange);
    }

    [Test]
    public void LockedContentsSurvivesTheFile()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Annotations.Add(new PdfTextAnnotation
        {
            Rectangle = new PdfRectangle(new XRect(10, 10, 20, 20)),
            Flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.LockedContents,
        });

        var reread = document.Reopened().Pages[0].Annotations[0];

        reread.Flags.Should().Be(PdfAnnotationFlags.Print | PdfAnnotationFlags.LockedContents);
    }

    private static (PdfDocument Document, PdfAcroForm Form) AForm()
    {
        var document = new PdfDocument();
        document.AddPage();
        return (document, document.GetOrCreateAcroForm());
    }
}
