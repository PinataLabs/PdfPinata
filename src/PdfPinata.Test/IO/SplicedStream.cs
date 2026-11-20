using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPinata.Test.IO;

/// <summary>
///   A read-only, seekable stream over parts that are either bytes or a run of one repeated
///   byte, so that a document of several gigabytes costs a few hundred bytes to hold.
/// </summary>
internal sealed class SplicedStream : Stream
{
    /// <summary>One part of the stream: the bytes given, or <see cref="Length" /> of <see cref="Filler" />.</summary>
    internal readonly record struct Part(byte[] Bytes, long Length, byte Filler)
    {
        internal static Part Of(byte[] bytes) => new(bytes, bytes.Length, 0);

        internal static Part Run(long length, byte filler) => new(null, length, filler);
    }

    private readonly List<(long Start, Part Part)> _parts = [];
    private int _current;

    internal SplicedStream(byte[] head, long fillerLength, byte filler, byte[] tail)
        : this([Part.Of(head), Part.Run(fillerLength, filler), Part.Of(tail)])
    { }

    internal SplicedStream(IEnumerable<Part> parts)
    {
        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;

            _parts.Add((Length, part));
            Length += part.Length;
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length { get; }

    public override long Position { get; set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count && Position < Length)
        {
            var (start, part) = PartAtPosition();
            var within = Position - start;
            var taken = (int)Math.Min(count - total, part.Length - within);
            if (part.Bytes == null)
                Array.Fill(buffer, part.Filler, offset + total, taken);
            else
                Array.Copy(part.Bytes, within, buffer, offset + total, taken);

            Position += taken;
            total += taken;
        }

        return total;
    }

    /// <summary>The part <see cref="Position" /> falls in, found from the one the last read fell in.</summary>
    private (long Start, Part Part) PartAtPosition()
    {
        while (Position < _parts[_current].Start)
            _current--;
        while (Position >= _parts[_current].Start + _parts[_current].Part.Length)
            _current++;
        return _parts[_current];
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
