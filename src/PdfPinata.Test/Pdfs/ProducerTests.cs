using System.IO;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Metadata;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <c>/Producer</c> names the version the library was released as, which MinVer takes from the git
///   tag and writes into the assembly's informational version. It used to be a constant inherited
///   from PDFsharp, so every document said <c>PdfPinata 1.50.4000-netstandard</c> whatever package
///   wrote it.
/// </summary>
public class ProducerTests
{
    private static readonly string ExpectedProducer =
        "PdfPinata " + BuiltVersion() + " (https://github.com/PinataLabs/PdfPinata)";

    [Fact]
    public void ANewDocumentNamesTheVersionTheLibraryWasBuiltAs()
    {
        var reopened = Reopen(Save(NewDocument()));

        reopened.Info.Producer.Should().Be(ExpectedProducer);
        reopened.Info.Creator.Should().Be(ExpectedProducer, "a document that names no creator is given the producer");
    }

    [Fact]
    public void TheVersionIsTheOneMinVerComputedWithoutTheCommitHash()
    {
        var version = ProductVersionInfo.InformationalVersion;

        version.Should().Be(BuiltVersion());
        version.Should().NotContain("+").And.NotStartWith("1.50.4000");
    }

    [Fact]
    public void TheXmpPacketNamesTheSameProducer()
    {
        var document = NewDocument();
        document.Options.MetadataStrategy = PdfMetadataStrategy.AutoGenerate;

        Encoding.Latin1.GetString(Save(document))
            .Should().Contain("<pdf:Producer>" + ExpectedProducer + "</pdf:Producer>");
    }

    [Fact]
    public void ADocumentFromAnotherProducerKeepsItsNameBehindOurs()
    {
        var document = NewDocument();
        document.Info.Elements.SetString("/Producer", "Microsoft Word");

        Reopen(Save(document)).Info.Producer
            .Should().Be(ExpectedProducer + " (Original: Microsoft Word)");
    }

    // ----- helpers ------------------------------------------------------------------------------------

    private static string BuiltVersion()
    {
        var version = typeof(PdfDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        var metadata = version.IndexOf('+');
        return metadata < 0 ? version : version[..metadata];
    }

    private static PdfDocument NewDocument()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        return document;
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static PdfDocument Reopen(byte[] bytes) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(bytes), Pdf.IO.PdfDocumentOpenMode.Import);
}
