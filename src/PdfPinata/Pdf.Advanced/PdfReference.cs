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
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents an indirect reference to a PdfObject.
/// </summary>
[DebuggerDisplay("iref({ObjectNumber}, {GenerationNumber})")]
public sealed class PdfReference : PdfItem
{
    // About PdfReference
    //
    // * A PdfReference holds either the ObjectID or the PdfObject or both.
    //
    // * Each PdfObject has a PdfReference if and only if it is an indirect object. Direct objects have
    //   no PdfReference, because they are embedded in a parent objects.
    //
    // * PdfReference objects are used to reference PdfObject instances. A value in a PDF dictionary
    //   or array that is a PdfReference represents an indirect reference. A value in a PDF dictionary or
    //   or array that is a PdfObject represents a direct (or embeddded) object.
    //
    // * When a PDF file is imported, the PdfXRefTable is filled with PdfReference objects keeping the
    //   ObjectsIDs and file positions (offsets) of all indirect objects.
    //
    // * Indirect objects can easily be renumbered because they do not rely on their ObjectsIDs.
    //
    // * During modification of a document the ObjectID of an indirect object has no meaning,
    //   except that they must be different in pairs.

    /// <summary>
    /// Initializes a new PdfReference instance for the specified indirect object.
    /// </summary>
    public PdfReference(PdfObject pdfObject)
    {
        if (pdfObject.Reference != null)
            throw new InvalidOperationException("Must not create iref for an object that already has one.");
        _value = pdfObject;
        pdfObject.Reference = this;
    }

    /// <summary>
    /// Initializes a new PdfReference instance from the specified object identifier and file position.
    /// </summary>
    public PdfReference(PdfObjectID objectId, long position)
    {
        _objectID = objectId;
        _position = position;
    }

    /// <summary>
    /// Writes the object in PDF iref table format.
    /// </summary>
    internal void WriteXRefEnty(PdfWriter writer)
    {
        // Each line must be exactly 20 bytes long, otherwise Acrobat repairs the file.
        var text = $"{_position:0000000000} {_objectID.GenerationNumber:00000} n\n"; // InUse ? 'n' : 'f');
        writer.WriteRaw(text);
    }

    /// <summary>
    /// Writes an indirect reference.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        writer.Write(this);
    }

    /// <summary>
    /// Gets or sets the object identifier.
    /// </summary>
    public PdfObjectID ObjectID
    {
        get => _objectID;
        set => _objectID = value;
    }
    private PdfObjectID _objectID;

    /// <summary>
    /// Gets the object number of the object identifier.
    /// </summary>
    public int ObjectNumber => _objectID.ObjectNumber;

    /// <summary>
    /// Gets the generation number of the object identifier.
    /// </summary>
    public int GenerationNumber => _objectID.GenerationNumber;

    /// <summary>
    /// Gets or sets the file position of the related PdfObject.
    /// </summary>
    public long Position
    {
        get => _position;
        set => _position = value;
    }
    private long _position;

    /// <summary>
    /// Gets or sets the referenced PdfObject.
    /// </summary>
    public PdfObject Value
    {
        get => _value;
        set
        {
            Debug.Assert(value != null, "The value of a PdfReference must never be null.");
            Debug.Assert(value.Reference == null || ReferenceEquals(value.Reference, this), "The reference of the value must be null or this.");
            _value = value;
            // value must never be null
            value.Reference = this;
        }
    }
    private PdfObject _value;

    /// <summary>
    /// Hack for dead objects.
    /// </summary>
    internal void SetObject(PdfObject value)
    {
        _value = value;
    }

    /// <summary>
    /// Gets or sets the document this object belongs to.
    /// </summary>
    public PdfDocument Document { get; set; }

    /// <summary>
    /// Gets a string representing the object identifier.
    /// </summary>
    public override string ToString()
    {
        return _objectID + " R";
    }

    internal static PdfReferenceComparer Comparer => new();

    /// <summary>
    /// Implements a comparer that compares PdfReference objects by their PdfObjectID.
    /// </summary>
    internal class PdfReferenceComparer : IComparer<PdfReference>
    {
        public int Compare(PdfReference x, PdfReference y)
        {
            var l = x;
            var r = y;
            if (l != null)
            {
                if (r != null)
                    return l._objectID.CompareTo(r._objectID);
                return -1;
            }
            return r != null ? 1 : 0;
        }
    }
}
