using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   NEnum is the last of the DOM's nullable wrapper structs. Unlike the four that were replaced by
///   bool?, int?, double? and string, it carries the enum's Type alongside the int, and uses it to
///   reject a value the enum does not define.
///
///   That validation is what a plain TEnum? would not do, so it decides whether NEnum can go. These
///   tests pin it either way.
/// </summary>
public class EnumValueTests
{
    private static Borders ABordersObject() =>
        new Document().AddSection().AddParagraph("Hello").Format.Borders;

    [Fact]
    public void AValueTheEnumDefinesIsAccepted()
    {
        var borders = ABordersObject();

        borders.Top.Style = BorderStyle.Dot;

        borders.Top.Style.Should().Be(BorderStyle.Dot);
    }

    [Fact]
    public void AValueTheEnumDoesNotDefineIsRejected()
    {
        var borders = ABordersObject();

        Action assigning = () => borders.Top.Style = (BorderStyle)999;

        assigning.Should().Throw<ArgumentException>(
            "NEnum validates against the enum type it carries; a plain BorderStyle? would not");
    }

    [Fact]
    public void AnUnsetEnumReadsAsItsFirstValue()
    {
        ABordersObject().Top.Style.Should().Be(0);
        ABordersObject().Top.IsNull("Style").Should().BeTrue();
    }

    [Fact]
    public void SettingAnEnumAndThenNullingItReadsAsUnsetAgain()
    {
        var borders = ABordersObject();

        borders.Top.Style = BorderStyle.DashDot;
        borders.Top.SetNull("Style");

        borders.Top.IsNull("Style").Should().BeTrue();
    }

    /// <summary>
    ///   Character.SymbolName is the exception NEnum carries a comment about: a value with the top
    ///   nibble clear is a character rather than a symbol, so it skips the check the others get.
    /// </summary>
    [Fact]
    public void TheSymbolNameEnumSkipsTheCheck()
    {
        var paragraph = new Document().AddSection().AddParagraph("Hello");

        Action assigning = () => paragraph.AddCharacter((SymbolName)0x2200A);

        assigning.Should().NotThrow();
    }

    [Fact]
    public void AnEnumRoundTripsThroughTheReflectionLayer()
    {
        var borders = ABordersObject();

        borders.Top.SetValue("Style", (int)BorderStyle.DashLargeGap);

        borders.Top.GetValue("Style", GV.GetNull).Should().Be(BorderStyle.DashLargeGap);
        borders.Top.Style.Should().Be(BorderStyle.DashLargeGap);
    }
}
