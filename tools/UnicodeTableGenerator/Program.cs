using System.Globalization;
using System.Text;

// Reads the Unicode Character Database and writes the range tables PdfPinata.Text looks
// character properties up in. See README.md beside this file.

var version = Argument(args, "--version") ?? "17.0.0";
var cache = Argument(args, "--cache") ?? Path.Combine(Path.GetTempPath(), "ucd-" + version);
var output = Argument(args, "--out") ?? Path.Combine("src", "PdfPinata", "Text");

Directory.CreateDirectory(cache);
Directory.CreateDirectory(output);

Console.WriteLine($"Unicode {version}, cache {cache}, out {output}");

// DerivedBidiClass.txt names its values by their short alias ("AL"), Scripts.txt by their long
// name ("Arabic"), and PropertyValueAliases.txt is what reconciles them. Everything below works in
// long names and reaches for the short one only when it needs a code.
var aliases = ReadAliases(await Fetch("ucd/PropertyValueAliases.txt"));
var (bidiToLong, bidiToShort) = aliases["bc"];
var (scriptToLong, scriptToShort) = aliases["sc"];

// ----- Bidi_Class -------------------------------------------------------------------------------

var bidiText = await Fetch("ucd/extracted/DerivedBidiClass.txt");

// The @missing lines are not commentary: DerivedBidiClass.txt lists the assigned characters and
// leaves unassigned ones to defaults that are *not* all Left_To_Right. Unassigned code points in
// the Hebrew and Arabic blocks default to Right_To_Left and Arabic_Letter, and getting that wrong
// makes the algorithm quietly incorrect for exactly the scripts it exists for.
var bidi = Partition(
    Canonical(Defaults(bidiText), bidiToLong),
    Canonical(Ranges(bidiText), bidiToLong),
    fallback: "Left_To_Right");

var bidiNames = OrderedValues(bidi, bidiToShort);
Write(Path.Combine(output, "BidiClass.g.cs"), Emit(
    version, "BidiClass", "byte",
    "The Bidi_Class property of a character, as UAX #9 names it.",
    bidiNames, bidi, bidiToShort, scriptCodes: false));

// ----- Script -----------------------------------------------------------------------------------

var scriptText = await Fetch("ucd/Scripts.txt");
var script = Partition(
    Canonical(Defaults(scriptText), scriptToLong),
    Canonical(Ranges(scriptText), scriptToLong),
    fallback: "Unknown");

var scriptNames = OrderedValues(script, scriptToShort);
Write(Path.Combine(output, "UnicodeScript.g.cs"), Emit(
    version, "UnicodeScript", "byte",
    "The Script property of a character, as UAX #24 names it.",
    scriptNames, script, scriptToShort, scriptCodes: true));

// ----- Line_Break ---------------------------------------------------------------------------------

var (lineBreakToLong, lineBreakToShort) = aliases["lb"];
var lineBreakText = await Fetch("ucd/LineBreak.txt");

// The same care as Bidi_Class. Unassigned code points do not all default to XX: those in the CJK
// ideograph blocks, in planes 2 and 3 and in two ranges of plane 1 default to ID, and those in the
// currency symbols block to PR, so that a character assigned there later breaks as its neighbours
// do. 17.0.0 lists those defaults as explicit ranges of reserved code points and keeps a single
// "@missing: 0000..10FFFF; XX" line for the rest; the defaults are read from @missing lines as
// well, so a version that states them there instead is materialised the same way.
var lineBreakValues = Materialize(
    Canonical(Defaults(lineBreakText), lineBreakToLong),
    Canonical(Ranges(lineBreakText), lineBreakToLong),
    fallback: "Unknown");
var lineBreak = Compress(lineBreakValues);

var lineBreakNames = OrderedValues(lineBreak, lineBreakToShort);
Write(Path.Combine(output, "LineBreakClass.g.cs"), Emit(
    version, "LineBreakClass", "byte",
    "The Line_Break property of a character, as UAX #14 names it.",
    lineBreakNames, lineBreak, lineBreakToShort, scriptCodes: false));

// ----- what else UAX #14's rules read -------------------------------------------------------------

// The line breaking rules are written mostly in Line_Break classes, but six of them also read
// another property of a character, and those properties are carried here rather than asked of the
// runtime so that every target framework answers the same, from the same Unicode:
//
//   East_Asian_Width   LB19a and LB30 treat a quotation mark or a parenthesis differently
//                      beside a Fullwidth, Wide or Halfwidth character.
//   General_Category   LB15a, LB15b and LB19 tell an initial quotation mark (Pi) from a final one
//                      (Pf); LB1 resolves an SA mark (Mn or Mc) to CM and any other SA to AL; and
//                      LB30b needs to know which code points are unassigned (Cn).
//   Extended_Pictographic  LB30b, with Cn: an emoji modifier is not broken from an unassigned
//                      pictographic code point, because a future emoji base may be assigned there.
//
// Only the combinations a rule reads are recorded - a final quotation mark is Pf *and* QU, an SA
// mark is SA *and* Mn or Mc - which keeps the table to the few hundred runs those rules need rather
// than a General_Category partition of several thousand.
var (eaToLong, _) = aliases["ea"];
var (gcToLong, _) = aliases["gc"];

var eastAsianText = await Fetch("ucd/EastAsianWidth.txt");
var eastAsianWidth = Materialize(
    Canonical(Defaults(eastAsianText), eaToLong),
    Canonical(Ranges(eastAsianText), eaToLong),
    fallback: "Neutral");

var categoryText = await Fetch("ucd/extracted/DerivedGeneralCategory.txt");
var category = Materialize(
    Canonical(Defaults(categoryText), gcToLong),
    Canonical(Ranges(categoryText), gcToLong),
    fallback: "Unassigned");

var pictographic = new bool[Ucd.MaxCodePoint + 1];
foreach (var (first, last, property) in Ranges(await Fetch("ucd/emoji/emoji-data.txt")))
{
    if (property != "Extended_Pictographic")
        continue;

    for (var cp = first; cp <= last; cp++)
        pictographic[cp] = true;
}

var lineBreakFlags = new string[Ucd.MaxCodePoint + 1];
for (var cp = 0; cp <= Ucd.MaxCodePoint; cp++)
{
    var flags = 0;
    if (eastAsianWidth[cp] is "Fullwidth" or "Wide" or "Halfwidth")
        flags |= 1;
    if (lineBreakValues[cp] == "Quotation" && category[cp] == "Initial_Punctuation")
        flags |= 2;
    if (lineBreakValues[cp] == "Quotation" && category[cp] == "Final_Punctuation")
        flags |= 4;
    if (lineBreakValues[cp] == "Complex_Context" && category[cp] is "Nonspacing_Mark" or "Spacing_Mark")
        flags |= 8;
    if (pictographic[cp] && category[cp] == "Unassigned")
        flags |= 16;

    lineBreakFlags[cp] = flags.ToString(CultureInfo.InvariantCulture);
}

Write(Path.Combine(output, "LineBreakFlags.g.cs"), EmitLineBreakFlags(version, Compress(lineBreakFlags)));

// ----- paired brackets, for rule N0 ---------------------------------------------------------------

// "code; paired code; type", one line per bracket, with the type in a third column that the
// general range reader has no use for.
var bracketText = await Fetch("ucd/BidiBrackets.txt");
var pairs = new List<(int Open, int Close)>();
foreach (var line in Lines(bracketText))
{
    var parts = line.Split(';', StringSplitOptions.TrimEntries);
    if (parts.Length < 3 || parts[2] != "o")
        continue;

    pairs.Add((Convert.ToInt32(parts[0], 16), Convert.ToInt32(parts[1], 16)));
}
pairs.Sort();

Write(Path.Combine(output, "BracketPairs.g.cs"), $@"// <auto-generated>
//   Generated from the Unicode Character Database {version} by tools/UnicodeTableGenerator.
//   Do not edit by hand. See BidiClass.g.cs for how to regenerate.
// </auto-generated>

namespace PdfPinata.Text;

static partial class UnicodeTables
{{
    /// <summary>
    /// The opening character of each canonical bracket pair, ascending - the Bidi_Paired_Bracket
    /// property where Bidi_Paired_Bracket_Type is Open. Rule N0 pairs them up.
    /// </summary>
    internal static readonly int[] BracketOpen =
    {{
{Wrap(pairs.Select(p => "0x" + p.Open.ToString("X", CultureInfo.InvariantCulture)))}    }};

    /// <summary>The closing character of each pair of <see cref=""BracketOpen""/>.</summary>
    internal static readonly int[] BracketClose =
    {{
{Wrap(pairs.Select(p => "0x" + p.Close.ToString("X", CultureInfo.InvariantCulture)))}    }};
}}
");

// Its own file, because the tables are partial parts of the same class and only one of them can
// declare it.
Write(Path.Combine(output, "UnicodeVersion.g.cs"), $@"// <auto-generated>
//   Generated from the Unicode Character Database {version} by tools/UnicodeTableGenerator.
//   Do not edit by hand. See BidiClass.g.cs for how to regenerate.
// </auto-generated>

namespace PdfPinata.Text;

static partial class UnicodeTables
{{
    /// <summary>The version of the Unicode Character Database these tables were built from.</summary>
    public const string UnicodeVersion = ""{version}"";
}}
");

Console.WriteLine("done");
return 0;

// ----- reading the database ----------------------------------------------------------------------

static string Argument(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

async Task<string> Fetch(string path)
{
    var file = Path.Combine(cache, path.Replace('/', '_'));
    if (File.Exists(file))
        return File.ReadAllText(file);

    var url = $"https://www.unicode.org/Public/{version}/{path}";
    Console.WriteLine("fetching " + url);
    using var http = new HttpClient();
    var text = await http.GetStringAsync(url);
    File.WriteAllText(file, text);
    return text;
}

/// <summary>
/// Per property: every spelling of a value to its long name, and every long name to its short
/// alias. A line reads "sc ; Arab ; Arabic", short first and long second, with any further
/// aliases after that.
/// </summary>
static Dictionary<string, (Dictionary<string, string> ToLong, Dictionary<string, string> ToShort)>
    ReadAliases(string text)
{
    var byProperty =
        new Dictionary<string, (Dictionary<string, string>, Dictionary<string, string>)>();

    foreach (var line in Lines(text))
    {
        var parts = line.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            continue;

        if (!byProperty.TryGetValue(parts[0], out var maps))
            byProperty[parts[0]] = maps = (new Dictionary<string, string>(), new Dictionary<string, string>());

        var (toLong, toShort) = maps;
        var longName = parts[2];

        toShort[longName] = parts[1];
        for (var idx = 1; idx < parts.Length; idx++)
            toLong[parts[idx]] = longName;
    }

    return byProperty;
}

/// <summary>The same ranges with every value spelled as its long name.</summary>
static List<(int First, int Last, string Value)> Canonical(
    IEnumerable<(int First, int Last, string Value)> ranges, Dictionary<string, string> toLong)
    => ranges.Select(r => (r.First, r.Last, toLong.GetValueOrDefault(r.Value, r.Value))).ToList();

static IEnumerable<string> Lines(string text)
{
    foreach (var raw in text.Split('\n'))
    {
        var line = raw.Split('#')[0].Trim();
        if (line.Length > 0)
            yield return line;
    }
}

/// <summary>The explicit ranges of a UCD file, as (first, last, value).</summary>
static IEnumerable<(int First, int Last, string Value)> Ranges(string text)
{
    foreach (var line in Lines(text))
    {
        var parts = line.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            continue;

        var (first, last) = Span(parts[0]);
        if (first >= 0)
            yield return (first, last, parts[1]);
    }
}

/// <summary>The ranges named on the file's @missing lines, which live inside comments.</summary>
static IEnumerable<(int First, int Last, string Value)> Defaults(string text)
{
    foreach (var raw in text.Split('\n'))
    {
        var idx = raw.IndexOf("@missing:", StringComparison.Ordinal);
        if (idx < 0)
            continue;

        var parts = raw[(idx + "@missing:".Length)..].Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            continue;

        var (first, last) = Span(parts[0]);
        if (first >= 0)
            yield return (first, last, parts[1]);
    }
}

static (int First, int Last) Span(string field)
{
    var halves = field.Split("..");
    if (!int.TryParse(halves[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var first))
        return (-1, -1);

    var last = first;
    if (halves.Length > 1)
        int.TryParse(halves[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out last);

    return (first, last);
}

// ----- turning them into one complete partition ----------------------------------------------------

/// <summary>
/// Every code point from 0 to 10FFFF given exactly one value, so that a lookup is one binary
/// search with no defaulting left to do at run time.
/// </summary>
static List<(int First, string Value)> Partition(
    List<(int First, int Last, string Value)> defaults,
    List<(int First, int Last, string Value)> explicitly,
    string fallback)
    => Compress(Materialize(defaults, explicitly, fallback));

/// <summary>
/// The value of every code point from 0 to 10FFFF, one entry each: the property's defaults, then
/// its explicit ranges painted over them.
/// </summary>
static string[] Materialize(
    List<(int First, int Last, string Value)> defaults,
    List<(int First, int Last, string Value)> explicitly,
    string fallback)
{
    var values = new string[Ucd.MaxCodePoint + 1];
    Array.Fill(values, fallback);

    // Defaults first, widest to narrowest, then every explicit range painted over the lot. Doing
    // it in two passes rather than sorting everything by width is the difference between "an
    // explicit range always wins" and "an explicit range wins as long as it is narrower than the
    // default it overlaps", which is true today and is not a property anyone should have to keep
    // true.
    foreach (var (first, last, value) in defaults.OrderByDescending(r => r.Last - r.First))
        Paint(values, first, last, value);

    foreach (var (first, last, value) in explicitly)
        Paint(values, first, last, value);

    return values;
}

/// <summary>Every code point's value as runs of equal value, each given by where it starts.</summary>
static List<(int First, string Value)> Compress(string[] values)
{
    var partition = new List<(int, string)> { (0, values[0]) };
    for (var cp = 1; cp <= Ucd.MaxCodePoint; cp++)
    {
        if (!string.Equals(values[cp], values[cp - 1], StringComparison.Ordinal))
            partition.Add((cp, values[cp]));
    }

    return partition;
}

static void Paint(string[] values, int first, int last, string value)
{
    for (var cp = first; cp <= last && cp <= Ucd.MaxCodePoint; cp++)
        values[cp] = value;
}

static List<string> OrderedValues(
    List<(int First, string Value)> partition, Dictionary<string, string> aliases)
{
    var names = partition.Select(entry => entry.Value).Distinct().ToList();

    // Every value the property defines, not only the ones some character happens to have, so that
    // the enum does not change shape because a Unicode version assigned nothing to a script.
    foreach (var known in aliases.Keys)
    {
        if (!names.Contains(known))
            names.Add(known);
    }

    names.Sort(StringComparer.Ordinal);
    return names;
}

// ----- emitting ------------------------------------------------------------------------------------

static string Emit(string version, string enumName, string storage, string summary,
    List<string> names, List<(int First, string Value)> partition,
    Dictionary<string, string> aliases, bool scriptCodes)
{
    var index = names.Select((name, idx) => (name, idx))
        .ToDictionary(pair => pair.name, pair => pair.idx, StringComparer.Ordinal);

    var sb = new StringBuilder();
    sb.Append($@"// <auto-generated>
//   Generated from the Unicode Character Database {version} by tools/UnicodeTableGenerator.
//   Do not edit by hand. To regenerate after a Unicode version bump:
//
//       dotnet run --project tools/UnicodeTableGenerator -- --version {version} --out src/PdfPinata/Text
//
//   The tool is deliberately outside PdfPinata.slnx: it runs by hand, and what it writes is
//   checked in so that the build needs no network and a version bump is a reviewable diff.
// </auto-generated>

namespace PdfPinata.Text;

/// <summary>
/// {summary}
/// </summary>
public enum {enumName} : {storage}
{{
");

    foreach (var name in names)
    {
        var member = MemberName(name, aliases, scriptCodes);
        var comment = member == name ? name : $"{name} ({aliases.GetValueOrDefault(name, "?")})";
        sb.Append($"    /// <summary>{comment}</summary>\r\n");
        sb.Append($"    {member} = {index[name]},\r\n\r\n");
    }

    sb.Length -= 2;
    sb.Append("}\r\n\r\n");

    sb.Append(@"static partial class UnicodeTables
{
");

    // Starts alone: the partition is complete, so a range ends where the next one begins and
    // storing both would be storing the same numbers twice.
    sb.Append($"    /// <summary>\r\n");
    sb.Append($"    /// The first code point of each run of equal {enumName}, ascending. A complete\r\n");
    sb.Append($"    /// partition of U+0000..U+10FFFF, defaults included, so every code point is in\r\n");
    sb.Append($"    /// exactly one run and a lookup is one binary search.\r\n");
    sb.Append($"    /// </summary>\r\n");
    sb.Append($"    internal static readonly int[] {enumName}RangeStart =\r\n    {{\r\n");
    sb.Append(Wrap(partition.Select(entry => "0x" + entry.First.ToString("X", CultureInfo.InvariantCulture))));
    sb.Append("    };\r\n\r\n");

    sb.Append($"    /// <summary>The value of each run of <see cref=\"{enumName}RangeStart\"/>.</summary>\r\n");
    sb.Append($"    internal static readonly {storage}[] {enumName}RangeValue =\r\n    {{\r\n");
    sb.Append(Wrap(partition.Select(entry => index[entry.Value].ToString(CultureInfo.InvariantCulture))));
    sb.Append("    };\r\n");

    if (scriptCodes)
    {
        sb.Append($"\r\n    /// <summary>\r\n");
        sb.Append($"    /// The ISO 15924 code of each <see cref=\"{enumName}\"/>, lowercased - the form a\r\n");
        sb.Append($"    /// shaper is told a run's script in.\r\n");
        sb.Append($"    /// </summary>\r\n");
        sb.Append($"    internal static readonly string[] {enumName}Code =\r\n    {{\r\n");
        sb.Append(Wrap(names.Select(name =>
            "\"" + aliases.GetValueOrDefault(name, "Zzzz").ToLowerInvariant() + "\"")));
        sb.Append("    };\r\n");
    }

    sb.Append("}\r\n");
    return sb.ToString();
}

/// <summary>
/// The properties besides Line_Break that UAX #14's rules read, as one partition of flags. The
/// enum is emitted here, beside the table, so that the bit each flag stands for is written down in
/// exactly one place: the loop above that sets them.
/// </summary>
static string EmitLineBreakFlags(string version, List<(int First, string Value)> partition)
{
    var sb = new StringBuilder();
    sb.Append($@"// <auto-generated>
//   Generated from the Unicode Character Database {version} by tools/UnicodeTableGenerator.
//   Do not edit by hand. See BidiClass.g.cs for how to regenerate.
// </auto-generated>

using System;

namespace PdfPinata.Text;

/// <summary>
/// What UAX #14's rules read of a character besides its Line_Break class. Each flag is recorded
/// only where a rule reads it, so a final quotation mark is <see cref=""FinalQuote""/> only if it is
/// also <see cref=""LineBreakClass.QU""/>.
/// </summary>
[Flags]
internal enum LineBreakFlags : byte
{{
    /// <summary>Nothing a rule reads.</summary>
    None = 0,

    /// <summary>
    /// East_Asian_Width Fullwidth, Wide or Halfwidth - the $EastAsian of rules LB19a and LB30.
    /// </summary>
    EastAsian = 1,

    /// <summary>A QU that is General_Category Pi, for rules LB15a and LB19.</summary>
    InitialQuote = 2,

    /// <summary>A QU that is General_Category Pf, for rules LB15b and LB19.</summary>
    FinalQuote = 4,

    /// <summary>An SA that is General_Category Mn or Mc, which rule LB1 resolves to CM.</summary>
    SouthEastAsianMark = 8,

    /// <summary>Extended_Pictographic and General_Category Cn, for rule LB30b.</summary>
    UnassignedPictographic = 16,
}}

static partial class UnicodeTables
{{
    /// <summary>
    /// The first code point of each run of equal <see cref=""LineBreakFlags""/>, ascending. A
    /// complete partition of U+0000..U+10FFFF.
    /// </summary>
    internal static readonly int[] LineBreakFlagsRangeStart =
    {{
");
    sb.Append(Wrap(partition.Select(entry => "0x" + entry.First.ToString("X", CultureInfo.InvariantCulture))));
    sb.Append(@"    };

    /// <summary>The value of each run of <see cref=""LineBreakFlagsRangeStart""/>.</summary>
    internal static readonly byte[] LineBreakFlagsRangeValue =
    {
");
    sb.Append(Wrap(partition.Select(entry => entry.Value)));
    sb.Append("    };\r\n}\r\n");
    return sb.ToString();
}

static string MemberName(string name, Dictionary<string, string> aliases, bool scriptCodes)
{
    // Bidi classes are referred to by their abbreviations throughout UAX #9 - the rules are
    // written as "W2. Search backward ... until the first strong type (R, L, AL, or sos)" - so the
    // algorithm reads like the specification only if the enum does too. Scripts are the other way
    // round: nobody calls Arabic "Arab" in prose, and the code is carried separately.
    return scriptCodes ? name : aliases.GetValueOrDefault(name, name);
}

static string Wrap(IEnumerable<string> items)
{
    var sb = new StringBuilder();
    var line = new StringBuilder("        ");
    foreach (var item in items)
    {
        if (line.Length + item.Length + 2 > 100)
        {
            sb.Append(line.ToString().TrimEnd()).Append("\r\n");
            line = new StringBuilder("        ");
        }
        line.Append(item).Append(", ");
    }

    if (line.ToString().Trim().Length > 0)
        sb.Append(line.ToString().TrimEnd()).Append("\r\n");

    return sb.ToString();
}

static void Write(string path, string text)
{
    // Normalised on the way out. Some of the emitted text comes from verbatim string literals in
    // this file, so without this the generated tables would carry whatever line endings this
    // source happened to be checked out with - and regenerating on a differently configured
    // machine would produce a diff of the whole file and no change of substance.
    text = text.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

    File.WriteAllText(path, text, new UTF8Encoding(false));
    Console.WriteLine($"wrote {path} ({new FileInfo(path).Length} bytes)");
}

/// <summary>
/// A top-level local cannot be reached from a static local function, so the one constant the
/// whole program shares lives here.
/// </summary>
static class Ucd
{
    public const int MaxCodePoint = 0x10FFFF;
}
