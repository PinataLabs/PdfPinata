using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfStringObject"/> is a string standing as an indirect object of its own, which
///   PdfPinata never makes but other producers do, so the parser makes one for every such object it
///   reads. Unlike <see cref="PdfString"/> it is a <see cref="PdfObject"/>, with identity rather
///   than value semantics, and so its value, encoding and hex-literal flag can all be changed in
///   place; the encoding and the flag share one field and each setter keeps the other intact.
///
///   <para>
///   Encryption works on it in place as well. A string object in an encrypted file is decrypted as
///   the file is opened, and it is only then that a byte order mark can be seen and the value taken
///   as UTF-16BE.
///   </para>
/// </summary>
public class PdfStringObjectTests
{
    private const string Japanese = "日本語";

    [Fact]
    public void ANewStringObjectIsEmptyAndRaw()
    {
        var text = new PdfStringObject();

        text.Value.Should().BeEmpty();
        text.Length.Should().Be(0);
        text.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
        text.HexLiteral.Should().BeFalse();
        text.IsIndirect.Should().BeFalse();
    }

    [Fact]
    public void AStringObjectMadeWithAnEncodingSaysSo()
    {
        var text = new PdfStringObject(Japanese, PdfStringEncoding.Unicode);

        text.Value.Should().Be(Japanese);
        text.Length.Should().Be(3);
        text.Encoding.Should().Be(PdfStringEncoding.Unicode);
        text.HexLiteral.Should().BeFalse();
        text.ToString().Should().Be(Japanese);
    }

    [Fact]
    public void AStringObjectMadeForADocumentIsRawUntilToldOtherwise()
    {
        var text = new PdfStringObject(new PdfDocument(), "plain");

        text.Value.Should().Be("plain");
        text.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
    }

    [Fact]
    public void ChangingTheEncodingKeepsTheHexLiteralFlag()
    {
        var text = new PdfStringObject("abc", PdfStringEncoding.RawEncoding)
        {
            HexLiteral = true,
            Encoding = PdfStringEncoding.Unicode
        };

        text.Encoding.Should().Be(PdfStringEncoding.Unicode);
        text.HexLiteral.Should().BeTrue();

        text.Encoding = PdfStringEncoding.PDFDocEncoding;

        text.Encoding.Should().Be(PdfStringEncoding.PDFDocEncoding);
        text.HexLiteral.Should().BeTrue();
    }

    [Fact]
    public void ChangingTheHexLiteralFlagKeepsTheEncoding()
    {
        var text = new PdfStringObject("abc", PdfStringEncoding.WinAnsiEncoding) { HexLiteral = true };

        text.HexLiteral.Should().BeTrue();
        text.Encoding.Should().Be(PdfStringEncoding.WinAnsiEncoding);

        text.HexLiteral = false;

        text.HexLiteral.Should().BeFalse();
        text.Encoding.Should().Be(PdfStringEncoding.WinAnsiEncoding);
    }

    [Fact]
    public void TheValueChangesInPlace()
    {
        // A PdfObject, not a simple type: the same instance takes the new value.
        var text = new PdfStringObject("before", PdfStringEncoding.RawEncoding);
        var same = text;

        text.Value = "after";

        same.Value.Should().Be("after");
        same.Length.Should().Be(5);
        same.ToString().Should().Be("after");
    }

    [Fact]
    public void AssigningNullLeavesAnEmptyValue()
    {
        var text = new PdfStringObject("something", PdfStringEncoding.RawEncoding) { Value = null };

        text.Value.Should().BeEmpty();
        text.Length.Should().Be(0);
        text.ToString().Should().BeEmpty();
    }

    [Fact]
    public void AnIndirectStringIsWrittenAsAnObjectOfItsOwnAndReadBackAsOne()
    {
        var (saved, number) = SaveWithIndirectString(new PdfStringObject("indirect text", PdfStringEncoding.RawEncoding));

        Encoding.Latin1.GetString(saved).Should().Contain(number + " 0 obj\n(indirect text)\nendobj");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
        ReadBack(reread).Value.Should().Be("indirect text");
        reread.Internals.Catalog.Elements.GetString("/TestText").Should().Be("indirect text");
    }

    [Fact]
    public void AHexLiteralStringObjectIsWrittenInHexadecimal()
    {
        var text = new PdfStringObject("Hi!", PdfStringEncoding.RawEncoding) { HexLiteral = true };
        var (saved, number) = SaveWithIndirectString(text);

        Encoding.Latin1.GetString(saved).Should().Contain(number + " 0 obj\n<486921>\nendobj");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
        ReadBack(reread).Value.Should().Be("Hi!");
    }

    [Fact]
    public void AUnicodeStringObjectIsWrittenAsUtf16AndReadBackAsTheSameText()
    {
        var (saved, number) = SaveWithIndirectString(new PdfStringObject(Japanese, PdfStringEncoding.Unicode));

        // The byte order mark, then 日 U+65E5, 本 U+672C and 語 U+8A9E as UTF-16BE. Text needing
        // two bytes a character is written in hexadecimal whether or not the flag asks for it.
        Encoding.Latin1.GetString(saved).Should().Contain(number + " 0 obj\n<FEFF65E5672C8A9E>\nendobj");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
        ReadBack(reread).Value.Should().Be(Japanese);
    }

    [Fact]
    public void AUnicodeStringObjectSurvivesBeingSavedASecondTime()
    {
        // The parser used to make every string object raw whatever the lexer had recognised, so
        // the text read back right once and was then written out a byte a character, keeping
        // only the low byte of each.
        var (saved, _) = SaveWithIndirectString(new PdfStringObject(Japanese, PdfStringEncoding.Unicode));

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);
        var number = ReadBack(reread).Reference.ObjectNumber;
        var savedAgain = Save(reread);

        var rereadAgain = Pdf.IO.PdfReader.Open(new MemoryStream(savedAgain), PdfDocumentOpenMode.Modify);
        ReadBack(rereadAgain).Value.Should().Be(Japanese);
        ReadBack(rereadAgain).Encoding.Should().Be(PdfStringEncoding.Unicode);
        Encoding.Latin1.GetString(savedAgain).Should().Contain(number + " 0 obj\n<FEFF65E5672C8A9E>\nendobj");
    }

    [Theory]
    [InlineData("(plain)", "plain", PdfStringEncoding.RawEncoding, false)]
    [InlineData("<706C61696E>", "plain", PdfStringEncoding.RawEncoding, true)]
    [InlineData("(þÿ\u0000A\u0000B)", "AB", PdfStringEncoding.Unicode, false)]
    [InlineData("<FEFF00410042>", "AB", PdfStringEncoding.Unicode, true)]
    public void AStringObjectIsReadWithTheEncodingAndFormTheLexerFoundInIt(
        string written, string value, PdfStringEncoding encoding, bool hexLiteral)
    {
        // As a direct string is: a byte order mark makes it UTF-16, angle brackets a hex literal.
        var saved = RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R/TestText 4 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 100 100]>>",
            written
        });

        var document = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);

        var text = ReadBack(document);
        text.Value.Should().Be(value);
        text.Encoding.Should().Be(encoding);
        text.HexLiteral.Should().Be(hexLiteral);
    }

    [Fact]
    public void AStringObjectInAFileWrittenByHandIsReadAsOne()
    {
        var saved = RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R/TestText 4 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 100 100]>>",
            "(from another producer)"
        });

        var document = Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Modify);

        var text = ReadBack(document);
        text.Value.Should().Be("from another producer");
        text.Reference.ObjectNumber.Should().Be(4);
    }

    [Theory]
    [InlineData("plain ASCII", PdfStringEncoding.RawEncoding, "(plain ASCII)")]
    [InlineData(Japanese, PdfStringEncoding.Unicode, "<FEFF65E5672C8A9E>")]
    public void AStringObjectInAnEncryptedFileIsDecryptedAsTheFileIsOpened(
        string value, PdfStringEncoding encoding, string writtenInTheClear)
    {
        const string password = "owner";
        var document = new PdfDocument();
        _ = document.AddPage();
        var text = new PdfStringObject(value, encoding);
        document.Internals.AddObject(text);
        document.Internals.Catalog.Elements["/TestText"] = text.Reference;
        document.SecuritySettings.OwnerPassword = password;

        var saved = Save(document);
        Encoding.Latin1.GetString(saved).Should().NotContain(writtenInTheClear, "the string is written encrypted");

        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(saved), password, PdfDocumentOpenMode.Modify);

        // The lexer sees only ciphertext, with no byte order mark to find, so the parser makes the
        // string object raw; it is the mark, found once the bytes are decrypted, that says a
        // string is UTF-16BE.
        var decrypted = ReadBack(reread);
        decrypted.Value.Should().Be(value);
        decrypted.Encoding.Should().Be(encoding);
    }

    [Fact]
    public void EncryptingAUnicodeStringObjectTwiceGivesBackTheText()
    {
        // The standard handler's RC4 is its own inverse, which is how the same method both
        // encrypts and decrypts. A Unicode string object holds no byte order mark in its value,
        // so its bytes are the UTF-16BE of the text alone, and they are read back the same way.
        const string password = "owner";
        var document = new PdfDocument();
        _ = document.AddPage();
        document.SecuritySettings.OwnerPassword = password;
        // A fixed identifier fixes the key, so that what the first pass produces is known not to
        // begin with a byte order mark of its own.
        document.Internals.FirstDocumentID = "0123456789ABCDEF";
        var reread = Pdf.IO.PdfReader.Open(new MemoryStream(Save(document)), password, PdfDocumentOpenMode.Modify);
        reread.Internals.FirstDocumentID.Should().Be("0123456789ABCDEF");

        var text = new PdfStringObject(Japanese, PdfStringEncoding.Unicode);
        reread.Internals.AddObject(text);

        reread.SecurityHandler.EncryptDocument();

        text.Value.Should().NotBe(Japanese);
        text.Length.Should().Be(3, "six bytes of ciphertext are three UTF-16 code units");
        text.Encoding.Should().Be(PdfStringEncoding.Unicode);

        reread.SecurityHandler.EncryptDocument();

        text.Value.Should().Be(Japanese);
        text.Encoding.Should().Be(PdfStringEncoding.Unicode);
    }

    private static (byte[] Saved, int ObjectNumber) SaveWithIndirectString(PdfStringObject text)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Internals.AddObject(text);
        document.Internals.Catalog.Elements["/TestText"] = text.Reference;
        return (Save(document), text.Reference.ObjectNumber);
    }

    private static PdfStringObject ReadBack(PdfDocument document)
    {
        var reference = document.Internals.Catalog.Elements["/TestText"];
        return reference.Should().BeOfType<PdfReference>().Which.Value
            .Should().BeOfType<PdfStringObject>().Subject;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }
}
