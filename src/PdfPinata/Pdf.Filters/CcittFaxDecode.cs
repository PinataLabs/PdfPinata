using System;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the CCITTFaxDecode filter: ITU-T T.4 (Group 3) and T.6 (Group 4) facsimile coding of
/// a bilevel image.
/// <para>
/// ISO 32000-1 7.4.6. The data is decoded to rows of 1 bit a pixel, each padded to a whole byte. A
/// black pixel is 0 and a white one 1, as an image of <c>/DeviceGray</c> with a default
/// <c>/Decode</c> expects, unless <c>/BlackIs1</c> is true.
/// </para>
/// <para>
/// Damaged data decodes as far as it goes and is never an exception: a row that cannot be decoded
/// keeps what it held up to the damage, and decoding stops there unless <c>/EndOfLine</c> marks
/// where the next row begins and <c>/DamagedRowsBeforeError</c> allows another damaged row. Every
/// row from where decoding stopped to <c>/Rows</c> is white. Without <c>/Rows</c>, the rows are
/// those the data holds, up to two end-of-line codes in a row, which end a Group 3 or a Group 4
/// block whether or not <c>/EndOfBlock</c> says to expect them.
/// </para>
/// </summary>
public sealed class CcittFaxDecode : Filter
{
    /// <summary>
    /// The most bytes the filter decodes to: 256 MiB. Past it, a few bytes of Group 4 data saying
    /// "the same row again" could stand for any number of rows.
    /// </summary>
    internal const int MaximumLength = 1 << 28;

    /// <summary>
    /// Not supported: this library decodes facsimile data and does not write it.
    /// </summary>
    public override byte[] Encode(byte[] data) =>
        throw new NotSupportedException("CCITTFaxDecode data is decoded by this library and is not encoded.");

    /// <summary>
    /// Decodes the specified data, with the parameters of ISO 32000-1 Table 11.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The parameters are out of range: <c>/Columns</c> less than 1 or <c>/Rows</c> less than 0, or
    /// so many that the image would be more than 256 MiB.
    /// </exception>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        ArgumentNullException.ThrowIfNull(data);

        var parameters = CcittFaxParameters.Read(parms?.DecodeParms);
        return CcittFaxDecoder.Decode(data, parameters, parameters.Rows).Data;
    }
}

/// <summary>The parameters of a CCITTFaxDecode filter, ISO 32000-1 Table 11.</summary>
internal sealed class CcittFaxParameters
{
    /// <summary>
    /// Below 0, Group 4: every row is coded in two dimensions. 0, Group 3 in one dimension. Above
    /// 0, Group 3 mixed: each row says which it is.
    /// </summary>
    internal int K { get; init; }

    internal bool EndOfLine { get; init; }

    internal bool EncodedByteAlign { get; init; }

    internal int Columns { get; init; } = 1728;

    /// <summary>
    /// The rows the image has, or null where they are as many as the data holds, which a
    /// <c>/Rows</c> of 0 also says.
    /// </summary>
    internal int? Rows { get; init; }

    internal bool BlackIs1 { get; init; }

    internal int DamagedRowsBeforeError { get; init; }

    /// <summary>The bytes of a row of <paramref name="columns"/> pixels, in <c>long</c> because 7 added to int.MaxValue wraps.</summary>
    internal static long RowLength(int columns) => ((long)columns + 7) / 8;

    /// <summary>
    /// The parameters a dictionary gives, each of which takes its default where it is absent or is
    /// not of its type.
    /// </summary>
    internal static CcittFaxParameters Read(PdfDictionary parms)
    {
        if (parms == null)
            return new CcittFaxParameters();

        var defaults = new CcittFaxParameters();
        var columns = Integer("/Columns", defaults.Columns);
        var rows = Integer("/Rows", 0);
        if (columns < 1)
            throw new InvalidOperationException($"CCITTFaxDecode's /Columns is {columns}, and must be at least 1.");
        if (rows < 0)
            throw new InvalidOperationException($"CCITTFaxDecode's /Rows is {rows}, and must not be negative.");
        if (RowLength(columns) > CcittFaxDecode.MaximumLength)
            throw new InvalidOperationException($"CCITTFaxDecode's /Columns is {columns}, and a row of it is more than the {CcittFaxDecode.MaximumLength} bytes decoded.");

        return new CcittFaxParameters
        {
            K = Integer("/K", defaults.K),
            EndOfLine = Boolean("/EndOfLine", defaults.EndOfLine),
            EncodedByteAlign = Boolean("/EncodedByteAlign", defaults.EncodedByteAlign),
            Columns = columns,
            Rows = rows == 0 ? null : rows,
            BlackIs1 = Boolean("/BlackIs1", defaults.BlackIs1),
            DamagedRowsBeforeError = Math.Max(0, Integer("/DamagedRowsBeforeError", defaults.DamagedRowsBeforeError)),
        };

        int Integer(string key, int value) => PdfItemValues.TryGetInteger(parms.Elements[key], out var read) ? read : value;

        bool Boolean(string key, bool value) => PdfItemValues.TryGetBoolean(parms.Elements[key], out var read) ? read : value;
    }
}

/// <summary>
/// The rows a CCITTFaxDecode filter decoded, and what was wrong with the data, if anything.
/// </summary>
/// <param name="Data">The rows, 1 bit a pixel, each padded to a whole byte.</param>
/// <param name="Problem">
/// What was wrong with the data, as the rest of a sentence whose subject is the data, such as
/// "ends after 30 of its 61 rows."; or null where nothing was.
/// </param>
internal sealed record CcittFaxResult(byte[] Data, string Problem);

/// <summary>
/// Decodes ITU-T T.4 and T.6 data a row at a time. A row is held as its changing elements: the
/// positions at which the colour changes, the first from white to black, which is the form both
/// recommendations code a row in.
/// </summary>
internal sealed class CcittFaxDecoder
{
    private const int EndOfLineZeros = 11;

    private readonly byte[] _data;
    private readonly long _length;
    private readonly int _columns;
    private long _at;

    private CcittFaxDecoder(byte[] data, int columns)
    {
        _data = data;
        _length = (long)data.Length * 8;
        _columns = columns;
    }

    private enum CodingMode
    {
        Pass,
        Horizontal,
        Vertical,
    }

    private enum RowStart
    {
        Data,
        EndOfLine,
        EndOfData,
    }

    /// <summary>
    /// Decodes <paramref name="rows"/> rows, white where the data holds no more; or, where
    /// <paramref name="rows"/> is null, as many rows as the data holds. The output is allocated
    /// once, at its final size: the rows of the second case are counted first, so data that holds
    /// none or few allocates none or few.
    /// </summary>
    internal static CcittFaxResult Decode(byte[] data, CcittFaxParameters parameters, int? rows)
    {
        var rowLength = checked((int)CcittFaxParameters.RowLength(parameters.Columns));
        if (rowLength > CcittFaxDecode.MaximumLength)
        {
            throw new InvalidOperationException(
                $"CCITTFaxDecode data of {parameters.Columns} columns has rows of more than the {CcittFaxDecode.MaximumLength} bytes decoded.");
        }

        var mostRows = CcittFaxDecode.MaximumLength / rowLength;
        if (rows > mostRows)
        {
            throw new InvalidOperationException(
                $"CCITTFaxDecode data of {parameters.Columns} columns and {rows} rows would be more than the {CcittFaxDecode.MaximumLength} bytes decoded.");
        }

        var white = parameters.BlackIs1 ? (byte)0x00 : (byte)0xFF;
        var counted = rows ?? new CcittFaxDecoder(data, parameters.Columns).DecodeRows(parameters, null, mostRows, rowLength, null).Rows;
        var output = new byte[counted * rowLength];
        output.AsSpan().Fill(white);

        var (_, problem) = new CcittFaxDecoder(data, parameters.Columns).DecodeRows(parameters, rows, rows ?? mostRows, rowLength, output);
        return new CcittFaxResult(output, problem);
    }

    /// <summary>
    /// Decodes into <paramref name="output"/>, or only counts the rows where that is null.
    /// </summary>
    private (int Rows, string Problem) DecodeRows(CcittFaxParameters parameters, int? rows, int rowLimit, int rowLength, byte[] output)
    {
        // Each row is coded against the one above it, and the row above the first is white.
        var reference = new ChangingElements();
        var current = new ChangingElements();
        var canResync = parameters.K >= 0 && parameters.EndOfLine;
        var decoded = 0;
        var damaged = 0;
        var twoDimensionalRowsLeft = 0;
        string problem = null;

        while (decoded < rowLimit)
        {
            var start = SkipToRow(parameters.EncodedByteAlign, parameters.EndOfLine);
            if (start == RowStart.EndOfData)
                break;

            // In Group 3 mixed, the bit after an end-of-line code says how the row is coded (T.4
            // 4.2.1.3.4). Without one there is no such bit, and a row in one dimension is followed
            // by K - 1 rows in two, as Ghostscript writes them.
            var isTwoDimensional = parameters.K < 0;
            if (parameters.K > 0)
            {
                isTwoDimensional = start == RowStart.EndOfLine ? ReadBit() == 0 : twoDimensionalRowsLeft > 0;
                twoDimensionalRowsLeft = isTwoDimensional ? twoDimensionalRowsLeft - 1 : parameters.K - 1;
            }

            // Two end-of-line codes in a row end the block: RTC in Group 3 and EOFB in Group 4.
            if (start == RowStart.EndOfLine && SkipEndOfLine() != RowStart.Data)
                break;

            current.Clear();
            var failure = isTwoDimensional ? DecodeTwoDimensional(reference, current) : DecodeOneDimensional(current);
            if (failure == null && _at > _length)
                failure = "the data ends inside it";

            if (output != null)
                Pack(current, output.AsSpan(decoded * rowLength, rowLength), parameters.BlackIs1);
            decoded++;
            (reference, current) = (current, reference);

            if (failure == null)
                continue;

            damaged++;
            if (canResync && damaged <= parameters.DamagedRowsBeforeError && SeekEndOfLine())
                continue;

            problem = $"cannot be decoded past row {decoded}, because {failure}.";
            break;
        }

        var damagedRowsSteppedOver = problem == null ? damaged : damaged - 1;
        if (problem == null && rows == null && decoded == rowLimit && !IsRestEmpty())
            problem = $"holds more than the {decoded} rows of {_columns} columns decoded, which are {CcittFaxDecode.MaximumLength} bytes.";
        if (problem == null && decoded < rows)
            problem = $"ends after {decoded} of its {rows} rows.";

        if (damagedRowsSteppedOver > 0)
        {
            var damage = $"{damagedRowsSteppedOver} damaged rows, each decoded as far as it goes";
            problem = problem == null ? $"has {damage}." : $"{problem} Before that it has {damage}.";
        }

        if (problem != null && rows > decoded)
            problem += $" The {rows - decoded} rows after row {decoded} are white.";

        return (decoded, problem);
    }

    /// <summary>T.4 4.1.1 and T.6 2.2: runs of white and black, in turn, beginning with white.</summary>
    private string DecodeOneDimensional(ChangingElements row)
    {
        var position = 0;
        var isBlack = false;
        while (position < _columns)
        {
            if (ReadRun(isBlack) is not { } run)
                return $"no {(isBlack ? "black" : "white")} run is coded at column {position}";
            if ((long)position + run > _columns)
                return $"a run at column {position} goes past the row's {_columns} columns";

            position += run;
            row.Toggle(position, _columns);
            isBlack = !isBlack;
        }

        return null;
    }

    /// <summary>
    /// T.4 4.2.1.3 and T.6 2.2: each changing element is coded by where it is against the row
    /// above. a0 is where the row is decoded to, b1 the first changing element of the row above
    /// past a0 that changes to the colour a0 is not, and b2 the next one after b1.
    /// </summary>
    private string DecodeTwoDimensional(ChangingElements reference, ChangingElements row)
    {
        reference.EndAt(_columns);
        var a0 = -1;
        var past = 0;
        while (a0 < _columns)
        {
            var isBlack = row.EndsBlack;
            while (reference[past] <= a0)
                past++;

            // An element at an even index changes to black, and one at an odd index to white.
            var b1Index = ((past & 1) == 1) == isBlack ? past : past + 1;
            var b1 = reference[b1Index];
            var b2 = reference[b1Index + 1];
            var from = Math.Max(a0, 0);

            switch (ReadMode())
            {
                case (CodingMode.Pass, _):
                    // The run of a0's colour goes on below b2.
                    a0 = b2;
                    break;

                case (CodingMode.Horizontal, _):
                    // Two runs, a0's colour and then the other, coded as in one dimension.
                    if (ReadRun(isBlack) is not { } first || ReadRun(!isBlack) is not { } second)
                        return $"no run is coded at column {from}";
                    if ((long)from + first + second > _columns)
                        return $"a run at column {from} goes past the row's {_columns} columns";

                    row.Toggle(from + first, _columns);
                    row.Toggle(from + first + second, _columns);
                    a0 = from + first + second;
                    break;

                case (CodingMode.Vertical, var offset):
                    // a1 is within three of b1. Past the row's end it is the row's end.
                    var a1 = (int)Math.Min((long)b1 + offset, _columns);
                    if (a1 < from)
                        return $"a changing element is coded at column {a1}, before column {from} that the row is decoded to";

                    row.Toggle(a1, _columns);
                    a0 = a1;
                    break;

                default:
                    return $"no mode is coded at column {from}";
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the mode code of T.4 Table 4 that the next bits begin with, and for a vertical mode
    /// the offset of a1 from b1; or reads nothing and answers null where they begin with none.
    /// </summary>
    private (CodingMode Mode, int Offset)? ReadMode()
    {
        (int Length, CodingMode Mode, int Offset)? code = Peek(7) switch
        {
            >= 0b1000000 => (1, CodingMode.Vertical, 0),
            >= 0b0110000 => (3, CodingMode.Vertical, 1),
            >= 0b0100000 => (3, CodingMode.Vertical, -1),
            >= 0b0010000 => (3, CodingMode.Horizontal, 0),
            >= 0b0001000 => (4, CodingMode.Pass, 0),
            >= 0b0000110 => (6, CodingMode.Vertical, 2),
            >= 0b0000100 => (6, CodingMode.Vertical, -2),
            0b0000011 => (7, CodingMode.Vertical, 3),
            0b0000010 => (7, CodingMode.Vertical, -3),
            _ => null,
        };

        if (code is not { } found)
            return null;

        Skip(found.Length);
        return (found.Mode, found.Offset);
    }

    /// <summary>
    /// A run of one colour: any make-up codes and then a terminating code; or null where the bits
    /// are no code, or the run is longer than a row.
    /// </summary>
    private int? ReadRun(bool isBlack)
    {
        long run = 0;
        while (true)
        {
            var code = isBlack
                ? CcittFaxCodes.Black[Peek(CcittFaxCodes.BlackBits)]
                : CcittFaxCodes.White[Peek(CcittFaxCodes.WhiteBits)];
            if (code.Length == 0)
                return null;

            Skip(code.Length);
            run += code.Run;
            if (run > _columns)
                return null;
            if (!code.IsMakeUp)
                return (int)run;
        }
    }

    private void Pack(ChangingElements row, Span<byte> pixels, bool blackIs1)
    {
        for (var index = 0; index < row.Count; index += 2)
            PaintBlack(pixels, row[index], index + 1 < row.Count ? row[index + 1] : _columns, blackIs1);
    }

    private static void PaintBlack(Span<byte> pixels, int from, int to, bool blackIs1)
    {
        while (from < to)
        {
            var count = Math.Min(8 - (from & 7), to - from);
            var bits = (byte)((0xFF >> (8 - count)) << (8 - (from & 7) - count));
            if (blackIs1)
                pixels[from >> 3] |= bits;
            else
                pixels[from >> 3] &= (byte)~bits;

            from += count;
            if ((from & 7) != 0 || to - from < 8)
                continue;

            var whole = (to - from) >> 3;
            pixels.Slice(from >> 3, whole).Fill(blackIs1 ? (byte)0xFF : (byte)0x00);
            from += whole << 3;
        }
    }

    /// <summary>
    /// Steps over the fill and the end-of-line code before a row, if there are any, and over the
    /// bits to the next byte where each row begins on one.
    /// </summary>
    /// <remarks>
    /// Where rows begin on a byte, so does whatever follows an end-of-line code, which therefore
    /// ends on one. That is how it is told from zeros that pad a row to its byte followed by a
    /// row's first code, which may begin with seven more. A white run of 1792 is coded
    /// <c>00000001000</c>, and four zeros of padding before it look like an end-of-line code that
    /// ends on a byte. So the code is looked for before the padding only where
    /// <c>/EndOfLine</c> says to expect one, and otherwise after it, where no row begins with 11
    /// zeros.
    /// </remarks>
    private RowStart SkipToRow(bool isByteAligned, bool expectsEndOfLine)
    {
        if (!isByteAligned)
            return SkipEndOfLine();

        if (expectsEndOfLine && SkipEndOfLine(endsOnAByte: true) is var start and not RowStart.Data)
            return start;

        _at = (_at + 7) & ~7L;
        return SkipEndOfLine();
    }

    /// <summary>
    /// Steps over an end-of-line code, which is at least 11 zeros and a one, the zeros past 11
    /// being fill; or over nothing, where there is none. Zeros that run to the end of the data are
    /// the padding after the last row.
    /// </summary>
    private RowStart SkipEndOfLine(bool endsOnAByte = false)
    {
        var zeros = 0L;
        while (_at + zeros < _length && ReadBitAt(_at + zeros) == 0)
            zeros++;

        if (_at + zeros >= _length)
            return RowStart.EndOfData;
        if (zeros < EndOfLineZeros || (endsOnAByte && ((_at + zeros + 1) & 7) != 0))
            return RowStart.Data;

        _at += zeros + 1;
        return RowStart.EndOfLine;
    }

    private bool SeekEndOfLine()
    {
        var zeros = 0;
        for (var at = _at; at < _length; at++)
        {
            if (ReadBitAt(at) == 0)
            {
                zeros++;
                continue;
            }

            if (zeros >= EndOfLineZeros)
            {
                _at = at - zeros;
                return true;
            }

            zeros = 0;
        }

        return false;
    }

    private bool IsRestEmpty() => SkipEndOfLine() != RowStart.Data;

    /// <summary>Up to 13 bits; zeros past the data.</summary>
    private int Peek(int count)
    {
        if (_at >= _length)
            return 0;

        var index = (int)(_at >> 3);
        var window = _data[index] << 16;
        if (index + 1 < _data.Length)
            window |= _data[index + 1] << 8;
        if (index + 2 < _data.Length)
            window |= _data[index + 2];

        return (window >> (24 - (int)(_at & 7) - count)) & ((1 << count) - 1);
    }

    private void Skip(int count) => _at += count;

    private int ReadBit()
    {
        var bit = _at < _length ? ReadBitAt(_at) : 0;
        _at++;
        return bit;
    }

    private int ReadBitAt(long at) => (_data[at >> 3] >> (7 - (int)(at & 7))) & 1;

    /// <summary>
    /// The changing elements of one row, strictly increasing and all within it. As a reference
    /// row it is followed by the row's width, as T.4 4.2.1.3.1 puts an imaginary changing element
    /// just past the end of the row.
    /// </summary>
    private sealed class ChangingElements
    {
        /// <summary>
        /// How many times <see cref="EndAt"/> puts the width after the elements: b1 may be the
        /// second of them, when the row ends in the colour b1 must change to, and b2 the third.
        /// </summary>
        private const int Ends = 3;

        private int[] _items = new int[64];

        internal int Count { get; private set; }

        internal bool EndsBlack => (Count & 1) == 1;

        internal int this[int index] => _items[index];

        internal void Clear() => Count = 0;

        /// <summary>
        /// Adds a changing element, or takes one away where the last is at the same place: a run
        /// of no length is two changes that undo each other. One at the row's end is no change.
        /// </summary>
        internal void Toggle(int position, int columns)
        {
            if (position >= columns)
                return;

            if (Count > 0 && _items[Count - 1] == position)
            {
                Count--;
                return;
            }

            if (Count + 1 + Ends > _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = position;
        }

        internal void EndAt(int columns) => _items.AsSpan(Count, Ends).Fill(columns);
    }
}
