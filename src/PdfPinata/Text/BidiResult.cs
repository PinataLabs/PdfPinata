using System;
using System.Collections.Generic;
using PdfPinata.Fonts;

namespace PdfPinata.Text;

/// <summary>
/// What the bidirectional algorithm made of a paragraph: what level every character ended up at,
/// and what order they are drawn in.
/// </summary>
public sealed class BidiResult
{
    internal BidiResult(byte paragraphLevel, byte[] levels, bool[] removed, int[] visualOrder,
        bool[] joining)
    {
        ParagraphLevel = paragraphLevel;
        Levels = levels;
        Removed = removed;
        VisualOrder = visualOrder;
        _joining = joining;
    }

    // Which of the removed characters are joining controls, and so belong inside the run they sit
    // in even though nothing is drawn for them. See UnicodeProperties.IsJoiningControl.
    readonly bool[] _joining;

    /// <summary>
    /// <see cref="_joining"/>, for the one caller that has to carry it from a result indexed by
    /// code point to one indexed by code unit.
    /// </summary>
    internal IReadOnlyList<bool> Joining => _joining;

    /// <summary>
    /// The level the paragraph as a whole runs at: even for left to right, odd for right to left.
    /// Either what the caller asked for, or what rules P2 and P3 read out of the text.
    /// </summary>
    public byte ParagraphLevel { get; }

    /// <summary>
    /// The embedding level of each code point, in the order they were written. Even levels run
    /// left to right and odd levels right to left, and the number is how deeply nested the run is.
    /// </summary>
    /// <remarks>
    /// A character whose <see cref="Removed"/> flag is set has a level here too, but it is a
    /// leftover rather than an answer: the algorithm takes those characters out before it resolves
    /// anything, and nothing should be drawn for them.
    /// </remarks>
    public IReadOnlyList<byte> Levels { get; }

    /// <summary>
    /// Whether each code point was removed by rule X9 - the explicit embedding and override
    /// controls, and anything else of class <see cref="BidiClass.BN"/>. They take part in no rule
    /// after X9 and appear nowhere in <see cref="VisualOrder"/>.
    /// </summary>
    public IReadOnlyList<bool> Removed { get; }

    /// <summary>
    /// The indices of the code points in the order they are drawn, left to right. Removed
    /// characters are not in it, so this is shorter than <see cref="Levels"/> whenever the text
    /// carried any control characters.
    /// </summary>
    public IReadOnlyList<int> VisualOrder { get; }

    /// <summary>
    /// Whether the paragraph as a whole runs right to left.
    /// </summary>
    public bool IsRightToLeft => (ParagraphLevel & 1) != 0;

    /// <summary>
    /// The runs of the paragraph in visual order, each one a stretch of text at a single level and
    /// so a single direction - which is the form a shaper wants them in.
    /// </summary>
    /// <remarks>
    /// A run covers the characters between its ends, which is not quite the same as the characters
    /// in <see cref="VisualOrder"/>: a joining control is removed by rule X9 and so appears in no
    /// visual order, but it sits inside the run it was written in and the run reaches over it. That
    /// is deliberate and it is what the control is for - <c>U+0627 U+200D U+0628</c> is one Arabic
    /// word asking for its joined forms, and cutting it into two runs at the joiner both loses the
    /// instruction and stops the shaper seeing the two letters together. Every other removed
    /// character ends the run, because the rest of them change direction and a run is one direction
    /// by definition.
    /// </remarks>
    public IReadOnlyList<BidiRun> Runs()
    {
        var runs = new List<BidiRun>();
        var order = VisualOrder;

        for (var idx = 0; idx < order.Count;)
        {
            var level = Levels[order[idx]];
            var start = idx;

            // A run is as long as the level holds and the indices keep stepping the way the level
            // says they should - +1 for an even level, -1 for an odd one. Anything else is a new
            // run, however equal the levels happen to be.
            var step = (level & 1) == 0 ? 1 : -1;
            while (idx + 1 < order.Count
                   && Levels[order[idx + 1]] == level
                   && order[idx + 1] == Next(order[idx], step))
            {
                idx++;
            }

            var first = Math.Min(order[start], order[idx]);
            var last = Math.Max(order[start], order[idx]);
            runs.Add(new BidiRun(first, last - first + 1, level));
            idx++;
        }

        return runs;
    }

    /// <summary>
    /// The next index a run may reach to from <paramref name="from"/>: one step on, and on again
    /// over any joining control, because those are inside runs rather than between them.
    /// </summary>
    int Next(int from, int step)
    {
        var at = from + step;
        while (at >= 0 && at < _joining.Length && _joining[at])
            at += step;

        return at;
    }
}

/// <summary>
/// One stretch of a paragraph at a single embedding level, and so running in a single direction.
/// </summary>
public readonly struct BidiRun
{
    internal BidiRun(int start, int length, byte level)
    {
        Start = start;
        Length = length;
        Level = level;
    }

    /// <summary>The index of the first code point of the run, in logical order.</summary>
    public int Start { get; }

    /// <summary>How many code points the run covers.</summary>
    public int Length { get; }

    /// <summary>The embedding level: even runs left to right, odd runs right to left.</summary>
    public byte Level { get; }

    /// <summary>Which way the run is written, which is the parity of its level.</summary>
    public XTextDirection Direction
        => (Level & 1) == 0 ? XTextDirection.LeftToRight : XTextDirection.RightToLeft;

    /// <inheritdoc/>
    public override string ToString() => $"[{Start}..{Start + Length - 1}] level {Level}";
}
