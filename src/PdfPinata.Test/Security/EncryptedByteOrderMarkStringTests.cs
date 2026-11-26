using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   An encrypted string is decrypted from the bytes the file holds and then read as those bytes
///   would be read unencrypted. The lexer takes a string opening FE FF for UTF-16 text, and it
///   sees the ciphertext, so the ciphertext has to be put back together before it is decrypted,
///   and the plaintext decoded the way the lexer decodes, odd byte and all (#408).
/// </summary>
public class EncryptedByteOrderMarkStringTests
{
    [Test]
    [Arguments("/V2", new byte[] { 0xFE, 0xFF, 0x00 })]
    [Arguments("/V2", new byte[] { 0xFE, 0xFF, 0x00, (byte)'A', 0x00 })]
    [Arguments("/AESV2", new byte[] { 0xFE, 0xFF, 0x00 })]
    [Arguments("/AESV2", new byte[] { 0xFE, 0xFF, 0x00, (byte)'A', 0x00 })]
    [Arguments("/AESV3", new byte[] { 0xFE, 0xFF, 0x00 })]
    [Arguments("/AESV3", new byte[] { 0xFE, 0xFF, 0x00, (byte)'A', 0x00 })]
    public void AStringDecryptingToAByteOrderMarkAndAnOddNumberOfBytesIsReadAsItWouldBeUnencrypted(string method,
        byte[] plain)
    {
        var encrypted = new CryptFilterDocument { Method = method, TitleBytes = plain };

        Title(encrypted).Should().Be(Title(Unencrypted(encrypted)));
    }

    [Test]
    [Arguments("/AESV2", "Plain or not")]
    [Arguments("/AESV2", "þÿ\0P\0l\0a\0i\0n")]
    [Arguments("/AESV3", "Plain or not")]
    [Arguments("/AESV3", "þÿ\0P\0l\0a\0i\0n")]
    public void AnAesStringWhoseInitializationVectorOpensFeFfIsDecrypted(string method, string title)
    {
        var encrypted = new CryptFilterDocument
        {
            Method = method,
            TitleBytes = Encoding.Latin1.GetBytes(title),
            InitializationVector = [0xFE, 0xFF, .. Enumerable.Range(0, 14).Select(i => (byte)(0xA0 + i))]
        };
        TitleInTheFile(encrypted).Should().StartWith("FEFF", "the point of this test is ciphertext that opens so");

        Title(encrypted).Should().Be(Title(Unencrypted(encrypted)));
    }

    /// <summary>
    ///   RC4 ciphertext is as long as its plaintext, so an odd number of bytes opening FE FF is
    ///   ciphertext through and through. The plaintext is chosen to encrypt to that.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public void AnRc4StringOfAnOddNumberOfBytesWhoseCiphertextOpensFeFfIsDecrypted(bool literal)
    {
        var keystream = StandardSecurity.Rc4(StandardSecurity.ObjectKey(new CryptFilterDocument().FileKey, 6, 0), new byte[5]);
        byte[] cipher = [0xFE, 0xFF, (byte)'a', (byte)'b', (byte)'c'];
        var plain = cipher.Zip(keystream, (c, k) => (byte)(c ^ k)).ToArray();
        var encrypted = new CryptFilterDocument { TitleBytes = plain, LiteralTitle = literal };
        TitleCiphertext(encrypted).Should().Equal(cipher, "the point of this test is ciphertext that opens so");

        Title(encrypted).Should().Be(Title(Unencrypted(encrypted)));
    }

    /// <summary>
    ///   A text string opening FE FF, encrypted under a key whose RC4 keystream opens with two zero
    ///   bytes, so the mark is the same in the ciphertext as in the plaintext. The document
    ///   identifier was found by searching for one whose title key does that.
    /// </summary>
    [Test]
    public void AnRc4TextStringWhoseCiphertextOpensFeFfIsDecrypted()
    {
        var id = Enumerable.Range(0, 16).Select(i => (byte)(i * 17)).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(id, 71597);
        var encrypted = new CryptFilterDocument
        {
            Id = id,
            TitleBytes = [0xFE, 0xFF, 0x00, (byte)'R', 0x00, (byte)'C', 0x00, (byte)'4']
        };
        TitleInTheFile(encrypted).Should().StartWith("FEFF", "the point of this test is ciphertext that opens so");

        Title(encrypted).Should().Be("RC4");
    }

    private static CryptFilterDocument Unencrypted(CryptFilterDocument document) => new()
    {
        Method = document.Method,
        TitleBytes = document.TitleBytes,
        LiteralTitle = document.LiteralTitle,
        StringFilter = "/Identity"
    };

    private static string Title(CryptFilterDocument fixture)
    {
        using var document = global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(fixture.Build()),
            CryptFilterDocument.OwnerPassword, PdfDocumentOpenMode.Import);
        return document.Info.Title;
    }

    private static string TitleInTheFile(CryptFilterDocument fixture) =>
        Regex.Match(Encoding.Latin1.GetString(fixture.Build()), @"/Title\s*<(\w*)>").Groups[1].Value;

    private static byte[] TitleCiphertext(CryptFilterDocument fixture) =>
        StandardSecurity.StringValue(Encoding.Latin1.GetString(fixture.Build()), "/Title");
}
