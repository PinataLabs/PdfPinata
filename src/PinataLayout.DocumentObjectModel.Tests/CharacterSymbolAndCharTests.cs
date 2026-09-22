using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   A Character holds either a symbol or a plain character, in two fields. They used to share one,
///   told apart by the top nibble of the value, and SymbolName still presents them as that one
///   value. These pin what the public properties read back after each kind of assignment, and that
///   the MDDDL written for every kind is what it was before the split.
/// </summary>
public class CharacterSymbolAndCharTests
{
    [Fact]
    public void ACharacterReplacesASymbolAndReadsBackThroughBothProperties()
    {
        var character = new Character { SymbolName = SymbolName.Euro };

        character.Char = 'x';

        character.Char.Should().Be('x');
        character.SymbolName.Should().Be((SymbolName)'x', "a character reads back as its own code");
    }

    [Fact]
    public void ASymbolReplacesACharacter()
    {
        var character = new Character { Char = 'x' };

        character.SymbolName = SymbolName.Bullet;

        character.SymbolName.Should().Be(SymbolName.Bullet);
        character.Char.Should().Be('\0', "a symbol is not a character");
    }

    [Fact]
    public void AssigningACharacterCodeToSymbolNameIsAssigningChar()
    {
        var character = new Character { SymbolName = (SymbolName)'q' };

        character.Char.Should().Be('q');
        character.SymbolName.Should().Be((SymbolName)'q');
    }

    [Fact]
    public void AddCharacterWithACharIsAddCharacterWithItsCode()
    {
        var paragraph = new Document().AddSection().AddParagraph();

        var byChar = paragraph.AddCharacter('Z', 2);
        var byCode = paragraph.AddCharacter((SymbolName)'Z', 2);

        byChar.Char.Should().Be(byCode.Char).And.Be('Z');
        byChar.SymbolName.Should().Be(byCode.SymbolName);
        byChar.Count.Should().Be(2);
    }

    [Theory]
    [InlineData(0xF0000000u)]
    [InlineData(0xF1000005u)]
    [InlineData(0x10000041u)]
    public void AnUndefinedSymbolIsRefused(uint value)
    {
        var character = new Character();

        var assigning = () => character.SymbolName = (SymbolName)value;

        assigning.Should().Throw<ArgumentException>(
            "a value with the top nibble set claims to be a symbol, and this one is not defined");
        character.SymbolName.Should().Be(default(SymbolName), "a refused value is not kept");
    }

    [Fact]
    public void ACodeAboveTheBasicPlaneIsKeptWholeAndCharReadsItsLowSixteenBits()
    {
        var character = new Character { SymbolName = (SymbolName)0x2200A };

        character.SymbolName.Should().Be((SymbolName)0x2200A);
        character.Char.Should().Be('\u200A');
    }

    [Fact]
    public void ACloneKeepsWhicheverOfTheTwoWasAssigned()
    {
        var symbol = new Character { SymbolName = SymbolName.EmDash };
        var letter = new Character { Char = 'k' };

        var symbolClone = (Character)symbol.Clone();
        var letterClone = (Character)letter.Clone();

        symbolClone.SymbolName.Should().Be(SymbolName.EmDash);
        symbolClone.Char.Should().Be('\0');
        letterClone.Char.Should().Be('k');
        letterClone.SymbolName.Should().Be((SymbolName)'k');
    }

    [Fact]
    public void EveryKindOfCharacterIsWrittenExactlyAsItWasBeforeTheSplit()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddCharacter(SymbolName.Euro);
        paragraph.AddSpace(1);
        paragraph.AddSpace(3);
        paragraph.AddCharacter(SymbolName.Em);
        paragraph.AddCharacter(SymbolName.Em, 2);
        paragraph.AddTab();
        paragraph.AddLineBreak();
        paragraph.AddCharacter(SymbolName.Tab, 2);
        paragraph.AddCharacter('A');
        paragraph.AddCharacter('A', 3);
        paragraph.AddCharacter((SymbolName)0x2200A);
        paragraph.AddCharacter('\0');

        var ddl = DdlWriter.WriteToString(document);
        var body = ddl[ddl.IndexOf("\\section", StringComparison.Ordinal)..];

        // Captured from the single-field implementation, byte for byte.
        body.Should().Be(
            "\\section\r\n" +
            "  {\r\n" +
            "    \\symbol(Euro)\\space(1)\\space(3)\\space(Em)\\space(Em, 2)\\tab \\linebreak\r\n" +
            "    \\symbol(Tab) \\chr(0x41) \\chr(0x41) \\chr(0x2200A) \\chr(0x0)\r\n" +
            "  }\r\n" +
            "}\r\n");
    }

    [Fact]
    public void EveryReadableKindRoundTripsThroughMdddl()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddCharacter(SymbolName.Euro);
        paragraph.AddSpace(3);
        paragraph.AddCharacter(SymbolName.Em, 2);
        paragraph.AddTab();
        paragraph.AddLineBreak();
        paragraph.AddCharacter('A');

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));
        var characters = ((Paragraph)reread.LastSection.Elements[0]).Elements.OfType<Character>().ToList();

        characters.Select(c => (c.SymbolName, c.Char, c.Count)).Should().Equal(
            (SymbolName.Euro, '\0', 1),
            (SymbolName.Blank, '\0', 3),
            (SymbolName.Em, '\0', 2),
            (SymbolName.Tab, '\0', 1),
            (SymbolName.LineBreak, '\0', 1),
            ((SymbolName)'A', 'A', 1));
    }
}
