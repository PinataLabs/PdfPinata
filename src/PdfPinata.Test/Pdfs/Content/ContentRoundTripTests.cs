using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using Xunit;

namespace PdfPinata.Test.Pdfs.Content;

/// <summary>
/// Content read with <see cref="ContentReader"/> and written back out with
/// <see cref="CSequence.ToContent"/>, which is what flattening a form or reusing an appearance
/// stream does. https://github.com/empira/PDFsharp/issues/12 reports four ways that round trip
/// lost or refused what it was given; each of them is a case below.
/// </summary>
public class ContentRoundTripTests
{
    // An operator with no operands - which q and Q, the ones that save and restore the graphics
    // state, never have. COperator.WriteObject used to write the operator inside the loop over
    // its operands, so an operator that had none was left out of the output altogether and the
    // content it was meant to enclose was drawn in whatever state came before it.
    [Theory]
    [InlineData("q (text) Tj Q ")]
    // The same without the trailing blank: the last token has no delimiter to end it and used
    // to be dropped, which loses the Q.
    [InlineData("q (text) Tj Q")]
    [InlineData("q (text) Tj Q\n")]
    public void OperatorsWithoutOperandsAreWrittenBackOut(string content)
    {
        RoundTripOf(content).Should().Be("q\n(text)Tj\nQ\n");
    }

    [Fact]
    public void ASequenceBuiltByHandIsWrittenAsTheContentItStandsFor()
    {
        var sequence = new CSequence
        {
            OpCodes.OperatorFromName("q"),
            WithOperand(OpCodes.OperatorFromName("Tj"),
                new CString { CStringType = CStringType.String, Value = "text" }),
            OpCodes.OperatorFromName("Q")
        };

        Written(sequence).Should().Be("q\n(text)Tj\nQ\n");
    }

    // A string parsed out of content has to be written back out, and CParser leaves the type of
    // a parsed string at the one type CString.ToString knows how to write.
    [Theory]
    [InlineData("(text) Tj", "(text)Tj\n")]
    [InlineData("<74657874> Tj", "(text)Tj\n")]
    // A hex string with an odd number of digits: the digit left over is the high one, so the
    // last byte is 0x20 rather than 0x02.
    [InlineData("<746578742> Tj", "(text )Tj\n")]
    // The characters a literal string cannot hold as they stand come back escaped.
    [InlineData(@"(a\(b\)c) Tj", "(a\\(b\\)c)Tj\n")]
    [InlineData(@"(a\\b) Tj", "(a\\\\b)Tj\n")]
    [InlineData(@"(a\nb) Tj", "(a\\nb)Tj\n")]
    public void StringsAreWrittenBackOut(string content, string expected)
    {
        RoundTripOf(content).Should().Be(expected);
    }

    [Fact]
    public void AStringKeepsItsBytes()
    {
        // The byte is written as an octal escape and has to come back out as the byte itself,
        // one char per byte, rather than as whatever it means in the encoding of the day.
        var written = ContentReader.ReadContent(Encoding.Latin1.GetBytes(@"(\251 2024) Tj")).ToContent();

        written.Should().Equal(Encoding.Latin1.GetBytes("(© 2024)Tj\n"));
    }

    [Theory]
    // The operands of an operator, in the order they were given.
    [InlineData("1 0 0 1 20 30 cm", "1 0 0 1 20 30 cm\n")]
    [InlineData("BT /F1 12 Tf ET", "BT\n/F1 12 Tf\nET\n")]
    [InlineData("0.5 .25 -3 rg", "0.5 0.25 -3 rg\n")]
    // An array operand, which is how text is shown with the spacing given between its runs.
    [InlineData("[(A) -250 (B)] TJ", "[(A)-250(B)]TJ\n")]
    // A name that ends the content stream sees no delimiter and used to be dropped.
    [InlineData("/Fm0 Do", "/Fm0 Do\n")]
    public void OperandsAreWrittenBackOut(string content, string expected)
    {
        RoundTripOf(content).Should().Be(expected);
    }

    [Fact]
    public void ContentSurvivesBeingReadAndWrittenTwice()
    {
        const string content = "q 1 0 0 1 20 30 cm BT /F1 12 Tf [(A) -250 (B)] TJ ET Q";

        var once = RoundTripOf(content);

        RoundTripOf(once).Should().Be(once);
    }

    private static COperator WithOperand(COperator op, CObject operand)
    {
        op.Operands.Add(operand);
        return op;
    }

    private static string RoundTripOf(string content)
    {
        return Written(ContentReader.ReadContent(Encoding.Latin1.GetBytes(content)));
    }

    private static string Written(CSequence sequence)
    {
        return Encoding.Latin1.GetString(sequence.ToContent());
    }
}
