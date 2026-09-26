using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   RC4 against the keystreams RFC 6229 publishes: fourteen keys from 40 to 256 bits, each at
///   eighteen offsets up to 4096 bytes in. Encrypting zeros gives the keystream itself.
/// </summary>
/// <remarks>
///   The library's RC4 is internal, and this repository carries no <c>InternalsVisibleTo</c>, so
///   it is reached by reflection. The same vectors check <see cref="StandardSecurity.Rc4"/>, the
///   independent copy the other security tests judge the library by, so that neither is trusted
///   on the other's word.
/// </remarks>
public class Rc4KnownAnswerTests
{
    private const int StreamLength = 4096 + 16;

    public static TheoryData<string> Keys()
    {
        var keys = new TheoryData<string>();
        foreach (var vector in Vectors())
            keys.Add(vector.Key);
        return keys;
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void TheIndependentCopyProducesTheRfcKeystream(string key)
    {
        var keystream = StandardSecurity.Rc4(FromHex(key), new byte[StreamLength]);

        ShouldMatch(keystream, Vectors().Single(v => v.Key == key));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void TheReadersCopyProducesTheRfcKeystream(string key)
    {
        var keystream = LibraryRc4.Reader(FromHex(key), StreamLength);

        ShouldMatch(keystream, Vectors().Single(v => v.Key == key));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void TheWritersCopyProducesTheRfcKeystream(string key)
    {
        var keystream = LibraryRc4.Writer(FromHex(key), StreamLength);

        ShouldMatch(keystream, Vectors().Single(v => v.Key == key));
    }

    [Fact]
    public void TheVectorFileHoldsEveryKeyAndOffsetOfTheRfc()
    {
        var vectors = Vectors();

        vectors.Should().HaveCount(14);
        vectors.Should().OnlyContain(v => v.Blocks.Count == 18);
    }

    private static void ShouldMatch(byte[] keystream, Vector vector)
    {
        foreach (var (offset, expected) in vector.Blocks)
            keystream.Skip(offset).Take(16).Should().Equal(expected, $"key {vector.Key} at offset {offset}");
    }

    private sealed record Vector(string Key, List<(int Offset, byte[] Bytes)> Blocks);

    private static List<Vector> Vectors()
    {
        var vectors = new List<Vector>();
        var path = PathHelper.GetInstance().GetAssetPath("Security", "rfc6229-test-vectors.txt");
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("key: 0x", StringComparison.Ordinal))
            {
                vectors.Add(new Vector(line.Substring(7).Trim(), []));
            }
            else if (line.StartsWith("DEC", StringComparison.Ordinal))
            {
                // DEC    0 HEX    0:  b2 39 63 05  f0 3d c0 27   cc c3 52 4a  0a 11 18 a8
                var colon = line.IndexOf(':');
                var offset = int.Parse(line.Substring(3, line.IndexOf("HEX", StringComparison.Ordinal) - 3).Trim(),
                    CultureInfo.InvariantCulture);
                var bytes = FromHex(string.Concat(line.Substring(colon + 1).Where(c => !char.IsWhiteSpace(c))));
                vectors[^1].Blocks.Add((offset, bytes));
            }
        }
        return vectors;
    }

    private static byte[] FromHex(string hex) => StandardSecurity.FromHex(hex);

    /// <summary>
    ///   The library's two RC4 implementations: the one the reader decrypts with, in
    ///   <c>RC4Encryptor</c>, and the one the writer encrypts with, in the security handler.
    /// </summary>
    private static class LibraryRc4
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static byte[] Reader(byte[] key, int length)
        {
            var type = typeof(PdfDocument).Assembly.GetType("PdfPinata.Pdf.Security.RC4Encryptor", true);
            var encryptor = Activator.CreateInstance(type!, true);
            return Keystream(encryptor, key, length);
        }

        public static byte[] Writer(byte[] key, int length) => Keystream(new PdfDocument().SecurityHandler, key, length);

        private static byte[] Keystream(object target, byte[] key, int length)
        {
            var type = target.GetType();
            var prepare = FindMethod(type, "PrepareRC4Key", typeof(byte[]), typeof(int), typeof(int));
            var apply = FindMethod(type, "EncryptRC4", typeof(byte[]), typeof(int), typeof(int), typeof(byte[]));

            var data = new byte[length];
            prepare.Invoke(target, [key, 0, key.Length]);
            apply.Invoke(target, [data, 0, data.Length, data]);
            return data;
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameters)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var method = t.GetMethod(name, Instance | BindingFlags.DeclaredOnly, null, parameters, null);
                if (method != null)
                    return method;
            }
            throw new MissingMethodException(type.FullName, name);
        }
    }
}
