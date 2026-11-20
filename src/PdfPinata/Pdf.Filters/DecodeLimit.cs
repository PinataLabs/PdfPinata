using System;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// A cap on how many bytes a filter may produce, and a record of whether it was reached.
/// </summary>
/// <remarks>
/// A Flate or LZW stream a few kilobytes long can inflate to gigabytes, so a reader that knows how
/// much it expects, or how much it is willing to hold, says so before decoding rather than after.
/// A decode with no limit behaves as it always did. One instance belongs to one decode, or one
/// chain of decodes on one thread: <see cref="Truncated"/> is set by whichever filter was cut off
/// and stays set.
/// </remarks>
public sealed class DecodeLimit
{
    /// <summary>
    /// Initializes a limit of <paramref name="maximumLength"/> bytes.
    /// </summary>
    public DecodeLimit(long maximumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        MaximumLength = maximumLength;
    }

    /// <summary>
    /// The most bytes a filter may produce. The cap applies to what the Flate or LZW decoder
    /// itself produces, before any predictor is undone, so for a PNG predictor it counts the
    /// filter-type byte in front of each row.
    /// </summary>
    public long MaximumLength { get; }

    /// <summary>
    /// Whether a filter stopped at <see cref="MaximumLength"/> with more to give, so that the
    /// decoded bytes are the start of the stream and not all of it.
    /// </summary>
    public bool Truncated { get; internal set; }
}
