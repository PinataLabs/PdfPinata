using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Text;
using TUnit.Core;

namespace PdfPinata.Test.Text;

/// <summary>
///   The Unicode Line Breaking Algorithm against <c>LineBreakTest.txt</c>, the conformance suite the
///   Unicode Consortium publishes for UAX #14's default rules: 19,338 texts and every position in
///   each.
/// </summary>
/// <remarks>
///   <para>
///     Checked in gzipped beside the bidi suites, and for the same reasons: it is Unicode 17.0.0,
///     the version the line break table was generated from, and a conformance claim that depends on
///     the network is not one a build can make. One <c>[Test]</c> for the whole suite rather than
///     one per case.
///   </para>
///   <para>
///     Each line gives a text as code points with a "×" or a "÷" at every position from the start
///     to the end, and a comment naming the rule that decided each. A failure is reported in those
///     terms - the position, what was expected, and the rule that expected it - because the rule is
///     where to look.
///   </para>
///   <para>
///     Nothing is skipped. The suite tests the default algorithm, and so does this, with
///     <see cref="LineBreakTailoring.None"/>; the tailoring PinataLayout asks for is tested on its
///     own in <see cref="LineBreakAlgorithmTests"/>.
///   </para>
/// </remarks>
public class LineBreakConformanceTests
{
    [Test]
    public void TheLineBreakTestSuitePasses()
    {
        var failures = new List<string>();
        var cases = 0;
        var lineNumber = 0;

        foreach (var raw in ReadLines("LineBreakTest.txt.gz"))
        {
            lineNumber++;
            var hash = raw.IndexOf('#');
            var data = (hash >= 0 ? raw[..hash] : raw).Trim();
            if (data.Length == 0)
                continue;

            cases++;
            var complaint = Complaint(data, hash >= 0 ? raw[(hash + 1)..] : "");
            if (complaint != null)
                failures.Add($"line {lineNumber}: {data}: {complaint}");
        }

        TestContext.Current!.Output.WriteLine($"LineBreakTest.txt: {cases - failures.Count} of {cases} cases passed.");
        foreach (var failure in failures.Take(20))
            TestContext.Current!.Output.WriteLine("  " + failure);

        cases.Should().BeGreaterThan(0, "LineBreakTest.txt should have been read and parsed");
        failures.Should().BeEmpty(
            $"{failures.Count} of {cases} cases of LineBreakTest.txt failed; the first few are in the output");
    }

    /// <summary>
    ///   What is wrong with the answer for one case, or null if nothing is.
    /// </summary>
    private static string Complaint(string data, string comment)
    {
        // "× 0041 ÷ 0020 ..." - a mark, then a code point, alternately, ending with a mark.
        var fields = data.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var marks = fields.Where((_, idx) => idx % 2 == 0).Select(mark => mark == "÷").ToList();
        var codePoints = fields.Where((_, idx) => idx % 2 == 1)
            .Select(field => int.Parse(field, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            .ToList();

        var text = new StringBuilder();
        var positions = new List<int>();
        foreach (var codePoint in codePoints)
        {
            positions.Add(text.Length);
            text.Append(char.ConvertFromUtf32(codePoint));
        }
        positions.Add(text.Length);

        var rules = Rules.Matches(comment).Select(match => match.Groups[1].Value).ToList();
        var answer = LineBreakAlgorithm.BreakOpportunities(text.ToString());

        for (var idx = 0; idx < positions.Count; idx++)
        {
            var broken = answer[positions[idx]] != LineBreakOpportunity.None;
            if (broken == marks[idx])
                continue;

            var where = idx < codePoints.Count ? $"before U+{codePoints[idx]:X4}" : "at the end";
            var rule = idx < rules.Count ? RuleName(rules[idx]) : "an unnamed rule";
            return $"position {idx} ({where}) should be {(marks[idx] ? "÷" : "×")} by {rule}, "
                 + $"and is {(broken ? "÷" : "×")}";
        }

        return null;
    }

    /// <summary>The "[13.02]" after each mark in a case's comment: the rule that decided it.</summary>
    private static readonly Regex Rules = new(@"[×÷]\s*\[([0-9.]+)\]", RegexOptions.Compiled);

    /// <summary>
    ///   UAX #14's name for one of the suite's rule numbers: "13.02" is LB13, "15.21" LB15b - the
    ///   first digit after the point being the letter, if there is one - and "999.0" LB31.
    /// </summary>
    private static string RuleName(string number)
    {
        if (number == "999.0")
            return "LB31";

        var halves = number.Split('.');
        if (halves[0] == "0")
            return "LB2/LB3 (sot and eot)";

        var letter = halves.Length > 1 && halves[1].Length > 0 && halves[1][0] != '0'
            ? ((char)('a' + halves[1][0] - '1')).ToString()
            : "";
        return $"LB{halves[0]}{letter} ([{number}])";
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
}
