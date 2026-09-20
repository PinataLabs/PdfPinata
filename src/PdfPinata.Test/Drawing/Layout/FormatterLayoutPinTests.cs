using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   Every way <see cref="PdfPinata.Drawing.Layout.XTextFormatter"/> can be asked to lay text
///   out, pinned to the bytes it wrote before the measure could vary from line to line.
/// </summary>
/// <remarks>
///   A drop cap needs the width available to a line to depend on where the line sits, which means
///   changing the loop that breaks every line of every document this library has written. A layout
///   regression is silent — the page still carries all its words, in slightly the wrong places —
///   so "unchanged when nothing narrows a line" has to be an observation rather than an intention.
///   <para>
///   To re-capture after a deliberate change, write
///   <c>FormatterOutput.OfEveryArrangement()</c> to <c>Assets/Layout/formatter-baseline.txt</c> and
///   read the diff before believing it.
///   </para>
/// </remarks>
public class FormatterLayoutPinTests
{
    [Fact]
    public void TextLaidOutWithNothingNarrowingItIsWrittenExactlyAsItWas()
    {
        var written = Normalized(FormatterOutput.OfEveryArrangement());
        var pinned = Normalized(File.ReadAllText(BaselinePath));

        // Compared arrangement by arrangement, so a failure names the one that moved rather than
        // reporting a character offset into six hundred lines.
        var writtenPages = SplitByArrangement(written);
        var pinnedPages = SplitByArrangement(pinned);

        writtenPages.Keys.Should().BeEquivalentTo(pinnedPages.Keys);

        foreach (var arrangement in pinnedPages.Keys)
            writtenPages[arrangement].Should().Be(pinnedPages[arrangement],
                "the '" + arrangement + "' arrangement must lay out as it did before");
    }

    [Fact]
    public void TheBaselineCoversEveryArrangementTheFormatterOffers()
    {
        var pinned = SplitByArrangement(Normalized(File.ReadAllText(BaselinePath)));

        // A pin that quietly stopped covering the justified case would pass while justification
        // broke. Taken from the arrangements themselves rather than written out again here, so
        // that adding one without re-capturing the baseline fails instead of going uncovered.
        pinned.Keys.Should().BeEquivalentTo(FormatterOutput.ArrangementNames);
        pinned.Keys.Should().Contain(new[]
        {
            "plain", "justified", "centred", "right", "ellipsis", "two columns", "rotated"
        });
    }

    static string BaselinePath =>
        Path.Combine(PathHelper.GetInstance().GetAssetPath("Layout"), "formatter-baseline.txt");

    /// <summary>
    ///   The content of each arrangement, keyed by the name the report gives it.
    /// </summary>
    static System.Collections.Generic.Dictionary<string, string> SplitByArrangement(string report)
    {
        var pages = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        string current = null;
        var body = new System.Text.StringBuilder();

        foreach (var line in report.Split('\n'))
        {
            if (line.StartsWith("--- ", StringComparison.Ordinal) && line.EndsWith(" ---", StringComparison.Ordinal))
            {
                if (current != null)
                    pages[current] = body.ToString();

                current = line.Substring(4, line.Length - 8);
                body.Clear();
                continue;
            }

            body.Append(line).Append('\n');
        }

        if (current != null)
            pages[current] = body.ToString();

        return pages;
    }

    /// <summary>
    ///   The same text with every line ending reduced to a line feed, so that what is compared is
    ///   the layout and not what the checkout did to the asset file.
    /// </summary>
    static string Normalized(string text) => text.Replace("\r\n", "\n");
}
