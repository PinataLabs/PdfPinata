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

    public PdfObjectID ReadObjectNumber(int position)
    {
        _lexer.Position = position;
        int objectNumber = ReadInteger();
        int generationNumber = ReadInteger();
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
        int objectNumber = objectID.ObjectNumber;
        int generationNumber = objectID.GenerationNumber;
        if (!fromObjecStream)
        {
            MoveToObject(objectID);
            objectNumber = ReadInteger();
            generationNumber = ReadInteger();
        }
        // The object header can disagree with the iref table that led here, and the object ID from
        // the table is the one to believe. PDF4NET 2.6's 'unicode.pdf' sample, for one, gives
        // objects 84 to 87 the same offset in its iref table, so all four read back as the same
        // dictionary with a header saying object 84 — and every reader tested shows it anyway.
        // Always use object ID from iref table (see above).
        objectNumber = objectID.ObjectNumber;
        generationNumber = objectID.GenerationNumber;

        if (!fromObjecStream)
            ReadSymbol(Symbol.Obj);

        bool checkForStream = false;
        Symbol symbol = ScanNextToken();
        switch (symbol)
        {
            case Symbol.BeginArray:
                PdfArray array;
                if (pdfObject == null)
                    array = new PdfArray(_document);
                else
                    array = (PdfArray)pdfObject;
                //PdfObject.RegisterObject(array, objectID, generation);
                pdfObject = ReadArray(array, includeReferences);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                break;

            case Symbol.BeginDictionary:
                PdfDictionary dict;
                if (pdfObject == null)
                    dict = new PdfDictionary(_document);
                else
                    dict = (PdfDictionary)pdfObject;
                //PdfObject.RegisterObject(dict, objectID, generation);
                checkForStream = true;
                pdfObject = ReadDictionary(dict, includeReferences);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                break;

            // Acrobat 6 Professional proudly presents: The Null object!
            // Even with a one-digit object number an indirect reference «x 0 R» to this object is
            // one character larger than the direct use of «null». Probable this is the reason why
            // it is true that Acrobat Web Capture 6.0 creates this object, but obviously never
            // creates a reference to it!
            case Symbol.Null:
                pdfObject = new PdfNullObject(_document);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Boolean:
                pdfObject = new PdfBooleanObject(_document,
                    String.Compare(_lexer.Token, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Integer:
                pdfObject = new PdfIntegerObject(_document, _lexer.TokenToInteger);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.UInteger:
                pdfObject = new PdfUIntegerObject(_document, _lexer.TokenToUInteger);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Long:
                pdfObject = new PdfLongObject(_document, _lexer.TokenToLong);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Real:
                pdfObject = new PdfRealObject(_document, _lexer.TokenToReal);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.String:
            case Symbol.UnicodeString:
            case Symbol.HexString:
            case Symbol.UnicodeHexString:
                pdfObject = new PdfStringObject(_document, _lexer.Token);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Name:
                pdfObject = new PdfNameObject(_document, _lexer.Token);
                pdfObject.SetObjectID(objectNumber, generationNumber);
                if (!fromObjecStream)
                    ReadEndObject();
                return pdfObject;

            case Symbol.Keyword:
                // Should not come here anymore.
                ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
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
        {
            PdfDictionary dict = (PdfDictionary)pdfObject;
            Debug.Assert(checkForStream, "Unexpected stream...");
            var startOfStream = _lexer.Position;
            byte[] bytes = TheStreamTheDictionaryDescribes(dict, startOfStream);

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
            symbol = ScanNextToken();
            if (symbol == Symbol.Eof)
            {
                symbol = Symbol.EndObj;
            }
        }

        if (!fromObjecStream && symbol != Symbol.EndObj)
            EndObject(symbol, endOfObject);
        return pdfObject;
    }

    /// <summary>
    ///   Reads the end of an indirect object, tolerating a file that leaves "endobj" out.
    /// </summary>
    private void ReadEndObject()
    {
        var endOfObject = _lexer.Position;
        Symbol symbol = _lexer.ScanNextToken();
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
        switch (symbol)
        {
            // The object number of the object that follows — but only if it really is one, so
            // that a stray number left after an object is still reported.
            case Symbol.Integer:
            case Symbol.UInteger:
            case Symbol.Long:
                return BeginsAnIndirectObject();

            // Or the end of the body of the file.
            case Symbol.XRef:
            case Symbol.Trailer:
            case Symbol.StartXRef:
            case Symbol.Eof:
                return true;

            default:
                return false;
        }
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
            Symbol generationNumber = _lexer.ScanNextToken();
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
        Symbol symbol = _lexer.Symbol;
        Debug.Assert(symbol == Symbol.BeginStream);
        Debug.Assert(dict.Stream == null, "Dictionary already has a stream.");
        var startOfStream = _lexer.Position;
        byte[] bytes = TheStreamTheDictionaryDescribes(dict, startOfStream);
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
        int length = GetStreamLength(dict);
        if (length < 0 || startOfStream + length > _lexer.PdfLength)
            return null;

        byte[] bytes = _lexer.ReadStream(length);
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
        int end = bytes.Length;
        if (end > 0 && bytes[end - 1] == (byte)Chars.LF)
            end--;
        if (end > 0 && bytes[end - 1] == (byte)Chars.CR)
            end--;

        return end == bytes.Length ? bytes : bytes.AsSpan(0, end).ToArray();
    }

    // HACK: Solve problem more general.
    private int GetStreamLength(PdfDictionary dict)
    {
        if (dict.Elements.Count == 0)
            return 0;

        if (dict.Elements["/F"] != null)
        {
            throw new NotImplementedException("File streams are not yet implemented.");
        }

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
        if (iref != null && iref.Value is PdfIntegerObject known)
            return known;

        // Objects inside an object stream have no position in the file. When such an object has not
        // been read yet there is no way to reach it from here.
        if (iref != null && iref.Position < 0)
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

        if (array == null)
            array = new PdfArray(_document);

        int sp = _stack.SP;
        ParseObject(Symbol.EndArray);
        int count = _stack.SP - sp;
        PdfItem[] items = _stack.ToArray(sp, count);
        _stack.Reduce(count);
        for (int idx = 0; idx < count; idx++)
        {
            PdfItem val = items[idx];
            if (includeReferences && val is PdfReference)
                val = ReadReference((PdfReference)val, true);
            array.Elements.Add(val);
        }

        return array;
    }

    internal PdfDictionary ReadDictionary(PdfDictionary dict, bool includeReferences)
    {
        Debug.Assert(Symbol == Symbol.BeginDictionary);

        if (dict == null)
            dict = new PdfDictionary(_document);
        DictionaryMeta meta = dict.Meta;

        int sp = _stack.SP;
        ParseObject(Symbol.EndDictionary);
        int count = _stack.SP - sp;
        PdfItem[] items = _stack.ToArray(sp, count);
        _stack.Reduce(count);
        // An entry is a name followed by a value, but writers do produce dictionaries that do not
        // hold to that: pdfTeX writes its PTEX.FullBanner as two strings with no key in front of
        // them, and a truncated dictionary leaves a key with no value behind it. Keep the pairs
        // that are there, and drop what cannot be paired up rather than refusing the document.
        for (int idx = 0; idx + 1 < count;)
        {
            PdfItem val = items[idx];
            if (!(val is PdfName))
            {
                // A value with nothing to key it under. Step over it alone, so that the entries
                // after it are still read as the pairs they are.
                idx++;
                continue;
            }

            string key = val.ToString();
            val = items[idx + 1];
            if (includeReferences && val is PdfReference)
                val = ReadReference((PdfReference)val, true);
            dict.Elements[key] = val;
            idx += 2;
        }

        return dict;
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

                case Symbol.Null:
                    _stack.Shift(PdfNull.Value);
                    break;

                case Symbol.Boolean:
                    _stack.Shift(new PdfBoolean(_lexer.TokenToBoolean));
                    break;

                case Symbol.Integer:
                    _stack.Shift(new PdfInteger(_lexer.TokenToInteger));
                    break;

                case Symbol.UInteger:
                    _stack.Shift(new PdfUInteger(_lexer.TokenToUInteger));
                    break;

                case Symbol.Long:
                    _stack.Shift(new PdfLong(_lexer.TokenToLong));
                    break;

                case Symbol.Real:
                    _stack.Shift(new PdfReal(_lexer.TokenToReal));
                    break;

                case Symbol.String:
                    //stack.Shift(new PdfString(lexer.Token, PdfStringFlags.PDFDocEncoding));
                    _stack.Shift(new PdfString(_lexer.Token, PdfStringFlags.RawEncoding));
                    break;

                case Symbol.UnicodeString:
                    _stack.Shift(new PdfString(_lexer.Token, PdfStringFlags.Unicode));
                    break;

                case Symbol.HexString:
                    _stack.Shift(new PdfString(_lexer.Token, PdfStringFlags.HexLiteral));
                    break;

                case Symbol.UnicodeHexString:
                    _stack.Shift(new PdfString(_lexer.Token, PdfStringFlags.Unicode | PdfStringFlags.HexLiteral));
                    break;

                case Symbol.Name:
                    _stack.Shift(new PdfName(_lexer.Token));
                    break;

                case Symbol.R:
                {
                    Debug.Assert(_stack.GetItem(-1) is PdfInteger && _stack.GetItem(-2) is PdfInteger);
                    PdfObjectID objectID = new PdfObjectID(_stack.GetInteger(-2), _stack.GetInteger(-1));

                    PdfReference iref = _document._irefTable[objectID];
                    if (iref == null)
                    {
                        // If a document has more than one PdfXRefTable it is possible that the first trailer has
                        // indirect references to objects whos iref entry is not yet read in.
                        if (_document._irefTable.IsUnderConstruction)
                        {
                            // XRefTable not complete when trailer is read. Create temporary irefs that are
                            // removed later in PdfTrailer.FixXRefs.
                            iref = new PdfReference(objectID, 0);
                            _stack.Reduce(iref, 2);
                            break;
                        }

                        // PDF Reference section 3.2.9:
                        // An indirect reference to an undefined object is not an error;
                        // it is simply treated as a reference to the null object.
                        _stack.Reduce(PdfNull.Value, 2);
                        // Let's see what null objects are good for...
                        //Debug.Assert(false, "Null object detected!");
                        //stack.Reduce(PdfNull.Value, 2);
                    }
                    else
                        _stack.Reduce(iref, 2);

                    break;
                }

                case Symbol.BeginArray:
                    PdfArray array = new PdfArray(_document);
                    ReadArray(array, false);
                    _stack.Shift(array);
                    break;

                case Symbol.BeginDictionary:
                    PdfDictionary dict = new PdfDictionary(_document);
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
                    _lexer.Position = _lexer.Position - _lexer.Token.Length;
                    return;

                // Not expected here:
                //case Symbol.None:
                //case Symbol.Keyword:
                //case Symbol.EndStream:
                //case Symbol.EndArray:
                //case Symbol.EndDictionary:
                //case Symbol.Obj:
                //case Symbol.XRef:
                //case Symbol.Trailer:
                //case Symbol.StartXRef:
                //case Symbol.Eof:
                default:
                    ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
                    SkipCharsUntil(stop);
                    return;
            }
        }

        ParserDiagnostics.ThrowParserException("Unexpected end of file.");
    }

    private Symbol ScanNextToken()
    {
        return _lexer.ScanNextToken();
    }

    private Symbol ScanNextToken(out string token)
    {
        Symbol symbol = _lexer.ScanNextToken();
        token = _lexer.Token;
        return symbol;
    }

    private Symbol SkipCharsUntil(Symbol stop)
    {
        Symbol symbol;
        switch (stop)
        {
            case Symbol.EndDictionary:
                return SkipCharsUntil(">>", stop);

            default:
                do
                {
                    symbol = ScanNextToken();
                } while (symbol != stop && symbol != Symbol.Eof);

                return symbol;
        }
    }

    private Symbol SkipCharsUntil(string text, Symbol stop)
    {
        int length = text.Length;
        int idx = 0;
        char ch;
        while ((ch = _lexer.ScanNextChar(true)) != Chars.EOF)
        {
            if (ch == text[idx])
            {
                if (idx + 1 == length)
                {
                    _lexer.ScanNextChar(true);
                    return stop;
                }

                idx++;
            }
            else
                idx = 0;
        }

        return Symbol.Eof;
    }

    //protected Symbol ScanNextToken(out string token, bool testReference)
    //{
    //  Symbol symbol = lexer.ScanNextToken(testReference);
    //  token = lexer.Token;
    //  return symbol;
    //}

    //    internal object ReadObject(int position)
    //    {
    //      lexer.Position = position;
    //      return ReadObject(false);
    //    }
    //
    //    internal virtual object ReadObject(bool directObject)
    //    {
    //      throw new InvalidOperationException("PdfParser.ReadObject() base class called");
    //    }

    /// <summary>
    /// Reads the object ID and the generation and sets it into the specified object.
    /// </summary>
    private void ReadObjectID(PdfObject obj)
    {
        int objectNubmer = ReadInteger();
        int generationNumber = ReadInteger();
        ReadSymbol(Symbol.Obj);
        if (obj != null)
            obj.SetObjectID(objectNubmer, generationNumber);
    }

    private PdfItem ReadReference(PdfReference iref, bool includeReferences)
    {
        throw new NotImplementedException("ReadReference");
    }

    /// <summary>
    /// Reads the next symbol that must be the specified one.
    /// </summary>
    private Symbol ReadSymbol(Symbol symbol)
    {
        Symbol current = _lexer.ScanNextToken();
        if (symbol != current && current != Symbol.Eof)
            ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return current;
    }

    /// <summary>
    /// Reads the next token that must be the specified one.
    /// </summary>
    private Symbol ReadToken(string token)
    {
        Symbol current = _lexer.ScanNextToken();
        if (token != _lexer.Token)
            ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return current;
    }

    /// <summary>
    /// Reads a name from the PDF data stream. The preceding slash is part of the result string.
    /// </summary>
    private string ReadName()
    {
        string name;
        Symbol symbol = ScanNextToken(out name);
        if (symbol != Symbol.Name)
            ParserDiagnostics.HandleUnexpectedToken(name);
        return name;
    }

    /*
        /// <summary>
        /// Reads a string immediately or (optionally) indirectly from the PDF data stream.
        /// </summary>
        protected string ReadString(bool canBeIndirect)
        {
          Symbol symbol = Symbol.None; //lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.String || symbol == Symbol.HexString)
            return lexer.Token;
          else if (symbol == Symbol.R)
          {
            int position = lexer.Position;
            MoveToObject(lexer.Token);
            ReadObjectID(null);
            string s = ReadString();
            ReadSymbol(Symbol.EndObj);
            lexer.Position = position;
            return s;
          }
          thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        protected string ReadString()
        {
          return ReadString(false);
        }

        /// <summary>
        /// Reads a string immediately or (optionally) indirectly from the PDF data stream.
        /// </summary>
        protected bool ReadBoolean(bool canBeIndirect)
        {
          Symbol symbol = lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.Boolean)
            return lexer.TokenToBoolean;
          else if (symbol == Symbol.R)
          {
            int position = lexer.Position;
            MoveToObject(lexer.Token);
            ReadObjectID(null);
            bool b = ReadBoolean();
            ReadSymbol(Symbol.EndObj);
            lexer.Position = position;
            return b;
          }
          thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        protected bool ReadBoolean()
        {
          return ReadBoolean(false);
        }
    */

    /// <summary>
    /// Reads an integer value directly from the PDF data stream.
    /// </summary>
    private int ReadInteger(bool canBeIndirect)
    {
        Symbol symbol = _lexer.ScanNextToken();
        if (symbol == Symbol.Integer)
            return _lexer.TokenToInteger;

        if (symbol == Symbol.R)
        {
            var position = _lexer.Position;
            //        MoveToObject(lexer.Token);
            ReadObjectID(null);
            int n = ReadInteger();
            ReadSymbol(Symbol.EndObj);
            _lexer.Position = position;
            return n;
        }

        ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return 0;
    }

    private int ReadInteger()
    {
        return ReadInteger(false);
    }

    private long ReadLong()
    {
        return ReadLong(false);
    }

    /// <summary>
    /// Reads an integer value directly from the PDF data stream.
    /// </summary>
    private long ReadLong(bool canBeIndirect)
    {
        Symbol symbol = _lexer.ScanNextToken();
        if (symbol == Symbol.Long)
            return _lexer.TokenToLong;

        if (symbol == Symbol.Integer)
            return _lexer.TokenToLong;

        if (symbol == Symbol.R)
        {
            var position = _lexer.Position;
            ReadObjectID(null);
            int n = ReadInteger();
            ReadSymbol(Symbol.EndObj);
            _lexer.Position = position;
            return n;
        }

        ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
        return 0;
    }

    //    /// <summary>
    //    /// Reads a real value directly or (optionally) indirectly from the PDF data stream.
    //    /// </summary>
    //    double ReadReal(bool canBeIndirect)
    //    {
    //      Symbol symbol = lexer.ScanNextToken(canBeIndirect);
    //      if (symbol == Symbol.Real || symbol == Symbol.Integer)
    //        return lexer.TokenToReal;
    //      else if (symbol == Symbol.R)
    //      {
    //        int position = lexer.Position;
    ////        MoveToObject(lexer.Token);
    //        ReadObjectID(null);
    //        double f = ReadReal();
    //        ReadSymbol(Symbol.EndObj);
    //        lexer.Position = position;
    //        return f;
    //      }
    //      thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
    //    }
    //
    //    double ReadReal()
    //    {
    //      return ReadReal(false);
    //    }

    //    /// <summary>
    //    /// Reads an object from the PDF input stream. If the object has a specialized parser, it it used.
    //    /// </summary>
    //    public static PdfObject ReadObject(PdfObject pdfObject, PdfObjectID objectID)
    //    {
    //      if (pdfObject == null)
    //        thr ow new ArgumentNullException("pdfObject");
    //      if (pdfObject.Document == null)
    //        th row new ArgumentException(PSSR.OwningDocumentRequired, "pdfObject");
    //
    //      Type type = pdfObject.GetType();
    //      PdfParser parser = CreateParser(pdfObject.Document, type);
    //      return parser.ReadObject(pdfObject, objectID, false);
    //    }

    /// <summary>
    /// Reads an object from the PDF input stream using the default parser.
    /// </summary>
    public static PdfObject ReadObject(PdfDocument owner, PdfObjectID objectID)
    {
        ArgumentNullException.ThrowIfNull(owner);

        Parser parser = new Parser(owner);
        return parser.ReadObject(null, objectID, false, false);
    }

    /// <summary>
    /// Reads the irefs from the compressed object with the specified index in the object stream
    /// of the object with the specified object id.
    /// </summary>
    internal void ReadIRefsFromCompressedObject(PdfObjectID objectID)
    {
        Debug.Assert(_document._irefTable.Contains(objectID));
        PdfReference iref = _document._irefTable[objectID];
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
                PdfDictionary pdfObject = (PdfDictionary)ReadObject(null, iref.ObjectID, false, false);
                PdfObjectStream objectStream = new PdfObjectStream(pdfObject);
                Debug.Assert(objectStream.Reference == iref);
                // objectStream.Reference = iref; Superfluous, see Assert in line before.
                Debug.Assert(objectStream.Reference.Value != null, "Something went wrong.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        Debug.Assert(iref.Value != null);

        PdfObjectStream objectStreamStream = iref.Value as PdfObjectStream;
        if (objectStreamStream == null)
        {
            Debug.Assert(((PdfDictionary)iref.Value).Elements.GetName("/Type") == "/ObjStm");

            objectStreamStream = new PdfObjectStream((PdfDictionary)iref.Value);
            Debug.Assert(objectStreamStream.Reference == iref);
            // objectStream.Reference = iref; Superfluous, see Assert in line before.
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
        PdfReference iref = _document._irefTable[objectID];
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
                PdfDictionary pdfObject = (PdfDictionary)ReadObject(null, iref.ObjectID, false, false);
                PdfObjectStream objectStream = new PdfObjectStream(pdfObject);
                Debug.Assert(objectStream.Reference == iref);
                // objectStream.Reference = iref; Superfluous, see Assert in line before.
                Debug.Assert(objectStream.Reference.Value != null, "Something went wrong.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                throw;
            }
        }

        Debug.Assert(iref.Value != null);

        PdfObjectStream objectStreamStream = iref.Value as PdfObjectStream;
        if (objectStreamStream == null)
        {
            Debug.Assert(((PdfDictionary)iref.Value).Elements.GetName("/Type") == "/ObjStm");

            objectStreamStream = new PdfObjectStream((PdfDictionary)iref.Value);
            Debug.Assert(objectStreamStream.Reference == iref);
            // objectStream.Reference = iref; Superfluous, see Assert in line before.
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
        PdfObjectID objectID = new PdfObjectID(objectNumber);
        _lexer.Position = offset;
        PdfObject obj = ReadObject(null, objectID, false, true);

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
        // TODO: Concept for general error  handling.
        // If the stream is corrupted a lot of things can go wrong here.
        // Make it sense to do a more detailed error checking?

        // Create n pairs of integers with object number and offset.
        int[][] header = new int[n][];
        for (int idx = 0; idx < n; idx++)
        {
            int number = ReadInteger();
            int offset = ReadInteger() + first; // Calculate absolute offset.
            header[idx] = new int[] { number, offset };
        }

        return header;
    }

    /// <summary>
    /// Reads the cross-reference table(s) and their trailer dictionary or
    /// cross-reference streams.
    /// </summary>
    internal PdfTrailer ReadTrailer(PdfReadAccuracy accuracy)
    {
        var length = _lexer.PdfLength;

        // Implementation note 18 Appendix  H:
        // Acrobat viewers require only that the %%EOF marker appear somewhere within the last 1024 bytes of the file.
        int idx;
        if (length <= 1030)
        {
            // Reading the final 30 bytes should work for all files. But often it does not.
            string trail = _lexer.ReadRawString(length - 31, 30); //lexer.Pdf.Substring(length - 30);
            idx = trail.LastIndexOf("startxref", StringComparison.Ordinal);
            _lexer.Position = length - 31 + idx;
        }
        else
        {
            // For larger files we read 1 kiB - in most cases we find "startxref" in that range.
            string trail = _lexer.ReadRawString(length - 1031, 1030);
            idx = trail.LastIndexOf("startxref", StringComparison.Ordinal);
            _lexer.Position = length - 1031 + idx;
        }

        // SAP sometimes creates files with a size of several MByte and place "startxref" somewhere in the middle...
        if (idx == -1)
        {
            // If "startxref" was still not found yet, read the file completely.
            if (length > int.MaxValue)
                //TODO: Implement chunking to read long files.
                throw new NotImplementedException(
                    "Reading >2GB files with a 'startxref' in the middle not implemented.");
            var trail = _lexer.ReadRawString(0, (int)length);
            idx = trail.LastIndexOf("startxref", StringComparison.Ordinal);
            _lexer.Position = idx;
        }

        if (idx == -1)
            throw new Exception("The StartXRef table could not be found, the file cannot be opened.");

        ReadSymbol(Symbol.StartXRef);
        _lexer.Position = ReadLong();

        // Read all trailers. The one to keep is decided here and handed back; the caller is what
        // puts it on the document, so that reading a trailer writes nothing behind its back.
        PdfTrailer firstTrailer = null;
        while (true)
        {
            PdfTrailer trailer = ReadXRefTableAndTrailer(_document._irefTable, accuracy);

            // 1st trailer seems to be the best.
            if (firstTrailer == null)
                firstTrailer = trailer;
            int prev = trailer != null ? trailer.Elements.GetInteger(PdfTrailer.Keys.Prev) : 0;
            if (prev == 0)
                break;

            //if (prev > lexer.PdfLength)
            //  break;
            _lexer.Position = prev;
        }

        return firstTrailer;
    }

    /// <summary>
    /// Reads cross reference table(s) and trailer(s).
    /// </summary>
    private PdfTrailer ReadXRefTableAndTrailer(PdfCrossReferenceTable xrefTable, PdfReadAccuracy accuracy)
    {
        Debug.Assert(xrefTable != null);

        Symbol symbol = ScanNextToken();

        if (symbol == Symbol.XRef) // Is it a cross-reference table?
        {
            // Reference: 3.4.3  Cross-Reference Table / Page 93
            while (true)
            {
                symbol = ScanNextToken();
                if (symbol == Symbol.Integer)
                {
                    int start = _lexer.TokenToInteger;
                    int length = ReadInteger();
                    for (int id = start; id < start + length; id++)
                    {
                        var position = ReadLong();
                        int generation = ReadInteger();
                        ReadSymbol(Symbol.Keyword);
                        string token = _lexer.Token;

                        // Skip start entry
                        if (id == 0)
                            continue;

                        // Skip unused entries.
                        if (token != "n")
                            continue;

                        //!!!new 2018-03-14 begin
                        // Check if the object at the address has the correct ID and generation.
                        int idToUse = id;
                        int idChecked, generationChecked;
                        if (!CheckXRefTableEntry(position, id, generation, out idChecked, out generationChecked))
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
                        PdfObjectID objectID = new PdfObjectID(idToUse, generation);

                        // Ignore the latter one.
                        if (xrefTable.Contains(objectID))
                            continue;
                        xrefTable.Add(new PdfReference(objectID, position));
                    }
                }
                else if (symbol == Symbol.Trailer)
                {
                    ReadSymbol(Symbol.BeginDictionary);
                    PdfTrailer trailer = new PdfTrailer(_document);
                    ReadDictionary(trailer, false);
                    return trailer;
                }
                else
                    ParserDiagnostics.HandleUnexpectedToken(_lexer.Token);
            }
        }
        // ReSharper disable once RedundantIfElseBlock because of code readability.
        else if (symbol == Symbol.Integer) // Is it an cross-reference stream?
        {
            // Reference: 3.4.7  Cross-Reference Streams / Page 93
            // TODO: Handle PDF files larger than 2 GiB, see implementation note 21 in Appendix H.

            // The parsed integer is the object id of the cross-refernece stream.
            return ReadXRefStream(xrefTable);
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
            //// TODO Should we use ScanKeyword here?
            //ReadKSymbol(Symbol.Keyword);
            //string token = _lexer.Token;
            Symbol symbol = _lexer.ScanNextToken();
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
    /// Reads cross reference stream(s).
    /// </summary>
    private PdfCrossReferenceStream ReadXRefStream(PdfCrossReferenceTable xrefTable)
    {
        // Read cross reference stream.
        //Debug.Assert(_lexer.Symbol == Symbol.Integer);

        int number = _lexer.TokenToInteger;
        int generation = ReadInteger();
        Debug.Assert(generation == 0);

        ReadSymbol(Symbol.Obj);
        ReadSymbol(Symbol.BeginDictionary);
        PdfObjectID objectID = new PdfObjectID(number, generation);

        PdfCrossReferenceStream xrefStream = new PdfCrossReferenceStream(_document);

        ReadDictionary(xrefStream, false);
        ReadSymbol(Symbol.BeginStream);
        ReadStream(xrefStream);

        // An additional cross-reference (/Prev) could have been referenced in the first cross-reference by position.
        // That goes into item.Type == 1 below and adds its objectID into the xrefTable with just a position and no value.
        // Then we can't just do `xrefTable.Add(iref)` because that is a no-op when the objectID is already present in the ObjectTable.
        // Making sure that the iref.Value is set here correctly ensure that the mechanisms in PdfReader will work correctly:
        // 1. It needs to find a PdfCrossReferenceStream in iref.Value in order to resolve compressed objects.
        // 2. If we leave null in iref.Value it would do redundant parsing to resolve the value again.
        PdfReference iref = xrefTable[objectID];
        if (iref != null)
        {
            if (iref.Value == null)
            {
                iref.Value = xrefStream;
            }
        }
        else
        {
            iref = new PdfReference(xrefStream);
            iref.ObjectID = objectID;
            iref.Value = xrefStream;
            xrefTable.Add(iref);
        }

        Debug.Assert(xrefStream.Stream != null);
        var bytes = xrefStream.Stream.UnfilteredValue;

        int size = xrefStream.Elements.GetInteger(PdfCrossReferenceStream.Keys.Size);
        PdfArray index = xrefStream.Elements.GetValue(PdfCrossReferenceStream.Keys.Index) as PdfArray;
        int prev = xrefStream.Elements.GetInteger(PdfCrossReferenceStream.Keys.Prev);
        PdfArray w = (PdfArray)xrefStream.Elements.GetValue(PdfCrossReferenceStream.Keys.W);

        // E.g.: W[1 2 1] ¤ Index[7 12] ¤ Size 19

        // Setup subsections.
        int subsectionCount;
        int[][] subsections = null;
        int subsectionEntryCount = 0;
        if (index == null)
        {
            // Setup with default values.
            subsectionCount = 1;
            subsections = new int[subsectionCount][];
            subsections[0] = new int[] { 0, size }; // HACK: What is size? Contratiction in PDF reference.
            subsectionEntryCount = size;
        }
        else
        {
            // Read subsections from array.
            Debug.Assert(index.Elements.Count % 2 == 0);
            subsectionCount = index.Elements.Count / 2;
            subsections = new int[subsectionCount][];
            for (int idx = 0; idx < subsectionCount; idx++)
            {
                subsections[idx] = new int[]
                    { index.Elements.GetInteger(2 * idx), index.Elements.GetInteger(2 * idx + 1) };
                subsectionEntryCount += subsections[idx][1];
            }
        }

        // W key.
        Debug.Assert(w.Elements.Count == 3);
        int[] wsize = { w.Elements.GetInteger(0), w.Elements.GetInteger(1), w.Elements.GetInteger(2) };
        int wsum = StreamHelper.WSize(wsize);
        Debug.Assert(wsum * subsectionEntryCount == bytes.Length, "Check implementation here.");
        int testcount = subsections[0][1];
        int[] currentSubsection = subsections[0];

        int index2 = -1;
        for (int ssc = 0; ssc < subsectionCount; ssc++)
        {
            int abc = subsections[ssc][1];
            for (int idx = 0; idx < abc; idx++)
            {
                index2++;

                PdfCrossReferenceStream.CrossReferenceStreamEntry item =
                    new PdfCrossReferenceStream.CrossReferenceStreamEntry();

                item.Type = StreamHelper.ReadBytes(bytes, index2 * wsum, wsize[0]);
                item.Field2 = StreamHelper.ReadBytes(bytes, index2 * wsum + wsize[0], wsize[1]);
                item.Field3 = StreamHelper.ReadBytes(bytes, index2 * wsum + wsize[0] + wsize[1], wsize[2]);

                xrefStream.Entries.Add(item);

                switch (item.Type)
                {
                    case 0:
                        // Nothing to do, not needed.
                        break;

                    case 1: // offset / generation number
                        //// Even it is restricted, an object can exists in more than one subsection.
                        //// (PDF Reference Implementation Notes 15).

                        int position = (int)item.Field2;
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
                        // Nothing to do yet.
                        break;
                }
            }
        }

        return xrefStream;
    }

    /// <summary>
    /// Parses a PDF date string.
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
    internal static DateTime ParseDateTime(string date, DateTime errorValue) // TODO: TryParseDateTime
    {
        DateTime datetime = errorValue;
        try
        {
            if (date.StartsWith("D:"))
            {
                // D:YYYYMMDDHHmmSSOHH'mm'
                //   ^2      ^10   ^16 ^20
                int length = date.Length;
                int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0, hh = 0, mm = 0;
                char o = 'Z';
                if (length >= 10)
                {
                    year = int.Parse(date.Substring(2, 4));
                    month = int.Parse(date.Substring(6, 2));
                    day = int.Parse(date.Substring(8, 2));
                    if (length >= 16)
                    {
                        hour = int.Parse(date.Substring(10, 2));
                        minute = int.Parse(date.Substring(12, 2));
                        second = int.Parse(date.Substring(14, 2));
                        if (length >= 23)
                        {
                            if ((o = date[16]) != 'Z')
                            {
                                hh = int.Parse(date.Substring(17, 2));
                                mm = int.Parse(date.Substring(20, 2));
                            }
                        }
                    }
                }

                // There are miserable PDF tools around the world.
                month = Math.Min(Math.Max(month, 1), 12);
                datetime = new DateTime(year, month, day, hour, minute, second);
                if (o != 'Z')
                {
                    TimeSpan ts = new TimeSpan(hh, mm, 0);
                    if (o == '-')
                        datetime = datetime.Add(ts);
                    else
                        datetime = datetime.Subtract(ts);
                }

                // Now that we converted datetime to UTC, mark it as UTC.
                datetime = DateTime.SpecifyKind(datetime, DateTimeKind.Utc);
            }
            else
            {
                // Some libraries use plain English format.
                datetime = DateTime.Parse(date, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // A date that will not parse is left as the default rather than failing the read: a
            // malformed /CreationDate is not a reason to refuse the document. The assertion gives
            // a hint in a DEBUG build and costs nothing in a Release one.
            Debug.Assert(false, ex.Message);
        }

        return datetime;
    }

    //    /// <summary>
    //    /// Creates a parser for the specified PDF object type. A PDF object can define a specialized
    //    /// parser in the optional PdfObjectInfoAttribute. If no parser is specified, the default
    //    /// Parser object is returned.
    //    /// </summary>
    //    public static Parser CreateParser(PdfDocument document, Type pdfObjectType)
    //    {
    //      // TODO: ParserFactory
    //      object[] attribs = null; //pdfObjectType.GetCustomAttributes(typeof(PdfObjectInfoAttribute), false);
    //      if (attribs.Length == 1)
    //      {
    //        PdfObjectInfoAttribute attrib = null; //(PdfObjectInfoAttribute)attribs[0];
    //        Type parserType = attrib.Parser;
    //        if (parserType != null)
    //        {
    //          ConstructorInfo ctorInfo = parserType.GetConstructor(
    //            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
    //            new Type[]{typeof(PdfDocument)}, null);
    //          Parser parser = (Parser)ctorInfo.Invoke(new object[]{document});
    //          Debug.Assert(parser != null, "Creation of parser failed.");
    //          return parser;
    //        }
    //      }
    //      return new Parser(document);
    //    }

    /*
        /// <summary>
        /// Reads a date value directly or (optionally) indirectly from the PDF data stream.
        /// </summary>
        protected DateTime ReadDate(bool canBeIndirect)
        {
          Symbol symbol = lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.String)
          {
            // D:YYYYMMDDHHmmSSOHH'mm'
            //   ^2      ^10   ^16 ^20
            string date = lexer.Token;
            int length = date.Length;
            int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0, hh = 0, mm = 0;
            char o = 'Z';
            if (length >= 10)
            {
              year = Int32.Parse(date.Substring(2, 4));
              month = Int32.Parse(date.Substring(6, 2));
              day = Int32.Parse(date.Substring(8, 2));
              if (length >= 16)
              {
                hour = Int32.Parse(date.Substring(10, 2));
                minute = Int32.Parse(date.Substring(12, 2));
                second = Int32.Parse(date.Substring(14, 2));
                if (length >= 23)
                {
                  if ((o = date[16]) != 'Z')
                  {
                    hh = Int32.Parse(date.Substring(17, 2));
                    mm = Int32.Parse(date.Substring(20, 2));
                  }
                }
              }
            }
            DateTime datetime = new DateTime(year, month, day, hour, minute, second);
            if (o != 'Z')
            {
              TimeSpan ts = new TimeSpan(hh, mm, 0);
              if (o == '+')
                datetime.Add(ts);
              else
                datetime.Subtract(ts);
            }
            return datetime;
          }
          else if (symbol == Symbol.R)
          {
            int position = lexer.Position;
            MoveToObject(lexer.Token);
            ReadObjectID(null);
            DateTime d = ReadDate();
            ReadSymbol(Symbol.EndObj);
            lexer.Position = position;
            return d;
          }
          thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        protected DateTime ReadDate()
        {
          return ReadDate(false);
        }

        /// <summary>
        /// Reads a PdfRectangle value directly or (optionally) indirectly from the PDF data stream.
        /// </summary>
        protected PdfRectangle ReadRectangle(bool canBeIndirect)
        {
          Symbol symbol = lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.BeginArray)
          {
            PdfRectangle rect = new PdfRectangle();
            rect.X1 = ReadReal();
            rect.Y1 = ReadReal();
            rect.X2 = ReadReal();
            rect.Y2 = ReadReal();
            ReadSymbol(Symbol.EndArray);
            return rect;
          }
          else if (symbol == Symbol.R)
          {
            int position = lexer.Position;
            MoveToObject(lexer.Token);
            ReadObjectID(null);
            PdfRectangle rect = ReadRectangle();
            ReadSymbol(Symbol.EndObj);
            lexer.Position = position;
            return rect;
          }
          thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        /// <summary>
        /// Short cut for ReadRectangle(false).
        /// </summary>
        protected PdfRectangle ReadRectangle()
        {
          return ReadRectangle(false);
        }

        /// <summary>
        /// Reads a generic dictionary.
        /// </summary>
        protected PdfDictionary ReadDictionary(bool canBeIndirect)
        {
          // Just read over dictionary
          PdfDictionary dictionary = new PdfDictionary();
          Symbol symbol = lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.BeginDictionary)
          {
            int nestingLevel = 0;
            symbol = ScanNextToken();
            while (symbol != Symbol.Eof)
            {
              switch (symbol)
              {
                case Symbol.BeginDictionary:
                  nestingLevel++;
                  break;

                case Symbol.EndDictionary:
                  if (nestingLevel == 0)
                    return dictionary;
                  else
                    nestingLevel--;
                  break;
              }
              symbol = ScanNextToken();
            }
            Debug.Assert(false, "Must not come here");
            return dictionary;
          }
          else if (symbol == Symbol.R)
          {
            return dictionary;
          }
          thr ow new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        /// <summary>
        /// Short cut for ReadDictionary(false).
        /// </summary>
        protected PdfDictionary ReadDictionary()
        {
          return ReadDictionary(false);
        }

        /// <summary>
        /// Reads a generic array.
        /// </summary>
        protected PdfArray ReadArray(bool canBeIndirect)
        {
          // Just read over array
          PdfArray array = new PdfArray();
          Symbol symbol = lexer.ScanNextToken(canBeIndirect);
          if (symbol == Symbol.BeginArray)
          {
            int nestingLevel = 0;
            symbol = ScanNextToken();
            while (symbol != Symbol.Eof)
            {
              switch (symbol)
              {
                case Symbol.BeginArray:
                  nestingLevel++;
                  break;

                case Symbol.EndArray:
                  if (nestingLevel == 0)
                    return array;
                  else
                    nestingLevel--;
                  break;
              }
              symbol = ScanNextToken();
            }
            Debug.Assert(false, "Must not come here");
            return array;
          }
          else if (symbol == Symbol.R)
          {
            return array;
          }
          th row new PdfReaderException(PSSR.UnexpectedToken(lexer.Token));
        }

        protected PdfArray ReadArray()
        {
          return ReadArray(false);
        }

        protected object ReadGeneric(KeysMeta meta, string token)
        {
          KeyDescriptor descriptor =  meta[token];
          Debug.Assert(descriptor != null);
          object result = null;
          switch (descriptor.KeyType & KeyType.TypeMask)
          {
            case KeyType.Name:
              result = ReadName();
              break;

            case KeyType.String:
              result = ReadString(descriptor.CanBeIndirect);
              break;

            case KeyType.Boolean:
              result = ReadBoolean(descriptor.CanBeIndirect);
              break;

            case KeyType.Integer:
              result = ReadInteger(descriptor.CanBeIndirect);
              break;

            case KeyType.Real:
              result = ReadReal(descriptor.CanBeIndirect);
              break;

            case KeyType.Date:
              result = ReadDate(descriptor.CanBeIndirect);
              break;

            case KeyType.Rectangle:
              result = ReadRectangle(descriptor.CanBeIndirect);
              break;

            case KeyType.Array:
              result = ReadArray(descriptor.CanBeIndirect);
              break;

            case KeyType.Dictionary:
              result = ReadDictionary(descriptor.CanBeIndirect);
              break;

            case KeyType.Stream:
              break;

            case KeyType.NumberTree:
              thr ow new NotImplementedException("KeyType.NumberTree");

            case KeyType.NameOrArray:
              char ch = lexer.MoveToNonWhiteSpace();
              if (ch == '/')
                result = ReadName();
              else if (ch == '[')
                result = ReadArray();
              else
                th row new NotImplementedException("KeyType.NameOrArray");
              break;

            case KeyType.ArrayOrDictionary:
              thr ow new NotImplementedException("KeyType.ArrayOrDictionary");
          }
          //Debug.Assert(false, "ReadGeneric");
          return result;
        }

        //    /// <summary>
        //    /// Gets the current symbol from the lexer.
        //    /// </summary>
        //    protected Symbol Symbol
        //    {
        //      get {return lexer.Symbol;}
        //    }
        //
        //    /// <summary>
        //    /// Gets the current token from the lexer.
        //    /// </summary>
        //    protected string Token
        //    {
        //      get {return lexer.Token.ToString();}
        //    }

        public static object Read(PdfObject o, string key)
        {
          return null;
        }
    */

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

    public static uint ReadBytes(byte[] bytes, int index, int byteCount)
    {
        uint value = 0;
        for (int idx = 0; idx < byteCount; idx++)
        {
            value *= 256;
            value += bytes[index + idx];
        }

        return value;
    }
}
