using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using Xunit;

namespace PdfPinata.Test.Pdfs.AcroForms;

/// <summary>
///   What each kind of field adds to <see cref="PdfAcroField"/>: a text field its text, a tick box
///   whether it is ticked, a choice field which option is chosen. Between them these are what a
///   caller filling in somebody else's form actually touches.
///   <para>
///   The values a field holds are not stored the way its property reads them. A tick box holds the
///   <em>name</em> of an appearance state and calls everything that is not <c>/Off</c> ticked; a
///   choice field holds the option text and finds its index by searching <c>/Opt</c>. So the
///   properties are doing real work rather than forwarding, and the round trips below are what
///   say the work is right.
///   </para>
/// </summary>
public class AcroFormFieldKindTests
{
    static PdfDocument FormWith(string fieldType, string name, Action<PdfDictionary> describe = null) =>
        new AcroFormBuilder().With(fieldType, name, describe).Build();

    // ----- text ----------------------------------------------------------------------------------

    [Fact]
    public void ATextFieldHoldsTheTextItIsGiven()
    {
        var document = FormWith("/Tx", "surname");
        var field = (PdfTextField)document.AcroForm.Fields["surname"];

        field.Text = "Bosch";

        field.Text.Should().Be("Bosch");
        field.Value.Should().BeOfType<PdfString>().Which.Value.Should().Be("Bosch");
    }

    [Fact]
    public void ATextFieldStartsEmptyRatherThanNull()
    {
        ((PdfTextField)FormWith("/Tx", "surname").AcroForm.Fields["surname"]).Text.Should().BeEmpty();
    }

    [Fact]
    public void ATextFieldRemembersHowLongItsTextMayBe()
    {
        var field = (PdfTextField)FormWith("/Tx", "surname").AcroForm.Fields["surname"];

        field.MaxLength.Should().Be(0, "no limit is written as no entry");

        field.MaxLength = 12;
        field.MaxLength.Should().Be(12);
    }

    [Fact]
    public void ATextFieldsAppearanceIsItsToChoose()
    {
        var field = (PdfTextField)FormWith("/Tx", "surname").AcroForm.Fields["surname"];

        field.Font.Should().NotBeNull("a field draws its own text, so it needs a font before anyone sets one");
        field.ForeColor.Should().Be(XColors.Black);
        field.BackColor.Should().Be(XColor.Empty, "no background at all rather than a white one");

        field.Font = new XFont("Arial", 14);
        field.ForeColor = XColors.Red;
        field.BackColor = XColors.LightGray;

        field.Font.Size.Should().Be(14);
        field.ForeColor.Should().Be(XColors.Red);
        field.BackColor.Should().Be(XColors.LightGray);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATextFieldCanBeTurnedIntoAMultiLineOneAndBack(bool multiLine)
    {
        var field = (PdfTextField)FormWith("/Tx", "surname").AcroForm.Fields["surname"];

        field.MultiLine = multiLine;

        field.MultiLine.Should().Be(multiLine);
        ((field.Flags & PdfAcroFieldFlags.Multiline) != 0).Should().Be(multiLine);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATextFieldCanBeTurnedIntoAPasswordFieldAndBack(bool password)
    {
        var field = (PdfTextField)FormWith("/Tx", "surname").AcroForm.Fields["surname"];

        field.Password = password;

        field.Password.Should().Be(password);
        ((field.Flags & PdfAcroFieldFlags.Password) != 0).Should().Be(password);
    }

    // ----- tick boxes ----------------------------------------------------------------------------

    [Fact]
    public void ATickBoxIsNotTickedUntilItIs()
    {
        var field = (PdfCheckBoxField)FormWith("/Btn", "agree",
            f => AcroFormBuilder.WithOnAndOffAppearances(f)).AcroForm.Fields["agree"];

        field.Checked.Should().BeFalse("a box with no value set is not ticked");
    }

    [Fact]
    public void TickingABoxSetsItToTheStateItsAppearanceCallsOn()
    {
        // The on state is whatever the appearance dictionary names that is not /Off - it is /Yes
        // by convention and /On, /1 or something in the author's own language in practice, so the
        // field has to look it up rather than assume.
        var field = (PdfCheckBoxField)FormWith("/Btn", "agree",
            f => AcroFormBuilder.WithOnAndOffAppearances(f, "/Ja")).AcroForm.Fields["agree"];

        field.Checked = true;

        field.Checked.Should().BeTrue();
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/Ja");
        field.Elements.GetName("/AS").Should().Be("/Ja", "the appearance has to follow the value");
    }

    [Fact]
    public void UntickingABoxSetsItToOff()
    {
        var field = (PdfCheckBoxField)FormWith("/Btn", "agree",
            f => AcroFormBuilder.WithOnAndOffAppearances(f)).AcroForm.Fields["agree"];

        field.Checked = true;
        field.Checked = false;

        field.Checked.Should().BeFalse();
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/Off");
        field.Elements.GetName("/AS").Should().Be("/Off");
    }

    /// <summary>
    ///   A box with no appearance dictionary names no state at all, so <c>GetNonOffValue</c> used
    ///   to return null and the setter handed that straight to <c>SetName</c>, which refuses it:
    ///   the caller asked to tick a box and got an ArgumentNullException naming a parameter called
    ///   <c>value</c> that they never passed. A form built by hand, or one whose appearances were
    ///   stripped, is exactly this case, so the lookup falls back to the conventional on state.
    /// </summary>
    [Fact]
    public void TickingABoxThatHasNoAppearanceFallsBackToTheConventionalOnState()
    {
        var field = (PdfCheckBoxField)FormWith("/Btn", "agree").AcroForm.Fields["agree"];

        field.Checked = true;

        field.Checked.Should().BeTrue();
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/Yes");

        field.Checked = false;

        field.Checked.Should().BeFalse();
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/Off");
    }

    // ----- radio groups --------------------------------------------------------------------------

    [Fact]
    public void ARadioGroupNamesItsButtonsInAnOptionArray()
    {
        var field = (PdfRadioButtonField)FormWith("/Btn", "size", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Radio);
            AcroFormBuilder.WithOptions(f, "small", "medium", "large");
        }).AcroForm.Fields["size"];

        field.SelectedIndex.Should().Be(-1, "nothing is chosen to begin with");

        field.SelectedIndex = 1;

        // A radio group records its choice as a name - the export value of the button that is on -
        // rather than as the text string the option array holds it as.
        field.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/medium");
        field.SelectedIndex.Should().Be(1, "it finds again what it wrote");
    }

    [Fact]
    public void ARadioGroupRefusesAButtonItDoesNotHave()
    {
        var field = (PdfRadioButtonField)FormWith("/Btn", "size", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Radio);
            AcroFormBuilder.WithOptions(f, "small", "large");
        }).AcroForm.Fields["size"];

        var tooHigh = () => field.SelectedIndex = 2;
        var negative = () => field.SelectedIndex = -1;

        tooHigh.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ARadioGroupWithNoOptionsAtAllQuietlyIgnoresBeingSet()
    {
        var field = (PdfRadioButtonField)FormWith("/Btn", "size",
            f => AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Radio)).AcroForm.Fields["size"];

        var act = () => field.SelectedIndex = 0;

        act.Should().NotThrow("there is no array to bound-check against");
        field.SelectedIndex.Should().Be(-1);
    }

    // ----- choice fields -------------------------------------------------------------------------

    [Fact]
    public void AListBoxChoosesByIndexAndRemembersTheTextOfWhatWasChosen()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey")).AcroForm.Fields["county"];

        field.SelectedIndex.Should().Be(-1);

        field.SelectedIndex = 2;

        field.SelectedIndex.Should().Be(2, "it finds again what it wrote");
    }

    [Fact]
    public void AComboBoxAlsoRecordsTheIndexSeparatelySoTheViewerFollowsIt()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndex = 1;

        field.SelectedIndex.Should().Be(1);
        SelectedIndicesOf(field).Should().Equal(new[] { 1 },
            "a viewer reads /I rather than searching /Opt");
    }

    // ----- a list box taking more than one choice -------------------------------------------------

    /// <summary>
    ///   A list box is the only field that can offer several choices at once - the MultiSelect
    ///   flag is meaningless on a combo box - and it could not be told to take one. It had an
    ///   index and no more.
    ///   <para>
    ///   Reading one was worse than not being able to write one. <c>/V</c> holds an array when
    ///   several options are chosen, and the getter read it with <c>GetString</c>, which is
    ///   written to throw <see cref="InvalidCastException"/> for anything that is not a string.
    ///   It threw <see cref="NullReferenceException"/> instead, on the way to saying so - see
    ///   <see cref="PdfNameObjectComparisonTests"/> for why. Either way, opening an ordinary form
    ///   built by something else and asking what was selected threw.
    ///   </para>
    /// </summary>
    [Fact]
    public void AListBoxReadsAValueThatNamesSeveralOptions()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
            // As another producer would have written it.
            var chosen = new PdfArray(f.Owner);
            chosen.Elements.Add(new PdfString("Kent"));
            chosen.Elements.Add(new PdfString("Surrey"));
            f.Elements["/V"] = chosen;
        }).AcroForm.Fields["county"];

        field.SelectedIndices.Should().Equal(new[] { 0, 2 });
        field.SelectedIndex.Should().Be(0, "the first of them, for a caller that wants only one");
    }

    [Fact]
    public void AListBoxSelectsSeveralOptionsAtOnce()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndices = new[] { 2, 0 };

        field.SelectedIndices.Should().Equal(new[] { 0, 2 }, "ascending, as the specification asks");
        SelectedIndicesOf(field).Should().Equal(new[] { 0, 2 });
        OptionTextsOfValue(field).Should().Equal(new[] { "Kent", "Surrey" });
    }

    /// <summary>
    ///   <c>/I</c> belongs to a list that allows several choices. The specification says it should
    ///   not be used by one that does not, so a single-choice list carries <c>/V</c> alone - and
    ///   an entry left over from when the list did allow several is taken away rather than left
    ///   saying something the field no longer does.
    /// </summary>
    [Fact]
    public void ASingleChoiceListCarriesNoIndexEntry()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Sussex")).AcroForm.Fields["county"];

        field.SelectedIndex = 1;

        field.Elements.ContainsKey(PdfChoiceField.Keys.I).Should().BeFalse();
        field.SelectedIndex.Should().Be(1, "/V alone is enough to find it again");
    }

    [Fact]
    public void ASingleChoiceListRefusesToSelectSeveralOptions()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Sussex")).AcroForm.Fields["county"];

        var act = () => field.SelectedIndices = new[] { 0, 1 };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MultiSelect*", "the flag is what the caller has to set");
        field.SelectedIndices.Should().BeEmpty("nothing was written");
    }

    [Fact]
    public void AListBoxSelectsNothingWhenGivenNoIndices()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];
        field.SelectedIndices = new[] { 0, 1 };

        field.SelectedIndices = Array.Empty<int>();

        field.SelectedIndices.Should().BeEmpty();
        field.Elements.ContainsKey("/V").Should().BeFalse();
        field.Elements.ContainsKey(PdfChoiceField.Keys.I).Should().BeFalse();
    }

    /// <summary>
    ///   An index the list has no option for leaves the field as it was, rather than writing
    ///   <c>/V</c> and then failing on the way to <c>/I</c>.
    /// </summary>
    [Fact]
    public void AListBoxRefusingAnIndexLeavesWhatWasThereAlone()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];
        field.SelectedIndex = 0;

        var act = () => field.SelectedIndices = new[] { 1, 7 };

        act.Should().Throw<ArgumentOutOfRangeException>();
        field.SelectedIndices.Should().Equal(new[] { 0 });
    }

    /// <summary>
    ///   Two options may display differently and export the same text, which is one of the two
    ///   cases the specification says <c>/I</c> shall be used for: searching <c>/Opt</c> for the
    ///   text in <c>/V</c> finds the first of them and cannot do better. Without reading <c>/I</c>
    ///   the round trip lost the selection - both chosen options collapsed onto the first.
    /// </summary>
    [Fact]
    public void TwoOptionsExportingTheSameTextAreToldApartByTheIndexEntry()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndices = new[] { 0, 1 };

        field.SelectedIndices.Should().Equal(new[] { 0, 1 },
            "/I says which two, where /V says only that both are Kent");
    }

    [Fact]
    public void OneOfTwoOptionsExportingTheSameTextIsToldApartByTheIndexEntry()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndices = new[] { 1 };

        field.SelectedIndices.Should().Equal(new[] { 1 }, "the second Kent, not the first");
    }

    /// <summary>
    ///   The specification requires <c>/I</c> in two cases, and only one of them is a list that
    ///   allows several choices: the other is two options exporting the same text, which a
    ///   single-choice list may have just as easily. Stripping <c>/I</c> from every such list lost
    ///   the choice - <c>/V</c> says only "Kent", and searching <c>/Opt</c> for it finds the first
    ///   Kent whichever one was picked.
    /// </summary>
    [Fact]
    public void ASingleChoiceListKeepsTheIndexEntryWhenTwoOptionsExportTheSameText()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey")).AcroForm.Fields["county"];

        field.SelectedIndex = 1;

        field.Elements.ContainsKey(PdfChoiceField.Keys.I).Should().BeTrue(
            "/V cannot say which Kent was chosen, so /I has to");
        field.SelectedIndex.Should().Be(1, "the second Kent, not the first");
    }

    /// <summary>
    ///   And no further than that: an option whose text finds itself is written as <c>/V</c> alone,
    ///   so the entry appears where it earns its place rather than on every single-choice list.
    /// </summary>
    [Fact]
    public void ASingleChoiceListNeedsNoIndexEntryForTheFirstOfTwoAlikeOptions()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey")).AcroForm.Fields["county"];

        field.SelectedIndex = 0;

        field.Elements.ContainsKey(PdfChoiceField.Keys.I).Should().BeFalse(
            "searching /Opt for Kent finds this one anyway");
        field.SelectedIndex.Should().Be(0);
    }

    [Fact]
    public void TheChosenOneOfTwoAlikeOptionsSurvivesARoundTripWithoutMultiSelect()
    {
        var document = FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey"));
        ((PdfListBoxField)document.AcroForm.Fields["county"]).SelectedIndex = 1;

        using var written = new MemoryStream();
        document.Save(written, false);
        written.Position = 0;
        // Fully qualified: this test assembly has a PdfReader of its own.
        var reopened = Pdf.IO.PdfReader.Open(
            written, Pdf.IO.PdfDocumentOpenMode.Modify);

        ((PdfListBoxField)reopened.AcroForm.Fields["county"]).SelectedIndex.Should().Be(1);
    }

    /// <summary>
    ///   <c>/I</c> is required where two options export the same text, but a producer may leave it
    ///   out all the same, and then <c>/V</c> naming that text twice is all there is to go on. Each
    ///   entry takes the first option no earlier entry has taken, so two named Kents are read as
    ///   the two options that are Kent rather than as the first of them twice over.
    /// </summary>
    [Fact]
    public void AValueNamingTheSameTextTwiceIsReadAsTwoOptionsWhenThereIsNoIndexEntry()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Kent", "Surrey");
            var chosen = new PdfArray(f.Owner);
            chosen.Elements.Add(new PdfString("Kent"));
            chosen.Elements.Add(new PdfString("Kent"));
            f.Elements["/V"] = chosen;
        }).AcroForm.Fields["county"];

        field.SelectedIndices.Should().Equal(new[] { 0, 1 });
    }

    /// <summary>
    ///   The limit of that: where only one option exports the text, a <c>/V</c> naming it twice
    ///   still selects the one option, because there is no second to give the second entry.
    /// </summary>
    [Fact]
    public void AValueNamingTheOneMatchingOptionTwiceSelectsItOnce()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
            var chosen = new PdfArray(f.Owner);
            chosen.Elements.Add(new PdfString("Kent"));
            chosen.Elements.Add(new PdfString("Kent"));
            f.Elements["/V"] = chosen;
        }).AcroForm.Fields["county"];

        field.SelectedIndices.Should().Equal(new[] { 0 });
    }

    /// <summary>
    ///   Where the two entries disagree the specification gives <c>/V</c> precedence, so an
    ///   <c>/I</c> naming options <c>/V</c> does not is passed over rather than believed.
    /// </summary>
    [Fact]
    public void AnIndexEntryThatContradictsTheValueIsIgnored()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
            f.Elements["/V"] = new PdfString("Surrey");
            var stale = new PdfArray(f.Owner);
            stale.Elements.Add(new PdfPinata.Pdf.PdfInteger(0));
            f.Elements["/I"] = stale;
        }).AcroForm.Fields["county"];

        field.SelectedIndices.Should().Equal(new[] { 2 }, "/V names Surrey, whatever /I says");
    }

    [Fact]
    public void AListWithNoOptionsRefusesToSelectOne()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county").AcroForm.Fields["county"];

        var act = () => (field).SelectedIndices = new[] { 0 };

        act.Should().Throw<ArgumentOutOfRangeException>();
        field.Elements.ContainsKey("/V").Should().BeFalse("nothing was written");
        field.Elements.ContainsKey(PdfChoiceField.Keys.I).Should().BeFalse();
    }

    [Fact]
    public void SeveralSelectedOptionsSurviveARoundTrip()
    {
        var document = FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        });
        ((PdfListBoxField)document.AcroForm.Fields["county"]).SelectedIndices = new[] { 0, 2 };

        using var written = new MemoryStream();
        document.Save(written, false);
        written.Position = 0;
        // Fully qualified: this test assembly has a PdfReader of its own.
        var reopened = Pdf.IO.PdfReader.Open(
            written, Pdf.IO.PdfDocumentOpenMode.Modify);

        var field = (PdfListBoxField)reopened.AcroForm.Fields["county"];
        field.SelectedIndices.Should().Equal(new[] { 0, 2 });
        SelectedIndicesOf(field).Should().Equal(new[] { 0, 2 });
        field.Elements.GetArray("/V").Should().NotBeNull("several chosen options make /V an array");
    }

    [Fact]
    public void AChoiceFieldRefusesAnIndexItHasNoOptionFor()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Sussex")).AcroForm.Fields["county"];

        var tooHigh = () => field.SelectedIndex = 5;
        var negative = () => field.SelectedIndex = -1;

        tooHigh.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    ///   <c>/I</c> was written as a bare integer, which the specification does not allow and which
    ///   <see cref="PdfChoiceField.Keys.I"/> does not claim either - its key metadata declares
    ///   <c>KeyType.Array</c>, so the code and the declaration beside it disagreed. A reader that
    ///   takes the key at its word gets an array and finds a number.
    /// </summary>
    [Fact]
    public void TheIndexOfTheChosenOptionIsWrittenAsAnArray()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndex = 2;

        field.Elements[PdfChoiceField.Keys.I].Should().BeOfType<PdfArray>()
            .Which.Elements.Count.Should().Be(1, "a combo box offers a single choice");
        SelectedIndicesOf(field).Should().Equal(new[] { 2 });
    }

    /// <summary>
    ///   The shape in the file is what matters, an integer and an array of one being different
    ///   bytes, so this one goes all the way out to a document and back rather than reading the
    ///   dictionary it was just written into.
    /// </summary>
    [Fact]
    public void TheIndexIsStillAnArrayAfterARoundTrip()
    {
        var document = FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        });
        ((PdfComboBoxField)document.AcroForm.Fields["county"]).SelectedIndex = 2;

        using var written = new MemoryStream();
        document.Save(written, false);
        written.Position = 0;
        // Fully qualified: this test assembly has a PdfReader of its own.
        var reopened = Pdf.IO.PdfReader.Open(written, Pdf.IO.PdfDocumentOpenMode.Modify);

        var field = (PdfComboBoxField)reopened.AcroForm.Fields["county"];
        field.Elements[PdfChoiceField.Keys.I].Should().BeOfType<PdfArray>();
        SelectedIndicesOf(field).Should().Equal(new[] { 2 });
        field.SelectedIndex.Should().Be(2, "/V and /I still agree");
    }

    /// <summary>
    ///   Choosing twice replaces the entry rather than growing it, so a field cannot end up
    ///   claiming two options are chosen at once.
    /// </summary>
    [Fact]
    public void ChoosingASecondOptionReplacesTheIndexRatherThanAddingToIt()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        }).AcroForm.Fields["county"];

        field.SelectedIndex = 0;
        field.SelectedIndex = 2;

        SelectedIndicesOf(field).Should().Equal(new[] { 2 });
    }

    [Fact]
    public void AComboBoxIgnoresBeingSetToNothingChosen()
    {
        // Minus one means nothing chosen, and the setter has an explicit arm to leave the field
        // alone rather than search for an option at index -1.
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        field.SelectedIndex = 0;
        var act = () => field.SelectedIndex = -1;

        act.Should().NotThrow();
        field.SelectedIndex.Should().Be(0, "the field keeps what it had");
    }

    // ----- a combo box taking text it does not offer ----------------------------------------------

    /// <summary>
    ///   An editable combo box accepts text that is none of its options, so the field has to record
    ///   that text <em>as</em> an option: otherwise <c>/V</c> names nothing in <c>/Opt</c> and
    ///   <c>/I</c> has no index to point at.
    ///   <para>
    ///   The append used to reach into the field dictionary by position - the third value in it,
    ///   whatever that happened to be - and cast it to an array. Here the third entry is
    ///   <c>/FT</c>, a name, so the cast threw and an empty catch swallowed it: the text never
    ///   reached <c>/Opt</c> and <c>/I</c> stayed where it was. Worse, in a dictionary whose third
    ///   entry <em>was</em> an array the text was appended to that array instead, silently.
    ///   </para>
    /// </summary>
    [Fact]
    public void AComboBoxGivenTextItDoesNotOfferAddsItToTheOptions()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.Edit);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        field.Value = new PdfString("Middlesex");

        OptionsOf(field).Should().Equal("Kent", "Sussex", "Middlesex");
        field.SelectedIndex.Should().Be(2);
        SelectedIndicesOf(field).Should().Equal(new[] { 2 },
            "a viewer reads /I rather than searching /Opt");
    }

    [Fact]
    public void AComboBoxGivenAValueItAlreadyOffersLeavesTheOptionsAlone()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        field.Value = new PdfString("Sussex");

        OptionsOf(field).Should().Equal(new[] { "Kent", "Sussex" },
            "the option was already on offer, so nothing needed adding");
        field.SelectedIndex.Should().Be(1);
    }

    /// <summary>
    ///   <c>/Opt</c> is optional, so a combo box may have none at all. The value still has to land
    ///   somewhere, which means making the array rather than assuming one.
    /// </summary>
    [Fact]
    public void AComboBoxWithNoOptionsAtAllIsGivenAnOptionArray()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.Edit))
            .AcroForm.Fields["county"];

        field.Value = new PdfString("Middlesex");

        OptionsOf(field).Should().Equal("Middlesex");
        field.SelectedIndex.Should().Be(0);
    }

    /// <summary>
    ///   A choice field's value is a text string, and so is every entry of its <c>/Opt</c> array.
    ///   A caller may still hand it a name, which is taken to mean the text the name stands for -
    ///   the same reading <see cref="PdfRadioButtonField"/> gives its own <c>/V</c>, where the
    ///   slash that makes a name a name is not part of the value. Stored as a name it would be
    ///   invisible to the search through <c>/Opt</c>, so <c>/I</c> would never be pointed at it.
    /// </summary>
    [Fact]
    public void AComboBoxGivenANameStoresTheTextItStandsFor()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.Edit);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        field.Value = new PdfName("/Middlesex");

        OptionsOf(field).Should().Equal(new[] { "Kent", "Sussex", "Middlesex" });
        field.Value.Should().BeOfType<PdfString>().Which.Value.Should().Be("Middlesex");
        field.SelectedIndex.Should().Be(2);
        SelectedIndicesOf(field).Should().Equal(new[] { 2 });
    }

    [Fact]
    public void AComboBoxGivenANameForAnOptionItAlreadyOffersChoosesThatOption()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        field.Value = new PdfName("/Sussex");

        OptionsOf(field).Should().Equal(new[] { "Kent", "Sussex" },
            "the option was already on offer, so nothing needed adding");
        field.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void AComboBoxRefusesAValueThatIsNotText()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo)).AcroForm.Fields["county"];

        // Fully qualified: this test assembly has a PdfInteger of its own.
        var act = () => field.Value = new PdfPinata.Pdf.PdfInteger(3);

        act.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    ///   The indices in <c>/I</c>. It is an array - of the options selected, sorted ascending -
    ///   which is what both the specification and this field's own key metadata say, so reading it
    ///   as one is what says it was written as one.
    /// </summary>
    static List<int> SelectedIndicesOf(PdfAcroField field)
    {
        var indices = new List<int>();
        var entry = field.Elements.GetArray(PdfChoiceField.Keys.I);
        if (entry != null)
            foreach (var item in entry.Elements)
                indices.Add(((PdfPinata.Pdf.PdfInteger)item).Value);
        return indices;
    }

    /// <summary>
    ///   The option texts named by <c>/V</c>, which is a text string when one option is chosen and
    ///   an array of them when several are.
    /// </summary>
    static List<string> OptionTextsOfValue(PdfAcroField field)
    {
        var texts = new List<string>();
        var value = field.Elements["/V"];
        if (value is PdfArray chosen)
            foreach (var item in chosen.Elements)
                texts.Add(((PdfString)item).Value);
        else if (value is PdfString single)
            texts.Add(single.Value);
        return texts;
    }

    static List<string> OptionsOf(PdfAcroField field)
    {
        var options = new List<string>();
        var opt = field.Elements.GetArray("/Opt");
        if (opt != null)
            foreach (var item in opt.Elements)
                options.Add(((PdfString)item).Value);
        return options;
    }

    [Fact]
    public void AChoiceFieldWithNoOptionsFindsNothing()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county").AcroForm.Fields["county"];

        field.SelectedIndex.Should().Be(-1);
    }

    // ----- option text against the value that names it -------------------------------------------

    /// <summary>
    ///   The choice and radio fields used to compare and copy their options with
    ///   <c>ToString()</c> rather than with the string's value. <c>PdfString.ToString()</c> writes
    ///   the string as it appears in the file - in parentheses - so "Sussex" was handled
    ///   throughout as "(Sussex)": choosing an option wrote the parentheses into <c>/V</c>, and a
    ///   radio group had it worse still, its value being written as a <em>name</em> so that
    ///   <c>/V</c> became the malformed <c>/(medium)</c>. Setting and then reading in the same
    ///   session agreed with itself, which is why it went unnoticed.
    /// </summary>
    [Fact]
    public void ChoosingAnOptionWritesTheOptionTextAndNothingElse()
    {
        var listBox = (PdfListBoxField)FormWith("/Ch", "county",
            f => AcroFormBuilder.WithOptions(f, "Kent", "Sussex")).AcroForm.Fields["county"];
        var radio = (PdfRadioButtonField)FormWith("/Btn", "size", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Radio);
            AcroFormBuilder.WithOptions(f, "small", "medium");
        }).AcroForm.Fields["size"];

        listBox.SelectedIndex = 1;
        radio.SelectedIndex = 1;

        listBox.Value.Should().BeOfType<PdfString>().Which.Value.Should().Be("Sussex");
        radio.Value.Should().BeOfType<PdfName>().Which.ToString().Should().Be("/medium");
    }

    [Fact]
    public void AChoiceFieldFindsWhatAnotherProducerChose()
    {
        // The same form as above, except that /V was already set - as every real form that has
        // been filled in has - to the option text without delimiters.
        var document = new AcroFormBuilder().With("/Ch", "county", field =>
        {
            AcroFormBuilder.WithOptions(field, "Kent", "Sussex");
            field.Elements.SetString(PdfAcroField.Keys.V, "Sussex");
        }).Build();

        var field = (PdfListBoxField)document.AcroForm.Fields["county"];

        field.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void AnOptionGivenAsAnExportAndDisplayPairIsFoundByItsExportValue()
    {
        // /Opt may hold [exportValue displayText] pairs rather than plain strings, and the export
        // value is what /V is meant to match.
        var document = new AcroFormBuilder().With("/Ch", "county", field =>
        {
            var opt = new PdfArray(field.Owner);
            var pair = new PdfArray(field.Owner);
            pair.Elements.Add(new PdfString("KEN"));
            pair.Elements.Add(new PdfString("Kent"));
            opt.Elements.Add(pair);
            field.Elements["/Opt"] = opt;
        }).Build();
        var listBox = (PdfListBoxField)document.AcroForm.Fields["county"];

        listBox.Value = new PdfString("KEN");

        listBox.SelectedIndex.Should().Be(0);
    }

    // ----- the field types that hold nothing of their own ----------------------------------------

    [Fact]
    public void APushButtonAndASignatureAndAnUnknownFieldAreStillFields()
    {
        // None of the three adds a value of its own - a push button does something rather than
        // holding something, a signature's value is a dictionary PDFsharp does not model, and a
        // generic field is whatever PDFsharp could not identify. All three still carry a name and
        // flags, which is what the tree-walking code needs of them.
        var document = new AcroFormBuilder()
            .With("/Btn", "print", f => AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Pushbutton))
            .With("/Sig", "signature")
            .With("/Nonsense", "mystery")
            .Build();

        foreach (var name in new[] { "print", "signature", "mystery" })
        {
            var field = document.AcroForm.Fields[name];
            field.Name.Should().Be(name);
            field.HasKids.Should().BeFalse();
            field.ReadOnly.Should().BeFalse();
        }

        document.AcroForm.Fields.DescendantNames
            .Should().BeEquivalentTo(new[] { "print", "signature", "mystery" });
    }

    // ----- a read-only field ---------------------------------------------------------------------

    /// <summary>
    ///   <see cref="PdfAcroField.Value"/> has always refused to fill a read-only field, and the
    ///   typed properties that fill one went round it: <c>Text</c>, <c>Checked</c> and each
    ///   <c>SelectedIndex</c> wrote <c>/V</c> directly, so the same field could be filled by one
    ///   name and not by another. Each now asks the same question and gives the same answer.
    /// </summary>
    [Fact]
    public void AReadOnlyTextFieldRefusesTextAsItRefusesAValue()
    {
        var field = (PdfTextField)FormWith("/Tx", "surname", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.ReadOnly);
            f.Elements.SetString(PdfAcroField.Keys.V, "Bosch");
        }).AcroForm.Fields["surname"];

        var act = () => field.Text = "Brueghel";

        act.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        field.Text.Should().Be("Bosch", "a refused value leaves the field as it was");
    }

    [Fact]
    public void AReadOnlyCheckBoxRefusesToBeTicked()
    {
        var field = (PdfCheckBoxField)FormWith("/Btn", "agree", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.ReadOnly);
            AcroFormBuilder.WithOnAndOffAppearances(f);
        }).AcroForm.Fields["agree"];

        var act = () => field.Checked = true;

        act.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        field.Checked.Should().BeFalse();
    }

    [Fact]
    public void AReadOnlyRadioGroupRefusesAChoice()
    {
        var field = (PdfRadioButtonField)FormWith("/Btn", "size", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Radio | PdfAcroFieldFlags.ReadOnly);
            AcroFormBuilder.WithOptions(f, "small", "large");
        }).AcroForm.Fields["size"];

        var act = () => field.SelectedIndex = 1;

        act.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        field.SelectedIndex.Should().Be(-1);
    }

    [Fact]
    public void AReadOnlyComboBoxRefusesAChoice()
    {
        var field = (PdfComboBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.ReadOnly);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex");
        }).AcroForm.Fields["county"];

        var act = () => field.SelectedIndex = 1;

        act.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        field.SelectedIndex.Should().Be(-1);
    }

    [Fact]
    public void AReadOnlyListBoxRefusesAChoiceByOneIndexOrByMany()
    {
        var field = (PdfListBoxField)FormWith("/Ch", "county", f =>
        {
            AcroFormBuilder.WithFlags(f, PdfAcroFieldFlags.MultiSelect | PdfAcroFieldFlags.ReadOnly);
            AcroFormBuilder.WithOptions(f, "Kent", "Sussex", "Surrey");
        }).AcroForm.Fields["county"];

        var one = () => field.SelectedIndex = 1;
        var many = () => field.SelectedIndices = new[] { 0, 2 };

        one.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        many.Should().Throw<InvalidOperationException>().WithMessage("*read only*");
        field.SelectedIndices.Should().BeEmpty();
    }
}
