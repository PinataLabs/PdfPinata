using System.IO;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// The data of a stream that is still in the file a document opened with
/// <see cref="PdfDocumentOpenMode.Lazy"/> reads from: which file, where in it, and how much.
/// </summary>
/// <remarks>
/// It says nothing of which document the stream belongs to. A stream cloned into another
/// document keeps the same one, and so still reads from the document it was cloned from, which
/// has to be open for as long as the data has not been read or written.
/// </remarks>
internal sealed class DeferredStreamBytes
{
    private readonly LazyObjectSource _source;

    internal DeferredStreamBytes(LazyObjectSource source, PdfObjectID objectID, long offset, long length)
    {
        _source = source;
        ObjectID = objectID;
        Offset = offset;
        Length = length;
    }

    /// <summary>What the data is read from.</summary>
    internal LazyObjectSource Source => _source;

    /// <summary>The object the stream is in the source document, which is what an error names.</summary>
    internal PdfObjectID ObjectID { get; }

    internal long Offset { get; }

    internal long Length { get; }

    /// <summary>Reads the data into an array, which a stream longer than an array cannot be.</summary>
    internal byte[] Read() => _source.ReadStreamBytes(this);

    /// <summary>Copies the data to <paramref name="output"/> a chunk at a time, as it is in the file.</summary>
    internal void CopyTo(Stream output) => _source.CopyStreamBytes(this, output);
}
