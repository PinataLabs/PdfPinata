using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using Xunit;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   <c>/FT</c> and <c>/Ff</c> are inheritable (ISO 32000-1 Table 220): a form read from a file
///   often says them once, on a field that only groups others, and leaves the fields under it
///   without. Reading a field's own entries alone called every such child a generic field with no
///   flags at all.
/// </summary>
public class AcroFormInheritedFlagsTests
{
    /// <summary>
    ///   A text-field parent, <c>name</c>, carrying the type and the given flags, with two named
    ///   children that carry neither unless <paramref name="describeLast"/> gives <c>last</c> some.
    /// </summary>
    static PdfDocument ANameGroup(PdfAcroFieldFlags flags,
        System.Action<PdfDictionary> describeLast = null) =>
        new AcroFormBuilder()
            .WithDescribedParent("name",
                parent =>
                {
                    parent.Elements.SetName(PdfAcroField.Keys.FT, "/Tx");
                    AcroFormBuilder.WithFlags(parent, flags);
                },
                kid => kid.Elements.SetString(PdfAcroField.Keys.T, "first"),
                kid =>
                {
                    kid.Elements.SetString(PdfAcroField.Keys.T, "last");
                    describeLast?.Invoke(kid);
                })
            .Build();

    [Fact]
    public void AChildWithNoFlagsOfItsOwnAnswersItsParents()
    {
        var document = ANameGroup(PdfAcroFieldFlags.ReadOnly | PdfAcroFieldFlags.Multiline);

        var first = document.AcroForm.Fields["name.first"];

        first.Should().BeOfType<PdfTextField>("the type is inherited as well");
        first.Flags.Should().Be(PdfAcroFieldFlags.ReadOnly | PdfAcroFieldFlags.Multiline);
        first.ReadOnly.Should().BeTrue();
        ((PdfTextField)first).MultiLine.Should().BeTrue();
    }

    [Fact]
    public void AChildsOwnFlagsWinOverItsParents()
    {
        var document = ANameGroup(PdfAcroFieldFlags.ReadOnly | PdfAcroFieldFlags.Multiline,
            kid => AcroFormBuilder.WithFlags(kid, PdfAcroFieldFlags.Password));

        document.AcroForm.Fields["name.last"].Flags.Should().Be(PdfAcroFieldFlags.Password,
            "an entry of the field's own replaces the inherited one rather than adding to it");
        document.AcroForm.Fields["name.first"].Flags
            .Should().Be(PdfAcroFieldFlags.ReadOnly | PdfAcroFieldFlags.Multiline);
    }

    [Fact]
    public void FlagsAreInheritedFromFurtherUpThanTheParent()
    {
        var document = new AcroFormBuilder()
            .WithDescribedParent("outer",
                parent =>
                {
                    parent.Elements.SetName(PdfAcroField.Keys.FT, "/Tx");
                    AcroFormBuilder.WithFlags(parent, PdfAcroFieldFlags.Required);
                },
                middle => middle.Elements.SetString(PdfAcroField.Keys.T, "middle"))
            .Build();

        // A grandchild, hung under the child once the document is read back.
        var middle = document.AcroForm.Fields["outer.middle"];
        var grandchild = new PdfDictionary(document);
        grandchild.Elements.SetString(PdfAcroField.Keys.T, "inner");
        document.Internals.AddObject(grandchild);
        grandchild.Elements.SetReference(PdfAcroField.Keys.Parent, middle);
        var kids = new PdfArray(document);
        kids.Elements.Add(grandchild.Reference);
        middle.Elements[PdfAcroField.Keys.Kids] = kids;

        var inner = document.AcroForm.Fields["outer.middle.inner"];

        inner.Should().BeOfType<PdfTextField>();
        inner.Flags.Should().Be(PdfAcroFieldFlags.Required);
    }

    [Fact]
    public void SettingOneFlagOnAnInheritingChildKeepsTheRest()
    {
        var document = ANameGroup(PdfAcroFieldFlags.Multiline);
        var first = (PdfTextField)document.AcroForm.Fields["name.first"];

        first.ReadOnly = true;

        first.Flags.Should().Be(PdfAcroFieldFlags.Multiline | PdfAcroFieldFlags.ReadOnly,
            "the child's own /Ff now stands in for the inherited one, so it has to carry it");
        first.Elements.GetInteger(PdfAcroField.Keys.Ff)
            .Should().Be((int)(PdfAcroFieldFlags.Multiline | PdfAcroFieldFlags.ReadOnly));
        document.AcroForm.Fields["name.last"].ReadOnly.Should().BeFalse("the parent is untouched");
    }

    [Theory]
    [InlineData("/Btn", PdfAcroFieldFlags.Radio, typeof(PdfRadioButtonField))]
    [InlineData("/Btn", PdfAcroFieldFlags.Pushbutton, typeof(PdfPushButtonField))]
    [InlineData("/Btn", (PdfAcroFieldFlags)0, typeof(PdfCheckBoxField))]
    [InlineData("/Ch", PdfAcroFieldFlags.Combo, typeof(PdfComboBoxField))]
    [InlineData("/Ch", (PdfAcroFieldFlags)0, typeof(PdfListBoxField))]
    public void AChildsKindIsReadFromWhatItInherits(string fieldType, PdfAcroFieldFlags flags,
        System.Type expected)
    {
        var document = new AcroFormBuilder()
            .WithDescribedParent("group",
                parent =>
                {
                    parent.Elements.SetName(PdfAcroField.Keys.FT, fieldType);
                    AcroFormBuilder.WithFlags(parent, flags);
                },
                kid => kid.Elements.SetString(PdfAcroField.Keys.T, "one"))
            .Build();

        document.AcroForm.Fields["group.one"].Should().BeOfType(expected);
    }

    [Fact(Timeout = 10_000)]
    public async Task AParentChainThatComesBackOnItselfEndsRatherThanLoopingForEver()
    {
        await Task.Run(() =>
        {
            var document = new PdfDocument();
            var field = new PdfTextField(document);
            var parent = new PdfDictionary(document);
            document.Internals.AddObject(field);
            document.Internals.AddObject(parent);
            field.Elements.SetReference(PdfAcroField.Keys.Parent, parent);
            parent.Elements.SetReference(PdfAcroField.Keys.Parent, field);

            field.Flags.Should().Be(0);
        });
    }

    [Fact]
    public void ATickBoxWhoseStateIsNeinIsNotTicked()
    {
        // Some forms name their off state in German. The check is on the path that reads the
        // answer from the first child, which is where it has always been.
        var document = new AcroFormBuilder()
            .WithTypedParent("/Btn", "agree",
                kid =>
                {
                    AcroFormBuilder.WithOnAndOffAppearances(kid, "/Ja");
                    kid.Elements.SetName(PdfAcroField.Keys.V, "/Nein");
                })
            .Build();

        ((PdfCheckBoxField)document.AcroForm.Fields["agree"]).Checked.Should().BeFalse();
    }
}
