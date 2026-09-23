#region Copyright
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
using System.Globalization;
using System.IO;
using PdfPinata.Exceptions;
using PdfPinata.Internal;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.IO.enums;

namespace PdfPinata.Pdf.IO;
/*
   Direct and indirect objects

   * If a simple object (boolean, integer, number, date, string, rectangle etc.) is referenced indirect,
     the parser reads this objects immediately and consumes the indirection.

   * If a composite object (dictionary, array etc.) is referenced indirect, a PdfReference objects
     is returned.

   * If a composite object is a direct object, no PdfReference is created and the object is
     parsed immediately.

   * A reference to a non-existing object is specified as legal, therefore null is returned.
*/

/// <summary>
/// Provides the functionality to parse PDF documents.
/// </summary>
internal sealed class Parser
{
    public Parser(PdfDocument document, Stream pdf)
    {
        _document = document;
        _lexer = new Lexer(pdf);
        _stack = new ShiftStack();
    }

    public Parser(PdfDocument document)
    {
        _document = document;
        _lexer = document._lexer;
        _stack = new ShiftStack();
    }

    /// <summary>
    /// Sets PDF input stream position to the specified object.
    /// </summary>
    public long MoveToObject(PdfObjectID objectID)
    {
        var position = _document._irefTable[objectID].Position;

        if (position < 0)
            throw new PositionNotFoundException(objectID);

        return _lexer.Position = position;
    }

    public Symbol Symbol => _lexer.Symbol;

    /// <summary>
    /// Every cross-reference stream <see cref="ReadTrailer"/> has read, newest revision first.
    /// Not every one of them is in the document's table: one whose number a later revision gave
    /// to another object is left out, and is still needed for the objects it compresses.
    /// </summary>
    internal List<PdfCrossReferenceStream> CrossReferenceStreams { get; } = [];

    /// <summary>
    /// The largest <c>/Size</c> any trailer <see cref="ReadTrailer"/> has read declares, over every
    /// revision rather than the newest alone, or zero when none declares one it can believe.
    /// </summary>
    /// <remarks>
    /// Every number below it is one the file accounts for, in use or freed, and an appended
    /// revision may not reuse one (ISO 32000-1 7.5.5). The newest trailer is not enough: a file an
    /// earlier incremental save shrank the /Size of carries the larger value only further back. A
    /// /Size beyond the most indirect objects ISO 32000-1 Annex C lets a file have is damage rather
    /// than a count, and is ignored rather than allowed to push new numbers towards overflow.
    /// </remarks>
    internal int LargestSize { get; private set; }

    /// <summary>ISO 32000-1 Table C.1: at most 8,388,607 indirect objects, so /Size is at most one more.</summary>
    private const int MaximumSize = 8_388_608;

    public PdfObjectID ReadObjectNumber(long position)
    {
        _lexer.Position = position;
        var objectNumber = ReadInteger();
        var generationNumber = ReadInteger();
        return new PdfObjectID(objectNumber, generationNumber);
    }


    /// <summary>
    /// Reads PDF object from input stream.
    /// </summary>
    /// <param name="pdfObject">Either the instance of a derived type or null. If it is null
    /// an appropriate object is created.</param>
    /// <param name="objectID">The address of the object.</param>
    /// <param name="includeReferences">If true, specifies that all indirect objects
    /// are included recursively.</param>
    /// <param name="fromObjecStream">If true, the objects is parsed from an object stream.</param>
    public PdfObject ReadObject(PdfObject pdfObject, PdfObjectID objectID, bool includeReferences,
        bool fromObjecStream)
    {
        if (!fromObjecStream)
        {
            MoveToObject(objectID);
            // The header's own numbers are read past and not used; see below.
            ReadInteger();
            ReadInteger();
        }
        // The object header can disagree with the iref table that led here, and the object ID from
        // the table is the one to believe. PDF4NET 2.6's 'unicode.pdf' sample, for one, gives
        // objects 84 to 87 the same offset in its iref table, so all four read back as the same
        // dictionary with a header saying object 84 — and every reader tested shows it anyway.
        // Always use object ID from iref table (see above).
        var objectNumber = objectID.ObjectNumber;
        var generationNumber = objectID.GenerationNumber;

        if (!fromObjecStream)
            ReadSymbol(Symbol.Obj);

        var symbol = ScanNextToken();
        var simpleObject = SimpleObjectFor(symbol);
        if (simpleObject != null)
        {
            simpleObject.SetObjectID(objectNumber, generationNumber);
            if (!fromObjecStream)
                ReadEndObject();
            return simpleObject;
        }

        var checkForStream = false;
        switch (symbol)
        {
            case Symbol.BeginArray:
                var array = pdfObject == null ? new PdfArray(_document) : (PdfArray)pdfObject;
                pdfObject = ReadArray(array, includeReferences);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                break;

            case Symbol.BeginDictionary:
                var dict = pdfObject == null ? new PdfDictionary(_document) : (PdfDictionary)pdfObject;
                checkForStream = true;
                pdfObject = ReadDictionary(dict, includeReferences);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                break;

            case Symbol.EndObj:
                pdfObject = new PdfNullObject(_document);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                return pdfObject;

            default:
                // Should not come here anymore.
                ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
                break;
        }

        var endOfObject = _lexer.Position;
        symbol = ScanNextToken();
        if (symbol == Symbol.BeginStream)
            symbol = ReadStreamOfObject((PdfDictionary)pdfObject, checkForStream, out endOfObject);

        if (!fromObjecStream && symbol != Symbol.EndObj)
            EndObject(symbol, endOfObject);
        return pdfObject;
    }

    /// <summary>
    ///   The indirect object a simple value read as the symbol given stands for, which is the whole
    ///   of the object, or null when the symbol does not begin one.
    /// </summary>
    private PdfObject SimpleObjectFor(Symbol symbol)
    {
        switch (symbol)
        {
            // Acrobat 6 Professional proudly presents: The Null object!
            // Even with a one-digit object number an indirect reference «x 0 R» to this object is
            // one character larger than the direct use of «null». Probable this is the reason why
            // it is true that Acrobat Web Capture 6.0 creates this object, but obviously never
            // creates a reference to it!
            case Symbol.Null:
                return new PdfNullObject(_document);

            case Symbol.Boolean:
                return new PdfBooleanObject(_document,
                    string.Compare(_lexer.Token, bool.TrueString, StringComparison.OrdinalIgnoreCase) == 0);

            case Symbol.Integer:
                return new PdfIntegerObject(_document, _lexer.TokenToInteger);

            case Symbol.UInteger:
                return new PdfUIntegerObject(_document, _lexer.TokenToUInteger);

            case Symbol.Long:
                return new PdfLongObject(_document, _lexer.TokenToLong);

            case Symbol.Real:
                return new PdfRealObject(_document, _lexer.TokenToReal);

            case Symbol.String:
            case Symbol.UnicodeString:
            case Symbol.HexString:
            case Symbol.UnicodeHexString:
                // The same flags a direct string is given. The lexer has already combined the
                // bytes of a UTF-16 string into characters, so taking it as raw would write each
                // character back out as its low byte alone.
                return new PdfStringObject(_document, _lexer.Token, StringFlagsFor(symbol));

            case Symbol.Name:
                return new PdfNameObject(_document, _lexer.Token);

            default:
                return null;
        }
    }

    /// <summary>
    ///   Reads the stream of an indirect object, the "stream" keyword just read, and what follows it.
    /// </summary>
    /// <param name="dict">The dictionary the stream belongs to.</param>
    /// <param name="checkForStream">Whether a stream was to be expected, which only a dictionary can have.</param>
    /// <param name="endOfObject">Receives where the object ends, which is where the symbol returned was read from.</param>
    /// <returns>
    ///   The symbol after "endstream", with the end of the file taken for "endobj".
    /// </returns>
    private Symbol ReadStreamOfObject(PdfDictionary dict, bool checkForStream, out long endOfObject)
    {
        Debug.Assert(checkForStream, "Unexpected stream...");
        var startOfStream = _lexer.Position;
        var bytes = TheStreamTheDictionaryDescribes(dict, startOfStream);

        if (bytes == null)
        {
            // The dictionary does not say how long its stream is, or says something the
            // file cannot hold.
            if (!TryReadStreamUpToEndOfStream(dict, startOfStream))
                throw new InvalidOperationException("Cannot retrieve stream length.");
        }
        else
        {
            dict.Stream = new PdfDictionary.PdfStream(bytes, dict);
            try
            {
                ReadSymbol(Symbol.EndStream);
            }
            catch (PdfReaderException)
            {
                // The stream length is incorrect, look for the end of the stream instead.
                if (!TryReadStreamUpToEndOfStream(dict, startOfStream))
                    throw;
            }
        }

        endOfObject = _lexer.Position;
        var symbol = ScanNextToken();
        return symbol == Symbol.Eof ? Symbol.EndObj : symbol;
    }

    /// <summary>
    ///   Reads the end of an indirect object, tolerating a file that leaves "endobj" out.
    /// </summary>
    private void ReadEndObject()
    {
        var endOfObject = _lexer.Position;
        var symbol = _lexer.ScanNextToken();
        if (symbol != Symbol.EndObj)
            EndObject(symbol, endOfObject);
    }

    /// <summary>
    ///   Accepts the end of an indirect object that does not say "endobj".
    /// </summary>
    /// <remarks>
    ///   A file that leaves the keyword out is corrupt, but the object before it is whole and
    ///   every reader in common use goes on to read the file. Nothing is lost by doing the same.
    ///   The object only ends here if what follows begins the next one or ends the file. Anything
    ///   else means the object itself did not parse, and is still reported.
    ///   See https://github.com/empira/PDFsharp/issues/211.
    /// </remarks>
    /// <param name="symbol">The symbol found where "endobj" should have been.</param>
    /// <param name="endOfObject">Where the object ended, which is where that symbol was read from.</param>
    private void EndObject(Symbol symbol, long endOfObject)
    {
        // Reading the token back is what clears it, so take a copy for the message first.
        var token = _lexer.Token;

        if (!EndsAnObject(symbol))
            ParserDiagnostics.ThrowParserException(PSSR.UnexpectedToken(token));

        // The token belongs to whatever comes next rather than to the object that just ended,
        // so put it back for whoever reads on from here.
        _lexer.Position = endOfObject;
    }

    /// <summary>
    ///   Tells whether the symbol read where "endobj" should have been marks the end of the object
    ///   anyway, by beginning the object that follows or ending the body of the file.
    /// </summary>
    private bool EndsAnObject(Symbol symbol)
    {
        return symbol switch
        {
            // The object number of the object that follows — but only if it really is one, so
            // that a stray number left after an object is still reported.
            Symbol.Integer or Symbol.UInteger or Symbol.Long => BeginsAnIndirectObject(),
            // Or the end of the body of the file.
            Symbol.XRef or Symbol.Trailer or Symbol.StartXRef or Symbol.Eof => true,
            _ => false
        };
    }

    /// <summary>
    ///   Tells whether the number just read opens an indirect object, "&lt;number&gt; &lt;generation&gt; obj",
    ///   without moving the lexer off it.
    /// </summary>
    private bool BeginsAnIndirectObject()
    {
        // Object numbers count from one; zero is the head of the list of free objects.
        if (_lexer.TokenToLong <= 0)
            return false;

        var state = SaveState();
        try
        {
            var generationNumber = _lexer.ScanNextToken();
            if (generationNumber != Symbol.Integer && generationNumber != Symbol.UInteger)
                return false;

            return _lexer.ScanNextToken() == Symbol.Obj;
        }
        finally
        {
            RestoreState(state);
        }
    }

    /// <summary>
    /// Reads the stream of a dictionary.
    /// </summary>
    private void ReadStream(PdfDictionary dict)
    {
        var symbol = _lexer.Symbol;
        Debug.Assert(symbol == Symbol.BeginStream);
        Debug.Assert(dict.Stream == null, "Dictionary already has a stream.");
        var startOfStream = _lexer.Position;
        var bytes = TheStreamTheDictionaryDescribes(dict, startOfStream);
        if (bytes == null)
        {
            if (!TryReadStreamUpToEndOfStream(dict, startOfStream))
                throw new InvalidOperationException("Cannot retrieve stream length.");
            ScanNextToken();
            return;
        }

        dict.Stream = new PdfDictionary.PdfStream(bytes, dict);
        try
        {
            ReadSymbol(Symbol.EndStream);
        }
        catch (PdfReaderException)
        {
            // The stream length is incorrect, look for the end of the stream instead.
            if (!TryReadStreamUpToEndOfStream(dict, startOfStream))
                throw;
        }
        ScanNextToken();
    }

    /// <summary>
    /// The bytes the dictionary says its stream holds, or null when it does not say how long the
    /// stream is or the file cannot deliver what it says. A length reaching past the end of the
    /// file describes no stream that is there, so the caller is better off looking for the end of
    /// the stream than believing it.
    /// </summary>
    private byte[] TheStreamTheDictionaryDescribes(PdfDictionary dict, long startOfStream)
    {
        var length = GetStreamLength(dict);
        if (length < 0 || startOfStream + length > _lexer.PdfLength)
            return null;

        var bytes = _lexer.ReadStream(length);
        // The end-of-line behind the "stream" keyword is not part of the data and is not counted
        // in startOfStream, so a length can clear the check above and still run the file out of
        // bytes. What came back short is not the stream the dictionary described, and taking it
        // would end the object at the end of the file with no keyword to say the stream ended -
        // which ReadSymbol accepts, since it lets an unexpected end of file pass.
        return bytes.Length == length ? bytes : null;
    }

    /// <summary>
    /// Reads the stream of a dictionary by looking for the keyword that ends it, for the
    /// documents whose /Length entry is missing or does not describe the stream.
    /// </summary>
    /// <returns>False when the document does not hold the keyword at all.</returns>
    private bool TryReadStreamUpToEndOfStream(PdfDictionary dict, long startOfStream)
    {
        _lexer.Position = startOfStream;
        _lexer.Position = _lexer.MoveToStartOfStream();
        // The keyword alone is what is looked for. A document that gets the length of its
        // streams wrong is not one to be trusted to put the end-of-line before the keyword
        // there either, and a stream ending without one still ends.
        var bytes = _lexer.ScanUntilMarker(PdfEncoders.RawEncoding.GetBytes("endstream"), out var markerFound);
        if (!markerFound)
            return false;

        bytes = WithoutTheEndOfLineBeforeTheKeyword(bytes);
        dict.Stream = new PdfDictionary.PdfStream(bytes, dict);
        // Record what was read. The dictionary has to describe its stream correctly once
        // the document is written again.
        dict.Elements.SetInteger(PdfDictionary.PdfStream.Keys.Length, bytes.Length);
        return true;
    }

    /// <summary>
    /// The data of a stream without the end-of-line that separates it from the keyword ending
    /// it. That end-of-line belongs to the file rather than to the stream, and a reader that
    /// keeps it adds a byte to every stream it has to recover.
    /// </summary>
    private static byte[] WithoutTheEndOfLineBeforeTheKeyword(byte[] bytes)
    {
        var end = bytes.Length;
        if (end > 0 && bytes[end - 1] == (byte)Chars.LF)
            end--;
        if (end > 0 && bytes[end - 1] == (byte)Chars.CR)
            end--;

        return end == bytes.Length ? bytes : [.. bytes.AsSpan(0, end)];
    }

    // HACK: Solve problem more general.
    private int GetStreamLength(PdfDictionary dict)
    {
        if (dict.Elements.Count == 0)
            return 0;

        // A stream saying its data is in another file, through /F, is read no differently here:
        // ISO 32000-1 Table 5 keeps /Length counting the bytes that are in this file, which are
        // usually none and are to be ignored whatever they are. Refusing such a document was the
        // whole of empira/PDFsharp#389; see docs/specs/external-file-streams.md.
        var value = dict.Elements["/Length"];
        if (value is PdfInteger)
        {
            return Convert.ToInt32(value);
        }

        if (!(value is PdfReference reference))
        {
            // The /Length entry is required, but it is missing in real world documents,
            // e.g. from the XMP metadata AutoCAD writes. Report that the length is unknown
            // and let the caller recover by looking for the end of the stream.
            return -1;
        }

        // The object holding the length may have been parsed already. That is always the case when it
        // lives in an object stream: such an object has no position in the file (it is marked with -1)
        // and therefore cannot be read through MoveToObject. Prefer the value that is already known.
        var length = ResolveLengthObject(reference);
        if (length == null)
        {
            // The length is stored in an object that cannot be reached. Report the length as unknown
            // and let the caller recover by looking for the end of the stream.
            return -1;
        }

        var len = length.Value;
        dict.Elements["/Length"] = new PdfInteger(len);
        return len;
    }

    /// <summary>
    /// Gets the integer object an indirect /Length entry points to, or null when it cannot be resolved.
    /// </summary>
    private PdfIntegerObject ResolveLengthObject(PdfReference reference)
    {
        if (reference.Value is PdfIntegerObject resolved)
            return resolved;

        // The reference may be a temporary one created while the xref table was under construction,
        // so look the object up in the table as well.
        var iref = _document != null ? _document._irefTable[reference.ObjectID] : null;
        if (iref is { Value: PdfIntegerObject known })
            return known;

        // Objects inside an object stream have no position in the file. When such an object has not
        // been read yet there is no way to reach it from here.
        if (iref is { Position: < 0 })
            return null;

        var state = SaveState();
        try
        {
            return ReadObject(null, reference.ObjectID, false, false) as PdfIntegerObject;
        }
        finally
        {
            RestoreState(state);
        }
    }

    public PdfArray ReadArray(PdfArray array, bool includeReferences)
    {
        Debug.Assert(Symbol == Symbol.BeginArray);

        array ??= new PdfArray(_document);

        var sp = _stack.SP;
        ParseObject(Symbol.EndArray);
        var count = _stack.SP - sp;
        var items = _stack.ToArray(sp, count);
        _stack.Reduce(count);
        for (var idx = 0; idx < count; idx++)
        {
            var val = items[idx];
            if (includeReferences && val is PdfReference)
                val = ReadReference();
            array.Elements.Add(val);
        }

        return array;
    }

    internal PdfDictionary ReadDictionary(PdfDictionary dict, bool includeReferences)
    {
        Debug.Assert(Symbol == Symbol.BeginDictionary);

        dict ??= new PdfDictionary(_document);

        var sp = _stack.SP;
        ParseObject(Symbol.EndDictionary);
        var count = _stack.SP - sp;
        var items = _stack.ToArray(sp, count);
        _stack.Reduce(count);
        // An entry is a name followed by a value, but writers do produce dictionaries that do not
        // hold to that: pdfTeX writes its PTEX.FullBanner as two strings with no key in front of
        // them, and a truncated dictionary leaves a key with no value behind it. Keep the pairs
        // that are there, and drop what cannot be paired up rather than refusing the document.
        for (var idx = 0; idx + 1 < count;)
        {
            var val = items[idx];
            if (!(val is PdfName))
            {
                // A value with nothing to key it under. Step over it alone, so that the entries
                // after it are still read as the pairs they are.
                idx++;
                continue;
            }

            var key = val.ToString();
            val = items[idx + 1];
            if (includeReferences && val is PdfReference)
                val = ReadReference();
            // ReSharper disable once AssignNullToNotNullAttribute
            dict.Elements[key] = val;
            idx += 2;
        }

        return dict;
    }

    /// <summary>
    /// The flags a string read as the symbol given is made with, whether it stands directly in a
    /// dictionary or an array or as an indirect object of its own. A byte order mark makes it
    /// UTF-16 and angle brackets a hex literal; anything else is raw, a character to a byte.
    /// </summary>
    private static PdfStringFlags StringFlagsFor(Symbol symbol)
    {
        return symbol switch
        {
            Symbol.UnicodeString => PdfStringFlags.Unicode,
            Symbol.HexString => PdfStringFlags.HexLiteral,
            Symbol.UnicodeHexString => PdfStringFlags.Unicode | PdfStringFlags.HexLiteral,
            _ => PdfStringFlags.RawEncoding
        };
    }

    /// <summary>
    /// Parses whatever comes until the specified stop symbol is reached.
    /// </summary>
    private void ParseObject(Symbol stop)
    {
        Symbol symbol;
        while ((symbol = ScanNextToken()) != Symbol.Eof)
        {
            if (symbol == stop)
                return;

            switch (symbol)
            {
                case Symbol.Comment:
                    // ignore comments
                    break;

                case Symbol.R:
                    ReduceReference();
                    break;

                case Symbol.BeginArray:
                    var array = new PdfArray(_document);
                    ReadArray(array, false);
                    _stack.Shift(array);
                    break;

                case Symbol.BeginDictionary:
                    var dict = new PdfDictionary(_document);
                    ReadDictionary(dict, false);
                    _stack.Shift(dict);
                    break;

                case Symbol.BeginStream:
                    throw new NotImplementedException();

                case Symbol.EndObj:
                    // The object ended before the dictionary or array inside it was closed.
                    // XnView, for one, writes "12 0 obj << endobj". Take the end of the object
                    // as the end of what it holds, and put the keyword back so that the caller,
                    // which looks for it next, still finds it.
                    _lexer.Position -= _lexer.Token.Length;
                    return;

                default:
                    var value = DirectValueFor(symbol);
                    if (value == null)
                    {
                        // Anything else is not expected here.
                        ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
                        SkipCharsUntil(stop);
                        return;
                    }
                    _stack.Shift(value);
                    break;
            }
        }

        ParserDiagnostics.ThrowParserException("Unexpected end of file.");
    }

    /// <summary>
    /// The direct object a simple value read as the symbol given stands for, or null when the
    /// symbol is not one.
    /// </summary>
    private PdfItem DirectValueFor(Symbol symbol)
    {
        switch (symbol)
        {
            case Symbol.Null:
                return PdfNull.Value;

            case Symbol.Boolean:
                return new PdfBoolean(_lexer.TokenToBoolean);

            case Symbol.Integer:
                return new PdfInteger(_lexer.TokenToInteger);

            case Symbol.UInteger:
                return new PdfUInteger(_lexer.TokenToUInteger);

            case Symbol.Long:
                return new PdfLong(_lexer.TokenToLong);

            case Symbol.Real:
                return new PdfReal(_lexer.TokenToReal);

            case Symbol.String:
            case Symbol.UnicodeString:
            case Symbol.HexString:
            case Symbol.UnicodeHexString:
                return new PdfString(_lexer.Token, StringFlagsFor(symbol));

            case Symbol.Name:
                return new PdfName(_lexer.Token);

            default:
                return null;
        }
    }

    /// <summary>
    /// Replaces the object number and generation on top of the stack, the 'R' after them just read,
    /// with the reference they make.
    /// </summary>
    private void ReduceReference()
    {
        Debug.Assert(_stack.GetItem(-1) is PdfInteger && _stack.GetItem(-2) is PdfInteger);
        var objectID = new PdfObjectID(_stack.GetInteger(-2), _stack.GetInteger(-1));
        _stack.Reduce(ReferenceTo(objectID), 2);
    }

    /// <summary>
    /// What a reference to the object given reads as: the entry the cross-reference table has for
    /// it, a temporary one while that table is still being read, or else null.
    /// </summary>
    private PdfItem ReferenceTo(PdfObjectID objectID)
    {
        var iref = _document._irefTable[objectID];
        if (iref != null)
            return iref;

        // If a document has more than one PdfXRefTable it is possible that the first trailer has
        // indirect references to objects whos iref entry is not yet read in.
        if (_document._irefTable.IsUnderConstruction)
        {
            // XRefTable not complete when trailer is read. Create temporary irefs that are
            // removed later in PdfTrailer.FixXRefs.
            return new PdfReference(objectID, 0);
        }

        // PDF Reference section 3.2.9:
        // An indirect reference to an undefined object is not an error;
        // it is simply treated as a reference to the null object.
        return PdfNull.Value;
    }

    private Symbol ScanNextToken()
    {
        return _lexer.ScanNextToken();
    }

    private void SkipCharsUntil(Symbol stop)
    {
        Symbol symbol;
        switch (stop)
        {
            case Symbol.EndDictionary:
                SkipCharsUntil(">>");
                break;

            default:
                do
                {
                    symbol = ScanNextToken();
                } while (symbol != stop && symbol != Symbol.Eof);

                break;
        }
    }

    private void SkipCharsUntil(string text)
    {
        var length = text.Length;
        var idx = 0;
        char ch;
        while ((ch = _lexer.ScanNextChar(true)) != Chars.EOF)
        {
            if (ch == text[idx])
            {
                if (idx + 1 == length)
                {
                    _lexer.ScanNextChar(true);
                    return;
                }

                idx++;
            }
            else
            {
                idx = 0;
            }
        }
    }

    /// <summary>
    /// Reads the object ID and the generation and sets it into the specified object.
    /// </summary>
    private void ReadObjectID(PdfObject obj)
    {
        var objectNubmer = ReadInteger();
        var generationNumber = ReadInteger();
        ReadSymbol(Symbol.Obj);
        obj?.SetObjectID(objectNubmer, generationNumber);
    }

    private PdfItem ReadReference()
    {
        throw new NotImplementedException("ReadReference");
    }

    /// <summary>
    /// Reads the next symbol that must be the specified one.
    /// </summary>
    private void ReadSymbol(Symbol symbol)
    {
        var current = _lexer.ScanNextToken();
        if (symbol != current && current != Symbol.Eof)
            ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
    }

    /// <summary>
    /// Reads an integer value directly from the PDF data stream.
    /// </summary>
    private int ReadInteger()
    {
        var symbol = _lexer.ScanNextToken();
        if (symbol == Symbol.Integer)
            return _lexer.TokenToInteger;

        if (symbol == Symbol.R)
        {
            var position = _lexer.Position;
            ReadObjectID(null);
            var n = ReadInteger();
            ReadSymbol(Symbol.EndObj);
            _lexer.Position = position;
            return n;
        }

        ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return 0;
    }

    /// <summary>
    /// Reads an integer value directly from the PDF data stream.
    /// </summary>
    private long ReadLong()
    {
        var symbol = _lexer.ScanNextToken();
        if (symbol == Symbol.Long)
            return _lexer.TokenToLong;

        if (symbol == Symbol.Integer)
            return _lexer.TokenToLong;

        if (symbol == Symbol.R)
        {
            var position = _lexer.Position;
            ReadObjectID(null);
            var n = ReadInteger();
            ReadSymbol(Symbol.EndObj);
            _lexer.Position = position;
            return n;
        }

        ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return 0;
    }

    /// <summary>
    /// Reads an object from the PDF input stream using the default parser.
    /// </summary>
    public static PdfObject ReadObject(PdfDocument owner, PdfObjectID objectID)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var parser = new Parser(owner);
        return parser.ReadObject(null, objectID, false, false);
    }

    /// <summary>
    /// Reads the irefs from the compressed object with the specified index in the object stream
    /// of the object with the specified object id.
    /// </summary>
    internal void ReadIRefsFromCompressedObject(PdfObjectID objectID)
    {
        Debug.Assert(_document._irefTable.Contains(objectID));
        var iref = _document._irefTable[objectID];
        if (iref == null)
        {
            // We should never come here because the object stream must be a type 1 entry in the xref stream
            // and iref was created before.
            throw new NotImplementedException("This case is not coded or something else went wrong");
        }

        // Read in object stream object when we come here for the very first time.
        if (iref.Value == null)
        {
            try
            {
                Debug.Assert(_document._irefTable.Contains(iref.ObjectID));
                var pdfObject = (PdfDictionary)ReadObject(null, iref.ObjectID, false, false);
                var objectStream = new PdfObjectStream(pdfObject);
                Debug.Assert(objectStream.Reference == iref);
                Debug.Assert(objectStream.Reference.Value != null, "Something went wrong.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        Debug.Assert(iref.Value != null);

        var objectStreamStream = iref.Value as PdfObjectStream;
        if (objectStreamStream == null)
        {
            Debug.Assert(((PdfDictionary)iref.Value).Elements.GetName("/Type") == "/ObjStm");

            objectStreamStream = new PdfObjectStream((PdfDictionary)iref.Value);
            Debug.Assert(objectStreamStream.Reference == iref);
            Debug.Assert(objectStreamStream.Reference.Value != null, "Something went wrong.");
        }

        objectStreamStream.ReadReferences(_document._irefTable);
    }

    /// <summary>
    /// Reads the compressed object with the specified index in the object stream
    /// of the object with the specified object id.
    /// </summary>
    internal PdfReference ReadCompressedObject(PdfObjectID objectID, int index)
    {
        Debug.Assert(_document._irefTable.Contains(objectID));
        var iref = _document._irefTable[objectID];
        if (iref == null)
        {
            throw new NotImplementedException("This case is not coded or something else went wrong");
        }

        // Read in object stream object when we come here for the very first time.
        if (iref.Value == null)
        {
            try
            {
                Debug.Assert(_document._irefTable.Contains(iref.ObjectID));
                var pdfObject = (PdfDictionary)ReadObject(null, iref.ObjectID, false, false);
                var objectStream = new PdfObjectStream(pdfObject);
                Debug.Assert(objectStream.Reference == iref);
                Debug.Assert(objectStream.Reference.Value != null, "Something went wrong.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        Debug.Assert(iref.Value != null);

        var objectStreamStream = iref.Value as PdfObjectStream;
        if (objectStreamStream == null)
        {
            Debug.Assert(((PdfDictionary)iref.Value).Elements.GetName("/Type") == "/ObjStm");

            objectStreamStream = new PdfObjectStream((PdfDictionary)iref.Value);
            Debug.Assert(objectStreamStream.Reference == iref);
            Debug.Assert(objectStreamStream.Reference.Value != null, "Something went wrong.");
        }

        return objectStreamStream.ReadCompressedObject(index);
    }

    /// <summary>
    /// Reads the compressed object with the specified number at the given offset.
    /// The parser must be initialized with the stream an object stream object.
    /// </summary>
    internal PdfReference ReadCompressedObject(int objectNumber, int offset)
    {
        // Generation is always 0 for compressed objects.
        var objectID = new PdfObjectID(objectNumber);
        _lexer.Position = offset;
        var obj = ReadObject(null, objectID, false, true);

        // Remember where it came from, so that decrypting the document afterwards knows to leave
        // this object's strings alone: the object stream it was in was decrypted as a whole, and
        // the strings inside it were never separately encrypted.
        obj.IsFromObjectStream = true;

        return obj.Reference;
    }

    /// <summary>
    /// Reads the object stream header as pairs of integers from the beginning of the
    /// stream of an object stream. Parameter first is the value of the First entry of
    /// the the object stream object.
    /// </summary>
    internal int[][] ReadObjectStreamHeader(int n, int first)
    {
        // Create n pairs of integers with object number and offset.
        var header = new int[n][];
        for (var idx = 0; idx < n; idx++)
        {
            var number = ReadInteger();
            var offset = ReadInteger() + first; // Calculate absolute offset.
            header[idx] = [number, offset];
        }

        return header;
    }

    /// <summary>
    /// Reads the cross-reference table(s) and their trailer dictionary or
    /// cross-reference streams.
    /// </summary>
    internal PdfTrailer ReadTrailer(PdfReadAccuracy accuracy)
    {
        // Implementation note 18 Appendix H:
        // Acrobat viewers require only that the %%EOF marker appear somewhere within the last 1024
        // bytes of the file, which says nothing at all about where "startxref" is. SAP writes
        // several MByte of padding behind it; the file reported as empira/PDFsharp#390 a comment of
        // a gigabyte. So the file is scanned backwards from its end a chunk at a time, which finds
        // the last "startxref" wherever it lies and never holds more than one chunk of the file.
        // Reading the whole file into one string to search it - which is what this replaced - could
        // not open a file longer than 1,073,741,791 bytes at all, because that is as long as a
        // string gets, whatever memory the machine has.
        var idx = _lexer.FindLastMarker("startxref");

        if (idx < 0)
            throw new Exception("The StartXRef table could not be found, the file cannot be opened.");

        _lexer.Position = idx;

        ReadSymbol(Symbol.StartXRef);
        _lexer.Position = ReadLong();

        // Read all trailers. The one to keep is decided here and handed back; the caller is what
        // puts it on the document, so that reading a trailer writes nothing behind its back.
        PdfTrailer firstTrailer = null;
        // Where every section read so far begins. /Prev is written by whoever wrote the file, and a
        // section naming itself, or two naming each other, used to be read round and round for
        // ever. Reading a section a second time adds nothing - entries already in the table win -
        // so under Moderate the walk stops at the first one it has seen, and the chain up to there
        // is the whole of what the file has to say. Under Strict a chain that comes back on itself
        // is a fault in the file like any other, and is reported.
        var sectionsRead = new HashSet<long>();
        while (true)
        {
            if (!sectionsRead.Add(_lexer.Position))
            {
                if (accuracy == PdfReadAccuracy.Strict)
                    ParserDiagnostics.ThrowParserException(
                        "The cross-reference section at position " + _lexer.Position +
                        " is named by /Prev after it has already been read: the chain of revisions is a cycle.");

                break;
            }

            var trailer = ReadXRefTableAndTrailer(_document._irefTable, accuracy);

            // 1st trailer seems to be the best.
            firstTrailer ??= trailer;

            // Before /Prev, because the stream belongs to the revision just read rather than to the
            // one before it.
            ReadHybridCrossReferenceStream(trailer, accuracy);

            var size = trailer != null ? trailer.Elements.GetInteger(PdfTrailer.Keys.Size) : 0;
            if (size <= MaximumSize)
                LargestSize = Math.Max(LargestSize, size);

            var prev = trailer != null ? trailer.Elements.GetInteger(PdfTrailer.Keys.Prev) : 0;
            if (prev == 0)
                break;

            _lexer.Position = prev;
        }

        return firstTrailer;
    }

    /// <summary>
    /// Reads the cross-reference stream a classic trailer names in /XRefStm, which is where a
    /// hybrid-reference file says its compressed objects are.
    /// </summary>
    /// <remarks>
    /// ISO 32000-1 7.5.8.4. Such a file carries both kinds of cross-reference section for the same
    /// revision: a classic table, which marks every object that lives in an object stream as free
    /// so that a reader knowing nothing of object streams sees a document without them, and beside
    /// it a cross-reference stream saying where those objects really are. Skipping the entry is
    /// therefore not a tolerant reading of the file - it is reading the smaller document the table
    /// describes, and the objects left out are silently missing from the pages that use them.
    ///
    /// Entries already in the table win, which is the rule the table itself is read under and what
    /// makes the newest revision the one that counts. The stream's own trailer is dropped, because
    /// the classic trailer of this revision is the document's, and its /Prev is not followed: the
    /// classic trailers are the chain of revisions and each of them names its own stream. Both
    /// pdf.js and pypdf read one in the same place and the same order.
    /// </remarks>
    private void ReadHybridCrossReferenceStream(PdfTrailer trailer, PdfReadAccuracy accuracy)
    {
        var position = trailer?.Elements.GetInteger(PdfTrailer.Keys.XRefStm) ?? 0;
        if (position == 0)
            return;

        if (position < 0 || position >= _lexer.PdfLength)
        {
            if (accuracy == PdfReadAccuracy.Strict)
                ParserDiagnostics.ThrowParserException(
                    "The trailer's /XRefStm names position " + position + ", which is not inside the file.");

            return;
        }

        // Read into a table of its own and merged only once the whole stream has been read, so that
        // a stream damaged halfway through is dropped whole rather than left half in effect.
        var section = new PdfCrossReferenceTable(_document);
        if (!TryReadHybridSection(position, section, accuracy, out var read))
            return;

        // A classic table, or nothing recognisable, is not what /XRefStm promises.
        if (read is not PdfCrossReferenceStream xrefStream)
        {
            if (accuracy == PdfReadAccuracy.Strict)
                ParserDiagnostics.ThrowParserException(
                    "The trailer's /XRefStm names position " + position + ", where there is no cross-reference stream.");

            return;
        }

        MergeHybridSection(section, xrefStream);
    }

    /// <summary>
    /// Reads the cross-reference section at the position a trailer's /XRefStm names into the table
    /// given, and says whether it could be read. Under Strict one that cannot be is reported.
    /// </summary>
    private bool TryReadHybridSection(int position, PdfCrossReferenceTable section, PdfReadAccuracy accuracy,
        out PdfTrailer read)
    {
        // ReadXRefStream keeps every stream it reads for PdfReader to resolve compressed objects
        // from, and it does so before it has decoded a single entry - so a stream dropped here has
        // to be taken back out of that list too, or PdfReader reads the objects its entries name
        // regardless, and a stream damaged halfway through is half in effect after all.
        var streamsBefore = CrossReferenceStreams.Count;
        try
        {
            _lexer.Position = position;
            read = ReadXRefTableAndTrailer(section, accuracy);
            return true;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // What a cross-reference stream can be damaged in is not a list worth enumerating. Under
            // Moderate the document opens as the PDF 1.4 file the classic table describes - without
            // whichever compressed objects only the stream said where to find.
            if (accuracy == PdfReadAccuracy.Strict)
                throw new PdfReaderException(
                    "The cross-reference stream at position " + position +
                    ", named by the trailer's /XRefStm, could not be read.", ex);

            Debug.WriteLine(ex.Message);
            CrossReferenceStreams.RemoveRange(streamsBefore, CrossReferenceStreams.Count - streamsBefore);
            read = null;
            return false;
        }
    }

    /// <summary>
    /// Adds the entries of a hybrid file's cross-reference stream to the document's table, where
    /// the entries already in it win.
    /// </summary>
    private void MergeHybridSection(PdfCrossReferenceTable section, PdfCrossReferenceStream xrefStream)
    {
        foreach (var iref in section.AllReferences)
        {
            if (ReferenceEquals(iref.Value, xrefStream) && TryMergeTheStreamsOwnEntry(iref, xrefStream))
                continue;

            // Entries already in the table win; Add leaves one it already has alone.
            _document._irefTable.Add(iref);
        }
    }

    /// <summary>
    /// Merges the stream's own entry into an entry the table already has for its number, and says
    /// whether there was one.
    /// </summary>
    /// <remarks>
    /// When the table already has one, ReadXRefStream would have filled in its value rather than
    /// added a second, so the merge does the same - and on the same condition, that the entry points
    /// at this stream. A newer revision may have given the stream's number to another object
    /// (empira/PDFsharp#353), and hanging the stream on that object's entry makes the object read as
    /// a cross-reference stream. Left out of the table, the stream is still in
    /// CrossReferenceStreams, which is what its compressed objects are read through.
    /// </remarks>
    private bool TryMergeTheStreamsOwnEntry(PdfReference iref, PdfCrossReferenceStream xrefStream)
    {
        var existing = _document._irefTable[iref.ObjectID];
        if (existing == null)
            return false;

        if (existing.Value == null &&
            PointsAtStream(existing, xrefStream.StartOfSection, xrefStream.EndOfNumber))
        {
            xrefStream.Reference = null;
            existing.Value = xrefStream;
        }

        return true;
    }

    /// <summary>
    /// Reads cross reference table(s) and trailer(s).
    /// </summary>
    private PdfTrailer ReadXRefTableAndTrailer(PdfCrossReferenceTable xrefTable, PdfReadAccuracy accuracy)
    {
        Debug.Assert(xrefTable != null);

        // Where this section begins: /Prev and startxref point here, and a newer cross-reference
        // stream that lists this one as an object gives this offset too.
        var startOfSection = _lexer.Position;
        var symbol = ScanNextToken();

        if (symbol == Symbol.XRef) // Is it a cross-reference table?
        {
            // Reference: 3.4.3  Cross-Reference Table / Page 93
            while (true)
            {
                symbol = ScanNextToken();
                if (symbol == Symbol.Integer)
                {
                    var start = _lexer.TokenToInteger;
                    var length = ReadInteger();
                    for (var id = start; id < start + length; id++)
                    {
                        var position = ReadLong();
                        var generation = ReadInteger();
                        ReadSymbol(Symbol.Keyword);
                        var token = _lexer.Token;

                        // Skip start entry
                        if (id == 0)
                            continue;

                        // Skip unused entries.
                        if (token != "n")
                            continue;

                        // Check if the object at the address has the correct ID and generation.
                        var idToUse = id;
                        if (!CheckXRefTableEntry(position, id, generation, out var idChecked, out var generationChecked))
                        {
                            // Found the keyword "obj", but ID or generation did not match.
                            // There is a tool where ID is off by one. In this case we use the ID from the object, not the ID from the XRef table.
                            if (generation == generationChecked && id == idChecked + 1)
                                idToUse = idChecked;
                            else if (accuracy == PdfReadAccuracy.Strict)
                                ParserDiagnostics.ThrowParserException("Invalid entry in XRef table, ID=" + id +
                                                                       ", Generation=" + generation +
                                                                       ", Position=" + position +
                                                                       ", ID of referenced object=" + idChecked +
                                                                       ", Generation of referenced object=" +
                                                                       generationChecked);
                        }

                        // Even it is restricted, an object can exists in more than one subsection.
                        // (PDF Reference Implementation Notes 15).
                        var objectID = new PdfObjectID(idToUse, generation);

                        // Ignore the latter one.
                        if (xrefTable.Contains(objectID))
                            continue;
                        xrefTable.Add(new PdfReference(objectID, position));
                    }
                }
                else if (symbol == Symbol.Trailer)
                {
                    ReadSymbol(Symbol.BeginDictionary);
                    var trailer = new PdfTrailer(_document);
                    ReadDictionary(trailer, false);
                    return trailer;
                }
                else
                {
                    ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
                }
            }
        }
        // ReSharper disable once RedundantIfElseBlock because of code readability.
        else if (symbol == Symbol.Integer) // Is it an cross-reference stream?
        {
            // Reference: 3.4.7  Cross-Reference Streams / Page 93

            // The parsed integer is the object id of the cross-refernece stream.
            return ReadXRefStream(xrefTable, startOfSection);
        }

        return null;
    }

    /// <summary>
    /// Checks the x reference table entry. Returns true if everything is correct.
    /// Return false if the keyword "obj" was found, but ID or Generation are incorrect.
    /// Throws an exception otherwise.
    /// </summary>
    /// <param name="position">The position where the object is supposed to be.</param>
    /// <param name="id">The ID from the XRef table.</param>
    /// <param name="generation">The generation from the XRef table.</param>
    /// <param name="idChecked">The identifier found in the PDF file.</param>
    /// <param name="generationChecked">The generation found in the PDF file.</param>
    /// <returns></returns>
    private bool CheckXRefTableEntry(long position, int id, int generation, out int idChecked,
        out int generationChecked)
    {
        var origin = _lexer.Position;
        idChecked = -1;
        generationChecked = -1;
        try
        {
            _lexer.Position = position;
            idChecked = ReadInteger();
            generationChecked = ReadInteger();
            var symbol = _lexer.ScanNextToken();
            if (symbol != Symbol.Obj)
                ParserDiagnostics.ThrowParserException("Invalid entry in XRef table, ID=" + id + ", Generation=" +
                                                       generation + ", Position=" + position);

            if (id != idChecked || generation != generationChecked)
                return false;
        }
        catch (PdfReaderException)
        {
            throw;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            ParserDiagnostics.ThrowParserException(
                "Invalid entry in XRef table, ID=" + id + ", Generation=" + generation + ", Position=" + position,
                ex);
        }
        finally
        {
            _lexer.Position = origin;
        }

        return true;
    }

    /// <summary>
    /// Whether an entry with no value yet is the entry of the cross-reference stream whose section
    /// begins at <paramref name="startOfSection"/> and whose object number ends at
    /// <paramref name="endOfNumber"/>, rather than another object's under the same number.
    /// </summary>
    private static bool PointsAtStream(PdfReference iref, long startOfSection, long endOfNumber)
        => iref.Position >= startOfSection && iref.Position <= endOfNumber;

    /// <summary>
    /// Reads cross reference stream(s).
    /// </summary>
    private PdfCrossReferenceStream ReadXRefStream(PdfCrossReferenceTable xrefTable, long startOfSection)
    {
        // Read cross reference stream.

        // The object number has just been read, so the stream's own header lies between here and
        // where the section began.
        var endOfNumber = _lexer.Position;
        var number = _lexer.TokenToInteger;
        var generation = ReadInteger();
        Debug.Assert(generation == 0);

        ReadSymbol(Symbol.Obj);
        ReadSymbol(Symbol.BeginDictionary);
        var objectID = new PdfObjectID(number, generation);

        var xrefStream = new PdfCrossReferenceStream(_document);

        ReadDictionary(xrefStream, false);
        ReadSymbol(Symbol.BeginStream);
        ReadStream(xrefStream);

        // An additional cross-reference (/Prev) could have been referenced in the first cross-reference by position.
        // That goes into item.Type == 1 below and adds its objectID into the xrefTable with just a position and no value.
        // Then we can't just do `xrefTable.Add(iref)` because that is a no-op when the objectID is already present in the ObjectTable.
        // Making sure that the iref.Value is set here correctly ensure that the mechanisms in PdfReader will work correctly:
        // 1. It needs to find a PdfCrossReferenceStream in iref.Value in order to resolve compressed objects.
        // 2. If we leave null in iref.Value it would do redundant parsing to resolve the value again.
        //
        // But an entry already holding this number is this stream only if it points at this
        // stream. An incremental update may give a new object the number an earlier revision's
        // cross-reference stream had - the file attached to empira/PDFsharp#213 puts its /AcroForm
        // there - and the newer revision is read first, so its entry is already here, with a
        // position and no value yet. Hanging this stream on it made the form read as a
        // cross-reference stream with no /Fields (empira/PDFsharp#353). Such a stream is left out
        // of the table, where the newer object keeps the number; CrossReferenceStreams is what
        // still finds it, for the compressed objects its revision indexes.
        xrefStream.StartOfSection = startOfSection;
        xrefStream.EndOfNumber = endOfNumber;
        var iref = xrefTable[objectID];
        if (iref != null)
        {
            if (iref.Value == null && PointsAtStream(iref, startOfSection, endOfNumber))
            {
                iref.Value = xrefStream;
            }
        }
        else
        {
            iref = new PdfReference(xrefStream)
            {
                ObjectID = objectID,
                Value = xrefStream
            };
            xrefTable.Add(iref);
        }

        CrossReferenceStreams.Add(xrefStream);

        Debug.Assert(xrefStream.Stream != null);
        var bytes = xrefStream.Stream.UnfilteredValue;

        var size = xrefStream.Elements.GetInteger(PdfCrossReferenceStream.Keys.Size);
        var index = xrefStream.Elements.GetValue(PdfCrossReferenceStream.Keys.Index) as PdfArray;
        var w = (PdfArray)xrefStream.Elements.GetValue(PdfCrossReferenceStream.Keys.W);

        // E.g.: W[1 2 1] ¤ Index[7 12] ¤ Size 19

        // Setup subsections.
        int subsectionCount;
        int[][] subsections;
        var subsectionEntryCount = 0;
        if (index == null)
        {
            // Setup with default values.
            subsectionCount = 1;
            subsections = new int[subsectionCount][];
            subsections[0] = [0, size]; // HACK: What is size? Contratiction in PDF reference.
            subsectionEntryCount = size;
        }
        else
        {
            // Read subsections from array.
            Debug.Assert(index.Elements.Count % 2 == 0);
            subsectionCount = index.Elements.Count / 2;
            subsections = new int[subsectionCount][];
            for (var idx = 0; idx < subsectionCount; idx++)
            {
                subsections[idx] = [index.Elements.GetInteger(2 * idx), index.Elements.GetInteger(2 * idx + 1)];
                subsectionEntryCount += subsections[idx][1];
            }
        }

        // W key.
        Debug.Assert(w.Elements.Count == 3);
        int[] wsize = [w.Elements.GetInteger(0), w.Elements.GetInteger(1), w.Elements.GetInteger(2)];
        var wsum = StreamHelper.WSize(wsize);
        Debug.Assert(wsum * subsectionEntryCount == bytes.Length, "Check implementation here.");

        var index2 = -1;
        for (var ssc = 0; ssc < subsectionCount; ssc++)
        {
            var abc = subsections[ssc][1];
            for (var idx = 0; idx < abc; idx++)
            {
                index2++;

                var item =
                    new PdfCrossReferenceStream.CrossReferenceStreamEntry
                    {
                        Type = (uint)StreamHelper.ReadBytes(bytes, index2 * wsum, wsize[0]),
                        Field2 = (long)StreamHelper.ReadBytes(bytes, index2 * wsum + wsize[0], wsize[1]),
                        Field3 = (uint)StreamHelper.ReadBytes(bytes, index2 * wsum + wsize[0] + wsize[1], wsize[2]),
                        ObjectNumber = subsections[ssc][0] + idx
                    };

                xrefStream.Entries.Add(item);

                switch (item.Type)
                {
                    case 0:
                        // Nothing to do, not needed.
                        break;

                    case 1: // offset / generation number
                        //// Even it is restricted, an object can exists in more than one subsection.
                        //// (PDF Reference Implementation Notes 15).

                        // A byte offset, and so as wide as the file: a file past 2 GiB has
                        // offsets an int cannot hold (implementation note 21 in Appendix H).
                        var position = item.Field2;
                        objectID = ReadObjectNumber(position);
                        Debug.Assert(objectID.GenerationNumber == item.Field3);

                        //// Ignore the latter one.
                        if (!xrefTable.Contains(objectID))
                        {
                            // Add iref for all uncompressed objects.
                            xrefTable.Add(new PdfReference(objectID, position));
                        }

                        break;

                    case 2:
                        // A compressed object is read later, once every object stream is known,
                        // but its number is claimed now, while the revisions are still being read
                        // newest first, so that an older revision's entry for the same number
                        // cannot take it. A position of -1 says only "compressed", and it is also
                        // what tells the reader later that an older revision's compressed copy of
                        // an object a newer revision wrote out in full is not the one to read.
                        // Object 0 is never an object, whatever a malformed stream says of it.
                        if (item.ObjectNumber < 1)
                            break;
                        var compressedID = new PdfObjectID(item.ObjectNumber);
                        if (!xrefTable.Contains(compressedID))
                            xrefTable.Add(new PdfReference(compressedID, -1));
                        break;
                }
            }
        }

        return xrefStream;
    }

    /// <summary>
    /// Parses a PDF date string, or a date in the invariant culture's format. Answers false for one
    /// that is malformed rather than throwing: a bad /CreationDate is no reason to refuse a document.
    /// </summary>
    /// <remarks>
    ///  Format is
    /// YYYY Year MM month DD day (01-31)  HH hour (00-23)  mm minute (00-59) ss second (00.59)
    /// O is the relationship of local time to Universal Time (UT), denoted by one of the characters +, -, or Z (see below)
    /// HH followed by ' is the absolute value of the offset from UT in hours (00-23)
    /// mm followed by ' is the absolute value of the offset from UT in minutes (00-59)
    /// For example, December 23, 1998, at 7:52 PM, U.S.Pacific Standard Time, is represented by the string,
    /// D:19981223195200-08'00'
    /// </remarks>
    internal static bool TryParseDateTime(string date, out DateTime value)
    {
        value = default;
        if (date == null)
            return false;

        if (!date.StartsWith("D:", StringComparison.Ordinal))
        {
            // Some libraries use plain English format.
            return DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        // D:YYYYMMDDHHmmSSOHH'mm'
        //   ^2      ^10   ^16 ^20
        // A group too short to be all there is left at zero - and a date without its day is no
        // date at all, since there is no year zero.
        if (!TryReadCalendarDate(date, out var year, out var month, out var day) ||
            !TryReadTimeOfDay(date, out var hour, out var minute, out var second) ||
            !TryReadOffsetFromUT(date, out var o, out var hh, out var mm))
            return false;

        // There are miserable PDF tools around the world.
        month = Math.Min(Math.Max(month, 1), 12);
        if (IsOutOfRange(year, month, day, hour, minute, second))
            return false;

        var local = new DateTime(year, month, day, hour, minute, second);
        if (!TryConvertToUniversalTime(local, o, hh, mm, out var datetime))
            return false;

        // Now that we converted datetime to UTC, mark it as UTC.
        value = DateTime.SpecifyKind(datetime, DateTimeKind.Utc);
        return true;
    }

    /// <summary>
    /// Reads the year, month and day of a PDF date, D:YYYYMMDD, all left at zero when the date is too
    /// short to hold them.
    /// </summary>
    private static bool TryReadCalendarDate(string date, out int year, out int month, out int day)
    {
        year = month = day = 0;
        if (date.Length < 10)
            return true;

        return TryParseField(date, 2, 4, out year) &&
               TryParseField(date, 6, 2, out month) &&
               TryParseField(date, 8, 2, out day);
    }

    /// <summary>
    /// Reads the hour, minute and second of a PDF date, which follow the day as HHmmSS, all left at
    /// zero when the date is too short to hold them.
    /// </summary>
    private static bool TryReadTimeOfDay(string date, out int hour, out int minute, out int second)
    {
        hour = minute = second = 0;
        if (date.Length < 16)
            return true;

        return TryParseField(date, 10, 2, out hour) &&
               TryParseField(date, 12, 2, out minute) &&
               TryParseField(date, 14, 2, out second);
    }

    /// <summary>
    /// Reads the relationship of a PDF date's time to UT, OHH'mm', which is 'Z' when the date says
    /// so or is too short to say anything.
    /// </summary>
    private static bool TryReadOffsetFromUT(string date, out char o, out int hh, out int mm)
    {
        o = 'Z';
        hh = mm = 0;
        if (date.Length < 23)
            return true;

        o = date[16];
        if (o == 'Z')
            return true;

        // Anything but +, - or Z is no designator, and without the apostrophes the
        // digits either side of them are not an offset's hours and minutes.
        if ((o != '+' && o != '-') || date[19] != '\'' || date[22] != '\'')
            return false;
        if (!TryParseField(date, 17, 2, out hh) ||
            !TryParseField(date, 20, 2, out mm))
            return false;

        return hh is >= 0 and <= 23 && mm is >= 0 and <= 59;
    }

    private static bool IsOutOfRange(int year, int month, int day, int hour, int minute, int second) =>
        year < 1 || year > 9999 || day < 1 || day > DateTime.DaysInMonth(year, month) ||
        hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59;

    /// <summary>
    /// Takes a date and time the offset given away from UT to UT, failing when that would carry it
    /// out of the range a <see cref="DateTime"/> holds.
    /// </summary>
    private static bool TryConvertToUniversalTime(DateTime local, char o, int hh, int mm, out DateTime universal)
    {
        universal = local;
        if (o == 'Z')
            return true;

        // West of UT is behind it, so the offset is added to reach UT; east of it, subtracted.
        var offset = new TimeSpan(hh, mm, 0).Ticks;
        var ticks = o == '-' ? local.Ticks + offset : local.Ticks - offset;
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            return false;

        universal = new DateTime(ticks);
        return true;
    }

    /// <summary>
    /// Reads one group of digits of a PDF date the way <see cref="int.Parse(string)"/> does, which
    /// is what read them before: white space around the digits and a sign are both accepted.
    /// </summary>
    private static bool TryParseField(string date, int start, int length, out int value) =>
        int.TryParse(date.AsSpan(start, length), NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out value);

    private ParserState SaveState()
    {
        return new ParserState
        {
            Position = _lexer.Position,
            Symbol = _lexer.Symbol
        };
    }

    private void RestoreState(ParserState state)
    {
        _lexer.Position = state.Position;
        _lexer.Symbol = state.Symbol;
    }

    private class ParserState
    {
        public long Position;
        public Symbol Symbol;
    }


    private readonly PdfDocument _document;
    private readonly Lexer _lexer;
    private readonly ShiftStack _stack;
}

internal static class StreamHelper
{
    public static int WSize(int[] w)
    {
        Debug.Assert(w.Length == 3);
        return w[0] + w[1] + w[2];
    }

    public static ulong ReadBytes(byte[] bytes, int index, int byteCount)
    {
        ulong value = 0;
        for (var idx = 0; idx < byteCount; idx++)
        {
            value *= 256;
            value += bytes[index + idx];
        }

        return value;
    }
}
