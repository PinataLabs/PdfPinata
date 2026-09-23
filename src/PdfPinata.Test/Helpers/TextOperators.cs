using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Reads the text operators back out of a page's content stream, for the tests that care about
///   what the renderer wrote rather than about where the result lands on the page.
/// </summary>
/// <remarks>
///   The text state operators - Tc, Tw, Tz - are not observable by rasterizing a page and looking
///   at it, at least not without measuring glyph positions to a precision Ghostscript does not
///   promise. Reading them out of the content is exact, and says which of them were written at
///   all, which is half of what these tests are about.
/// </remarks>
internal static class TextOperators
{
    /// <summary>
    ///   The number every occurrence of a one-operand operator was given, in the order they were
    ///   written. Tc, Tw and Tz each take exactly one.
    /// </summary>
    internal static IReadOnlyList<double> NumbersGivenTo(PdfPage page, OpCodeName opCode)
    {
        return [..Operators(page)
            .Where(op => op.OpCode.OpCodeName == opCode && op.Operands.Count == 1)
            .Select(op => Number(op.Operands[0]))];
    }

    /// <summary>
    ///   The strings shown on the page, in the order they were drawn.
    /// </summary>
    /// <remarks>
    ///   Readable only for a font encoded as WinAnsi, which writes its text as a string literal.
    ///   A font embedded as Identity-H writes glyph identifiers instead, and what comes back is
    ///   the numbers of the glyphs rather than the characters they stand for.
    /// </remarks>
    internal static IReadOnlyList<string> ShownStrings(PdfPage page)
    {
        return [..Operators(page)
            .Where(op => op.OpCode.OpCodeName is OpCodeName.Tj or OpCodeName.TJ)
            .SelectMany(op => ItemsOf(op.Operands)
                .SelectMany(operand => operand is CArray array ? ItemsOf(array) : [operand])
                .OfType<CString>())
            .Select(text => text.Value)];
    }

    /// <summary>
    ///   Every operand list an operator was given, in the order they were written.
    /// </summary>
    internal static IReadOnlyList<double[]> OperandsGivenTo(PdfPage page, OpCodeName opCode)
    {
        return [..Operators(page)
            .Where(op => op.OpCode.OpCodeName == opCode)
            .Select(op => ItemsOf(op.Operands).Select(Number).ToArray())];
    }

    /// <summary>
    ///   How far each Tm on the page leans - its M21 component - in the order they were written.
    ///   Zero for an upright text matrix.
    /// </summary>
    internal static IReadOnlyList<double> TextMatrixSkews(PdfPage page)
    {
        return [..Operators(page)
            .Where(op => op.OpCode.OpCodeName == OpCodeName.Tm && op.Operands.Count == 6)
            .Select(op => Number(op.Operands[2]))];
    }

    /// <summary>
    ///   The offset each Td on the page was given, in order.
    /// </summary>
    internal static IReadOnlyList<(double X, double Y)> TdOffsets(PdfPage page)
    {
        return [..Operators(page)
            .Where(op => op.OpCode.OpCodeName == OpCodeName.Td && op.Operands.Count == 2)
            .Select(op => (Number(op.Operands[0]), Number(op.Operands[1])))];
    }

    /// <summary>
    ///   How many times an operator appears on the page.
    /// </summary>
    internal static int CountOf(PdfPage page, OpCodeName opCode)
    {
        return Operators(page).Count(op => op.OpCode.OpCodeName == opCode);
    }

    /// <summary>
    ///   The show-text operators used, in order: Tj for a run drawn in one go, TJ for one whose
    ///   parts are moved apart individually.
    /// </summary>
    internal static IReadOnlyList<OpCodeName> ShowTextOperators(PdfPage page)
    {
        return [..Operators(page)
            .Select(op => op.OpCode.OpCodeName)
            .Where(name => name is OpCodeName.Tj or OpCodeName.TJ)];
    }

    /// <summary>
    ///   The numbers inside every TJ array on the page - the amounts the pen is moved by between
    ///   the runs of glyphs, in negated thousandths of the font size.
    /// </summary>
    internal static IReadOnlyList<double> TjAdjustments(PdfPage page)
    {
        return [..TjArrays(page)
            .SelectMany(array => ItemsOf(array).OfType<CNumber>())
            .Select(Number)];
    }

    /// <summary>
    ///   How many separate runs of glyphs each TJ array on the page is broken into.
    /// </summary>
    internal static IReadOnlyList<int> TjRunCounts(PdfPage page)
    {
        return [..TjArrays(page)
            .Select(array => ItemsOf(array).OfType<CString>().Count())];
    }

    /// <summary>
    ///   The strings shown on the page, in the order a reader sees them: down the page, and left to
    ///   right along a line.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Not the same list as <see cref="ShownStrings"/>, which is in the order the renderer wrote
    ///     them. The two differ exactly when something is drawn somewhere other than after the thing
    ///     before it - which is what laying a right-to-left line out in the order it is read amounts
    ///     to, and is the whole of what a test of that wants to see.
    ///   </para>
    ///   <para>
    ///     The positions it is sorted by come from <see cref="Placed"/>; a test that needs them
    ///     rather than the order they produce wants <see cref="ShownWithPositions"/>.
    ///   </para>
    /// </remarks>
    internal static IReadOnlyList<string> ShownAcrossThePage(PdfPage page)
    {
        return [..Placed(page)
            .OrderByDescending(run => run.Y)
            .ThenBy(run => run.X)
            .ThenBy(run => run.Written)
            .Select(run => run.Text)];
    }

    /// <summary>
    ///   What each show-text operator drew and the position it drew it at, in the order they were
    ///   written.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     For a test that has to say <em>where</em> something landed rather than only in what
    ///     order. Positions are as PDF measures them, so a bigger Y is further up the page.
    ///   </para>
    ///   <para>
    ///     One entry per operator, not per string: a <c>TJ</c> array is a single run drawn at a
    ///     single origin, and the numbers between its strings are kerning inside that run rather
    ///     than placements of their own. So its strings come back joined.
    ///   </para>
    ///   <para>
    ///     <b>Throws</b> rather than answering for a page it cannot place - see
    ///     <see cref="Placed"/> for which pages those are. Every position it does answer with is
    ///     exact.
    ///   </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   Something on the page was shown at a position that cannot be known without the font's
    ///   glyph widths.
    /// </exception>
    internal static IReadOnlyList<(double X, double Y, string Text)> ShownWithPositions(PdfPage page)
    {
        var placed = Placed(page);

        // Refused rather than approximated. A test asking where something is and quietly given the
        // position of the thing before it is worse than one that stops: it passes, and it goes on
        // passing when the thing moves.
        if (placed.Any(run => !run.Exact))
        {
            throw new InvalidOperationException(
                "This page shows text at a position that cannot be worked out from the content "
                + "stream alone: a show-text operator follows another with nothing repositioning "
                + "the pen in between, so where the second one starts is the width of the first, "
                + "which only the font's glyph widths can say. The renderer writes that shape when "
                + "one string is drawn from more than one face - see FallenBackTextOperators. Use "
                + "ShownStrings or ShownAcrossThePage, which need no position, or measure the run.");
        }

        return [..placed.Select(run => (run.X, run.Y, run.Text))];
    }

    /// <summary>
    ///   What each show-text operator drew, with the pen position it was drawn at and whether that
    ///   position is known.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The position is accumulated from the text-positioning operators: <c>Tm</c> sets it
    ///     outright and <c>Td</c> moves it, both relative to the start of the text object, which is
    ///     enough to place what is on one page against itself.
    ///   </para>
    ///   <para>
    ///     <b>Showing text also moves the pen</b>, by the width of what was shown - and that width
    ///     is a sum of glyph advances, which lives in the embedded font rather than in the content
    ///     stream. So the first operator after a <c>BT</c>, <c>Td</c>, <c>TD</c> or <c>Tm</c> has a
    ///     position this can give exactly, and anything shown after it before the next one does
    ///     not. Working the width out here would mean reading the font's own metrics and adding up
    ///     advances, character spacing, word spacing and horizontal scaling - which is to say
    ///     reimplementing the renderer inside the helper that is supposed to check it, where a
    ///     shared mistake would cancel itself out. It reports what it knows instead.
    ///   </para>
    ///   <para>
    ///     <c>T*</c> and the two quote operators move the pen by the text leading, which is not
    ///     tracked either. This library's renderer emits none of them, so rather than carry a third
    ///     case they are refused outright.
    ///   </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   The page uses a text operator whose effect on the pen this does not follow.
    /// </exception>
    private static List<(double X, double Y, int Written, string Text, bool Exact)> Placed(PdfPage page)
    {
        var shown = new List<(double X, double Y, int Written, string Text, bool Exact)>();
        double x = 0, y = 0;

        // Whether the pen is where the positioning operators alone say it is.
        var exact = true;

        foreach (var op in Operators(page))
        {
            var operands = ItemsOf(op.Operands);
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.BT:
                    x = y = 0;
                    exact = true;
                    break;

                case OpCodeName.Tm when operands.Count == 6:
                    x = Number(operands[4]);
                    y = Number(operands[5]);
                    exact = true;
                    break;

                case OpCodeName.Td when operands.Count == 2:
                case OpCodeName.TD when operands.Count == 2:
                    x += Number(operands[0]);
                    y += Number(operands[1]);
                    exact = true;
                    break;

                case OpCodeName.Tj:
                case OpCodeName.TJ:
                    // One entry for the operator, not one per string: the numbers between a TJ
                    // array's strings move the glyphs within the run rather than placing pieces of
                    // it separately, and the run began at the pen.
                    shown.Add((x, y, shown.Count, string.Concat(operands
                        .SelectMany(o => o is CArray array ? ItemsOf(array) : [o])
                        .OfType<CString>()
                        .Select(text => text.Value)), exact));

                    // The pen has moved on by the width of what was just drawn, and that width is
                    // not in the content stream.
                    exact = false;
                    break;

                case OpCodeName.Tx:
                case OpCodeName.QuoteSingle:
                case OpCodeName.QuoteDbl:
                    // These move to the next line by the leading, and the two quote operators show
                    // a string as well - which this would then both misplace and leave out, because
                    // only Tj and TJ are collected. Refused outright rather than answered for:
                    // nothing in this library writes them, so a page carrying one did not come from
                    // the renderer and no assertion here should be built on a guess about it.
                    throw new InvalidOperationException(
                        $"The page uses the {op.OpCode.Name} operator, which moves the pen by the "
                        + "text leading. Positions and reading order here are accumulated from BT, "
                        + "Td, TD and Tm alone, so neither can be given for this page.");
            }
        }

        return shown;
    }

    private static IEnumerable<CArray> TjArrays(PdfPage page)
    {
        return Operators(page)
            .Where(op => op.OpCode.OpCodeName == OpCodeName.TJ)
            .SelectMany(op => ItemsOf(op.Operands).OfType<CArray>());
    }

    private static IEnumerable<COperator> Operators(PdfPage page)
    {
        return ItemsOf(ContentReader.ReadContent(PageContent.Of(page))).OfType<COperator>();
    }

    /// <summary>
    ///   The items of a sequence as a plain list.
    /// </summary>
    /// <remarks>
    ///   CSequence implements IList&lt;CObject&gt;, but its explicit
    ///   IEnumerable&lt;CObject&gt;.GetEnumerator throws NotImplementedException, so LINQ - which
    ///   asks for exactly that one - cannot be pointed at a sequence directly. Its public
    ///   GetEnumerator, the one foreach binds to, works.
    /// </remarks>
    private static IReadOnlyList<CObject> ItemsOf(CSequence sequence)
    {
        var items = new List<CObject>();
        foreach (var item in sequence)
            items.Add(item);
        return items;
    }

    private static double Number(CObject operand)
    {
        return operand switch
        {
            CInteger integer => integer.Value,
            CReal real => real.Value,
            _ => 0.0
        };
    }
}
