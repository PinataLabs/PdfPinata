using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   A tab that meets none of the paragraph's own stops goes to the next of the document's default
///   ones, found by stepping along the line one <see cref="Document.DefaultTabStop" /> at a time.
///   A step of nothing, or a step backwards, never reached the end of the line, so a document whose
///   default tab stop was zero or negative never finished rendering.
/// </summary>
public class DefaultTabStopTests
{
    [Test, Timeout(60000)]
    [Arguments(0.0)]
    [Arguments(-28.35)]
    [Arguments(1e-9)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NaN)]
    public async Task ADefaultTabStopThatCannotBeSteppedByIsTreatedAsUnset(double points)
    {
        // Unset, the renderer steps 1.25cm at a time. A value that cannot be stepped by is no
        // more an interval than no value at all, so the tab lands where an unset one would.
        // Zero and a negative never reach the end of the line, a billionth of a point takes
        // hundreds of billions of steps to, and infinity used to reach no stop at all and break
        // the line at the tab.
        var unset = await PositionsAfterATab(defaultTabStop: null);
        var unusable = await PositionsAfterATab(Unit.FromPoint(points));

        unusable.Should().HaveCount(unset.Count);
        for (var idx = 0; idx < unset.Count; idx++)
            unusable[idx].Should().BeApproximately(unset[idx], 0.001);
    }

    [Test, Timeout(60000)]
    public async Task APositiveDefaultTabStopIsStillHonoured()
    {
        // The control: a usable interval is the one the tab steps by, not the fallback.
        var unset = await PositionsAfterATab(defaultTabStop: null);
        var threeCentimeters = await PositionsAfterATab(Unit.FromCentimeter(3));

        // A4 with PinataLayout's default 2.5cm margins: the text after the tab starts on the
        // first 3cm stop, which the text before it falls well short of.
        var leftEdge = Unit.FromCentimeter(2.5).Point;
        threeCentimeters[^1].Should().BeApproximately(leftEdge + Unit.FromCentimeter(3).Point, 0.001);
        unset[^1].Should().BeApproximately(leftEdge + Unit.FromCentimeter(1.25).Point, 0.001);
    }

    /// <summary>
    ///   Where every run of text on the page starts, for a paragraph of one short word, a tab, and
    ///   another word.
    /// </summary>
    private static async Task<IReadOnlyList<double>> PositionsAfterATab(Unit? defaultTabStop)
    {
        return await Interruptibly.Run<IReadOnlyList<double>>(() =>
        {
            var document = new Document();
            if (defaultTabStop is { } stop)
                document.DefaultTabStop = stop;

            var paragraph = document.AddSection().AddParagraph();
            paragraph.AddText("a");
            paragraph.AddTab();
            paragraph.AddText("b");

            var renderer = new PdfDocumentRenderer(true) { Document = document };
            renderer.RenderDocument();

            // Saving is what closes the content streams, so the page cannot be read before it.
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);

            var positions = new List<double>();
            foreach (var (x, _) in TextBaselines.PositionsOf(renderer.PdfDocument.Pages[0]))
                positions.Add(x);
            return positions;
        });
    }
}
