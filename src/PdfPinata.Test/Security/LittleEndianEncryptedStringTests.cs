using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   An encrypted string whose plaintext opens FF FE is read as UTF-16LE text only where the
///   lexer would read the same bytes that way unencrypted, and only where writing it back gives
///   the same bytes. Anything else is kept as the bytes it decrypted to, as before the little
///   endian mark was remembered.
/// </summary>
public class LittleEndianEncryptedStringTests
{
    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    [Arguments("/AESV3")]
    public void AnOddLengthStringThatOpensFfFeKeepsItsLengthThroughAnEncryptedSave(string method)
    {
        byte[] plain = [0xFF, 0xFE, (byte)'A', 0x00, (byte)'B'];
        var original = new CryptFilterDocument { Method = method, TitleBytes = plain, LiteralTitle = true }.Build();

        var saved = Resaved(original);

        TitleBytes(saved).Should().Equal(plain, "a fifth byte has no partner, so the string is not read as text");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    [Arguments("/AESV3")]
    public void AnEvenLengthStringThatOpensFfFeIsStillReadAsText(string method)
    {
        byte[] plain = [0xFF, 0xFE, (byte)'A', 0x00, (byte)'B', 0x00];
        var original = new CryptFilterDocument { Method = method, TitleBytes = plain, LiteralTitle = true }.Build();

        using var document = global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(original), CryptFilterDocument.OwnerPassword,
            PdfDocumentOpenMode.Import);

        document.Info.Title.Should().Be("AB");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    [Arguments("/AESV3")]
    public void AnEncryptedHexStringThatOpensFfFeIsReadAsItsBytes(string method)
    {
        byte[] plain = [0xFF, 0xFE, (byte)'A', 0x00, (byte)'B', 0x00];
        var original = new CryptFilterDocument { Method = method, TitleBytes = plain }.Build();

        TitleBytes(original).Should().Equal(plain, "the lexer reads a hex string as its bytes, encrypted or not");
    }

    private static byte[] Resaved(byte[] file)
    {
        using var document = global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(file), CryptFilterDocument.OwnerPassword,
            PdfDocumentOpenMode.Modify);
        document.SecuritySettings.OwnerPassword = CryptFilterDocument.OwnerPassword;
        document.SecuritySettings.UserPassword = "";
        var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>The title as read, one byte per character, so that a raw string shows its bytes.</summary>
    private static byte[] TitleBytes(byte[] file)
    {
        using var document = global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(file), CryptFilterDocument.OwnerPassword,
            PdfDocumentOpenMode.Import);
        var title = (PdfString)document.Info.Elements["/Title"];
        title.Encoding.Should().Be(PdfStringEncoding.RawEncoding);
        return title.Value.Select(c => (byte)c).ToArray();
    }
}
