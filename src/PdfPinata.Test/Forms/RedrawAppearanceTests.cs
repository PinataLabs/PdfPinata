using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Forms;

/// <summary>
///   <see cref="PdfAcroField.RedrawAppearance"/> is asked for explicitly, so it is held to what an
///   explicit request is held to: it changes the document as setting the value does, and is refused
///   where setting the value is, and a redraw that cannot happen says so rather than doing nothing.
///   Issue #340, and the review of it.
/// </summary>
public class RedrawAppearanceTests
{
    private static PdfDocument ATextField(Action<PdfTextField> describe = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var field = new PdfTextField(document) { Name = "text" };
        document.GetOrCreateAcroForm().Fields.Add(field);
        field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        field.Text = "Ada Lovelace";
        describe?.Invoke(field);
        return document;
    }

    private static PdfTextField TheField(PdfDocument document) =>
        (PdfTextField)document.AcroForm.Fields["text"];

    [Test]
    [Arguments(PdfDocumentOpenMode.ReadOnly)]
    [Arguments(PdfDocumentOpenMode.Import)]
    public void ARedrawIsRefusedWhereSettingTheValueIs(PdfDocumentOpenMode mode)
    {
        var field = TheField(ATextField().Reopened(mode));

        var redraw = () => field.RedrawAppearance();
        var fill = () => field.Text = "Grace Hopper";

        redraw.Should().Throw<InvalidOperationException>().WithMessage("*" + mode + "*");
        fill.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ARedrawOfAReadOnlyFieldIsRefused()
    {
        var field = TheField(ATextField(f => f.ReadOnly = true).Reopened());

        var redraw = () => field.RedrawAppearance();

        redraw.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
    }

    [Test]
    public void ARedrawInADocumentOpenedToBeChangedIsAllowed()
    {
        var field = TheField(ATextField().Reopened(PdfDocumentOpenMode.Append));

        var redraw = () => field.RedrawAppearance();

        redraw.Should().NotThrow();
    }

    [Test]
    public void AFieldThatDrawsNoAppearanceOfItsOwnSaysSoWhenAskedToRedraw()
    {
        // A check box shows one of the states its widgets were given; nothing here draws one.
        var document = new PdfDocument();
        var page = document.AddPage();
        var box = new PdfCheckBoxField(document) { Name = "tick" };
        document.GetOrCreateAcroForm().Fields.Add(box);
        box.AddWidget(page, new PdfRectangle(new XRect(60, 700, 20, 20)));

        var redraw = () => box.RedrawAppearance();

        redraw.Should().Throw<InvalidOperationException>().WithMessage("*PdfCheckBoxField*");
    }

    [Test]
    public void AFieldInNoDocumentSaysSoWhenAskedToRedraw()
    {
        var field = new PdfTextField((PdfDocument)null);

        var redraw = () => field.RedrawAppearance();

        redraw.Should().Throw<InvalidOperationException>().WithMessage("*document*");
    }
}
