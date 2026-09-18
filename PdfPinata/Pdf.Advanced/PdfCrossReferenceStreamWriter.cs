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
            Field3 = 65535,
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
                Field3 = (uint)placement.Index,
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

    static PdfCrossReferenceStream.CrossReferenceStreamEntry InUse(PdfReference iref) =>
        new()
        {
            Type = 1,
            Field2 = (uint)iref.Position,
            Field3 = (uint)iref.GenerationNumber,
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
            PdfTrailer.Keys.Encrypt,
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
            at = WriteField(bytes, at, entry.Field2, FieldWidths[1]);
            at = WriteField(bytes, at, entry.Field3, FieldWidths[2]);
        }

        return bytes;
    }

    static int WriteField(byte[] bytes, int at, uint value, int width)
    {
        for (var shift = width - 1; shift >= 0; shift--)
            bytes[at++] = (byte)(value >> (shift * 8));
        return at;
    }
}
