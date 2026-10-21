using System;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   <see cref="PdfAnnotation.Title"/> and <see cref="PdfAnnotation.Subject"/> write <c>/T</c> and
///   <c>/Subj</c>, keys of a markup annotation. On a widget <c>/T</c> is a field's partial name, so
///   setting either there is refused rather than turning the widget into a nested field or renaming
///   the field it is merged with. Issue #330.
/// </summary>
public class WidgetMarkupKeyTests
{
    private static readonly PdfRectangle Box = new(new XRect(60, 700, 200, 20));

    private static (PdfDocument Document, PdfTextField Field, PdfWidgetAnnotation Widget) ATextFieldOnAPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        return (document, field, widget);
    }

    private static PdfDocument AMergedFieldAndWidget()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "merged" };
        form.Fields.Add(field);
        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");
        field.Elements.SetRectangle("/Rect", Box);
        field.Elements.SetReference("/P", page);
        page.Annotations.Elements.Add(field.Reference);
        return document.Reopened(PdfDocumentOpenMode.Modify);
    }

    [Test]
    public void SettingTitleOnAWidgetIsRefusedAndNamesWhatToUseInstead()
    {
        var (_, field, widget) = ATextFieldOnAPage();

        var act = () => widget.Title = "Jane";

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(nameof(PdfAcroField.Name)).And.Contain(nameof(PdfAcroField.ToolTip));
        widget.Elements.ContainsKey("/T").Should().BeFalse("the widget must stay widget-only");
        field.Widgets.Should().ContainSingle().Which.Should().BeSameAs(widget);
    }

    [Test]
    public void SettingSubjectOnAWidgetIsRefused()
    {
        var (_, _, widget) = ATextFieldOnAPage();

        var act = () => widget.Subject = "Comment";

        act.Should().Throw<InvalidOperationException>();
        widget.Elements.ContainsKey("/Subj").Should().BeFalse();
    }

    [Test]
    public void SettingTitleOnAMergedFieldsWidgetViewLeavesTheFieldsNameAlone()
    {
        var document = AMergedFieldAndWidget();
        var view = document.Pages[0].Annotations[0];
        view.Should().BeOfType<PdfWidgetAnnotation>();

        var act = () => view.Title = "a.b";

        act.Should().Throw<InvalidOperationException>();
        var form = document.AcroForm!;
        form.Fields.DescendantNames.Should().Equal("merged");
        form.Fields["merged"]!.Name.Should().Be("merged");
    }

    public static IEnumerable<Func<PdfDocument, PdfAnnotation>> MarkupAnnotations()
    {
        yield return d => new PdfTextAnnotation(d);
        yield return d => new PdfSquareAnnotation(d);
        yield return d => new PdfLineAnnotation(d);
        yield return d => new PdfFreeTextAnnotation(d);
    }

    [Test]
    [MethodDataSource(nameof(MarkupAnnotations))]
    public void TitleAndSubjectOnAMarkupAnnotationAreWrittenAsBefore(Func<PdfDocument, PdfAnnotation> make)
    {
        var document = new PdfDocument();
        document.AddPage();
        var annotation = make(document);

        annotation.Title = "Jane";
        annotation.Subject = "Review";

        annotation.Elements.GetString("/T").Should().Be("Jane");
        annotation.Elements.GetString("/Subj").Should().Be("Review");
        annotation.Title.Should().Be("Jane");
        annotation.Subject.Should().Be("Review");
    }

    [Test]
    public void AWidgetReadWithATitleStillReadsIt()
    {
        // Only the setter is guarded: a file whose widget already says /T is read as it was.
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "text" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, Box);
        widget.Elements.SetString("/T", "kid");
        widget.Elements.SetString("/Subj", "subject");

        var reopened = document.Reopened(PdfDocumentOpenMode.Modify);
        var annotation = reopened.Pages[0].Annotations[0];

        annotation.Title.Should().Be("kid");
        annotation.Subject.Should().Be("subject");
    }
}
