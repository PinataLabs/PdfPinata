using System;
using System.Collections.Generic;

namespace PdfPinata.Text;

public static partial class BidiAlgorithm
{
    /// <summary>
    /// One isolating run sequence, per BD13: the stretch of text the W, N and I rules are applied
    /// to. It is not necessarily contiguous - an isolate initiator's run and the run its matching
    /// PDI starts are one sequence with everything between them left out - which is the whole
    /// reason the rules are defined over sequences rather than over the paragraph.
    /// </summary>
    private sealed class Sequence
    {
        private readonly Paragraph _paragraph;
        private readonly List<int> _indices;
        private readonly byte _level;
        private readonly BidiClass _sos;
        private readonly BidiClass _eos;

        internal Sequence(Paragraph paragraph, List<int> indices, byte level,
            BidiClass sos, BidiClass eos)
        {
            _paragraph = paragraph;
            _indices = indices;
            _level = level;
            _sos = sos;
            _eos = eos;
        }

        private int Count => _indices.Count;

        private BidiClass TypeAt(int index) => _paragraph.Types[_indices[index]];

        private void SetType(int index, BidiClass type) => _paragraph.Types[_indices[index]] = type;

        private BidiClass InitialAt(int index) => _paragraph.Initial[_indices[index]];

        internal void Resolve()
        {
            ResolveWeakTypes();
            ResolveBracketPairs();
            ResolveNeutralTypes();
            ResolveImplicitLevels();
        }

        // ----- W1 to W7 ---------------------------------------------------------------------------

        private void ResolveWeakTypes()
        {
            // W1. A non-spacing mark takes the type of what it is attached to, and ON when what it
            // is attached to is an isolate initiator or a PDI - because those are about to become
            // neutrals and a mark must not inherit a direction from one.
            var previous = _sos;
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type == BidiClass.NSM)
                {
                    type = IsIsolateInitiator(previous) || previous == BidiClass.PDI
                        ? BidiClass.ON
                        : previous;

                    SetType(idx, type);
                }

                // The resolved type, not the one that was there before. A second mark on the same
                // character is attached to the first, so a run of them all end up the same - and
                // carrying the unresolved NSM forward instead makes the second one a mark attached
                // to a mark, which is nothing at all.
                previous = type;
            }

            // W2. A European number after an Arabic letter is an Arabic number.
            var strong = _sos;
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type is BidiClass.L or BidiClass.R or BidiClass.AL)
                    strong = type;
                else if (type == BidiClass.EN && strong == BidiClass.AL)
                    SetType(idx, BidiClass.AN);
            }

            // W3. Arabic letters are simply strong right-to-left from here on.
            for (var idx = 0; idx < Count; idx++)
            {
                if (TypeAt(idx) == BidiClass.AL)
                    SetType(idx, BidiClass.R);
            }

            // W4. A single separator between two numbers of the same kind joins them.
            for (var idx = 1; idx < Count - 1; idx++)
            {
                var type = TypeAt(idx);
                if (type != BidiClass.ES && type != BidiClass.CS)
                    continue;

                var before = TypeAt(idx - 1);
                var after = TypeAt(idx + 1);

                if (before == BidiClass.EN && after == BidiClass.EN)
                    SetType(idx, BidiClass.EN);
                else if (type == BidiClass.CS && before == BidiClass.AN && after == BidiClass.AN)
                    SetType(idx, BidiClass.AN);
            }

            // W5. A run of terminators touching a European number joins it - "$1" and "1%" alike,
            // so the run has to be looked at from both ends.
            for (var idx = 0; idx < Count; idx++)
            {
                if (TypeAt(idx) != BidiClass.ET)
                    continue;

                var end = idx;
                while (end + 1 < Count && TypeAt(end + 1) == BidiClass.ET)
                    end++;

                var adjacent = (idx > 0 && TypeAt(idx - 1) == BidiClass.EN)
                               || (end + 1 < Count && TypeAt(end + 1) == BidiClass.EN);

                if (adjacent)
                {
                    for (var scan = idx; scan <= end; scan++)
                        SetType(scan, BidiClass.EN);
                }

                idx = end;
            }

            // W6. Whatever separators and terminators are left are neutral.
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type is BidiClass.ET or BidiClass.ES or BidiClass.CS)
                    SetType(idx, BidiClass.ON);
            }

            // W7. A European number in left-to-right context is simply left-to-right.
            strong = _sos;
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type is BidiClass.L or BidiClass.R)
                    strong = type;
                else if (type == BidiClass.EN && strong == BidiClass.L)
                    SetType(idx, BidiClass.L);
            }
        }

        // ----- N0 and BD16: paired brackets --------------------------------------------------------

        /// <summary>
        /// The direction the sequence is embedded in - what a neutral falls back to.
        /// </summary>
        private BidiClass Embedding => (_level & 1) == 0 ? BidiClass.L : BidiClass.R;

        private BidiClass Opposite => (_level & 1) == 0 ? BidiClass.R : BidiClass.L;

        /// <summary>
        /// A strong direction for the purposes of the N rules, where a number counts as
        /// right-to-left however it was written.
        /// </summary>
        private static BidiClass StrongDirectionOf(BidiClass type)
        {
            if (type == BidiClass.L)
                return BidiClass.L;

            if (type is BidiClass.R or BidiClass.EN or BidiClass.AN)
                return BidiClass.R;

            return BidiClass.ON;
        }

        private void ResolveBracketPairs()
        {
            var pairs = BracketPairs();
            foreach (var (open, close) in pairs)
            {
                // N0 b and c: what is inside the brackets decides, and only strong types count.
                bool foundEmbedding = false, foundOpposite = false;
                for (var idx = open + 1; idx < close; idx++)
                {
                    var strong = StrongDirectionOf(TypeAt(idx));
                    if (strong == BidiClass.ON)
                        continue;

                    if (strong == Embedding)
                        foundEmbedding = true;
                    else
                        foundOpposite = true;
                }

                if (foundEmbedding)
                {
                    // b. Something inside runs the way the brackets already do.
                    SetBracket(open, close, Embedding);
                }
                else if (foundOpposite)
                {
                    // c. Something inside runs the other way, so what came before the brackets
                    // decides whether they follow it or stay with the embedding.
                    var context = _sos;
                    for (var idx = open - 1; idx >= 0; idx--)
                    {
                        var strong = StrongDirectionOf(TypeAt(idx));
                        if (strong != BidiClass.ON)
                        {
                            context = strong;
                            break;
                        }
                    }

                    SetBracket(open, close, context == Opposite ? Opposite : Embedding);
                }

                // d. Nothing strong inside: the brackets are left to the N1 and N2 rules.
            }
        }

        /// <summary>
        /// Sets a bracket pair's type, and with it any mark hanging off either bracket - N0's last
        /// clause, without which an accent on a bracket is resolved as though the bracket had not
        /// been.
        /// </summary>
        private void SetBracket(int open, int close, BidiClass type)
        {
            SetType(open, type);
            SetType(close, type);

            foreach (var bracket in new[] { open, close })
            {
                for (var idx = bracket + 1; idx < Count; idx++)
                {
                    if (InitialAt(idx) != BidiClass.NSM)
                        break;

                    SetType(idx, type);
                }
            }
        }

        /// <summary>
        /// BD16: the bracket pairs of the sequence, by opening position.
        /// </summary>
        private List<(int Open, int Close)> BracketPairs()
        {
            // "If an opening paired bracket is found and there is no room in the stack, stop
            // processing BD16 for the remainder of the isolating run sequence." Sixty-three is the
            // number the specification gives, and it is not negotiable: a longer stack would find
            // pairs a conformant implementation does not.
            const int capacity = 63;

            var stack = new List<(int Closing, int Position)>();
            var pairs = new List<(int Open, int Close)>();

            for (var idx = 0; idx < Count; idx++)
            {
                // Only a bracket that is still a neutral is a bracket for this purpose.
                if (TypeAt(idx) != BidiClass.ON)
                    continue;

                var codePoint = _paragraph.CodePoints[_indices[idx]];

                var closing = ClosingBracketOf(codePoint);
                if (closing >= 0)
                {
                    if (stack.Count == capacity)
                        break;

                    stack.Add((Canonical(closing), idx));
                    continue;
                }

                if (!IsClosingBracket(codePoint))
                    continue;

                var wanted = Canonical(codePoint);
                for (var depth = stack.Count - 1; depth >= 0; depth--)
                {
                    if (stack[depth].Closing != wanted)
                        continue;

                    pairs.Add((stack[depth].Position, idx));
                    stack.RemoveRange(depth, stack.Count - depth);
                    break;
                }
            }

            pairs.Sort((left, right) => left.Open.CompareTo(right.Open));
            return pairs;
        }

        /// <summary>
        /// The two angle brackets that are canonically equivalent to two others, folded together -
        /// without which "〈a〉" written with one pair and closed with the other would not pair up.
        /// </summary>
        private static int Canonical(int codePoint) => codePoint switch
        {
            0x3008 => 0x2329,
            0x3009 => 0x232A,
            _ => codePoint
        };

        private static int ClosingBracketOf(int codePoint)
        {
            var index = Array.BinarySearch(UnicodeTables.BracketOpen, codePoint);
            return index >= 0 ? UnicodeTables.BracketClose[index] : -1;
        }

        private static bool IsClosingBracket(int codePoint)
            => Array.IndexOf(UnicodeTables.BracketClose, codePoint) >= 0;

        // ----- N1 and N2: the neutrals ---------------------------------------------------------------

        private void ResolveNeutralTypes()
        {
            for (var idx = 0; idx < Count; idx++)
            {
                if (!IsNeutralOrIsolate(TypeAt(idx)))
                    continue;

                var end = idx;
                while (end + 1 < Count && IsNeutralOrIsolate(TypeAt(end + 1)))
                    end++;

                var before = idx == 0 ? _sos : StrongDirectionOf(TypeAt(idx - 1));
                var after = end + 1 == Count ? _eos : StrongDirectionOf(TypeAt(end + 1));

                // N1 when the two sides agree, N2 - the embedding direction - when they do not.
                var resolved = before == after && before != BidiClass.ON ? before : Embedding;
                for (var scan = idx; scan <= end; scan++)
                    SetType(scan, resolved);

                idx = end;
            }
        }

        // ----- I1 and I2: from types back to levels ---------------------------------------------------

        private void ResolveImplicitLevels()
        {
            var even = (_level & 1) == 0;
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                int bump;

                if (even)
                {
                    // I1. In an even run, right-to-left text goes one deeper and a number two, so
                    // that the number sits inside the right-to-left text around it.
                    bump = type == BidiClass.R ? 1
                        : type is BidiClass.AN or BidiClass.EN ? 2
                        : 0;
                }
                else
                {
                    // I2. In an odd run, anything left-to-right - a number included - goes one
                    // deeper.
                    bump = type is BidiClass.L or BidiClass.EN or BidiClass.AN
                        ? 1
                        : 0;
                }

                _paragraph.Levels[_indices[idx]] = (byte)(_level + bump);
            }
        }
    }
}
