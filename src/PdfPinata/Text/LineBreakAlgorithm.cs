using System;
using System.Buffers;
using static PdfPinata.Text.LineBreakClass;

namespace PdfPinata.Text;

/// <summary>
/// The Unicode Line Breaking Algorithm, UAX #14: where in a text a line may be broken, where it
/// must be, and where it may not.
/// </summary>
/// <remarks>
/// <para>
/// Pure text processing, like <see cref="BidiAlgorithm"/>, and in the core for the same reason: it
/// touches no font and no backend, and a caller who only wants to know where a line may end should
/// not have to install anything to find out. It answers where a break is <em>allowed</em>; choosing
/// which of those to take is the layout engine's business.
/// </para>
/// <para>
/// The rules are UAX #14's for Unicode 17.0.0, the version <see cref="UnicodeProperties"/> carries
/// its tables from, and the comments below name them as the specification does - LB9, LB15a, LB25.
/// <c>LineBreakTest.txt</c> passes in full with <see cref="LineBreakTailoring.None"/>.
/// </para>
/// <para>
/// Each answer is about the position <em>before</em> a UTF-16 index: a break at index 3 puts the
/// character at 3 at the start of the next line. A position inside a surrogate pair is never a
/// break.
/// </para>
/// </remarks>
public static class LineBreakAlgorithm
{
    /// <summary>
    /// What may happen at every position of a text: one entry per UTF-16 index from 0 to
    /// <c>text.Length</c>, each saying whether a line may be broken before the code unit there.
    /// The first is always <see cref="LineBreakOpportunity.None"/> (rule LB2) and the last always
    /// <see cref="LineBreakOpportunity.Mandatory"/> (LB3) unless the text is empty.
    /// </summary>
    public static LineBreakOpportunity[] BreakOpportunities(
        string text, LineBreakTailoring tailoring = LineBreakTailoring.None)
    {
        ArgumentNullException.ThrowIfNull(text);

        var result = new LineBreakOpportunity[text.Length + 1];
        var rented = ArrayPool<Unit>.Shared.Rent(Math.Max(text.Length, 1));
        try
        {
            var units = rented.AsSpan(0, Resolve(text.AsSpan(), ReadOnlySpan<char>.Empty, tailoring, rented, out _));
            for (var k = 0; k <= units.Length; k++)
                result[k < units.Length ? units[k].Index : text.Length] = Decide(units, k);
        }
        finally
        {
            ArrayPool<Unit>.Shared.Return(rented);
        }

        return result;
    }

    /// <summary>
    /// What may happen at one position of a text: whether a line may be broken before the code
    /// unit at <paramref name="index"/>. <paramref name="index"/> may be <c>text.Length</c>, the
    /// end of the text, where a break is mandatory.
    /// </summary>
    public static LineBreakOpportunity BreakBefore(
        ReadOnlySpan<char> text, int index, LineBreakTailoring tailoring = LineBreakTailoring.None)
    {
        if (index < 0 || index > text.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "The position must be within the text or at its end.");

        if (index > 0 && index < text.Length && char.IsHighSurrogate(text[index - 1]) && char.IsLowSurrogate(text[index]))
            return LineBreakOpportunity.None;

        return Decide(text[..index], text[index..], tailoring);
    }

    /// <summary>
    /// Whether a line may be broken before the code unit at <paramref name="index"/> - see
    /// <see cref="BreakBefore"/> - mandatorily or not.
    /// </summary>
    public static bool IsBreakAllowed(
        ReadOnlySpan<char> text, int index, LineBreakTailoring tailoring = LineBreakTailoring.None)
        => BreakBefore(text, index, tailoring) != LineBreakOpportunity.None;

    /// <summary>
    /// Whether a line may be broken where <paramref name="before"/> ends and
    /// <paramref name="after"/> begins, in the text the two make one after the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a layout engine that holds a text in pieces - a run of one formatting followed by a run
    /// of another - and needs to know whether the place they meet is a place a line may be
    /// broken. The two are read where they lie rather than copied into one string, and nothing is
    /// allocated unless together they are longer than a word or two, so this is cheap enough to
    /// ask at every seam.
    /// </para>
    /// <para>
    /// Every rule reads the text either side of the position, some as far back as the start of a
    /// number or a row of spaces, so pass as much of each side as could matter: the whole of each
    /// piece, or a generous window of it. What is not passed counts as the start or the end of the
    /// text. An empty <paramref name="before"/> therefore answers false (rule LB2, never break at
    /// the start), and an empty <paramref name="after"/> true (LB3, always break at the end).
    /// </para>
    /// </remarks>
    public static bool IsBreakAllowedBetween(
        ReadOnlySpan<char> before, ReadOnlySpan<char> after, LineBreakTailoring tailoring = LineBreakTailoring.None)
        => Decide(before, after, tailoring) != LineBreakOpportunity.None;

    /// <summary>
    /// The position between <paramref name="before"/> and <paramref name="after"/>, decided over
    /// a buffer on the stack while the two are short enough for one.
    /// </summary>
    private static LineBreakOpportunity Decide(
        ReadOnlySpan<char> before, ReadOnlySpan<char> after, LineBreakTailoring tailoring)
    {
        var length = before.Length + after.Length;
        Unit[] rented = null;
        Span<Unit> buffer = length <= StackLimit
            ? stackalloc Unit[length]
            : rented = ArrayPool<Unit>.Shared.Rent(length);
        try
        {
            var count = Resolve(before, after, tailoring, buffer, out var position);
            return Decide(buffer[..count], position);
        }
        finally
        {
            if (rented != null)
                ArrayPool<Unit>.Shared.Return(rented);
        }
    }

    /// <summary>How many code units a text may be and still be resolved on the stack.</summary>
    private const int StackLimit = 128;

    // ----- LB1, LB9 and LB10: what each character is, as far as the rules are concerned ------------

    /// <summary>One code point of the text, with what the rules read of it.</summary>
    private struct Unit
    {
        /// <summary>Its class as rule LB1 resolves it, which rules LB4 to LB8a read.</summary>
        public LineBreakClass Original;

        /// <summary>
        /// Its class once rule LB10 has made a combining mark that is no part of a sequence an
        /// AL, which rules LB11 onwards read. Unused where <see cref="Absorbed"/>.
        /// </summary>
        public LineBreakClass Class;

        public LineBreakFlags Flags;

        /// <summary>
        /// A combining mark or joiner rule LB9 has made part of the character before it, which
        /// rules LB11 onwards read through to that character.
        /// </summary>
        public bool Absorbed;

        public int CodePoint;

        /// <summary>The UTF-16 index of its first code unit in the text it came from.</summary>
        public int Index;
    }

    /// <summary>
    /// Resolves <paramref name="first"/> and then <paramref name="second"/> into
    /// <paramref name="units"/>, as one text, and answers how many units that took and how many of
    /// them <paramref name="first"/> made.
    /// </summary>
    private static int Resolve(
        ReadOnlySpan<char> first, ReadOnlySpan<char> second, LineBreakTailoring tailoring, Span<Unit> units,
        out int firstCount)
    {
        firstCount = Append(first, 0, tailoring, units, 0);
        var count = Append(second, first.Length, tailoring, units, firstCount);

        for (var k = 0; k < count; k++)
        {
            ref var unit = ref units[k];

            // LB9: X (CM | ZWJ)* is X, for every X but the hard breaks, a space and ZW. A mark
            // following a mark is part of whatever the first one is part of, or of the AL LB10 is
            // about to make it, so looking one back is enough.
            if (unit.Original is CM or ZWJ && k > 0 && units[k - 1].Original is not (BK or CR or LF or NL or SP or ZW))
            {
                unit.Absorbed = true;
                continue;
            }

            // LB10: any other is an AL, and has the properties of U+0041 too.
            if (unit.Original is CM or ZWJ)
            {
                unit.Class = AL;
                unit.Flags = LineBreakFlags.None;
            }
            else
            {
                unit.Class = unit.Original;
            }
        }

        return count;
    }

    private static int Append(
        ReadOnlySpan<char> text, int offset, LineBreakTailoring tailoring, Span<Unit> units, int count)
    {
        for (var idx = 0; idx < text.Length; count++)
        {
            int codePoint;
            var start = idx;
            if (char.IsHighSurrogate(text[idx]) && idx + 1 < text.Length && char.IsLowSurrogate(text[idx + 1]))
            {
                codePoint = char.ConvertToUtf32(text[idx], text[idx + 1]);
                idx += 2;
            }
            else
            {
                codePoint = text[idx];
                idx++;
            }

            var flags = UnicodeProperties.LineBreakFlagsOf(codePoint);
            units[count] = new Unit
            {
                Original = ResolvedClass(UnicodeProperties.LineBreakClassOf(codePoint), flags, tailoring),
                Flags = flags,
                CodePoint = codePoint,
                Index = offset + start
            };
        }

        return count;
    }

    /// <summary>
    /// LB1: the classes the rules have nothing to say about made into ones they do. AI, SG and XX
    /// are AL, SA is CM where it is a mark and AL elsewhere, and CJ - the small kana - is NS. CB is
    /// left alone; LB20 has a rule for it.
    /// </summary>
    private static LineBreakClass ResolvedClass(LineBreakClass lineBreakClass, LineBreakFlags flags, LineBreakTailoring tailoring)
        => lineBreakClass switch
        {
            AI or SG or XX => AL,
            SA when (flags & LineBreakFlags.SouthEastAsianMark) != 0 => CM,
            SA when (tailoring & LineBreakTailoring.SouthEastAsianAsIdeographs) != 0 => ID,
            SA => AL,
            CJ => NS,
            _ => lineBreakClass
        };

    // ----- the rules --------------------------------------------------------------------------------

    /// <summary>The position before unit <paramref name="k"/>, by rules LB2 to LB9.</summary>
    private static LineBreakOpportunity Decide(ReadOnlySpan<Unit> units, int k)
    {
        // LB2, LB3: never at the start, always at the end.
        if (k == 0)
            return LineBreakOpportunity.None;
        if (k == units.Length)
            return LineBreakOpportunity.Mandatory;

        var before = units[k - 1].Original;
        var at = units[k].Original;

        // LB4, LB5: after a hard line break, CR LF being one.
        if (before == BK)
            return LineBreakOpportunity.Mandatory;
        if (before == CR && at == LF)
            return LineBreakOpportunity.None;
        if (before is CR or LF or NL)
            return LineBreakOpportunity.Mandatory;

        // LB6, LB7: not before a hard line break, a space or ZW.
        if (at is BK or CR or LF or NL or SP or ZW)
            return LineBreakOpportunity.None;

        // LB8: ZW SP* ÷
        var j = k - 1;
        while (j >= 0 && units[j].Original == SP)
            j--;
        if (j >= 0 && units[j].Original == ZW)
            return LineBreakOpportunity.Allowed;

        // LB8a: ZWJ ×
        if (before == ZWJ)
            return LineBreakOpportunity.None;

        // LB9: not inside a combining character sequence.
        if (units[k].Absorbed)
            return LineBreakOpportunity.None;

        // The first unit is never absorbed, so there is always one before.
        return Pair(units, Previous(units, k), k);
    }

    /// <summary>
    /// The position between units <paramref name="a"/> and <paramref name="b"/>, neither absorbed
    /// and nothing between them but what LB9 absorbed, by rules LB11 to LB31.
    /// </summary>
    private static LineBreakOpportunity Pair(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        // LB11: × WJ, WJ ×
        if (left == WJ || right == WJ)
            return LineBreakOpportunity.None;

        // LB12: GL ×
        if (left == GL)
            return LineBreakOpportunity.None;

        // LB12a: [^SP BA HY HH] × GL
        if (right == GL && left is not (SP or BA or HY or HH))
            return LineBreakOpportunity.None;

        // LB13: × CL, × CP, × EX, × SY
        if (right is CL or CP or EX or SY)
            return LineBreakOpportunity.None;

        var beforeSpaces = SkipSpaces(units, a);

        // LB14: OP SP* ×
        if (beforeSpaces >= 0 && units[beforeSpaces].Class == OP)
            return LineBreakOpportunity.None;

        // LB15a: (sot | BK | CR | LF | NL | OP | QU | GL | SP | ZW) [\p{Pi}&QU] SP* ×
        if (beforeSpaces >= 0 && IsInitialQuote(units[beforeSpaces]) && OpensQuotation(units, Previous(units, beforeSpaces)))
            return LineBreakOpportunity.None;

        // LB15b: × [\p{Pf}&QU] ( SP | GL | WJ | CL | QU | CP | EX | IS | SY | BK | CR | LF | NL | ZW | eot)
        if (IsFinalQuote(units[b]) && ClosesQuotation(units, Next(units, b)))
            return LineBreakOpportunity.None;

        // LB15c: SP ÷ IS NU
        if (left == SP && right == IS && Is(units, Next(units, b), NU))
            return LineBreakOpportunity.Allowed;

        // LB15d: × IS
        if (right == IS)
            return LineBreakOpportunity.None;

        // LB16: (CL | CP) SP* × NS
        if (right == NS && beforeSpaces >= 0 && units[beforeSpaces].Class is CL or CP)
            return LineBreakOpportunity.None;

        // LB17: B2 SP* × B2
        if (right == B2 && beforeSpaces >= 0 && units[beforeSpaces].Class == B2)
            return LineBreakOpportunity.None;

        // LB18: SP ÷
        if (left == SP)
            return LineBreakOpportunity.Allowed;

        return Quotation(units, a, b)
            ?? Hyphenation(units, a, b)
            ?? Numbers(units, a, b)
            ?? Syllables(units, a, b)
            ?? LineBreakOpportunity.Allowed; // LB31: everywhere else.
    }

    /// <summary>LB19, LB19a and LB20: quotation marks, and CB.</summary>
    private static LineBreakOpportunity? Quotation(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        // LB19: × [ QU - \p{Pi} ], [ QU - \p{Pf} ] ×
        if (right == QU && !IsInitialQuote(units[b]))
            return LineBreakOpportunity.None;
        if (left == QU && !IsFinalQuote(units[a]))
            return LineBreakOpportunity.None;

        // LB19a: unless East Asian characters surround it, not either side of a quotation mark.
        if (right == QU)
        {
            // [^$EastAsian] × QU, × QU ( [^$EastAsian] | eot )
            var next = Next(units, b);
            if (!IsEastAsian(units[a]) || next == units.Length || !IsEastAsian(units[next]))
                return LineBreakOpportunity.None;
        }

        if (left == QU)
        {
            // QU × [^$EastAsian], ( sot | [^$EastAsian] ) QU ×
            var previous = Previous(units, a);
            if (!IsEastAsian(units[b]) || previous < 0 || !IsEastAsian(units[previous]))
                return LineBreakOpportunity.None;
        }

        // LB20: ÷ CB, CB ÷
        if (left == CB || right == CB)
            return LineBreakOpportunity.Allowed;

        return null;
    }

    /// <summary>LB20a to LB22: hyphens, nonstarters and ellipses.</summary>
    private static LineBreakOpportunity? Hyphenation(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        // LB20a: ( sot | BK | CR | LF | NL | SP | ZW | CB | GL ) ( HY | HH ) × ( AL | HL )
        if (left is HY or HH && right is AL or HL)
        {
            var previous = Previous(units, a);
            if (previous < 0 || units[previous].Class is BK or CR or LF or NL or SP or ZW or CB or GL)
                return LineBreakOpportunity.None;
        }

        // LB21: × BA, × HH, × HY, × NS, BB ×
        if (right is BA or HH or HY or NS || left == BB)
            return LineBreakOpportunity.None;

        // LB21a: HL (HY | HH) × [^HL]
        if (left is HY or HH && right != HL && Is(units, Previous(units, a), HL))
            return LineBreakOpportunity.None;

        // LB21b: SY × HL
        if (left == SY && right == HL)
            return LineBreakOpportunity.None;

        // LB22: × IN
        if (right == IN)
            return LineBreakOpportunity.None;

        return null;
    }

    /// <summary>LB23 to LB25: letters, numbers and what is written either side of a number.</summary>
    private static LineBreakOpportunity? Numbers(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        // LB23: (AL | HL) × NU, NU × (AL | HL)
        if ((left is AL or HL && right == NU) || (left == NU && right is AL or HL))
            return LineBreakOpportunity.None;

        // LB23a: PR × (ID | EB | EM), (ID | EB | EM) × PO
        if ((left == PR && right is ID or EB or EM) || (left is ID or EB or EM && right == PO))
            return LineBreakOpportunity.None;

        // LB24: (PR | PO) × (AL | HL), (AL | HL) × (PR | PO)
        if ((left is PR or PO && right is AL or HL) || (left is AL or HL && right is PR or PO))
            return LineBreakOpportunity.None;

        // LB25, as the regular expressions it has been written as since Unicode 15.1:
        if (right is PO or PR)
        {
            // NU ( SY | IS )* (CL | CP)? × (PO | PR)
            var number = left is CL or CP ? Previous(units, a) : a;
            if (EndsANumber(units, number))
                return LineBreakOpportunity.None;
        }

        if (left is PO or PR)
        {
            // (PO | PR) × OP NU, (PO | PR) × OP IS NU, (PO | PR) × NU
            if (right == NU)
                return LineBreakOpportunity.None;

            if (right == OP)
            {
                var next = Next(units, b);
                if (Is(units, next, NU) || (Is(units, next, IS) && Is(units, Next(units, next), NU)))
                    return LineBreakOpportunity.None;
            }
        }

        // HY × NU, IS × NU, NU ( SY | IS )* × NU
        if (right == NU && (left is HY or IS || EndsANumber(units, a)))
            return LineBreakOpportunity.None;

        return null;
    }

    /// <summary>LB26 to LB30b: Korean syllables, letters, Brahmic syllables, brackets and emoji.</summary>
    private static LineBreakOpportunity? Syllables(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        // LB26: JL × (JL | JV | H2 | H3), (JV | H2) × (JV | JT), (JT | H3) × JT
        if ((left == JL && right is JL or JV or H2 or H3)
            || (left is JV or H2 && right is JV or JT)
            || (left is JT or H3 && right == JT))
            return LineBreakOpportunity.None;

        // LB27: (JL | JV | JT | H2 | H3) × PO, PR × (JL | JV | JT | H2 | H3)
        if ((left is JL or JV or JT or H2 or H3 && right == PO) || (left == PR && right is JL or JV or JT or H2 or H3))
            return LineBreakOpportunity.None;

        // LB28: (AL | HL) × (AL | HL)
        if (left is AL or HL && right is AL or HL)
            return LineBreakOpportunity.None;

        if (BrahmicSyllable(units, a, b))
            return LineBreakOpportunity.None;

        // LB29: IS × (AL | HL)
        if (left == IS && right is AL or HL)
            return LineBreakOpportunity.None;

        // LB30: (AL | HL | NU) × [OP - $EastAsian], [CP - $EastAsian] × (AL | HL | NU)
        if ((left is AL or HL or NU && right == OP && !IsEastAsian(units[b]))
            || (left == CP && !IsEastAsian(units[a]) && right is AL or HL or NU))
            return LineBreakOpportunity.None;

        // LB30a: sot (RI RI)* RI × RI, [^RI] (RI RI)* RI × RI
        if (left == RI && right == RI)
        {
            var preceding = 0;
            for (var j = a; j >= 0 && units[j].Class == RI; j = Previous(units, j))
                preceding++;

            if (preceding % 2 == 1)
                return LineBreakOpportunity.None;
        }

        // LB30b: EB × EM, [\p{Extended_Pictographic}&\p{Cn}] × EM
        if (right == EM && (left == EB || (units[a].Flags & LineBreakFlags.UnassignedPictographic) != 0))
            return LineBreakOpportunity.None;

        return null;
    }

    /// <summary>
    /// LB28a: AP × (AK | ◌ | AS), (AK | ◌ | AS) × (VF | VI), (AK | ◌ | AS) VI × (AK | ◌),
    /// (AK | ◌ | AS) × (AK | ◌ | AS) VF.
    /// </summary>
    private static bool BrahmicSyllable(ReadOnlySpan<Unit> units, int a, int b)
    {
        var left = units[a].Class;
        var right = units[b].Class;

        if (left == AP && IsAksara(units[b]))
            return true;

        if (IsAksara(units[a]) && right is VF or VI)
            return true;

        if (left == VI && (right == AK || units[b].CodePoint == DottedCircle))
        {
            var previous = Previous(units, a);
            if (previous >= 0 && IsAksara(units[previous]))
                return true;
        }

        return IsAksara(units[a]) && IsAksara(units[b]) && Is(units, Next(units, b), VF);
    }

    // ----- reading round the position --------------------------------------------------------------

    /// <summary>The unit before <paramref name="k"/> that LB9 did not absorb, or -1 for sot.</summary>
    private static int Previous(ReadOnlySpan<Unit> units, int k)
    {
        var j = k - 1;
        while (j >= 0 && units[j].Absorbed)
            j--;
        return j;
    }

    /// <summary>
    /// The unit after <paramref name="k"/> that LB9 did not absorb, or <c>units.Length</c> for eot.
    /// </summary>
    private static int Next(ReadOnlySpan<Unit> units, int k)
    {
        var j = k + 1;
        while (j < units.Length && units[j].Absorbed)
            j++;
        return j;
    }

    /// <summary>The first unit at or before <paramref name="k"/> that is not a space, or -1.</summary>
    private static int SkipSpaces(ReadOnlySpan<Unit> units, int k)
    {
        while (k >= 0 && units[k].Class == SP)
            k = Previous(units, k);
        return k;
    }

    /// <summary>Whether unit <paramref name="k"/> exists - is not sot or eot - and is of a class.</summary>
    private static bool Is(ReadOnlySpan<Unit> units, int k, LineBreakClass lineBreakClass)
        => k >= 0 && k < units.Length && units[k].Class == lineBreakClass;

    /// <summary>Whether a number ends at unit <paramref name="k"/>: NU ( SY | IS )*.</summary>
    private static bool EndsANumber(ReadOnlySpan<Unit> units, int k)
    {
        while (k >= 0 && units[k].Class is SY or IS)
            k = Previous(units, k);
        return Is(units, k, NU);
    }

    /// <summary>What may come before an initial quotation mark for LB15a: sot or one of nine classes.</summary>
    private static bool OpensQuotation(ReadOnlySpan<Unit> units, int k)
        => k < 0 || units[k].Class is BK or CR or LF or NL or OP or QU or GL or SP or ZW;

    /// <summary>What may come after a final quotation mark for LB15b: eot or one of fourteen classes.</summary>
    private static bool ClosesQuotation(ReadOnlySpan<Unit> units, int k)
        => k >= units.Length
            || units[k].Class is SP or GL or WJ or CL or QU or CP or EX or IS or SY or BK or CR or LF or NL or ZW;

    private static bool IsInitialQuote(Unit unit)
        => unit.Class == QU && (unit.Flags & LineBreakFlags.InitialQuote) != 0;

    private static bool IsFinalQuote(Unit unit)
        => unit.Class == QU && (unit.Flags & LineBreakFlags.FinalQuote) != 0;

    /// <summary>$EastAsian: East_Asian_Width Fullwidth, Wide or Halfwidth.</summary>
    private static bool IsEastAsian(Unit unit) => (unit.Flags & LineBreakFlags.EastAsian) != 0;

    /// <summary>LB28a's (AK | ◌ | AS).</summary>
    private static bool IsAksara(Unit unit) => unit.Class is AK or AS || unit.CodePoint == DottedCircle;

    /// <summary>U+25CC DOTTED CIRCLE, which LB28a names on its own.</summary>
    private const int DottedCircle = 0x25CC;
}
