using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

// This namespace has a PdfReader of its own, so the library's needs naming.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Security;

/// <summary>
///   An incremental update of an encrypted document is encrypted with the keys the document already
///   has (ISO 32000-1 7.5.6): the same encryption dictionary, the same first identifier, the same
///   file key, so that a reader holding the password reads the old revisions and the new one alike.
///   <see cref="PdfDocument.SaveIncremental"/> threw a <see cref="NullReferenceException"/> instead,
///   because the security handler had only ever been prepared for writing by a full save, which
///   makes new keys. Issue #412.
/// </summary>
/// <remarks>
///   The ciphertext is checked by the independent implementations in <see cref="StandardSecurity"/>
///   and <see cref="Aes256Security"/>, not only by reopening: the reader hands back as it stands any
///   AES string it cannot decrypt, so a revision written in plain would read back perfectly.
/// </remarks>
public class IncrementalSaveOfEncryptedDocumentTests
{
    private const string NewTitle = "A title appended to an encrypted document";

    private const int InfoObject = 6;

    [Test]
    [Arguments("/V2", 3, CryptFilterDocument.OwnerPassword)]
    [Arguments("/V2", 3, "")]
    [Arguments("/V2", 4, CryptFilterDocument.OwnerPassword)]
    [Arguments("/V2", 4, "")]
    [Arguments("/AESV2", 4, CryptFilterDocument.OwnerPassword)]
    [Arguments("/AESV2", 4, "")]
    [Arguments("/AESV3", 6, CryptFilterDocument.OwnerPassword)]
    [Arguments("/AESV3", 6, "")]
    public void TheAppendedRevisionReopensWithThePasswordAndShowsTheChange(string method, int revision, string password)
    {
        var fixture = new CryptFilterDocument { Method = method, Revision = revision };

        var updated = AppendChange(fixture.Build(), password, document => document.Info.Title = NewTitle);

        var reread = Reader.Open(new MemoryStream(updated), password, PdfDocumentOpenMode.Import);
        reread.Info.Title.Should().Be(NewTitle);
        reread.PageCount.Should().Be(1);
    }

    [Test]
    [Arguments("/V2", 3)]
    [Arguments("/V2", 4)]
    [Arguments("/AESV2", 4)]
    [Arguments("/AESV3", 6)]
    public void TheAppendedStringIsEncryptedWithTheDocumentsOwnKey(string method, int revision)
    {
        var fixture = new CryptFilterDocument { Method = method, Revision = revision };
        var original = fixture.Build();

        var updated = AppendChange(original, "", document => document.Info.Title = NewTitle);

        var raw = StandardSecurity.StringValue(AppendedObject(updated, original.Length, InfoObject), "/Title");
        Text(Decrypt(fixture, InfoObject, raw)).Should().Be(NewTitle);
    }

    [Test]
    [Arguments("/V2", 3)]
    [Arguments("/V2", 4)]
    [Arguments("/AESV2", 4)]
    [Arguments("/AESV3", 6)]
    public void AnAppendedStreamIsEncryptedWithTheDocumentsOwnKey(string method, int revision)
    {
        var fixture = new CryptFilterDocument { Method = method, Revision = revision };
        var original = fixture.Build();

        var updated = AppendChange(original, "", document =>
        {
            using var gfx = XGraphics.FromPdfPage(document.Pages[0], XGraphicsPdfPageOptions.Append);
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 20, 20);
        });

        var contents = NewStreams(updated, original.Length)
            .Select(stream => Inflated(stream.Dictionary, Decrypt(fixture, stream.Number, stream.Data)))
            .Select(Encoding.ASCII.GetString)
            .ToList();
        contents.Should().Contain(content => content.Contains(" re"),
            "the rectangle is drawn in a content stream of the appended revision, decrypted with that object's key");
    }

    [Test]
    [Arguments("/V2", 4)]
    [Arguments("/AESV3", 6)]
    public void TheAppendedTrailerKeepsTheEncryptionDictionaryAndTheFirstIdentifier(string method, int revision)
    {
        var fixture = new CryptFilterDocument { Method = method, Revision = revision };
        var original = fixture.Build();
        var originalId = Regex.Match(Encoding.Latin1.GetString(original), @"/ID \[<([0-9A-F]+)>").Groups[1].Value;

        var updated = AppendChange(original, "", document => document.Info.Title = NewTitle);

        var appended = Encoding.Latin1.GetString(updated, original.Length, updated.Length - original.Length);
        appended.Should().MatchRegex(@"/Encrypt\s*7 0 R");
        appended.Should().NotContain("7 0 obj", "the encryption dictionary is the original's, and is not written again");
        Regex.Match(appended, @"/ID\s*\[\s*<([0-9A-Fa-f]+)>").Groups[1].Value
            .Should().BeEquivalentTo(originalId, "the file key is derived from the first identifier");
    }

    private static byte[] AppendChange(byte[] original, string password, Action<PdfDocument> change)
    {
        var document = Reader.Open(new MemoryStream(original), password, PdfDocumentOpenMode.Append);
        change(document);
        using var output = new MemoryStream();
        document.SaveIncremental(output);
        return output.ToArray();
    }

    /// <summary>The body of object <paramref name="number"/> as the appended revision defines it.</summary>
    private static string AppendedObject(byte[] updated, int originalLength, int number)
    {
        var appended = Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);
        var match = Regex.Match(appended, @"(?<![0-9])" + number + @"\s+0\s+obj(.*?)endobj", RegexOptions.Singleline);
        match.Success.Should().BeTrue($"object {number} is in the appended revision");
        return match.Groups[1].Value;
    }

    /// <summary>Every stream the appended revision defines, its data as it stands in the file.</summary>
    private static IEnumerable<(int Number, string Dictionary, byte[] Data)> NewStreams(byte[] updated, int originalLength)
    {
        var appended = Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);
        foreach (Match obj in Regex.Matches(appended, @"(?<![0-9])(\d+)\s+0\s+obj((?:(?!endobj).)*?)stream\r?\n", RegexOptions.Singleline))
        {
            var dictionary = obj.Groups[2].Value;
            var length = int.Parse(Regex.Match(dictionary, @"/Length\s+(\d+)").Groups[1].Value);
            var start = originalLength + obj.Index + obj.Length;
            yield return (int.Parse(obj.Groups[1].Value), dictionary, updated[start..(start + length)]);
        }
    }

    /// <summary>
    ///   <paramref name="data"/> of object <paramref name="number"/> decrypted by Algorithm 1 for RC4 and
    ///   AES-128, and with the file key itself for AES-256. An AES padding that does not check out throws.
    /// </summary>
    private static byte[] Decrypt(CryptFilterDocument fixture, int number, byte[] data)
    {
        if (fixture.Method == "/V2")
            return StandardSecurity.Rc4(StandardSecurity.ObjectKey(fixture.FileKey, number, 0), data);

        using var aes = Aes.Create();
        aes.Key = fixture.Method == "/AESV3"
            ? fixture.FileKey
            : StandardSecurity.ObjectKey(fixture.FileKey, number, 0, aes: true);
        return aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16), PaddingMode.PKCS7);
    }

    /// <summary>A text string as a reader decodes it: UTF-16 behind its byte order mark, or else a byte to a character.</summary>
    private static string Text(byte[] plain) =>
        plain is [0xFE, 0xFF, ..]
            ? Encoding.BigEndianUnicode.GetString(plain, 2, plain.Length - 2)
            : Encoding.Latin1.GetString(plain);

    private static byte[] Inflated(string dictionary, byte[] data)
    {
        if (!dictionary.Contains("/FlateDecode"))
            return data;

        using var inflater = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
        using var plain = new MemoryStream();
        inflater.CopyTo(plain);
        return plain.ToArray();
    }
}
