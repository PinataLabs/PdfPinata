using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   An RC4 encryption dictionary whose <c>/Length</c> is larger than 128 bits.
/// </summary>
/// <remarks>
///   Up to revision 4 the file key is cut from a 16-byte MD5 digest, which is why ISO 32000-1
///   Table 20 limits <c>/Length</c> to 40 through 128. A larger value made every rehash read past
///   the end of that digest, so the document threw <c>ArgumentException</c> with every password.
///   qpdf 12.4.1 opens the same file as 128-bit, and this library now does too.
///   <para>
///     The fixture is Ghostscript 10.08.0's 128-bit <c>/R 3</c> output, with
///     <c>-sOwnerPassword=owner -sUserPassword=user -dEncryptionR=3 -dKeyLength=128</c>. Its
///     <c>/Length 128</c> was then edited to <c>/Length 256</c>. The text is the same length, so
///     no offset moves. Nothing else in the file changes, so the keys are still the 128-bit ones.
///   </para>
/// </remarks>
public class RC4OversizedKeyLengthTests
{
    private static string Asset =>
        PathHelper.GetInstance().GetAssetPath("rc4-r3-length256-ghostscript.pdf");

    [Theory]
    [InlineData("owner", true)]
    [InlineData("user", false)]
    public void ItOpensAs128BitWithEitherPassword(string password, bool owner)
    {
        using var document = Pdf.IO.PdfReader.Open(Asset, password, PdfDocumentOpenMode.ReadOnly);

        document.SecuritySettings.HasOwnerPermissions.Should().Be(owner);
        document.Info.Title.Should().Be("RC4 R3 test");
    }
}
