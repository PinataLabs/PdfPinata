using System;
using System.Globalization;
using System.IO;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Signs a document: reserves a hole for the signature, writes the revision that will be signed,
/// and patches the signature into the hole it left.
/// </summary>
/// <remarks>
/// <para>
/// A PDF signature is a chicken-and-egg problem the format solves by cheating. The signature covers
/// the file, and the signature is in the file, so the bytes to be hashed cannot be known until the
/// signature is written and the signature cannot be computed until they are. The way out is to write
/// the file with a hole of a known size where the signature will go, with a <c>/ByteRange</c> saying
/// "everything except that hole" — then compute the signature over what was written and patch it in
/// without moving a single byte. Every field involved is therefore written at a <b>fixed width</b>:
/// that is what makes the second pass a patch rather than a re-layout.
/// </para>
/// <para>
/// The revision is always <b>appended</b> rather than rewritten. That is not an optimisation either:
/// rewriting a file invalidates every signature already on it, so signing a signed document any
/// other way destroys what was there. See <see cref="PdfDocument.SaveIncremental"/>.
/// </para>
/// </remarks>
public static class PdfSigner
{
    /// <summary>
    /// The <c>/ByteRange</c> as it is first written. Every value is ten digits wide so the real
    /// offsets — which are not known until this array has been written — can be patched over it
    /// without changing its length and so moving what it points at.
    /// </summary>
    private const string ByteRangePlaceholder = "[0 0000000000 0000000000 0000000000]";

    private const int ByteRangeFieldWidth = 10;

    /// <summary>
    /// Signs a document that was opened for appending, and writes the signed file to a stream.
    /// </summary>
    /// <param name="document">
    /// A document opened with <see cref="PdfDocumentOpenMode.Append"/>. Any other mode either
    /// renumbers the objects or does not keep the original bytes, and a signature needs both.
    /// </param>
    /// <param name="output">Where the signed file is written. Not closed by this method.</param>
    /// <param name="signer">Produces the detached signature itself.</param>
    /// <param name="options">What to record about the signature, and where to show it.</param>
    public static void Sign(PdfDocument document, Stream output, IPdfSigner signer,
        PdfSignatureOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(signer);

        if (!document.CanSaveIncremental)
            throw new InvalidOperationException(
                "A document can only be signed if it was opened with PdfDocumentOpenMode.Append. "
                + "Signing appends a revision, and appending needs the bytes the document was read "
                + "from together with the object numbers it was read with. To sign a document built "
                + "in memory, save it first and open the result for appending — the Sign overload "
                + "taking a stream does exactly that.");

        // Signing is a form field value in DocMDP's terms — the standard groups digital signing with
        // form fill-in — so a document certified against changes, or certified for form fill-in and
        // above, is asked the same question filling in an ordinary field is.
        document.EnsureCanModify("signing the document", PdfChangeKind.FormFieldValues);

        options ??= new PdfSignatureOptions();

        // A document can carry only one certifying signature, and it must be the first signature
        // applied. EnsureCanModify above already refuses this signature if an existing certification
        // forbids FormFieldValues; this refuses the narrower case it lets through — a document still
        // open to signing (FormFillingAllowed or above) but already certified — where Certify below
        // would otherwise silently replace the certification a reader has already relied on.
        if (options.Certification != PdfCertificationLevel.NotCertified
            && PdfSignatures.CertificationOf(document) != PdfCertificationLevel.NotCertified)
            throw new InvalidOperationException(
                "The document is already certified. A document can carry only one certifying "
                + "signature, and it must be the first signature applied to the document.");

        // Refused before anything is added to the document: the widget's flags are covered by the
        // signature, so they cannot be put right after the fact.
        Metadata.PdfConformanceWriter.CheckAnnotationFlags(document.Options.Conformance,
            options.AnnotationFlags, "signature widget");

        if (signer.EstimatedSignatureSize < 1)
            throw new ArgumentOutOfRangeException(nameof(signer),
                "The signer must reserve at least one byte for its signature.");

        var reserved = signer.EstimatedSignatureSize;
        var signature = BuildSignatureDictionary(document, signer, options, reserved);
        var field = BuildSignatureField(document, options, signature);

        AttachToPage(document, options, field);
        AttachToAcroForm(document, field);
        if (options.Certification != PdfCertificationLevel.NotCertified)
            Certify(document, signature, options.Certification);
        if (options.LockAction != null)
            Lock(document, field, signature, options);

        var buffer = WriteRevision(document);

        // Everything appended by this revision starts here, so the placeholders can be looked for
        // from this point on and cannot be confused with anything an earlier revision wrote — an
        // already-signed document has a /Contents of its own, and it is full of real signature.
        var appendedFrom = document.OriginalByteCount;

        var contentsAt = FindContentsHole(buffer, appendedFrom, reserved);
        var byteRangeAt = FindByteRangePlaceholder(buffer, appendedFrom);

        // The hole is the hexadecimal string including its angle brackets: what is signed is
        // everything before the opening one and everything after the closing one.
        var holeStart = contentsAt;
        var holeEnd = contentsAt + reserved * 2 + 2;
        var tailLength = buffer.Length - holeEnd;

        PatchByteRange(buffer, byteRangeAt, holeStart, holeEnd, tailLength);

        byte[] produced;
        using (var content = new ByteRangeStream(buffer, 0, holeStart, holeEnd, tailLength))
            produced = signer.Sign(content);

        if (produced == null || produced.Length == 0)
            throw new InvalidOperationException("The signer produced no signature.");

        if (produced.Length > reserved)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                "The signature is {0} bytes but only {1} were reserved for it. The signer's "
                + "EstimatedSignatureSize has to be at least as large as anything it will produce, "
                + "and the room it asks for cannot be revised once the document has been written.",
                produced.Length, reserved));

        WriteHex(buffer, holeStart + 1, produced, reserved);

        output.Write(buffer, 0, buffer.Length);
        output.Flush();
    }

    /// <summary>
    /// Signs the document in a stream and writes the signed file to another.
    /// </summary>
    /// <remarks>
    /// The convenience form, and the one to use for a document that has just been built: save it to
    /// a stream, hand the stream to this, and the opening for append is done here.
    /// </remarks>
    public static void Sign(Stream input, Stream output, IPdfSigner signer,
        PdfSignatureOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        var document = PdfReader.Open(input, PdfDocumentOpenMode.Append);
        Sign(document, output, signer, options);
    }

    /// <summary>
    /// The signature dictionary — the object the field's value points at, and the one that carries
    /// the two placeholders.
    /// </summary>
    private static PdfDictionary BuildSignatureDictionary(PdfDocument document, IPdfSigner signer,
        PdfSignatureOptions options, int reserved)
    {
        var signature = new PdfDictionary(document);
        document.Internals.AddObject(signature);

        signature.Elements.SetName("/Type", "/Sig");

        // Adobe.PPKLite is the handler every reader understands; the SubFilter is what actually
        // says how to interpret /Contents, and that belongs to the signer rather than to us.
        signature.Elements.SetName("/Filter", "/Adobe.PPKLite");
        signature.Elements.SetName("/SubFilter", signer.SubFilter);

        signature.Elements["/ByteRange"] = new PdfLiteral(ByteRangePlaceholder);
        signature.Elements["/Contents"] = new PdfLiteral("<" + new string('0', reserved * 2) + ">");

        signature.Elements.SetDateTime("/M", options.SigningTime ?? GlobalTimeSettings.Now);

        if (!string.IsNullOrEmpty(options.SignerName))
            signature.Elements.SetString("/Name", options.SignerName);
        if (!string.IsNullOrEmpty(options.Reason))
            signature.Elements.SetString("/Reason", options.Reason);
        if (!string.IsNullOrEmpty(options.Location))
            signature.Elements.SetString("/Location", options.Location);
        if (!string.IsNullOrEmpty(options.ContactInfo))
            signature.Elements.SetString("/ContactInfo", options.ContactInfo);

        return signature;
    }

    /// <summary>
    /// The form field, which is also the annotation that shows it.
    /// </summary>
    /// <remarks>
    /// A field with exactly one widget may be merged with it into a single dictionary, which is what
    /// nearly every producer does and what readers are best at. So this one object is both the
    /// <c>/FT /Sig</c> field in the AcroForm's <c>/Fields</c> and the <c>/Widget</c> annotation in
    /// the page's <c>/Annots</c>.
    /// </remarks>
    private static PdfSignatureField BuildSignatureField(PdfDocument document, PdfSignatureOptions options,
        PdfDictionary signature)
    {
        var field = new PdfSignatureField(document);
        document.Internals.AddObject(field);

        field.Elements.SetName("/FT", "/Sig");
        field.Elements.SetString("/T", options.FieldName ?? "Signature1");
        field.Elements["/V"] = signature.Reference;

        field.Elements.SetName("/Type", "/Annot");
        field.Elements.SetName("/Subtype", "/Widget");

        // Print by default, so the field is part of the paper document too. A signature that only
        // exists on screen is a surprise nobody wants. Written here or never: the widget is part of
        // the signed revision, so changing /F afterwards changes a signed object. No flags omits the
        // entry, whose default is already zero.
        if (options.AnnotationFlags != 0)
            field.Elements.SetInteger("/F", (int)options.AnnotationFlags);

        return field;
    }

    /// <summary>
    /// Puts the field on its page, giving it a rectangle and — if the caller wants one — something
    /// to show inside it.
    /// </summary>
    private static void AttachToPage(PdfDocument document, PdfSignatureOptions options, PdfSignatureField field)
    {
        if (options.PageIndex < 0 || options.PageIndex >= document.PageCount)
            throw new ArgumentOutOfRangeException(nameof(options),
                string.Format(CultureInfo.InvariantCulture,
                    "The document has {0} page(s), so page index {1} does not name one.",
                    document.PageCount, options.PageIndex));

        var page = document.Pages[options.PageIndex];

        if (options.Rectangle.HasValue)
        {
            var visible = options.Rectangle.Value;

            // The caller measures from the top of the page downwards, because that is how XGraphics
            // draws; a PDF rectangle is measured from the bottom upwards.
            var height = page.Height.Point;
            field.Elements.SetRectangle("/Rect", new PdfRectangle(
                visible.X, height - visible.Y - visible.Height,
                visible.X + visible.Width, height - visible.Y));

            if (options.DrawAppearance != null)
                field.Elements["/AP"] = BuildAppearance(document, options, visible);
        }
        else
        {
            // An invisible signature: a real field with nothing to show. Zero width and height is
            // what says so — the field is still there, still in /Fields and still covering the whole
            // document.
            field.Elements.SetRectangle("/Rect", new PdfRectangle(0, 0, 0, 0));
        }

        field.Elements["/P"] = page.Reference;

        var annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
        {
            annotations = new PdfArray(document);
            page.Elements["/Annots"] = annotations;
        }

        annotations.Elements.Add(field.Reference);

        // The annotation array is usually direct, held inside the page dictionary rather than
        // indirect in its own right, so changing it changes the page and only saying so gets the
        // page written into the appended revision.
        annotations.MarkAsChanged();
        page.MarkAsChanged();
    }

    /// <summary>
    /// Draws the visible appearance into a form XObject and returns the appearance dictionary
    /// referring to it.
    /// </summary>
    private static PdfDictionary BuildAppearance(PdfDocument document, PdfSignatureOptions options, XRect visible)
    {
        var form = new XForm(document, new XSize(visible.Width, visible.Height));
        using (var gfx = XGraphics.FromForm(form))
            options.DrawAppearance(gfx, new XRect(0, 0, visible.Width, visible.Height));
        form.DrawingFinished();

        var appearance = new PdfDictionary(document) { Elements = { ["/N"] = form.PdfForm.Reference } };
        return appearance;
    }

    /// <summary>
    /// Registers the field with the document's interactive form, creating the form if the document
    /// has none.
    /// </summary>
    private static void AttachToAcroForm(PdfDocument document, PdfSignatureField field)
    {
        var catalog = document.Catalog;
        var form = catalog.AcroForm;
        if (form == null)
        {
            form = new PdfAcroForm(document);
            document.Internals.AddObject(form);
            catalog.AcroForm = form;
            catalog.MarkAsChanged();
        }

        var fields = form.Elements.GetArray(PdfAcroForm.Keys.Fields);
        if (fields == null)
        {
            fields = new PdfArray(document);
            form.Elements[PdfAcroForm.Keys.Fields] = fields;
        }

        fields.Elements.Add(field.Reference);
        fields.MarkAsChanged();

        // SignaturesExist and AppendOnly. The second is the one that matters: it tells a reader that
        // this document must only ever be added to, never rewritten, which is exactly the promise a
        // signature depends on.
        form.Elements.SetInteger(PdfAcroForm.Keys.SigFlags, 3);
        form.MarkAsChanged();
    }

    /// <summary>
    /// Makes the signature a certifying one by declaring, through <c>/DocMDP</c>, what a later
    /// revision is still allowed to do.
    /// </summary>
    private static void Certify(PdfDocument document, PdfDictionary signature, PdfCertificationLevel level)
    {
        var parameters = new PdfDictionary(document);
        parameters.Elements.SetName("/Type", "/TransformParams");
        parameters.Elements.SetInteger("/P", (int)level);
        parameters.Elements.SetName("/V", "/1.2");

        var reference = new PdfDictionary(document);
        reference.Elements.SetName("/Type", "/SigRef");
        reference.Elements.SetName("/TransformMethod", "/DocMDP");
        reference.Elements["/TransformParams"] = parameters;
        reference.Elements.SetName("/DigestMethod", "/SHA256");

        AddReference(document, signature, reference);

        // And the catalog has to point back at the signature, or a reader has no way of finding out
        // that the document is certified at all.
        var permissions = new PdfDictionary(document) { Elements = { ["/DocMDP"] = signature.Reference } };

        var catalog = document.Catalog;
        catalog.Elements["/Perms"] = permissions;
        catalog.MarkAsChanged();
    }

    /// <summary>
    /// Appends a signature reference dictionary to the signature's <c>/Reference</c> array, which a
    /// certifying signature that also locks fields has two of.
    /// </summary>
    private static void AddReference(PdfDocument document, PdfDictionary signature, PdfDictionary reference)
    {
        var references = signature.Elements.GetArray("/Reference");
        if (references == null)
        {
            references = new PdfArray(document);
            signature.Elements["/Reference"] = references;
        }

        references.Elements.Add(reference);
    }

    /// <summary>
    /// Locks the fields the options ask for: <c>/Lock</c> on the field, a <c>/FieldMDP</c>
    /// reference on the signature, and the read-only flag on every field covered.
    /// </summary>
    private static void Lock(PdfDocument document, PdfSignatureField field, PdfDictionary signature,
        PdfSignatureOptions options)
    {
        var action = options.LockAction!.Value;
        var fields = action == PdfFieldLockAction.All ? null : (options.LockFields ?? []).ToArray();

        // A fresh dictionary per signing, rather than one the options hold, so that one options
        // object can sign any number of documents.
        field.Lock = new PdfSignatureFieldLock(document, action, fields);

        var parameters = new PdfDictionary(document);
        parameters.Elements.SetName("/Type", "/TransformParams");
        parameters.Elements.SetName("/Action", "/" + action);
        if (fields != null)
            parameters.Elements["/Fields"] = field.Lock.Elements["/Fields"].Clone();
        parameters.Elements.SetName("/V", "/1.2");

        var reference = new PdfDictionary(document);
        reference.Elements.SetName("/Type", "/SigRef");
        reference.Elements.SetName("/TransformMethod", "/FieldMDP");
        reference.Elements["/TransformParams"] = parameters;
        reference.Elements.SetName("/DigestMethod", "/SHA256");
        AddReference(document, signature, reference);

        var form = document.Catalog.AcroForm;
        if (form == null)
            return;

        var own = field.Elements.GetString(PdfAcroField.Keys.T);
        foreach (var name in form.Fields.DescendantNames)
        {
            if (name == own || !field.Lock.Covers(name))
                continue;

            var locked = form.Fields[name];
            if (locked == null || locked.ReadOnly)
                continue;

            locked.ReadOnly = true;

            // Written into the appended revision only if it says it changed.
            locked.MarkAsChanged();
        }
    }

    /// <summary>
    /// Writes the revision that will be signed, into memory, so that it can still be patched.
    /// </summary>
    private static byte[] WriteRevision(PdfDocument document)
    {
        using var buffer = new MemoryStream();
        document.SaveIncremental(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// Finds the run of zeros reserved for the signature, and proves it is the one we wrote.
    /// </summary>
    private static int FindContentsHole(byte[] buffer, int from, int reserved)
    {
        var placeholder = "<" + new string('0', reserved * 2) + ">";
        var at = IndexOf(buffer, PdfEncoders.RawEncoding.GetBytes(placeholder), from);
        if (at < 0)
            throw new InvalidOperationException(
                "The space reserved for the signature is not in the file that was written. Nothing "
                + "should be able to cause this; a filter or an encryption handler rewriting "
                + "/Contents would.");

        return at;
    }

    private static int FindByteRangePlaceholder(byte[] buffer, int from)
    {
        var at = IndexOf(buffer, PdfEncoders.RawEncoding.GetBytes(ByteRangePlaceholder), from);
        if (at < 0)
            throw new InvalidOperationException(
                "The /ByteRange placeholder is not in the file that was written.");

        return at;
    }

    /// <summary>
    /// Writes the real byte range over its placeholder, in exactly the space the placeholder took.
    /// </summary>
    private static void PatchByteRange(byte[] buffer, int at, int holeStart, int holeEnd, int tailLength)
    {
        var patched = "[0 " + Field(holeStart) + " " + Field(holeEnd) + " " + Field(tailLength) + "]";
        if (patched.Length != ByteRangePlaceholder.Length)
            throw new InvalidOperationException(
                "The byte range does not fit the space reserved for it, which means the file is "
                + "larger than a ten-digit offset can name.");

        var bytes = PdfEncoders.RawEncoding.GetBytes(patched);
        Array.Copy(bytes, 0, buffer, at, bytes.Length);
    }

    /// <summary>
    /// One byte-range value, right-aligned in a fixed-width field. Padding with spaces rather than
    /// with leading zeros keeps the numbers readable and is what every other producer does; either
    /// is lawful, since a PDF array does not care how much white space separates its elements.
    /// </summary>
    private static string Field(int value) =>
        value.ToString(CultureInfo.InvariantCulture).PadLeft(ByteRangeFieldWidth);

    /// <summary>
    /// Writes the signature into the reserved hole as hexadecimal, leaving the unused remainder of
    /// the hole as the zeros it already held.
    /// </summary>
    private static void WriteHex(byte[] buffer, int at, byte[] signature, int reserved)
    {
        const string digits = "0123456789ABCDEF";

        for (var index = 0; index < signature.Length; index++)
        {
            buffer[at + index * 2] = (byte)digits[(signature[index] >> 4) & 0xF];
            buffer[at + index * 2 + 1] = (byte)digits[signature[index] & 0xF];
        }

        // The rest of the hole stays as it was written. A reader stops at the length the signature
        // itself declares, so the trailing zeros are padding and not part of it.
        for (var index = signature.Length * 2; index < reserved * 2; index++)
            buffer[at + index] = (byte)'0';
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from)
    {
        for (var at = Math.Max(0, from); at <= haystack.Length - needle.Length; at++)
        {
            var index = 0;
            while (index < needle.Length && haystack[at + index] == needle[index])
                index++;

            if (index == needle.Length)
                return at;
        }

        return -1;
    }
}
