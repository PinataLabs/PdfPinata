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
    /// The most bytes the filter decodes to: 256 MiB, a page of A0 scanned at 1200 dots to the
    /// inch. Past it, a few bytes of Group 4 data saying "the same row again" could stand for any
    /// number of rows.
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

    /// <summary>The rows the image has, or 0 where they are as many as the data holds.</summary>
    internal int Rows { get; init; }

    internal bool BlackIs1 { get; init; }

    internal int DamagedRowsBeforeError { get; init; }

    /// <summary>
    /// The parameters a dictionary gives, each of which takes its default where it is absent or is
    /// not of its type.
    /// </summary>
    internal static CcittFaxParameters Read(PdfDictionary parms)
    {
        if (parms == null)
            return new CcittFaxParameters();

        var defaults = new CcittFaxParameters();
        var parameters = new CcittFaxParameters
        {
            K = Integer("/K", defaults.K),
            EndOfLine = Boolean("/EndOfLine", defaults.EndOfLine),
            EncodedByteAlign = Boolean("/EncodedByteAlign", defaults.EncodedByteAlign),
            Columns = Integer("/Columns", defaults.Columns),
            Rows = Integer("/Rows", defaults.Rows),
            BlackIs1 = Boolean("/BlackIs1", defaults.BlackIs1),
            DamagedRowsBeforeError = Math.Max(0, Integer("/DamagedRowsBeforeError", defaults.DamagedRowsBeforeError)),
        };

        if (parameters.Columns < 1)
            throw new InvalidOperationException($"CCITTFaxDecode's /Columns is {parameters.Columns}, and must be at least 1.");
        if (parameters.Rows < 0)
            throw new InvalidOperationException($"CCITTFaxDecode's /Rows is {parameters.Rows}, and must not be negative.");
        return parameters;

        int Integer(string key, int value) => PdfItemValues.TryGetInteger(parms.Elements[key], out var read) ? read : value;

        bool Boolean(string key, bool value) => PdfItemValues.TryGetBoolean(parms.Elements[key], out var read) ? read : value;
    }
}

/// <summary>
/// The rows a CCITTFaxDecode filter decoded, and what was wrong with the data, if anything.
/// </summary>
/// <param name="Data">The rows, 1 bit a pixel, each padded to a whole byte.</param>
/// <param name="Rows">How many rows <paramref name="Data"/> holds.</param>
/// <param name="DamagedRows">How many rows could not be decoded to their end.</param>
/// <param name="Problem">What was wrong with the data, as a sentence; or null where nothing was.</param>
internal sealed record CcittFaxResult(byte[] Data, int Rows, int DamagedRows, string Problem);

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

    /// <summary>
    /// Decodes <paramref name="rows"/> rows, white where the data holds no more; or, where
    /// <paramref name="rows"/> is 0, as many rows as the data holds.
    /// </summary>
    internal static CcittFaxResult Decode(byte[] data, CcittFaxParameters parameters, int rows)
    {
        var rowLength = (parameters.Columns + 7) / 8;
        var mostRows = CcittFaxDecode.MaximumLength / rowLength;
        if (rows > mostRows || mostRows == 0)
        {
            throw new InvalidOperationException(
                $"CCITTFaxDecode data of {parameters.Columns} columns and {rows} rows would be more than the {CcittFaxDecode.MaximumLength} bytes decoded.");
        }

        return new CcittFaxDecoder(data, parameters.Columns).DecodeRows(parameters, rows, rows == 0 ? mostRows : rows, rowLength);
    }

    private CcittFaxResult DecodeRows(CcittFaxParameters parameters, int rows, int rowLimit, int rowLength)
    {
        var white = parameters.BlackIs1 ? (byte)0x00 : (byte)0xFF;
        var output = new byte[(long)(rows > 0 ? rows : Math.Min(rowLimit, 64)) * rowLength];
        output.AsSpan().Fill(white);

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

            if ((long)(decoded + 1) * rowLength > output.Length)
            {
                var filled = output.Length;
                Array.Resize(ref output, (int)Math.Min((long)output.Length * 2, (long)rowLimit * rowLength));
                output.AsSpan(filled).Fill(white);
            }
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

        if (problem == null && rows == 0 && decoded == rowLimit && !IsRestEmpty())
            problem = $"holds more than the {decoded} rows of {_columns} columns decoded, which are {CcittFaxDecode.MaximumLength} bytes.";
        if (problem == null && rows > 0 && decoded < rows)
            problem = $"ends after {decoded} of its {rows} rows.";
        if (problem != null && damaged > 1)
            problem += $" {damaged} rows are damaged in all.";
        if (problem == null && damaged > 0)
            problem = $"has {damaged} damaged rows, each decoded as far as it goes.";
        if (problem != null && rows > decoded)
            problem += $" The {rows - decoded} rows after it are white.";

        if (rows == 0 && output.Length != (long)decoded * rowLength)
            Array.Resize(ref output, decoded * rowLength);
        return new CcittFaxResult(output, rows > 0 ? rows : decoded, damaged, problem);
    }

    // ----- Rows ----------------------------------------------------------------------------

    /// <summary>T.4 4.1.1 and T.6 2.2: runs of white and black, in turn, beginning with white.</summary>
    private string DecodeOneDimensional(ChangingElements row)
    {
        var position = 0;
        var isBlack = false;
        while (position < _columns)
        {
            var run = ReadRun(isBlack);
            if (run < 0)
                return $"no {(isBlack ? "black" : "white")} run is coded at column {position}";
            if (position + run > _columns)
                return $"a run at column {position} goes past the row's {_columns} columns";

            position += run;
            row.Add(position, _columns);
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
            // The colour at a0 is the colour after the changing elements so far, white for none.
            var colour = row.Count & 1;
            while (reference[past] <= a0)
                past++;
            var b1Index = (past & 1) == colour ? past : past + 1;
            var b1 = reference[b1Index];
            var b2 = reference[b1Index + 1];

            var mode = Peek(7);
            if (mode >= 0b0001000 && mode < 0b0010000)
            {
                // Pass: the run of a0's colour goes on below b2.
                Skip(4);
                a0 = b2;
                continue;
            }

            if (mode >= 0b0010000 && mode < 0b0100000)
            {
                // Horizontal: two runs, a0's colour and then the other, coded as in one dimension.
                Skip(3);
                var from = Math.Max(a0, 0);
                var first = ReadRun(colour == 1);
                var second = first < 0 ? -1 : ReadRun(colour == 0);
                if (second < 0)
                    return $"no run is coded at column {from}";
                if ((long)from + first + second > _columns)
                    return $"a run at column {from} goes past the row's {_columns} columns";

                row.Add(from + first, _columns);
                row.Add(from + first + second, _columns);
                a0 = from + first + second;
                continue;
            }

            var offset = VerticalOffset(mode);
            if (offset == null)
                return $"no mode is coded at column {Math.Max(a0, 0)}";

            // Vertical: a1 is within three of b1. Past the row's end it is the row's end.
            var a1 = Math.Min(b1 + offset.Value, _columns);
            if (a1 < Math.Max(a0, 0))
                return $"a changing element is coded at column {a1}, before column {Math.Max(a0, 0)} that the row is decoded to";

            row.Add(a1, _columns);
            a0 = a1;
        }

        return null;
    }

    /// <summary>
    /// The offset of a1 from b1 that a vertical mode code at the next 7 bits stands for, having
    /// read the code; or null where they are not one.
    /// </summary>
    private int? VerticalOffset(int bits)
    {
        (int Length, int Offset)? code = bits switch
        {
            >= 0b1000000 => (1, 0),
            >= 0b0110000 => (3, 1),
            >= 0b0100000 => (3, -1),
            >= 0b0000110 and < 0b0001000 => (6, 2),
            >= 0b0000100 and < 0b0000110 => (6, -2),
            0b0000011 => (7, 3),
            0b0000010 => (7, -3),
            _ => null,
        };

        if (code is not { } found)
            return null;

        Skip(found.Length);
        return found.Offset;
    }

    /// <summary>
    /// A run of one colour: any make-up codes and then a terminating code; or -1 where the bits
    /// are no code, or the run is longer than a row.
    /// </summary>
    private int ReadRun(bool isBlack)
    {
        var run = 0;
        while (true)
        {
            var entry = isBlack
                ? CcittFaxCodes.Black[Peek(CcittFaxCodes.BlackBits)]
                : CcittFaxCodes.White[Peek(CcittFaxCodes.WhiteBits)];
            if (entry == 0)
                return -1;

            Skip(entry & 0xFF);
            var length = entry >> 8;
            run += length;
            if (run > _columns)
                return -1;
            if (length < CcittFaxCodes.FirstMakeUp)
                return run;
        }
    }

    /// <summary>A row's pixels from its changing elements: white up to the first, then each in turn.</summary>
    private void Pack(ChangingElements row, Span<byte> pixels, bool blackIs1)
    {
        for (var index = 0; index < row.Count; index += 2)
            Paint(pixels, row[index], index + 1 < row.Count ? row[index + 1] : _columns, blackIs1);
    }

    /// <summary>Makes the pixels from <paramref name="from"/> up to <paramref name="to"/> black.</summary>
    private static void Paint(Span<byte> pixels, int from, int to, bool blackIs1)
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

    // ----- End-of-line codes and fill --------------------------------------------------------

    private enum RowStart
    {
        Data,
        EndOfLine,
        EndOfData,
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

    /// <summary>Moves to the next end-of-line code, past a damaged row; false where there is none.</summary>
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

    // ----- Bits ------------------------------------------------------------------------------

    /// <summary>The next <paramref name="count"/> bits, up to 13, as a number; zeros past the data.</summary>
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
        private int[] _items = new int[64];

        internal int Count { get; private set; }

        internal int this[int index] => _items[index];

        internal void Clear() => Count = 0;

        /// <summary>
        /// Adds a changing element, or takes one away where the last is at the same place: a run
        /// of no length is two changes that undo each other. One at the row's end is no change.
        /// </summary>
        internal void Add(int position, int columns)
        {
            if (position >= columns)
                return;

            if (Count > 0 && _items[Count - 1] == position)
            {
                Count--;
                return;
            }

            // Room for this one and the three EndAt puts after it.
            if (Count + 4 > _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            _items[Count++] = position;
        }

        /// <summary>Puts the row's width after its changing elements, three times, for b1 and b2.</summary>
        internal void EndAt(int columns)
        {
            _items[Count] = columns;
            _items[Count + 1] = columns;
            _items[Count + 2] = columns;
        }
    }
}
