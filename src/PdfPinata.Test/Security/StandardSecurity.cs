using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfPinata.Test.Security;

/// <summary>
///   An independent implementation of the standard security handler, enough of it to decrypt the
///   strings of a document encrypted with RC4 and an empty user password. Tests that check what
///   PdfPinata writes need a reader that does not share its assumptions: a writer and a reader
///   that make the same mistake agree with each other perfectly, which is how the fault in issue
///   460 survived a round trip test.
///
///   The key derivation is checked against the /U entry the document itself carries, so a test
///   using this class fails loudly if the derivation is wrong rather than quietly comparing noise.
///   Algorithms 1, 2, 4 and 5 of ISO 32000-1, clause 7.6.
/// </summary>
internal sealed class StandardSecurity
{
    private readonly string _pdf;
    private readonly byte[] _fileKey;

    public StandardSecurity(byte[] document)
    {
        _pdf = Encoding.Latin1.GetString(document);

        var id = FromHex(Last(_pdf, @"/ID\s*\[\s*<([0-9A-Fa-f]+)>").Groups[1].Value);
        var encrypt = ObjectBody(int.Parse(Last(_pdf, @"/Encrypt\s+(\d+)\s+\d+\s+R").Groups[1].Value));

        Revision = int.Parse(Regex.Match(encrypt, @"/R\s+(\d+)").Groups[1].Value);
        var permissions = int.Parse(Regex.Match(encrypt, @"/P\s+(-?\d+)").Groups[1].Value);
        var keyLength = (encrypt.Contains("/Length")
            ? int.Parse(Regex.Match(encrypt, @"/Length\s+(\d+)").Groups[1].Value)
            : 40) / 8;

        _fileKey = FileKey(StringValue(encrypt, "/O"), permissions, id, Revision, keyLength);

        var expected = UserEntry(_fileKey, id, Revision);
        var actual = StringValue(encrypt, "/U");
        DerivedKeyMatchesTheDocument = StartsWith(actual, expected, Revision == 2 ? 32 : 16);
    }

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

    // Algorithm 2, with the empty user password.
    private static byte[] FileKey(byte[] owner, int permissions, byte[] id, int revision, int keyLength)
    {
        var input = new List<byte>(Padding);
        input.AddRange(owner);
        input.AddRange(BitConverter.GetBytes(permissions));
        input.AddRange(id);

        var hash = MD5.HashData(input.ToArray());
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
                hash = MD5.HashData(Take(hash, keyLength));
        }
        return Take(hash, keyLength);
    }

    // Algorithm 1: the key an individual object is encrypted with.
    private static byte[] ObjectKey(byte[] fileKey, int objectNumber, int generation)
    {
        var input = new List<byte>(fileKey)
        {
            (byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16),
            (byte)generation, (byte)(generation >> 8)
        };
        return Take(MD5.HashData(input.ToArray()), Math.Min(fileKey.Length + 5, 16));
    }

    // Algorithms 4 and 5: the /U entry for an empty user password.
    private static byte[] UserEntry(byte[] fileKey, byte[] id, int revision)
    {
        if (revision == 2)
            return Rc4(fileKey, Padding);

        var input = new List<byte>(Padding);
        input.AddRange(id);

        var result = Rc4(fileKey, MD5.HashData(input.ToArray()));
        for (var i = 1; i <= 19; i++)
        {
            var key = new byte[fileKey.Length];
            for (var j = 0; j < fileKey.Length; j++)
                key[j] = (byte)(fileKey[j] ^ i);
            result = Rc4(key, result);
        }
        return result;
    }

    private static byte[] Rc4(byte[] key, byte[] data)
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
    private static byte[] StringValue(string dictionary, string key)
    {
        var hex = Regex.Match(dictionary, Regex.Escape(key) + @"\s*<([0-9A-Fa-f\s]*)>", RegexOptions.Singleline);
        if (hex.Success)
            return FromHex(Regex.Replace(hex.Groups[1].Value, @"\s", ""));

        var literal = Regex.Match(dictionary, Regex.Escape(key) + @"\s*\(", RegexOptions.Singleline);
        if (!literal.Success)
            return null;

        var bytes = new List<byte>();
        var depth = 1;
        for (var i = literal.Index + literal.Length; i < dictionary.Length; i++)
        {
            var c = dictionary[i];
            if (c == '\\')
            {
                var escaped = dictionary[++i];
                switch (escaped)
                {
                    case 'n': bytes.Add((byte)'\n'); break;
                    case 'r': bytes.Add((byte)'\r'); break;
                    case 't': bytes.Add((byte)'\t'); break;
                    case 'b': bytes.Add(8); break;
                    case 'f': bytes.Add(12); break;
                    default:
                        if (escaped >= '0' && escaped <= '7')
                        {
                            var value = escaped - '0';
                            for (var digit = 0; digit < 2 && i + 1 < dictionary.Length
                                                          && dictionary[i + 1] >= '0' && dictionary[i + 1] <= '7'; digit++)
                                value = value * 8 + (dictionary[++i] - '0');
                            bytes.Add((byte)value);
                        }
                        else
                            bytes.Add((byte)escaped);
                        break;
                }
                continue;
            }
            if (c == '(') depth++;
            if (c == ')' && --depth == 0) break;
            bytes.Add((byte)c);
        }
        return bytes.ToArray();
    }

    private static bool StartsWith(byte[] actual, byte[] expected, int count)
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

    private static byte[] FromHex(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    private static Match Last(string text, string pattern)
    {
        Match last = null;
        foreach (Match m in Regex.Matches(text, pattern, RegexOptions.Singleline))
            last = m;
        return last;
    }

    private static readonly byte[] Padding =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
    };
}
