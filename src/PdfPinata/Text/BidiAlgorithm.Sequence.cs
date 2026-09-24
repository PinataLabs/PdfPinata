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
            ResolveW1NonSpacingMarks();
            ResolveW2EuropeanNumbersAfterArabicLetters();
            ResolveW3ArabicLetters();
            ResolveW4SingleSeparators();
            ResolveW5Terminators();
            ResolveW6RemainingSeparatorsAndTerminators();
            ResolveW7EuropeanNumbersInLeftToRightContext();
        }

        /// <summary>
        /// W1. A non-spacing mark takes the type of what it is attached to, and ON when what it is
        /// attached to is an isolate initiator or a PDI - because those are about to become
        /// neutrals and a mark must not inherit a direction from one.
        /// </summary>
        private void ResolveW1NonSpacingMarks()
        {
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
        }

        /// <summary>
        /// W2. A European number after an Arabic letter is an Arabic number.
        /// </summary>
        private void ResolveW2EuropeanNumbersAfterArabicLetters()
        {
            var strong = _sos;
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type is BidiClass.L or BidiClass.R or BidiClass.AL)
                    strong = type;
                else if (type == BidiClass.EN && strong == BidiClass.AL)
                    SetType(idx, BidiClass.AN);
            }
        }

        /// <summary>
        /// W3. Arabic letters are simply strong right-to-left from here on.
        /// </summary>
        private void ResolveW3ArabicLetters()
        {
            for (var idx = 0; idx < Count; idx++)
            {
                if (TypeAt(idx) == BidiClass.AL)
                    SetType(idx, BidiClass.R);
            }
        }

        /// <summary>
        /// W4. A single separator between two numbers of the same kind joins them.
        /// </summary>
        private void ResolveW4SingleSeparators()
        {
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
        }

        /// <summary>
        /// W5. A run of terminators touching a European number joins it - "$1" and "1%" alike, so
        /// the run has to be looked at from both ends.
        /// </summary>
        private void ResolveW5Terminators()
        {
            for (var idx = 0; idx < Count; idx++)
            {
                if (TypeAt(idx) != BidiClass.ET)
                    continue;

                var end = EndOfRun(idx, BidiClass.ET);
                if (TypeBefore(idx) == BidiClass.EN || TypeAfter(end) == BidiClass.EN)
                    SetTypes(idx, end, BidiClass.EN);

                idx = end;
            }
        }

        /// <summary>
        /// The last position of the run of <paramref name="type"/> that starts at
        /// <paramref name="start"/>.
        /// </summary>
        private int EndOfRun(int start, BidiClass type)
        {
            var end = start;
            while (end + 1 < Count && TypeAt(end + 1) == type)
                end++;

            return end;
        }

        /// <summary>
        /// The type just before <paramref name="position"/>, or <see cref="BidiClass.ON"/> at the
        /// start of the sequence.
        /// </summary>
        private BidiClass TypeBefore(int position) => position > 0 ? TypeAt(position - 1) : BidiClass.ON;

        /// <summary>
        /// The type just after <paramref name="position"/>, or <see cref="BidiClass.ON"/> at the end
        /// of the sequence.
        /// </summary>
        private BidiClass TypeAfter(int position) => position + 1 < Count ? TypeAt(position + 1) : BidiClass.ON;

        private void SetTypes(int start, int end, BidiClass type)
        {
            for (var idx = start; idx <= end; idx++)
                SetType(idx, type);
        }

        /// <summary>
        /// W6. Whatever separators and terminators are left are neutral.
        /// </summary>
        private void ResolveW6RemainingSeparatorsAndTerminators()
        {
            for (var idx = 0; idx < Count; idx++)
            {
                var type = TypeAt(idx);
                if (type is BidiClass.ET or BidiClass.ES or BidiClass.CS)
                    SetType(idx, BidiClass.ON);
            }
        }

        /// <summary>
        /// W7. A European number in left-to-right context is simply left-to-right.
        /// </summary>
        private void ResolveW7EuropeanNumbersInLeftToRightContext()
        {
            var strong = _sos;
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
                var inside = StrongDirectionInside(open, close);
                if (inside == Embedding)
                {
                    // b. Something inside runs the way the brackets already do.
                    SetBracket(open, close, Embedding);
                }
                else if (inside == Opposite)
                {
                    // c. Something inside runs the other way, so what came before the brackets
                    // decides whether they follow it or stay with the embedding.
                    SetBracket(open, close, StrongDirectionBefore(open) == Opposite ? Opposite : Embedding);
                }

                // d. Nothing strong inside: the brackets are left to the N1 and N2 rules.
            }
        }

        /// <summary>
        /// The embedding direction when any strong type between the brackets matches it, else the
        /// opposite direction when there is a strong type at all, else <see cref="BidiClass.ON"/>.
        /// </summary>
        private BidiClass StrongDirectionInside(int open, int close)
        {
            var result = BidiClass.ON;
            for (var idx = open + 1; idx < close; idx++)
            {
                var strong = StrongDirectionOf(TypeAt(idx));
                if (strong == Embedding)
                    return Embedding;

                if (strong != BidiClass.ON)
                    result = Opposite;
            }

            return result;
        }

        /// <summary>
        /// The nearest strong direction before <paramref name="position"/>, or the start of the
        /// sequence's when there is none.
        /// </summary>
        private BidiClass StrongDirectionBefore(int position)
        {
            for (var idx = position - 1; idx >= 0; idx--)
            {
                var strong = StrongDirectionOf(TypeAt(idx));
                if (strong != BidiClass.ON)
                    return strong;
            }

            return _sos;
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
                var opens = closing >= 0;
                if (opens && stack.Count == capacity)
                    break;

                if (opens)
                    stack.Add((Canonical(closing), idx));
                else if (IsClosingBracket(codePoint))
                    CloseBracket(stack, pairs, Canonical(codePoint), idx);
            }

            pairs.Sort((left, right) => left.Open.CompareTo(right.Open));
            return pairs;
        }

        /// <summary>
        /// Pairs the closing bracket at <paramref name="position"/> with the nearest opener on the
        /// stack that it closes, dropping that opener and everything above it. A closer that closes
        /// nothing on the stack is ignored.
        /// </summary>
        private static void CloseBracket(
            List<(int Closing, int Position)> stack, List<(int Open, int Close)> pairs, int wanted, int position)
        {
            for (var depth = stack.Count - 1; depth >= 0; depth--)
            {
                if (stack[depth].Closing != wanted)
                    continue;

                pairs.Add((stack[depth].Position, position));
                stack.RemoveRange(depth, stack.Count - depth);
                return;
            }
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

                SetTypes(idx, end, NeutralResolution(idx, end));
                idx = end;
            }
        }

        /// <summary>
        /// What the run of neutrals from <paramref name="start"/> to <paramref name="end"/> resolves
        /// to, from the strong directions either side of it.
        /// </summary>
        private BidiClass NeutralResolution(int start, int end)
        {
            var before = start == 0 ? _sos : StrongDirectionOf(TypeAt(start - 1));
            var after = end + 1 == Count ? _eos : StrongDirectionOf(TypeAt(end + 1));

            // N1 when the two sides agree, N2 - the embedding direction - when they do not.
            return before == after && before != BidiClass.ON ? before : Embedding;
        }

        // ----- I1 and I2: from types back to levels ---------------------------------------------------

        private void ResolveImplicitLevels()
        {
            var even = (_level & 1) == 0;
            for (var idx = 0; idx < Count; idx++)
                _paragraph.Levels[_indices[idx]] = (byte)(_level + ImplicitBump(TypeAt(idx), even));
        }

        private static int ImplicitBump(BidiClass type, bool even)
        {
            if (even)
            {
                // I1. In an even run, right-to-left text goes one deeper and a number two, so
                // that the number sits inside the right-to-left text around it.
                return type == BidiClass.R ? 1
                    : type is BidiClass.AN or BidiClass.EN ? 2
                    : 0;
            }

            // I2. In an odd run, anything left-to-right - a number included - goes one
            // deeper.
            return type is BidiClass.L or BidiClass.EN or BidiClass.AN
                ? 1
                : 0;
        }
    }
}
