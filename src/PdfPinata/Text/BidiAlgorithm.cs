using System;
using System.Collections.Generic;

namespace PdfPinata.Text;

/// <summary>
/// The Unicode Bidirectional Algorithm, UAX #9: works out what order the characters of a paragraph
/// are drawn in when some of them run right to left.
/// </summary>
/// <remarks>
/// <para>
/// Pure text processing. It touches no font, no image and no backend, which is why it is in the
/// core package rather than behind the shaping seam - a caller who only wants to know which way
/// a string runs should not have to install a shaper to find out.
/// </para>
/// <para>
/// The rule names in the comments below - P2, X5a, W7, N0, L1 - are UAX #9's own, and the method
/// names follow them. That is worth more than prettier names: the specification is the only
/// documentation this algorithm has, every implementation of it is discussed in those terms, and
/// the conformance suite reports failures against them.
/// </para>
/// </remarks>
public static partial class BidiAlgorithm
{
    /// <summary>
    /// The deepest an embedding may nest, per BD2. Beyond it the algorithm counts overflows rather
    /// than pushing, and the extra controls have no effect at all.
    /// </summary>
    private const int MaxDepth = 125;

    /// <summary>
    /// Resolves one paragraph of text given as code points.
    /// </summary>
    /// <param name="codePoints">
    /// The paragraph, one entry per Unicode code point rather than per UTF-16 code unit. Use
    /// <see cref="Resolve(string,BidiParagraphDirection)"/> for a .NET string.
    /// </param>
    /// <param name="direction">Which way the paragraph runs, or Automatic to read it off the text.</param>
    public static BidiResult Resolve(
        IReadOnlyList<int> codePoints, BidiParagraphDirection direction = BidiParagraphDirection.Automatic)
    {
        ArgumentNullException.ThrowIfNull(codePoints);

        return new Paragraph(codePoints, direction).Resolve();
    }

    /// <summary>
    /// Resolves one paragraph of text, with one level per <see cref="char"/> of the string. Both
    /// halves of a surrogate pair carry the level of the character they spell.
    /// </summary>
    public static BidiResult Resolve(
        string text, BidiParagraphDirection direction = BidiParagraphDirection.Automatic)
    {
        ArgumentNullException.ThrowIfNull(text);

        var codePoints = new List<int>(text.Length);
        var unitsPer = new List<int>(text.Length);
        for (var idx = 0; idx < text.Length;)
        {
            if (char.IsHighSurrogate(text[idx]) && idx + 1 < text.Length
                && char.IsLowSurrogate(text[idx + 1]))
            {
                codePoints.Add(char.ConvertToUtf32(text[idx], text[idx + 1]));
                unitsPer.Add(2);
                idx += 2;
            }
            else
            {
                codePoints.Add(text[idx]);
                unitsPer.Add(1);
                idx++;
            }
        }

        var resolved = Resolve(codePoints, direction);
        if (codePoints.Count == text.Length)
            return resolved;

        return Spread(resolved, unitsPer, text.Length);
    }

    /// <summary>
    /// The same answer indexed by UTF-16 code unit rather than by code point, with a surrogate
    /// pair's two units carrying what its one character resolved to.
    /// </summary>
    private static BidiResult Spread(BidiResult resolved, List<int> unitsPer, int length)
    {
        var levels = new byte[length];
        var removed = new bool[length];
        var joining = new bool[length];
        var firstUnit = new int[unitsPer.Count];

        for (int idx = 0, unit = 0; idx < unitsPer.Count; idx++)
        {
            firstUnit[idx] = unit;
            for (var repeat = 0; repeat < unitsPer[idx]; repeat++, unit++)
            {
                levels[unit] = resolved.Levels[idx];
                removed[unit] = resolved.Removed[idx];
                joining[unit] = resolved.Joining[idx];
            }
        }

        var order = new List<int>(length);
        foreach (var idx in resolved.VisualOrder)
        {
            // A surrogate pair is drawn as one character, so its units stay in written order
            // inside the run however the run itself was reversed.
            for (var repeat = 0; repeat < unitsPer[idx]; repeat++)
                order.Add(firstUnit[idx] + repeat);
        }

        return new BidiResult(resolved.ParagraphLevel, levels, removed, order.ToArray(), joining);
    }

    /// <summary>Whether rule X9 takes this class out before anything is resolved.</summary>
    internal static bool IsRemovedByX9(BidiClass type)
        => type == BidiClass.RLE || type == BidiClass.LRE || type == BidiClass.RLO
        || type == BidiClass.LRO || type == BidiClass.PDF || type == BidiClass.BN;

    private static bool IsIsolateInitiator(BidiClass type)
        => type == BidiClass.LRI || type == BidiClass.RLI || type == BidiClass.FSI;

    /// <summary>A "neutral or isolate formatting character", as the N rules call them.</summary>
    private static bool IsNeutralOrIsolate(BidiClass type)
        => type == BidiClass.B || type == BidiClass.S || type == BidiClass.WS
        || type == BidiClass.ON || type == BidiClass.FSI || type == BidiClass.LRI
        || type == BidiClass.RLI || type == BidiClass.PDI;

    /// <summary>
    /// One paragraph being resolved. A class rather than a pile of parameters because the rules
    /// read and write the same half-dozen arrays throughout, and threading them through twenty
    /// methods would obscure what each rule actually does.
    /// </summary>
    private sealed class Paragraph
    {
        private readonly IReadOnlyList<int> _codePoints;
        private readonly BidiParagraphDirection _direction;
        private readonly int _length;

        private readonly BidiClass[] _initial;   // as the database gives them, never modified
        private readonly BidiClass[] _types;     // as the W, N and X rules leave them
        private readonly byte[] _levels;
        private readonly int[] _matchingPdi;     // for an isolate initiator, where its PDI is (or _length)
        private readonly int[] _matchingInitiator; // for a PDI, where its initiator is (or -1)

        private byte _paragraphLevel;

        internal Paragraph(IReadOnlyList<int> codePoints, BidiParagraphDirection direction)
        {
            _codePoints = codePoints;
            _direction = direction;
            _length = codePoints.Count;

            _initial = new BidiClass[_length];
            _types = new BidiClass[_length];
            _levels = new byte[_length];
            _matchingPdi = new int[_length];
            _matchingInitiator = new int[_length];

            for (var idx = 0; idx < _length; idx++)
                _initial[idx] = _types[idx] = UnicodeProperties.BidiClassOf(codePoints[idx]);
        }

        internal BidiResult Resolve()
        {
            DetermineMatchingIsolates();
            _paragraphLevel = _direction switch
            {
                BidiParagraphDirection.LeftToRight => 0,
                BidiParagraphDirection.RightToLeft => 1,
                _ => ParagraphLevelOf(0, _length)
            };

            ResolveExplicitLevels();

            foreach (var sequence in IsolatingRunSequences())
                sequence.Resolve();

            ResetWhitespaceLevels();

            var removed = new bool[_length];
            var joining = new bool[_length];
            for (var idx = 0; idx < _length; idx++)
            {
                removed[idx] = IsRemovedByX9(_initial[idx]);

                // Removed from the ordering and kept for the shaper: see BidiResult.Runs.
                joining[idx] = removed[idx] && _codePoints[idx] <= char.MaxValue
                    && UnicodeProperties.IsJoiningControl((char)_codePoints[idx]);
            }

            return new BidiResult(_paragraphLevel, _levels, removed, Reorder(removed), joining);
        }

        // ----- BD9: which PDI closes which isolate initiator --------------------------------------

        private void DetermineMatchingIsolates()
        {
            for (var idx = 0; idx < _length; idx++)
            {
                _matchingPdi[idx] = -1;
                _matchingInitiator[idx] = -1;
            }

            for (var idx = 0; idx < _length; idx++)
            {
                if (!IsIsolateInitiator(_initial[idx]))
                    continue;

                var depth = 1;
                var scan = idx + 1;
                for (; scan < _length; scan++)
                {
                    var type = _initial[scan];
                    if (IsIsolateInitiator(type))
                    {
                        depth++;
                    }
                    else if (type == BidiClass.PDI)
                    {
                        if (--depth == 0)
                        {
                            _matchingInitiator[scan] = idx;
                            break;
                        }
                    }
                }

                // An initiator with nothing to close it runs to the end of the paragraph.
                _matchingPdi[idx] = scan < _length ? scan : _length;
            }
        }

        // ----- P2, P3: the level of a paragraph, or of the inside of an FSI -----------------------

        private byte ParagraphLevelOf(int start, int end)
        {
            for (var idx = start; idx < end; idx++)
            {
                var type = _initial[idx];

                // P2 looks past the whole of an isolate: what is inside it says nothing about
                // which way the text around it runs.
                if (IsIsolateInitiator(type))
                {
                    idx = _matchingPdi[idx];
                    if (idx >= end)
                        break;
                    continue;
                }

                if (type == BidiClass.L)
                    return 0;

                if (type == BidiClass.R || type == BidiClass.AL)
                    return 1;
            }

            return 0;
        }

        // ----- X1 to X8: explicit embeddings, overrides and isolates ------------------------------

        private void ResolveExplicitLevels()
        {
            var stack = new Stack<Status>();
            stack.Push(new Status(_paragraphLevel, BidiClass.ON, false));

            int overflowIsolate = 0, overflowEmbedding = 0, validIsolate = 0;

            for (var idx = 0; idx < _length; idx++)
            {
                var type = _initial[idx];
                switch (type)
                {
                    // X2 to X5: the embeddings and overrides.
                    case BidiClass.RLE:
                    case BidiClass.LRE:
                    case BidiClass.RLO:
                    case BidiClass.LRO:
                    {
                        _levels[idx] = stack.Peek().Level;

                        var rightToLeft = type == BidiClass.RLE || type == BidiClass.RLO;
                        var next = NextLevel(stack.Peek().Level, rightToLeft);
                        var over = type == BidiClass.RLO ? BidiClass.R
                            : type == BidiClass.LRO ? BidiClass.L
                            : BidiClass.ON;

                        if (next <= MaxDepth && overflowIsolate == 0 && overflowEmbedding == 0)
                            stack.Push(new Status((byte)next, over, false));
                        else if (overflowIsolate == 0)
                            overflowEmbedding++;

                        break;
                    }

                    // X5a, X5b, X5c: the isolates. An FSI is whichever of the two the text inside
                    // it turns out to be, which is P2 and P3 applied to that stretch alone.
                    case BidiClass.RLI:
                    case BidiClass.LRI:
                    case BidiClass.FSI:
                    {
                        var rightToLeft = type == BidiClass.RLI
                                          || (type == BidiClass.FSI
                                              && ParagraphLevelOf(idx + 1, Math.Min(_matchingPdi[idx], _length)) == 1);

                        _levels[idx] = stack.Peek().Level;
                        Override(idx, stack.Peek().Override);

                        var next = NextLevel(stack.Peek().Level, rightToLeft);
                        if (next <= MaxDepth && overflowIsolate == 0 && overflowEmbedding == 0)
                        {
                            validIsolate++;
                            stack.Push(new Status((byte)next, BidiClass.ON, true));
                        }
                        else
                        {
                            overflowIsolate++;
                        }

                        break;
                    }

                    // X6a: a PDI closes the nearest valid isolate, and any embeddings opened
                    // inside it go with it.
                    case BidiClass.PDI:
                    {
                        if (overflowIsolate > 0)
                        {
                            overflowIsolate--;
                        }
                        else if (validIsolate > 0)
                        {
                            overflowEmbedding = 0;
                            while (!stack.Peek().Isolate)
                                stack.Pop();

                            stack.Pop();
                            validIsolate--;
                        }

                        _levels[idx] = stack.Peek().Level;
                        Override(idx, stack.Peek().Override);
                        break;
                    }

                    // X7: a PDF closes the nearest embedding, but never reaches past an isolate.
                    case BidiClass.PDF:
                    {
                        _levels[idx] = stack.Peek().Level;

                        if (overflowIsolate > 0)
                        {
                            // Nothing: the isolate it is inside never opened.
                        }
                        else if (overflowEmbedding > 0)
                        {
                            overflowEmbedding--;
                        }
                        else if (!stack.Peek().Isolate && stack.Count >= 2)
                        {
                            stack.Pop();
                        }

                        break;
                    }

                    // X8: a paragraph separator belongs to the paragraph, not to anything open
                    // inside it.
                    case BidiClass.B:
                    {
                        _levels[idx] = _paragraphLevel;
                        break;
                    }

                    default:
                    {
                        _levels[idx] = stack.Peek().Level;
                        Override(idx, stack.Peek().Override);
                        break;
                    }
                }
            }
        }

        private void Override(int index, BidiClass over)
        {
            if (over != BidiClass.ON)
                _types[index] = over;
        }

        private static int NextLevel(byte level, bool rightToLeft)
            => rightToLeft ? (level + 1) | 1 : (level + 2) & ~1;

        private readonly struct Status
        {
            internal Status(byte level, BidiClass over, bool isolate)
            {
                Level = level;
                Override = over;
                Isolate = isolate;
            }

            internal byte Level { get; }
            internal BidiClass Override { get; }
            internal bool Isolate { get; }
        }

        // ----- X10, BD13: the isolating run sequences ---------------------------------------------

        /// <summary>
        /// The level runs of the paragraph, over the characters X9 did not remove.
        /// </summary>
        private List<List<int>> LevelRuns()
        {
            var runs = new List<List<int>>();
            List<int> current = null;
            byte level = 0;

            for (var idx = 0; idx < _length; idx++)
            {
                if (IsRemovedByX9(_initial[idx]))
                    continue;

                if (current == null || _levels[idx] != level)
                {
                    current = new List<int>();
                    runs.Add(current);
                    level = _levels[idx];
                }

                current.Add(idx);
            }

            return runs;
        }

        private List<Sequence> IsolatingRunSequences()
        {
            var runs = LevelRuns();
            var runOfCharacter = new Dictionary<int, int>();
            for (var idx = 0; idx < runs.Count; idx++)
            {
                foreach (var character in runs[idx])
                    runOfCharacter[character] = idx;
            }

            var used = new bool[runs.Count];
            var sequences = new List<Sequence>();

            for (var idx = 0; idx < runs.Count; idx++)
            {
                if (used[idx])
                    continue;

                // BD13: a sequence starts at a run whose first character is not a PDI that closes
                // something. A PDI that does belongs to the sequence its initiator started.
                var first = runs[idx][0];
                if (_initial[first] == BidiClass.PDI && _matchingInitiator[first] != -1)
                    continue;

                var indices = new List<int>();
                var run = idx;
                while (true)
                {
                    used[run] = true;
                    indices.AddRange(runs[run]);

                    var last = runs[run][runs[run].Count - 1];
                    if (!IsIsolateInitiator(_initial[last]))
                        break;

                    var pdi = _matchingPdi[last];
                    if (pdi >= _length || !runOfCharacter.TryGetValue(pdi, out var next) || used[next])
                        break;

                    run = next;
                }

                sequences.Add(BuildSequence(indices));
            }

            // A run whose first character is a matched PDI but whose initiator was never reached -
            // which happens when the initiator overflowed - is still a sequence of its own.
            for (var idx = 0; idx < runs.Count; idx++)
            {
                if (!used[idx])
                    sequences.Add(BuildSequence(new List<int>(runs[idx])));
            }

            return sequences;
        }

        private Sequence BuildSequence(List<int> indices)
        {
            var level = _levels[indices[0]];

            // sos: the higher of this sequence's level and the level of whatever precedes it,
            // read as a direction. eos: the same looking forward, except that a sequence ending in
            // an isolate initiator with nothing to close it looks at the paragraph instead.
            var before = _paragraphLevel;
            for (var idx = indices[0] - 1; idx >= 0; idx--)
            {
                if (IsRemovedByX9(_initial[idx]))
                    continue;

                before = _levels[idx];
                break;
            }

            var lastIndex = indices[^1];
            var after = _paragraphLevel;
            if (!(IsIsolateInitiator(_initial[lastIndex]) && _matchingPdi[lastIndex] >= _length))
            {
                for (var idx = lastIndex + 1; idx < _length; idx++)
                {
                    if (IsRemovedByX9(_initial[idx]))
                        continue;

                    after = _levels[idx];
                    break;
                }
            }

            var lastLevel = _levels[lastIndex];
            return new Sequence(this, indices, level,
                DirectionOf(Math.Max(level, before)),
                DirectionOf(Math.Max(lastLevel, after)));
        }

        private static BidiClass DirectionOf(int level) => (level & 1) == 0 ? BidiClass.L : BidiClass.R;

        // ----- L1: put the separators and the trailing whitespace back ----------------------------

        private void ResetWhitespaceLevels()
        {
            // Read from the *original* types, not the resolved ones: by now a space may have been
            // turned into something strong, and L1 is not interested in that.
            var trailing = true;
            for (var idx = _length - 1; idx >= 0; idx--)
            {
                var type = _initial[idx];
                if (type == BidiClass.B || type == BidiClass.S)
                {
                    _levels[idx] = _paragraphLevel;
                    trailing = true;
                }
                else if (trailing && (type == BidiClass.WS || IsIsolateInitiator(type)
                                      || type == BidiClass.PDI || IsRemovedByX9(type)))
                {
                    _levels[idx] = _paragraphLevel;
                }
                else
                {
                    trailing = false;
                }
            }
        }

        // ----- L2: draw the highest levels backwards ----------------------------------------------

        private int[] Reorder(bool[] removed)
        {
            var order = new List<int>(_length);
            for (var idx = 0; idx < _length; idx++)
            {
                if (!removed[idx])
                    order.Add(idx);
            }

            if (order.Count == 0)
                return Array.Empty<int>();

            byte highest = 0;
            byte lowestOdd = MaxDepth + 1;
            foreach (var idx in order)
            {
                var level = _levels[idx];
                if (level > highest)
                    highest = level;

                if ((level & 1) != 0 && level < lowestOdd)
                    lowestOdd = level;
            }

            var array = order.ToArray();
            for (int level = highest; level >= lowestOdd; level--)
            {
                for (var start = 0; start < array.Length; start++)
                {
                    if (_levels[array[start]] < level)
                        continue;

                    var end = start;
                    while (end + 1 < array.Length && _levels[array[end + 1]] >= level)
                        end++;

                    Array.Reverse(array, start, end - start + 1);
                    start = end;
                }
            }

            return array;
        }

        internal BidiClass[] Types => _types;
        internal BidiClass[] Initial => _initial;
        internal byte[] Levels => _levels;
        internal IReadOnlyList<int> CodePoints => _codePoints;
    }
}
