using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Builds the object streams a document is written with, which is the half of
/// <see cref="PdfObjectStream"/> that produces one rather than reads one.
/// </summary>
/// <remarks>
/// An object stream holds the bodies of several objects run together, with a table at the front
/// saying which object each body belongs to and where it starts:
/// <code>
/// /Type /ObjStm  /N 4  /First 22  /Filter /FlateDecode
/// ┌──────────────────────────┬───────────────────────────────┐
/// │ 12 0  15 48  19 96  23 131 │ &lt;&lt;/Type/Page …&gt;&gt; &lt;&lt;/Font …&gt;&gt; │
/// └── the /First prologue ────┴── the bodies, concatenated ───┘
/// </code>
/// The whole thing is then compressed as one unit, and that is where the saving comes from: two
/// hundred small dictionaries share a compression window instead of each getting one of its own.
/// </remarks>
internal static class PdfObjectStreamWriter
{
    /// <summary>
    /// Answers whether the object behind a reference may be moved into an object stream.
    /// </summary>
    /// <remarks>
    /// Four things may not go in, and each for its own reason rather than by convention:
    /// a stream, because a stream cannot nest inside a stream; an object of a generation other than
    /// zero, because a compressed object's generation is implicitly zero and there is nowhere to
    /// record another; the encryption dictionary, because a reader has to reach it before it can
    /// decrypt anything, including the object stream that would be hiding it; and a free or dangling
    /// reference, because there is no body to write.
    /// </remarks>
    public static bool MayBeCompressed(PdfReference iref, PdfObject encryptionDictionary)
    {
        if (iref?.Value == null)
            return false;

        if (iref.GenerationNumber != 0)
            return false;

        if (ReferenceEquals(iref.Value, encryptionDictionary))
            return false;

        // A cross-reference stream and an object stream are both streams, so both are caught here
        // as well — but say it plainly, because it is not obvious that they are excluded by the
        // same rule that excludes a page's content.
        if (iref.Value is PdfDictionary dictionary && dictionary.Stream != null)
            return false;

        return true;
    }

    /// <summary>
    /// Builds one object stream holding the given objects, in the order given. The result is not
    /// yet part of the document: the caller adds it to the cross-reference table, which is what
    /// gives it the object number the entries of the compressed objects point at.
    /// </summary>
    public static PdfObjectStream Build(PdfDocument document, IList<PdfReference> members)
    {
        var header = new StringBuilder();
        var bodies = new MemoryStream();

        foreach (var iref in members)
        {
            // The offset recorded is relative to /First, so it is taken before the prologue exists
            // and needs no adjusting afterwards.
            header.Append(iref.ObjectNumber).Append(' ').Append(bodies.Length).Append(' ');
            WriteBody(iref, bodies);
        }

        var prologue = PdfEncoders.RawEncoding.GetBytes(header.ToString());
        var content = new byte[prologue.Length + bodies.Length];
        prologue.CopyTo(content, 0);
        bodies.ToArray().CopyTo(content, prologue.Length);

        var objectStream = new PdfObjectStream(document);
        objectStream.Elements.SetName(PdfObjectStream.Keys.Type, "/ObjStm");
        objectStream.Elements.SetInteger(PdfObjectStream.Keys.N, members.Count);
        objectStream.Elements.SetInteger(PdfObjectStream.Keys.First, prologue.Length);

        if (document.Options.NoCompression)
        {
            objectStream.CreateStream(content);
        }
        else
        {
            objectStream.CreateStream(Filtering.FlateDecode.Encode(content, document.Options.FlateEncodeMode));
            objectStream.Elements.SetName(PdfDictionary.PdfStream.Keys.Filter, "/FlateDecode");
        }

        return objectStream;
    }

    /// <summary>
    /// Writes one object's body — what lies between "<c>N 0 obj</c>" and "<c>endobj</c>", and
    /// neither of those.
    /// </summary>
    /// <remarks>
    /// The writer is given no security handler, and that is the load-bearing part rather than an
    /// omission. Strings inside an object stream are covered by the encryption of the stream that
    /// contains them and must not be encrypted a second time; a document that does encrypt them
    /// twice opens, looks well, and yields mojibake for every string in it.
    /// </remarks>
    static void WriteBody(PdfReference iref, MemoryStream destination)
    {
        var writer = new PdfWriter(destination, null)
        {
            Layout = PdfWriterLayout.Compact,
            OmitIndirectFraming = true,
        };
        iref.Value.WriteObject(writer);
        destination.Flush();

        // Bodies are found by the offsets in the prologue, so nothing separates them of necessity.
        // One byte of whitespace is cheap and means a reader that scans rather than seeks — and
        // they exist — does not run two objects together.
        destination.WriteByte((byte)'\n');
    }
}
