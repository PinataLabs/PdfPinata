using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Shapes;
using TUnit.Core;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Pins the observable behaviour of the DOM's nullable value types - NBool, NInt, NDouble and
///   NString - so that replacing them with bool?, int?, double? and string can be shown to change
///   nothing a caller can see.
///
///   Three things matter and are easy to break. A value that was never set reads as a default
///   rather than throwing. A value explicitly set to that same default is still distinguishable
///   from one never set, because only the latter is omitted from serialized DDL. And SetNull puts
///   a value back to never-set.
/// </summary>
public class NullableValueSemanticsTests
{
    // A DOM object per underlying type, reached the way a caller would reach it.
    private static Font AFont() => new Document().AddSection().AddParagraph("x").Format.Font;

    private static PageSetup APageSetup() => new Document().AddSection().PageSetup;

    private static DocumentInfo ADocumentInfo() => new Document().Info;

    private static Image AnImage() => new();

    // ---------------------------------------------------------------- unset reads as a default

    [Test]
    public void AnUnsetBooleanReadsAsFalse()
    {
        var font = AFont();

        font.Bold.Should().BeFalse();
        font.IsNull("Bold").Should().BeTrue();
    }

    [Test]
    public void AnUnsetIntegerReadsAsZero()
    {
        var pageSetup = APageSetup();

        pageSetup.StartingNumber.Should().Be(0);
        pageSetup.IsNull("StartingNumber").Should().BeTrue();
    }

    [Test]
    public void AnUnsetDoubleReadsAsZero()
    {
        var image = AnImage();

        image.ScaleWidth.Should().Be(0);
        image.IsNull("ScaleWidth").Should().BeTrue();
    }

    [Test]
    public void AnUnsetStringReadsAsEmpty()
    {
        var info = ADocumentInfo();

        info.Title.Should().BeEmpty();
        info.IsNull("Title").Should().BeTrue();
    }

    // ------------------------------------------- a value set to its default is not the same as unset

    [Test]
    public void ABooleanSetToFalseIsNotNull()
    {
        var font = AFont();

        font.Bold = false;

        font.Bold.Should().BeFalse();
        font.IsNull("Bold").Should().BeFalse();
    }

    [Test]
    public void AnIntegerSetToZeroIsNotNull()
    {
        var pageSetup = APageSetup();

        pageSetup.StartingNumber = 0;

        pageSetup.StartingNumber.Should().Be(0);
        pageSetup.IsNull("StartingNumber").Should().BeFalse();
    }

    [Test]
    public void ADoubleSetToZeroIsNotNull()
    {
        var image = AnImage();

        image.ScaleWidth = 0;

        image.ScaleWidth.Should().Be(0);
        image.IsNull("ScaleWidth").Should().BeFalse();
    }

    [Test]
    public void AStringSetToEmptyIsNotNull()
    {
        var info = ADocumentInfo();

        info.Title = "";

        info.Title.Should().BeEmpty();
        info.IsNull("Title").Should().BeFalse();
    }

    // ------------------------------------------------------------------------- values round-trip

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void ABooleanRoundTripsThroughTheProperty(bool value)
    {
        var font = AFont();

        font.Bold = value;

        font.Bold.Should().Be(value);
    }

    [Test]
    [Arguments(1)]
    [Arguments(-1)]
    [Arguments(int.MaxValue)]
    public void AnIntegerRoundTripsThroughTheProperty(int value)
    {
        var pageSetup = APageSetup();

        pageSetup.StartingNumber = value;

        pageSetup.StartingNumber.Should().Be(value);
    }

    [Test]
    [Arguments(1.5)]
    [Arguments(-1.5)]
    [Arguments(double.MaxValue)]
    public void ADoubleRoundTripsThroughTheProperty(double value)
    {
        var image = AnImage();

        image.ScaleWidth = value;

        image.ScaleWidth.Should().Be(value);
    }

    [Test]
    [Arguments("Arial")]
    [Arguments(" leading and trailing ")]
    public void AStringRoundTripsThroughTheProperty(string value)
    {
        var info = ADocumentInfo();

        info.Title = value;

        info.Title.Should().Be(value);
    }

    // ----------------------------------------------------------------------- SetNull undoes a set

    [Test]
    public void SettingABooleanAndThenNullingItReadsAsUnsetAgain()
    {
        var font = AFont();

        font.Bold = true;
        font.SetNull("Bold");

        font.Bold.Should().BeFalse();
        font.IsNull("Bold").Should().BeTrue();
    }

    [Test]
    public void SettingAnIntegerAndThenNullingItReadsAsUnsetAgain()
    {
        var pageSetup = APageSetup();

        pageSetup.StartingNumber = 5;
        pageSetup.SetNull("StartingNumber");

        pageSetup.StartingNumber.Should().Be(0);
        pageSetup.IsNull("StartingNumber").Should().BeTrue();
    }

    [Test]
    public void SettingADoubleAndThenNullingItReadsAsUnsetAgain()
    {
        var image = AnImage();

        image.ScaleWidth = 2.5;
        image.SetNull("ScaleWidth");

        image.ScaleWidth.Should().Be(0);
        image.IsNull("ScaleWidth").Should().BeTrue();
    }

    [Test]
    public void SettingAStringAndThenNullingItReadsAsUnsetAgain()
    {
        var info = ADocumentInfo();

        info.Title = "Title";
        info.SetNull("Title");

        info.Title.Should().BeEmpty();
        info.IsNull("Title").Should().BeTrue();
    }

    // ------------------------------------------------- the reflection layer sees the same values

    [Test]
    public void GetValueReturnsNullForAnUnsetValueAndTheValueOnceSet()
    {
        var font = AFont();

        font.GetValue("Bold", GV.GetNull).Should().BeNull();

        font.Bold = true;

        font.GetValue("Bold", GV.GetNull).Should().Be(true);
        font.GetValue("Bold").Should().Be(true);
    }

    [Test]
    [Arguments("Bold", true)]
    [Arguments("Italic", false)]
    public void SetValueThroughTheReflectionLayerIsVisibleOnTheProperty(string name, bool value)
    {
        var font = AFont();

        font.SetValue(name, value);

        font.IsNull(name).Should().BeFalse();
        font.GetValue(name, GV.GetNull).Should().Be(value);
    }

    [Test]
    public void SetValueThroughTheReflectionLayerCarriesEachUnderlyingType()
    {
        var pageSetup = APageSetup();
        var info = ADocumentInfo();
        var image = AnImage();

        pageSetup.SetValue("StartingNumber", 12);
        info.SetValue("Title", "A title");
        image.SetValue("ScaleWidth", 3.25);

        pageSetup.StartingNumber.Should().Be(12);
        info.Title.Should().Be("A title");
        image.ScaleWidth.Should().Be(3.25);
    }

    [Test]
    public void GetValueOnAnUnsetValueOfEachUnderlyingTypeIsNull()
    {
        APageSetup().GetValue("StartingNumber", GV.GetNull).Should().BeNull();
        ADocumentInfo().GetValue("Title", GV.GetNull).Should().BeNull();
        AnImage().GetValue("ScaleWidth", GV.GetNull).Should().BeNull();
        AFont().GetValue("Bold", GV.GetNull).Should().BeNull();
    }

    // ------------------------------------------------------------------------------- IsNull() all

    [Test]
    public void AFreshObjectReportsItselfNull()
    {
        AFont().IsNull().Should().BeTrue();
        AnImage().IsNull().Should().BeTrue();
    }

    [Test]
    public void AnObjectWithOneValueSetNoLongerReportsItselfNull()
    {
        var font = AFont();

        font.Bold = true;

        font.IsNull().Should().BeFalse();
    }
}
