using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   What <see cref="PdfStringEncodingTests"/> and <see cref="UnicodeTextStringTests"/> leave
///   out of <see cref="PdfString"/>: a string with no value at all, an encoding the constructor
///   has no case for, the WinAnsi encoding, and <see cref="PdfString.ToStringFromPdfDocEncoded"/>,
///   which reads each character of the value as a PDFDocEncoding byte and answers the Unicode
///   character that byte stands for (ISO 32000-1, Annex D.2).
/// </summary>
public class PdfStringTests
{
    [Fact]
    public void AStringMadeFromNullIsEmptyAndRaw()
    {
        var text = new PdfString(null);

        text.Value.Should().BeEmpty();
        text.Length.Should().Be(0);
        text.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
        text.HexLiteral.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AnEmptyRawStringIsAcceptedAndStaysEmpty(string value)
    {
        var text = new PdfString(value, PdfStringEncoding.RawEncoding);

        text.Value.Should().BeEmpty();
        text.Length.Should().Be(0);
        text.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
        text.ToString().Should().Be("()");
    }

    [Theory]
    [InlineData(0x0F)]                // inside the encoding mask, but no encoding
    [InlineData(0x86)]                // Unicode with the hex-literal flag, which is not an encoding
    [InlineData(-1)]
    public void AnEncodingTheConstructorHasNoCaseForIsRefused(int encoding)
    {
        FluentActions.Invoking(() => new PdfString("text", (PdfStringEncoding)encoding))
            .Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("encoding");
    }

    [Fact]
    public void AWinAnsiStringKeepsItsValueAndSaysItIsWinAnsi()
    {
        var text = new PdfString("café", PdfStringEncoding.WinAnsiEncoding);

        text.Encoding.Should().Be(PdfStringEncoding.WinAnsiEncoding);
        text.Value.Should().Be("café");
        text.Length.Should().Be(4);
        text.ToString().Should().Be("(café)");
    }

    [Fact]
    public void AWinAnsiStringComesBackFromTheFileWithTheSameCharacters()
    {
        // é is 0xE9 in WinAnsi and in the Latin-1 the reader takes an unmarked string's bytes as.
        var document = new PdfDocument();
        document.AddPage();
        document.Internals.Catalog.Elements["/TestText"] = new PdfString("café", PdfStringEncoding.WinAnsiEncoding);

        using var output = new MemoryStream();
        document.Save(output, false);

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(output.ToArray()), PdfDocumentOpenMode.Import);
        var item = reread.Internals.Catalog.Elements["/TestText"];
        item.Should().BeOfType<PdfString>().Which.Value.Should().Be("café");
    }

    [Fact]
    public void ReadingAsPdfDocEncodingLeavesAsciiAsItIs()
    {
        new PdfString("Chapter 1: (Intro)").ToStringFromPdfDocEncoded().Should().Be("Chapter 1: (Intro)");
    }

    [Theory]
    [InlineData('\x80', '•')] // bullet
    [InlineData('\x84', '—')] // em dash
    [InlineData('\x8D', '“')] // left double quotation mark
    [InlineData('\x92', '™')] // trade mark
    [InlineData('\x93', 'ﬁ')] // fi ligature
    [InlineData('\x96', 'Œ')] // OE ligature
    [InlineData('\x9E', 'ž')] // z caron
    [InlineData('\xA0', '€')] // euro sign
    [InlineData('\xE9', 'é')] // the upper half agrees with Latin-1 from 0xA1
    [InlineData('\xFF', 'ÿ')]
    public void ReadingAsPdfDocEncodingMapsEachByteToTheCharacterItStandsFor(char encoded, char expected)
    {
        var text = new PdfString(new string(encoded, 1), PdfStringEncoding.RawEncoding);

        text.ToStringFromPdfDocEncoded().Should().Be(new string(expected, 1));
    }

    [Fact]
    public void ReadingAsPdfDocEncodingMapsEveryCharacterOfTheString()
    {
        var text = new PdfString("\x93nal \x84 \xA0" + "5", PdfStringEncoding.RawEncoding);

        text.ToStringFromPdfDocEncoded().Should().Be("ﬁnal — €5");
        text.Value.Should().Be("\x93nal \x84 \xA0" + "5", "the string itself is a simple type and does not change");
    }

    [Fact]
    public void ReadingAsPdfDocEncodingRefusesACharacterNoByteCanHold()
    {
        FluentActions.Invoking(() => new PdfString("Ā", PdfStringEncoding.Unicode).ToStringFromPdfDocEncoded())
            .Should().Throw<InvalidOperationException>();
    }
}
