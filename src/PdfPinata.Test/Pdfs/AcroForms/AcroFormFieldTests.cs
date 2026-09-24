using System;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using Xunit;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   An interactive form is a tree of dictionaries in the file and a tree of typed objects in
///   memory, and the whole of the AcroForms namespace is the translation between the two. Which
///   class a dictionary becomes is decided by its <c>/FT</c> entry and its flags, nothing else -
///   there is no constructor a caller can reach - so the type transformation is where these tests
///   start. See <see cref="AcroFormBuilder"/> for how a form is put together to read back.
/// </summary>
public class AcroFormFieldTests
{
    [Fact]
    public void ADocumentWithNoFormHasNoFieldsToOffer()
    {
        new PdfDocument().AcroForm.Should().BeNull();
    }

    [Fact]
    public void EveryFieldTypeIsRecognisedFromItsDictionary()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "text")
            .With("/Sig", "signature")
            .With("/Btn", "checkbox")
            .With("/Btn", "radio", field => AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Radio))
            .With("/Btn", "push", field => AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Pushbutton))
            .With("/Ch", "listbox")
            .With("/Ch", "combo", field => AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Combo))
            .With("/Nonsense", "unknown")
            .Build();

        var fields = document.AcroForm.Fields;

        fields["text"].Should().BeOfType<PdfTextField>();
        fields["signature"].Should().BeOfType<PdfSignatureField>();
        fields["checkbox"].Should().BeOfType<PdfCheckBoxField>("a button that is neither radio nor push is a tick box");
        fields["radio"].Should().BeOfType<PdfRadioButtonField>();
        fields["push"].Should().BeOfType<PdfPushButtonField>();
        fields["listbox"].Should().BeOfType<PdfListBoxField>("a choice field that is not a combo is a list");
        fields["combo"].Should().BeOfType<PdfComboBoxField>();
        fields["unknown"].Should().BeOfType<PdfGenericField>("a field type PdfPinata does not know still has a name and flags");
    }

    [Fact]
    public void APushButtonIsAPushButtonEvenWhenItAlsoClaimsToBeARadio()
    {
        // Both flags on one field is a contradiction, and the order the switch tests them in is
        // what settles it. Pinned because it is a decision rather than an accident.
        var document = new AcroFormBuilder()
            .With("/Btn", "confused", field => AcroFormBuilder.WithFlags(field,
                PdfAcroFieldFlags.Pushbutton | PdfAcroFieldFlags.Radio))
            .Build();

        document.AcroForm.Fields["confused"].Should().BeOfType<PdfPushButtonField>();
    }

    // ----- what every field has ------------------------------------------------------------------

    [Fact]
    public void AFieldKnowsItsOwnNameAndFlags()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "surname", field => AcroFormBuilder.WithFlags(field,
                PdfAcroFieldFlags.Required | PdfAcroFieldFlags.Multiline))
            .Build();

        var field = document.AcroForm.Fields["surname"];

        field.Name.Should().Be("surname");
        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Required);
        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Multiline);
        field.ReadOnly.Should().BeFalse();
    }

    [Fact]
    public void AFieldCanBeMadeReadOnlyAndBackAgainWithoutDisturbingItsOtherFlags()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "surname", field => AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Required))
            .Build();
        var field = document.AcroForm.Fields["surname"];

        field.ReadOnly = true;
        field.ReadOnly.Should().BeTrue();
        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Required, "read only is one bit of many");

        field.ReadOnly = false;
        field.ReadOnly.Should().BeFalse();
        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Required);
    }

    [Fact]
    public void AFieldsValueCanBeSetToAStringOrAName()
    {
        var document = new AcroFormBuilder().With("/Tx", "surname").Build();
        var field = document.AcroForm.Fields["surname"];

        field.Value = new PdfString("Bosch");
        // Value rather than ToString: a PdfString writes itself as it appears in the file, in
        // parentheses, and that is not what the field says.
        field.Value.Should().BeOfType<PdfString>().Which.Value.Should().Be("Bosch");

        field.Value = new PdfName("/Yes");
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/Yes");
    }

    [Fact]
    public void AFieldsValueCannotBeSetToAnythingElse()
    {
        var document = new AcroFormBuilder().With("/Tx", "surname").Build();
        var field = document.AcroForm.Fields["surname"];

        var act = () => field.Value = new PdfPinata.Pdf.PdfInteger(7);

        act.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void AReadOnlyFieldRefusesToBeGivenAValue()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "surname", field => AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.ReadOnly))
            .Build();
        var field = document.AcroForm.Fields["surname"];

        var act = () => field.Value = new PdfString("Bosch");

        act.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
    }

    // ----- finding a field -----------------------------------------------------------------------

    [Fact]
    public void TheFieldsOfAFormCanBeReachedByPositionOrByName()
    {
        var document = new AcroFormBuilder().With("/Tx", "first").With("/Tx", "second").Build();

        var fields = document.AcroForm.Fields;

        fields.Elements.Count.Should().Be(2);
        fields[0].Name.Should().Be("first");
        fields[1].Name.Should().Be("second");
        fields["second"].Should().NotBeNull();
        fields.Names.Should().Equal("first", "second");
    }

    [Fact]
    public void AskingForAFieldThatIsNotThereGivesNothingBack()
    {
        var document = new AcroFormBuilder().With("/Tx", "first").Build();

        document.AcroForm.Fields["nosuchfield"].Should().BeNull();
        document.AcroForm.Fields[""].Should().BeNull();
        document.AcroForm.Fields[null].Should().BeNull();
    }

    [Fact]
    public void AChildFieldIsReachedThroughItsParentByADottedName()
    {
        // A form names its fields hierarchically: the field called "address" with a child called
        // "town" is addressed as "address.town", and each part of the name walks one level.
        var document = new AcroFormBuilder()
            .WithParent("address", ("/Tx", "town"), ("/Tx", "postcode"))
            .Build();

        var address = document.AcroForm.Fields["address"];

        address.HasKids.Should().BeTrue();
        address.Fields.Elements.Count.Should().Be(2);
        document.AcroForm.Fields["address.town"].Should().NotBeNull();
        document.AcroForm.Fields["address.town"].Name.Should().Be("town");
        document.AcroForm.Fields["address.nosuchchild"].Should().BeNull();
    }

    [Fact]
    public void AFieldWithNoChildrenSaysSoAndOffersNoneOfThem()
    {
        var document = new AcroFormBuilder().With("/Tx", "surname").Build();
        var field = document.AcroForm.Fields["surname"];

        field.HasKids.Should().BeFalse();
        field.GetDescendantNames().Should().BeEmpty();
        field["anything"].Should().BeNull("a childless field has nothing to look inside");
        field[""].Should().BeSameAs(field, "an empty name means the field itself");
    }

    [Fact]
    public void TheNamesOfEveryFieldInATreeAreListedWithTheirParentsInFront()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "surname")
            .WithParent("address", ("/Tx", "town"), ("/Tx", "postcode"))
            .Build();

        var names = document.AcroForm.Fields.DescendantNames;

        names.Should().BeEquivalentTo("surname", "address.town", "address.postcode");
    }

    [Fact]
    public void AParentListsTheNamesBelowItWithoutItsOwnInFront()
    {
        var document = new AcroFormBuilder()
            .WithParent("address", ("/Tx", "town"), ("/Tx", "postcode"))
            .Build();

        document.AcroForm.Fields["address"].GetDescendantNames()
            .Should().BeEquivalentTo("town", "postcode");
    }

    [Fact]
    public void AFieldTreeIsWrittenAsAFormAViewerWouldAccept()
    {
        // PDFsharp's tree walking reads /Kids and /T and would be satisfied with less than this,
        // so what the assertions above prove depends on the fixture being a real form rather than
        // the least a reader will tolerate. The reference asks that the leaves of the tree are the
        // widget annotations - each with a rectangle, each pointing back at its parent, each
        // listed on the page it is drawn on - while /Fields holds only the root.
        var document = new AcroFormBuilder()
            .With("/Tx", "surname")
            .WithParent("address", ("/Tx", "town"), ("/Tx", "postcode"))
            .Build();

        var fields = document.Internals.Catalog.Elements.GetDictionary("/AcroForm")
            .Elements.GetArray(PdfAcroForm.Keys.Fields);
        var annotations = document.Pages[0].Elements.GetArray("/Annots");

        fields.Elements.Count.Should().Be(2, "the children are reached through the parent");
        annotations.Elements.Count.Should().Be(3, "every widget is drawn on the page");

        var parent = document.AcroForm.Fields["address"];
        foreach (var name in new[] { "town", "postcode" })
        {
            var kid = document.AcroForm.Fields["address." + name];
            kid.Elements.GetDictionary(PdfAcroField.Keys.Parent)
                .Should().BeSameAs(parent, "a child names the field above it");
            kid.Elements.GetRectangle(PdfAcroField.Keys.Rect)
                .Should().NotBe(new PdfRectangle(), "a widget is somewhere on the page");
        }
    }

    // ----- appearances ---------------------------------------------------------------------------

    [Fact]
    public void AFieldListsTheAppearanceStatesItCanBeDrawnIn()
    {
        var document = new AcroFormBuilder()
            .With("/Btn", "agree", field => AcroFormBuilder.WithOnAndOffAppearances(field))
            .Build();

        document.AcroForm.Fields["agree"].GetAppearanceNames()
            .Should().BeEquivalentTo("/Yes", "/Off");
    }

    [Fact]
    public void AFieldWithNoAppearanceDictionaryListsNoStates()
    {
        var document = new AcroFormBuilder().With("/Tx", "surname").Build();

        document.AcroForm.Fields["surname"].GetAppearanceNames().Should().BeEmpty();
    }

    // ----- the form itself -----------------------------------------------------------------------

    [Fact]
    public void AFormIsTheSameCollectionEveryTimeItIsAskedForItsFields()
    {
        var document = new AcroFormBuilder().With("/Tx", "surname").Build();

        document.AcroForm.Fields.Should().BeSameAs(document.AcroForm.Fields);
    }

    [Fact]
    public void EveryFieldOfADocumentSurvivesBeingSavedAndReadAgain()
    {
        var document = new AcroFormBuilder()
            .With("/Tx", "text")
            .With("/Btn", "checkbox", field => AcroFormBuilder.WithOnAndOffAppearances(field))
            .With("/Ch", "combo", field =>
            {
                AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Combo);
                AcroFormBuilder.WithOptions(field, "one", "two");
            })
            .Build();

        document.AcroForm.Fields.Names.Should().Equal("text", "checkbox", "combo");
        document.AcroForm.Fields.Count.Should().Be(3);
    }
}
