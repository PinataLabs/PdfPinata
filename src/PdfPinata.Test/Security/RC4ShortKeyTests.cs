using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   RC4 at revision 3 with a key shorter than 128 bits: <c>/V 1</c> or <c>/V 2</c>, <c>/R 3</c>,
///   <c>/Length</c> 40, 56 or 96.
/// </summary>
/// <remarks>
///   None of these documents could be opened at all, whichever password was given. Algorithm 5
///   step (e) encrypts the user value 20 times with the file key XORed with the round number, and
///   the reader built each round's key over the 16-byte MD5 digest rather than over the file key,
///   so a file key shorter than 16 bytes was read past its end. This library only writes 40-bit
///   keys at revision 2 and 128-bit keys at revision 3, so nothing it had written itself went down
///   that path.
///   <para>
///     Found while looking at issue #135, which suspected the owner-password check of the same
///     key lengths. That check turns out to be right. Its 50 rehashes read only the first
///     key-length bytes of each digest, and Ghostscript and qpdf both do the same. qpdf refuses
///     the owner password of a file whose <c>/O</c> was made from whole digests.
///   </para>
///   <para>
///     The three files are Ghostscript 10.08.0's <c>pdfwrite</c>, with
///     <c>-sOwnerPassword=owner -sUserPassword=user -dEncryptionR=3 -dKeyLength=</c><i>n</i>
///     <c>-dPermissions=-3904</c>. qpdf 12.4.1 accepts <c>owner</c> as the owner password of each.
///     Each has the title "RC4 R3 test", an encrypted string, so decrypting it proves the file
///     key is right and not just the password check.
///   </para>
/// </remarks>
public class RC4ShortKeyTests
{
    private const string Title = "RC4 R3 test";

    private static string Asset(int bits) =>
        PathHelper.GetInstance().GetAssetPath($"rc4-r3-{bits}bit-ghostscript.pdf");

    [Theory]
    [InlineData(40)]
    [InlineData(56)]
    [InlineData(96)]
    public void TheOwnerPasswordOpensItWithOwnerPermissions(int bits)
    {
        using var document = Pdf.IO.PdfReader.Open(Asset(bits), "owner", PdfDocumentOpenMode.Modify);

        document.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
        document.Info.Title.Should().Be(Title);
        document.PageCount.Should().Be(1);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(56)]
    [InlineData(96)]
    public void TheUserPasswordOpensItWithoutOwnerPermissions(int bits)
    {
        using var document = Pdf.IO.PdfReader.Open(Asset(bits), "user", PdfDocumentOpenMode.ReadOnly);

        document.SecuritySettings.HasOwnerPermissions.Should().BeFalse();
        document.Info.Title.Should().Be(Title);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(96)]
    public void AWrongPasswordIsRefusedRatherThanThrowingSomethingElse(int bits)
    {
        var open = () => Pdf.IO.PdfReader.Open(Asset(bits), "neither", PdfDocumentOpenMode.ReadOnly);

        open.Should().Throw<PdfReaderException>();
    }
}
