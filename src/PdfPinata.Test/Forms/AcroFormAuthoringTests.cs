using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Forms;

/// <summary>
///   Authoring an interactive form through the typed API, which until now could only fill one in.
/// </summary>
/// <remarks>
///   Every constructor under <c>PdfPinata.Pdf.AcroForms</c> was internal, the field collection
///   had no <c>Add</c>, <c>PdfWidgetAnnotation</c> was internal and <c>PdfDocument.Catalog</c> is
///   still internal, so a caller who wanted to make a form assembled ISO 32000-1 section 12.7 out
///   of raw dictionaries. These tests are the other half of the fix: the form built here is read
///   back through <c>PdfReader</c> and has to come out fully typed, because a form that is right
///   in memory and wrong in the file looks identical from the calling side.
/// </remarks>
public class AcroFormAuthoringTests
{
    [Fact]
    public void ADocumentHasNoFormUntilOneIsAskedFor()
    {
        var document = new PdfDocument();

        document.AcroForm.Should().BeNull();

        var form = document.GetOrCreateAcroForm();

        form.Should().NotBeNull();
        document.AcroForm.Should().BeSameAs(form);
    }

    [Fact]
    public void AskingTwiceAnswersTheSameFormRatherThanASecondOne()
    {
        var document = new PdfDocument();

        var first = document.GetOrCreateAcroForm();
        var second = document.GetOrCreateAcroForm();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void TheFormIsIndirectAndIsNamedByTheCatalogue()
    {
        var document = new PdfDocument();

        var form = document.GetOrCreateAcroForm();

        form.Reference.Should().NotBeNull();
        document.Internals.Catalog.Elements["/AcroForm"].Should().BeOfType<Pdf.Advanced.PdfReference>();
    }

    [Theory]
    [InlineData(typeof(PdfTextField), "/Tx", 0)]
    [InlineData(typeof(PdfCheckBoxField), "/Btn", 0)]
    [InlineData(typeof(PdfRadioButtonField), "/Btn", (int)PdfAcroFieldFlags.Radio)]
    [InlineData(typeof(PdfPushButtonField), "/Btn", (int)PdfAcroFieldFlags.Pushbutton)]
    [InlineData(typeof(PdfComboBoxField), "/Ch", (int)PdfAcroFieldFlags.Combo)]
    [InlineData(typeof(PdfListBoxField), "/Ch", 0)]
    [InlineData(typeof(PdfSignatureField), "/Sig", 0)]
    public void EveryFieldTypeWritesWhatSaysWhatItIs(Type type, string fieldType, int flags)
    {
        var document = new PdfDocument();

        var field = (PdfAcroField)Activator.CreateInstance(type, document);

        // /FT and the one flag that tells the three buttons and the two choices apart. Left to the
        // caller, either of them missing turns the field into something else on the way back in.
        // ReSharper disable once PossibleNullReferenceException
        field.Elements.GetName("/FT").Should().Be(fieldType);
        ((int)field.Flags).Should().Be(flags);
    }

    [Fact]
    public void AddingAFieldMakesItIndirectAndPutsAReferenceInFields()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        field.Reference.Should().BeNull();

        form.Fields.Add(field);

        field.Reference.Should().NotBeNull();
        form.Fields.Elements.Count.Should().Be(1);
        form.Fields.Elements[0].Should().BeOfType<Pdf.Advanced.PdfReference>();
    }

    [Fact]
    public void AFieldFromAnotherDocumentIsRefused()
    {
        var document = new PdfDocument();
        var elsewhere = new PdfDocument();

        var field = new PdfTextField(elsewhere);

        var act = () => document.GetOrCreateAcroForm().Fields.Add(field);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TheNameTheToolTipAndTheFlagsAreWritable()
    {
        var document = new PdfDocument();
        var field = new PdfTextField(document);

        field.Name = "fullName";
        field.ToolTip = "Your name as it appears on your passport";
        field.Flags = PdfAcroFieldFlags.Required | PdfAcroFieldFlags.DoNotScroll;

        field.Elements.GetString("/T").Should().Be("fullName");
        field.Elements.GetString("/TU").Should().Be("Your name as it appears on your passport");
        field.Flags.Should().Be(PdfAcroFieldFlags.Required | PdfAcroFieldFlags.DoNotScroll);
    }

    [Fact]
    public void APartialNameWithAPeriodInItIsRefused()
    {
        var document = new PdfDocument();
        var field = new PdfTextField(document);

        // "name.full" is the obvious thing to write and the one thing it cannot mean: a period
        // joins two partial names, so the field would be looked for under a parent called "name"
        // and never found. Refused at the call rather than discovered when the form does nothing.
        Action act = () => field.Name = "name.full";

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ADottedPathIsSpeltAsFieldsNestedInsideFields()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        // A non-terminal field: a container for the name and for whatever its children inherit,
        // with no type or value of its own. /Kids is the same collection /Fields is, so the same
        // Add serves for both.
        var group = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(group);

        var full = new PdfTextField(document) { Name = "full" };
        group.Fields.Add(full);
        full.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        full.Text = "Ada Lovelace";

        var reopened = SaveAndReopen(document);

        reopened.AcroForm.Fields["name.full"].Should().BeOfType<PdfTextField>();
        ((PdfTextField)reopened.AcroForm.Fields["name.full"]).Text.Should().Be("Ada Lovelace");
        reopened.AcroForm.Fields.DescendantNames.Should().Contain("name.full");
    }

    [Fact]
    public void TheCombFlagIsBitTwentyFive()
    {
        var document = new PdfDocument();
        var field = new PdfTextField(document);

        // The one field flag the enumeration was missing, so a caller wanting a postcode drawn in
        // equal cells wrote 1 << 24 by hand and lost the enumeration.
        field.MaxLength = 6;
        field.Flags = PdfAcroFieldFlags.Comb;

        field.Elements.GetInteger("/Ff").Should().Be(1 << 24);
    }

    [Fact]
    public void AWidgetGoesOnThePageAndPointsBackAtItsField()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);

        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        widget.Elements.GetName("/Subtype").Should().Be("/Widget");
        page.Annotations.Count.Should().Be(1);

        // The three links that make a widget part of a form rather than a loose annotation.
        widget.Elements["/Parent"].Should().BeOfType<Pdf.Advanced.PdfReference>();
        widget.Elements["/P"].Should().BeOfType<Pdf.Advanced.PdfReference>();
        field.Elements.GetArray("/Kids").Elements.Count.Should().Be(1);
    }

    [Fact]
    public void AWidgetIsMarkedAsPrintingBecauseAFormThatVanishesOnPaperIsAlmostNeverMeant()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);

        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        widget.Flags.Should().Be(PdfAnnotationFlags.Print);
    }

    [Fact]
    public void AFieldThatIsNotYetOnAFormCannotBePutOnAPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        var field = new PdfTextField(document) { Name = "fullName" };

        // Without a reference there is nothing for the widget's /Parent to name, and the failure
        // would otherwise surface as a form whose field is invisible.
        Action act = () => field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AStandardFontIsRegisteredInTheDefaultResources()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        form.AddStandardFont("Helv", "Helvetica");

        var fonts = form.DefaultResources.Elements.GetDictionary("/Font");
        var helvetica = fonts.Elements.GetDictionary("/Helv");

        helvetica.Elements.GetName("/BaseFont").Should().Be("/Helvetica");
        helvetica.Elements.GetName("/Subtype").Should().Be("/Type1");
        helvetica.Elements.GetName("/Encoding").Should().Be("/WinAnsiEncoding");
    }

    [Fact]
    public void ASymbolicFontKeepsItsOwnEncodingRatherThanBeingGivenWinAnsi()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        form.AddStandardFont("/ZaDb", "/ZapfDingbats");

        var dingbats = form.DefaultResources.Elements
            .GetDictionary("/Font").Elements.GetDictionary("/ZaDb");

        // WinAnsi would override the built-in encoding, and ZapfDingbats is how a check box draws
        // its tick - so the tick would come out as whatever letter that code point is in WinAnsi.
        dingbats.Elements.ContainsKey("/Encoding").Should().BeFalse();
    }

    [Fact]
    public void TheDefaultResourcesAreMadeOnceAndSharedRatherThanRebuilt()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        var first = form.DefaultResources;

        form.DefaultResources.Should().BeSameAs(first);
        first.Reference.Should().NotBeNull();
    }

    [Fact]
    public void AChoiceFieldsOptionsRoundTrip()
    {
        var document = new PdfDocument();
        var field = new PdfComboBoxField(document);

        field.Options = new[] { "Australia", "Canada", "Ireland" };

        field.Options.Should().Equal("Australia", "Canada", "Ireland");
        field.Elements.GetArray("/Opt").Elements.Count.Should().Be(3);
    }

    [Fact]
    public void ACheckBoxTogglesBetweenTheAppearancesItsWidgetWasGiven()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var box = new PdfCheckBoxField(document) { Name = "subscribe" };
        form.Fields.Add(box);
        var widget = box.AddWidget(page, new PdfRectangle(new XRect(60, 700, 16, 16)));

        var ticked = new XForm(document, new XSize(16, 16));
        using (var gfx = XGraphics.FromForm(ticked))
            gfx.DrawLine(new XPen(XColors.Black, 2), 3, 8, 13, 8);

        // The off state of a tick box is an empty content stream, which is the case that used to
        // throw: XForm.Finish disposed a graphics object that had never been made.
        var blank = new XForm(document, new XSize(16, 16));

        widget.SetAppearance("/Yes", ticked);
        widget.SetAppearance("/Off", blank);

        box.Checked = true;

        widget.Elements.GetName("/AS").Should().Be("/Yes");
        box.Elements.GetName("/V").Should().Be("/Yes");
        box.Checked.Should().BeTrue();
    }

    [Fact]
    public void ATextFieldDrawsItsValueIntoTheWidgetRatherThanIntoNothing()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        field.Text = "Ada Lovelace";

        // The rectangle belongs to the widget, so the appearance does too. Reading /Rect off the
        // field - which is what this used to do whatever the field's shape - gave a form of no
        // size, hung where no reader looks.
        widget.Elements.GetDictionary("/AP").Should().NotBeNull();
        field.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    [Fact]
    public void StylingATextFieldDrawsItRatherThanWaitingForAValue()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        widget.Elements.ContainsKey("/AP").Should().BeFalse();

        // The colours used to be read only when the value changed, so setting one on a field that
        // never gets a value did nothing at all - a box the caller asked for and never got.
        field.BackColor = XColors.WhiteSmoke;
        field.BorderColor = XColors.Gray;

        widget.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void AFieldStyledBeforeItIsPlacedIsStillDrawn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);

        // Described first, placed second - which is the order a caller writes as often as the
        // other, and until now the order that lost everything set before the widget existed.
        field.BackColor = XColors.WhiteSmoke;
        field.Text = "Ada Lovelace";

        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        widget.Elements.GetDictionary("/AP").Should().NotBeNull();
    }

    [Fact]
    public void AnUndecoratedTextFieldKeepsNoAppearanceSoThatMkStillDecoratesIt()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);
        var widget = field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        field.Text = "Ada Lovelace";
        widget.Elements.GetDictionary("/AP").Should().NotBeNull();

        field.Text = "";

        // An appearance is what a reader shows in place of building one from /MK, so an empty one
        // blanks a field decorated that way rather than leaving it alone.
        widget.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    [Fact]
    public void ARadioGroupsExportValuesRoundTrip()
    {
        var document = new PdfDocument();
        var delivery = new PdfRadioButtonField(document) { Name = "delivery" };

        delivery.Options = new[] { "Standard", "Express", "Collect" };
        delivery.SelectedIndex = 1;

        delivery.Options.Should().Equal("Standard", "Express", "Collect");
        delivery.Elements.GetName("/V").Should().Be("/Express");
        delivery.SelectedIndex.Should().Be(1);
    }

    [Fact]
    public void AFieldMayNameItsOwnDefaultAppearance()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();
        form.DefaultAppearance = "/Helv 0 Tf 0 g";

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);
        field.DefaultAppearance = "/Helv 9 Tf 0 g";

        // A size of zero is auto-size, and what a reader makes of that on a multiline field is
        // its own business - so a field naming a real one is what makes a form look the same
        // everywhere, and it has to be able to disagree with the form.
        field.Elements.GetString("/DA").Should().Be("/Helv 9 Tf 0 g");
        form.Elements.GetString("/DA").Should().Be("/Helv 0 Tf 0 g");
    }

    [Fact]
    public void TheAppearanceStateSaysWhichOfSeveralAppearancesIsShowing()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var delivery = new PdfRadioButtonField(document) { Name = "delivery" };
        form.Fields.Add(delivery);
        var button = delivery.AddWidget(page, new PdfRectangle(new XRect(60, 700, 14, 14)));

        button.AppearanceState.Should().BeNull();

        button.SetAppearance("/Standard", new XForm(document, new XSize(14, 14)));
        button.SetAppearance("/Off", new XForm(document, new XSize(14, 14)));

        // SetAppearance points /AS at whatever it has just written, because an appearance nobody
        // is showing is invisible. Exactly one button of a group has to be told otherwise.
        button.AppearanceState.Should().Be("/Off");

        button.AppearanceState = "Standard";
        button.AppearanceState.Should().Be("/Standard");
    }

    [Fact]
    public void AFormBuiltThroughTheTypedApiComesBackThroughTheReaderFullyTyped()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        form.NeedAppearances = true;
        form.DefaultAppearance = "/Helv 9 Tf 0 g";
        form.AddStandardFont("/Helv", "/Helvetica");

        var name = new PdfTextField(document) { Name = "fullName", ToolTip = "Your name" };
        form.Fields.Add(name);
        name.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));
        name.Text = "Ada Lovelace";

        var country = new PdfComboBoxField(document) { Name = "country" };
        form.Fields.Add(country);
        country.AddWidget(page, new PdfRectangle(new XRect(60, 660, 200, 20)));
        country.Options = new[] { "Ireland", "United Kingdom" };
        country.SelectedIndex = 1;

        var delivery = new PdfRadioButtonField(document) { Name = "delivery" };
        form.Fields.Add(delivery);
        delivery.AddWidget(page, new PdfRectangle(new XRect(60, 620, 14, 14)));
        delivery.AddWidget(page, new PdfRectangle(new XRect(90, 620, 14, 14)));

        var reopened = SaveAndReopen(document);

        var read = reopened.AcroForm;
        read.Should().NotBeNull();
        read.NeedAppearances.Should().BeTrue();
        read.DefaultAppearance.Should().Be("/Helv 9 Tf 0 g");

        read.Fields["fullName"].Should().BeOfType<PdfTextField>();
        read.Fields["country"].Should().BeOfType<PdfComboBoxField>();
        read.Fields["delivery"].Should().BeOfType<PdfRadioButtonField>();

        ((PdfTextField)read.Fields["fullName"]).Text.Should().Be("Ada Lovelace");
        ((PdfComboBoxField)read.Fields["country"]).SelectedIndex.Should().Be(1);
        read.Fields["delivery"].Elements.GetArray("/Kids").Elements.Count.Should().Be(2);
    }

    [Fact]
    public void EveryWidgetIsOnThePageItWasPutOn()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "notes" };
        form.Fields.Add(field);
        field.AddWidget(first, new PdfRectangle(new XRect(60, 700, 200, 20)));
        field.AddWidget(second, new PdfRectangle(new XRect(60, 700, 200, 20)));

        // One field appearing in two places is the whole reason a widget is a separate object.
        first.Annotations.Count.Should().Be(1);
        second.Annotations.Count.Should().Be(1);
        field.Elements.GetArray("/Kids").Elements.Count.Should().Be(2);
    }

    [Fact]
    public void AFieldNestedUnderAnotherPointsBackAtIt()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        var group = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(group);

        var full = new PdfTextField(document) { Name = "full" };
        group.Fields.Add(full);

        // ISO 32000-1 Table 220: /Parent is required of a field that is the child of another.
        // Nothing here needs it - every lookup in this library walks down from /Fields - but a
        // reader assembling a field's full name walks up, and has nothing to walk up.
        full.Elements.GetReference(PdfAcroField.Keys.Parent).Value.Should().BeSameAs(group);
    }

    [Fact]
    public void ARootFieldPointsBackAtNothing()
    {
        var document = new PdfDocument();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "fullName" };
        form.Fields.Add(field);

        // The same collection class is a form's /Fields and a field's /Kids, and the entry is
        // required of the one and forbidden of the other. A root field having one would claim a
        // parent that does not list it.
        field.Elements.ContainsKey(PdfAcroField.Keys.Parent).Should().BeFalse();
    }

    [Fact]
    public void TheParentChainSurvivesTheFile()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var group = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(group);

        var full = new PdfTextField(document) { Name = "full" };
        group.Fields.Add(full);
        full.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

        var reopened = SaveAndReopen(document);

        var read = reopened.AcroForm.Fields["name.full"];
        var parent = (PdfDictionary)read.Elements.GetReference(PdfAcroField.Keys.Parent).Value;

        parent.Elements.GetString(PdfAcroField.Keys.T).Should().Be("name");
    }

    [Theory]
    [InlineData("combo")]
    [InlineData("radio")]
    [InlineData("push")]
    public void AssigningFlagsDoesNotAssignAwayWhatKindOfFieldItIs(string kind)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        // The bit that says which kind of /Btn or /Ch this is written by the constructor, and the
        // public Flags setter replaces /Ff outright - so a caller asking for one unrelated flag
        // used to hand back a field of a different type, and only reopening the file said so.
        PdfAcroField field = kind switch
        {
            "combo" => new PdfComboBoxField(document),
            "radio" => new PdfRadioButtonField(document),
            _ => new PdfPushButtonField(document)
        };
        field.Name = kind;
        form.Fields.Add(field);
        field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 120, 20)));
        field.Flags = PdfAcroFieldFlags.Required;

        field.Flags.Should().HaveFlag(PdfAcroFieldFlags.Required);

        var read = SaveAndReopen(document).AcroForm.Fields[kind];

        switch (kind)
        {
            case "combo":
                read.Should().BeOfType<PdfComboBoxField>();
                break;
            case "radio":
                read.Should().BeOfType<PdfRadioButtonField>();
                break;
            default:
                read.Should().BeOfType<PdfPushButtonField>();
                break;
        }
    }

    [Fact]
    public void AKindOfFieldCannotBeAssignedOntoAnother()
    {
        var document = new PdfDocument();

        // The other direction: a check box is the /Btn that says neither Pushbutton nor Radio, so
        // a caller writing Radio onto one is describing a field the class is not.
        var box = new PdfCheckBoxField(document);
        box.Flags = PdfAcroFieldFlags.Radio | PdfAcroFieldFlags.Required;

        box.Flags.Should().Be(PdfAcroFieldFlags.Required);

        // And a list box is the /Ch that does not say Combo.
        var list = new PdfListBoxField(document);
        list.Flags = PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.MultiSelect;

        list.Flags.Should().Be(PdfAcroFieldFlags.MultiSelect);
    }

    [Fact]
    public void AFieldOfNoRoomToDrawInIsLeftUndrawnRatherThanRefused()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();

        var field = new PdfTextField(document) { Name = "sliver" };
        form.Fields.Add(field);

        // Asked for something, so that it is the size and not the emptiness that stops the
        // drawing - a field asked for nothing removes its appearance either way.
        field.BackColor = XColors.White;

        // XForm's floor is a point in each direction, and the guard used to be against zero - so
        // a rectangle between the two got past it and threw out of a property setter.
        Action placing = () => field.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 0.5)));

        placing.Should().NotThrow();
        page.Annotations[0].Elements.ContainsKey("/AP").Should().BeFalse();

        // A point high is enough, and then it does draw.
        field.AddWidget(page, new PdfRectangle(new XRect(60, 660, 200, 1)));

        page.Annotations[1].Elements.ContainsKey("/AP").Should().BeTrue();
    }

    private static PdfDocument SaveAndReopen(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        // Named in full: this assembly has a test class called PdfReader too, and it wins.
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }
}
