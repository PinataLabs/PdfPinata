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
///   an out-of-range assignment still throws, and that Character - whose SymbolName property also
///   reads and writes plain characters - lets a character through the check, as it did under NEnum.
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

        foreach (var alignment in Enum.GetValues<ParagraphAlignment>())
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
        var section = ddl[ddl.IndexOf("\\section", StringComparison.Ordinal)..];

        section.Should().Contain("Alignment", "the assigned enum is written");
        section.Should().NotContain("OutlineLevel", "an enum nobody assigned stays out of the output");
    }

    /// <summary>
    ///   Character keeps a character and a symbol in separate fields, but SymbolName presents them
    ///   as one value told apart by the top nibble, as it did when they shared a field. A character
    ///   read through it is therefore not a defined SymbolName, which is why NEnum carved SymbolName
    ///   out of its own validation and why EnumGuard applies only to a value with the nibble set.
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

    /// <summary>
    ///   The character and the symbol live in separate fields, but the value model still knows
    ///   them by the one name, SymbolName, and answers for a character as it did when they shared a
    ///   field.
    /// </summary>
    [Fact]
    public void TheValueModelSeesACharacterUnderSymbolName()
    {
        var character = new Character { Char = 'A' };

        character.IsNull("SymbolName").Should().BeFalse();
        character.GetValue("SymbolName", GV.GetNull).Should().Be((SymbolName)'A');
    }

    [Fact]
    public void SetValueOnSymbolNameReachesWhicheverFieldTheValueBelongsTo()
    {
        var character = new Character();

        character.SetValue("SymbolName", (int)'B');
        character.Char.Should().Be('B');

        character.SetValue("SymbolName", SymbolName.Bullet);
        character.SymbolName.Should().Be(SymbolName.Bullet);
        character.Char.Should().Be('\0');
    }

    [Fact]
    public void SetNullOnSymbolNameClearsACharacterAndASymbolAlike()
    {
        var letter = new Character { Char = 'A' };
        var symbol = new Character { SymbolName = SymbolName.Euro };

        letter.SetNull("SymbolName");
        symbol.SetNull("SymbolName");

        letter.IsNull("SymbolName").Should().BeTrue();
        letter.Char.Should().Be('\0');
        symbol.IsNull("SymbolName").Should().BeTrue();
        symbol.SymbolName.Should().Be(default);
    }
}
