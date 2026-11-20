using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// What a document opened with <see cref="PdfDocumentOpenMode.Lazy"/> reads its objects from, one
/// at a time, as <see cref="PdfReference.Value"/> is asked for each.
/// </summary>
/// <remarks>
/// <para>
/// The cross-reference table is the cache: an object read once is held by its reference for the
/// document's lifetime, and this class is not asked about it again. What a reference has not read
/// is told from its two fields, as the eager reader tells it. A position of zero or more is an
/// object in the file. A position of -1 is an object in an object stream when
/// <see cref="_compressed"/> has its number, and the null object when it does not.
/// </para>
/// <para>
/// A document is not thread-safe and this does not make it so. Every read moves the one source
/// stream, so a whole read runs under a lock: two threads then wait for each other, where without
/// it each would silently read from where the other had left the stream.
/// </para>
/// </remarks>
internal sealed class LazyObjectSource : IDisposable
{
    /// <summary>
    /// How many decoded object streams are kept. Most large files keep their dictionaries in
    /// object streams, and decoding one again for every object taken from it would be quadratic;
    /// keeping all of them would hold every dictionary of the file twice.
    /// </summary>
    private const int DecodedObjectStreamsKept = 4;

    /// <summary>
    /// How much of a stream is copied to the output at a time. Under the 85,000 bytes at which an
    /// array goes to the large object heap.
    /// </summary>
    private const int CopyChunk = 80 * 1024;

    /// <summary>The longest array the runtime makes of bytes.</summary>
    private const int LongestArray = 0x7FFFFFC7;

    private readonly PdfDocument _document;
    private readonly Parser _parser;
    private readonly Stream _stream;
    private readonly bool _ownsStream;

    /// <summary>Where each compressed object is: the number of its object stream, and its index there.</summary>
    private readonly Dictionary<int, (int ObjectStream, int Index)> _compressed = [];

    /// <summary>The decoded object streams, most recently used first.</summary>
    private readonly List<PdfObjectStream> _objectStreams = new(DecodedObjectStreamsKept + 1);

    /// <summary>
    /// The objects being read. Reading one can ask for another, and an object asked for while it is
    /// being read answers null rather than being read again without end.
    /// </summary>
    private readonly HashSet<PdfObjectID> _resolving = [];

    private readonly object _lock = new();
    private byte[] _copyBuffer;
    private bool _disposed;

    /// <summary>
    /// Takes over a document whose cross-reference sections <paramref name="parser"/> has read,
    /// and from here on every entry of its table answers for its object by reading it.
    /// </summary>
    internal LazyObjectSource(PdfDocument document, Parser parser, Stream stream, bool ownsStream)
    {
        _document = document;
        _parser = parser;
        _stream = stream;
        _ownsStream = ownsStream;
        parser.DeferStreamsTo = this;

        IndexCompressedObjects();

        // Giving an entry its document is what arms it: until now Value answered the bare field,
        // which reading the sections and PdfTrailer.Finish both depend on.
        var table = document._irefTable;
        foreach (var reference in table.ObjectTable.Values)
        {
            reference.Document = document;
            table.MaxObjectNumber = Math.Max(table.MaxObjectNumber, reference.ObjectNumber);
        }
    }

    /// <summary>
    /// Notes where every compressed object is, without reading the object stream it is in.
    /// </summary>
    private void IndexCompressedObjects()
    {
        // Newest revision first, and only the newest revision's word on an object counts: an
        // object a newer revision wrote out in full has a position in the table, and an older
        // revision's compressed copy of it is not the one to read.
        foreach (var xrefStream in _parser.CrossReferenceStreams)
        {
            foreach (var item in xrefStream.Entries)
            {
                if (item.Type != 2 || item.ObjectNumber < 1)
                    continue;

                if (_document._irefTable[new PdfObjectID(item.ObjectNumber)] is { Position: < 0, Value: null })
                    _compressed.TryAdd(item.ObjectNumber, ((int)item.Field2, (int)item.Field3));
            }
        }
    }

    /// <summary>
    /// Reads the object <paramref name="reference"/> names, or answers null when the file has
    /// nothing under that number, which is what such a reference stands for.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The document was disposed before the object was read.</exception>
    /// <exception cref="PdfReaderException">The object cannot be read. Asking again reads again.</exception>
    internal PdfObject Resolve(PdfReference reference)
    {
        lock (_lock)
        {
            // Another thread may have read it while this one waited, and reading it again would
            // put a second object where callers already hold the first.
            if (reference.ValueIfRead is { } read)
                return read;

            // Only the table's own entries are read for. A reference made while the sections were
            // being read, or by a caller, has nothing of its own in the file.
            if (!ReferenceEquals(_document._irefTable[reference.ObjectID], reference))
                return null;

            var inTheFile = reference.Position >= 0;
            if (!inTheFile && !_compressed.ContainsKey(reference.ObjectNumber))
                return null;

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PdfDocument),
                    $"Object {reference.ObjectID} had not been read when the document was disposed, and a document "
                    + "opened with PdfDocumentOpenMode.Lazy reads an object from its source when it is first asked for.");
            }

            if (!_resolving.Add(reference.ObjectID))
                return null;

            // One place starts a read inside another: an indirect /Length, asked for while the
            // lexer stands at the start of the stream's data, which is where it has to be again
            // afterwards. A read nothing is waiting on restores nothing, because that is a seek.
            var waiting = _resolving.Count > 1 ? _parser.SaveState() : null;
            try
            {
                return inTheFile
                    ? _parser.ReadObject(null, reference.ObjectID, false, false)
                    : ReadCompressedObject(reference);
            }
            catch
            {
                // The parser files a dictionary under its reference before reading its stream, so a
                // read that fails part-way would otherwise leave the half-read object to be answered
                // from then on as if it were whole.
                reference.SetObject(null);
                throw;
            }
            finally
            {
                if (waiting != null)
                    _parser.RestoreState(waiting);
                _resolving.Remove(reference.ObjectID);
            }
        }
    }

    /// <summary>
    /// Reads an object out of the object stream its cross-reference entry names.
    /// </summary>
    private PdfObject ReadCompressedObject(PdfReference reference)
    {
        var (objectStreamNumber, index) = _compressed[reference.ObjectNumber];
        var objectStream = ObjectStream(objectStreamNumber);

        // The object stream numbers its own objects, and files what it reads under its own number
        // for it. Where that is not the number asked for, reading it would put it in another
        // object's place, so the entry is forgotten and the reference stands for nothing.
        if (objectStream.ObjectNumberAt(index) != reference.ObjectNumber)
        {
            _compressed.Remove(reference.ObjectNumber);
            return null;
        }

        return objectStream.ReadCompressedObject(index).Value;
    }

    /// <summary>
    /// The decoded object stream with the given number, read and decoded unless it is one of the
    /// few kept.
    /// </summary>
    private PdfObjectStream ObjectStream(int number)
    {
        var kept = _objectStreams.FindIndex(objectStream => objectStream.ObjectNumber == number);
        if (kept >= 0)
        {
            var recent = _objectStreams[kept];
            _objectStreams.RemoveAt(kept);
            _objectStreams.Insert(0, recent);
            return recent;
        }

        var decoded = Decode(number);
        _objectStreams.Insert(0, decoded);
        if (_objectStreams.Count > DecodedObjectStreamsKept)
            _objectStreams.RemoveAt(DecodedObjectStreamsKept);
        return decoded;
    }

    /// <summary>
    /// Reads an object stream from the file and decodes it, leaving its entry in the table as it
    /// was found.
    /// </summary>
    private PdfObjectStream Decode(int number)
    {
        var reference = _document._irefTable[new PdfObjectID(number)];
        if (reference is not { Position: >= 0 })
        {
            throw new PdfReaderException(
                $"A cross-reference stream says objects are compressed in object stream {number}, "
                + "and the file has no such object.");
        }

        // Making a PdfObjectStream of a dictionary points the dictionary's reference at it, which
        // would keep the decoded stream for as long as the table lives. The entry is put back to
        // what it held, which is nothing unless a caller has asked for the object stream itself.
        var held = reference.ValueIfRead;
        try
        {
            if (_parser.ReadObject(null, reference.ObjectID, false, false) is not PdfDictionary { Stream: not null } dictionary)
            {
                throw new PdfReaderException(
                    $"A cross-reference stream says objects are compressed in object stream {number}, "
                    + $"and object {number} is not a stream, so the objects in it cannot be read.");
            }

            return new PdfObjectStream(dictionary);
        }
        finally
        {
            reference.SetObject(held);
        }
    }

    /// <summary>
    /// Reads the data of a stream that was left in the file into an array.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stream is longer than an array can be.</exception>
    /// <exception cref="ObjectDisposedException">The document, or the stream it reads from, was disposed.</exception>
    internal byte[] ReadStreamBytes(DeferredStreamBytes bytes)
    {
        if (bytes.Length > LongestArray)
        {
            throw new InvalidOperationException(
                $"The stream of object {bytes.ObjectID} is {bytes.Length} bytes long, which is more than an array "
                + "holds, so its data cannot be read as one. Saving a document the object was imported into copies "
                + "the data without reading it into memory.");
        }

        var data = new byte[bytes.Length];
        lock (_lock)
        {
            ReadFromTheSource(bytes, () => Fill(bytes, 0, data, data.Length));
        }
        return data;
    }

    /// <summary>
    /// Copies the data of a stream that was left in the file to <paramref name="output"/>, a chunk
    /// at a time and as it is in the file, so that it costs the same however long the stream is.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The document, or the stream it reads from, was disposed.</exception>
    internal void CopyStreamBytes(DeferredStreamBytes bytes, Stream output)
    {
        lock (_lock)
        {
            _copyBuffer ??= new byte[CopyChunk];
            ReadFromTheSource(bytes, () =>
            {
                for (long copied = 0; copied < bytes.Length;)
                {
                    var count = (int)Math.Min(bytes.Length - copied, CopyChunk);
                    Fill(bytes, copied, _copyBuffer, count);
                    output.Write(_copyBuffer, 0, count);
                    copied += count;
                }
            });
        }
    }

    /// <summary>
    /// Runs <paramref name="read"/>, which moves the source, and leaves the parser where it was
    /// if it is in the middle of an object.
    /// </summary>
    private void ReadFromTheSource(DeferredStreamBytes bytes, Action read)
    {
        if (_disposed)
            throw SourceIsGone(bytes, null);

        var waiting = _resolving.Count > 0 ? _parser.SaveState() : null;
        try
        {
            read();
        }
        finally
        {
            if (waiting != null)
                _parser.RestoreState(waiting);
        }
    }

    /// <summary>
    /// Reads <paramref name="count"/> bytes of the data, beginning <paramref name="from"/> bytes
    /// into it, into <paramref name="buffer"/>.
    /// </summary>
    private void Fill(DeferredStreamBytes bytes, long from, byte[] buffer, int count)
    {
        int read;
        try
        {
            _stream.Position = bytes.Offset + from;
            read = PdfPinata.Internal.StreamHelper.ReadUpTo(_stream, buffer, 0, count);
        }
        catch (ObjectDisposedException closed)
        {
            // The caller's own stream, closed while the document that reads from it was not.
            throw SourceIsGone(bytes, closed);
        }

        if (read == count)
            return;

        throw new PdfReaderException(
            $"The stream of object {bytes.ObjectID} was {bytes.Length} bytes at offset {bytes.Offset} when "
            + $"{SourceName} was opened, and the file now ends before them.");
    }

    private ObjectDisposedException SourceIsGone(DeferredStreamBytes bytes, Exception inner)
    {
        var message = $"The stream of object {bytes.ObjectID} is read from {SourceName} when it is first needed, and "
                      + "that document was disposed, or its stream closed, before then. A document opened with "
                      + "PdfDocumentOpenMode.Lazy has to stay open until every document its pages were imported "
                      + "into has been saved.";
        return inner == null
            ? new ObjectDisposedException(nameof(PdfDocument), message)
            : new ObjectDisposedException(message, inner);
    }

    /// <summary>What an error calls the document: its file, when it was opened from one.</summary>
    private string SourceName =>
        _document.FullPath.Length > 0 ? $"the document '{_document.FullPath}'" : "a document opened from a stream";

    /// <summary>
    /// Closes the source if the document opened it. Objects already read keep answering; one that
    /// has not been read throws when it is asked for.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _objectStreams.Clear();
            if (_ownsStream)
                _stream.Dispose();
        }
    }
}
