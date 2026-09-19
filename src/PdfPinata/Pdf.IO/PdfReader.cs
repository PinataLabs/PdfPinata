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
                if (stream != null)
                {
                    stream.Dispose();
                }
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
    internal static int GetPdfFileVersion(byte[] bytes)
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
    static int ScanFileVersion(System.Text.Encoding encoding, byte[] bytes)
    {
        try
        {
            // Acrobat accepts headers like «%!PS-Adobe-N.n PDF-M.m»...
            var header = encoding.GetString(bytes, 0, bytes.Length);
            if (header.Length == 0)
                return 0;
            if (header[0] == '%' || header.Contains("%PDF", StringComparison.Ordinal))
            {
                var ich = header.IndexOf("PDF-", StringComparison.Ordinal);
                if (ich > 0 && ich + 6 < header.Length && header[ich + 5] == '.')
                {
                    var major = header[ich + 4];
                    var minor = header[ich + 6];
                    // PDF 1.0 to 1.7 and PDF 2.0 are the versions defined so far.
                    if (major >= '1' && major <= '2' && minor >= '0' && minor <= '9')
                        return (major - '0') * 10 + (minor - '0');
                }
            }
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        { }

        return 0;
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
            if (document != null)
            {
                document._fullPath = Path.GetFullPath(path);
            }
        }
        finally
        {
            if (stream != null)
                stream.Dispose();

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
        PdfDocument document;
        try
        {
            var lexer = new Lexer(stream);
            document = new PdfDocument(lexer);
            document._state |= DocumentState.Imported;
            document._openMode = openmode;
            document._fileSize = stream.Length;

            // Get file version.
            var header = new byte[1024];
            stream.Position = 0;
            // A file shorter than the buffer is normal here; the remainder stays zero.
            PdfPinata.Internal.StreamHelper.ReadUpTo(stream, header, 0, 1024);
            document._version = GetPdfFileVersion(header);
            if (document._version == 0)
                throw new InvalidOperationException(PSSR.InvalidPdf);

            document._irefTable.IsUnderConstruction = true;
            var parser = new Parser(document);

            // Read all trailers or cross-reference streams, but no objects.
            document._trailer = parser.ReadTrailer(accuracy);

            if (document._trailer == null)
                ParserDiagnostics.ThrowParserException("Invalid PDF file: no trailer found.");

            Debug.Assert(document._irefTable.IsUnderConstruction);
            document._irefTable.IsUnderConstruction = false;

            // Is document encrypted?
            var xrefEncrypt = document._trailer.Elements[PdfTrailer.Keys.Encrypt] as PdfReference;
            if (xrefEncrypt != null)
            {
                //xrefEncrypt.Value = parser.ReadObject(null, xrefEncrypt.ObjectID, false);
                var encrypt = parser.ReadObject(null, xrefEncrypt.ObjectID, false, false);

                encrypt.Reference = xrefEncrypt;
                xrefEncrypt.Value = encrypt;
                var securityHandler = document.SecurityHandler;
                TryAgain:
                var validity = securityHandler.ValidatePassword(password);
                if (validity == PasswordValidity.Invalid)
                {
                    if (passwordProvider != null)
                    {
                        var args = new PdfPasswordProviderArgs();
                        passwordProvider(args);
                        if (args.Abort)
                            return null;
                        password = args.Password;
                        goto TryAgain;
                    }
                    else
                    {
                        if (password == null)
                            throw new PdfReaderException(PSSR.PasswordRequired);
                        else
                            throw new PdfReaderException(PSSR.InvalidPassword);
                    }
                }
                else if (validity == PasswordValidity.UserPassword && openmode == PdfDocumentOpenMode.Modify)
                {
                    if (passwordProvider != null)
                    {
                        var args = new PdfPasswordProviderArgs();
                        passwordProvider(args);
                        if (args.Abort)
                            return null;
                        password = args.Password;
                        goto TryAgain;
                    }
                    else
                        throw new PdfReaderException(PSSR.OwnerPasswordRequired);
                }

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
            }
            else
            {
                if (password != null)
                {
                    // Password specified but document is not encrypted.
                    // ignore
                }
            }

            var irefs2 = document._irefTable.AllReferences;
            var count2 = irefs2.Length;

            // 3rd: Create iRefs for all compressed objects.
            var objectStreams = new Dictionary<int, object>();
            for (var idx = 0; idx < count2; idx++)
            {
                var iref = irefs2[idx];
                var xrefStream = iref.Value as PdfCrossReferenceStream;
                if (xrefStream != null)
                {
                    for (var idx2 = 0; idx2 < xrefStream.Entries.Count; idx2++)
                    {
                        var item = xrefStream.Entries[idx2];
                        // Is type xref to compressed object?
                        if (item.Type == 2)
                        {
                            //PdfReference irefNew = parser.ReadCompressedObject(new PdfObjectID((int)item.Field2), (int)item.Field3);
                            //document._irefTable.Add(irefNew);
                            var objectNumber = (int)item.Field2;
                            if (!objectStreams.ContainsKey(objectNumber))
                            {
                                objectStreams.Add(objectNumber, null);
                                var objectID = new PdfObjectID((int)item.Field2);
                                parser.ReadIRefsFromCompressedObject(objectID);
                            }
                        }
                    }
                }
            }

            // 4th: Read compressed objects.
            for (var idx = 0; idx < count2; idx++)
            {
                var iref = irefs2[idx];
                var xrefStream = iref.Value as PdfCrossReferenceStream;
                if (xrefStream != null)
                {
                    for (var idx2 = 0; idx2 < xrefStream.Entries.Count; idx2++)
                    {
                        var item = xrefStream.Entries[idx2];
                        // Is type xref to compressed object?
                        if (item.Type == 2)
                        {
                            parser.ReadCompressedObject(new PdfObjectID((int)item.Field2),
                                (int)item.Field3);
                            Debug.Assert(document._irefTable.Contains(iref.ObjectID));
                            //document._irefTable.Add(irefNew);
                        }
                    }
                }
            }


            var irefs = document._irefTable.AllReferences;
            var count = irefs.Length;

            // Read all indirect objects.
            for (var idx = 0; idx < count; idx++)
            {
                var iref = irefs[idx];
                if (iref.Value == null)
                {
                    try
                    {
                        Debug.Assert(document._irefTable.Contains(iref.ObjectID));
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
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        // 4STLA rethrow exception to notify caller.
                        throw;
                    }
                }
                else
                {
                    Debug.Assert(document._irefTable.Contains(iref.ObjectID));
                    //iref.GetType();
                }
                // Set maximum object number.
                document._irefTable.MaxObjectNumber = Math.Max(document._irefTable.MaxObjectNumber,
                    iref.ObjectNumber);
            }

            // Encrypt all objects.
            if (xrefEncrypt != null)
            {
                document.SecurityHandler.EncryptDocument();
            }

            // Fix references of trailer values and then objects and irefs are consistent.
            document._trailer.Finish();

            if (openmode == PdfDocumentOpenMode.Modify || openmode == PdfDocumentOpenMode.Append)
            {
                // Create new or change existing document IDs. /ID[0] identifies the document across
                // its whole life and /ID[1] identifies this revision of it, so only the second is
                // replaced when there already is a pair — which is exactly what an appended
                // revision needs as well.
                if (document.Internals.SecondDocumentID == "")
                    document._trailer.CreateNewDocumentIDs();
                else
                {
                    var agTemp = Guid.NewGuid().ToByteArray();
                    document.Internals.SecondDocumentID = PdfEncoders.RawEncoding.GetString(agTemp, 0, agTemp.Length);
                }

                // The modification date is not stamped here. It is stamped when the document is
                // written, in PdfDocument.PrepareForSave, so that opening a document to read its
                // dates does not change the date it is read for.

                if (openmode == PdfDocumentOpenMode.Append)
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
                    var pages = document.Pages;
                    Debug.Assert(pages != null);

                    document.CaptureOriginalBytes(stream);
                }
                else
                {
                    // Remove all unreachable objects
                    var removed = document._irefTable.Compact();
                    if (removed != 0)
                        Debug.WriteLine("Number of deleted unreachable objects: " + removed);

                    // Force flattening of page tree
                    var pages = document.Pages;
                    Debug.Assert(pages != null);

                    //bool b = document.irefTable.Contains(new PdfObjectID(1108));
                    //b.GetType();

                    document._irefTable.Renumber();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            throw;
        }
        return document;
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
