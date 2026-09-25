#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Diagnostics;
using System.IO;
using PdfPinata.Exceptions;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Internal;
using PdfPinata.Internal;
using PdfPinata.Pdf.IO.enums;

namespace PdfPinata.Pdf.IO;

/// <summary>
/// Encapsulates the arguments of the PdfPasswordProvider delegate.
/// </summary>
public class PdfPasswordProviderArgs
{
    /// <summary>
    /// Sets the password to open the document with.
    /// </summary>
    public string Password;

    /// <summary>
    /// When set to true the PdfReader.Open function returns null indicating that no PdfDocument was created.
    /// </summary>
    public bool Abort;
}

/// <summary>
/// A delegated used by the PdfReader.Open function to retrieve a password if the document is protected.
/// </summary>
public delegate void PdfPasswordProvider(PdfPasswordProviderArgs args);

/// <summary>
/// Represents the functionality for reading PDF documents.
/// </summary>
public static class PdfReader
{
    /// <summary>
    /// Determines whether the file specified by its path is a PDF file by inspecting the first eight
    /// bytes of the data. If the file header has the form «%PDF-x.y» the function returns the version
    /// number as integer (e.g. 14 for PDF 1.4). If the file header is invalid or inaccessible
    /// for any reason, 0 is returned. The function never throws an exception.
    /// </summary>
    public static int TestPdfFile(string path)
    {
        FileStream stream = null;
        try
        {
            var realPath = Drawing.XPdfForm.ExtractPageNumber(path, out _);
            if (File.Exists(realPath)) // prevent unwanted exceptions during debugging
            {
                stream = new FileStream(realPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var bytes = new byte[1024];
                // A file shorter than the buffer is normal here; the remainder stays zero.
                PdfPinata.Internal.StreamHelper.ReadUpTo(stream, bytes, 0, 1024);
                return GetPdfFileVersion(bytes);
            }
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch { }
        finally
        {
            try
            {
                stream?.Dispose();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch
            {
            }
        }
        return 0;
    }

    /// <summary>
    /// Determines whether the specified stream is a PDF file by inspecting the first eight
    /// bytes of the data. If the data begins with «%PDF-x.y» the function returns the version
    /// number as integer (e.g. 14 for PDF 1.4). If the data is invalid or inaccessible
    /// for any reason, 0 is returned. The function never throws an exception.
    /// </summary>
    public static int TestPdfFile(Stream stream)
    {
        long pos = -1;
        try
        {
            pos = stream.Position;
            var bytes = new byte[1024];
            // A file shorter than the buffer is normal here; the remainder stays zero.
            PdfPinata.Internal.StreamHelper.ReadUpTo(stream, bytes, 0, 1024);
            return GetPdfFileVersion(bytes);
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch { }
        finally
        {
            try
            {
                if (pos != -1)
                    stream.Position = pos;
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch { }
        }
        return 0;
    }

    /// <summary>
    /// Determines whether the specified data is a PDF file by inspecting the first eight
    /// bytes of the data. If the data begins with «%PDF-x.y» the function returns the version
    /// number as integer (e.g. 14 for PDF 1.4). If the data is invalid or inaccessible
    /// for any reason, 0 is returned. The function never throws an exception.
    /// </summary>
    public static int TestPdfFile(byte[] data)
    {
        return GetPdfFileVersion(data);
    }

    /// <summary>
    /// Implements scanning the PDF file version.
    /// </summary>
    ///
    private static int GetPdfFileVersion(byte[] bytes)
    {
        var version = ScanFileVersion(PdfEncoders.RawEncoding, bytes);

        // If it doesn't work with the specified encoding the file might be incorrectly encoded as ASCII.
        if (version == 0)
            version = ScanFileVersion(System.Text.Encoding.ASCII, bytes);

        return version;
    }

    /// <summary>
    /// Scans the file header for «%PDF-x.y» using the specified encoding and returns the version
    /// as an integer (e.g. 14 for PDF 1.4, 20 for PDF 2.0), or 0 if no version was found.
    /// </summary>
    private static int ScanFileVersion(System.Text.Encoding encoding, byte[] bytes)
    {
        string header;
        try
        {
            header = encoding.GetString(bytes, 0, bytes.Length);
        }
        // Only the decoding can throw - a null array reaches here from the public TestPdfFile - and
        // bytes that cannot be decoded have no version to find.
        catch
        {
            return 0;
        }

        return VersionIn(header);
    }

    /// <summary>
    /// Finds «PDF-x.y» in a decoded header and returns it as an integer, or 0 if there is none.
    /// </summary>
    private static int VersionIn(string header)
    {
        // Acrobat accepts headers like «%!PS-Adobe-N.n PDF-M.m»...
        var looksLikePdf = (header.Length > 0 && header[0] == '%') || header.Contains("%PDF", StringComparison.Ordinal);
        if (!looksLikePdf)
            return 0;

        var ich = header.IndexOf("PDF-", StringComparison.Ordinal);
        if (ich <= 0 || ich + 6 >= header.Length || header[ich + 5] != '.')
            return 0;

        var major = header[ich + 4];
        var minor = header[ich + 6];

        // PDF 1.0 to 1.7 and PDF 2.0 are the versions defined so far.
        if (major is < '1' or > '2' || minor is < '0' or > '9')
            return 0;

        return (major - '0') * 10 + (minor - '0');
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, PdfDocumentOpenMode openmode)
    {
        return Open(path, null, openmode, null, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, PdfDocumentOpenMode openmode, PdfReadAccuracy accuracy)
    {
        return Open(path, null, openmode, null, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, PdfDocumentOpenMode openmode, PdfPasswordProvider provider)
    {
        return Open(path, null, openmode, provider, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, PdfDocumentOpenMode openmode, PdfPasswordProvider provider, PdfReadAccuracy accuracy)
    {
        return Open(path, null, openmode, provider, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password, PdfDocumentOpenMode openmode)
    {
        return Open(path, password, openmode, null, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password, PdfDocumentOpenMode openmode, PdfReadAccuracy accuracy)
    {
        return Open(path, password, openmode, null, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password, PdfDocumentOpenMode openmode, PdfPasswordProvider provider)
    {
        return Open(path, password, openmode, provider, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password, PdfDocumentOpenMode openmode, PdfPasswordProvider provider, PdfReadAccuracy accuracy)
    {
        PdfDocument document;
        Stream stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            document = Open(stream, password, openmode, provider, accuracy);
            document?.FullPath = Path.GetFullPath(path);
        }
        finally
        {
            stream?.Dispose();

        }
        return document;
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path)
    {
        return Open(path, null, PdfDocumentOpenMode.Modify, null, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, PdfReadAccuracy accuracy)
    {
        return Open(path, null, PdfDocumentOpenMode.Modify, null, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password)
    {
        return Open(path, password, PdfDocumentOpenMode.Modify, null, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(string path, string password, PdfReadAccuracy accuracy)
    {
        return Open(path, password, PdfDocumentOpenMode.Modify, null, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, PdfDocumentOpenMode openmode)
    {
        return Open(stream, null, openmode, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, PdfDocumentOpenMode openmode, PdfReadAccuracy accuracy)
    {
        return Open(stream, null, openmode, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, PdfDocumentOpenMode openmode, PdfPasswordProvider passwordProvider)
    {
        return Open(stream, null, openmode, passwordProvider, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, PdfDocumentOpenMode openmode, PdfPasswordProvider passwordProvider, PdfReadAccuracy accuracy)
    {
        return Open(stream, null, openmode, passwordProvider, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, string password, PdfDocumentOpenMode openmode)
    {
        return Open(stream, password, openmode, null, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, string password, PdfDocumentOpenMode openmode, PdfReadAccuracy accuracy)
    {
        return Open(stream, password, openmode, null, accuracy);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, string password, PdfDocumentOpenMode openmode, PdfPasswordProvider passwordProvider)
    {
        return Open(stream, password, openmode, passwordProvider, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, string password, PdfDocumentOpenMode openmode, PdfPasswordProvider passwordProvider, PdfReadAccuracy accuracy)
    {
        var lexer = new Lexer(stream);
        var document = new PdfDocument(lexer);
        document._state |= DocumentState.Imported;
        document._openMode = openmode;
        document.FileSize = stream.Length;

        document._version = ReadHeaderVersion(stream);
        if (document._version == 0)
            throw new InvalidOperationException(PSSR.InvalidPdf);

        var parser = new Parser(document);
        ReadTrailers(document, parser, accuracy);

        // A password given for a document that is not encrypted is ignored.
        var xrefEncrypt = document._trailer.Elements[PdfTrailer.Keys.Encrypt] as PdfReference;
        if (xrefEncrypt != null)
        {
            ReadEncryptDictionary(document, parser, xrefEncrypt);
            if (!TryUnlock(document, password, openmode, passwordProvider))
                return null;
        }

        ReadObjectStreamReferences(parser);
        ReadCompressedObjects(document, parser);
        ReadIndirectObjects(document, parser, accuracy);

        // Encrypt all objects.
        if (xrefEncrypt != null)
            document.SecurityHandler.EncryptDocument();

        // Fix references of trailer values and then objects and irefs are consistent.
        document._trailer.Finish();

        if (openmode is PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append)
        {
            RenewRevisionId(document);

            // The modification date is not stamped here. It is stamped when the document is
            // written, in PdfDocument.PrepareForSave, so that opening a document to read its
            // dates does not change the date it is read for.

            if (openmode == PdfDocumentOpenMode.Append)
                PrepareForAppending(document, parser, stream);
            else
                CompactAndRenumber(document);
        }

        return document;
    }

    /// <summary>
    /// Reads the version from the first kilobyte of the stream, or 0 when it has none.
    /// </summary>
    private static int ReadHeaderVersion(Stream stream)
    {
        var header = new byte[1024];
        stream.Position = 0;
        // A file shorter than the buffer is normal here; the remainder stays zero.
        PdfPinata.Internal.StreamHelper.ReadUpTo(stream, header, 0, 1024);
        return GetPdfFileVersion(header);
    }

    /// <summary>
    /// Reads all trailers or cross-reference streams, but no objects.
    /// </summary>
    private static void ReadTrailers(PdfDocument document, Parser parser, PdfReadAccuracy accuracy)
    {
        document._irefTable.IsUnderConstruction = true;

        document._trailer = parser.ReadTrailer(accuracy);
        if (document._trailer == null)
            ParserDiagnostics.ThrowParserException("Invalid PDF file: no trailer found.");

        Debug.Assert(document._irefTable.IsUnderConstruction);
        document._irefTable.IsUnderConstruction = false;
    }

    /// <summary>
    /// Reads the encryption dictionary the trailer refers to, which the security handler is built on.
    /// </summary>
    private static void ReadEncryptDictionary(PdfDocument document, Parser parser, PdfReference xrefEncrypt)
    {
        document._readEncrypted = true;
        var encrypt = parser.ReadObject(null, xrefEncrypt.ObjectID, false, false);

        encrypt.Reference = xrefEncrypt;
        xrefEncrypt.Value = encrypt;
    }

    /// <summary>
    /// Validates the password, asking the provider for another for as long as the one given is not
    /// enough for the open mode.
    /// </summary>
    /// <returns>False when the provider aborts, in which case no document is opened.</returns>
    /// <exception cref="PdfReaderException">The password is not enough and there is no provider to ask.</exception>
    private static bool TryUnlock(PdfDocument document, string password, PdfDocumentOpenMode openmode, PdfPasswordProvider passwordProvider)
    {
        var securityHandler = document.SecurityHandler;
        while (true)
        {
            var validity = securityHandler.ValidatePassword(password);
            var refusal = RefusalFor(validity, password, openmode);
            if (refusal == null)
            {
                // Which of the two passwords got us in. PdfSecuritySettings.HasOwnerPermissions
                // exists to answer exactly that question and was never written to: the field was
                // initialized to true and assigned nowhere, so the property answered "yes, owner"
                // for every document however it had been opened - including one opened with the
                // user password, which is the one case anybody would ask about.
                //
                // ValidatePassword has always known the answer and returned it; only the caller
                // never recorded it.
                document.SecuritySettings._hasOwnerPermissions =
                    validity == PasswordValidity.OwnerPassword;
                return true;
            }

            if (passwordProvider == null)
                throw new PdfReaderException(refusal);

            var args = new PdfPasswordProviderArgs();
            passwordProvider(args);
            if (args.Abort)
                return false;
            password = args.Password;
        }
    }

    /// <summary>
    /// Why the password does not open the document in the given mode, or null when it does.
    /// </summary>
    private static string RefusalFor(PasswordValidity validity, string password, PdfDocumentOpenMode openmode)
    {
        if (validity == PasswordValidity.Invalid)
            return password == null ? PSSR.PasswordRequired : PSSR.InvalidPassword;

        if (validity == PasswordValidity.UserPassword && openmode == PdfDocumentOpenMode.Modify)
            return PSSR.OwnerPasswordRequired;

        return null;
    }

    /// <summary>
    /// Creates iRefs for all compressed objects, reading each object stream's index once.
    /// </summary>
    private static void ReadObjectStreamReferences(Parser parser)
    {
        // The cross-reference streams are taken from the parser rather than looked for in the
        // table: one whose number a later revision gave to another object is not in the table,
        // and the objects its revision compressed are still to be read.
        var objectStreams = new HashSet<int>();
        foreach (var xrefStream in parser.CrossReferenceStreams)
        {
            foreach (var item in xrefStream.Entries)
            {
                // Is type xref to compressed object?
                if (item.Type != 2)
                    continue;

                var objectNumber = (int)item.Field2;
                if (objectStreams.Add(objectNumber))
                    parser.ReadIRefsFromCompressedObject(new PdfObjectID(objectNumber));
            }
        }
    }

    /// <summary>
    /// Reads every compressed object that no newer revision defines.
    /// </summary>
    private static void ReadCompressedObjects(PdfDocument document, Parser parser)
    {
        foreach (var xrefStream in parser.CrossReferenceStreams)
        {
            foreach (var item in xrefStream.Entries)
            {
                // Is type xref to compressed object?
                if (item.Type != 2 || IsReadAlready(document, item))
                    continue;

                parser.ReadCompressedObject(new PdfObjectID((int)item.Field2), (int)item.Field3);
            }
        }
    }

    /// <summary>
    /// Whether the object a compressed entry names has been placed in the table already, by a
    /// newer revision.
    /// </summary>
    private static bool IsReadAlready(PdfDocument document, PdfCrossReferenceStream.CrossReferenceStreamEntry item)
    {
        // Only the newest revision's word on an object counts. Reading an older
        // revision's compressed copy puts it in the table over whatever the newer
        // one says - a catalog a signing tool wrote out uncompressed, with the
        // /AcroForm it added, lost to the compressed catalog it replaced. The
        // streams are newest first, so the first to read an object is the newest.
        if (item.ObjectNumber < 1)
            return false;

        var entry = document._irefTable[new PdfObjectID(item.ObjectNumber)];
        return entry is not { Position: < 0, Value: null };
    }

    /// <summary>
    /// Reads every indirect object not read yet, and records the largest object number.
    /// </summary>
    private static void ReadIndirectObjects(PdfDocument document, Parser parser, PdfReadAccuracy accuracy)
    {
        foreach (var iref in document._irefTable.AllReferences)
        {
            Debug.Assert(document._irefTable.Contains(iref.ObjectID));
            if (iref.Value == null)
                ReadIndirectObject(parser, iref, accuracy);

            document._irefTable.MaxObjectNumber = Math.Max(document._irefTable.MaxObjectNumber,
                iref.ObjectNumber);
        }
    }

    /// <summary>
    /// Reads one indirect object. An object whose position cannot be found is skipped unless the
    /// accuracy is strict.
    /// </summary>
    private static void ReadIndirectObject(Parser parser, PdfReference iref, PdfReadAccuracy accuracy)
    {
        try
        {
            var pdfObject = parser.ReadObject(null, iref.ObjectID, false, false);
            Debug.Assert(pdfObject.Reference == iref);
            pdfObject.Reference = iref;
            Debug.Assert(pdfObject.Reference.Value != null, "Something went wrong.");
        }
        catch (PositionNotFoundException ex)
        {
            Debug.WriteLine(ex.Message);

            if (accuracy == PdfReadAccuracy.Strict)
                throw;
        }
    }

    /// <summary>
    /// Creates new or changes existing document IDs.
    /// </summary>
    /// <remarks>
    /// /ID[0] identifies the document across its whole life and /ID[1] identifies this revision of
    /// it, so only the second is replaced when there already is a pair — which is exactly what an
    /// appended revision needs as well.
    /// </remarks>
    private static void RenewRevisionId(PdfDocument document)
    {
        if (document.Internals.SecondDocumentID == "")
        {
            document._trailer.CreateNewDocumentIDs();
            return;
        }

        var agTemp = Guid.NewGuid().ToByteArray();
        document.Internals.SecondDocumentID = PdfEncoders.RawEncoding.GetString(agTemp, 0, agTemp.Length);
    }

    /// <summary>
    /// Prepares a document opened to have a revision appended, keeping its numbers and its bytes.
    /// </summary>
    private static void PrepareForAppending(PdfDocument document, Parser parser, Stream stream)
    {
        // Neither compacted nor renumbered, and both matter. An incremental update
        // shadows an object by writing a new definition under the same number, so
        // renumbering would make every appended object overwrite the wrong one. And an
        // object unreachable from the catalog is still in the file we are appending to,
        // so removing it from the table would not remove it from the document — it
        // would only lose track of a number that is already taken.
        // Flatten the page tree first and capture afterwards. Flattening mutates the
        // page tree, and capturing is what decides which objects count as untouched —
        // do it the other way round and every page is reported changed by the act of
        // reading it, so an incremental save rewrites the lot.
        //
        // Assigned to a local first, and that is the whole point: Debug.Assert is
        // [Conditional("DEBUG")], so the compiler removes the call *and its argument* in
        // a release build. Written as an assertion on document.Pages, the flattening
        // this depends on simply would not happen where it matters most.
        //
        // Before anything can be given a number: every number below a revision's /Size
        // is one the file already accounts for, in use or freed, so the next new object
        // starts from the largest of them rather than one past the highest object in
        // use. A section that ends in free entries has a /Size above that, and numbering
        // from the live objects shrank the appended /Size and reused a freed number,
        // both of which ISO 32000-1 7.5.5 forbids an update to do.
        document._irefTable.MaxObjectNumber = Math.Max(document._irefTable.MaxObjectNumber,
            parser.LargestSize - 1);

        var pages = document.Pages;
        Debug.Assert(pages != null);

        document.CaptureOriginalBytes(stream, parser.StartXref);
    }

    /// <summary>
    /// Removes the objects nothing reaches and numbers the rest afresh.
    /// </summary>
    private static void CompactAndRenumber(PdfDocument document)
    {
        var removed = document._irefTable.Compact();
        if (removed != 0)
            Debug.WriteLine("Number of deleted unreachable objects: " + removed);

        // Force flattening of page tree
        var pages = document.Pages;
        Debug.Assert(pages != null);

        document._irefTable.Renumber();
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream)
    {
        return Open(stream, PdfDocumentOpenMode.Modify, PdfReadAccuracy.Strict);
    }

    /// <summary>
    /// Opens an existing PDF document.
    /// </summary>
    public static PdfDocument Open(Stream stream, PdfReadAccuracy accuracy)
    {
        return Open(stream, PdfDocumentOpenMode.Modify, accuracy);
    }
}
