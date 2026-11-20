using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPinata.Test.IO;

/// <summary>
///   Somewhere to save a document of several gigabytes: a write of one repeated byte is kept as
///   its length alone, which is what the data of a <see cref="SplicedPdf.StreamOfFiller" /> is
///   written as, and everything else is kept as written. What was saved can then be read back.
/// </summary>
internal sealed class RunCollapsingStream : Stream
{
    /// <summary>Shorter than any chunk a stream is copied in, and longer than a run a document writes by chance.</summary>
    private const int ShortestRun = 1024;

    private readonly List<SplicedStream.Part> _parts = [];
    private MemoryStream _bytes = new();
    private long _length;

    /// <summary>What was written, as a stream to read it back from.</summary>
    internal SplicedStream Written()
    {
        KeepBytes();
        return new SplicedStream(_parts);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _length;

    public override long Position
    {
        get => _length;
        // A save rewinds the stream it was given once it has finished.
        set { }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var written = buffer.AsSpan(offset, count);
        _length += count;

        if (count < ShortestRun || written.IndexOfAnyExcept(written[0]) >= 0)
        {
            _bytes.Write(written);
            return;
        }

        KeepBytes();
        if (_parts.Count > 0 && _parts[^1] is { Bytes: null } run && run.Filler == written[0])
            _parts[^1] = SplicedStream.Part.Run(run.Length + count, run.Filler);
        else
            _parts.Add(SplicedStream.Part.Run(count, written[0]));
    }

    private void KeepBytes()
    {
        if (_bytes.Length == 0)
            return;

        _parts.Add(SplicedStream.Part.Of(_bytes.ToArray()));
        _bytes = new MemoryStream();
    }

    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
