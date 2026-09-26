using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfPinata.Test.Security;

/// <summary>
///   An independent implementation of the standard security handler, enough of it to decrypt the
///   strings of a document encrypted with RC4. Tests that check what PdfPinata writes need a
///   reader that does not share its assumptions: a writer and a reader that make the same mistake
///   agree with each other perfectly, which is how the fault in issue 460 survived a round trip
///   test.
///
///   The key derivation is checked against the /U entry the document itself carries, so a test
///   using this class fails loudly if the derivation is wrong rather than quietly comparing noise.
///   Algorithms 1 to 5 of ISO 32000-1, clause 7.6, which <see cref="StandardSecurityAlgorithmTests"/>
///   checks in turn against documents Ghostscript, qpdf and three online services wrote.
/// </summary>
internal sealed class StandardSecurity
{
    private readonly string _pdf;
    private readonly byte[] _fileKey;

    public StandardSecurity(byte[] document, string userPassword = "")
    {
        _pdf = Encoding.Latin1.GetString(document);
        Entries = EncryptionEntries.Read(document);
        Revision = Entries.Revision;

        _fileKey = FileKey(Pad(userPassword), Entries.Owner, Entries.Permissions, Entries.Id, Revision,
            Entries.KeyLength, Entries.EncryptMetadata);

        var expected = UserEntry(_fileKey, Entries.Id, Revision);
        DerivedKeyMatchesTheDocument = StartsWith(Entries.User, expected, Revision == 2 ? 32 : 16);
    }

    /// <summary>What the encryption dictionary and the trailer say.</summary>
    public EncryptionEntries Entries { get; }

    public int Revision { get; }

    /// <summary>Whether this class agrees with the document about the file encryption key.</summary>
    public bool DerivedKeyMatchesTheDocument { get; }

    /// <summary>
    ///   The entry of the /Info dictionary with the whole of it decrypted, byte order mark and
    ///   all. What a conforming reader has in hand before it decides how to decode it.
    /// </summary>
    public byte[] DecryptedInfoBytes(string key)
    {
        return Rc4(ObjectKey(_fileKey, InfoObjectNumber, 0), StringValue(InfoDictionary, key));
    }

    /// <summary>The entry of the /Info dictionary, decrypted and decoded the way a reader decodes it.</summary>
    public string DecryptInfoString(string key)
    {
        var plain = DecryptedInfoBytes(key);
        if (plain.Length >= 2 && plain[0] == 0xFE && plain[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(plain, 2, plain.Length - 2);
        return Encoding.Latin1.GetString(plain);
    }

    /// <summary>The entry of the /Info dictionary exactly as it stands in the file.</summary>
    public string RawInfoString(string key)
    {
        var hex = Regex.Match(InfoDictionary, Regex.Escape(key) + @"\s*(<[0-9A-Fa-f]*>)");
        return hex.Success ? hex.Groups[1].Value : null;
    }

    /// <summary>
    ///   Rewrites an /Info entry the way PdfPinata wrote it before the fix, with the byte order
    ///   mark spelled out in front of the ciphertext instead of encrypted with it. The result is
    ///   the same length as what it replaces, so the cross-reference offsets still hold.
    /// </summary>
    public byte[] RewriteAsWrittenBeforeTheFix(string key, string text)
    {
        var withoutMark = Encoding.BigEndianUnicode.GetBytes(text);
        var replacement = "<FEFF" + ToHex(Rc4(ObjectKey(_fileKey, InfoObjectNumber, 0), withoutMark)) + ">";
        var original = RawInfoString(key);

        if (original == null)
            throw new InvalidOperationException($"{key} is not a hexadecimal string in this document.");
        if (replacement.Length != original.Length)
            throw new InvalidOperationException(
                $"{key}: replacement is {replacement.Length} characters and the original {original.Length}; " +
                "the offsets in the document would no longer hold.");

        var at = _pdf.IndexOf(original, InfoDictionaryIndex, StringComparison.Ordinal);
        var rewritten = string.Concat(_pdf.AsSpan(0, at), replacement, _pdf.AsSpan(at + original.Length));
        return Encoding.Latin1.GetBytes(rewritten);
    }

    private int InfoObjectNumber => int.Parse(Last(_pdf, @"/Info\s+(\d+)\s+\d+\s+R").Groups[1].Value);
    private string InfoDictionary => ObjectBody(InfoObjectNumber);
    private int InfoDictionaryIndex => ObjectMatch(InfoObjectNumber).Groups[1].Index;

    private Match ObjectMatch(int number) =>
        Regex.Match(_pdf, @"(?<![0-9])" + number + @"\s+0\s+obj(.*?)endobj", RegexOptions.Singleline);

    private string ObjectBody(int number) => ObjectMatch(number).Groups[1].Value;

    /// <summary>A password as Algorithm 2 step (a) pads it: its first 32 bytes, then the padding string.</summary>
    public static byte[] Pad(string password)
    {
        var bytes = Encoding.Latin1.GetBytes(password ?? "");
        var padded = new byte[32];
        var length = Math.Min(bytes.Length, 32);
        Array.Copy(bytes, padded, length);
        Array.Copy(Padding, 0, padded, length, 32 - length);
        return padded;
    }

    /// <summary>
    ///   Algorithm 2: the file encryption key. From revision 3 on the MD5 digest is taken 50 more
    ///   times, each over the first key-length bytes of the one before, as step (h) says.
    /// </summary>
    public static byte[] FileKey(byte[] paddedUserPassword, byte[] owner, int permissions, byte[] id, int revision,
        int keyLength, bool encryptMetadata = true)
    {
        var input = new List<byte>(paddedUserPassword);
        input.AddRange(owner);
        input.AddRange(BitConverter.GetBytes(permissions));
        input.AddRange(id);
        if (revision >= 4 && !encryptMetadata)
            input.AddRange([0xFF, 0xFF, 0xFF, 0xFF]);

        var hash = MD5.HashData(input.ToArray());
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
                hash = MD5.HashData(Take(hash, keyLength));
        }
        return Take(hash, keyLength);
    }

    /// <summary>
    ///   Algorithm 3: the /O entry. An empty owner password is replaced by the user password, as
    ///   step (a) says. The 50 rehashes of step (c) read the first key-length bytes of each digest,
    ///   as Ghostscript and qpdf both do, rather than the whole of it.
    /// </summary>
    public static byte[] OwnerEntry(string ownerPassword, string userPassword, int revision, int keyLength)
    {
        var hash = MD5.HashData(Pad(string.IsNullOrEmpty(ownerPassword) ? userPassword : ownerPassword));
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
                hash = MD5.HashData(Take(hash, keyLength));
        }
        var key = Take(hash, revision == 2 ? 5 : keyLength);

        var result = Rc4(key, Pad(userPassword));
        if (revision >= 3)
            result = NineteenMoreRounds(key, result);
        return result;
    }

    /// <summary>Algorithm 1: the key an individual object is encrypted with, RC4 or AES.</summary>
    public static byte[] ObjectKey(byte[] fileKey, int objectNumber, int generation, bool aes = false)
    {
        var input = new List<byte>(fileKey)
        {
            (byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16),
            (byte)generation, (byte)(generation >> 8)
        };
        if (aes)
            input.AddRange("sAlT"u8.ToArray());
        return Take(MD5.HashData(input.ToArray()), Math.Min(fileKey.Length + 5, 16));
    }

    /// <summary>
    ///   Algorithms 4 and 5: the /U entry. At revision 3 and later only its first 16 bytes are
    ///   defined, and those are what this answers.
    /// </summary>
    public static byte[] UserEntry(byte[] fileKey, byte[] id, int revision)
    {
        if (revision == 2)
            return Rc4(fileKey, Padding);

        var input = new List<byte>(Padding);
        input.AddRange(id);

        return NineteenMoreRounds(fileKey, Rc4(fileKey, MD5.HashData(input.ToArray())));
    }

    /// <summary>Algorithm 3 step (f) and Algorithm 5 step (e): RC4 again with the key XORed with 1 to 19.</summary>
    private static byte[] NineteenMoreRounds(byte[] key, byte[] data)
    {
        for (var i = 1; i <= 19; i++)
        {
            var round = new byte[key.Length];
            for (var j = 0; j < key.Length; j++)
                round[j] = (byte)(key[j] ^ i);
            data = Rc4(round, data);
        }
        return data;
    }

    public static byte[] Rc4(byte[] key, byte[] data)
    {
        var s = new byte[256];
        for (var i = 0; i < 256; i++)
            s[i] = (byte)i;
        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }

        var result = new byte[data.Length];
        for (int n = 0, i = 0, j = 0; n < data.Length; n++)
        {
            i = (i + 1) & 0xFF;
            j = (j + s[i]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
            result[n] = (byte)(data[n] ^ s[(s[i] + s[j]) & 0xFF]);
        }
        return result;
    }

    /// <summary>Reads a string entry, whether it is written as a hexadecimal or a literal string.</summary>
    internal static byte[] StringValue(string dictionary, string key)
    {
        var hex = Regex.Match(dictionary, Regex.Escape(key) + @"\s*<([0-9A-Fa-f\s]*)>", RegexOptions.Singleline);
        if (hex.Success)
            return FromHex(Regex.Replace(hex.Groups[1].Value, @"\s", ""));

        var literal = Regex.Match(dictionary, Regex.Escape(key) + @"\s*\(", RegexOptions.Singleline);
        return literal.Success ? LiteralBytes(dictionary, literal.Index + literal.Length) : null;
    }

    /// <summary>
    ///   The bytes of a literal string, read from just after its opening parenthesis up to the one
    ///   that balances it. Parentheses nested inside it are part of its value.
    /// </summary>
    private static byte[] LiteralBytes(string text, int start)
    {
        var bytes = new List<byte>();
        var depth = 1;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                bytes.Add(EscapedByte(text, ref i));
                continue;
            }
            if (c == '(') depth++;
            if (c == ')' && --depth == 0) break;
            bytes.Add((byte)c);
        }
        return [..bytes];
    }

    /// <summary>
    ///   The byte a backslash escape stands for. <paramref name="i"/> is on the backslash and is
    ///   left on the last character of the escape.
    /// </summary>
    private static byte EscapedByte(string text, ref int i)
    {
        var escaped = text[++i];
        switch (escaped)
        {
            case 'n': return (byte)'\n';
            case 'r': return (byte)'\r';
            case 't': return (byte)'\t';
            case 'b': return 8;
            case 'f': return 12;
            default: return IsOctalDigit(escaped) ? OctalByte(text, ref i) : (byte)escaped;
        }
    }

    /// <summary>An octal escape of up to three digits, the first of which <paramref name="i"/> is on.</summary>
    private static byte OctalByte(string text, ref int i)
    {
        var value = text[i] - '0';
        for (var digit = 0; digit < 2 && i + 1 < text.Length && IsOctalDigit(text[i + 1]); digit++)
            value = value * 8 + (text[++i] - '0');
        return (byte)value;
    }

    private static bool IsOctalDigit(char c) => c is >= '0' and <= '7';

    internal static bool StartsWith(byte[] actual, byte[] expected, int count)
    {
        if (actual == null || actual.Length < count || expected.Length < Math.Min(count, expected.Length))
            return false;
        for (var i = 0; i < count && i < expected.Length; i++)
            if (actual[i] != expected[i])
                return false;
        return true;
    }

    private static byte[] Take(byte[] bytes, int count)
    {
        var result = new byte[count];
        Array.Copy(bytes, result, count);
        return result;
    }

    private static string ToHex(byte[] bytes)
    {
        var text = new StringBuilder();
        foreach (var b in bytes)
            text.AppendFormat("{0:X2}", b);
        return text.ToString();
    }

    internal static byte[] FromHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    internal static Match Last(string text, string pattern)
    {
        Match last = null;
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Singleline))
            last = m;
        return last;
    }

    internal static readonly byte[] Padding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    ];
}

/// <summary>
///   The entries of a document's standard security handler, read out of the file as text: the
///   encryption dictionary, and the first document identifier from the trailer or from the
///   cross-reference stream that stands in for one. Neither of those is ever encrypted, so no key
///   is needed to read them.
/// </summary>
internal sealed class EncryptionEntries
{
    public byte[] Id { get; private init; }
    public byte[] Owner { get; private init; }
    public byte[] User { get; private init; }
    public int Permissions { get; private init; }
    public int Revision { get; private init; }
    public int Version { get; private init; }

    /// <summary>The key length in bytes: /Length over 8, or 5 where the entry is absent.</summary>
    public int KeyLength { get; private init; }

    /// <summary>The /Length entry as written, in bits, or null where the entry is absent.</summary>
    public int? LengthInBits { get; private init; }

    public bool EncryptMetadata { get; private init; }

    public static EncryptionEntries Read(byte[] document)
    {
        var pdf = Encoding.Latin1.GetString(document);
        var number = int.Parse(StandardSecurity.Last(pdf, @"/Encrypt\s+(\d+)\s+\d+\s+R").Groups[1].Value);
        var body = Regex.Match(pdf, @"(?<![0-9])" + number + @"\s+0\s+obj(.*?)endobj", RegexOptions.Singleline)
            .Groups[1].Value;
        // The crypt filters of revision 4 carry a /Length of their own, in bytes. Only the
        // dictionary's own entries are wanted here.
        var own = Regex.Replace(body, @"/CF\s*<<\s*(/\w+\s*<<[^>]*>>\s*)*>>", "");

        var length = Regex.Match(own, @"/Length\s+(\d+)");
        int? bits = length.Success ? int.Parse(length.Groups[1].Value) : null;
        return new EncryptionEntries
        {
            Id = StandardSecurity.FromHex(StandardSecurity.Last(pdf, @"/ID\s*\[\s*<([0-9A-Fa-f]+)>").Groups[1].Value),
            Owner = StandardSecurity.StringValue(own, "/O"),
            User = StandardSecurity.StringValue(own, "/U"),
            Permissions = int.Parse(Regex.Match(own, @"/P\s+(-?\d+)").Groups[1].Value),
            Revision = int.Parse(Regex.Match(own, @"/R\s+(\d+)").Groups[1].Value),
            Version = int.Parse(Regex.Match(own, @"/V\s+(\d+)").Groups[1].Value),
            LengthInBits = bits,
            KeyLength = (bits ?? 40) / 8,
            EncryptMetadata = !Regex.IsMatch(own, @"/EncryptMetadata\s+false")
        };
    }
}
