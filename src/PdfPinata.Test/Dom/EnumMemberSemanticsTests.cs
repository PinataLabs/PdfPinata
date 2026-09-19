using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   NEnum was the last of the nullable wrapper structs. It stored an int plus the enum's Type,
///   marked "not set" with int.MinValue, and validated assignments with Enum.IsDefined. Enum members
///   are now plain TEnum? fields, and the range check moved to the public setters as EnumGuard.
///
///   These pin the behaviour that had to survive that move: what an unset enum reads back as, that
///   an out-of-range assignment still throws, and that Character - which stores raw characters in
///   the same field its SymbolName property uses - is exempt from the check, as it was under NEnum.
/// </summary>
public class EnumMemberSemanticsTests
{
    static Border ABorder() => new Document().AddSection().AddParagraph().Format.Borders.Top;

    static ParagraphFormat AFormat() => new Document().AddSection().AddParagraph().Format;

    [Fact]
    public void AnUnsetEnumReadsBackAsTheZeroValue()
    {
        var border = ABorder();

        border.IsNull("Style").Should().BeTrue("nothing has assigned it");
        border.Style.Should().Be(BorderStyle.None, "NEnum read back 0 when null and TEnum? must too");
    }

    [Fact]
    public void AnUnsetEnumIsNullOnlyUnderGetNull()
    {
        var border = ABorder();

        border.GetValue("Style", GV.GetNull).Should().BeNull();
        border.GetValue("Style", GV.ReadWrite).Should().Be(BorderStyle.None);
    }

    [Fact]
    public void AssigningTheZeroValueIsNotTheSameAsLeavingItUnset()
    {
        var border = ABorder();

        border.Style = BorderStyle.None;

        border.IsNull("Style").Should().BeFalse("an explicit assignment is a value, not an absence");
        border.GetValue("Style", GV.GetNull).Should().Be(BorderStyle.None);
    }

    [Fact]
    public void SetNullReturnsAnEnumToUnset()
    {
        var border = ABorder();
        border.Style = BorderStyle.DashDot;

        border.SetNull("Style");

        border.IsNull("Style").Should().BeTrue();
        border.Style.Should().Be(BorderStyle.None);
    }

    [Fact]
    public void AnUndefinedEnumValueIsStillRejected()
    {
        var border = ABorder();

        var assign = () => border.Style = (BorderStyle)999;

        assign.Should().Throw<ArgumentException>("EnumGuard carries forward NEnum's Enum.IsDefined check");
        border.IsNull("Style").Should().BeTrue("the rejected assignment left nothing behind");
    }

    /// <summary>
    ///   NEnum held the value as an int and its setter took one, so an int handed to the model API
    ///   reached an enum member. A TEnum? member is assigned by cast, and unboxing has to name the
    ///   boxed type exactly - so without the conversion the generated setter makes, this throws
    ///   InvalidCastException on a call that has worked since the port.
    /// </summary>
    [Fact]
    public void AnEnumMemberStillTakesAnIntThroughTheModelApi()
    {
        var border = ABorder();

        border.SetValue("Style", (int)BorderStyle.DashLargeGap);

        border.Style.Should().Be(BorderStyle.DashLargeGap);
        border.GetValue("Style", GV.GetNull).Should().Be(BorderStyle.DashLargeGap);
    }

    [Fact]
    public void EveryDefinedValueIsAccepted()
    {
        var format = AFormat();

        foreach (ParagraphAlignment alignment in Enum.GetValues<ParagraphAlignment>())
        {
            format.Alignment = alignment;
            format.Alignment.Should().Be(alignment);
        }
    }

    [Fact]
    public void AnEnumSurvivesTheDdlRoundTrip()
    {
        var document = new Document();
        document.AddSection().AddParagraph().Format.Borders.Top.Style = BorderStyle.DashLargeGap;

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        var paragraph = (Paragraph)reread.LastSection.Elements[0];
        paragraph.Format.Borders.Top.Style.Should().Be(BorderStyle.DashLargeGap);
    }

    [Fact]
    public void AnUnsetEnumIsNotWrittenToDdl()
    {
        var document = new Document();
        document.AddSection().AddParagraph().Format.Alignment = ParagraphAlignment.Center;

        // Only the section body. The built-in Heading1..9 styles assign OutlineLevel themselves, so
        // the \styles block legitimately mentions it whatever this paragraph does.
        var ddl = DdlWriter.WriteToString(document);
        var section = ddl.Substring(ddl.IndexOf("\\section", StringComparison.Ordinal));

        section.Should().Contain("Alignment", "the assigned enum is written");
        section.Should().NotContain("OutlineLevel", "an enum nobody assigned stays out of the output");
    }

    /// <summary>
    ///   Character.Char writes raw character values through the same field SymbolName reads, and
    ///   separates the two by the top nibble. Most of what the field legitimately holds is therefore
    ///   not a defined SymbolName, which is why NEnum carved SymbolName out of its own validation
    ///   and why the migrated property must not take EnumGuard.
    /// </summary>
    [Fact]
    public void CharacterAcceptsRawCharactersThroughTheSymbolNameField()
    {
        var character = new Character { Char = 'A' };

        character.Char.Should().Be('A');
        character.SymbolName.Should().Be((SymbolName)'A', "the raw value is what is stored");
    }

    [Fact]
    public void CharacterStillDistinguishesASymbolFromACharacter()
    {
        var symbol = new Character { SymbolName = SymbolName.Euro };
        var letter = new Character { Char = 'Z' };

        symbol.Char.Should().Be('\0', "a symbol name has its top nibble set, so it is not a character");
        letter.SymbolName.Should().Be((SymbolName)'Z');
        letter.Char.Should().Be('Z');
    }

    [Fact]
    public void AnUnsetCharacterReadsBackAsZero()
    {
        var character = new Character();

        character.Char.Should().Be('\0');
        character.SymbolName.Should().Be(default);
    }
}
