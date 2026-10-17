using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Test.Helpers;
using SampleBlazorWasm;
using TUnit.Core;

namespace PdfPinata.Test.Samples;

/// <summary>
/// The part of the Blazor WebAssembly sample that does not need a browser: the document its
/// Generate button builds, and what its AES button says about the encrypted document it opens.
/// </summary>
/// <remarks>
/// The sample is a browser app and no test project references it. These two classes are plain C#
/// with no Blazor in them, so they are linked into this project as source and run here against the
/// backend this suite registers. What the browser adds - fetching the fonts, the download, and the
/// runtime that really has no AES - is checked by running the published app.
/// </remarks>
public class BlazorWasmSampleTests
{
    private static string SampleAsset(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "SampleBlazorWasm", name);

    [Test]
    public void TheGeneratedDocumentIsOnePageWithTextATableAnImageAndAChart()
    {
        var bytes = SampleDocument.Build(File.ReadAllBytes(SampleAsset("alpha-badge.png")));

        var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), Pdf.IO.PdfDocumentOpenMode.Import);
        document.PageCount.Should().Be(SampleDocument.PageCount).And.Be(1);

        var text = PdfTextExtractor.ExtractText(document.Pages[0]);
        text.Should().Contain("Built in your browser");     // the heading
        text.Should().Contain("Liberation Sans");           // a table cell
        text.Should().Contain("North");                     // the chart's legend

        var xObjects = document.Pages[0].Resources.Elements.GetDictionary("/XObject");
        xObjects.Should().NotBeNull();
        xObjects!.Elements.Values
            .Select(item => (item as PdfPinata.Pdf.Advanced.PdfReference)?.Value as PdfDictionary)
            .Should().Contain(xObject => xObject != null && xObject.Elements.GetName("/Subtype") == "/Image");
    }

    [Test]
    public void OpeningTheAesDocumentWhereThereIsNoAesShowsTheClearMessage()
    {
        var bytes = File.ReadAllBytes(SampleAsset("aes-256.pdf"));

        string message;
        // The way the browser refuses: an instance, and no transforms from it.
        using (AesRefusal.BeginAtTransform())
            message = EncryptedSample.TryOpen(bytes);

        message.Should().StartWith("AES-256 encryption is not supported on this platform, ");
    }

    [Test]
    public void TheAesDocumentOpensWhereThereIsAes()
    {
        var bytes = File.ReadAllBytes(SampleAsset("aes-256.pdf"));

        EncryptedSample.TryOpen(bytes).Should().Be("Opened the AES-256 document: 1 page.");
    }
}
