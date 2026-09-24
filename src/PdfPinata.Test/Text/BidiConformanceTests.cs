using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Text;
using Xunit;
using Xunit.Abstractions;

namespace PdfPinata.Test.Text;

/// <summary>
///   The Unicode Bidirectional Algorithm against the conformance suites the Unicode Consortium
///   publishes for it: <c>BidiTest.txt</c> and <c>BidiCharacterTest.txt</c>, 582,553 cases between
///   them.
/// </summary>
/// <remarks>
///   <para>
///     This is why UAX #9 was worth implementing rather than approximating. The algorithm is a
///     hundred-odd interacting rules and there is no reading of the specification careful enough to
///     be sure of it; either the suite passes or it does not. Every rule this repository could get
///     subtly wrong - the overflow counters, the isolating run sequences, sos and eos, the bracket
///     pairs - has cases here that fail loudly when it is.
///   </para>
///   <para>
///     The files are checked in gzipped, 1.7 MB against 14.8 MB unpacked, because a conformance
///     claim that depends on the network is not one you can make about a build. They are Unicode
///     17.0.0, the same version <c>UnicodeProperties</c> carries its tables from; bumping one
///     without the other tests one Unicode against another's expectations, which is what
///     <c>UnicodeProperties.UnicodeVersion</c> is pinned in a test for.
///   </para>
///   <para>
///     One <c>[Fact]</c> per suite rather than a theory per case: half a million xUnit test cases
///     is not a test run, it is a denial of service on the runner. Each collects its failures and
///     reports the first few with everything needed to reproduce them by hand.
///   </para>
/// </remarks>
public class BidiConformanceTests
{
    private readonly ITestOutputHelper _out;

    public BidiConformanceTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    ///   A code point of each Bidi_Class, for BidiTest.txt - which gives its cases as class names
    ///   rather than as characters, and leaves the choice of representative to the implementation.
    /// </summary>
    /// <remarks>
    ///   The one that needs care is ON: most punctuation is ON, but a bracket is also subject to
    ///   rule N0, which would resolve the case differently from every other ON. An exclamation
    ///   mark is ON and is not a bracket.
    /// </remarks>
    private static readonly Dictionary<string, int> Representative = new()
    {
        ["L"] = 0x0041,     // A
        ["R"] = 0x05D0,     // Hebrew alef
        ["AL"] = 0x0627,    // Arabic alef
        ["EN"] = 0x0030,    // digit zero
        ["ES"] = 0x002B,    // plus
        ["ET"] = 0x0023,    // number sign
        ["AN"] = 0x0660,    // Arabic-Indic digit zero
        ["CS"] = 0x002C,    // comma
        ["NSM"] = 0x0300,   // combining grave
        ["BN"] = 0x00AD,    // soft hyphen
        ["B"] = 0x2029,     // paragraph separator
        ["S"] = 0x0009,     // tab
        ["WS"] = 0x0020,    // space
        ["ON"] = 0x0021,    // exclamation mark
        ["LRE"] = 0x202A,
        ["RLE"] = 0x202B,
        ["PDF"] = 0x202C,
        ["LRO"] = 0x202D,
        ["RLO"] = 0x202E,
        ["LRI"] = 0x2066,
        ["RLI"] = 0x2067,
        ["FSI"] = 0x2068,
        ["PDI"] = 0x2069
    };

    [Fact]
    public void EveryRepresentativeCharacterReallyHasTheClassItStandsFor()
    {
        // The suite is only meaningful if the substitution is faithful, and a wrong representative
        // would fail thousands of cases in ways that looked like algorithm bugs.
        foreach (var (name, codePoint) in Representative)
        {
            UnicodeProperties.BidiClassOf(codePoint).ToString()
                .Should().Be(name, $"U+{codePoint:X4} was chosen to stand for {name}");
        }
    }

    private static IEnumerable<string> ReadLines(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Unicode", name);
        using var file = File.OpenRead(path);
        using var unpacked = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(unpacked, Encoding.UTF8);

        while (reader.ReadLine() is { } line)
            yield return line;
    }

    // ----- BidiTest.txt -------------------------------------------------------------------------

    [Fact]
    public void TheBidiTestSuitePasses()
    {
        var failures = new List<string>();
        var cases = 0;

        var expectedLevels = Array.Empty<byte>();
        var levelIgnored = Array.Empty<bool>();
        var expectedOrder = Array.Empty<int>();

        foreach (var raw in ReadLines("BidiTest.txt.gz"))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                (expectedLevels, levelIgnored) = Levels(Fields(line["@Levels:".Length..]));
                continue;
            }

            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                expectedOrder = Order(Fields(line["@Reorder:".Length..]));
                continue;
            }

            var parts = line.Split(';');
            if (parts.Length < 2)
                continue;

            cases += RunBidiTestLine(parts, expectedLevels, levelIgnored, expectedOrder, failures);
        }

        Report(cases, failures, "BidiTest.txt");
    }

    // The bit in a BidiTest.txt line's bitset that asks for each paragraph direction.
    private static readonly (int Bit, BidiParagraphDirection Direction)[] BitsetDirections =
    [
        (1, BidiParagraphDirection.Automatic),
        (2, BidiParagraphDirection.LeftToRight),
        (4, BidiParagraphDirection.RightToLeft)
    ];

    /// <summary>
    ///   Runs one line of BidiTest.txt in every paragraph direction its bitset asks for, and answers
    ///   how many cases that was.
    /// </summary>
    private static int RunBidiTestLine(string[] parts, byte[] expectedLevels, bool[] levelIgnored,
        int[] expectedOrder, List<string> failures)
    {
        var classes = Fields(parts[0]);
        var codePoints = classes.Select(name => Representative[name]).ToList();
        var bitset = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);

        var cases = 0;
        foreach (var (bit, direction) in BitsetDirections)
        {
            if ((bitset & bit) == 0)
                continue;

            cases++;
            var result = BidiAlgorithm.Resolve(codePoints, direction);
            var complaint = Compare(result, expectedLevels, levelIgnored, expectedOrder);
            if (complaint != null)
                failures.Add($"{string.Join(" ", classes)}; {direction}: {complaint}");
        }

        return cases;
    }

    // ----- BidiCharacterTest.txt ----------------------------------------------------------------

    [Fact]
    public void TheBidiCharacterTestSuitePasses()
    {
        var failures = new List<string>();
        var cases = 0;

        foreach (var raw in ReadLines("BidiCharacterTest.txt.gz"))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.Length == 0)
                continue;

            // codepoints ; direction ; paragraph level ; levels ; visual order
            var parts = line.Split(';');
            if (parts.Length < 5)
                continue;

            cases++;
            var complaint = CharacterTestComplaint(parts);
            if (complaint != null)
                failures.Add($"{parts[0].Trim()}; dir {parts[1].Trim()}: {complaint}");
        }

        Report(cases, failures, "BidiCharacterTest.txt");
    }

    /// <summary>
    ///   Runs one case of BidiCharacterTest.txt, given as its semicolon-separated fields, and says
    ///   what is wrong with the result, or null if nothing is.
    /// </summary>
    private static string CharacterTestComplaint(string[] parts)
    {
        var codePoints = Fields(parts[0])
            .Select(f => int.Parse(f, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToList();

        var direction = int.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) switch
        {
            0 => BidiParagraphDirection.LeftToRight,
            1 => BidiParagraphDirection.RightToLeft,
            _ => BidiParagraphDirection.Automatic
        };

        var paragraphLevel = byte.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
        var (expectedLevels, levelIgnored) = Levels(Fields(parts[3]));
        var expectedOrder = Order(Fields(parts[4]));

        var result = BidiAlgorithm.Resolve(codePoints, direction);

        return result.ParagraphLevel != paragraphLevel
            ? $"paragraph level {result.ParagraphLevel}, expected {paragraphLevel}"
            : Compare(result, expectedLevels, levelIgnored, expectedOrder);
    }

    // ----- comparing ----------------------------------------------------------------------------

    private static string[] Fields(string text)
        => text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///   The expected levels, and which of them are an "x" - a character rule X9 removes, whose
    ///   level is not compared and stands in the array as 0.
    /// </summary>
    private static (byte[] Levels, bool[] Ignored) Levels(string[] fields)
        => ([..fields.Select(f => f == "x" ? (byte)0 : byte.Parse(f, CultureInfo.InvariantCulture))],
            [..fields.Select(f => f == "x")]);

    private static int[] Order(string[] fields)
        => [..fields.Select(f => int.Parse(f, CultureInfo.InvariantCulture))];

    /// <summary>
    ///   What is wrong with a result, or null if nothing is. The suites mark a character the
    ///   algorithm removed with an "x" and leave it out of the expected order, so both have to be
    ///   compared over the characters that survived rule X9 rather than over all of them.
    /// </summary>
    private static string Compare(BidiResult result, byte[] expectedLevels, bool[] levelIgnored, int[] expectedOrder)
        => CompareLevels(result, expectedLevels, levelIgnored) ?? CompareOrder(result, expectedOrder);

    private static string CompareLevels(BidiResult result, byte[] expectedLevels, bool[] levelIgnored)
    {
        if (result.Levels.Count != expectedLevels.Length)
            return $"{result.Levels.Count} levels, expected {expectedLevels.Length}";

        for (var idx = 0; idx < expectedLevels.Length; idx++)
        {
            var complaint = CompareLevelAt(result, expectedLevels, levelIgnored, idx);
            if (complaint != null)
                return complaint;
        }

        return null;
    }

    private static string CompareLevelAt(BidiResult result, byte[] expectedLevels, bool[] levelIgnored, int idx)
    {
        if (levelIgnored[idx])
            return result.Removed[idx] ? null : $"level at {idx} should have been removed by X9";

        if (result.Removed[idx])
            return $"character at {idx} was removed by X9 and should not have been";

        if (result.Levels[idx] != expectedLevels[idx])
            return $"level at {idx} is {result.Levels[idx]}, expected {expectedLevels[idx]}"
                 + $" (levels {string.Join(" ", result.Levels)})";

        return null;
    }

    private static string CompareOrder(BidiResult result, int[] expectedOrder)
    {
        var order = result.VisualOrder;
        if (order.Count != expectedOrder.Length)
            return $"{order.Count} in visual order, expected {expectedOrder.Length}"
                 + $" ({string.Join(" ", order)} against {string.Join(" ", expectedOrder)})";

        for (var idx = 0; idx < expectedOrder.Length; idx++)
        {
            if (order[idx] != expectedOrder[idx])
                return $"visual order {string.Join(" ", order)}, expected {string.Join(" ", expectedOrder)}";
        }

        return null;
    }

    private void Report(int cases, List<string> failures, string suite)
    {
        _out.WriteLine($"{suite}: {cases - failures.Count} of {cases} cases passed.");
        foreach (var failure in failures.Take(20))
            _out.WriteLine("  " + failure);

        cases.Should().BeGreaterThan(0, $"{suite} should have been read and parsed");
        failures.Should().BeEmpty(
            $"{failures.Count} of {cases} cases of {suite} failed; the first few are in the output");
    }
}
