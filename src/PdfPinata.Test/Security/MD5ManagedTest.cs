using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   The standard security handler needs MD5, which the PDF specification prescribes for the
///   revisions PdfPinata writes. The browser target of .NET does not offer it, so the library
///   carries its own implementation. These tests confirm it really computes MD5.
/// </summary>
public class MD5ManagedTest
{
    // MD5Managed is internal, but it derives from the public HashAlgorithm, so everything the
    // security handler asks of it can be reached through the base class.
    private static HashAlgorithm CreateMD5Managed()
    {
        var type = typeof(PdfDocument).Assembly.GetType("PdfPinata.Pdf.Security.MD5Managed");
        type.Should().NotBeNull("the managed MD5 implementation is what makes encryption work without platform support");
        // ReSharper disable once AssignNullToNotNullAttribute
        return (HashAlgorithm)Activator.CreateInstance(type, true);
    }

    // The test suite from RFC 1321, appendix A.5.
    [Theory]
    [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
    [InlineData("a", "0cc175b9c0f1b6a831c399e269772661")]
    [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData("message digest", "f96b697d7cb7938d525a2f31aaf161d0")]
    [InlineData("abcdefghijklmnopqrstuvwxyz", "c3fcd3d76192e4007dfb496cca67e13b")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", "d174ab98d277d9f5a5611c2c9f419d9f")]
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456" +
                "78901234567890", "57edf4a22be3c955ac49da2e2107b67a")]
    public void ComputesTheDigestsOfRfc1321(string input, string expected)
    {
        using var md5 = CreateMD5Managed();

        var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(input));

        ToHex(hash).Should().Be(expected);
    }

    [Fact]
    public void ComputesTheSameDigestWhenTheInputArrivesInBlocks()
    {
        // The security handler feeds the hash in pieces, so the block boundaries have to be
        // handled correctly, including inputs longer than the 64 byte block size.
        var input = Enumerable.Range(0, 200).Select(i => (byte)i).ToArray();
        using var reference = CreateMD5Managed();
        var expected = ToHex(reference.ComputeHash(input));

        using var md5 = CreateMD5Managed();
        md5.Initialize();
        md5.TransformBlock(input, 0, 70, input, 0);
        md5.TransformBlock(input, 70, 60, input, 70);
        md5.TransformFinalBlock(input, 130, 70);

        ToHex(md5.Hash).Should().Be(expected);
    }

    [Fact]
    public void CanBeReusedAfterInitialize()
    {
        using var md5 = CreateMD5Managed();

        md5.ComputeHash("message digest"u8.ToArray());
        md5.Initialize();

        ToHex(md5.ComputeHash("abc"u8.ToArray())).Should().Be("900150983cd24fb0d6963f7d28e17f72");
    }

    private static string ToHex(byte[] hash)
    {
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}
