using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Opening a form and reading its fields needs no font face. <c>PdfTextField.Font</c> and
///   <c>PdfChoiceField.Font</c> are lazy, and nothing in <c>Pdf.AcroForms</c> asks the font resolver
///   for a face until an appearance is drawn - so a caller can list a form's fields and read their
///   values without a backend. These tests pin that (#351).
///   <para>
///   Why they are here, in a project about the document object model. The check needs a process
///   whose font resolver <em>throws</em> when asked for a face, and <c>GlobalFontSettings.FontResolver</c>
///   is process-wide and refuses to be replaced once any font has been created. <c>PdfPinata.Test</c>
///   registers a real resolver for the whole assembly, so it cannot swap one in without pulling the
///   floor out from under every test running beside it; every other test project registers a real
///   one too. This project registers <see cref="NamedFontsOnly"/>, which serves a font <em>name</em>
///   and throws from <c>ResolveTypeface</c> and <c>GetFont</c>, which is the oracle wanted, already
///   in place and shared by nothing that wants a face. It reaches PdfPinata through the DOM
///   reference, and the fields are read through public API alone, as a consumer reads them.
///   </para>
///   <para>
///   The fixture is written as bytes rather than built through the API, because the API draws:
///   setting <c>Text</c> on a field renders its appearance and so asks for a face. Nothing here
///   sets a value, it only reads the ones the file carries. Do not read <c>Font</c> in these tests
///   either; that is the one getter that does resolve a face, by design, and
///   <see cref="TheResolverInThisProcessRefusesToResolveAFace"/> shows the oracle would catch it.
///   </para>
/// </summary>
public class ReadingAFormNeedsNoFontTests
{
    // Object numbers of the fields are not asserted anywhere: the names are what a caller sees.
    private static byte[] FormWithOneOfEveryKindOfField()
    {
        // Merged field/widget dictionaries throughout, as most files in the wild write them. The
        // radio group is the exception, with widgets of its own under /Kids, and "Address" has
        // fields under /Kids, so DescendantNames has a tree to walk.
        var objects = new List<string>
        {
            // 1
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 12 0 R 13 0 R] "
            + "/DA (/Helv 12 Tf 0 g) >> >>",
            // 2
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            // 3
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + "/Annots [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 14 0 R 15 0 R 16 0 R 17 0 R 18 0 R] >>",
            // 4: unused, keeps the field numbers easy to read against the list above.
            "<< >>",
            // 5: text, with a /DA of its own.
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /V (Jane Doe) /DA (/Helv 9 Tf 0 g) "
            + "/Rect [20 700 220 720] >>",
            // 6: text, read only, taking the form's /DA.
            "<< /Type /Annot /Subtype /Widget /FT /Tx /Ff 1 /T (Locked) /V (fixed) /Rect [20 670 220 690] >>",
            // 7: text, with a /DA size of zero (auto-size), the one size an eager font would have to special-case.
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Auto) /V (anything) /DA (/Helv 0 Tf 0 g) "
            + "/Rect [20 640 220 660] >>",
            // 8: combo box, flag 1 << 17.
            "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (Colour) /Opt [(Red) (Green) (Blue)] /V (Green) "
            + "/Rect [20 610 220 630] >>",
            // 9: list box.
            "<< /Type /Annot /Subtype /Widget /FT /Ch /T (Size) /Opt [(Small) (Medium) (Large)] /V (Large) "
            + "/Rect [20 520 220 600] >>",
            // 10: check box, ticked.
            "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /V /Yes /AS /Yes /Rect [20 490 40 510] >>",
            // 11: check box, not ticked.
            "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Newsletter) /V /Off /AS /Off /Rect [20 460 40 480] >>",
            // 12: radio group, flag 1 << 15, its widgets under /Kids.
            "<< /FT /Btn /Ff 32768 /T (Delivery) /Opt [(post) (courier) (collect)] /V /courier /Kids [14 0 R 15 0 R 16 0 R] >>",
            // 13: a parent with fields of its own, itself of no kind.
            "<< /T (Address) /Kids [17 0 R 18 0 R] >>",
            // 14, 15, 16: the radio widgets.
            "<< /Type /Annot /Subtype /Widget /Parent 12 0 R /AS /Off /Rect [20 430 40 450] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 12 0 R /AS /courier /Rect [60 430 80 450] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 12 0 R /AS /Off /Rect [100 430 120 450] >>",
            // 17, 18: the fields under "Address".
            "<< /Type /Annot /Subtype /Widget /Parent 13 0 R /FT /Tx /T (Street) /V (1 Main St) "
            + "/Rect [20 400 220 420] >>",
            "<< /Type /Annot /Subtype /Widget /Parent 13 0 R /FT /Tx /T (City) /V (Springfield) "
            + "/Rect [20 370 220 390] >>",
        };

        var text = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var number = 1; number <= objects.Count; number++)
        {
            offsets.Add(text.Length);
            text.Append(number).Append(" 0 obj\n").Append(objects[number - 1]).Append("\nendobj\n");
        }

        var xref = text.Length;
        text.Append("xref\n0 ").Append(objects.Count + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            text.Append(offset.ToString("D10")).Append(" 00000 n \n");
        text.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\n");
        text.Append("startxref\n").Append(xref).Append("\n%%EOF\n");

        // One char per byte, as the library reads.
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    private static PdfDocument OpenTheForm() =>
        PdfReader.Open(new MemoryStream(FormWithOneOfEveryKindOfField()), PdfDocumentOpenMode.ReadOnly);

    private static string TextOf(PdfItem item) => item switch
    {
        PdfString s => s.Value,
        PdfName n => n.Value,
        _ => throw new InvalidOperationException($"Expected a string or a name, found {item?.GetType().Name ?? "nothing"}."),
    };

    // ----- the oracle -----------------------------------------------------------------------------

    [Test]
    public void TheResolverInThisProcessRefusesToResolveAFace()
    {
        // Without this the tests below could pass because nothing here ever bites. Reading Font is
        // the one thing on a field that does ask for a face, so it must throw from the resolver.
        using var document = OpenTheForm();
        var field = (PdfTextField)document.AcroForm.Fields["Name"];

        GlobalFontSettings.FontResolver.Should().BeOfType<NamedFontsOnly>();
        var read = () => field.Font;
        read.Should().Throw<NotSupportedException>().WithMessage("*Unresolved Test Face*");
    }

    // ----- reading -------------------------------------------------------------------------------

    [Test]
    public void TheNamesOfEveryFieldCanBeListed()
    {
        using var document = OpenTheForm();

        document.AcroForm.Fields.DescendantNames.Should().Equal(
            "Name", "Locked", "Auto", "Colour", "Size", "Agree", "Newsletter", "Delivery",
            "Address.Street", "Address.City");
    }

    [Test]
    public void EveryFieldIsTheClassItsKindNames()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        fields["Name"].Should().BeOfType<PdfTextField>();
        fields["Locked"].Should().BeOfType<PdfTextField>();
        fields["Auto"].Should().BeOfType<PdfTextField>();
        fields["Colour"].Should().BeOfType<PdfComboBoxField>();
        fields["Size"].Should().BeOfType<PdfListBoxField>();
        fields["Agree"].Should().BeOfType<PdfCheckBoxField>();
        fields["Newsletter"].Should().BeOfType<PdfCheckBoxField>();
        fields["Delivery"].Should().BeOfType<PdfRadioButtonField>();
        fields["Address.Street"].Should().BeOfType<PdfTextField>();
    }

    [Test]
    public void ATextFieldGivesItsValueWhateverItsDefaultAppearanceSaysOfSize()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        // DefaultAppearance answers the field's own /DA: one naming a size of nine, none (the form's
        // twelve applies to the field but is not the field's to report), and one naming a size of zero,
        // which asks for auto-sizing. Reading them must not build the font they would size.
        var named = (PdfTextField)fields["Name"];
        TextOf(named.Value).Should().Be("Jane Doe");
        named.Text.Should().Be("Jane Doe");
        named.DefaultAppearance.Should().Be("/Helv 9 Tf 0 g");

        var inherited = (PdfTextField)fields["Locked"];
        TextOf(inherited.Value).Should().Be("fixed");
        inherited.Text.Should().Be("fixed");
        inherited.DefaultAppearance.Should().BeEmpty();

        var auto = (PdfTextField)fields["Auto"];
        TextOf(auto.Value).Should().Be("anything");
        auto.Text.Should().Be("anything");
        auto.DefaultAppearance.Should().Be("/Helv 0 Tf 0 g");
    }

    [Test]
    public void AFieldsFlagsAndReadOnlyAreReadFromTheFile()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        fields["Name"].ReadOnly.Should().BeFalse();
        fields["Locked"].ReadOnly.Should().BeTrue();
        fields["Locked"].Flags.Should().HaveFlag(PdfAcroFieldFlags.ReadOnly);
        fields["Colour"].Flags.Should().HaveFlag(PdfAcroFieldFlags.Combo);
        fields["Colour"].ReadOnly.Should().BeFalse();
        fields["Size"].Flags.Should().NotHaveFlag(PdfAcroFieldFlags.Combo);
        fields["Delivery"].Flags.Should().HaveFlag(PdfAcroFieldFlags.Radio);
        fields["Agree"].Flags.Should().NotHaveFlag(PdfAcroFieldFlags.Radio);
    }

    [Test]
    public void AComboBoxAndAListBoxGiveTheirValueAndTheirSelection()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        var combo = (PdfComboBoxField)fields["Colour"];
        TextOf(combo.Value).Should().Be("Green");
        combo.SelectedIndex.Should().Be(1);
        combo.Options.Should().Equal("Red", "Green", "Blue");
        combo.DefaultAppearance.Should().BeEmpty();

        var list = (PdfListBoxField)fields["Size"];
        TextOf(list.Value).Should().Be("Large");
        list.SelectedIndex.Should().Be(2);
        list.SelectedIndices.Should().Equal(2);
        list.Options.Should().Equal("Small", "Medium", "Large");
    }

    [Test]
    public void ACheckBoxAndARadioGroupGiveTheirStateAndTheirSelection()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        var agree = (PdfCheckBoxField)fields["Agree"];
        TextOf(agree.Value).Should().Be("/Yes");
        agree.Checked.Should().BeTrue();

        var newsletter = (PdfCheckBoxField)fields["Newsletter"];
        TextOf(newsletter.Value).Should().Be("/Off");
        newsletter.Checked.Should().BeFalse();

        var delivery = (PdfRadioButtonField)fields["Delivery"];
        TextOf(delivery.Value).Should().Be("/courier");
        delivery.SelectedIndex.Should().Be(1);
        delivery.Options.Should().Equal("post", "courier", "collect");
    }

    [Test]
    public void AFieldNestedUnderAParentIsReadAsOneUnderTheTopOfTheForm()
    {
        using var document = OpenTheForm();
        var fields = document.AcroForm.Fields;

        TextOf(((PdfTextField)fields["Address.Street"]).Value).Should().Be("1 Main St");
        ((PdfTextField)fields["Address.City"]).Text.Should().Be("Springfield");
    }
}
