using System;
using System.IO;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Reads the two spans a signature's <c>/ByteRange</c> names as though they were one stream.
/// </summary>
/// <remarks>
/// The bytes a PDF signature covers are the whole file with a hole cut out of it, and the hole is
/// where the signature itself goes. Presenting the two remaining spans as a single stream is what
/// lets <see cref="IPdfSigner.Sign"/> take a stream at all, and it means a signer that hashes
/// incrementally never needs a second copy of a document that may be tens of megabytes.
/// </remarks>
sealed class ByteRangeStream : Stream
{
    readonly byte[] _buffer;
    readonly int _firstOffset;
    readonly int _firstLength;
    readonly int _secondOffset;
    readonly int _secondLength;
    long _position;

    public ByteRangeStream(byte[] buffer, int firstOffset, int firstLength, int secondOffset, int secondLength)
    {
        _buffer = buffer;
        _firstOffset = firstOffset;
        _firstLength = firstLength;
        _secondOffset = secondOffset;
        _secondLength = secondLength;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _firstLength + _secondLength;

    public override long Position
    {
        get => _position;
        set
        {
            if (value < 0 || value > Length)
                throw new ArgumentOutOfRangeException(nameof(value));
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        var copied = 0;
        while (count > 0 && _position < Length)
        {
            // Which span the current position falls in, and how much of it is left. The loop runs at
            // most twice, once for each span, and once more to notice there is nothing left.
            var inSecond = _position >= _firstLength;
            var source = inSecond ? _secondOffset + (int)(_position - _firstLength) : _firstOffset + (int)_position;
            var available = inSecond ? _secondLength - (int)(_position - _firstLength) : _firstLength - (int)_position;

            var take = Math.Min(count, available);
            Array.Copy(_buffer, source, buffer, offset, take);

            offset += take;
            count -= take;
            copied += take;
            _position += take;
        }

        return copied;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        Position = target;
        return _position;
    }

    public override void Flush()
    { }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
