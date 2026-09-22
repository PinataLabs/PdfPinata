using System.IO;

namespace PdfPinata.Internal;

internal static class StreamHelper
{
    /// <summary>
    /// Reads until <paramref name="count"/> bytes have been read or the stream ends, and
    /// returns the number of bytes actually read.
    /// </summary>
    /// <remarks>
    /// A single <see cref="Stream.Read(byte[], int, int)"/> is allowed to return fewer bytes
    /// than requested even when more are available - buffered, compressed and crypto streams
    /// routinely do - so reading in a loop is the only way to fill a buffer reliably (CA2022).
    /// Callers that tolerate a truncated stream keep the untouched remainder of the buffer,
    /// which is how the single-shot reads this replaced already behaved.
    /// </remarks>
    public static int ReadUpTo(Stream stream, byte[] buffer, int offset, int count)
    {
        var total = 0;

        while (total < count)
        {
            var read = stream.Read(buffer, offset + total, count - total);
            if (read <= 0)
                break;

            total += read;
        }

        return total;
    }
}
