using System;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// A baseline JPEG of three components with one thing wrong in its header, named for what is wrong.
/// </summary>
/// <remarks>
/// Each defect is one libjpeg-turbo or Skia's codec treats as a fatal error, and each ended the
/// process with an access violation when SkiaSharp 4.156.0 decoded it on Windows x64. So no test
/// hands one to <c>SKCodec</c> itself: they go through the device, which must refuse them first.
/// </remarks>
internal static class CorruptJpeg
{
    public const string QuantizationTableNumberedFour = "a quantization table numbered 4";
    public const string QuantizationTableOfWideValuesInTheRoomOfNarrow = "a quantization table of 16-bit values in the room of 8-bit ones";
    public const string QuantizationTableNeverDefined = "a component whose quantization table is never defined";
    public const string HuffmanTableNumberedFour = "a Huffman table numbered 4";
    public const string HuffmanTableWhoseCodesDoNotFit = "a Huffman table whose codes do not fit their lengths";
    public const string HuffmanTableNeverDefined = "a scan whose Huffman table is never defined";
    public const string FrameWithNoWidth = "a frame with no width";
    public const string ComponentSampledZeroTimes = "a component sampled zero times";
    public const string SecondFrame = "a second frame";
    public const string FrameOfAnUnsupportedProcess = "a frame of a process no decoder here supports";
    public const string ScanOfAComponentTheFrameLacks = "a scan of a component the frame lacks";
    public const string ScanOutOfTheFramesOrder = "a scan naming its components out of the frame's order";
    public const string SegmentRunningPastTheEnd = "an application segment longer than the file";
    public const string UnknownMarker = "a marker that is not a JPEG marker";

    private const byte Quantization = 0xDB;
    private const byte Huffman = 0xC4;
    private const byte Frame = 0xC0;
    private const byte Scan = 0xDA;

    /// <summary>A copy of <paramref name="jpeg"/> with <paramref name="defect"/> in it.</summary>
    /// <param name="jpeg">A baseline JPEG of three components whose segments follow one another with nothing between.</param>
    /// <param name="defect">One of the constants of this class.</param>
    public static byte[] With(byte[] jpeg, string defect)
    {
        var copy = (byte[])jpeg.Clone();

        // A segment is its marker, two bytes of length, and its body from the fourth byte on.
        switch (defect)
        {
            case QuantizationTableNumberedFour:
                copy[SegmentOf(copy, Quantization) + 4] = 0x04;
                return copy;

            case QuantizationTableOfWideValuesInTheRoomOfNarrow:
                copy[SegmentOf(copy, Quantization) + 4] |= 0x10;
                return copy;

            // The frame's first component: its identifier, its sampling factors, its table.
            case QuantizationTableNeverDefined:
                copy[SegmentOf(copy, Frame) + 12] = 0x03;
                return copy;

            case HuffmanTableNumberedFour:
                copy[SegmentOf(copy, Huffman) + 4] = 0x04;
                return copy;

            // Every code is given a length of one bit, and one bit has two codes.
            case HuffmanTableWhoseCodesDoNotFit:
            {
                var counts = SegmentOf(copy, Huffman) + 5;
                var codes = 0;
                for (var length = 0; length < 16; length++)
                {
                    codes += copy[counts + length];
                    copy[counts + length] = 0;
                }

                copy[counts] = (byte)codes;
                return copy;
            }

            // The scan's first component: its identifier, then its two tables.
            case HuffmanTableNeverDefined:
                copy[SegmentOf(copy, Scan) + 6] = 0x33;
                return copy;

            case FrameWithNoWidth:
                copy[SegmentOf(copy, Frame) + 7] = 0;
                copy[SegmentOf(copy, Frame) + 8] = 0;
                return copy;

            case ComponentSampledZeroTimes:
                copy[SegmentOf(copy, Frame) + 11] = 0x00;
                return copy;

            case SecondFrame:
            {
                var at = SegmentOf(copy, Frame);
                var end = at + 2 + ((copy[at + 2] << 8) | copy[at + 3]);
                var twice = new byte[copy.Length + end - at];
                copy.AsSpan(0, end).CopyTo(twice);
                copy.AsSpan(at).CopyTo(twice.AsSpan(end));
                return twice;
            }

            // SOF5, a differential sequential frame.
            case FrameOfAnUnsupportedProcess:
                copy[SegmentOf(copy, Frame) + 1] = 0xC5;
                return copy;

            case ScanOfAComponentTheFrameLacks:
                copy[SegmentOf(copy, Scan) + 5] = 0x63;
                return copy;

            // The identifiers of the scan's first two components, each followed by its tables.
            case ScanOutOfTheFramesOrder:
            {
                var at = SegmentOf(copy, Scan);
                (copy[at + 5], copy[at + 7]) = (copy[at + 7], copy[at + 5]);
                return copy;
            }

            // The JFIF segment, which a decoder skips without reading.
            case SegmentRunningPastTheEnd:
            {
                var at = SegmentOf(copy, 0xE0);
                copy[at + 2] = 0xFF;
                copy[at + 3] = 0xFF;
                return copy;
            }

            // The first segment after the start of the image, whatever it was.
            case UnknownMarker:
                copy[3] = 0x02;
                return copy;

            default:
                throw new ArgumentOutOfRangeException(nameof(defect), defect, "Not a defect this class makes.");
        }
    }

    // The offset of the first segment with this marker, among those before the first scan's data.
    private static int SegmentOf(byte[] jpeg, byte marker)
    {
        var at = 2;
        while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
        {
            if (jpeg[at + 1] == marker)
                return at;

            at += 2 + ((jpeg[at + 2] << 8) | jpeg[at + 3]);
        }

        throw new InvalidOperationException($"The JPEG has no segment 0xFF{marker:X2} before its data.");
    }
}
