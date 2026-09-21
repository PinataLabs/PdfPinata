using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace PdfPinata.Test.Security;

public class PdfSecurity
{
    private readonly ITestOutputHelper output;

    public PdfSecurity(ITestOutputHelper testOutputHelper)
    {
        output = testOutputHelper;
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit, "hunter1")]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit, "hunter1")]
    public void CreateAndReadPasswordProtectedPdf(PdfDocumentSecurityLevel securityLevel, string password)
    {
        var document = new PdfDocument();
        var pageNewRenderer = document.AddPage();
        var renderer = XGraphics.FromPdfPage(pageNewRenderer);
        renderer.DrawString("Test Test Test", new XFont("Arial", 12), XBrushes.Black, new XPoint(12, 12));
        // validate correct handling of unicode strings (issue #264)
        document.Outlines.Add("The only page", pageNewRenderer);
        document.SecuritySettings.DocumentSecurityLevel = securityLevel;
        document.SecuritySettings.UserPassword = password;

        using var ms = new MemoryStream();
        document.Save(ms);
        ms.Position = 0;

        var loadDocument = Pdf.IO.PdfReader.Open(ms, PdfDocumentOpenMode.Modify,
            delegate(PdfPasswordProviderArgs args) { args.Password = password; });

        loadDocument.PageCount.Should().Be(1);
        loadDocument.Outlines[0].Title.Should().Be("The only page");
        loadDocument.Info.Producer.Should().Contain("PdfPinata");
    }

    [Fact]
    public void SavingAnUnencryptedDocumentDoesNotCreateAHashAlgorithm()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        // Saving reads this property whenever the trailer is a cross-reference stream, whether
        // or not the document is encrypted, so constructing the handler must stay harmless.
        var securityHandler = document.SecurityHandler;

        using var ms = new MemoryStream();
        document.Save(ms, false);

        document.SecuritySettings.DocumentSecurityLevel.Should().Be(PdfDocumentSecurityLevel.None);
        // ReSharper disable PossibleNullReferenceException
        var md5 = securityHandler.GetType()
            .GetField("_md5Instance", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(securityHandler);
        // ReSharper restore PossibleNullReferenceException
        md5.Should().BeNull("nothing is encrypted, so no hash algorithm should have been created");
    }

    [Fact]
    public void ShouldBeAbleToOpenAesEncryptedDocuments()
    {
        // this document has a V value of 4 (see PdfReference 1.7, Chapter 7.6.1, Table 20)
        // and an R value of 4 (see PdfReference 1.7, Chapter 7.6.3.2, Table 21)
        // see also: Adobe Supplement to the ISO 32000, BaseVersion: 1.7, ExtensionLevel: 3
        //           Chapter 3.5.2, Table 3.19
        var file = PathHelper.GetInstance().GetAssetPath("AesEncrypted.pdf");
        var fi = new FileInfo(file);
        var document = Pdf.IO.PdfReader.Open(file, PdfDocumentOpenMode.Import);

        // verify document was actually AES-encrypted
        var cf = document.SecurityHandler.Elements.GetDictionary("/CF");
        var stdCf = cf.Elements.GetDictionary("/StdCF");
        stdCf.Elements.GetString("/CFM").Should().Be("/AESV2");

        IO.PdfReader.AssertIsAValidPdfDocumentWithProperties(document, (int)fi.Length);
    }

    [Fact]
    public void DocumentWithUserPasswordCannotBeOpenedWithoutPassword()
    {
        using var saved = SaveWithUserPassword("supersecret!11", out _);

        // should throw because no password was provided
        var ex = Assert.Throws<PdfReaderException>(() =>
        {
            Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        });
        ex.Message.Should().Contain("A password is required to open the PDF document");
    }

    [Fact]
    public void DocumentWithUserPasswordCanBeOpenedWithThePassword()
    {
        using var saved = SaveWithUserPassword("supersecret!11", out var pageCount);

        var readBackDoc = Pdf.IO.PdfReader.Open(saved, "supersecret!11", PdfDocumentOpenMode.Import);
        IO.PdfReader.AssertIsAValidPdfDocumentWithProperties(readBackDoc, (int)saved.Length);
        readBackDoc.PageCount.Should().Be(pageCount);
    }

    /// <summary>
    /// Imports the pages of an existing encrypted asset into a new document and saves that,
    /// protected by <paramref name="userPassword"/>, to a stream positioned at its start.
    /// </summary>
    static MemoryStream SaveWithUserPassword(string userPassword, out int pageCount)
    {
        var file = PathHelper.GetInstance().GetAssetPath("AesEncrypted.pdf");
        var document = Pdf.IO.PdfReader.Open(file, PdfDocumentOpenMode.Import);
        pageCount = document.PageCount;

        // import pages into a new document
        var encryptedDoc = new PdfDocument();
        foreach (var page in document.Pages)
            _ = encryptedDoc.AddPage(page);

        // save encrypted
        encryptedDoc.SecuritySettings.UserPassword = userPassword;
        var saved = new MemoryStream();
        encryptedDoc.Save(saved, false);
        saved.Position = 0;
        return saved;
    }

    // Same PDF protected by different tools or online-services
    [Theory]
    // https://www.ilovepdf.com/protect-pdf, 128 bit, /V 2 /R 3
    [InlineData(@"protected-ilovepdf.pdf", "test123")]
        
    // https://www.adobe.com/de/acrobat/online/password-protect-pdf.html, 128 bit, /V 4 /R 4
    [InlineData(@"protected-adobe.pdf", "test123")]

    // https://pdfencrypt.net, 256 bit, /V 5 /R 5
    [InlineData(@"protected-pdfencrypt.pdf", "test123")]

    // https://www.sodapdf.com/password-protect-pdf/
    // this is the only tool tested, that encrypts with the latest known algorithm (256 bit, /V 5 /R 6)
    // Note: SodaPdf also produced a pdf that would be considered "invalid" by PdfSharp, because of incorrect stream-lengths
    // (in the Stream-Dictionary, the length was reported as 32, but in fact the length was 16)
    // this needed to be handled as well
    [InlineData(@"protected-sodapdf.pdf", "test123")]
    public void CanReadPdfEncryptedWithSupportedAlgorithms(string fileName, string password)
    {
        var path = PathHelper.GetInstance().GetAssetPath(fileName);

        var doc = Pdf.IO.PdfReader.Open(path, password, PdfDocumentOpenMode.Import);
        doc.Should().NotBeNull();
        doc.PageCount.Should().BeGreaterThan(0);
        output.WriteLine("Creator : {0}", doc.Info.Creator);
        output.WriteLine("Producer: {0}", doc.Info.Producer);
    }

    // 128 bit AES, /V 4 /R 4. The owner password only serves to recover the user password,
    // which the file encryption key is derived from, so both have to decrypt the document.
    // The file was contributed for the test suite in issue #467.
    [Theory]
    [InlineData("jinglebob8")] // user password
    [InlineData("pigsfly2")]   // owner password
    public void CanReadAnEncryptedPdfWithEitherOfItsPasswords(string password)
    {
        var path = PathHelper.GetInstance().GetAssetPath("protected-user-and-owner-password.pdf");

        var doc = Pdf.IO.PdfReader.Open(path, password, PdfDocumentOpenMode.Import);

        doc.PageCount.Should().Be(1);
        // Strings and streams are decrypted by two separate encryptors, and both need the
        // file encryption key. The strings ...
        doc.Info.Producer.Should().Contain("Word");
        // ... and the streams, which only decompress when they were decrypted correctly.
        ContentReader.ReadContent(doc.Pages[0]).Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ReadingAnEncryptedPdfWithTheWrongPasswordIsRejected()
    {
        var path = PathHelper.GetInstance().GetAssetPath("protected-user-and-owner-password.pdf");

        var ex = Assert.Throws<PdfReaderException>(
            () => Pdf.IO.PdfReader.Open(path, "not the password", PdfDocumentOpenMode.Import));

        ex.Message.Should().Contain("password is invalid");
    }
}
