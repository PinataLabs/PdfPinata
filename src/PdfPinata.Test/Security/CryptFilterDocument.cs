using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PdfPinata.Test.Security;

/// <summary>
///   A one-page document encrypted by the standard security handler at revision 4, written byte by
///   byte with the crypt filters a test asks for — including <c>/Identity</c>, which no writer to
///   hand will produce on request. The keys come from <see cref="StandardSecurity"/>, the
///   independent implementation, so nothing here shares the reader's assumptions.
/// </summary>
/// <remarks>
///   The user password is empty and the owner password <see cref="OwnerPassword"/>. Objects: 1 the
///   catalog, 2 the page tree, 3 the page, 4 the metadata stream, 5 the content stream, 6 the
///   information dictionary, 7 the encryption dictionary.
/// </remarks>
internal sealed class CryptFilterDocument
{
    public const string OwnerPassword = "owner328";
    public const int MetadataObject = 4;

    /// <summary>The crypt filter <c>/StdCF</c> is defined as: <c>/V2</c> (RC4) or <c>/AESV2</c>.</summary>
    public string Method { get; init; } = "/V2";

    /// <summary>The crypt filter streams are encrypted with: <c>/StdCF</c> or <c>/Identity</c>.</summary>
    public string StreamFilter { get; init; } = "/StdCF";

    /// <summary>The crypt filter strings are encrypted with: <c>/StdCF</c> or <c>/Identity</c>.</summary>
    public string StringFilter { get; init; } = "/StdCF";

    public bool EncryptMetadata { get; init; } = true;

    /// <summary>Whether the metadata stream is left in plain whatever <see cref="EncryptMetadata"/> says.</summary>
    public bool PlainMetadata { get; init; }

    /// <summary>The extra entries of the metadata stream's dictionary, such as a <c>/Filter /Crypt</c>.</summary>
    public string MetadataEntries { get; init; } = "";

    public byte[] Metadata { get; init; } = Encoding.ASCII.GetBytes(
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>\n<?xpacket end=\"w\"?>");

    public byte[] Content { get; init; } = Encoding.ASCII.GetBytes("0 0 m 100 100 l S\n");

    public string Title { get; init; } = "Plain or not";

    private static readonly byte[] Id = Enumerable.Range(0, 16).Select(i => (byte)(i * 17)).ToArray();
    private static readonly byte[] InitializationVector = Enumerable.Range(0, 16).Select(i => (byte)(0xA0 + i)).ToArray();
    private const int Permissions = -4;

    public byte[] FileKey => StandardSecurity.FileKey(StandardSecurity.Pad(""), Owner, Permissions, Id, 4, 16,
        EncryptMetadata);

    private static byte[] Owner => StandardSecurity.OwnerEntry(OwnerPassword, "", 4, 16);

    private bool Aes => Method == "/AESV2";

    /// <summary>
    ///   What the reader would answer for <paramref name="data"/> of object <paramref name="number"/>
    ///   if it decrypted bytes that were never encrypted, with AES-128 and the padding check off:
    ///   the last byte is the padding length a PKCS #7 check reads.
    /// </summary>
    public byte LastByteIfDecrypted(int number, byte[] data)
    {
        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = StandardSecurity.ObjectKey(FileKey, number, 0, aes: true);
        var plain = aes.DecryptCbc(data.AsSpan(16), data.AsSpan(0, 16), PaddingMode.None);
        return plain[^1];
    }

    public byte[] Build()
    {
        var metadata = EncryptMetadata && !PlainMetadata ? EncryptStream(MetadataObject, Metadata) : Metadata;
        var content = EncryptStream(5, Content);
        var title = StringFilter == "/Identity"
            ? Encoding.ASCII.GetBytes(Title)
            : Encrypt(6, Encoding.ASCII.GetBytes(Title));

        var user = StandardSecurity.UserEntry(FileKey, Id, 4).Concat(new byte[16]).ToArray();
        var encryptMetadata = EncryptMetadata ? "" : " /EncryptMetadata false";

        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R >>"),
            Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 5 0 R >>"),
            Stream($"/Type /Metadata /Subtype /XML{MetadataEntries}", metadata),
            Stream("", content),
            Ascii($"<< /Title <{Hex(title)}> >>"),
            Ascii($"<< /Filter /Standard /V 4 /R 4 /Length 128 /CF << /StdCF << /CFM {Method} /AuthEvent /DocOpen " +
                  $"/Length 16 >> >> /StmF {StreamFilter} /StrF {StringFilter} /O <{Hex(Owner)}> /U <{Hex(user)}> " +
                  $"/P {Permissions}{encryptMetadata} >>")
        };

        var file = new List<byte>(Ascii("%PDF-1.6\n"));
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(file.Count);
            file.AddRange(Ascii($"{i + 1} 0 obj\n"));
            file.AddRange(objects[i]);
            file.AddRange(Ascii("\nendobj\n"));
        }

        var xref = file.Count;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f\r\n");
        foreach (var offset in offsets)
            table.Append($"{offset:D10} 00000 n\r\n");
        table.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R /Info 6 0 R /Encrypt 7 0 R " +
                     $"/ID [<{Hex(Id)}> <{Hex(Id)}>] >>\nstartxref\n{xref}\n%%EOF\n");
        file.AddRange(Ascii(table.ToString()));
        return [..file];
    }

    private byte[] EncryptStream(int number, byte[] data) =>
        StreamFilter == "/Identity" ? data : Encrypt(number, data);

    private byte[] Encrypt(int number, byte[] data)
    {
        if (!Aes)
            return StandardSecurity.Rc4(StandardSecurity.ObjectKey(FileKey, number, 0), data);

        using var aes = System.Security.Cryptography.Aes.Create();
        aes.Key = StandardSecurity.ObjectKey(FileKey, number, 0, aes: true);
        return [..InitializationVector, ..aes.EncryptCbc(data, InitializationVector, PaddingMode.PKCS7)];
    }

    private static byte[] Stream(string entries, byte[] data)
    {
        var bytes = new List<byte>(Ascii($"<< /Length {data.Length} {entries} >>\nstream\n"));
        bytes.AddRange(data);
        bytes.AddRange(Ascii("\nendstream"));
        return [..bytes];
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
}
