using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.IO;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The entries a page inherits from the page tree, read as the catalog's pages are first
///   flattened. /Rotate was cast to a direct integer and /Resources to a dictionary, so an
///   indirect or real /Rotate, or a /Resources that is not a dictionary, threw while the document
///   was being opened and left nothing of it usable.
///   See https://github.com/PinataLabs/PdfPinata/issues/332.
/// </summary>
public class InheritedPageEntryTests
{
    [Test]
    public void AnIndirectRotateInheritedFromThePageTreeIsRead()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2/Rotate 5 0 R/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R>>",
            "<</Type/Page/Parent 2 0 R>>",
            "90");

        document.PageCount.Should().Be(2);
        document.Pages[0].Rotate.Should().Be(90);
        document.Pages[1].Rotate.Should().Be(90);
    }

    [Test]
    public void ARealRotateInheritedFromThePageTreeIsRead()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/Rotate 90.0/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R>>");

        document.Pages[0].Rotate.Should().Be(90);
    }

    [Test]
    public void ARealRotateOnThePageItselfIsRead()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R/Rotate 90.0>>");

        var page = document.Pages[0];

        page.Rotate.Should().Be(90);
        page.Orientation.Should().Be(PdfPinata.PageOrientation.Landscape);
    }

    [Test]
    public void AnIndirectRealRotateIsRead()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/Rotate 4 0 R/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R>>",
            "270.0");

        document.Pages[0].Rotate.Should().Be(270);
    }

    [Test]
    public void ARotateOnThePageWinsOverTheInheritedOne()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/Rotate 4 0 R/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R/Rotate 180>>",
            "90");

        document.Pages[0].Rotate.Should().Be(180);
    }

    [Test]
    [Arguments("42")]
    [Arguments("[1 2 3]")]
    [Arguments("/Name")]
    [Arguments("5 0 R")]
    public void ResourcesOnAPageTreeNodeThatAreNotADictionaryAreSkipped(string resources)
    {
        // The root states the font; the intermediate node's /Resources is not a dictionary, so the
        // page takes its resources from the root, as it would if the node said nothing.
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 200 100]/Resources<</Font<</F1 6 0 R>>>>>>",
            "<</Type/Pages/Parent 2 0 R/Kids[4 0 R]/Count 1/Resources " + resources + ">>",
            "<</Type/Page/Parent 3 0 R>>",
            "(not a dictionary)",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        document.PageCount.Should().Be(1);
        var pageResources = document.Pages[0].Elements.GetDictionary("/Resources");
        pageResources.Should().NotBeNull();
        pageResources!.Elements.GetDictionary("/Font")!.Elements.ContainsKey("/F1").Should().BeTrue();
    }

    [Test]
    public void ResourcesOnThePageThatAreNotADictionaryGiveWayToTheInheritedOnes()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 200 100]/Resources<</Font<</F1 4 0 R>>>>>>",
            "<</Type/Page/Parent 2 0 R/Resources 42>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        var pageResources = document.Pages[0].Elements.GetDictionary("/Resources");
        pageResources.Should().NotBeNull();
        pageResources!.Elements.GetDictionary("/Font")!.Elements.ContainsKey("/F1").Should().BeTrue();
    }

    [Test]
    public void IndirectResourcesOnThePageAreKept()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/MediaBox[0 0 200 100]/Resources<</Font<</F1 4 0 R>>>>>>",
            "<</Type/Page/Parent 2 0 R/Resources 5 0 R>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            "<</ProcSet[/PDF]>>");

        var pageResources = document.Pages[0].Elements.GetDictionary("/Resources")!;
        pageResources.Elements.ContainsKey("/ProcSet").Should().BeTrue();
    }

    [Test]
    public void AWellFormedInheritedRotateAndResourcesAreReadAsBefore()
    {
        var document = Open(
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/Rotate 270/MediaBox[0 0 200 100]/Resources<</Font<</F1 4 0 R>>>>>>",
            "<</Type/Page/Parent 2 0 R/Resources<</ProcSet[/PDF]>>>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        var page = document.Pages[0];
        page.Rotate.Should().Be(270);
        var pageResources = page.Elements.GetDictionary("/Resources")!;
        pageResources.Elements.ContainsKey("/ProcSet").Should().BeTrue();
        pageResources.Elements.ContainsKey("/Font").Should().BeTrue();
    }

    [Test]
    public void AnImportOfAPageWithAnIndirectInheritedRotateKeepsTheTurn()
    {
        var source = Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1/Rotate 4 0 R/MediaBox[0 0 200 100]>>",
            "<</Type/Page/Parent 2 0 R>>",
            "90"
        })), PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        var imported = target.AddPage(source.Pages[0]);

        imported.Rotate.Should().Be(90);
    }

    private static PdfDocument Open(params string[] objects) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(RawPdf.Build(objects)), PdfDocumentOpenMode.Modify);
}
