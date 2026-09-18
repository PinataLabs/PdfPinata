using System;
using System.Collections.Generic;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Finds the signatures a document already carries.
/// </summary>
/// <remarks>
/// Reading only. What is here is enough to say what a document claims about who signed it and which
/// bytes each signature covers, which is the part that needs the PDF object model. Deciding whether
/// to believe any of it is cryptography, and lives in <c>PdfPinata.Signing</c>.
/// </remarks>
public static class PdfSignatures
{
    /// <summary>
    /// Every signature in the document, in the order the interactive form lists its fields.
    /// </summary>
    public static IReadOnlyList<PdfSignatureInfo> InDocument(PdfDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var found = new List<PdfSignatureInfo>();

        var form = document.Catalog.Elements.GetDictionary(PdfCatalogAcroFormKey);
        var fields = form?.Elements.GetArray(PdfAcroForm.Keys.Fields);
        if (fields == null)
            return found;

        Collect(fields, found, new HashSet<PdfDictionary>(), 0);
        return found;
    }

    const string PdfCatalogAcroFormKey = "/AcroForm";

    /// <summary>
    /// Walks the field tree. Fields nest — a field may stand for a group and hold its members in
    /// <c>/Kids</c> — so a signature is not always a direct child of <c>/Fields</c>.
    /// </summary>
    /// <remarks>
    /// Bounded by what has been seen rather than by how deep the descent has gone. A depth limit
    /// stops the recursion but not the fan-out: a malformed field whose <c>/Kids</c> holds two
    /// references back to an ancestor doubles the work at every level, so a limit of 32 is four
    /// billion visits before it bites. This is read from files nobody here wrote, so the bound has
    /// to be exact.
    /// </remarks>
    static void Collect(PdfArray fields, List<PdfSignatureInfo> found, HashSet<PdfDictionary> seen,
        int depth)
    {
        // The visited set bounds the work; this bounds the stack, which a long chain of distinct
        // fields would otherwise run out of before the set had anything to say about it.
        if (depth > 64)
            return;

        for (var index = 0; index < fields.Elements.Count; index++)
        {
            var field = fields.Elements.GetDictionary(index);
            if (field == null || !seen.Add(field))
                continue;

            var kids = field.Elements.GetArray("/Kids");
            if (kids != null)
                Collect(kids, found, seen, depth + 1);

            if (field.Elements.GetName("/FT") != "/Sig")
                continue;

            var value = field.Elements.GetDictionary("/V");
            var info = Read(field, value);
            if (info != null)
                found.Add(info);
        }
    }

    static PdfSignatureInfo Read(PdfDictionary field, PdfDictionary value)
    {
        if (value == null)
            return null;

        var range = value.Elements.GetArray("/ByteRange");
        if (range == null)
            return null;

        var byteRange = new int[range.Elements.Count];
        for (var index = 0; index < byteRange.Length; index++)
            byteRange[index] = range.Elements.GetInteger(index);

        return new PdfSignatureInfo(
            field.Elements.GetString("/T"),
            value.Elements.GetName("/SubFilter"),
            ContentsOf(value),
            byteRange,
            NullIfEmpty(value.Elements.GetString("/Name")),
            NullIfEmpty(value.Elements.GetString("/Reason")),
            NullIfEmpty(value.Elements.GetString("/Location")),
            NullIfEmpty(value.Elements.GetString("/ContactInfo")),
            SigningTimeOf(value),
            CertificationLevelOf(value));
    }

    /// <summary>
    /// The bytes of <c>/Contents</c>, one per character.
    /// </summary>
    /// <remarks>
    /// A PDF string is a byte string throughout this library — the lexer reads one character per
    /// byte and the writer asserts it on the way out — so turning one back into bytes is the raw
    /// encoding and never a text decoding.
    /// </remarks>
    static byte[] ContentsOf(PdfDictionary value)
    {
        var contents = value.Elements["/Contents"] as PdfString;
        if (contents == null)
            return Array.Empty<byte>();

        return PdfEncoders.RawEncoding.GetBytes(contents.Value);
    }

    static DateTime? SigningTimeOf(PdfDictionary value)
    {
        if (!value.Elements.ContainsKey("/M"))
            return null;

        var time = value.Elements.GetDateTime("/M", DateTime.MinValue);
        return time == DateTime.MinValue ? (DateTime?)null : time;
    }

    /// <summary>
    /// The <c>/P</c> of a <c>/DocMDP</c> transform, which is what makes a signature a certifying one.
    /// </summary>
    static int CertificationLevelOf(PdfDictionary value)
    {
        var references = value.Elements.GetArray("/Reference");
        if (references == null)
            return 0;

        for (var index = 0; index < references.Elements.Count; index++)
        {
            var reference = references.Elements.GetDictionary(index);
            if (reference?.Elements.GetName("/TransformMethod") != "/DocMDP")
                continue;

            var parameters = reference.Elements.GetDictionary("/TransformParams");
            if (parameters != null)
            {
                // /P is optional and defaults to 2 (FormFillingAllowed) when absent — the spec's
                // default, not this library's. Reading it as 0 here would read an incomplete but
                // otherwise genuine certification as NotCertified and enforce nothing at all.
                return parameters.Elements.ContainsKey("/P")
                    ? parameters.Elements.GetInteger("/P")
                    : (int)PdfCertificationLevel.FormFillingAllowed;
            }
        }

        return 0;
    }

    static string NullIfEmpty(string value) => String.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// The document's own certification level, read from <c>/Perms/DocMDP</c> — the signature, if
    /// there is one, that certifies the whole document rather than merely approving it.
    /// </summary>
    /// <remarks>
    /// A structural read, not a cryptographic one: this says what the document <em>claims</em>, which
    /// is exactly what <see cref="PdfDocument.EnsureCanModify"/> needs to enforce the claim, the same
    /// way it already enforces the open mode without asking whether the file's contents can be
    /// trusted.
    /// </remarks>
    internal static PdfCertificationLevel CertificationOf(PdfDocument document)
    {
        var perms = document.Catalog.Elements.GetDictionary("/Perms");
        var signature = perms?.Elements.GetDictionary("/DocMDP");

        return signature == null
            ? PdfCertificationLevel.NotCertified
            : (PdfCertificationLevel)CertificationLevelOf(signature);
    }

    /// <summary>
    /// Whether a document certified at <paramref name="level"/> permits a change of kind
    /// <paramref name="kind"/>.
    /// </summary>
    /// <remarks>
    /// A document's structure and content are never permitted to change once certified, at any level
    /// — that is the whole point of certifying against "no changes" and remains true even for the two
    /// levels that permit something. The other two kinds nest: form field values (including signing)
    /// are permitted from <see cref="PdfCertificationLevel.FormFillingAllowed"/> up, and annotations
    /// only at <see cref="PdfCertificationLevel.FormFillingAndAnnotationsAllowed"/>.
    /// </remarks>
    internal static bool Permits(PdfCertificationLevel level, PdfChangeKind kind)
    {
        return level switch
        {
            PdfCertificationLevel.NotCertified => true,
            PdfCertificationLevel.NoChangesAllowed => false,
            PdfCertificationLevel.FormFillingAllowed => kind == PdfChangeKind.FormFieldValues,
            PdfCertificationLevel.FormFillingAndAnnotationsAllowed =>
                kind == PdfChangeKind.FormFieldValues || kind == PdfChangeKind.Annotations,
            _ => false
        };
    }
}
