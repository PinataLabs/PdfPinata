using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   A corpus of PinataLayout documents, pinned to the bytes they rendered to before the area a line
///   is laid out in could be anything but a rectangle.
/// </summary>
/// <remarks>
///   Text flowing beside a shape is a change in the middle of the layout engine, and a layout
///   regression is silent: every word is still on the page, a little way from where it was. So
///   "a document that asks for no side wrap is unchanged" has to be an observation.
///   <para>
///   To re-capture after a deliberate change, write <c>PinataLayoutCorpus.OfEveryDocument()</c> to
///   <c>Assets/Layout/PinataLayout-baseline.txt</c> and read the diff before believing it.
///   </para>
/// </remarks>
public class PinataLayoutLayoutPinTests
{
    [Fact]
    public void EveryDocumentInTheCorpusRendersExactlyAsItDid()
    {
        var rendered = SplitByDocument(Normalized(PinataLayoutDocCorpus.OfEveryDocument(tagged: false)));
        var pinned = SplitByDocument(Normalized(File.ReadAllText(BaselinePath)));

        rendered.Keys.Should().BeEquivalentTo(pinned.Keys);

        // Document by document, so a failure names the one that moved rather than reporting a
        // character offset into seven thousand lines.
        foreach (var document in pinned.Keys)
            rendered[document].Should().Be(pinned[document],
                "the '" + document + "' document must render as it did before");
    }

    /// <summary>
    ///   Tagging a document says what is on the page. It must not change what is on the page.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   Tagging is on by default, so the pin above renders with it off — which shows that the
    ///   untagged path is byte-for-byte what it always was, and shows nothing about the path
    ///   everybody now takes. This is the other half.
    ///   </para>
    ///   <para>
    ///   It compares the glyph runs rather than the whole stream, and that is not a weakening for
    ///   convenience. A <c>BDC</c> is always written in graphic mode — a marked-content sequence that
    ///   opened inside a text object would nest within the text rather than contain it — so tagging
    ///   ends the text object before each scope and starts a new one after it. Every <c>Td</c> in a
    ///   fresh text object is measured from the origin instead of from the line before, so the
    ///   operands legitimately differ while every glyph lands in the same place. What can be compared
    ///   exactly is the text: the same runs, in the same order, page for page.
    ///   </para>
    /// </remarks>
    [Fact]
    public void TaggingDrawsTheSameTextInTheSameOrder()
    {
        var tagged = SplitByDocument(Normalized(PinataLayoutDocCorpus.OfEveryDocument(tagged: true)));
        var plain = SplitByDocument(Normalized(PinataLayoutDocCorpus.OfEveryDocument(tagged: false)));

        foreach (var document in plain.Keys)
            GlyphRuns(tagged[document]).Should().Equal(GlyphRuns(plain[document]),
                "tagging the '" + document + "' document must not have changed a word of it");
    }

    /// <summary>
    ///   Every run of glyphs drawn, and the page it was drawn on, in order.
    /// </summary>
    /// <remarks>
    ///   The glyphs alone, without the positioning operand that precedes them on the line. That
    ///   operand is exactly what tagging is entitled to change — a text object restarted after a
    ///   <c>BDC</c> measures from the origin rather than from the previous line — so including it
    ///   would fail this test for the one difference it exists to permit.
    /// </remarks>
    static List<string> GlyphRuns(string content)
    {
        var runs = new List<string>();

        foreach (var line in content.Split('\n'))
        {
            // The page markers are kept in the list rather than filtered out, so that a run moving
            // from one page to another fails instead of passing on the strength of the total.
            if (line.StartsWith("--- page ", StringComparison.Ordinal))
            {
                runs.Add(line);
                continue;
            }

            if (!line.EndsWith(" Tj", StringComparison.Ordinal)
                && !line.EndsWith(" TJ", StringComparison.Ordinal))
                continue;

            var opens = line.IndexOf('<');
            var closes = line.LastIndexOf('>');
            if (opens >= 0 && closes > opens)
                runs.Add(line.Substring(opens, closes - opens + 1));
        }

        return runs;
    }

    [Fact]
    public void TheBaselineCoversEveryDocumentInTheCorpus()
    {
        var pinned = SplitByDocument(Normalized(File.ReadAllText(BaselinePath)));

        // Taken from the corpus rather than written out again here, so that adding a document
        // without re-capturing fails instead of going uncovered.
        pinned.Keys.Should().BeEquivalentTo(PinataLayoutDocCorpus.Names);
        pinned.Keys.Should().Contain(new[]
        {
            "table across a page break", "text frame beside prose", "image between paragraphs",
        });
    }

    static string BaselinePath =>
        Path.Combine(PathHelper.GetInstance().GetAssetPath("Layout"), "PinataLayout-baseline.txt");

    static Dictionary<string, string> SplitByDocument(string report)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        string current = null;
        var body = new StringBuilder();

        foreach (var line in report.Split('\n'))
        {
            if (line.StartsWith("=== ", StringComparison.Ordinal) && line.EndsWith(" ===", StringComparison.Ordinal))
            {
                if (current != null)
                    documents[current] = body.ToString();

                current = line.Substring(4, line.Length - 8);
                body.Clear();
                continue;
            }

            body.Append(line).Append('\n');
        }

        if (current != null)
            documents[current] = body.ToString();

        return documents;
    }

    /// <summary>
    ///   The same text with every line ending reduced to a line feed, so that what is compared is
    ///   the layout and not what the checkout did to the asset file.
    /// </summary>
    static string Normalized(string text) => text.Replace("\r\n", "\n");
}
