using System;
using System.IO;

namespace PdfPinata.Test.IO;

/// <summary>
///   A read-only, seekable stream over a head, a run of one repeated byte and a tail, so that
///   a document of several gigabytes costs a few hundred bytes to hold.
/// </summary>
internal sealed class SplicedStream : Stream
{
    private readonly byte[] _head;
    private readonly byte[] _tail;
    private readonly long _fillerLength;
    private readonly byte _filler;

    internal SplicedStream(byte[] head, long fillerLength, byte filler, byte[] tail)
    {
        _head = head;
        _fillerLength = fillerLength;
        _filler = filler;
        _tail = tail;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _head.Length + _fillerLength + _tail.Length;

    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var fillerEnd = _head.Length + _fillerLength;
        var total = 0;

        while (total < count && Position < Length)
        {
            int taken;
            if (Position < _head.Length)
            {
                taken = (int)Math.Min(count - total, _head.Length - Position);
                Array.Copy(_head, Position, buffer, offset + total, taken);
            }
            else if (Position < fillerEnd)
            {
                taken = (int)Math.Min(count - total, fillerEnd - Position);
                Array.Fill(buffer, _filler, offset + total, taken);
            }
            else
            {
                taken = (int)Math.Min(count - total, Length - Position);
                Array.Copy(_tail, Position - fillerEnd, buffer, offset + total, taken);
            }

            Position += taken;
            total += taken;
        }

        return total;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => Length + offset
        };
        return Position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
