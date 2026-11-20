using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Passes every read through and remembers where it fell, which is what says whether a reader
///   took a file a buffer at a time, all at once, or only the parts it was asked about.
/// </summary>
internal sealed class RecordingStream : Stream
{
    private readonly Stream _inner;
    private readonly List<(long Start, long End)> _ranges = [];

    internal RecordingStream(Stream inner) => _inner = inner;

    /// <summary>The largest count any one read asked for.</summary>
    internal int LargestRead { get; private set; }

    /// <summary>How many bytes have been handed out since the last <see cref="Forget" />.</summary>
    internal long BytesRead { get; private set; }

    /// <summary>Whether any byte of <paramref name="range" />, which stops short of its end, has been read.</summary>
    internal bool HasRead((long Start, long End) range)
    {
        foreach (var (start, end) in _ranges)
        {
            if (start < range.End && range.Start < end)
                return true;
        }

        return false;
    }

    /// <summary>Starts the record again, so that what follows is counted on its own.</summary>
    internal void Forget()
    {
        _ranges.Clear();
        BytesRead = 0;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        LargestRead = Math.Max(LargestRead, count);
        var start = _inner.Position;
        var read = _inner.Read(buffer, offset, count);
        if (read > 0)
            Record(start, start + read);
        return read;
    }

    private void Record(long start, long end)
    {
        BytesRead += end - start;

        // A lexer reads a byte at a time, so nearly every read continues the one before it.
        if (_ranges.Count > 0 && _ranges[^1].End == start)
            _ranges[^1] = (_ranges[^1].Start, end);
        else
            _ranges.Add((start, end));
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
