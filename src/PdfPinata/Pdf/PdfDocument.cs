#region Copyright

//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Security;
using PdfPinata.Pdf.Signatures;

// ReSharper disable ConvertPropertyToExpressionBody

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a PDF document.
/// </summary>
[DebuggerDisplay("(Name={Name})")] // A name makes debugging easier
public sealed class PdfDocument : PdfObject, IDisposable
{
    internal DocumentState _state;
    internal PdfDocumentOpenMode _openMode;

    /// <summary>
    /// Creates a new PDF document in memory.
    /// To open an existing PDF file, use the PdfReader class.
    /// </summary>
    public PdfDocument()
    {
        //PdfDocument.Gob.AttatchDocument(Handle);

        _creation = GlobalTimeSettings.Now;
        _state = DocumentState.Created;
        _version = 14;
        Initialize();
        Info.CreationDate = _creation;
    }

    /// <summary>
    /// Creates a new PDF document with the specified file name. The file is immediately created and keeps
    /// locked until the document is closed, at that time the document is saved automatically.
    /// Do not call Save() for documents created with this constructor, just call Close().
    /// To open an existing PDF file and import it, use the PdfReader class.
    /// </summary>
    public PdfDocument(string filename)
    {
        //PdfDocument.Gob.AttatchDocument(Handle);

        _creation = GlobalTimeSettings.Now;
        _state = DocumentState.Created;
        _version = 14;
        Initialize();
        Info.CreationDate = _creation;

        // TODO 4STLA: encapsulate the whole c'tor with #if !NETFX_CORE?
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new PDF document using the specified stream.
    /// The stream won't be used until the document is closed, at that time the document is saved automatically.
    /// Do not call Save() for documents created with this constructor, just call Close().
    /// To open an existing PDF file, use the PdfReader class.
    /// </summary>
    public PdfDocument(Stream outputStream)
    {
        //PdfDocument.Gob.AttatchDocument(Handle);

        _creation = GlobalTimeSettings.Now;
        _state = DocumentState.Created;
        // The version has to be set here as well as in the other two constructors. Without it the
        // field stayed 0 and the file header read "%PDF-0.0", which no reader will open - so every
        // document built on an output stream was written unreadable.
        _version = 14;
        Initialize();
        Info.CreationDate = _creation;

        _outStream = outputStream;
    }

    internal PdfDocument(Lexer lexer)
    {
        //PdfDocument.Gob.AttatchDocument(Handle);

        _creation = GlobalTimeSettings.Now;
        _state = DocumentState.Imported;

        //_info = new PdfInfo(this);
        //_pages = new PdfPages(this);
        //_fontTable = new PdfFontTable();
        //_catalog = new PdfCatalog(this);
        ////_font = new PdfFont();
        //_objects = new PdfObjectTable(this);
        //_trailer = new PdfTrailer(this);
        _irefTable = new PdfCrossReferenceTable(this);
        _lexer = lexer;
    }

    void Initialize()
    {
        //_info = new PdfInfo(this);
        _fontTable = new PdfFontTable(this);
        _imageTable = new PdfImageTable(this);
        _trailer = new PdfTrailer(this);
        _irefTable = new PdfCrossReferenceTable(this);
        _trailer.CreateNewDocumentIDs();
    }

    //~PdfDocument()
    //{
    //  Dispose(false);
    //}

    /// <summary>
    /// Disposes all references to this document stored in other documents. This function should be called
    /// for documents you finished importing pages from. Calling Dispose is technically not necessary but
    /// useful for earlier reclaiming memory of documents you do not need anymore.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        //GC.SuppressFinalize(this);
    }

    void Dispose(bool disposing)
    {
        if (_state != DocumentState.Disposed)
        {
            if (disposing)
            {
                // Dispose managed resources.
            }
            //PdfDocument.Gob.DetatchDocument(Handle);
        }

        _state = DocumentState.Disposed;
    }

    /// <summary>
    /// Gets or sets a user defined object that contains arbitrary information associated with this document.
    /// The tag is not used by PdfPinata.
    /// </summary>
    public object Tag
    {
        get { return _tag; }
        set { _tag = value; }
    }

    object _tag;

    /// <summary>
    /// Gets or sets a value used to distinguish PdfDocument objects.
    /// The name is not used by PdfPinata.
    /// </summary>
    string Name
    {
        get { return _name; }
        set { _name = value; }
    }

    string _name = NewName();

    /// <summary>
    /// Get a new default name for a new document.
    /// </summary>
    static string NewName()
    {
        return "Document " + _nameCount++;
    }

    static int _nameCount;

    /// <summary>
    /// Whether this document may be changed. The same question <see cref="IsReadOnly"/> answers,
    /// asked the way round the operations that refuse ask it.
    /// </summary>
    /// <remarks>
    /// This answered <c>true</c> unconditionally for years, with the real check commented out
    /// beside it, so every operation that guarded on it enforced nothing at all - a caller who
    /// opened a document <see cref="PdfDocumentOpenMode.ReadOnly"/> and added a page got the page
    /// and found out much later, if at all, that none of it would be written. It is defined in
    /// terms of <see cref="IsReadOnly"/> rather than repeating the mode comparison so that the two
    /// cannot drift apart again: there is one question here, not two.
    /// </remarks>
    internal bool CanModify => !IsReadOnly;

    /// <summary>
    /// Refuses an operation that changes the document when the document was not opened for
    /// changing, or when a certifying signature does not permit this kind of change, with a message
    /// naming whichever of the two refused.
    /// </summary>
    /// <param name="operation">
    /// What the caller was trying to do, as a gerund phrase - "adding a page", "saving the
    /// document". It is read as the middle of a sentence.
    /// </param>
    /// <param name="kind">
    /// What kind of change this is, in the terms a <c>/DocMDP</c> certification distinguishes.
    /// Defaults to <see cref="PdfChangeKind.DocumentStructure"/>, which is right for every operation
    /// that changes something other than a form field's value or an annotation - and is also the kind
    /// no certification level ever permits, so an operation that does not pass this explicitly is
    /// refused by any certifying signature exactly as it should be.
    /// </param>
    /// <remarks>
    /// The open mode is checked first. A document opened <see cref="PdfDocumentOpenMode.ReadOnly"/>
    /// that also happens to be certified is refused for the mode, because that is the more fundamental
    /// reason and the one a caller needs to act on first - reopening the document differently, rather
    /// than wondering whether some other change would have been permitted.
    /// </remarks>
    internal void EnsureCanModify(string operation, PdfChangeKind kind = PdfChangeKind.DocumentStructure)
    {
        if (!CanModify)
            throw new InvalidOperationException(PSSR.CannotModify(operation, _openMode));

        var certification = PdfSignatures.CertificationOf(this);
        if (!PdfSignatures.Permits(certification, kind))
            throw new InvalidOperationException(PSSR.CertificationForbids(operation, certification));
    }

    /// <summary>
    /// Closes this instance.
    /// </summary>
    /// <remarks>
    /// Deliberately not guarded by the open mode, where the other operations that write are.
    /// Closing a document is not changing it, and this writes only when the document was
    /// constructed on an output stream - which a document read by <see cref="PdfReader"/> never
    /// is. For a read-only document there is nothing here to refuse.
    /// </remarks>
    public void Close()
    {
        if (_outStream != null)
        {
            // Get security handler if document gets encrypted
            PdfStandardSecurityHandler securityHandler = null;
            if (SecuritySettings.DocumentSecurityLevel != PdfDocumentSecurityLevel.None)
                securityHandler = SecuritySettings.SecurityHandler;

            var writer = new PdfWriter(_outStream, securityHandler);
            try
            {
                DoSave(writer);
            }
            finally
            {
                writer.Close();
            }
        }
    }

    /// <summary>
    /// Saves the document to the specified path. If a file already exists, it will be overwritten.
    /// </summary>
    public void Save(string path)
    {
        EnsureCanModify("saving the document");


        using (Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            Save(stream);
        }
    }

    /// <summary>
    /// Saves the document to the specified stream.
    /// </summary>
    public void Save(Stream stream, bool closeStream)
    {
        EnsureCanModify("saving the document");

        // TODO: more diagnostic checks
        var message = "";
        if (!CanSave(ref message))
            throw new PdfSharpException(message);

        // Saving back into the stream the document was read from is a common way to modify a
        // document in place. Reading has left the position near the end of the stream, so writing
        // would start there and keep the entire original file as a prefix. The result still opens,
        // because readers locate the last startxref, but the file has roughly doubled in size for
        // no reason. All objects were read into memory when the document was opened, so the stream
        // is no longer needed and can be rewound and truncated.
        // The new file is written into a buffer first, and the stream is not touched until that
        // has succeeded. Truncating it up front would leave a save that fails part way through
        // with neither the document it started from nor the one it was asked for.
        MemoryStream buffer = null;
        if (_lexer != null && ReferenceEquals(_lexer.PdfStream, stream) && stream.CanSeek && stream.CanWrite)
            buffer = new MemoryStream();

        // Get security handler if document gets encrypted.
        PdfStandardSecurityHandler securityHandler = null;
        if (SecuritySettings.DocumentSecurityLevel != PdfDocumentSecurityLevel.None)
            securityHandler = SecuritySettings.SecurityHandler;

        PdfWriter writer = null;
        try
        {
            writer = new PdfWriter(buffer ?? stream, securityHandler);
            DoSave(writer);

            if (buffer != null)
            {
                stream.Position = 0;
                stream.SetLength(0);
                buffer.WriteTo(stream);
                stream.Flush();
            }
        }
        finally
        {
            if (buffer != null)
                buffer.Dispose();
            if (stream != null)
            {
                if (closeStream)
                    stream.Dispose();
                else
                    stream.Position = 0; // Reset the stream position if the stream is kept open.
            }

            if (writer != null)
                writer.Close(closeStream);
        }
    }

    /// <summary>
    /// Saves the document to the specified stream.
    /// The stream is not closed by this function.
    /// (Older versions of PDFsharp closes the stream. That was not very useful.)
    /// </summary>
    public void Save(Stream stream)
    {
        Save(stream, false);
    }

    /// <summary>
    /// Appends the changes made to this document to the bytes it was read from, as a new revision,
    /// rather than writing the whole document afresh.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The original bytes are copied through untouched and only the objects that have changed —
    /// together with a new cross-reference section and trailer — are appended. A reader starts at
    /// the last <c>startxref</c> and follows <c>/Prev</c> backwards, so a later definition of an
    /// object shadows the earlier one.
    /// </para>
    /// <para>
    /// This is not an optimisation. For a signed document it is the only lawful way to change
    /// anything: a signature covers a byte range of the file, so rewriting the file invalidates it
    /// and rewriting is what <see cref="Save(Stream)"/> does. It is also what makes a change
    /// auditable, since every earlier revision is still there to be recovered.
    /// </para>
    /// <para>
    /// The document has to have been opened with <see cref="PdfDocumentOpenMode.Append"/>. Any
    /// other mode either renumbers the objects or does not keep the bytes, and both make appending
    /// impossible.
    /// </para>
    /// </remarks>
    public void SaveIncremental(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (_originalBytes == null)
            throw new InvalidOperationException(
                (IsImported
                    ? "This document was opened with PdfDocumentOpenMode." + _openMode + ". "
                    : "This document was created rather than opened. ")
                + "Only a document opened with PdfDocumentOpenMode.Append can be saved incrementally. "
                + "Modify renumbers every object, which makes the appended definitions shadow the "
                + "wrong ones, and a document created from scratch has nothing to append to.");

        // The whole file is written, original bytes and all, so the destination has to be empty.
        // Handing this the file it was read from is the tempting mistake and the damaging one: the
        // original is rewritten over itself, the revision is appended, and because nothing truncates,
        // whatever of the old file ran past the new end survives — including its startxref, which a
        // reader scanning backwards finds first. The appended revision is then ignored in silence,
        // signature and all.
        if (stream.CanSeek && stream.Length != 0)
            throw new ArgumentException(
                "An incremental save writes the whole file and so needs an empty stream to write it "
                + "to. In particular it cannot be given the stream the document was read from: that "
                + "leaves the tail of the old file beyond the new revision, and a reader looking "
                + "backwards for the last startxref finds the stale one.", nameof(stream));

        PrepareForSave();

        stream.Write(_originalBytes, 0, _originalBytes.Length);

        var writer = new PdfWriter(stream, _securitySettings?.SecurityHandler);
        try
        {
            var changed = new List<PdfReference>();
            foreach (var iref in _irefTable.AllReferences)
            {
                if (iref.Value != null && (iref.Value.IsDirty || !_originalObjectNumbers.Contains(iref.ObjectNumber)))
                    changed.Add(iref);
            }

            foreach (var iref in changed)
            {
                iref.Position = writer.Position;
                iref.Value.WriteObject(writer);
            }

            var startxref = writer.Position;
            WriteIncrementalCrossReferenceTable(writer, changed);

            writer.WriteRaw("trailer\n");
            _trailer.Elements.SetInteger("/Size", _irefTable.MaxObjectNumber + 1);

            // Where the previous revision's cross-reference section begins. Without it a reader
            // sees only the handful of objects in this revision and nothing else in the document.
            // The cast is safe because CaptureOriginalBytes refuses a document larger than an array
            // can hold, so this offset lies inside one; checked so that a future change to that
            // refusal fails here rather than writing a negative /Prev.
            _trailer.Elements.SetInteger(PdfTrailer.Keys.Prev, checked((int)_originalStartXref));
            _trailer.WriteObject(writer);
            writer.WriteEof(this, startxref);
        }
        finally
        {
            writer.Stream.Flush();
        }
    }

    /// <summary>
    /// Writes a cross-reference section naming only the objects this revision changed, in runs of
    /// consecutive object numbers.
    /// </summary>
    /// <remarks>
    /// A classic section is a list of subsections, each headed by its first object number and a
    /// count. A full save writes one subsection covering everything; an incremental one writes as
    /// many as the changed numbers fall into runs, because the numbers in between belong to objects
    /// this revision has not touched and whose earlier entries must go on standing.
    /// </remarks>
    static void WriteIncrementalCrossReferenceTable(PdfWriter writer, List<PdfReference> changed)
    {
        writer.WriteRaw("xref\n");

        var ordered = new List<PdfReference>(changed);
        ordered.Sort(PdfReference.Comparer);

        var at = 0;
        while (at < ordered.Count)
        {
            var runLength = 1;
            while (at + runLength < ordered.Count
                   && ordered[at + runLength].ObjectNumber == ordered[at].ObjectNumber + runLength)
                runLength++;

            writer.WriteRaw($"{ordered[at].ObjectNumber} {runLength}\n");
            for (var index = at; index < at + runLength; index++)
            {
                // Exactly 20 bytes per line, as the classic table demands.
                writer.WriteRaw($"{ordered[index].Position:0000000000} {ordered[index].GenerationNumber:00000} n \n");
            }

            at += runLength;
        }
    }

    /// <summary>
    /// Keeps the bytes this document was read from, and the object numbers it was read with, so
    /// that <see cref="SaveIncremental"/> has something to append to and knows which objects are
    /// new. Called by the reader for <see cref="PdfDocumentOpenMode.Append"/> and nothing else.
    /// </summary>
    internal void CaptureOriginalBytes(Stream stream)
    {
        // Appending means holding the original, and an array cannot hold more than this. Said here,
        // where the reason is visible, rather than left to surface as an OutOfMemoryException from
        // an array length nobody wrote. It also makes the offsets below provably fit an int, which
        // is what the /Prev entry and the cross-reference table are written as.
        if (stream.Length > int.MaxValue)
            throw new InvalidOperationException(
                "A document of more than 2 GB cannot be opened with PdfDocumentOpenMode.Append, "
                + "because appending to it means holding the whole of it in a single array. Open it "
                + "with Modify and save it afresh, accepting that this invalidates any signature.");

        var position = stream.Position;
        stream.Position = 0;
        _originalBytes = new byte[stream.Length];
        PdfPinata.Internal.StreamHelper.ReadUpTo(stream, _originalBytes, 0, _originalBytes.Length);
        stream.Position = position;

        _originalStartXref = FindLastStartXref(_originalBytes);

        _originalObjectNumbers = new HashSet<int>();
        foreach (var iref in _irefTable.AllReferences)
        {
            _originalObjectNumbers.Add(iref.ObjectNumber);

            // Reading a document mutates plenty of it — the trailer's own IDs, the page tree as it
            // is flattened — so whatever is dirty at this point is dirty from being read rather
            // than from being changed, and none of it needs writing again.
            if (iref.Value != null)
                iref.Value.IsDirty = false;
        }
    }

    /// <summary>
    /// The offset the last <c>startxref</c> of a file names, which is where a reader of the next
    /// revision has to be told to look for the one before it.
    /// </summary>
    static long FindLastStartXref(byte[] bytes)
    {
        var marker = "startxref";
        var text = PdfEncoders.RawEncoding.GetString(bytes, Math.Max(0, bytes.Length - 2048),
            Math.Min(2048, bytes.Length));

        var at = text.LastIndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
            throw new InvalidOperationException(
                "The document has no startxref, so there is no previous revision to point back at.");

        var digits = new StringBuilder();
        for (var index = at + marker.Length; index < text.Length; index++)
        {
            var ch = text[index];
            if (ch >= '0' && ch <= '9')
                digits.Append(ch);
            else if (digits.Length > 0)
                break;
        }

        return long.Parse(digits.ToString());
    }

    byte[] _originalBytes;
    long _originalStartXref;
    HashSet<int> _originalObjectNumbers;

    /// <summary>
    /// Whether this document has the original bytes <see cref="SaveIncremental"/> needs, which is
    /// true of a document opened with <see cref="PdfDocumentOpenMode.Append"/> and of nothing else.
    /// </summary>
    internal bool CanSaveIncremental => _originalBytes != null;

    /// <summary>
    /// How many bytes an incremental save copies through before anything of its own — so the offset
    /// in the written file at which this revision begins.
    /// </summary>
    internal int OriginalByteCount => _originalBytes?.Length ?? 0;

    /// <summary>
    /// Implements saving a PDF file.
    /// </summary>
    void DoSave(PdfWriter writer)
    {
        if (_pages == null || _pages.Count == 0)
        {
            if (_outStream != null)
            {
                // Give feedback if the wrong constructor was used.
                throw new InvalidOperationException(
                    "Cannot save a PDF document with no pages. Do not use \"public PdfDocument(string filename)\" or \"public PdfDocument(Stream outputStream)\" if you want to open an existing PDF document from a file or stream; use PdfReader.Open() for that purpose.");
            }

            throw new InvalidOperationException("Cannot save a PDF document with no pages.");
        }

        try
        {
            // HACK: Remove XRefTrailer
            if (_trailer is PdfCrossReferenceStream)
            {
                // HACK^2: Preserve the SecurityHandler.
                var securityHandler = _securitySettings.SecurityHandler;
                _trailer = new PdfTrailer((PdfCrossReferenceStream)_trailer);
                _trailer._securityHandler = securityHandler;
            }

            var encrypt = _securitySettings.DocumentSecurityLevel != PdfDocumentSecurityLevel.None;
            if (encrypt)
            {
                var securityHandler = _securitySettings.SecurityHandler;
                if (securityHandler.Reference == null)
                    _irefTable.Add(securityHandler);
                else
                    Debug.Assert(_irefTable.Contains(securityHandler.ObjectID));
                _trailer.Elements[PdfTrailer.Keys.Encrypt] = _securitySettings.SecurityHandler.Reference;
            }
            else
                _trailer.Elements.Remove(PdfTrailer.Keys.Encrypt);

            PrepareForSave();

            if (encrypt)
                _securitySettings.SecurityHandler.PrepareEncryption();

            if (Options.CrossReferenceFormat == PdfCrossReferenceFormat.Stream)
            {
                // A cross-reference stream is a PDF 1.5 construct, so a file that has one may not
                // announce itself as anything earlier. Raised on the field rather than the property:
                // the property refuses a document that cannot be modified, and by here the decision
                // to write has already been taken.
                PdfVersionRequirements.Require(this, 15);

                writer.WriteFileHeader(this);
                var startxref = PdfCrossReferenceStreamWriter.WriteBody(this, writer);
                writer.WriteEof(this, startxref);
            }
            else
            {
                writer.WriteFileHeader(this);
                var irefs = _irefTable.AllReferences;
                var count = irefs.Length;
                for (var idx = 0; idx < count; idx++)
                {
                    var iref = irefs[idx];
                    iref.Position = writer.Position;
                    iref.Value.WriteObject(writer);
                }

                var startxref = writer.Position;
                _irefTable.WriteObject(writer);
                writer.WriteRaw("trailer\n");
                _trailer.Elements.SetInteger("/Size", count + 1);
                _trailer.WriteObject(writer);
                writer.WriteEof(this, startxref);
            }

            //if (encrypt)
            //{
            //  state &= ~DocumentState.SavingEncrypted;
            //  //_securitySettings.SecurityHandler.EncryptDocument();
            //}
        }
        finally
        {
            if (writer != null)
            {
                writer.Stream.Flush();
                // DO NOT CLOSE WRITER HERE
                //writer.Close();
            }
        }
    }

    /// <summary>
    /// Dispatches PrepareForSave to the objects that need it.
    /// </summary>
    internal override void PrepareForSave()
    {
        var info = Info;

        // Add patch level to producer if it is not '0'.
        var pdfSharpProducer = VersionInfo.Producer;
        if (!ProductVersionInfo.VersionPatch.Equals("0"))
            pdfSharpProducer = ProductVersionInfo.Producer2;

        // Set Creator if value is undefined.
        if (info.Elements[PdfDocumentInformation.Keys.Creator] == null)
            info.Creator = pdfSharpProducer;

        // Keep original producer if file was imported.
        var producer = info.Producer;
        if (producer.Length == 0)
            producer = pdfSharpProducer;
        else
        {
            // Prevent endless concatenation if file is edited with PDFsharp more than once.
            if (!producer.StartsWith(VersionInfo.Title))
                producer = pdfSharpProducer + " (Original: " + producer + ")";
        }

        info.Elements.SetString(PdfDocumentInformation.Keys.Producer, producer);

        // Stamp a document opened for modification with the time it was written. This used to be
        // done when the document was opened, which meant that reading one to look at its dates
        // changed the very date being looked at, whether or not anything was ever written. Set the
        // element rather than the property, so that saving twice stamps twice and neither save is
        // taken for a date the caller chose.
        // IsImported tells a document PdfReader opened from a newly created one, which shares the
        // default open mode of Modify and is dated by its creation date alone, as it always was.
        if (IsImported && _openMode == PdfDocumentOpenMode.Modify && !info.ModificationDateIsTheCallersOwn)
            info.Elements.SetDateTime(PdfDocumentInformation.Keys.ModDate, GlobalTimeSettings.Now);

        // Prepare used fonts. A CIDFont subset asks Metadata.PdfConformanceWriter.RequiresCidSet
        // whether the profile claimed wants a /CIDSet, rather than reading Options.Conformance
        // itself — so this may run before or after the conformance writer below without either one
        // caring, both reading the same caller-set property rather than anything the other computes.
        if (_fontTable != null)
            _fontTable.PrepareForSave();

        // Written now rather than as they were named, because a destination points at a page and
        // a page has no object number to point at until the document is being saved.
        if (_namedDestinations != null)
            _namedDestinations.PrepareForSave();

        // Let catalog do the rest.
        Catalog.PrepareForSave();

        // Before the metadata, because a document that is tagged says so in its XMP as well as in
        // its catalog, and before Compact, because the tree's objects reach the catalog only once
        // this has hung them there.
        if (IsTagged)
            Structure.PrepareForSave();

        // After the information dictionary is settled and before anything is compacted away: the
        // XMP packet is built from that dictionary and has to agree with it, and the objects it
        // adds are reachable from the catalog, so Compact leaves them alone.
        Metadata.PdfConformanceWriter.PrepareForSave(this);

        // Neither of these may happen on the way to an incremental save. Renumbering would make
        // every appended definition shadow the wrong object, and compacting would drop objects that
        // are still in the file being appended to — losing track of numbers that remain taken
        // rather than freeing them.
        if (_originalBytes != null)
            return;

        // Remove all unreachable objects (e.g. from deleted pages)
        var removed = _irefTable.Compact();
        if (removed != 0)
            Debug.WriteLine("PrepareForSave: Number of deleted unreachable objects: " + removed);
        _irefTable.Renumber();
    }

    /// <summary>
    /// Determines whether the document can be saved.
    /// </summary>
    public bool CanSave(ref string message)
    {
        if (!SecuritySettings.CanSave(ref message))
            return false;

        return true;
    }

    internal bool HasVersion(string version)
    {
        return String.CompareOrdinal(Catalog.Version, version) >= 0;
    }

    /// <summary>
    /// Gets the document options used for saving the document.
    /// </summary>
    public PdfDocumentOptions Options
    {
        get
        {
            if (_options == null)
                _options = new PdfDocumentOptions();
            return _options;
        }
    }

    PdfDocumentOptions _options;

    /// <summary>
    /// Gets PDF specific document settings.
    /// </summary>
    public PdfDocumentSettings Settings
    {
        get
        {
            if (_settings == null)
                _settings = new PdfDocumentSettings();
            return _settings;
        }
    }

    PdfDocumentSettings _settings;

    /// <summary>
    /// NYI Indicates whether large objects are written immediately to the output stream to relieve
    /// memory consumption.
    /// </summary>
    internal static bool EarlyWrite
    {
        get { return false; }
    }

    /// <summary>
    /// Gets or sets a hook called with the XMP metadata packet just before it is written, for
    /// anything the built-in schemas do not cover.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The packet is built from the information dictionary at save time rather than kept beside it,
    /// so that the two cannot drift apart and have a validator notice. That leaves nowhere for a
    /// caller to reach it, which is what this is for: a PDF/UA identifier, a ZUGFeRD extension
    /// schema, or a namespace nobody has thought of, added through
    /// <see cref="Metadata.XmpMetadata.AdditionalDescriptions"/>.
    /// </para>
    /// <para>
    /// Kept for compatibility, and it is a single slot: assigning it replaces whatever was there
    /// before. A caller who does not know whether another component has already claimed it — an
    /// extension package such as <c>PdfPinata.EInvoice</c>, in particular — should call
    /// <see cref="AddMetadataContributor"/> instead, which cannot replace anyone else's contribution.
    /// Both are invoked, this one first.
    /// </para>
    /// </remarks>
    public Action<Metadata.XmpMetadata> CustomizeMetadata { get; set; }

    /// <summary>
    /// Registers a hook called with the XMP metadata packet just before it is written, alongside
    /// every other one already registered rather than in place of it.
    /// </summary>
    /// <remarks>
    /// <see cref="CustomizeMetadata"/> is a single assignable delegate, which is the wrong shape for
    /// something several independent packages may each want to contribute to: whichever assigns it
    /// last silently drops what an earlier one wrote, and the order two callers happen to run in
    /// becomes a hidden rule neither of them can see. This has no such ordering to get wrong — a
    /// caller who registers a contributor is guaranteed it runs alongside every other one, whatever
    /// else the document is already carrying and whatever registers after it.
    /// </remarks>
    public void AddMetadataContributor(Action<Metadata.XmpMetadata> contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        (_metadataContributors ??= new List<Action<Metadata.XmpMetadata>>()).Add(contributor);
    }

    /// <summary>
    /// Calls <see cref="CustomizeMetadata"/> and every contributor registered through
    /// <see cref="AddMetadataContributor"/>, in the order each was added.
    /// </summary>
    internal void InvokeMetadataContributors(Metadata.XmpMetadata metadata)
    {
        CustomizeMetadata?.Invoke(metadata);

        if (_metadataContributors == null)
            return;

        foreach (var contributor in _metadataContributors)
            contributor(metadata);
    }

    List<Action<Metadata.XmpMetadata>> _metadataContributors;

    /// <summary>
    /// Claims an archival profile for this document, and checks immediately what the claim can be
    /// held to right now: that the document has a title, is not encrypted, its colour mode can be
    /// described, that — for PDF/A-1 — nothing has already pushed the version past what that profile
    /// allows, and — for an <c>A</c> level — that the document is already tagged. Refuses rather than
    /// sets the claim when one of those is not yet true, so the mistake is found where it was made
    /// rather than discovered later at <see cref="Save(Stream)"/>.
    /// </summary>
    /// <remarks>
    /// What cannot be settled by looking at the document now — chiefly, whether it carries an
    /// attachment PDF/A-3 alone may hold — is not checked here, because a caller may legitimately add
    /// one afterwards; that stays a <see cref="Save(Stream)"/>-time rule, kept exactly as it was.
    /// Setting <see cref="PdfDocumentOptions.Conformance"/> directly still works and is still
    /// enforced at <see cref="Save(Stream)"/> — this is an earlier door onto the same enforcement, not
    /// a replacement for it.
    /// </remarks>
    public void ClaimConformance(PdfAConformance conformance)
    {
        Metadata.PdfConformanceWriter.CheckClaimable(this, conformance);
        Metadata.PdfConformanceWriter.CheckVersionAgainstProfile(this, conformance);
        Options.Conformance = conformance;
    }

    /// <summary>
    /// Gets the structure tree of this document, creating it on first use.
    /// </summary>
    /// <remarks>
    /// Asking for this is what makes a document tagged. A document that never does is written
    /// exactly as it was before — no structure tree, no <c>/MarkInfo</c>, and not one extra byte.
    /// </remarks>
    public Structure.PdfStructureBuilder Structure => _structure ??= new Structure.PdfStructureBuilder(this);

    Structure.PdfStructureBuilder _structure;

    /// <summary>
    /// Gets the files this document carries, and the way to attach one.
    /// </summary>
    /// <remarks>
    /// Reading this changes nothing about the document — it looks at the catalog rather than
    /// building anything — so a document that never attaches a file is written exactly as it was
    /// before. Attaching one lists it in both places an attachment belongs: see
    /// <see cref="Advanced.PdfAttachments"/> for why both are needed, and note that only PDF/A-3
    /// among the archival profiles may carry one at all.
    /// </remarks>
    public PdfAttachments Attachments => _attachments ??= new PdfAttachments(this);

    PdfAttachments _attachments;

    /// <summary>
    /// Gets a value indicating whether anything has been tagged, without creating a structure tree
    /// by asking.
    /// </summary>
    internal bool IsTagged => _structure != null;

    /// <summary>
    /// Gets or sets the PDF version number. Return value 14 e.g. means PDF 1.4 / Acrobat 5 etc.
    /// Return value 20 means PDF 2.0.
    /// </summary>
    public int Version
    {
        get { return _version; }
        set
        {
            EnsureCanModify("setting the PDF version");
            if ((value < 12 || value > 17) && value != 20) // TODO not really implemented
                throw new ArgumentException(PSSR.InvalidVersionNumber, nameof(value));
            _version = value;
        }
    }

    internal int _version;

    /// <summary>
    /// Gets the number of pages in the document.
    /// </summary>
    /// <remarks>
    /// There was a second way of counting here, taken when <see cref="CanModify"/> was false and
    /// labelled "PdfOpenMode is InformationOnly": it read <c>/Count</c> off the page tree root
    /// instead of walking the tree. <see cref="CanModify"/> answered true unconditionally, so no
    /// document ever took it. Making that guard live would have taken it for the first time, in a
    /// branch nothing had ever exercised - and needlessly, because every open mode reads the file
    /// in full and builds the page tree. One way of counting is enough.
    /// </remarks>
    public int PageCount
    {
        get { return Pages.Count; }
    }

    /// <summary>
    /// Gets the file size of the document.
    /// </summary>
    public long FileSize
    {
        get { return _fileSize; }
    }

    internal long _fileSize; // TODO: make private

    /// <summary>
    /// Gets the full qualified file name if the document was read form a file, or an empty string otherwise.
    /// </summary>
    public string FullPath
    {
        get { return _fullPath; }
    }

    internal string _fullPath = String.Empty; // TODO: make private

    /// <summary>
    /// Gets a Guid that uniquely identifies this instance of PdfDocument.
    /// </summary>
    public Guid Guid
    {
        get { return _guid; }
    }

    Guid _guid = Guid.NewGuid();

    internal DocumentHandle Handle
    {
        get
        {
            if (_handle == null)
                _handle = new DocumentHandle(this);
            return _handle;
        }
    }

    DocumentHandle _handle;

    /// <summary>
    /// Returns a value indicating whether the document was newly created or opened from an existing document.
    /// Returns true if the document was opened with the PdfReader.Open function, false otherwise.
    /// </summary>
    public bool IsImported
    {
        get { return (_state & DocumentState.Imported) != 0; }
    }

    /// <summary>
    /// Returns a value indicating whether the document is read only or can be modified.
    /// </summary>
    public bool IsReadOnly
    {
        get
        {
            return _openMode != PdfDocumentOpenMode.Modify
                   && _openMode != PdfDocumentOpenMode.Append;
        }
    }

    internal static Exception DocumentNotImported()
    {
        return new InvalidOperationException("Document not imported.");
    }

    /// <summary>
    /// Gets information about the document.
    /// </summary>
    public PdfDocumentInformation Info
    {
        get
        {
            if (_info == null)
                _info = _trailer.Info;
            return _info;
        }
    }

    PdfDocumentInformation _info; // never changes if once created

    /// <summary>
    /// This function is intended to be undocumented.
    /// </summary>
    public PdfCustomValues CustomValues
    {
        get
        {
            if (_customValues == null)
                _customValues = PdfCustomValues.Get(Catalog.Elements);
            return _customValues;
        }
        set
        {
            if (value != null)
                throw new ArgumentException("Only null is allowed to clear all custom values.");
            PdfCustomValues.Remove(Catalog.Elements);
            _customValues = null;
        }
    }

    PdfCustomValues _customValues;

    /// <summary>
    /// Get the pages dictionary.
    /// </summary>
    public PdfPages Pages
    {
        get
        {
            if (_pages == null)
                _pages = Catalog.Pages;
            return _pages;
        }
    }

    PdfPages _pages; // never changes if once created

    /// <summary>
    /// Gets or sets a value specifying the page layout to be used when the document is opened.
    /// </summary>
    public PdfPageLayout PageLayout
    {
        get { return Catalog.PageLayout; }
        set
        {
            EnsureCanModify("setting the page layout");
            Catalog.PageLayout = value;
        }
    }

    /// <summary>
    /// Gets or sets a value specifying how the document should be displayed when opened.
    /// </summary>
    public PdfPageMode PageMode
    {
        get { return Catalog.PageMode; }
        set
        {
            EnsureCanModify("setting the page mode");
            Catalog.PageMode = value;
        }
    }

    /// <summary>
    /// Gets the viewer preferences of this document.
    /// </summary>
    public PdfViewerPreferences ViewerPreferences
    {
        get { return Catalog.ViewerPreferences; }
    }

    /// <summary>
    /// Gets the root of the outline (or bookmark) tree.
    /// </summary>
    public PdfOutlineCollection Outlines
    {
        get { return Catalog.Outlines; }
    }

    /// <summary>
    /// Gets the page labels of the document: what a reader shows for a page instead of its
    /// position, so that front matter can be numbered i, ii, iii while the body starts again
    /// at 1. A document with no labels reads as having none rather than being given any.
    /// </summary>
    public PdfPageLabels PageLabels
    {
        get { return _pageLabels ?? (_pageLabels = new PdfPageLabels(this)); }
    }

    PdfPageLabels _pageLabels;

    /// <summary>
    /// Get the AcroForm dictionary.
    /// </summary>
    public PdfAcroForm AcroForm
    {
        get { return Catalog.AcroForm; }
    }

    /// <summary>
    /// Gets this document's interactive form, making one and putting it in the catalogue if there
    /// is not one yet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The way in to authoring a form. <see cref="AcroForm"/> only reads, and every constructor
    /// under <c>PdfPinata.Pdf.AcroForms</c> used to be internal, so a caller who wanted to
    /// <em>make</em> a form had to assemble ISO 32000-1 section 12.7 out of raw dictionaries.
    /// </para>
    /// <para>
    /// A getter that creates would be a trap - reading a property would write an interactive form
    /// into every document that asked whether it had one - so this is a method and
    /// <see cref="AcroForm"/> is left answering null.
    /// </para>
    /// </remarks>
    public PdfAcroForm GetOrCreateAcroForm()
    {
        EnsureCanModify("creating an interactive form");

        var form = Catalog.AcroForm;
        if (form == null)
        {
            form = new PdfAcroForm(this);
            Internals.AddObject(form);
            Catalog.Elements.SetReference(PdfCatalog.Keys.AcroForm, form);
        }

        return form;
    }

    /// <summary>
    /// Gets or sets the default language of the document.
    /// </summary>
    public string Language
    {
        get { return Catalog.Language; }
        set
        {
            EnsureCanModify("setting the document language");
            Catalog.Language = value;
        }
        //get { return Catalog.Elements.GetString(PdfCatalog.Keys.Lang); }
        //set { Catalog.Elements.SetString(PdfCatalog.Keys.Lang, value); }
    }

    /// <summary>
    /// Gets the security settings of this document.
    /// </summary>
    public PdfSecuritySettings SecuritySettings
    {
        get { return _securitySettings ?? (_securitySettings = new PdfSecuritySettings(this)); }
    }

    internal PdfSecuritySettings _securitySettings;

    /// <summary>
    /// Gets the document font table that holds all fonts used in the current document.
    /// </summary>
    internal PdfFontTable FontTable
    {
        get { return _fontTable ?? (_fontTable = new PdfFontTable(this)); }
    }

    PdfFontTable _fontTable;

    /// <summary>
    /// Gets the document image table that holds all images used in the current document.
    /// </summary>
    internal PdfImageTable ImageTable
    {
        get
        {
            if (_imageTable == null)
                _imageTable = new PdfImageTable(this);
            return _imageTable;
        }
    }

    PdfImageTable _imageTable;

    /// <summary>
    /// Gets the document form table that holds all form external objects used in the current document.
    /// </summary>
    internal PdfFormXObjectTable FormTable // TODO: Rename to ExternalDocumentTable.
    {
        get { return _formTable ?? (_formTable = new PdfFormXObjectTable(this)); }
    }

    PdfFormXObjectTable _formTable;

    /// <summary>
    /// Gets the document ExtGState table that holds all form state objects used in the current document.
    /// </summary>
    internal PdfExtGStateTable ExtGStateTable
    {
        get { return _extGStateTable ?? (_extGStateTable = new PdfExtGStateTable(this)); }
    }

    PdfExtGStateTable _extGStateTable;

    /// <summary>
    /// Gets the PdfCatalog of the current document.
    /// </summary>
    internal PdfCatalog Catalog
    {
        get { return _catalog ?? (_catalog = _trailer.Root); }
    }

    PdfCatalog _catalog; // never changes if once created

    /// <summary>
    /// Gets the named destinations of this document - places in it that can be linked to by name
    /// rather than by page number.
    /// </summary>
    public PdfNamedDestinationTable NamedDestinations
    {
        get { return _namedDestinations ?? (_namedDestinations = new PdfNamedDestinationTable(this)); }
    }

    PdfNamedDestinationTable _namedDestinations;

    /// <summary>
    /// Gets the PdfInternals object of this document, that grants access to some internal structures
    /// which are not part of the public interface of PdfDocument.
    /// </summary>
    public new PdfInternals Internals
    {
        get { return _internals ?? (_internals = new PdfInternals(this)); }
    }

    PdfInternals _internals;

    /// <summary>
    /// Creates a new page, <b>appends it to this document</b>, and returns it.
    /// Depending of the IsMetric property of the current region the page size is set to
    /// A4 or Letter respectively. If this size is not appropriate it should be changed before
    /// any drawing operations are performed on the page.
    /// <para>
    /// The page returned is already part of this document. Do not pass it to
    /// <see cref="AddPage(PdfPage, AnnotationCopyingType)"/> or
    /// <see cref="InsertPage(int, PdfPage, AnnotationCopyingType)"/> afterwards - that places
    /// the same page twice and throws. Draw on the page returned here instead.
    /// </para>
    /// <para>
    /// To build a page before deciding where it goes, use <c>new PdfPage(document)</c>, which
    /// creates a drawable page without adding it to the page tree, and place it later with
    /// <see cref="PlacePage"/>.
    /// </para>
    /// </summary>
    [MustUseReturnValue]
    public PdfPage AddPage()
    {
        return Catalog.Pages.Add();
    }

    /// <summary>
    /// Adds the specified page to this document. If the page is from an external document,
    /// it is imported to this document. In this case the returned page is not the same
    /// object as the specified one.
    /// <para>
    /// <b>Always use the value returned</b> rather than the page passed in: whether the two are
    /// the same object depends on which document owned the page, which is not visible at the
    /// call site. Prefer <see cref="ImportPage"/> or <see cref="PlacePage"/>, which each do one
    /// of those two things and say which in their name.
    /// </para>
    /// </summary>
    [MustUseReturnValue]
    public PdfPage AddPage(PdfPage page, AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        return Catalog.Pages.Add(page, annotationCopying);
    }

    /// <summary>
    /// Creates a new page, <b>inserts it in this document</b> at the specified position, and
    /// returns it. The page returned is already part of this document - draw on it rather than
    /// passing it to another Add or Insert call.
    /// </summary>
    [MustUseReturnValue]
    public PdfPage InsertPage(int index)
    {
        return Catalog.Pages.Insert(index);
    }

    /// <summary>
    /// Inserts the specified page in this document. If the page is from an external document,
    /// it is imported to this document. In this case the returned page is not the same
    /// object as the specified one.
    /// <para>
    /// <b>Always use the value returned</b> rather than the page passed in: whether the two are
    /// the same object depends on which document owned the page, which is not visible at the
    /// call site. Prefer <see cref="ImportPage"/> or <see cref="PlacePage"/>, which each do one
    /// of those two things and say which in their name.
    /// </para>
    /// </summary>
    [MustUseReturnValue]
    public PdfPage InsertPage(int index, PdfPage page,
        AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        return Catalog.Pages.Insert(index, page, annotationCopying);
    }

    /// <summary>
    /// Places a page this document already owns but has not yet added to the page tree, and
    /// returns that same page object.
    /// <para>
    /// This is the counterpart of <c>new PdfPage(document)</c>: create a page, draw on it, then
    /// place it where you want it. Unlike <see cref="AddPage(PdfPage, AnnotationCopyingType)"/>
    /// the page returned is always the page passed in, and a page from another document is
    /// rejected rather than silently imported.
    /// </para>
    /// </summary>
    /// <param name="index">The position to place the page at.</param>
    /// <param name="page">A page owned by this document that is not yet placed.</param>
    public PdfPage PlacePage(int index, PdfPage page)
    {
        return Catalog.Pages.Place(index, page);
    }

    /// <summary>
    /// Imports a page from another document and returns the imported copy, which is always a
    /// different object from the page passed in.
    /// <para>
    /// Unlike <see cref="AddPage(PdfPage, AnnotationCopyingType)"/> this always copies, so the
    /// return value never aliases the argument. A page this document already owns is rejected.
    /// The source document must be opened with <see cref="PdfDocumentOpenMode.Import"/>.
    /// </para>
    /// </summary>
    /// <param name="index">The position to place the imported copy at.</param>
    /// <param name="page">A page belonging to another document.</param>
    /// <param name="annotationCopying">Annotation copying action, by default annotations are copied shallowly.</param>
    [MustUseReturnValue]
    public PdfPage ImportPage(int index, PdfPage page,
        AnnotationCopyingType annotationCopying = AnnotationCopyingType.ShallowCopy)
    {
        return Catalog.Pages.Import(index, page, annotationCopying);
    }

    /// <summary>
    /// Adds a second page showing the content of an existing page of this document, and
    /// returns the new page.
    /// <para>
    /// The duplicate shares the content stream of the source rather than copying its bytes, so
    /// the file does not grow by the size of the page, but it gets a resource dictionary of its
    /// own so that later drawing on either page does not affect the other. Annotations are not
    /// carried over, because an annotation names the page that owns it.
    /// </para>
    /// </summary>
    /// <param name="sourceIndex">The index of the page to duplicate.</param>
    /// <param name="index">The index to place the duplicate at.</param>
    [MustUseReturnValue]
    public PdfPage DuplicatePage(int sourceIndex, int index)
    {
        return Catalog.Pages.Duplicate(sourceIndex, index);
    }

    /// <summary>
    /// Moves a page within the page sequence of this document.
    /// </summary>
    /// <param name="oldIndex">The page index before this operation.</param>
    /// <param name="newIndex">The page index after this operation.</param>
    public void MovePage(int oldIndex, int newIndex)
    {
        Catalog.Pages.MovePage(oldIndex, newIndex);
    }

    /// <summary>
    /// Marks the acroform fields readonly
    /// </summary>
    public void MakeAcroFormsReadOnly()
    {
        for (var i = 0; i < AcroForm?.Fields.Count(); i++)
        {
            AcroForm.Fields[i].ReadOnly = true;
        }
    }

    /// <summary>
    /// Drops from each page the resources it does not draw with.
    /// <para>
    /// Pages of a document commonly share one resource dictionary naming every font and image in
    /// it, whether or not a given page draws with them. Importing such a page brings all of them
    /// along, which is why splitting a document into one file per page can give every file the
    /// weight of the whole document. Call this on the document that is about to be saved.
    /// </para>
    /// <para>
    /// A page whose content cannot be read in full is left as it stands, so this never drops a
    /// resource a page turns out to need.
    /// </para>
    /// </summary>
    public void PruneUnusedResources()
    {
        foreach (var page in Pages)
            PdfResourcePruner.Prune(page);
    }

    /// <summary>
    /// Changes every page of this document to the size given, moving what is drawn on each page
    /// into the new size rather than cropping it, and moving the annotations and the destinations
    /// that point at each page along with the content.
    /// </summary>
    /// <param name="size">The size the pages are to become.</param>
    /// <param name="orientation">Which way round that size goes.</param>
    /// <param name="options">
    /// How the content is to be fitted into the new size. Null fits each page in whole, centred.
    /// Turn on <see cref="PageResizeOptions.AutoRotate"/> to normalise a document of mixed
    /// orientation without shrinking the pages that are the other way round.
    /// </param>
    /// <remarks>
    /// Prefer this to calling <see cref="PdfPage.Resize(PageSize, PageOrientation, PageResizeOptions)"/>
    /// on each page in turn. Finding the destinations that point at a page means walking the whole
    /// document, and this walks it once for all the pages rather than once for each of them.
    /// </remarks>
    public void ResizePages(PageSize size, PageOrientation orientation = PageOrientation.Portrait,
        PageResizeOptions options = null)
    {
        if (!Enum.IsDefined(size))
            throw new InvalidEnumArgumentException(nameof(size), (int)size, typeof(PageSize));

        PdfPageResizer.ResizeAll(this, PdfPage.SizeInPoints(size, orientation), size, options);
    }

    /// <summary>
    /// Changes every page of this document to a size in points, moving what is drawn on each page
    /// into the new size rather than cropping it.
    /// </summary>
    /// <param name="size">The size the pages are to become, in points, as the reader will see it.</param>
    /// <param name="options">How the content is to be fitted into the new size.</param>
    public void ResizePages(XSize size, PageResizeOptions options = null)
    {
        PdfPageResizer.ResizeAll(this, size, PageSize.Undefined, options);
    }

    /// <summary>
    /// Replaces images with identical content by a single shared XObject, so that a document which
    /// drew the same picture on many pages carries one copy of it. Images are matched by the MD5 of
    /// their stream, so only byte-identical ones are merged.
    /// </summary>
    public void ConsolidateImages()
    {
        PdfImageConsolidator.Consolidate(Pages);
    }

    /// <summary>
    /// Gets the security handler.
    /// </summary>
    public PdfStandardSecurityHandler SecurityHandler
    {
        get { return _trailer.SecurityHandler; }
    }

    internal PdfTrailer _trailer;
    internal PdfCrossReferenceTable _irefTable;
    internal Stream _outStream;

    // Imported Document
    internal Lexer _lexer;

    internal DateTime _creation;

    /// <summary>
    /// Occurs when the specified document is not used anymore for importing content.
    /// </summary>
    internal void OnExternalDocumentFinalized(DocumentHandle handle)
    {
        if (tls != null)
        {
            //PdfDocument[] documents = tls.Documents;
            tls.DetachDocument(handle);
        }

        if (_formTable != null)
            _formTable.DetachDocument(handle);
    }

    //internal static GlobalObjectTable Gob = new GlobalObjectTable();

    /// <summary>
    /// Gets the ThreadLocalStorage object. It is used for caching objects that should created
    /// only once.
    /// </summary>
    internal static ThreadLocalStorage Tls
    {
        get { return tls ?? (tls = new ThreadLocalStorage()); }
    }

    [ThreadStatic] static ThreadLocalStorage tls;

    /// <summary>
    /// A comparable, storable stand-in for "this document, weakly", so that the tables which
    /// remember an imported document do not keep it alive.
    /// <para>
    /// Identity is the document's own <see cref="Guid"/>. It used to be that Guid reformatted
    /// into a string, which is the same information said less directly and compared more
    /// slowly.
    /// </para>
    /// </summary>
    [DebuggerDisplay("(Id={Id}, alive={IsAlive})")]
    internal class DocumentHandle
    {
        public DocumentHandle(PdfDocument document)
        {
            _weakRef = new WeakReference<PdfDocument>(document);
            Id = document.Guid;
        }

        public bool IsAlive
        {
            get { return _weakRef.TryGetTarget(out _); }
        }

        public PdfDocument Target
        {
            get { return _weakRef.TryGetTarget(out var document) ? document : null; }
        }

        readonly WeakReference<PdfDocument> _weakRef;

        readonly Guid Id;

        public override bool Equals(object obj)
        {
            var handle = obj as DocumentHandle;
            if (!ReferenceEquals(handle, null))
                return Id == handle.Id;
            return false;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

#pragma warning disable S3875 // A handle is equal to another by the document id it carries, and ThreadLocalStorage and PdfFormXObjectTable compare handles with ==.
        public static bool operator ==(DocumentHandle left, DocumentHandle right)
        {
            return left?.Equals(right) ?? ReferenceEquals(right, null);
        }
#pragma warning restore S3875

        public static bool operator !=(DocumentHandle left, DocumentHandle right)
        {
            return !(left == right);
        }
    }
}
