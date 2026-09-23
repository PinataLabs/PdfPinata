using System;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   ISO 32000-1 7.3.5: a name is ended by any delimiter or white space, so a name containing one
///   has to write it as #xx, and a byte outside ! to ~ should be written that way too. The writer
///   escaped six of the ten delimiters, so a name holding a bracket or a brace was read back as a
///   shorter name followed by an array or by garbage.
/// </summary>
public class NameEscapingTests
{
    private const string Key = "/PdfPinataTestName";
    private const string Follower = "/PdfPinataFollower";

    private static string RoundTripped(string name)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Elements.SetName(Key, name);
        // A second entry after the name, so that a name ended early leaves wreckage somewhere.
        page.Elements.SetInteger(Follower, 42);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        var reread = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages[0];
        reread.Elements.GetInteger(Follower).Should().Be(42);
        return reread.Elements.GetName(Key);
    }

    private static string Saved(string name)
    {
        var document = new PdfDocument();
        document.AddPage().Elements.SetName(Key, name);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return new string(stream.ToArray().Select(b => (char)b).ToArray());
    }

    [Theory]
    [InlineData("/A[B")]
    [InlineData("/A]B")]
    [InlineData("/A{B")]
    [InlineData("/A}B")]
    [InlineData("/A(B)C<D>E/F%G#H")]
    [InlineData("/A B")]
    public void ANameHoldingADelimiterReadsBackWhole(string name)
    {
        RoundTripped(name).Should().Be(name);
    }

    [Fact]
    public void EveryDelimiterIsWrittenAsAHashEscape()
    {
        Saved("/x()<>[]{}/%#").Should().Contain("/x#28#29#3C#3E#5B#5D#7B#7D#2F#25#23");
    }

    [Fact]
    public void ABytePastTildeIsWrittenAsAHashEscapeAndReadsBackAsTheSameByte()
    {
        // One char per byte, never decoded: 0xE9 is written as #E9 and read back as 0xE9.
        var name = "/caf" + (char)0xE9 + (char)0x7F;

        Saved(name).Should().Contain("/caf#E9#7F");
        RoundTripped(name).Should().Be(name);
    }

    [Fact]
    public void ANameACallerWroteInUnicodeIsWrittenAsItsUtf8Bytes()
    {
        // U+4E2D U+6587. The writer used to escape each char by its whole value, "#4E2D#6587",
        // which reads back as the byte 0x4E, the characters "2D", the byte 0x65 and "87".
        var name = "/Zh\u4E2D\u6587";

        Saved(name).Should().Contain("/Zh#E4#B8#AD#E6#96#87");
    }

    [Fact]
    public void ANameACallerWroteInUnicodeReadsBackAsItsUtf8BytesOneCharPerByte()
    {
        // A name that is read is never decoded, so what comes back is the UTF-8 bytes of what was
        // written, one char each - the same shape as every other name read from a file.
        var name = "/Zh\u4E2D\u6587";
        var utf8 = Encoding.UTF8.GetBytes(name.Substring(1));

        var reread = RoundTripped(name);

        reread.Should().Be("/" + new string(utf8.Select(b => (char)b).ToArray()));
        reread.Should().Be("/Zh" + (char)0xE4 + (char)0xB8 + (char)0xAD + (char)0xE6 + (char)0x96 + (char)0x87);
    }

    [Fact]
    public void ACharBelow256InANameThatAlsoHoldsUnicodeIsEncodedAsUtf8Too()
    {
        // The whole name is one encoding or the other: once a char past 0xFF says the caller wrote
        // Unicode, an e-acute beside it is U+00E9 and becomes C3 A9, not the byte E9.
        var name = "/caf" + (char)0xE9 + "\u4E2D";

        Saved(name).Should().Contain("/caf#C3#A9#E4#B8#AD");
    }

    [Fact]
    public void ADelimiterInANameWrittenInUnicodeIsStillEscaped()
    {
        var name = "/a b(\u4E2D)";

        Saved(name).Should().Contain("/a#20b#28#E4#B8#AD#29");
        RoundTripped(name).Should().Be("/a b(" + (char)0xE4 + (char)0xB8 + (char)0xAD + ")");
    }

    [Fact]
    public void ANameHoldingAnUnpairedSurrogateIsRefusedRatherThanWrittenAsAReplacementCharacter()
    {
        // The UTF-8 encoder used to put U+FFFD in the surrogate's place, so the name written was
        // not the name given, and two names differing only there were written as one.
        (string Name, int Index)[] cases =
        [
            ("/Lone\uD800", 5),
            ("/Lone\uDC00", 5),
            ("/Lone\uD800x", 5),
            ("/Swap\uDC00\uD800", 5),
            ("/Pair\uD83D\uDE00\uD83D", 7),
        ];

        foreach (var (name, index) in cases)
        {
            var save = () => Saved(name);

            save.Should().Throw<ArgumentException>().WithMessage($"*unpaired surrogate*index {index}*");
        }
    }

    [Fact]
    public void ANameHoldingASurrogatePairIsWrittenAsTheUtf8OfTheCharacterItMakes()
    {
        // U+1F600 is F0 9F 98 80: a pair is one character, not two lone halves.
        Saved("/Face\uD83D\uDE00").Should().Contain("/Face#F0#9F#98#80");
    }
}
