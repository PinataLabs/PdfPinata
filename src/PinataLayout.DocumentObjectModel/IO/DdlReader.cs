#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using System.IO;
using System.Text;

namespace PinataLayout.DocumentObjectModel.IO;

/// <summary>
/// Represents a reader that provides access to DDL data.
/// </summary>
public class DdlReader : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the DdlReader class with the specified Stream and ErrorManager2.
    /// </summary>
    public DdlReader(Stream stream, DdlReaderErrors errors = null)
    {
        _errorManager = errors;
        _reader = new StreamReader(stream);
    }

    /// <summary>
    /// Initializes a new instance of the DdlReader class with the specified filename and ErrorManager2.
    /// </summary>
    public DdlReader(string filename, DdlReaderErrors errors = null)
    {
        _fileName = filename;
        _errorManager = errors;
        _reader = new StreamReader(File.OpenRead(filename), Encoding.UTF8, false, 1028, false);
    }

    /// <summary>
    /// Initializes a new instance of the DdlReader class with the specified TextReader and ErrorManager2.
    /// </summary>
    public DdlReader(TextReader reader, DdlReaderErrors errors = null)
    {
        _errorManager = errors;
        _reader = reader;
    }

    /// <summary>
    /// Reads and returns a Document from a file or a DDL string.
    /// </summary>
    public Document ReadDocument()
    {
        var ddl = _reader.ReadToEnd();

        Document document;
        if (!string.IsNullOrEmpty(_fileName))
        {
            var parser = new DdlParser(_fileName, ddl, _errorManager);
            document = parser.ParseDocument(null);
            document.ddlFile = _fileName;
        }
        else
        {
            var parser = new DdlParser(ddl, _errorManager);
            document = parser.ParseDocument(null);
        }

        return document;
    }

    /// <summary>
    /// Reads and returns a DocumentObject from a file or a DDL string.
    /// </summary>
    private DocumentObject ReadObject()
    {
        var ddl = _reader.ReadToEnd();

        var parser = !string.IsNullOrEmpty(_fileName) ? new DdlParser(_fileName, ddl, _errorManager) : new DdlParser(ddl, _errorManager);
        return parser.ParseDocumentObject();
    }

    /// <summary>
    /// Reads and returns a Document from the specified file.
    /// </summary>
    public static Document DocumentFromFile(string documentFileName) //, ErrorManager2 _errorManager)
    {
        using var reader = new DdlReader(documentFileName);
        return reader.ReadDocument();
    }

    /// <summary>
    /// Reads and returns a Document from the specified DDL string.
    /// </summary>
    public static Document DocumentFromString(string ddl)
    {
        using var stringReader = new StringReader(ddl);
        using var reader = new DdlReader(stringReader);
        return reader.ReadDocument();
    }

    /// <summary>
    /// Reads and returns a domain object from the specified file.
    /// </summary>
    public static DocumentObject ObjectFromFile(string documentFileName, DdlReaderErrors errors = null)
    {
        using var reader = new DdlReader(documentFileName, errors);
        return reader.ReadObject();
    }

    /// <summary>
    /// Reads and returns a domain object from the specified DDL string.
    /// </summary>
    public static DocumentObject ObjectFromString(string ddl, DdlReaderErrors errors = null)
    {
        using var stringReader = new StringReader(ddl);
        // With the errors: this overload used to build the reader without them, so the list
        // the caller passed in was never connected to the parser and came back empty however
        // wrong the DDL was. ObjectFromFile has always passed its own along.
        using var reader = new DdlReader(stringReader, errors);
        return reader.ReadObject();
    }

    /// <summary>Releases the underlying reader.</summary>
    public void Dispose()
    {
        // Dispose of unmanaged resources.
        Dispose(true);
        // Suppress finalization.
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the underlying reader.</summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/> rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null;
        }
    }

    private TextReader _reader;
    private readonly DdlReaderErrors _errorManager;
    private readonly string _fileName;
}
