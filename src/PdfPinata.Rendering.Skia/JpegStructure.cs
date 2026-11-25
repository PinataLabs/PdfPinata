using System;

namespace PdfPinata.Rendering.Skia;

/// <summary>
/// Walks the markers of a JPEG and answers whether Skia's codec would decode it without
/// libjpeg-turbo raising a fatal error.
/// </summary>
/// <remarks>
/// On Windows x64, SkiaSharp 4.156.0 to 4.159.0-rc.1 end the process with an access violation
/// whenever libjpeg-turbo raises a fatal error, in <c>SKCodec.Create</c> as much as in
/// <c>SKCodec.GetPixels</c>: a table that is malformed or never defined, a scan naming a component
/// the frame lacks, a marker libjpeg-turbo does not know. SkiaSharp 3.119 answers null or
/// <c>InvalidInput</c> for the same files. No exception handler can catch an access violation, so
/// this reads the file first and refuses what would be fatal there. A file whose data is merely
/// corrupt or cut short draws only a warning, and is not refused here.
/// <para>
/// Each check is one of the fatal errors of libjpeg-turbo 3.1's <c>jdmarker.c</c>,
/// <c>jdinput.c</c>, <c>jdhuff.c</c>, <c>jdphuff.c</c> and <c>jdsample.c</c>, named by its
/// <c>JERR_</c> code, or one of the three places Skia's <c>SkJpegCodec</c> raises one itself.
/// Where a check is stricter than the decoder, it is because the other branch cannot be told
/// apart without decoding:
/// </para>
/// <list type="bullet">
/// <item>A segment must end inside the file. One that is skipped unread is fatal when it does
/// not, and one that is read only stops the decoder, which is not told apart here.</item>
/// <item>A table is checked when it is defined, not when it is used.</item>
/// <item>Only 8-bit Huffman frames of one, three or four components are accepted. An
/// arithmetic-coded frame is refused although Skia decodes a whole one, because that decoder
/// treats running out of data as fatal. A lossless frame is refused as not being DCT at all.</item>
/// <item>A progressive scan of first DC bits is refused when its table and its size together
/// allow the running DC value to overflow (<c>JERR_BAD_DCT_COEF</c>), whether or not the data
/// does. With the tables encoders write that is an image above 67 million samples.</item>
/// </list>
/// <para>
/// The walk stops where the decoder stops reading. A sequential frame whose first scan holds
/// every component is decoded from that scan and nothing after it is read, so nothing after it is
/// checked. A progressive frame, or one with a scan for each component, is read to its end before
/// a row is answered, so every marker of it is checked. Between markers the walk looks only for
/// the next 0xFF, so it is linear in the file.
/// </para>
/// </remarks>
internal static class JpegStructure
{
    private const int MaximumDimension = 65500;
    private const int MaximumBlocksInMcu = 10;

    // SkJpegDecoderMgr's progress monitor raises a fatal error at the hundredth scan.
    private const int MaximumScans = 99;

    /// <summary>Whether <paramref name="jpeg"/> is a JPEG Skia's codec decodes without a fatal error.</summary>
    internal static bool IsWellFormed(ReadOnlySpan<byte> jpeg)
    {
        // Skia takes a file for a JPEG by these three bytes, and tries its other codecs otherwise.
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[2] != 0xFF)
            return false;

        var frame = new Frame();
        var at = 2;
        while (true)
        {
            at = NextMarker(jpeg, at);
            if (at < 0)
                return frame.Scans > 0;

            // JERR_NO_IMAGE and JERR_SOF_NO_SOS for an end of image before any scan.
            var marker = jpeg[at + 1];
            if (marker == 0xD9)
                return frame.Scans > 0;

            // JERR_SOI_DUPLICATE, JERR_SOF_UNSUPPORTED and JERR_UNKNOWN_MARKER.
            if (!IsSegment(marker))
                return false;

            if (jpeg.Length - at < 4)
                return false;

            var length = (jpeg[at + 2] << 8) | jpeg[at + 3];
            if (length < 2 || length > jpeg.Length - at - 2)
                return false;

            var body = jpeg.Slice(at + 4, length - 2);
            var read = marker switch
            {
                0xC0 or 0xC1 or 0xC2 => frame.ReadFrame(body, progressive: marker == 0xC2),
                0xC4 => frame.ReadHuffmanTables(body),
                0xDB => frame.ReadQuantizationTables(body),
                0xDD => frame.ReadRestartInterval(body),
                0xDA => frame.ReadScan(body),
                _ => true,
            };

            if (!read)
                return false;

            if (frame.Scans > 0 && !frame.HasMoreScans)
                return true;

            at += 2 + length;
        }
    }

    // The offset of the next marker's 0xFF at or after at, or -1 when the file ends first.
    // libjpeg-turbo skips anything else between markers with a warning, which inside a scan is its
    // entropy-coded data: a stuffed zero, a fill byte and a restart marker are none of them markers.
    private static int NextMarker(ReadOnlySpan<byte> jpeg, int at)
    {
        while (at < jpeg.Length)
        {
            var found = jpeg[at..].IndexOf((byte)0xFF);
            if (found < 0)
                return -1;

            at += found;
            var next = at + 1;
            while (next < jpeg.Length && jpeg[next] == 0xFF)
                next++;

            if (next >= jpeg.Length)
                return -1;

            var marker = jpeg[next];
            if (marker == 0x00 || marker == 0x01 || marker is >= 0xD0 and <= 0xD7)
            {
                at = next + 1;
                continue;
            }

            return next - 1;
        }

        return -1;
    }

    // Markers with a length, among those accepted at all: the frames, tables and scans read
    // below, and the DNL, COM and APPn the decoder skips or saves.
    private static bool IsSegment(byte marker) =>
        marker is 0xC0 or 0xC1 or 0xC2 or 0xC4 or 0xDA or 0xDB or 0xDC or 0xDD or 0xFE or >= 0xE0 and <= 0xEF;

    private sealed class Frame
    {
        private readonly byte[] _ids = new byte[4];
        private readonly byte[] _horizontal = new byte[4];
        private readonly byte[] _vertical = new byte[4];
        private readonly byte[] _quantization = new byte[4];
        private readonly byte[] _largestDcSymbol = new byte[4];
        private int _width;
        private int _height;
        private int _widest;
        private int _tallest;
        private int _components;
        private bool _progressive;
        private int _quantizationTables;
        private int _dcTables;
        private int _acTables;
        private int _latched;
        private int _restartInterval;

        internal int Scans { get; private set; }

        /// <summary>Whether libjpeg-turbo reads past the first scan: has_multiple_scans in jdinput.c.</summary>
        internal bool HasMoreScans { get; private set; }

        private bool HasFrame => _components > 0;

        internal bool ReadFrame(ReadOnlySpan<byte> body, bool progressive)
        {
            // JERR_SOF_DUPLICATE, then JERR_BAD_LENGTH and JERR_BAD_PRECISION.
            if (HasFrame || body.Length < 6 || body[0] != 8)
                return false;

            // JERR_EMPTY_IMAGE and JERR_IMAGE_TOO_BIG.
            _height = (body[1] << 8) | body[2];
            _width = (body[3] << 8) | body[4];
            var components = body[5];
            if (_height == 0 || _width == 0 || _height > MaximumDimension || _width > MaximumDimension)
                return false;

            if (components is not (1 or 3 or 4) || body.Length != 6 + 3 * components)
                return false;

            _widest = _tallest = 1;
            for (var c = 0; c < components; c++)
            {
                var entry = body.Slice(6 + 3 * c, 3);
                _ids[c] = entry[0];
                _horizontal[c] = (byte)(entry[1] >> 4);
                _vertical[c] = (byte)(entry[1] & 0x0F);
                _quantization[c] = entry[2];

                // JERR_BAD_SAMPLING.
                if (_horizontal[c] is < 1 or > 4 || _vertical[c] is < 1 or > 4)
                    return false;

                _widest = Math.Max(_widest, _horizontal[c]);
                _tallest = Math.Max(_tallest, _vertical[c]);
            }

            // JERR_FRACT_SAMPLE_NOTIMPL: each component is upsampled by a whole factor or not at all.
            for (var c = 0; c < components; c++)
            {
                if (_widest % _horizontal[c] != 0 || _tallest % _vertical[c] != 0)
                    return false;
            }

            _components = components;
            _progressive = progressive;
            return true;
        }

        internal bool ReadHuffmanTables(ReadOnlySpan<byte> body)
        {
            Span<int> counts = stackalloc int[17];
            var at = 0;
            while (body.Length - at > 16)
            {
                var index = body[at];
                var symbols = 0;
                for (var bits = 1; bits <= 16; bits++)
                {
                    counts[bits] = body[at + bits];
                    symbols += counts[bits];
                }

                at += 17;

                // JERR_BAD_HUFF_TABLE, then JERR_DHT_INDEX.
                if (symbols > 256 || symbols > body.Length - at)
                    return false;

                var isAc = (index & 0x10) != 0;
                var id = isAc ? index - 0x10 : index;
                if (id is < 0 or > 3)
                    return false;

                // jpeg_make_d_derived_tbl: the codes must fit their lengths, and a DC symbol is a
                // bit count of at most 15.
                var code = 0L;
                for (var bits = 1; bits <= 16; bits++)
                {
                    code += counts[bits];
                    if (counts[bits] > 0 && code >= 1L << bits)
                        return false;

                    code <<= 1;
                }

                if (isAc)
                {
                    _acTables |= 1 << id;
                }
                else
                {
                    byte largest = 0;
                    foreach (var symbol in body.Slice(at, symbols))
                        largest = Math.Max(largest, symbol);

                    if (largest > 15)
                        return false;

                    _largestDcSymbol[id] = largest;
                    _dcTables |= 1 << id;
                }

                at += symbols;
            }

            // JERR_BAD_LENGTH.
            return at == body.Length;
        }

        internal bool ReadQuantizationTables(ReadOnlySpan<byte> body)
        {
            var at = 0;
            while (at < body.Length)
            {
                // JERR_DQT_INDEX.
                var id = body[at] & 0x0F;
                if (id > 3)
                    return false;

                // JERR_BAD_LENGTH: a table is 64 values, of two bytes each under any precision but 0.
                at += 1 + (body[at] >> 4 != 0 ? 128 : 64);
                _quantizationTables |= 1 << id;
            }

            return at == body.Length;
        }

        internal bool ReadRestartInterval(ReadOnlySpan<byte> body)
        {
            // JERR_BAD_LENGTH.
            if (body.Length != 2)
                return false;

            _restartInterval = (body[0] << 8) | body[1];
            return true;
        }

        internal bool ReadScan(ReadOnlySpan<byte> body)
        {
            // JERR_SOS_NO_SOF, then Skia's limit on scans.
            if (!HasFrame || body.Length < 1 || Scans >= MaximumScans)
                return false;

            // JERR_BAD_LENGTH.
            var count = body[0];
            if (count is < 1 or > 4 || body.Length != 4 + 2 * count)
                return false;

            var start = body[1 + 2 * count];
            var end = body[2 + 2 * count];
            var high = body[3 + 2 * count] >> 4;
            var low = body[3 + 2 * count] & 0x0F;

            // JERR_BAD_PROGRESSION. A sequential scan's values are only warned about.
            if (_progressive)
            {
                var bad = start == 0 ? end != 0 : end < start || end > 63 || count != 1;
                if ((high != 0 && low != high - 1) || low > 13 || bad)
                    return false;
            }

            var needsDc = !_progressive || (start == 0 && high == 0);
            var needsAc = !_progressive || start != 0;
            var inScan = 0;
            var blocks = 0;
            for (var s = 0; s < count; s++)
            {
                // JERR_BAD_COMPONENT_ID. get_sos looks for the identifier among the frame's
                // components from the scan's own position on, so a scan naming them out of the
                // frame's order is as fatal as one naming a component the frame lacks.
                var selector = body[1 + 2 * s];
                var c = s < _components ? Array.IndexOf(_ids, selector, s, _components - s) : -1;
                if (c < 0 || (inScan & (1 << c)) != 0)
                    return false;

                inScan |= 1 << c;
                blocks += _horizontal[c] * _vertical[c];
                var dc = body[2 + 2 * s] >> 4;
                var ac = body[2 + 2 * s] & 0x0F;

                // JERR_NO_HUFF_TABLE. A sequential decoder supplies the standard tables 0 and 1
                // when a file defines none, and a progressive one does not.
                if (needsDc && !IsHuffmanTable(_dcTables, dc))
                    return false;

                if (needsAc && !IsHuffmanTable(_acTables, ac))
                    return false;

                // JERR_NO_QUANT_TABLE: a component's table is latched at its first scan.
                if ((_latched & (1 << c)) == 0)
                {
                    if (_quantization[c] > 3 || (_quantizationTables & (1 << _quantization[c])) == 0)
                        return false;

                    _latched |= 1 << c;
                }

                if (_progressive && needsDc && CanOverflowDc(c, dc, interleaved: count > 1))
                    return false;
            }

            // JERR_BAD_MCU_SIZE.
            if (count > 1 && blocks > MaximumBlocksInMcu)
                return false;

            if (Scans == 0)
                HasMoreScans = _progressive || count < _components;

            Scans++;
            return true;
        }

        private bool IsHuffmanTable(int defined, int id) =>
            id <= 3 && ((defined & (1 << id)) != 0 || (id <= 1 && !_progressive));

        // JERR_BAD_DCT_COEF: decode_mcu_DC_first adds a difference of up to the table's largest
        // symbol in bits to a running int for every block, from one restart to the next.
        private bool CanOverflowDc(int component, int table, bool interleaved)
        {
            long across, down, inUnit;
            if (interleaved)
            {
                across = Ceiling(_width, 8L * _widest);
                down = Ceiling(_height, 8L * _tallest);
                inUnit = _horizontal[component] * _vertical[component];
            }
            else
            {
                across = Ceiling((long)_width * _horizontal[component], 8L * _widest);
                down = Ceiling((long)_height * _vertical[component], 8L * _tallest);
                inUnit = 1;
            }

            var units = across * down;
            if (_restartInterval > 0)
                units = Math.Min(units, _restartInterval);

            var largestDifference = (1L << _largestDcSymbol[table]) - 1;
            return units * inUnit * largestDifference > int.MaxValue;
        }

        private static long Ceiling(long value, long divisor) => (value + divisor - 1) / divisor;
    }
}
