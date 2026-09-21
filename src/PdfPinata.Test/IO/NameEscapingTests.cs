using System.IO;
using System.Linq;
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
    const string Key = "/PdfPinataTestName";
    const string Follower = "/PdfPinataFollower";

    static string RoundTripped(string name)
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

    static string Saved(string name)
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
}
