using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Metadata;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfDocumentOptions.MetadataStrategy"/>: what a save does with the XMP packet, and
///   above all what it does with the packet a read document arrived with.
/// </summary>
public class MetadataStrategyTests
{
    [Fact]
    public void ByDefaultANewDocumentIsWrittenWithNoPacket()
    {
        var document = new PdfDocument();
        document.AddPage();

        document.Options.MetadataStrategy.Should().Be(PdfMetadataStrategy.KeepExisting);
        Packets(Save(document)).Should().Be(0);
    }

    /// <summary>
    ///   The behaviour that made the strategy worth having, kept as the default because changing it
    ///   would change every existing document that is opened and saved: the packet is left alone,
    ///   and so says what it said before the title changed.
    /// </summary>
    [Fact]
    public void ByDefaultAReadDocumentKeepsItsPacketAsItWas()
    {
        var reopened = Reopen(WithPacket("Old title"));
        reopened.Info.Title = "New title";

        var bytes = Save(reopened);

        Packets(bytes).Should().Be(1);
        Text(bytes).Should().Contain("Old title").And.NotContain(">New title<");
    }

    [Fact]
    public void AutoGenerateReplacesAReadDocumentsPacketWithOneThatAgreesWithItsInformation()
    {
        var reopened = Reopen(WithPacket("Old title"));
        reopened.Info.Title = "New title";
        reopened.Options.MetadataStrategy = PdfMetadataStrategy.AutoGenerate;

        var bytes = Save(reopened);

        Packets(bytes).Should().Be(1, "the old packet is replaced, not joined");
        Text(bytes).Should().Contain(">New title<").And.NotContain("Old title<");
    }

    [Fact]
    public void AutoGenerateStillCallsTheCustomisationHooks()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Options.MetadataStrategy = PdfMetadataStrategy.AutoGenerate;
        var called = 0;
        document.AddMetadataContributor(_ => called++);

        Save(document);

        called.Should().Be(1);
    }

    [Fact]
    public void NoMetadataRemovesTheReadDocumentsPacket()
    {
        var reopened = Reopen(WithPacket("Old title"));
        reopened.Options.MetadataStrategy = PdfMetadataStrategy.NoMetadata;

        var bytes = Save(reopened);

        Packets(bytes).Should().Be(0);
        Reopen(bytes).Internals.Catalog.Elements.ContainsKey("/Metadata").Should().BeFalse();
    }

    [Fact]
    public void AConformanceClaimWithNoMetadataIsRefusedAtSave()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = "Claimed";
        document.Options.Conformance = PdfAConformance.PdfA2B;
        document.Options.MetadataStrategy = PdfMetadataStrategy.NoMetadata;

        Action act = () => Save(document);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NoMetadata*");
    }

    [Fact]
    public void AnAccessibilityClaimWithNoMetadataIsRefusedAtSave()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = "Claimed";
        document.Options.UAConformance = PdfUAConformance.PdfUA1;
        document.Options.MetadataStrategy = PdfMetadataStrategy.NoMetadata;

        Action act = () => Save(document);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NoMetadata*");
    }

    [Fact]
    public void ClaimingConformanceWithNoMetadataIsRefusedWhereItIsClaimed()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = "Claimed";
        document.Options.MetadataStrategy = PdfMetadataStrategy.NoMetadata;

        Action act = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        act.Should().Throw<InvalidOperationException>().WithMessage("*NoMetadata*");
        document.Options.Conformance.Should().Be(PdfAConformance.None);
    }

    /// <summary>
    ///   A claim writes a fresh packet under the default strategy, as it always has - a read
    ///   document's own packet cannot be trusted to carry the claim or to agree with /Info.
    /// </summary>
    [Fact]
    public void AConformanceClaimWritesAFreshPacketUnderTheDefault()
    {
        var reopened = Reopen(WithPacket("Old title"));
        reopened.Info.Title = "New title";
        reopened.Options.Conformance = PdfAConformance.PdfA2B;

        var bytes = Save(reopened);

        Packets(bytes).Should().Be(1);
        Text(bytes).Should().Contain(">New title<").And.Contain("pdfaid:part");
    }

    [Fact]
    public void WriteXmpMetadataIsTheOlderSpellingOfAutoGenerate()
    {
        var options = new PdfDocument().Options;

        options.WriteXmpMetadata = true;
        options.MetadataStrategy.Should().Be(PdfMetadataStrategy.AutoGenerate);

        options.WriteXmpMetadata = false;
        options.MetadataStrategy.Should().Be(PdfMetadataStrategy.KeepExisting);

        options.MetadataStrategy = PdfMetadataStrategy.NoMetadata;
        options.WriteXmpMetadata.Should().BeFalse();
        options.WriteXmpMetadata = false;
        options.MetadataStrategy.Should().Be(PdfMetadataStrategy.NoMetadata, "false undoes only AutoGenerate");
    }

    [Fact]
    public void AnUndefinedStrategyIsRefused()
    {
        var options = new PdfDocument().Options;

        Action act = () => options.MetadataStrategy = (PdfMetadataStrategy)42;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----- helpers ------------------------------------------------------------------------------------

    static byte[] WithPacket(string title)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = title;
        document.Options.MetadataStrategy = PdfMetadataStrategy.AutoGenerate;
        return Save(document);
    }

    static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    static PdfDocument Reopen(byte[] bytes) =>
        PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);

    static string Text(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    static int Packets(byte[] bytes)
    {
        var text = Text(bytes);
        var count = 0;
        for (var at = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal); at >= 0;
             at = text.IndexOf("<x:xmpmeta", at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }
}
