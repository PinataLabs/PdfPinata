using System;
using System.Globalization;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using Xunit;

namespace PdfPinata.Test.Pdfs.Content.Objects;

/// <summary>
///   What each kind of content object writes when the sequence holding it is turned back into
///   content with <see cref="CSequence.ToContent"/>, and the <c>ToString</c> that writing is
///   built on. <see cref="ContentRoundTripTests"/> covers what the parser hands back; most of the
///   objects here are built by hand, because that is the only way to reach the kinds and the
///   characters the parser never produces - a comment, which it throws away, and the control
///   characters a literal string has to escape.
/// </summary>
public class ContentObjectWritingTests
{
    [Fact]
    public void ACommentIsWrittenOnALineOfItsOwn()
    {
        var comment = new CComment { Text = "drawn by hand" };

        comment.Text.Should().Be("drawn by hand");
        comment.ToString().Should().Be("% drawn by hand");

        // A comment runs to the end of the line, so whatever follows it has to start a new one or
        // it would be commented out too.
        Written(new CSequence { comment, OpCodes.OperatorFromName("q") })
            .Should().Be("% drawn by hand\nq\n");
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(42, "42")]
    [InlineData(-7, "-7")]
    [InlineData(int.MaxValue, "2147483647")]
    [InlineData(int.MinValue, "-2147483648")]
    public void AnIntegerIsWrittenInDigitsAndEndedWithABlank(int value, string digits)
    {
        var integer = new CInteger { Value = value };

        integer.Value.Should().Be(value);
        integer.ToString().Should().Be(digits);
        Written(new CSequence { integer }).Should().Be(digits + " ");
    }

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(-2.25, "-2.25")]
    // At least one decimal, so a whole number still reads as a real.
    [InlineData(3.0, "3.0")]
    // At most ten, so a third is cut off rather than written to seventeen digits.
    [InlineData(1.0 / 3, "0.3333333333")]
    [InlineData(1e-11, "0.0")]
    public void ARealIsWrittenWithBetweenOneAndTenDecimals(double value, string text)
    {
        var real = new CReal { Value = value };

        real.Value.Should().Be(value);
        real.ToString().Should().Be(text);
        Written(new CSequence { real }).Should().Be(text + " ");
    }

    [Fact]
    public void ARealIsWrittenWithAPointWhateverTheCurrentCulture()
    {
        // A comma is a delimiter nowhere in PDF syntax, so a German decimal separator would turn
        // one operand into two. CurrentCulture is per thread, so this touches no other test.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Written(new CSequence { new CReal { Value = 1.5 } }).Should().Be("1.5 ");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("plain", "(plain)")]
    [InlineData("", "()")]
    [InlineData("a\nb", @"(a\nb)")]
    [InlineData("a\rb", @"(a\rb)")]
    [InlineData("a\tb", @"(a\tb)")]
    [InlineData("a\bb", @"(a\bb)")]
    [InlineData("a\fb", @"(a\fb)")]
    [InlineData("(a)", @"(\(a\))")]
    [InlineData(@"a\b", @"(a\\b)")]
    public void ALiteralStringEscapesWhatItCannotHoldAsItStands(string value, string written)
    {
        var text = new CString { Value = value };

        text.CStringType.Should().Be(CStringType.String, "a literal string is what a new one is");
        text.ToString().Should().Be(written);

        // No blank after it: the closing parenthesis is a delimiter, so nothing is needed.
        Written(new CSequence { text }).Should().Be(written);
    }

    [Fact]
    public void AStringReadWithEveryControlEscapeIsWrittenBackWithTheSameEscapes()
    {
        const string content = @"(1\r2\t3\b4\f5\n6) Tj";

        var sequence = ContentReader.ReadContent(Encoding.Latin1.GetBytes(content));

        var shown = sequence[0].Should().BeOfType<COperator>().Subject.Operands[0]
            .Should().BeOfType<CString>().Subject;
        shown.Value.Should().Be("1\r2\t3\b4\f5\n6", "the parser decodes the escapes");
        Written(sequence).Should().Be(@"(1\r2\t3\b4\f5\n6)Tj" + "\n");
    }

    [Theory]
    [InlineData("<</MCID 0>>")]
    // The type exists for a dictionary the parser has no object for, so what it holds is written
    // exactly as it stands - including nothing at all.
    [InlineData("")]
    public void ADictionaryStringIsWrittenVerbatim(string value)
    {
        var dictionary = new CString { Value = value, CStringType = CStringType.Dictionary };

        dictionary.ToString().Should().Be(value);
        Written(new CSequence { dictionary }).Should().Be(value);
    }

    [Fact]
    public void AStringOfATypeTheEnumDoesNotDefineHasNoWrittenForm()
    {
        var text = new CString { Value = "text", CStringType = (CStringType)99 };

        text.Invoking(t => t.ToString()).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AnInlineDictionaryIsCarriedOnAPseudoOperatorThatWritesAsABlank()
    {
        // The content parser has no dictionary object. It keeps the dictionary's text in a string
        // of type Dictionary and hangs it, with the name before it, on an operator of its own
        // whose written form is a single blank - which is what lets the real operator after it
        // still find its operands in front of it once the whole is written back out.
        var sequence = ContentReader.ReadContent(Encoding.Latin1.GetBytes("/Span <</MCID 0>> BDC (x) Tj EMC"));

        var carrier = sequence[0].Should().BeOfType<COperator>().Subject;
        carrier.OpCode.OpCodeName.Should().Be(OpCodeName.Dictionary);
        carrier.ToString().Should().Be(" ");
        carrier.Operands.Count.Should().Be(2);
        carrier.Operands[0].Should().BeOfType<CName>().Which.Name.Should().Be("/Span");
        var dictionary = carrier.Operands[1].Should().BeOfType<CString>().Subject;
        dictionary.CStringType.Should().Be(CStringType.Dictionary);
        dictionary.Value.Should().Be("<</MCID 0>>");

        var written = Written(sequence);
        written.Should().Be("/Span <</MCID 0>> \nBDC\n(x)Tj\nEMC\n");
        Written(ContentReader.ReadContent(Encoding.Latin1.GetBytes(written))).Should().Be(written);
    }

    [Fact]
    public void ANameIsWrittenWithItsSlashAndEndedWithABlank()
    {
        var name = new CName("/F1");

        name.Name.Should().Be("/F1");
        name.ToString().Should().Be("/F1");
        Written(new CSequence { name, new CInteger { Value = 12 } }).Should().Be("/F1 12 ");
    }

    [Fact]
    public void ANameMadeWithoutOneIsTheBareSlash()
    {
        // The empty name, which PDF allows: a slash followed by nothing.
        new CName().Name.Should().Be("/");
    }

    [Fact]
    public void TheConstructorHoldsANameToTheSameRuleAsTheSetterDoes()
    {
        var withoutSlash = () => new CName("F1");
        var nothing = () => new CName(null);
        var empty = () => new CName("");

        withoutSlash.Should().Throw<ArgumentException>();
        nothing.Should().Throw<ArgumentNullException>();
        empty.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AnArrayIsWrittenInsideItsBrackets()
    {
        var array = new CArray
        {
            new CString { Value = "A" },
            new CInteger { Value = -250 },
            new CString { Value = "B" }
        };
        var show = OpCodes.OperatorFromName("TJ");

        // Cast to CObject, because the CSequence overload of Add would spread the array's items
        // into the operands and lose its brackets.
        show.Operands.Add((CObject)array);

        array.ToString().Should().Be("[(A)-250(B)]");
        Written(new CSequence { show }).Should().Be("[(A)-250(B)]TJ\n");
    }

    [Theory]
    // A dash pattern: two numbers side by side, which used to be written as the one number 32.
    [InlineData("[3 2] 0 d", "[3 2]0 d\n")]
    [InlineData("[0.5 1.5] 0 d", "[0.5 1.5]0 d\n")]
    [InlineData("[] 0 d", "[]0 d\n")]
    // A name ends in a regular character too, so it needs a blank either side of a number.
    [InlineData("[/A 1 /B 2.5] TJ", "[/A 1 /B 2.5]TJ\n")]
    // A string or an array delimits itself, and nothing is put beside one.
    [InlineData("[(A) -250 (B) 3 4 (C)] TJ", "[(A)-250(B)3 4(C)]TJ\n")]
    public void TheItemsOfAnArrayAreSeparatedWhereTheyWouldOtherwiseRunTogether(string content, string expected)
    {
        var written = Written(ContentReader.ReadContent(Encoding.Latin1.GetBytes(content)));

        written.Should().Be(expected);
        Written(ContentReader.ReadContent(Encoding.Latin1.GetBytes(written)))
            .Should().Be(written, "what is written reads back as the same items");
    }

    [Fact]
    public void AnArrayBuiltByHandKeepsItsNumbersApart()
    {
        var array = new CArray
        {
            new CInteger { Value = 3 },
            new CReal { Value = 2.5 },
            new CName("/F1"),
            new CInteger { Value = -1 }
        };

        array.ToString().Should().Be("[3 2.5 /F1 -1]");

        var reread = ContentReader.ReadContent(Encoding.Latin1.GetBytes(array + " TJ"));
        var items = reread[0].Should().BeOfType<COperator>().Subject.Operands[0]
            .Should().BeOfType<CArray>().Subject;
        items.Should().HaveCount(4);
        items[0].Should().BeOfType<CInteger>().Which.Value.Should().Be(3);
        items[1].Should().BeOfType<CReal>().Which.Value.Should().Be(2.5);
        items[2].Should().BeOfType<CName>().Which.Name.Should().Be("/F1");
        items[3].Should().BeOfType<CInteger>().Which.Value.Should().Be(-1);
    }

    [Fact]
    public void AnArrayInsideAnArrayDelimitsItselfLikeAString()
    {
        // Built by hand, because the content parser refuses an array within an array - and each
        // inner one cast to CObject, because the CSequence overload of Add would spread it.
        var array = new CArray();
        array.Add((CObject)new CArray { new CInteger { Value = 1 }, new CInteger { Value = 2 } });
        array.Add(new CInteger { Value = 3 });
        array.Add((CObject)new CArray { new CInteger { Value = 4 } });

        array.ToString().Should().Be("[[1 2]3[4]]");
    }

    [Fact]
    public void AnOperatorIsWrittenAfterItsOperandsAndEndsTheLine()
    {
        var move = OpCodes.OperatorFromName("Td");
        move.Operands.Add(new CInteger { Value = 10 });
        move.Operands.Add(new CReal { Value = 20.5 });

        move.Name.Should().Be("Td");
        move.ToString().Should().Be("Td");
        Written(new CSequence { move, OpCodes.OperatorFromName("Q") }).Should().Be("10 20.5 Td\nQ\n");
    }

    [Fact]
    public void ASequenceReadsAsItsItemsRunTogether()
    {
        // ToString is for reading in a debugger, not for writing - there is no separator and no
        // line end, which is what the writer adds.
        var sequence = new CSequence
        {
            new CName("/F1"),
            new CString { Value = "x" },
            OpCodes.OperatorFromName("Q")
        };

        sequence.ToString().Should().Be("/F1(x)Q");
    }

    [Fact]
    public void AnEmptySequenceWritesNoContentAtAll()
    {
        new CSequence().ToContent().Should().BeEmpty();
    }

    private static string Written(CSequence sequence)
    {
        return Encoding.Latin1.GetString(sequence.ToContent());
    }
}
