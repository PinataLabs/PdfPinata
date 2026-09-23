using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A number tree maps integer keys to values, and the catalog holds the page labels of a
///   document in one. Asking a document for its /PageLabels used to throw NotImplementedException
///   from KeysMeta, so the entry could be neither read nor written through the typed API.
///   See https://github.com/ststeiger/PdfSharpCore/issues/358.
/// </summary>
public class NumberTreeTests
{
    [Fact]
    public void AskingADocumentForItsPageLabelsNoLongerThrows()
    {
        var document = Open(DocumentWithPageLabels("<</Nums[0 5 0 R 3 6 0 R]>>"));

        var labels = document.Internals.Catalog.Elements.GetValue("/PageLabels");

        labels.Should().BeOfType<PdfNumberTreeNode>();
    }

    [Fact]
    public void TheEntriesOfAFlatTreeAreRead()
    {
        var tree = PageLabelsOf(DocumentWithPageLabels("<</Nums[0 5 0 R 3 6 0 R]>>"));

        tree.Count.Should().Be(2);
        tree.GetKeys().Should().Equal(0, 3);
        tree.GetDictionary(0).Elements.GetName("/S").Should().Be("/r");
        tree.GetDictionary(3).Elements.GetName("/S").Should().Be("/D");
    }

    [Fact]
    public void TheEntriesBelowTheNodesOfATreeAreRead()
    {
        // A root of two leaves, which is the shape a large tree takes.
        var tree = PageLabelsOf(DocumentWithPageLabels(
            "<</Kids[7 0 R 8 0 R]>>",
            "<</Nums[0 5 0 R]/Limits[0 0]>>",
            "<</Nums[3 6 0 R]/Limits[3 3]>>"));

        tree.Count.Should().Be(2);
        tree.GetKeys().Should().Equal(0, 3);
    }

    [Fact]
    public void EntriesOutOfOrderAreReadInOrder()
    {
        // The standard asks for ascending keys. A document that does otherwise is still one
        // a reader shows, so the entries are sorted rather than refused.
        var tree = PageLabelsOf(DocumentWithPageLabels("<</Nums[3 6 0 R 0 5 0 R]>>"));

        tree.GetKeys().Should().Equal(0, 3);
        tree.GetDictionary(0).Elements.GetName("/S").Should().Be("/r");
    }

    [Fact]
    public void ANodeHoldingBothItsEntriesAndNodesBelowItIsReadInFull()
    {
        // The standard says a node holds one or the other. Where a document holds both,
        // taking only one of them would lose entries that are plainly there.
        var tree = PageLabelsOf(DocumentWithPageLabels(
            "<</Nums[0 5 0 R]/Kids[7 0 R]>>",
            "<</Nums[3 6 0 R]/Limits[3 3]>>"));

        tree.GetKeys().Should().Equal(0, 3);
    }

    [Fact]
    public void ATreeThatLeadsBackToItselfIsReadOnceAndStops()
    {
        var tree = PageLabelsOf(DocumentWithPageLabels(
            "<</Kids[7 0 R]>>",
            "<</Nums[0 5 0 R]/Kids[4 0 R]/Limits[0 0]>>"));

        tree.GetKeys().Should().Equal(0);
    }

    [Fact]
    public void AValueIsGivenBackAsTheObjectItRefersTo()
    {
        var tree = PageLabelsOf(DocumentWithPageLabels("<</Nums[0 5 0 R]>>"));

        // Held in the file as an indirect reference; wanted by a caller as the dictionary.
        tree.GetValue(0).Should().BeAssignableTo<PdfDictionary>();
        tree.GetValue(1).Should().BeNull();
    }

    [Fact]
    public void EntriesPutInAreFoundAgainInOrder()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);

        tree.SetValue(3, Label(document, "/D"));
        tree.SetValue(0, Label(document, "/r"));

        tree.Count.Should().Be(2);
        tree.GetKeys().Should().Equal(0, 3);
        tree.Contains(0).Should().BeTrue();
        tree.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void PuttingAValueUnderAKeyThatIsTakenReplacesIt()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);

        tree.SetValue(0, Label(document, "/r"));
        tree.SetValue(0, Label(document, "/D"));

        tree.Count.Should().Be(1);
        tree.GetDictionary(0).Elements.GetName("/S").Should().Be("/D");
    }

    [Fact]
    public void AnEntryCanBeTakenOutAgain()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);
        tree.SetValue(0, Label(document, "/r"));

        tree.Remove(0).Should().BeTrue();
        tree.Remove(0).Should().BeFalse();
        tree.Count.Should().Be(0);
    }

    [Fact]
    public void ASmallTreeIsWrittenAsOneNodeStatingNoLimits()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);

        tree.SetValue(0, Label(document, "/r"));

        // The root of a tree states no limits, whatever the nodes below it do.
        tree.Elements.GetArray("/Nums").Elements.Count.Should().Be(2);
        tree.Elements.ContainsKey("/Kids").Should().BeFalse();
        tree.Elements.ContainsKey("/Limits").Should().BeFalse();
    }

    [Fact]
    public void ATreeTooBigForOneNodeIsWrittenAsNodesBelowTheRoot()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);

        for (var page = 0; page < 200; page++)
            tree.SetValue(page, Label(document, "/D"));

        var kids = tree.Elements.GetArray("/Kids");
        kids.Should().NotBeNull();
        tree.Elements.ContainsKey("/Nums").Should().BeFalse();
        tree.Elements.ContainsKey("/Limits").Should().BeFalse();

        for (var at = 0; at < kids.Elements.Count; at++)
        {
            // Every node below the root is referred to indirectly and states its limits.
            kids.Elements[at].Should().BeOfType<PdfReference>();

            var leaf = kids.Elements.GetDictionary(at);
            var limits = leaf.Elements.GetArray("/Limits");
            limits.Elements.Count.Should().Be(2);
            limits.Elements.GetInteger(0).Should().BeLessThanOrEqualTo(limits.Elements.GetInteger(1));
        }
    }

    [Fact]
    public void ATreeSurvivesBeingSavedAndReadBack()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);
        tree.SetValue(0, Label(document, "/r"));
        tree.SetValue(3, Label(document, "/D"));

        var reopened = PageLabelsOf(SaveAndOpen(document));

        reopened.GetKeys().Should().Equal(0, 3);
        reopened.GetDictionary(0).Elements.GetName("/S").Should().Be("/r");
        reopened.GetDictionary(3).Elements.GetName("/S").Should().Be("/D");
    }

    [Fact]
    public void ATreeOfManyNodesSurvivesBeingSavedAndReadBack()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var tree = NewPageLabels(document);
        for (var page = 0; page < 500; page++)
            tree.SetValue(page * 2, Started(document, page + 1));

        var reopened = PageLabelsOf(SaveAndOpen(document));

        reopened.Count.Should().Be(500);
        reopened.GetKeys().Should().BeInAscendingOrder();
        reopened.GetKeys().First().Should().Be(0);
        reopened.GetKeys().Last().Should().Be(998);
        reopened.GetDictionary(998).Elements.GetInteger("/St").Should().Be(500);
    }

    [Fact]
    public void ReadingATreeLeavesTheDocumentAsItWas()
    {
        var document = Open(DocumentWithPageLabels("<</Nums[3 6 0 R 0 5 0 R]>>"));
        var tree = PageLabelsOf(document);

        tree.GetKeys().Should().Equal(0, 3);

        // Sorted for the caller, but nothing is written back over a document that was only
        // read: the entries stay in the file in the order they were found.
        var nums = tree.Elements.GetArray("/Nums");
        nums.Elements.GetInteger(0).Should().Be(3);
    }

    private static PdfNumberTreeNode NewPageLabels(PdfDocument document)
    {
        return (PdfNumberTreeNode)document.Internals.Catalog.Elements
            .GetValue("/PageLabels", VCF.CreateIndirect);
    }

    private static PdfNumberTreeNode PageLabelsOf(PdfDocument document)
    {
        return (PdfNumberTreeNode)document.Internals.Catalog.Elements.GetValue("/PageLabels");
    }

    private static PdfNumberTreeNode PageLabelsOf(byte[] document)
    {
        return PageLabelsOf(Open(document));
    }

    private static PdfDictionary Label(PdfDocument document, string style)
    {
        var label = new PdfDictionary(document);
        label.Elements.SetName("/S", style);
        return label;
    }

    private static PdfDictionary Started(PdfDocument document, int start)
    {
        var label = Label(document, "/D");
        label.Elements.SetInteger("/St", start);
        return label;
    }

    private static PdfDocument SaveAndOpen(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static PdfDocument Open(byte[] document)
    {
        return Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);
    }

    /// <summary>
    ///   A one page document whose catalog names object 4 as its page labels. Object 4 is the
    ///   first of the objects given; the two page label dictionaries are objects 5 and 6.
    /// </summary>
    private static byte[] DocumentWithPageLabels(params string[] treeObjects)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R/PageLabels 4 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>",
            treeObjects[0],
            "<</S/r>>",
            "<</S/D/St 1>>"
        };
        objects.AddRange(treeObjects.Skip(1));

        return RawPdf.Build(objects);
    }
}
