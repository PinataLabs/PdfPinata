using System.Collections.Generic;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Writes a document's objects indexed by a cross-reference stream rather than by a cross-reference
/// table, gathering into object streams everything that may go into one.
/// </summary>
/// <remarks>
/// This is the whole of <see cref="PdfCrossReferenceFormat.Stream"/>. The classic path in
/// <see cref="PdfDocument.DoSave"/> writes every object on its own and indexes them by byte offset;
/// this one writes the objects that cannot be compressed the same way, packs the rest into object
/// streams, and indexes both kinds in a stream of fixed-width binary rows.
/// </remarks>
internal static class PdfCrossReferenceStreamWriter
{
    /// <summary>
    /// The widths of the three fields of an entry, as they go in <c>/W</c>.
    /// </summary>
    /// <remarks>
    /// Four bytes for the second field caps the file at 4 GB and an object stream at four billion
    /// members, neither of which is reachable in practice; two for the third leaves room for the
    /// largest generation number a PDF may have. Computing the narrowest widths that fit would save
    /// a few bytes per object and cost the clarity of a fixed layout — the entries are compressed
    /// afterwards anyway, and repeated leading zeroes are exactly what a compressor is good at.
    /// </remarks>
    static readonly int[] FieldWidths = { 1, 4, 2 };

    /// <summary>
    /// Writes the body of the document, then the cross-reference stream that indexes it, and
    /// answers the offset the <c>startxref</c> at the end of the file has to name.
    /// </summary>
    public static long WriteBody(PdfDocument document, PdfWriter writer)
    {
        var irefTable = document._irefTable;
        var encryptionDictionary = document._trailer.Elements[PdfTrailer.Keys.Encrypt] is PdfReference encryptRef
            ? encryptRef.Value
            : null;

        // Partition first, and take the list before anything is added to the table: the object
        // streams built below join it, and an object stream may not live inside another one.
        var uncompressed = new List<PdfReference>();
        var compressible = new List<PdfReference>();
        foreach (var iref in irefTable.AllReferences)
        {
            if (PdfObjectStreamWriter.MayBeCompressed(iref, encryptionDictionary))
                compressible.Add(iref);
            else
                uncompressed.Add(iref);
        }

        // Which object stream each compressed object ended up in, and where in it. Recorded now
        // because the entries cannot be written until every offset is known, and the offsets are
        // not known until everything has been written.
        var placements = new Dictionary<int, (int ObjectStreamNumber, int Index)>();
        var objectStreams = new List<PdfReference>();

        var perStream = document.Options.MaxObjectsPerObjectStream;
        for (var start = 0; start < compressible.Count; start += perStream)
        {
            var members = compressible.GetRange(start, System.Math.Min(perStream, compressible.Count - start));
            var objectStream = PdfObjectStreamWriter.Build(document, members);
            irefTable.Add(objectStream);
            objectStreams.Add(objectStream.Reference);

            for (var index = 0; index < members.Count; index++)
                placements[members[index].ObjectNumber] = (objectStream.ObjectNumber, index);
        }

        // The cross-reference stream is an object like any other and needs a number of its own, and
        // an entry of its own pointing at where it is about to be written.
        var xrefStream = new PdfCrossReferenceStream(document);
        irefTable.Add(xrefStream);

        foreach (var iref in uncompressed)
        {
            iref.Position = writer.Position;
            iref.Value.WriteObject(writer);
        }
        foreach (var iref in objectStreams)
        {
            iref.Position = writer.Position;
            iref.Value.WriteObject(writer);
        }

        var startxref = writer.Position;
        xrefStream.Reference.Position = startxref;

        var size = irefTable.MaxObjectNumber + 1;
        var entries = new PdfCrossReferenceStream.CrossReferenceStreamEntry[size];

        // Object zero is the head of the free list and is always present, whether or not anything
        // is free. Its generation is 65535 by convention rather than by meaning.
        entries[0] = new PdfCrossReferenceStream.CrossReferenceStreamEntry
        {
            Type = 0,
            Field2 = 0,
            Field3 = 65535
        };

        foreach (var iref in uncompressed)
            entries[iref.ObjectNumber] = InUse(iref);
        foreach (var iref in objectStreams)
            entries[iref.ObjectNumber] = InUse(iref);
        entries[xrefStream.ObjectNumber] = InUse(xrefStream.Reference);

        foreach (var iref in compressible)
        {
            var placement = placements[iref.ObjectNumber];
            entries[iref.ObjectNumber] = new PdfCrossReferenceStream.CrossReferenceStreamEntry
            {
                Type = 2,
                Field2 = (uint)placement.ObjectStreamNumber,
                Field3 = (uint)placement.Index
            };
        }

        CopyTrailerElements(document._trailer, xrefStream);
        xrefStream.Elements.SetName(PdfCrossReferenceStream.Keys.Type, "/XRef");
        xrefStream.Elements.SetInteger(PdfCrossReferenceStream.Keys.Size, size);

        var widths = new PdfArray(document);
        foreach (var width in FieldWidths)
            widths.Elements.Add(new PdfInteger(width));
        xrefStream.Elements[PdfCrossReferenceStream.Keys.W] = widths;

        // /Index is omitted deliberately. Its default is [0 Size], and PrepareForSave renumbers the
        // objects from 1 with no gaps, so that default is exactly right and saying so again would
        // only be another thing that could disagree with the entries.

        var content = Encode(entries);
        if (document.Options.NoCompression)
        {
            xrefStream.CreateStream(content);
        }
        else
        {
            xrefStream.CreateStream(Filtering.FlateDecode.Encode(content, document.Options.FlateEncodeMode));
            xrefStream.Elements.SetName(PdfDictionary.PdfStream.Keys.Filter, "/FlateDecode");
        }

        // PdfTrailer.WriteObject turns encryption off around itself, which a cross-reference stream
        // inherits and requires: it is never encrypted, because a reader has to read it before it
        // knows how to decrypt anything.
        xrefStream.WriteObject(writer);

        return startxref;
    }

    /// <summary>
    /// Writes the cross-reference stream that indexes the objects an incremental save has just
    /// appended, and answers the offset the <c>startxref</c> after it has to name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A revision appended to a file whose last revision was indexed by a cross-reference stream is
    /// indexed by another one, as <c>docs/specs/incremental-update-save.md</c> always said it should
    /// be. The document's trailer is then the <em>previous</em> stream, read back in, and writing it
    /// as though it were a trailer dictionary is what issue #55 was: the keyword <c>trailer</c>, then
    /// the old stream's object header, then its stale entries, in a file nothing could read.
    /// </para>
    /// <para>
    /// The stream takes the next object number free and is <em>not</em> added to the table, just as
    /// the classic path adds nothing: the document is left as it was read, so a second incremental
    /// save from it appends to the same original rather than finding this revision's index among the
    /// objects it thinks are new. <c>/Index</c> names the runs the entries fall into, because the
    /// numbers in between belong to objects this revision leaves where they were.
    /// </para>
    /// <para>
    /// The changed objects themselves are written standing on their own rather than gathered into an
    /// object stream. A reader follows a type 1 entry as readily as a type 2 one, and an appended
    /// revision is usually a handful of objects, where packing would save little and would add an
    /// object stream to every revision.
    /// </para>
    /// </remarks>
    public static long WriteIncrementalSection(PdfDocument document, PdfWriter writer,
        List<PdfReference> changed, long previousStartXref)
    {
        var xrefStream = new PdfCrossReferenceStream(document);
        xrefStream.SetObjectID(document._irefTable.MaxObjectNumber + 1, 0);

        var startxref = writer.Position;
        xrefStream.Reference.Position = startxref;

        var ordered = new List<PdfReference>(changed) { xrefStream.Reference };
        ordered.Sort(PdfReference.Comparer);

        var index = new PdfArray(document);
        var entries = new PdfCrossReferenceStream.CrossReferenceStreamEntry[ordered.Count];
        var at = 0;
        while (at < ordered.Count)
        {
            var runLength = 1;
            while (at + runLength < ordered.Count
                   && ordered[at + runLength].ObjectNumber == ordered[at].ObjectNumber + runLength)
                runLength++;

            index.Elements.Add(new PdfInteger(ordered[at].ObjectNumber));
            index.Elements.Add(new PdfInteger(runLength));
            at += runLength;
        }
        for (var entry = 0; entry < ordered.Count; entry++)
            entries[entry] = InUse(ordered[entry]);

        CopyTrailerElements(document._trailer, xrefStream);
        xrefStream.Elements.SetName(PdfCrossReferenceStream.Keys.Type, "/XRef");
        xrefStream.Elements.SetInteger(PdfCrossReferenceStream.Keys.Size, xrefStream.ObjectNumber + 1);
        xrefStream.Elements[PdfCrossReferenceStream.Keys.Index] = index;

        // Where the previous revision's cross-reference stream begins. The cast is safe for the
        // reason SaveIncremental gives for its own /Prev: an original that fits in an array ends
        // before int.MaxValue.
        xrefStream.Elements.SetInteger(PdfCrossReferenceStream.Keys.Prev, checked((int)previousStartXref));

        var widths = new PdfArray(document);
        foreach (var width in FieldWidths)
            widths.Elements.Add(new PdfInteger(width));
        xrefStream.Elements[PdfCrossReferenceStream.Keys.W] = widths;

        var content = Encode(entries);
        if (document.Options.NoCompression)
        {
            xrefStream.CreateStream(content);
        }
        else
        {
            xrefStream.CreateStream(Filtering.FlateDecode.Encode(content, document.Options.FlateEncodeMode));
            xrefStream.Elements.SetName(PdfDictionary.PdfStream.Keys.Filter, "/FlateDecode");
        }

        // Never encrypted, for the reason WriteBody gives.
        xrefStream.WriteObject(writer);

        return startxref;
    }

    static PdfCrossReferenceStream.CrossReferenceStreamEntry InUse(PdfReference iref) =>
        new()
        {
            Type = 1,
            Field2 = (uint)iref.Position,
            Field3 = (uint)iref.GenerationNumber
        };

    /// <summary>
    /// Moves the entries that were the trailer dictionary onto the cross-reference stream, which is
    /// where they live when a file has no trailer to put them in.
    /// </summary>
    static void CopyTrailerElements(PdfTrailer trailer, PdfCrossReferenceStream xrefStream)
    {
        string[] carried =
        {
            PdfTrailer.Keys.Root,
            PdfTrailer.Keys.Info,
            PdfTrailer.Keys.ID,
            PdfTrailer.Keys.Encrypt
        };

        foreach (var key in carried)
        {
            var value = trailer.Elements[key];
            if (value != null)
                xrefStream.Elements[key] = value;
        }
    }

    /// <summary>
    /// Lays the entries out as the fixed-width big-endian rows the stream is made of.
    /// </summary>
    static byte[] Encode(PdfCrossReferenceStream.CrossReferenceStreamEntry[] entries)
    {
        var rowLength = FieldWidths[0] + FieldWidths[1] + FieldWidths[2];
        var bytes = new byte[entries.Length * rowLength];

        var at = 0;
        foreach (var entry in entries)
        {
            at = WriteField(bytes, at, entry.Type, FieldWidths[0]);
            at = WriteField(bytes, at, (ulong)entry.Field2, FieldWidths[1]);
            at = WriteField(bytes, at, entry.Field3, FieldWidths[2]);
        }

        return bytes;
    }

    static int WriteField(byte[] bytes, int at, ulong value, int width)
    {
        for (var shift = width - 1; shift >= 0; shift--)
            bytes[at++] = (byte)(value >> (shift * 8));
        return at;
    }
}
