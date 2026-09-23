using System;
using System.Globalization;
using System.Text;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf;

/// <summary>
/// The way the numeric part of a page label is written.
/// </summary>
public enum PdfPageLabelStyle
{
    /// <summary>No numeric part. The label is the prefix alone.</summary>
    None,

    /// <summary>Decimal numerals: 1, 2, 3.</summary>
    Decimal,

    /// <summary>Uppercase roman numerals: I, II, III.</summary>
    UppercaseRoman,

    /// <summary>Lowercase roman numerals: i, ii, iii.</summary>
    LowercaseRoman,

    /// <summary>Uppercase letters: A to Z, then AA to ZZ, and so on.</summary>
    UppercaseLetters,

    /// <summary>Lowercase letters: a to z, then aa to zz, and so on.</summary>
    LowercaseLetters
}

/// <summary>
/// One run of pages labelled the same way: where it starts, how its pages are numbered, and
/// what is put in front of the number.
/// </summary>
public sealed class PdfPageLabelRange
{
    internal PdfPageLabelRange(int startPageIndex, PdfPageLabelStyle style, string prefix, int start)
    {
        StartPageIndex = startPageIndex;
        Style = style;
        Prefix = prefix;
        Start = start;
    }

    /// <summary>The index of the first page this range covers, counting from zero.</summary>
    public int StartPageIndex { get; private set; }

    /// <summary>The way the numeric part of the label is written.</summary>
    public PdfPageLabelStyle Style { get; private set; }

    /// <summary>What is put in front of the number, or null where nothing is.</summary>
    public string Prefix { get; private set; }

    /// <summary>The number given to the first page of the range.</summary>
    public int Start { get; private set; }
}

/// <summary>
/// The page labels of a document: what a reader shows for a page instead of its position, so
/// that front matter can be numbered i, ii, iii while the body starts again at 1.
/// <para>
/// Labels are given a range at a time, each one starting at the page it is added for and
/// running until the next range begins. A document with labels is asked to label the page at
/// index zero, so a range added further in brings one there with it, numbering those earlier
/// pages from one as a reader would have shown them anyway. Adding a range at page zero
/// puts the caller's own in its place.
/// </para>
/// </summary>
public sealed class PdfPageLabels
{
    internal PdfPageLabels(PdfDocument document)
    {
        _document = document;
    }

    private readonly PdfDocument _document;

    /// <summary>
    /// The tree the labels are held in, or null where the document has none and none is being
    /// added. Reading a document that has no labels does not give it any.
    /// </summary>
    private PdfNumberTreeNode Tree(bool create)
    {
        PdfDictionary catalog = _document.Internals.Catalog;
        var value = catalog.Elements.GetValue(PdfCatalog.Keys.PageLabels,
            create ? VCF.CreateIndirect : VCF.None);
        return value as PdfNumberTreeNode;
    }

    /// <summary>
    /// The number of ranges the document is labelled in.
    /// </summary>
    public int Count
    {
        get
        {
            var tree = Tree(false);
            return tree == null ? 0 : tree.Count;
        }
    }

    /// <summary>
    /// The page each range starts at, least first.
    /// </summary>
    public int[] GetRangeStarts()
    {
        var tree = Tree(false);
        return tree == null ? [] : tree.GetKeys();
    }

    /// <summary>
    /// Labels the pages from the given one until the next range begins.
    /// </summary>
    /// <param name="startPageIndex">The first page of the range, counting from zero.</param>
    /// <param name="style">How the numeric part of the label is written.</param>
    /// <param name="prefix">What is put in front of the number. May be null.</param>
    /// <param name="start">The number given to the first page of the range.</param>
    public void Add(int startPageIndex, PdfPageLabelStyle style, string prefix, int start)
    {
        if (startPageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startPageIndex), "A page index is not negative.");

        if (start < 1)
            throw new ArgumentOutOfRangeException(nameof(start), "A page label is numbered from one.");

        Write(startPageIndex, style, prefix, start);
        LabelPageZero();
    }

    private void Write(int startPageIndex, PdfPageLabelStyle style, string prefix, int start)
    {
        var label = new PdfDictionary(_document);
        label.Elements.SetName(PdfPageLabelKeys.Type, "/PageLabel");

        var name = NameOf(style);
        if (name != null)
            label.Elements.SetName(PdfPageLabelKeys.Style, name);

        if (!string.IsNullOrEmpty(prefix))
            label.Elements.SetString(PdfPageLabelKeys.Prefix, prefix);

        // One is the default, and is left out rather than written.
        if (start != 1)
            label.Elements.SetInteger(PdfPageLabelKeys.Start, start);

        _document.Internals.AddObject(label);
        Tree(true).SetValue(startPageIndex, label);
    }

    /// <summary>
    /// A document with page labels is asked to label the page at index zero, and a document
    /// that does not is one a reader is left to guess about. So where the ranges do not reach
    /// that far, a range numbering the pages from one is put there.
    /// <para>
    /// That is what a reader shows for a page with no label anyway, so the pages before the
    /// first range the caller asked for are labelled the way they already looked, and the
    /// document says plainly what it would otherwise leave unsaid. A range the caller adds at
    /// page zero later takes its place.
    /// </para>
    /// </summary>
    private void LabelPageZero()
    {
        var tree = Tree(false);
        if (tree == null || tree.Count == 0 || tree.Contains(0))
            return;

        Write(0, PdfPageLabelStyle.Decimal, null, 1);
    }

    /// <summary>
    /// Labels the pages from the given one, numbering them from one with no prefix.
    /// </summary>
    public void Add(int startPageIndex, PdfPageLabelStyle style)
    {
        Add(startPageIndex, style, null, 1);
    }

    /// <summary>
    /// Takes away the range starting at the given page, and says whether there was one.
    /// </summary>
    public bool Remove(int startPageIndex)
    {
        var tree = Tree(false);
        if (tree == null || !tree.Remove(startPageIndex))
            return false;

        // A tree left holding nothing would say the document has labels and then label no
        // page, which is not a document the standard describes. Taking the last range away
        // takes the entry with it.
        if (tree.Count == 0)
            Clear();
        else
            LabelPageZero();

        return true;
    }

    /// <summary>
    /// Takes away every range, leaving the document labelled by position again.
    /// </summary>
    public void Clear()
    {
        _document.Internals.Catalog.Elements.Remove(PdfCatalog.Keys.PageLabels);
    }

    /// <summary>
    /// The range covering the given page, or null where the document has no labels or none
    /// that reaches it.
    /// </summary>
    public PdfPageLabelRange GetRange(int pageIndex)
    {
        var tree = Tree(false);
        if (tree == null)
            return null;

        // The range a page falls in is the last one starting at or before it.
        var start = -1;
        foreach (var candidate in tree.GetKeys())
        {
            if (candidate > pageIndex)
                break;

            start = candidate;
        }

        if (start < 0)
            return null;

        var label = tree.GetDictionary(start);
        if (label == null)
            return null;

        return new PdfPageLabelRange(start,
            StyleOf(label.Elements.GetName(PdfPageLabelKeys.Style)),
            label.Elements.ContainsKey(PdfPageLabelKeys.Prefix)
                ? label.Elements.GetString(PdfPageLabelKeys.Prefix)
                : null,
            label.Elements.ContainsKey(PdfPageLabelKeys.Start)
                ? label.Elements.GetInteger(PdfPageLabelKeys.Start)
                : 1);
    }

    /// <summary>
    /// The label a reader shows for the given page, or null where the document has none for
    /// it and the reader would show its position instead.
    /// </summary>
    public string GetLabel(int pageIndex)
    {
        var range = GetRange(pageIndex);
        if (range == null)
            return null;

        var number = range.Start + (pageIndex - range.StartPageIndex);
        return (range.Prefix ?? "") + Numeral(number, range.Style);
    }

    #region Writing a number the way a style asks for

    internal static string Numeral(int number, PdfPageLabelStyle style)
    {
        return style switch
        {
            PdfPageLabelStyle.Decimal => number.ToString(CultureInfo.InvariantCulture),
            PdfPageLabelStyle.UppercaseRoman => Roman(number),
            PdfPageLabelStyle.LowercaseRoman => Roman(number).ToLowerInvariant(),
            PdfPageLabelStyle.UppercaseLetters => Letters(number, 'A'),
            PdfPageLabelStyle.LowercaseLetters => Letters(number, 'a'),
            // No numeric part at all: the label is the prefix alone.
            _ => ""
        };
    }

    private static readonly int[] RomanValues = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];
    private static readonly string[] RomanNumerals =
        ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];

    private static string Roman(int number)
    {
        if (number < 1)
            return "";

        var numeral = new StringBuilder();
        for (var at = 0; at < RomanValues.Length; at++)
        {
            while (number >= RomanValues[at])
            {
                numeral.Append(RomanNumerals[at]);
                number -= RomanValues[at];
            }
        }
        return numeral.ToString();
    }

    /// <summary>
    /// A to Z for the first twenty-six, then AA to ZZ, then AAA to ZZZ. The letter repeats
    /// rather than counting up as a number in base twenty-six would.
    /// </summary>
    private static string Letters(int number, char first)
    {
        if (number < 1)
            return "";

        var index = (number - 1) % 26;
        var repeats = (number - 1) / 26 + 1;
        return new string((char)(first + index), repeats);
    }

    #endregion

    private static string NameOf(PdfPageLabelStyle style)
    {
        return style switch
        {
            PdfPageLabelStyle.Decimal => "/D",
            PdfPageLabelStyle.UppercaseRoman => "/R",
            PdfPageLabelStyle.LowercaseRoman => "/r",
            PdfPageLabelStyle.UppercaseLetters => "/A",
            PdfPageLabelStyle.LowercaseLetters => "/a",
            _ => null
        };
    }

    private static PdfPageLabelStyle StyleOf(string name)
    {
        return name switch
        {
            "/D" => PdfPageLabelStyle.Decimal,
            "/R" => PdfPageLabelStyle.UppercaseRoman,
            "/r" => PdfPageLabelStyle.LowercaseRoman,
            "/A" => PdfPageLabelStyle.UppercaseLetters,
            "/a" => PdfPageLabelStyle.LowercaseLetters,
            _ => PdfPageLabelStyle.None
        };
    }

    /// <summary>
    /// The keys of a page label dictionary.
    /// </summary>
    private static class PdfPageLabelKeys
    {
        internal const string Type = "/Type";
        internal const string Style = "/S";
        internal const string Prefix = "/P";
        internal const string Start = "/St";
    }
}
