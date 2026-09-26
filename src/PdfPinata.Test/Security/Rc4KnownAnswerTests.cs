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
///   The library's <c>Rc4</c> is internal, and this repository carries no <c>InternalsVisibleTo</c>,
///   so it is reached by reflection. It used to be two copies, one the reader decrypted with and one
///   the writer encrypted with, and both passed these vectors before they were joined. The same vectors check <see cref="StandardSecurity.Rc4"/>, the
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
    public void TheLibrarysRc4ProducesTheRfcKeystream(string key)
    {
        var rc4 = new LibraryRc4();
        rc4.SetKey(FromHex(key));
        var keystream = new byte[StreamLength];
        rc4.Apply(keystream, 0, keystream.Length);

        ShouldMatch(keystream, Vectors().Single(v => v.Key == key));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(16)]
    [InlineData(1000)]
    public void TheKeystreamCarriesOnFromOneCallToTheNext(int chunk)
    {
        var vector = Vectors()[4]; // 128 bits, the longest key the standard security handler uses
        var rc4 = new LibraryRc4();
        rc4.SetKey(FromHex(vector.Key));
        var keystream = new byte[StreamLength];
        for (var offset = 0; offset < keystream.Length; offset += chunk)
            rc4.Apply(keystream, offset, System.Math.Min(chunk, keystream.Length - offset));

        ShouldMatch(keystream, vector);
    }

    [Fact]
    public void SettingTheKeyStartsTheKeystreamAgain()
    {
        var vector = Vectors()[0];
        var rc4 = new LibraryRc4();
        rc4.SetKey(FromHex("ffffffffffffffff"));
        rc4.Apply(new byte[100], 0, 100);

        rc4.SetKey(FromHex(vector.Key));
        var keystream = new byte[StreamLength];
        rc4.Apply(keystream, 0, keystream.Length);

        ShouldMatch(keystream, vector);
    }

    [Fact]
    public void AKeyCanBeTakenFromTheMiddleOfAnArray()
    {
        var vector = Vectors()[0];
        var key = FromHex(vector.Key);
        var padded = new byte[key.Length + 7];
        key.CopyTo(padded, 3);
        var rc4 = new LibraryRc4();
        rc4.SetKey(padded, 3, key.Length);
        var keystream = new byte[StreamLength];
        rc4.Apply(keystream, 0, keystream.Length);

        ShouldMatch(keystream, vector);
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

    /// <summary>The library's <c>Rc4</c>, which is internal.</summary>
    private sealed class LibraryRc4
    {
        private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

        private static readonly Type Type =
            typeof(PdfDocument).Assembly.GetType("PdfPinata.Pdf.Security.Rc4", true)!;

        private readonly object _rc4 = Activator.CreateInstance(Type, true);

        public void SetKey(byte[] key) => SetKey(key, 0, key.Length);

        public void SetKey(byte[] key, int offset, int length) =>
            Type.GetMethod("SetKey", Public, null, [typeof(byte[]), typeof(int), typeof(int)], null)!
                .Invoke(_rc4, [key, offset, length]);

        /// <summary>Encrypts <paramref name="data"/> in place, as the library does everywhere.</summary>
        public void Apply(byte[] data, int offset, int length) =>
            Type.GetMethod("Apply", Public, null, [typeof(byte[]), typeof(int), typeof(int), typeof(byte[])], null)!
                .Invoke(_rc4, [data, offset, length, data]);
    }
}
