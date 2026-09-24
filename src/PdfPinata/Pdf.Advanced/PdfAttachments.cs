using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// The files a document carries as a whole, and the way to add one.
/// </summary>
/// <remarks>
/// <para>
/// An attachment reaches a document through two places at once, and both are needed. The catalog's
/// <c>/AF</c> array <em>associates</em> the file with the document — which is what makes it part of
/// the document rather than merely present in it, and what PDF/A-3 requires of every embedded file
/// — while the <c>/Names /EmbeddedFiles</c> name tree is what a viewer reads to populate its
/// attachments pane. A file in only the first is invisible to a reader; a file in only the second
/// is invisible to a validator. <see cref="Add(string,byte[],PdfAFRelationship,string,string)"/>
/// writes both.
/// </para>
/// <para>
/// Reading looks in both, so an attachment put there by another producer is found whichever of the
/// two that producer wrote. The order is the <c>/AF</c> order first, then anything the name tree
/// holds that <c>/AF</c> does not.
/// </para>
/// <para>
/// PDF/A-3 is the only archival profile that may carry an attachment at all, and it is what the
/// hybrid e-invoice formats — ZUGFeRD, Factur-X — are built on. Attaching the invoice XML with
/// <see cref="PdfAFRelationship.Data"/> and claiming
/// <see cref="PdfAConformance.PdfA3B"/> is the whole of what those formats ask of the PDF side.
/// </para>
/// </remarks>
public sealed class PdfAttachments : IEnumerable<PdfFileSpecification>
{
    /// <summary>
    /// The kind of name tree an attachment is listed in, under the catalog's <c>/Names</c>.
    /// </summary>
    private const string EmbeddedFiles = "/EmbeddedFiles";

    /// <summary>
    /// What ISO 19005-3 says to write as an attachment's media type when the real one is not known.
    /// </summary>
    private const string UnknownMediaType = "application/octet-stream";

    private readonly PdfDocument _document;

    internal PdfAttachments(PdfDocument document)
    {
        _document = document;
    }

    /// <summary>
    /// Gets how many files the document carries.
    /// </summary>
    public int Count => Specifications().Count;

    /// <summary>
    /// Gets the file at the given position, in the order <see cref="GetEnumerator"/> reports.
    /// </summary>
    public PdfFileSpecification this[int index]
    {
        get
        {
            var specifications = Specifications();
            if (index < 0 || index >= specifications.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "The document carries " + specifications.Count + " attachment(s).");
            return specifications[index];
        }
    }

    /// <summary>
    /// Attaches a file to the document and returns the specification describing it, so that a caller
    /// can set anything this does not cover.
    /// </summary>
    /// <param name="fileName">
    /// The name the file is carried under, which is both what a reader shows and the key it is found
    /// by. Two attachments may not share one.
    /// </param>
    /// <param name="bytes">The content of the file. Embedded as it is given, uncompressed by this.</param>
    /// <param name="relationship">
    /// What the file has to do with the document. The default says nothing, honestly; an e-invoice
    /// wants <see cref="PdfAFRelationship.Data"/>.
    /// </param>
    /// <param name="description">What a viewer shows beside the name, or null for none.</param>
    /// <param name="mimeType">
    /// The media type, written as the embedded stream's <c>/Subtype</c> — "text/xml" for an invoice.
    /// Null leaves it unsaid.
    /// </param>
    public PdfFileSpecification Add(string fileName, byte[] bytes,
        PdfAFRelationship relationship = PdfAFRelationship.Unspecified,
        string description = null, string mimeType = null)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (fileName.Length == 0)
            throw new ArgumentException(
                "An attachment has to be named: the name is what a reader shows and what the file is "
                + "found by.", nameof(fileName));
        ArgumentNullException.ThrowIfNull(bytes);

        if (IsTaken(fileName))
            throw new InvalidOperationException(
                "This document already carries a file called '" + fileName + "'. A name is what "
                + "identifies an attachment to a reader and what it is listed under, so two cannot "
                + "share one — attach the second under a name of its own.");

        var embedded = new PdfEmbeddedFile(_document, bytes)
        {
            // Always said, and said as octet-stream when the caller had nothing to say. PDF/A-3 requires
            // the media type of every attachment, and the standard names this value for "unknown" —
            // so leaving the entry out to be honest about not knowing produces a file that conforms to
            // nothing, where writing it says exactly as much and conforms.
            MimeType = string.IsNullOrEmpty(mimeType) ? UnknownMediaType : mimeType,

            // Stamped rather than left out. PDF/A-3 requires it of an attachment, and an archive that
            // cannot say how old the thing it is keeping is has lost half the point of keeping it — the
            // file system the bytes came from is not there to be asked afterwards.
            ModificationDate = GlobalTimeSettings.Now
        };

        var specification = new PdfFileSpecification(_document, fileName, embedded)
        {
            Relationship = relationship,
            Description = description
        };

        // Indirect because it has to be: ISO 32000-1 says a file specification carrying /EF is
        // referenced indirectly, and both places it goes below hold a reference to it rather than
        // a copy — which is what keeps them talking about one file instead of two.
        _document._irefTable.Add(specification);

        // An attachment is written with entries later than the header this library defaults to: /UF
        // is PDF 1.7 and /AF later still, and a document announcing 1.4 while carrying them tells a
        // reader it may ignore exactly the parts that make the attachment findable. A PDF/A-3 claim
        // raises the same floor, so this is what the document that makes no claim was missing.
        PdfVersionRequirements.Require(_document, 17);

        Associate(specification);
        Register(fileName, specification);

        return specification;
    }

    /// <summary>
    /// Associates a file specification the caller already has with the document, by listing it in
    /// the catalog's <c>/AF</c> array. Answers whether it was added, which is false if it was listed
    /// already.
    /// </summary>
    /// <remarks>
    /// What <see cref="Add(string,byte[],PdfAFRelationship,string,string)"/> does for a file it has
    /// just embedded, offered on its own for the file that arrived another way — the one on a
    /// <see cref="Annotations.PdfFileAttachmentAnnotation"/>, most of all. Such a file is in the
    /// document and PDF/A-3 requires it to be associated, and there is nothing to attach a second
    /// time: it is the same bytes, wanting one more mention. The name tree is deliberately not
    /// touched, because an annotation's attachment is shown by the annotation.
    /// </remarks>
    public bool Associate(PdfFileSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        if (!specification.IsIndirect)
            _document._irefTable.Add(specification);

        var associated = AssociatedFiles();
        foreach (var item in associated.Elements)
        {
            if (ReferenceEquals(Resolve(item), specification))
                return false;
        }

        associated.Elements.Add(specification.Reference);
        return true;
    }

    /// <summary>
    /// Stops the document carrying the given file, and answers whether it was carrying it. The
    /// bytes are not hunted down and deleted: an object nothing reaches is dropped when the
    /// document is saved.
    /// </summary>
    public bool Remove(PdfFileSpecification specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        var removedFromAssociated = RemoveFromAssociated(specification);
        var removedFromLeaves = RemoveFromLeaves(specification);
        return removedFromAssociated || removedFromLeaves;
    }

    /// <summary>
    /// Removes the file from the catalog's <c>/AF</c> array, dropping the array once it is empty.
    /// </summary>
    private bool RemoveFromAssociated(PdfFileSpecification specification)
    {
        var associated = _document.Catalog.Elements.GetArray(PdfCatalog.Keys.AF);
        if (associated == null)
            return false;

        var removed = false;
        for (var idx = associated.Elements.Count - 1; idx >= 0; idx--)
        {
            if (!ReferenceEquals(Resolve(associated.Elements[idx]), specification))
                continue;

            associated.Elements.RemoveAt(idx);
            removed = true;
        }

        if (associated.Elements.Count == 0)
            _document.Catalog.Elements.Remove(PdfCatalog.Keys.AF);

        return removed;
    }

    /// <summary>
    /// Removes the file from the <c>/EmbeddedFiles</c> name tree's leaves.
    /// </summary>
    private bool RemoveFromLeaves(PdfFileSpecification specification)
    {
        var leaves = Leaves();
        if (leaves == null)
            return false;

        var removed = false;
        // Backwards, and two at a time: the array alternates key and value, so removing a pair
        // from the front would renumber every pair after it mid-walk.
        for (var idx = leaves.Elements.Count - 2; idx >= 0; idx -= 2)
        {
            if (!ReferenceEquals(Resolve(leaves.Elements[idx + 1]), specification))
                continue;

            leaves.Elements.RemoveAt(idx + 1);
            leaves.Elements.RemoveAt(idx);
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// Gets an enumerator over the files the document carries.
    /// </summary>
    public IEnumerator<PdfFileSpecification> GetEnumerator() => Specifications().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Every file the document carries, from the association array first and then from the name
    /// tree, without repeating one that is in both — which, for anything this library wrote, is
    /// all of them.
    /// </summary>
    private List<PdfFileSpecification> Specifications() => Reachable(includeAnnotations: false);

    /// <summary>
    /// Every file specification the document can be reached through: the catalog's <c>/AF</c> array
    /// first, then the <c>/EmbeddedFiles</c> name tree, and — when asked for — each page's file
    /// attachment annotations, without repeating one that is in more than one of them.
    /// </summary>
    /// <param name="includeAnnotations">
    /// Whether to reach a file that hangs off a <see cref="Annotations.PdfFileAttachmentAnnotation"/>
    /// and is listed nowhere else. False is what the public enumeration wants, because an
    /// annotation's attachment is shown by the annotation rather than listed a second time — see
    /// <see cref="Associate"/>. True is what PDF/A-3's association rule wants, because that rule is
    /// about the file being in the document and not about how it got there, and annotation
    /// attachment was the one path onto a document a name-tree-only check could not see.
    /// </param>
    /// <remarks>
    /// The one walk, so that the public surface and
    /// <see cref="Metadata.PdfConformanceWriter"/> cannot drift apart over what a document carries.
    /// It answers what is reachable and nothing more: whether a specification carries bytes or
    /// merely names a file somewhere else is a PDF/A question, and stays beside the rule that asks
    /// it. No cycle guard of its own is needed either — <c>/AF</c> and a page's <c>/Annots</c> are
    /// flat arrays, and <see cref="PdfNameTree"/> already guards the one structure that can lead
    /// back into itself.
    /// </remarks>
    internal List<PdfFileSpecification> Reachable(bool includeAnnotations)
    {
        var found = new List<PdfFileSpecification>();

        var associated = _document.Catalog.Elements.GetArray(PdfCatalog.Keys.AF);
        if (associated != null)
        {
            foreach (var item in associated.Elements)
                AddOnce(found, item);
        }

        foreach (var entry in PdfNameTree.Enumerate(_document, EmbeddedFiles))
            AddOnce(found, entry.Value);

        if (includeAnnotations)
            AddAnnotationAttachments(found);

        return found;
    }

    /// <summary>
    /// Adds the file each page's annotations hang off by <c>/FS</c>.
    /// </summary>
    private void AddAnnotationAttachments(List<PdfFileSpecification> found)
    {
        foreach (var page in _document.Pages)
        {
            var annotations = page.Elements.GetArray("/Annots");
            if (annotations == null)
                continue;

            foreach (var item in annotations.Elements)
            {
                var annotation = Dictionary(item);
                if (annotation != null)
                    AddOnce(found, annotation.Elements["/FS"]);
            }
        }
    }

    private static void AddOnce(List<PdfFileSpecification> found, PdfItem item)
    {
        var specification = Resolve(item);
        if (specification == null)
            return;

        // By instance rather than by name. Resolving transforms the dictionary in place and points
        // the reference at the result, so the same file reached from both places is the same object
        // the second time — while two files that happen to share a name stay two.
        foreach (var existing in found)
        {
            if (ReferenceEquals(existing, specification))
                return;
        }

        found.Add(specification);
    }

    /// <summary>
    /// The specification an entry stands for, whichever of the three shapes it arrived in: already
    /// this type, an indirect reference to one, or the plain dictionary a reader parsed.
    /// </summary>
    internal static PdfFileSpecification Resolve(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfFileSpecification specification)
            return specification;

        // Transforming re-points the reference at the new instance, so a second look answers the
        // same object rather than building another wrapper around the same dictionary.
        return item is PdfDictionary dictionary ? new PdfFileSpecification(dictionary) : null;
    }

    /// <summary>
    /// The dictionary an annotation entry stands for, whether the array holds it directly or holds
    /// a reference to it.
    /// </summary>
    private static PdfDictionary Dictionary(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;
        return item as PdfDictionary;
    }

    /// <summary>
    /// The catalog's association array, made on first use.
    /// </summary>
    private PdfArray AssociatedFiles()
    {
        var associated = _document.Catalog.Elements.GetArray(PdfCatalog.Keys.AF);
        if (associated == null)
        {
            associated = new PdfArray(_document);
            _document.Catalog.Elements[PdfCatalog.Keys.AF] = associated;
        }
        return associated;
    }

    /// <summary>
    /// The one leaf of the <c>/EmbeddedFiles</c> name tree, or null if the document has no tree yet.
    /// </summary>
    private PdfArray Leaves()
    {
        return PdfNameTree.Root(_document, EmbeddedFiles)?.Elements.GetArray("/Names");
    }

    /// <summary>
    /// Whether the document already carries a file under this name, wherever it is listed.
    /// </summary>
    private bool IsTaken(string fileName)
    {
        foreach (var specification in Specifications())
        {
            if (string.Equals(specification.FileName, fileName, StringComparison.Ordinal))
                return true;
        }

        foreach (var entry in PdfNameTree.Enumerate(_document, EmbeddedFiles))
        {
            if (string.Equals(entry.Key, fileName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Lists the file in the <c>/EmbeddedFiles</c> name tree, which is what a viewer reads to show
    /// a document's attachments.
    /// </summary>
    /// <remarks>
    /// One flat leaf, however many files there are. A name tree may be nested and has to be sorted,
    /// and nesting exists so that a reader can binary-search a tree of thousands without loading it
    /// — which is a real concern for the destinations of a large document and no concern at all for
    /// attachments, of which a document has a handful. Sorted it must be regardless, because a
    /// reader is entitled to binary-search even one node.
    /// </remarks>
    private void Register(string fileName, PdfFileSpecification specification)
    {
        var names = _document.Catalog.Elements.GetDictionary(PdfCatalog.Keys.Names);
        if (names == null)
        {
            names = new PdfDictionary(_document);
            _document._irefTable.Add(names);
            _document.Catalog.Elements[PdfCatalog.Keys.Names] = names.Reference;
        }

        var root = names.Elements.GetDictionary(EmbeddedFiles);
        if (root == null)
        {
            root = new PdfDictionary(_document);
            names.Elements.SetObject(EmbeddedFiles, root);
        }

        var leaves = root.Elements.GetArray("/Names");
        if (leaves == null)
        {
            leaves = new PdfArray(_document);
            root.Elements.SetObject("/Names", leaves);
        }

        // Where the name belongs, by the byte order a reader compares keys in. The pair goes in
        // together, so the array keeps alternating key and value.
        var at = leaves.Elements.Count;
        for (var idx = 0; idx + 1 < leaves.Elements.Count; idx += 2)
        {
            if (string.CompareOrdinal(PdfNameTree.TextOf(leaves.Elements[idx]) ?? "", fileName) > 0)
            {
                at = idx;
                break;
            }
        }

        leaves.Elements.Insert(at, new PdfString(fileName));
        leaves.Elements.Insert(at + 1, specification.Reference);
    }
}
