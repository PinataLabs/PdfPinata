using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Writes certificates and revocation responses into a document's security store (<c>/DSS</c>), so a
/// signature can still be checked once the certificate it was made with has expired.
/// </summary>
/// <remarks>
/// <para>
/// This is PAdES B-LT / LTV. The store is a dictionary of streams — PDF machinery with no
/// cryptography in it, exactly like the byte range and the placeholder <c>PdfSigner</c> writes — so it
/// belongs here rather than in <c>PdfPinata.Signing</c>, which is where the bytes it stores come
/// from.
/// </para>
/// <para>
/// <b>Added by incremental update, always.</b> Rewriting the file would invalidate the very signature
/// this data exists to support, so this only ever appends a revision — the same capability
/// <see cref="PdfSigner"/> signs through, and it needs nothing new from it.
/// </para>
/// <para>
/// <b>Available separately from signing.</b> The document being archived was often signed by someone
/// else, so adding validation data is its own call rather than an option on <see cref="PdfSigner"/>'s.
/// </para>
/// <para>
/// <b>Not gated by a certifying signature's <c>/DocMDP</c> level, deliberately.</b> Every other change
/// this library makes to a document goes through <c>PdfDocument.EnsureCanModify</c>, which a
/// certification of <see cref="PdfCertificationLevel.NoChangesAllowed"/> refuses outright — but this
/// does not call it. A document certified against all further change is exactly the kind of document
/// LTV exists to keep verifiable for: the store adds evidence about what the document already was, it
/// does not change what the document says, and refusing it here would defeat the archival workflow
/// PAdES B-LT is for. The only gate that applies is the same one every incremental write needs — the
/// document has to have been opened with <see cref="PdfPinata.Pdf.IO.PdfDocumentOpenMode.Append"/>.
/// </para>
/// </remarks>
public static class PdfValidationData
{
    /// <summary>
    /// Writes <paramref name="data"/> into the document's <c>/DSS</c>, creating it if the document
    /// does not have one yet, and appends the revision to <paramref name="output"/>.
    /// </summary>
    /// <param name="document">A document opened with <see cref="PdfPinata.Pdf.IO.PdfDocumentOpenMode.Append"/>.</param>
    /// <param name="output">Where the appended revision is written. Not closed by this method.</param>
    /// <param name="data">The certificates and revocation responses to add.</param>
    public static void Add(PdfDocument document, Stream output, PdfValidationDataEntry data)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (!document.CanSaveIncremental)
            throw new InvalidOperationException(
                "Validation data can only be added to a document opened with PdfDocumentOpenMode.Append. "
                + "Adding it appends a revision, and appending needs the bytes the document was read "
                + "from together with the object numbers it was read with.");

        var catalog = document.Catalog;
        var dss = catalog.Elements.GetDictionary("/DSS");
        var isNewStore = dss == null;
        if (isNewStore)
        {
            dss = new PdfDictionary(document);
            document.Internals.AddObject(dss);
        }

        Extend(document, dss, "/Certs", data.Certificates);
        Extend(document, dss, "/OCSPs", data.OcspResponses);
        Extend(document, dss, "/CRLs", data.Crls);

        if (isNewStore)
        {
            catalog.Elements["/DSS"] = dss.Reference;
            catalog.MarkAsChanged();
        }

        document.SaveIncremental(output);
    }

    /// <summary>
    /// Whether the document already carries a security store — evidence that at least some signature
    /// in it can be checked without reaching the network.
    /// </summary>
    public static bool IsPresent(PdfDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        return document.Catalog.Elements.GetDictionary("/DSS") != null;
    }

    /// <summary>
    /// Appends one stream per byte array to the array named <paramref name="key"/> in the store,
    /// creating the array if this is the first thing written under that key.
    /// </summary>
    static void Extend(PdfDocument document, PdfDictionary dss, string key, IReadOnlyList<byte[]> items)
    {
        if (items.Count == 0)
            return;

        var array = dss.Elements.GetArray(key);
        if (array == null)
        {
            array = new PdfArray(document);
            dss.Elements[key] = array;
        }

        foreach (var item in items)
        {
            var stream = new PdfDictionary(document);
            document.Internals.AddObject(stream);
            stream.CreateStream(item);
            array.Elements.Add(stream.Reference);
        }

        array.MarkAsChanged();
        dss.MarkAsChanged();
    }
}
