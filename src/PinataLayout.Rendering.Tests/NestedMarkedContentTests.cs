using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Two marked-content sequences each carrying an MCID are never one inside the other.
/// </summary>
/// <remarks>
///   <para>
///     A sequence with an MCID is a content item of exactly one structure element. Nest two and the
///     inner glyphs belong to both: the mark of a footnote inside the paragraph that cites it is
///     claimed by the <c>/Reference</c> and by the <c>/P</c>, and nothing says which should read it.
///     veraPDF warns <c>Nested MCID</c> about precisely this, and the tagged corpus document used to
///     draw four of them.
///   </para>
///   <para>
///     The fix is to suspend the open sequence and resume it afterwards rather than to nest inside
///     it, which works because an element may own as many content items as it likes — the same thing
///     that already makes a paragraph broken over two pages one paragraph.
///   </para>
/// </remarks>
public class NestedMarkedContentTests
{
    /// <summary>The content stream of the document's first page.</summary>
    private static string ContentOf(Document document)
        => Encoding.ASCII.GetString(PageContent.Of(Rendered.FirstPageOf(document)));

    /// <summary>Every marked-content operator on the page, in order.</summary>
    private static string[] Operators(Document document)
        => Regex.Matches(ContentOf(document), @"/\w+\s*(?:<<[^>]*>>\s*)?(BDC|BMC)|EMC")
            .Select(match => match.Value.Contains("EMC") ? "EMC" : "BEGIN")
            .ToArray();

    /// <summary>How deep the marked-content nesting ever gets.</summary>
    private static int DeepestNesting(Document document)
    {
        int depth = 0, deepest = 0;
        foreach (var op in Operators(document))
        {
            if (op == "EMC")
                depth--;
            else
                deepest = System.Math.Max(deepest, ++depth);
        }

        depth.Should().Be(0, "every sequence opened has to be closed");
        return deepest;
    }

    [Fact]
    public void AFootnoteMarkIsNotNestedInsideTheParagraphThatCitesIt()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        DeepestNesting(document).Should().Be(1,
            "the paragraph is suspended around the reference rather than wrapped round it");
    }

    [Fact]
    public void AHyperlinkIsNotNestedInsideItsParagraph()
    {
        var document = Document(out var section);
        var paragraph = section.AddParagraph("See ");
        paragraph.AddHyperlink("https://example.com", HyperlinkType.Web).AddText("the terms");
        paragraph.AddText(" before signing.");

        DeepestNesting(document).Should().Be(1);
    }

    [Fact]
    public void AListLabelIsNotNestedInsideItsBody()
    {
        var document = Document(out var section);
        foreach (var text in new[] { "First", "Second" })
        {
            var item = section.AddParagraph(text);
            item.Format.ListInfo.ListType = ListType.NumberList1;
        }

        DeepestNesting(document).Should().Be(1);
    }

    [Fact]
    public void TheParagraphKeepsTheTextOnBothSidesOfWhatInterruptedIt()
    {
        // Suspending is only safe because the sequence is resumed. Without the resume the text after
        // the link would be outside the structure tree entirely, which is a PDF/UA failure rather
        // than the warning nesting was.
        var document = Document(out var section);
        var paragraph = section.AddParagraph("Before ");
        paragraph.AddHyperlink("https://example.com", HyperlinkType.Web).AddText("the link");
        paragraph.AddText(" and after.");

        var text = Structure.Of(document).OfTag("P").Single();

        text.MarkCount.Should().BeGreaterThan(1,
            "the paragraph owns a content item on each side of the link");
    }

    [Fact]
    public void AParagraphEndingInALinkGainsNoEmptyContentItem()
    {
        // The sequence resumed after the link is closed with nothing drawn in it, so it is taken
        // back — and its identifier has to go back too, or the tree names marks the content stream
        // does not hold.
        var document = Document(out var section);
        var paragraph = section.AddParagraph("See ");
        paragraph.AddHyperlink("https://example.com", HyperlinkType.Web).AddText("the terms");

        var content = ContentOf(document);
        var identifiers = Regex.Matches(content, @"/MCID (\d+)")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();

        identifiers.Should().OnlyHaveUniqueItems();
        identifiers.OrderBy(id => id).Should().Equal(Enumerable.Range(0, identifiers.Length),
            "the identifiers are indices into the page's marks, so a gap means one was handed out "
            + "for a sequence that was then removed");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    private static Document Document(out Section section)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Sans";
        normal.Font.Size = 11;

        document.Styles[StyleNames.Footnote].Font.Size = 8;

        section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        return document;
    }
}
