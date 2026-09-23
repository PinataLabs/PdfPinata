using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Internal;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   PDFDocEncoding in both directions (ISO 32000-1, Annex D). The decoder used to throw
///   <see cref="NotImplementedException"/>, and the table it now reads had the eight spacing
///   accents at 0x18 to 0x1F as control characters and two undefined codes, 0x7F and 0xAD, as
///   the Latin-1 characters of the same number. The encoder, which goes through WinAnsi, wrote
///   ƒ as the ellipsis, ‰ as the single right guillemet and š as the right single quotation mark.
/// </summary>
/// <remarks>
///   The encoding is internal and this repository carries no InternalsVisibleTo, so it is
///   reached by reflection; what comes back is an ordinary <see cref="Encoding"/>.
/// </remarks>
public class DocEncodingTests
{
    private static readonly Encoding DocEncoding = (Encoding)typeof(PdfDocument).Assembly
        .GetType("PdfPinata.Pdf.Internal.PdfEncoders", throwOnError: true)!
        .GetProperty("DocEncoding", BindingFlags.Public | BindingFlags.Static)!
        .GetValue(null)!;

    /// <summary>
    ///   Every code Annex D defines at or above 0x18, with the character it stands for, written
    ///   out from the standard rather than from the table under test.
    /// </summary>
    private static Dictionary<byte, char> Defined()
    {
        var map = new Dictionary<byte, char>();
        var accents = "˘ˇˆ˙˝˛˚˜"; // breve … tilde
        for (var idx = 0; idx < accents.Length; idx++)
            map[(byte)(0x18 + idx)] = accents[idx];
        for (var code = 0x20; code <= 0x7E; code++)
            map[(byte)code] = (char)code;
        var upper = "•†‡…—–ƒ⁄‹›−‰„“”‘"
                  + "’‚™ﬁﬂŁŒŠŸŽıłœšž";
        for (var idx = 0; idx < upper.Length; idx++)
            map[(byte)(0x80 + idx)] = upper[idx];
        map[0xA0] = '€';
        for (var code = 0xA1; code <= 0xFF; code++)
        {
            if (code != 0xAD)
                map[(byte)code] = (char)code;
        }
        return map;
    }

    [Fact]
    public void EveryDefinedCodeDecodesToTheCharacterAnnexDGivesIt()
    {
        foreach (var (code, expected) in Defined())
            DocEncoding.GetString(new[] { code }).Should().Be(expected.ToString(), $"0x{code:X2} is {expected}");
    }

    [Theory]
    [InlineData(0x18, '˘')] // breve
    [InlineData(0x19, 'ˇ')] // caron
    [InlineData(0x1A, 'ˆ')] // circumflex
    [InlineData(0x1F, '˜')] // tilde
    public void TheCodesBelowTheSpaceThatAreAccentsDecodeAsAccents(byte code, char expected)
    {
        DocEncoding.GetString(new[] { code }).Should().Be(expected.ToString());
    }

    [Theory]
    [InlineData(0x7F)]
    [InlineData(0x9F)]
    [InlineData(0xAD)]
    public void AnUndefinedCodeDecodesAsTheReplacementCharacter(byte code)
    {
        DocEncoding.GetString(new[] { code }).Should().Be("�");
    }

    [Fact]
    public void ACodeBelowTheAccentsDecodesAsTheControlCharacterItIsInLatin1()
    {
        DocEncoding.GetString(new byte[] { 0x09, 0x0A, 0x0D }).Should().Be("\t\n\r");
    }

    [Fact]
    public void DecodingCountsOneCharacterPerByte()
    {
        var bytes = new byte[] { 0x41, 0x80, 0xA0, 0x18 };

        DocEncoding.GetCharCount(bytes).Should().Be(4);
        DocEncoding.GetMaxCharCount(bytes.Length).Should().Be(4);
        DocEncoding.GetString(bytes).Should().Be("A•€˘");
    }

    [Fact]
    public void EveryCharacterWinAnsiAndPdfDocEncodingShareComesBackAsItWent()
    {
        // The encoder goes through WinAnsi, so a character WinAnsi cannot hold is written as the
        // currency sign it writes for any such character; everything else must round-trip.
        foreach (var (code, ch) in Defined())
        {
            if (!AnsiEncoding.IsAnsi1252Char(ch))
                continue;

            var bytes = DocEncoding.GetBytes(ch.ToString());

            bytes.Should().Equal([code], $"{ch} is 0x{code:X2}");
            DocEncoding.GetString(bytes).Should().Be(ch.ToString());
        }
    }

    [Theory]
    [InlineData('ƒ', 0x86)]
    [InlineData('‰', 0x8B)]
    [InlineData('š', 0x9D)]
    public void ACharacterTheEncoderUsedToMisplaceIsWrittenAsItsOwnCode(char ch, byte code)
    {
        DocEncoding.GetBytes(ch.ToString()).Should().Equal(new[] { code });
    }

    [Theory]
    [InlineData('\u007F')] // DEL: 0x7F is undefined
    [InlineData('\u00AD')] // soft hyphen: 0xAD is undefined
    [InlineData('\u0018')] // the controls whose codes are the accents
    [InlineData('\u001F')]
    [InlineData('\u0081')] // the five C1 controls WinAnsi passes through, whose codes are
    [InlineData('\u008D')] // printable characters in PDFDocEncoding
    [InlineData('\u008F')]
    [InlineData('\u0090')]
    [InlineData('\u009D')]
    public void ACharacterPdfDocEncodingCannotHoldIsWrittenAsTheCurrencySign(char ch)
    {
        DocEncoding.GetBytes(ch.ToString()).Should().Equal(new byte[] { 0xA4 });
    }

    [Fact]
    public void NoCharacterComesBackAsADifferentCharacter()
    {
        // Whatever the encoder takes comes back either as itself or as the currency sign it
        // writes for what it cannot hold, never as some third character. The one exception is
        // the no-break space, which is written as a space because its WinAnsi code, 0xA0, is the
        // euro sign here.
        for (var code = 0; code <= 0xFFFF; code++)
        {
            var ch = ((char)code).ToString();
            var expected = code == 0xA0 ? " " : ch;
            DocEncoding.GetString(DocEncoding.GetBytes(ch)).Should().BeOneOf(expected, "\u00A4");
        }
    }

    [Fact]
    public void RepresentativeTextComesBackAsItWent()
    {
        const string text = "Œuvres – “complètes” … ƒ(x) ‰ Šibenik ž ™ € ˆ˜";

        DocEncoding.GetString(DocEncoding.GetBytes(text)).Should().Be(text);
    }

    [Fact]
    public void AStringMadeAsPdfDocEncodingIsWrittenInIt()
    {
        // PdfString.ToString is the one place a string's own value goes through the encoder.
        new PdfString("ƒ‰š", PdfStringEncoding.PDFDocEncoding).ToString().Should().Be("(\u0086\u008B\u009D)");
    }
}
