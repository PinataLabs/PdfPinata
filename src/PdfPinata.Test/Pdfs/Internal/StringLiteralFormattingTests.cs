using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs.Internal;

/// <summary>
///   What <c>PdfEncoders.FormatStringLiteral</c> writes for the bytes of a string, before any
///   encryption: a literal string in parentheses with its escapes, a hex string in angle brackets,
///   or - for UTF-16, whatever was asked for - a hex string with a line break every 24 characters.
/// </summary>
/// <remarks>
///   PdfEncoders is internal and this repository carries no InternalsVisibleTo, so it is reached by
///   reflection, the way <see cref="PdfPinata.Test.Fonts.TextNormalizationTests"/> reaches the
///   normalizer. Every string a document writes goes through it, but nothing else pins which bytes
///   a literal string escapes and which it writes as they are - and a form feed is one it leaves
///   alone on purpose.
/// </remarks>
public class StringLiteralFormattingTests
{
    private static readonly MethodInfo FormatStringLiteralMethod =
        typeof(PdfDocument).Assembly.GetType("PdfPinata.Pdf.Internal.PdfEncoders", throwOnError: true)
            .GetMethod("FormatStringLiteral", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException("PdfEncoders", "FormatStringLiteral");

    [Theory]
    [InlineData(false, "()")]
    [InlineData(true, "<>")]
    public void NoBytesAtAllAreAnEmptyString(bool hex, string expected)
    {
        Format(null, unicode: false, prefix: false, hex).Should().Be(expected);
        Format([], unicode: true, prefix: true, hex).Should().Be(expected);
    }

    [Fact]
    public void ALiteralStringEscapesItsDelimitersAndFourOfTheControlCharacters()
    {
        var text = Format(Bytes("(a)\\\n\r\t\b"), unicode: false, prefix: false, hex: false);

        text.Should().Be("(\\(a\\)\\\\\\n\\r\\t\\b)");
    }

    [Fact]
    public void ALiteralStringWritesEveryOtherByteAsItIs()
    {
        // The form feed above all: escaping it as \f corrupted encrypted text.
        var bytes = Bytes("\f\0\u0001\u001F ~\u007F\u0080ÿ");

        Format(bytes, unicode: false, prefix: false, hex: false)
            .Should().Be("(" + Latin1(bytes) + ")");
    }

    [Fact]
    public void AHexStringWritesTwoUpperCaseDigitsToAByte()
    {
        Format([0x00, 0x0A, 0xAB, 0xFF], unicode: false, prefix: false, hex: true)
            .Should().Be("<000AABFF>");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeIsWrittenInHexWhateverWasAskedFor(bool hex)
    {
        Format([0x00, 0x41, 0x65, 0xE5], unicode: true, prefix: true, hex)
            .Should().Be("<FEFF004165E5>");
    }

    [Fact]
    public void UnicodeBreaksTheLineAfterTheCharacterAtEveryMultipleOf24NotCountingTheByteOrderMark()
    {
        // The character at index 0 breaks nothing, so the first line holds 25 characters and
        // every one after it 24.
        var bytes = Enumerable.Range(0, 50).SelectMany(_ => new byte[] { 0x00, 0x41 }).ToArray();
        var line = string.Concat(Enumerable.Repeat("0041", 24));

        // Counted from the text, the mark does not shift where the lines break.
        Format(bytes, unicode: true, prefix: true, hex: true)
            .Should().Be("<FEFF" + line + "0041\n" + line + "\n0041>");
        Format(bytes, unicode: true, prefix: false, hex: true)
            .Should().Be("<" + line + "0041\n" + line + "\n0041>");
    }

    private static string Format(byte[] bytes, bool unicode, bool prefix, bool hex)
    {
        try
        {
            var result = (byte[])FormatStringLiteralMethod.Invoke(null, [bytes, unicode, prefix, hex, null]);
            return Latin1(result);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static byte[] Bytes(string text) => [.. text.Select(ch => (byte)ch)];

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
