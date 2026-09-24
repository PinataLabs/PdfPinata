using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   A check box whose widgets are annotations of their own under <c>/Kids</c>. The state is the
///   field's <c>/V</c> and nothing else; each widget shows it, by an <c>/AS</c> that is the value
///   when its appearances offer that state and <c>/Off</c> when they do not (ISO 32000-1 section
///   12.7.4.2.3). So a box drawn in two places is ticked in both, and a pair whose widgets name
///   different on states is ticked in the first alone.
///   <para>
///   This replaces a scheme the setter's own comment called "fields that exist twice with the same
///   name": a field with two widgets was ticked by turning the first on and the second off,
///   unticked the other way round, and given no value of its own - so what a reader showed and
///   what <c>Checked</c> read disagreed. Issue #146.
///   </para>
/// </summary>
public class CheckBoxWidgetStateTests
{
    /// <summary>A tick box whose widgets each offer the given on state and /Off.</summary>
    private static PdfCheckBoxField ATickBox(params string[] onStates) =>
        (PdfCheckBoxField)new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree", onStates
                .Select(onState => new System.Action<PdfDictionary>(
                    kid => AcroFormBuilder.WithOnAndOffAppearances(kid, onState)))
                .ToArray())
            .Build().AcroForm.Fields["agree"];

    /// <summary>The value and appearance state of one of the field's widgets.</summary>
    private static (string Value, string Appearance) WidgetState(PdfAcroField field, int index)
    {
        var widget = field.Widgets[index];
        return (widget.Elements.GetName(PdfAcroField.Keys.V), widget.Elements.GetName(PdfAnnotation.Keys.AS));
    }

    [Fact]
    public void ABoxDrawnTwiceIsOneFieldWithTwoWidgets()
    {
        var field = ATickBox("/Yes", "/Yes");

        field.Widgets.Should().HaveCount(2);
        field.Fields.Should().BeEmpty();
        field.Checked.Should().BeFalse("nothing has been set");
    }

    [Fact]
    public void TickingABoxDrawnTwiceTicksItInBothPlaces()
    {
        var field = ATickBox("/Ja", "/Ja");

        field.Checked = true;

        field.Checked.Should().BeTrue();
        field.Value.Should().BeOfType<PdfName>().Which.Value.Should().Be("/Ja");
        WidgetState(field, 0).Should().Be(("", "/Ja"), "the value is the field's, and the widget shows it");
        WidgetState(field, 1).Should().Be(("", "/Ja"));
    }

    [Fact]
    public void UntickingABoxDrawnTwiceClearsItInBothPlaces()
    {
        var field = ATickBox("/Ja", "/Ja");
        field.Checked = true;

        field.Checked = false;

        field.Checked.Should().BeFalse();
        field.Value.Should().BeOfType<PdfName>().Which.Value.Should().Be("/Off");
        WidgetState(field, 0).Should().Be(("", "/Off"));
        WidgetState(field, 1).Should().Be(("", "/Off"));
    }

    [Fact]
    public void ABoxCanBeTickedAndUntickedRepeatedlyWithoutDrifting()
    {
        var field = ATickBox("/Yes", "/Yes");

        for (var round = 0; round < 3; round++)
        {
            field.Checked = true;
            field.Checked.Should().BeTrue("round {0}", round);
            field.Checked = false;
            field.Checked.Should().BeFalse("round {0}", round);
        }
    }

    [Fact]
    public void WhatWasSetSurvivesBeingWrittenOutAndReadBack()
    {
        var field = ATickBox("/Ja", "/Ja");
        field.Checked = true;

        using var saved = new MemoryStream();
        field.Owner.Save(saved, false);
        saved.Position = 0;
        var reread = (PdfCheckBoxField)PdfPinata.Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Modify).AcroForm.Fields["agree"];

        reread.Checked.Should().BeTrue();
        reread.Elements.GetName(PdfAcroField.Keys.V).Should().Be("/Ja");
        WidgetState(reread, 0).Should().Be(("", "/Ja"));
        WidgetState(reread, 1).Should().Be(("", "/Ja"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void AnyNumberOfWidgetsFollowsTheValue(int count)
    {
        // One widget and three used to be handled differently from two - three not at all.
        var field = ATickBox(Enumerable.Repeat("/Yes", count).ToArray());

        field.Checked = true;

        field.Checked.Should().BeTrue();
        for (var index = 0; index < count; index++)
            WidgetState(field, index).Should().Be(("", "/Yes"), "widget {0} shows the value", index);
    }

    [Fact]
    public void WidgetsNamingDifferentOnStatesAreTickedByTheFirstsName()
    {
        // The value names one state. A widget that has no appearance for it shows /Off, which is
        // what makes such a pair behave like a pair of radio buttons in a reader.
        var field = ATickBox("/Yes", "/Ja");

        field.CheckedName.Should().Be("/Yes");
        field.Checked = true;

        field.Elements.GetName(PdfAcroField.Keys.V).Should().Be("/Yes");
        WidgetState(field, 0).Should().Be(("", "/Yes"));
        WidgetState(field, 1).Should().Be(("", "/Off"));
    }

    [Fact]
    public void AWidgetWithNoAppearanceIsGivenNoState()
    {
        var field = (PdfCheckBoxField)new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid => AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja"),
                _ => { })
            .Build().AcroForm.Fields["agree"];

        field.Checked = true;

        WidgetState(field, 0).Should().Be(("", "/Ja"));
        WidgetState(field, 1).Should().Be(("", ""), "there was no state to give it");
        field.Checked.Should().BeTrue();
    }

    [Fact]
    public void AFileWithNoValueAnywhereIsReadByItsWidgets()
    {
        // What the replaced scheme wrote, and what some other software writes: the appearance
        // states set, and no /V on the field.
        var ticked = (PdfCheckBoxField)new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid =>
                {
                    AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja");
                    kid.Elements.SetName(PdfAcroField.Keys.V, "/Ja");
                    kid.Elements.SetName(PdfAnnotation.Keys.AS, "/Ja");
                },
                kid =>
                {
                    AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja");
                    kid.Elements.SetName(PdfAnnotation.Keys.AS, "/Off");
                })
            .Build().AcroForm.Fields["agree"];

        ticked.Checked.Should().BeTrue("a widget shows an on state");
    }

    [Fact]
    public void SettingTheStateClearsAValueAWidgetCarried()
    {
        var field = (PdfCheckBoxField)new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid =>
                {
                    AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja");
                    kid.Elements.SetName(PdfAcroField.Keys.V, "/Ja");
                })
            .Build().AcroForm.Fields["agree"];

        field.Checked = false;

        field.Checked.Should().BeFalse();
        WidgetState(field, 0).Should().Be(("", "/Off"), "a widget's /V is read by nothing and would contradict the field's");
    }

    [Fact]
    public void TheValueIsInheritedFromAParent()
    {
        // /V is inheritable (ISO 32000-1 Table 220), so a box whose value is said once, on the
        // field above it, is as ticked as one that says it itself.
        var document = new AcroFormBuilder()
            .WithDescribedParent("group",
                parent =>
                {
                    parent.Elements.SetName(PdfAcroField.Keys.FT, "/Btn");
                    parent.Elements.SetName(PdfAcroField.Keys.V, "/Yes");
                },
                kid =>
                {
                    kid.Elements.SetString(PdfAcroField.Keys.T, "box");
                    AcroFormBuilder.WithOnAndOffAppearances(kid);
                })
            .Build();

        ((PdfCheckBoxField)document.AcroForm.Fields["group.box"]).Checked.Should().BeTrue();
    }
}
