using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Forms;

/// <summary>
///   <see cref="PdfAcroField.PdfAcroFieldCollection"/> is both a form's <c>/Fields</c> and a
///   field's <c>/Kids</c>. Its indexer hands back typed fields; enumerating it - with
///   <c>foreach</c>, as <see cref="IEnumerable{T}"/> of <see cref="PdfItem"/>, which is what LINQ
///   sees, or as plain <see cref="IEnumerable"/> - has to hand back the same objects, not the
///   references underneath them.
/// </summary>
public class AcroFieldCollectionEnumerationTests
{
    private static PdfAcroField.PdfAcroFieldCollection AFormWithThreeFields()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();
        form.Fields.Add(new PdfTextField(document) { Name = "text" });
        form.Fields.Add(new PdfCheckBoxField(document) { Name = "check" });
        form.Fields.Add(new PdfComboBoxField(document) { Name = "combo" });
        return form.Fields;
    }

    private static List<PdfAcroField> ByIndex(PdfAcroField.PdfAcroFieldCollection fields)
    {
        var byIndex = new List<PdfAcroField>();
        for (var idx = 0; idx < fields.Count; idx++)
            byIndex.Add(fields[idx]);
        return byIndex;
    }

    private static void EveryEnumerationYieldsWhatTheIndexerDoes(PdfAcroField.PdfAcroFieldCollection fields)
    {
        var byIndex = ByIndex(fields);

        var typed = new List<PdfAcroField>();
        foreach (var field in fields)
            typed.Add(field);
        typed.Should().Equal(byIndex);

        var untyped = new List<object>();
        foreach (var field in (IEnumerable)fields)
            untyped.Add(field);
        untyped.Should().Equal(byIndex);

        var asArray = new List<PdfItem>();
        foreach (var item in (PdfArray)fields)
            asArray.Add(item);
        asArray.Should().Equal(byIndex);

        fields.OfType<PdfAcroField>().Should().Equal(byIndex);
        fields.Cast<PdfAcroField>().Should().Equal(byIndex);
        fields.OfType<PdfReference>().Should().BeEmpty();
    }

    [Fact]
    public void AFormCountsItsFields()
    {
        var fields = AFormWithThreeFields();

        fields.Count.Should().Be(3);
        fields.Count.Should().Be(fields.Elements.Count);
    }

    [Fact]
    public void AFormsFieldsEnumerateAsTheIndexerGivesThem()
    {
        var fields = AFormWithThreeFields();

        EveryEnumerationYieldsWhatTheIndexerDoes(fields);
        fields.OfType<PdfAcroField>().Select(field => field.Name).Should().Equal("text", "check", "combo");
    }

    [Fact]
    public void AFieldsKidsEnumerateAsTheIndexerGivesThem()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();
        var group = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(group);
        var first = new PdfTextField(document) { Name = "first" };
        var last = new PdfTextField(document) { Name = "last" };
        group.Fields.Add(first);
        group.Fields.Add(last);

        group.Fields.Count.Should().Be(2);
        group.Fields.OfType<PdfAcroField>().Should().Equal(first, last);
        EveryEnumerationYieldsWhatTheIndexerDoes(group.Fields);
    }

    [Fact]
    public void FieldsReadFromAFileEnumerateAsTheIndexerGivesThem()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var text = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(text);
        text.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        var group = new PdfTextField(document) { Name = "group" };
        form.Fields.Add(group);
        group.Fields.Add(new PdfTextField(document) { Name = "inner" });
        form.Fields.Add(new PdfCheckBoxField(document) { Name = "check" });

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var fields = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).AcroForm.Fields;

        // Read back, nothing is typed until it is asked for: the enumeration is what gives each
        // field its class, exactly as the indexer does, and the second pass sees the same objects.
        fields.Count.Should().Be(3);
        fields.OfType<PdfAcroField>().Select(field => field.GetType())
            .Should().Equal(typeof(PdfTextField), typeof(PdfTextField), typeof(PdfCheckBoxField));
        EveryEnumerationYieldsWhatTheIndexerDoes(fields);

        var reopenedGroup = fields[1];
        reopenedGroup.Fields.Count.Should().Be(1);
        reopenedGroup.Fields.OfType<PdfTextField>().Single().Name.Should().Be("inner");
        EveryEnumerationYieldsWhatTheIndexerDoes(reopenedGroup.Fields);
    }

    [Fact]
    public void MakingTheFormReadOnlyReachesEveryRootField()
    {
        var fields = AFormWithThreeFields();
        var document = fields.Owner;

        document.MakeAcroFormsReadOnly();

        fields.Should().OnlyContain(item => ((PdfAcroField)item).ReadOnly);
    }
}
