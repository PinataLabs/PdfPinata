using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   <c>/V</c> is inheritable (ISO 32000-1 Table 220): a field that has none takes its nearest
///   ancestor's. Only the check box read it that way; every other kind of field read its own entry
///   alone, so a child leaving its value to its parent read as empty and drew itself empty.
///   Issue #334.
/// </summary>
public class AcroFormInheritedValueTests
{
    /// <summary>
    ///   A grouping parent carrying the type, the flags and <paramref name="parentValue"/>, with
    ///   one child, <c>kid</c>, that carries none of them but whatever <paramref name="describeKid"/>
    ///   gives it.
    /// </summary>
    private static PdfDocument AGroup(string fieldType, PdfAcroFieldFlags flags, PdfItem parentValue,
        System.Action<PdfDictionary> describeKid = null) =>
        new AcroFormBuilder()
            .WithDescribedParent("group",
                parent =>
                {
                    parent.Elements.SetName(PdfAcroField.Keys.FT, fieldType);
                    parent.Elements.SetString(PdfAcroField.Keys.DA, "/Helv 10 Tf 0 g");
                    AcroFormBuilder.WithFlags(parent, flags);
                    parent.Elements[PdfAcroField.Keys.V] = parentValue;
                },
                kid =>
                {
                    kid.Elements.SetString(PdfAcroField.Keys.T, "kid");
                    describeKid?.Invoke(kid);
                })
            .Build();

    private static T Kid<T>(PdfDocument document) where T : PdfAcroField =>
        (T)document.AcroForm.Fields["group.kid"];

    private static PdfDictionary Parent(PdfDocument document) => document.AcroForm.Fields["group"];

    [Test]
    public void ATextFieldWithNoValueOfItsOwnReadsItsParents()
    {
        var document = AGroup("/Tx", 0, new PdfString("abc"));

        var kid = Kid<PdfTextField>(document);

        kid.Text.Should().Be("abc");
        ((PdfString)kid.Value).Value.Should().Be("abc");
    }

    [Test]
    public void ATextFieldDrawsTheValueItInherits()
    {
        // The same field twice over, once inheriting "abc" and once holding it, each redrawn by
        // something other than its value: the two have to draw alike. A kid drawn empty, with
        // no colour and no border to draw either, has no appearance at all.
        var inheriting = Kid<PdfTextField>(AGroup("/Tx", 0, new PdfString("abc")));
        var holding = Kid<PdfTextField>(AGroup("/Tx", 0, new PdfString("xyz"),
            kid => kid.Elements.SetString(PdfAcroField.Keys.V, "abc")));
        inheriting.ForeColor = XColors.Black;
        holding.ForeColor = XColors.Black;

        var drawn = AppearanceOf(Kid<PdfTextField>(inheriting.Owner.Reopened()));

        drawn.Should().NotBeNull("a field with a value to show has an appearance to show it in");
        drawn.Should().Equal(AppearanceOf(Kid<PdfTextField>(holding.Owner.Reopened())));
    }

    [Test]
    public void AComboBoxWithNoValueOfItsOwnSelectsItsParents()
    {
        var document = AGroup("/Ch", PdfAcroFieldFlags.Combo, new PdfString("b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b", "c"));

        var kid = Kid<PdfComboBoxField>(document);

        kid.SelectedIndex.Should().Be(1);
        ((PdfString)kid.Value).Value.Should().Be("b");
    }

    [Test]
    public void AListBoxWithNoValueOfItsOwnSelectsItsParents()
    {
        var chosen = new PdfArray(null, new PdfString("a"), new PdfString("c"));
        var document = AGroup("/Ch", PdfAcroFieldFlags.MultiSelect, chosen,
            kid => AcroFormBuilder.WithOptions(kid, "a", "b", "c"));

        var kid = Kid<PdfListBoxField>(document);

        kid.SelectedIndices.Should().Equal(0, 2);
        kid.SelectedIndex.Should().Be(0);
    }

    [Test]
    public void ARadioGroupWithNoValueOfItsOwnSelectsItsParents()
    {
        var document = AGroup("/Btn", PdfAcroFieldFlags.Radio, new PdfName("/b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b"));

        Kid<PdfRadioButtonField>(document).SelectedIndex.Should().Be(1);
    }

    [Test]
    public void AKidsOwnValueWinsOverItsParents()
    {
        var text = AGroup("/Tx", 0, new PdfString("parent"),
            kid => kid.Elements.SetString(PdfAcroField.Keys.V, "own"));
        var combo = AGroup("/Ch", PdfAcroFieldFlags.Combo, new PdfString("a"), kid =>
        {
            AcroFormBuilder.WithOptions(kid, "a", "b", "c");
            kid.Elements.SetString(PdfAcroField.Keys.V, "c");
        });
        var radio = AGroup("/Btn", PdfAcroFieldFlags.Radio, new PdfName("/a"), kid =>
        {
            AcroFormBuilder.WithOptions(kid, "a", "b");
            kid.Elements.SetName(PdfAcroField.Keys.V, "/b");
        });

        Kid<PdfTextField>(text).Text.Should().Be("own");
        Kid<PdfComboBoxField>(combo).SelectedIndex.Should().Be(2);
        Kid<PdfRadioButtonField>(radio).SelectedIndex.Should().Be(1);
    }

    [Test]
    public void SettingAKidsTextWritesItOnTheKidAndLeavesTheParentAlone()
    {
        var document = AGroup("/Tx", 0, new PdfString("abc"));
        var kid = Kid<PdfTextField>(document);

        kid.Text = "new";

        kid.Elements.GetString(PdfAcroField.Keys.V).Should().Be("new");
        Parent(document).Elements.GetString(PdfAcroField.Keys.V).Should().Be("abc");

        var reopened = document.Reopened();
        Kid<PdfTextField>(reopened).Text.Should().Be("new");
        Parent(reopened).Elements.GetString(PdfAcroField.Keys.V).Should().Be("abc");
    }

    [Test]
    public void SettingAKidsSelectionWritesItOnTheKidAndLeavesTheParentAlone()
    {
        var combo = AGroup("/Ch", PdfAcroFieldFlags.Combo, new PdfString("b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b", "c"));
        var list = AGroup("/Ch", 0, new PdfString("b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b", "c"));
        var radio = AGroup("/Btn", PdfAcroFieldFlags.Radio, new PdfName("/b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b"));

        Kid<PdfComboBoxField>(combo).SelectedIndex = 2;
        Kid<PdfListBoxField>(list).SelectedIndex = 0;
        Kid<PdfRadioButtonField>(radio).SelectedIndex = 0;

        Kid<PdfComboBoxField>(combo).Elements.GetString(PdfAcroField.Keys.V).Should().Be("c");
        Parent(combo).Elements.GetString(PdfAcroField.Keys.V).Should().Be("b");
        Kid<PdfListBoxField>(list).Elements.GetString(PdfAcroField.Keys.V).Should().Be("a");
        Parent(list).Elements.GetString(PdfAcroField.Keys.V).Should().Be("b");
        Kid<PdfRadioButtonField>(radio).Elements.GetName(PdfAcroField.Keys.V).Should().Be("/a");
        Parent(radio).Elements.GetName(PdfAcroField.Keys.V).Should().Be("/b");
    }

    [Test]
    public void ClearingAKidsSelectionDoesNotBringBackItsParents()
    {
        var document = AGroup("/Ch", 0, new PdfString("b"),
            kid => AcroFormBuilder.WithOptions(kid, "a", "b", "c"));
        var kid = Kid<PdfListBoxField>(document);

        kid.SelectedIndices = [];

        kid.SelectedIndices.Should().BeEmpty();
        Kid<PdfListBoxField>(document.Reopened()).SelectedIndices.Should().BeEmpty();
        Parent(document).Elements.GetString(PdfAcroField.Keys.V).Should().Be("b");
    }

    [Test]
    public void ClearingAListThatNothingWasEverSelectedInWritesNoValue()
    {
        var document = new PdfDocument();
        document.AddPage();
        var list = new PdfListBoxField(document) { Name = "list" };
        document.GetOrCreateAcroForm().Fields.Add(list);

        list.SelectedIndices = [];

        list.SelectedIndices.Should().BeEmpty();
        list.Elements.ContainsKey(PdfAcroField.Keys.V).Should().BeFalse();
    }

    private static byte[] AppearanceOf(PdfAcroField field)
    {
        var appearances = field.Widgets.Single().Elements.GetDictionary("/AP");
        return (appearances?.Elements.GetObject("/N") as PdfDictionary)?.Stream.UnfilteredValue;
    }
}
