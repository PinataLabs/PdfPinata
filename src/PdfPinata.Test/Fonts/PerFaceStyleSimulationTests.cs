using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   Bold simulation decided per face rather than once for the string.
/// </summary>
/// <remarks>
///   <para>
///     A family with no bold file has its boldness drawn on: the glyphs are stroked as well as
///     filled, and widened by a character spacing of their own. That is a property of the
///     <em>face</em>, and a string that fell back is drawn out of more than one - so the face the
///     caller named having no bold says nothing about the face that rescued it.
///   </para>
///   <para>
///     It used to be decided once, from the face that was asked for, and applied to the whole
///     string. A fallback with a real bold was stroked and widened anyway, and a fallback without
///     one was left thin beside a primary that had been thickened.
///   </para>
///   <para>
///     The two faces here make both directions reachable. <c>PinnedFontResolver</c> ships only a
///     regular Source Code Pro and simulates its bold, and answers every other family with one of
///     Liberation Sans's four real style files - so Source Code Pro Bold is a simulated bold and
///     Arial Bold is a real one.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class PerFaceStyleSimulationTests
{
    // U+1F512 LOCK: in Source Code Pro's format 12 subtable and in no Liberation Sans face.
    private const int Lock = 0x1F512;

    // Two Arabic letters, which Liberation Sans has and Source Code Pro has not.
    private const string Arabic = "سل";

    /// <summary>Bold here is simulated: the family ships a regular face alone.</summary>
    private static XFont SimulatedBold() => new XFont(PinnedFontResolver.CffFamilyName, 20, XFontStyle.Bold);

    /// <summary>Bold here is a real file.</summary>
    private static XFont RealBold() => new XFont("Arial", 20, XFontStyle.Bold);

    private sealed class Installed : IDisposable
    {
        internal Installed(IFontFallback fallback) => GlobalFontSettings.FontFallback = fallback;

        public void Dispose() => GlobalFontSettings.FontFallback = null;
    }

    private sealed class Only : IFontFallback
    {
        private readonly HashSet<int> _mine;
        private readonly string[] _families;

        internal Only(IEnumerable<int> codePoints, params string[] families)
        {
            _mine = new HashSet<int>(codePoints);
            _families = families;
        }

        public IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic)
            => _mine.Contains(codePoint) ? _families : Enumerable.Empty<string>();
    }

    /// <summary>The text rendering modes written, in order: 0 fills, 2 fills and strokes.</summary>
    private static int[] RenderingModes(string content)
        => Regex.Matches(content, @"(\d+) Tr")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToArray();

    // ----- the case the gap was written for -----------------------------------------------------

    [Fact]
    public void AFallbackWithARealBoldIsNotSimulated()
    {
        // Source Code Pro Bold is stroked because the family has no bold file. The Arabic beside it
        // is drawn from a face that was never asked to simulate anything, and used to be stroked
        // regardless - a thickened Arabic letter for no reason but its neighbour.
        using var _ = new Installed(new Only(Arabic.Select(letter => (int)letter),
            PinnedFontResolver.ArabicFamilyName));

        var content = DrawnText.ContentOf(DrawnText.Page("A" + Arabic, SimulatedBold()));

        RenderingModes(content).Should().Equal(new[] { 2, 0, 2 },
            "the simulated primary strokes, the fallback does not, and the state is put back");
    }

    [Fact]
    public void TheFallbackIsNotWidenedEither()
    {
        // The other half of simulation. A character spacing left in place would space the fallback
        // out as though it had been thickened, which is visible even where the stroke is not.
        using var _ = new Installed(new Only(Arabic.Select(letter => (int)letter),
            PinnedFontResolver.ArabicFamilyName));

        var content = DrawnText.ContentOf(DrawnText.Page("A" + Arabic, SimulatedBold()));

        Regex.Matches(content, @"([\d.]+) Tc").Select(match => match.Groups[1].Value)
            .Should().Equal(new[] { "0.4", "0", "0.4" },
                "0.4 is 20pt at the bold-emphasis factor; the fallback owes none of it");
    }

    // ----- and the reverse, which the same rule has to cover ----------------------------------------

    [Fact]
    public void AFallbackWithoutARealBoldIsSimulated()
    {
        // Arial Bold is a real file and is not stroked. The lock is drawn from Source Code Pro,
        // whose bold is simulated, so that segment alone is stroked - which needs the stroking
        // colour and width to have been set up even though the face that was asked for did not
        // want them.
        using var _ = new Installed(new Only(new[] { Lock }, PinnedFontResolver.CffFamilyName));

        var content = DrawnText.ContentOf(
            DrawnText.Page("A" + char.ConvertFromUtf32(Lock) + "B", RealBold()));

        RenderingModes(content).Should().Equal(new[] { 2, 0, 2, 0, 2 },
            "realized for the face that needs stroking, off for the Latin, on for the emoji, "
            + "off for the Latin after it, and back to what the graphics state believes");
    }

    [Fact]
    public void TheStrokingWidthIsReadyEvenWhenTheFaceAskedForDoesNotWantIt()
    {
        // Without this the emoji above would be stroked with whatever line width happened to be
        // current, because nothing would have realized a pen for a primary face that needed none.
        using var _ = new Installed(new Only(new[] { Lock }, PinnedFontResolver.CffFamilyName));

        var content = DrawnText.ContentOf(
            DrawnText.Page("A" + char.ConvertFromUtf32(Lock), RealBold()));

        content.Should().MatchRegex(@"0\.4 w",
            "20pt at the bold-emphasis factor, realized before any text is shown");
    }

    // ----- measuring has to agree, or the line is laid out at a width nothing draws -------------------

    [Fact]
    public void EachSegmentIsMeasuredAtItsOwnSimulation()
    {
        // The assertion that ties the two paths together. If measuring still applied the primary
        // face's simulation to every glyph, the mixed string would measure wider than its parts.
        using var _ = new Installed(new Only(new[] { Lock }, PinnedFontResolver.CffFamilyName));

        var emoji = char.ConvertFromUtf32(Lock);

        var mixed = DrawnText.MeasuredWidth("A" + emoji, RealBold());
        var latinAlone = DrawnText.MeasuredWidth("A", RealBold());
        var emojiAlone = DrawnText.MeasuredWidth(emoji, SimulatedBold());

        mixed.Should().BeApproximately(latinAlone + emojiAlone, 1e-9,
            "the Latin is measured unsimulated and the emoji simulated, each as it is drawn");
    }

    // ----- what must not have changed -----------------------------------------------------------------

    [Fact]
    public void AStringThatNeededNoFallbackWritesNoExtraState()
    {
        // Every string that never fell back goes down the other path entirely, and this is the
        // assertion that says the common case did not pay for any of the above.
        var content = DrawnText.ContentOf(DrawnText.Page("Hello", SimulatedBold()));

        RenderingModes(content).Should().Equal(new[] { 2 },
            "one rendering mode, written once, exactly as before");
    }

    [Fact]
    public void AnUnsimulatedStringIsStillModeZero()
    {
        var content = DrawnText.ContentOf(DrawnText.Page("Hello", RealBold()));

        RenderingModes(content).Should().BeEmpty(
            "mode 0 is the initial state and is never written out");
    }
}
