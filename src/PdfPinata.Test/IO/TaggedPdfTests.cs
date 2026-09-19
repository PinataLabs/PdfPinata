using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;  // PdfArray lives here for the tree assertions
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Structure;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   The library could not say what anything on a page was. A heading and a caption were both a
///   <c>Tj</c> in a content stream, and a reader had nothing to go on but position — so a screen
///   reader got a soup of glyphs in drawing order, and the document could not be made accessible at
///   any price.
///
///   The names were all in the source and nothing stood behind them: <c>/StructTreeRoot</c> and
///   <c>/MarkInfo</c> were key-name constants on <see cref="PdfCatalog"/>, and <c>BDC</c>, <c>BMC</c>
///   and <c>EMC</c> lived only in the content-stream <em>reader's</em> operator table, never emitted.
///
///   This is Stage A: the plumbing and a hand-driven API. Tagging PinataLayout's own output is Stage B
///   and is not here.
/// </summary>
public class TaggedPdfTests
{
    [Fact]
    public void ADocumentThatNeverAsksToBeTaggedIsWrittenExactlyAsBefore()
    {
        var bytes = Save((gfx, _) => gfx.DrawString("Plain", Font, XBrushes.Black, 40, 60));

        var catalog = Catalog(bytes);
        catalog.Elements.ContainsKey("/StructTreeRoot").Should().BeFalse();
        catalog.Elements.ContainsKey("/MarkInfo").Should().BeFalse();

        ContentOf(bytes).Should().NotContain("BDC",
            "not one extra byte for a document that wants none of this");
    }

    [Fact]
    public void TaggedContentIsWrappedInAMarkedContentSequence()
    {
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.H1))
                gfx.DrawString("Invoice", Font, XBrushes.Black, 40, 60);
        });

        var content = ContentOf(bytes);
        content.Should().Contain("/H1 <</MCID 0>> BDC");
        content.Should().Contain("EMC");
    }

    [Fact]
    public void TheCatalogSaysTheDocumentIsTagged()
    {
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("Body", Font, XBrushes.Black, 40, 60);
        });

        using var saved = new MemoryStream(bytes);
        var catalog = Reader.Open(saved, PdfDocumentOpenMode.Modify).Internals.Catalog;

        catalog.Elements.GetDictionary("/MarkInfo")
            .Elements.GetBoolean("/Marked").Should().BeTrue();
        catalog.Elements["/StructTreeRoot"].Should().NotBeNull();
    }

    [Fact]
    public void TheStructureTreeIsReadBackWithTheTypesItWasGiven()
    {
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.H1))
                gfx.DrawString("Heading", Font, XBrushes.Black, 40, 60);
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("Body", Font, XBrushes.Black, 40, 100);
        });

        var kids = TreeRoot(bytes).Elements.GetArray("/K");

        kids.Elements.Count.Should().Be(2);
        TypeOf(kids, 0).Should().Be("/H1");
        TypeOf(kids, 1).Should().Be("/P");
    }

    [Fact]
    public void AScopeOpenedInsideAnotherBecomesItsChild()
    {
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.Section))
            {
                using (gfx.BeginMarkedContent(PdfTag.H2))
                    gfx.DrawString("Nested heading", Font, XBrushes.Black, 40, 60);
            }
        });

        var kids = TreeRoot(bytes).Elements.GetArray("/K");
        kids.Elements.Count.Should().Be(1, "only the section hangs off the root");

        var section = kids.Elements.GetDictionary(0);
        section.Elements.GetName("/S").Should().Be("/Sect");

        // The section's children are its own marked-content identifier and then the heading. Both
        // belong there: the identifier covers anything drawn directly in the section and outside
        // the heading, and a structure element is allowed to mix marks with child elements. So the
        // heading is found by looking for it rather than by assuming it comes first.
        var inner = ChildElementOf(section);
        inner.Elements.GetName("/S").Should().Be("/H2");
        inner.Elements[PdfStructureElement.Keys.P].Should().NotBeNull("the tree is linked both ways");
    }

    [Fact]
    public void AScopeOpenedBeforeAnythingIsDrawnStillNestsInsideThePage()
    {
        // BeginPage writes the opening q and the view matrix, and it runs on the first thing that
        // draws. A scope opened before that would have the q land inside it while the matching Q,
        // written when the page closes, lands after the EMC — the two pairs crossing rather than
        // nesting, which is not allowed.
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("First thing drawn", Font, XBrushes.Black, 40, 60);
        });

        var content = ContentOf(bytes);

        content.IndexOf('q')
            .Should().BeLessThan(content.IndexOf("BDC", StringComparison.Ordinal),
                "the page's own q has to open before the marked-content sequence does");
    }

    [Fact]
    public void AnAnnotationIsFiledUnderAKeyOfItsOwn()
    {
        // A page's key resolves to the array of elements its marks belong to; an annotation's has to
        // resolve straight to its element. Handing the annotation the page's key points a reader at
        // an array where it expects an element, so the annotation's place in the structure cannot be
        // found at all.
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        using (gfx.BeginMarkedContent(PdfTag.P))
            gfx.DrawString("Body", Font, XBrushes.Black, 40, 60);
        gfx.Dispose();

        var link = new PdfDictionary(document);
        link.Elements.SetName("/Subtype", "/Link");
        document.Internals.AddObject(link);

        var annotations = new PdfArray(document);
        annotations.Elements.Add(link.Reference);
        page.Elements["/Annots"] = annotations;

        var element = document.Structure.CreateElement(PdfTag.Link);
        document.Structure.AddAnnotation(element, page, link);

        using var output = new MemoryStream();
        document.Save(output, false);
        var bytes = output.ToArray();

        var saved = Reader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Modify);
        var savedPage = saved.Pages[0];
        var annotation = savedPage.Elements.GetArray("/Annots").Elements.GetDictionary(0);

        var pageKey = savedPage.Elements.GetInteger("/StructParents");
        var annotationKey = annotation.Elements.GetInteger("/StructParent");

        annotationKey.Should().NotBe(pageKey, "the two resolve to different kinds of thing");

        var root = TreeRoot(bytes);
        root.Elements.GetInteger(PdfStructureTreeRoot.Keys.ParentTreeNextKey)
            .Should().BeGreaterThan(Math.Max(pageKey, annotationKey),
                "the next key must not be one already handed out");
    }

    [Fact]
    public void AnArtifactJoinsNoStructureElement()
    {
        // A page number read out between every paragraph is worse than no page number.
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("Body", Font, XBrushes.Black, 40, 60);
            using (gfx.BeginArtifact())
                gfx.DrawString("Page 1 of 1", Font, XBrushes.Gray, 500, 800);
        });

        ContentOf(bytes).Should().Contain("/Artifact BMC");
        TreeRoot(bytes).Elements.GetArray("/K").Elements.Count
            .Should().Be(1, "the folio is on the page and is not part of what the page says");
    }

    [Fact]
    public void EveryPageThatCarriesMarksIsIndexedByTheParentTree()
    {
        // The parent tree is what takes a reader from a mark back to its meaning. Without it the
        // marks are numbers with nothing behind them.
        var bytes = SaveTwoPages();

        var root = TreeRoot(bytes);
        var parentTree = root.Elements.GetDictionary(PdfStructureTreeRoot.Keys.ParentTree);

        parentTree.Should().NotBeNull();
        root.Elements.GetInteger(PdfStructureTreeRoot.Keys.ParentTreeNextKey)
            .Should().Be(2, "two pages carried marks");
    }

    [Fact]
    public void EachPageCountsItsOwnMarkedContentFromZero()
    {
        // The identifier is an index into that page's run of the parent tree, so it restarts on
        // every page rather than counting across the document.
        var bytes = SaveTwoPages();

        Occurrences(ContentOf(bytes, 0), "<</MCID 0>>").Should().Be(1);
        Occurrences(ContentOf(bytes, 1), "<</MCID 0>>").Should().Be(1);
    }

    [Fact]
    public void APageThatCarriesMarksSaysWhereItsRunOfTheParentTreeIs()
    {
        var bytes = SaveTwoPages();

        using var saved = new MemoryStream(bytes);
        var document = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        document.Pages[0].Elements.GetInteger("/StructParents").Should().Be(0);
        document.Pages[1].Elements.GetInteger("/StructParents").Should().Be(1);
    }

    [Fact]
    public void AFigureCarriesTheTextThatStandsInForIt()
    {
        var bytes = Save((gfx, _) =>
        {
            using (gfx.BeginMarkedContent(PdfTag.Figure, "A bar chart of monthly revenue"))
                gfx.DrawRectangle(XBrushes.LightGray, 40, 40, 120, 80);
        });

        var figure = TreeRoot(bytes).Elements.GetArray("/K").Elements.GetDictionary(0);

        figure.Elements.GetString(PdfStructureElement.Keys.Alt)
            .Should().Be("A bar chart of monthly revenue");
    }

    [Fact]
    public void TheDocumentCanSayWhatLanguageItIsIn()
    {
        var bytes = Save((gfx, document) =>
        {
            document.Structure.Language = "en-GB";
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("Body", Font, XBrushes.Black, 40, 60);
        });

        using var saved = new MemoryStream(bytes);
        Reader.Open(saved, PdfDocumentOpenMode.Modify).Internals.Catalog
            .Elements.GetString("/Lang").Should().Be("en-GB");
    }

    [Fact]
    public void ATypeOfTheDocumentsOwnInventionIsExplainedByTheRoleMap()
    {
        // A structure type that is neither standard nor in the role map means nothing to anybody.
        var bytes = Save((gfx, document) =>
        {
            document.Structure.Root.RoleMap["Invoice"] = "Sect";
            using (gfx.BeginMarkedContent(new PdfTag("Invoice")))
                gfx.DrawString("Body", Font, XBrushes.Black, 40, 60);
        });

        var roleMap = TreeRoot(bytes).Elements.GetDictionary(PdfStructureTreeRoot.Keys.RoleMap);

        roleMap.Elements.GetName("/Invoice").Should().Be("/Sect");
    }

    /// <summary>
    ///   Two sequences opened through the tag overload are not nested one inside the other.
    /// </summary>
    /// <remarks>
    ///   The same rule the element overload follows, and it has to be the same rule: a sequence
    ///   carrying an MCID is a content item of exactly one element, so nesting two makes the inner
    ///   glyphs belong to both. This is the overload a caller tagging a page by hand uses, so fixing
    ///   only the one PinataLayout happens to call would leave the documented manual API doing the very
    ///   thing the other was changed to stop doing.
    /// </remarks>
    [Fact]
    public void NestedTagScopesAreNotNestedInTheContentStream()
    {
        var document = new PdfDocument();
        var gfx = XGraphics.FromPdfPage(document.AddPage());

        using (gfx.BeginMarkedContent(PdfTag.P))
        {
            gfx.DrawString("before", Font, XBrushes.Black, 40, 100);

            using (gfx.BeginMarkedContent(PdfTag.Span))
                gfx.DrawString("inside", Font, XBrushes.Black, 100, 100);

            gfx.DrawString("after", Font, XBrushes.Black, 160, 100);
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        var content = ContentOf(output.ToArray());

        DeepestNesting(content).Should().Be(1,
            "the paragraph is suspended around the span rather than wrapped round it");

        // Suspending is only safe because the sequence is resumed: without it "after" would be
        // outside the structure tree altogether.
        Occurrences(content, "BDC").Should().Be(3, "the paragraph either side of the span, and the span");
        Occurrences(content, "EMC").Should().Be(3);
    }

    /// <summary>
    ///   A tag scope that draws nothing after a nested one still writes no empty pair, and hands its
    ///   identifier back rather than leaving the tree naming a mark that is not in the stream.
    /// </summary>
    [Fact]
    public void ATagScopeEndingInANestedOneLeavesNoGapInTheIdentifiers()
    {
        var document = new PdfDocument();
        var gfx = XGraphics.FromPdfPage(document.AddPage());

        using (gfx.BeginMarkedContent(PdfTag.P))
        {
            gfx.DrawString("before", Font, XBrushes.Black, 40, 100);

            using (gfx.BeginMarkedContent(PdfTag.Span))
                gfx.DrawString("inside", Font, XBrushes.Black, 100, 100);
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        var content = ContentOf(output.ToArray());

        var identifiers = Regex.Matches(content, @"/MCID (\d+)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .OrderBy(id => id)
            .ToArray();

        identifiers.Should().Equal(Enumerable.Range(0, identifiers.Length),
            "an identifier handed out for a sequence that was then removed leaves a gap");
    }

    /// <summary>How deep the marked-content nesting in a content stream ever gets.</summary>
    static int DeepestNesting(string content)
    {
        int depth = 0, deepest = 0;
        foreach (Match match in Regex.Matches(content, @"\b(BDC|BMC|EMC)\b"))
        {
            if (match.Value == "EMC")
                depth--;
            else
                deepest = Math.Max(deepest, ++depth);
        }

        depth.Should().Be(0, "every sequence opened has to be closed");
        return deepest;
    }

    [Fact]
    public void AMarkedContentSequenceIsClosedEvenWhenTheDrawingThrows()
    {
        // The scope is a using, so an early return or an exception cannot leave a BDC unbalanced —
        // and an unbalanced one corrupts every mark after it on the page.
        var document = new PdfDocument();
        var gfx = XGraphics.FromPdfPage(document.AddPage());

        try
        {
            using (gfx.BeginMarkedContent(PdfTag.P))
                throw new InvalidOperationException("something went wrong mid-paragraph");
        }
        catch (InvalidOperationException)
        {
            // Expected — the point is what the content stream looks like afterwards.
        }

        gfx.DrawString("After", Font, XBrushes.Black, 40, 100);
        using var output = new MemoryStream();
        document.Save(output, false);

        // Counted against one rather than against each other: a scope that was never opened would
        // satisfy "as many EMC as BDC" with none of either, and that is exactly the failure a
        // release build produces when this reads the file instead of the stream.
        var content = ContentOf(output.ToArray());
        Occurrences(content, "BDC").Should().Be(1);
        Occurrences(content, "EMC").Should().Be(1);
    }

    [Fact]
    public void MarkedContentCannotBeWrittenToSomethingThatIsNotAPdfPage()
    {
        var form = new XForm(new PdfDocument(), XUnit.FromPoint(100), XUnit.FromPoint(100));
        var gfx = XGraphics.FromForm(form);

        var tagging = () => gfx.BeginMarkedContent(PdfTag.P);

        tagging.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ATaggedDocumentSurvivesBeingIndexedByACrossReferenceStream()
    {
        // Tagging multiplies the object count, which is the reason the compressed cross-reference
        // format landed first. The two have to work together or neither is much use.
        var bytes = Save((gfx, document) =>
        {
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
            using (gfx.BeginMarkedContent(PdfTag.H1))
                gfx.DrawString("Heading", Font, XBrushes.Black, 40, 60);
        });

        TreeRoot(bytes).Elements.GetArray("/K").Elements.Count.Should().Be(1);
    }

    private static XFont Font => new("Arial", 12);

    /// <summary>
    ///   The first child of an element that is an element rather than a marked-content identifier.
    /// </summary>
    private static PdfDictionary ChildElementOf(PdfDictionary element)
    {
        var kids = element.Elements.GetArray("/K");
        for (var index = 0; index < kids.Elements.Count; index++)
        {
            var child = kids.Elements.GetDictionary(index);
            if (child != null)
                return child;
        }

        throw new InvalidOperationException("The element has no child element, only marks.");
    }

    private static string TypeOf(PdfArray kids, int index) =>
        kids.Elements.GetDictionary(index).Elements.GetName("/S");

    private static PdfDictionary TreeRoot(byte[] bytes) =>
        Catalog(bytes).Elements.GetDictionary("/StructTreeRoot");

    private static PdfCatalog Catalog(byte[] bytes)
    {
        using var saved = new MemoryStream(bytes);
        return Reader.Open(saved, PdfDocumentOpenMode.Modify).Internals.Catalog;
    }

    /// <summary>
    ///   The operators of a page's content stream, as text.
    /// </summary>
    /// <remarks>
    ///   Read back through the page rather than out of the file's bytes, because a content stream is
    ///   compressed or not depending on <c>CompressContentStreams</c> — which defaults to false in a
    ///   debug build and true in a release one. Searching the raw file for <c>BDC</c> therefore
    ///   passes locally and fails in CI, and the two tests here that assert something is *absent*
    ///   fail the other way round: they pass against a release build for the wrong reason, because
    ///   nothing at all is legible in a deflated stream.
    /// </remarks>
    private static string ContentOf(byte[] bytes, int pageIndex = 0)
    {
        using var saved = new MemoryStream(bytes);
        var document = Reader.Open(saved, PdfDocumentOpenMode.Modify);
        return Encoding.Latin1.GetString(PageContent.Of(document.Pages[pageIndex]));
    }

    private static byte[] SaveTwoPages()
    {
        var document = new PdfDocument();
        for (var index = 0; index < 2; index++)
        {
            var gfx = XGraphics.FromPdfPage(document.AddPage());
            using (gfx.BeginMarkedContent(PdfTag.P))
                gfx.DrawString("Page " + index, Font, XBrushes.Black, 40, 60);
            gfx.Dispose();
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] Save(Action<XGraphics, PdfDocument> draw)
    {
        var document = new PdfDocument();
        document.Info.Title = "Tagged";
        var gfx = XGraphics.FromPdfPage(document.AddPage());

        draw(gfx, document);
        gfx.Dispose();

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(value, at + 1, StringComparison.Ordinal))
            count++;
        return count;
    }
}
