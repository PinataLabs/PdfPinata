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
using System.Collections;
using System.Collections.Generic;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents the cross-reference table of a PDF document.
/// It contains all indirect objects of a document.
/// </summary>
internal sealed class PdfCrossReferenceTable // Must not be derive from PdfObject.
{
    public PdfCrossReferenceTable(PdfDocument document)
    {
        _document = document;
    }

    readonly PdfDocument _document;

    /// <summary>
    /// Represents the relation between PdfObjectID and PdfReference for a PdfDocument.
    /// </summary>
    public Dictionary<PdfObjectID, PdfReference> ObjectTable = new();

    internal bool IsUnderConstruction
    {
        get => _isUnderConstruction;
        set => _isUnderConstruction = value;
    }

    bool _isUnderConstruction;

    /// <summary>
    /// Adds a cross-reference entry to the table. Used when parsing the trailer.
    /// </summary>
    public void Add(PdfReference iref)
    {
        if (iref.ObjectID.IsEmpty)
            iref.ObjectID = new PdfObjectID(GetNewObjectNumber());

        if (ObjectTable.ContainsKey(iref.ObjectID))
            return;
        ObjectTable.Add(iref.ObjectID, iref);
    }

    /// <summary>
    /// Adds a PdfObject to the table.
    /// </summary>
    public void Add(PdfObject value)
    {
        if (value.Owner == null)
            value.Document = _document;
        else
            Debug.Assert(value.Owner == _document);

        if (value.ObjectID.IsEmpty)
            value.SetObjectID(GetNewObjectNumber(), 0);

        if (ObjectTable.ContainsKey(value.ObjectID))
            return;
        ObjectTable.Add(value.ObjectID, value.Reference);
    }

    public void Remove(PdfReference iref)
    {
        ObjectTable.Remove(iref.ObjectID);
    }

    /// <summary>
    /// Gets a cross-reference entry from an object identifier.
    /// Returns null if no object with the specified ID exists in the object table.
    /// </summary>
    public PdfReference this[PdfObjectID objectID]
    {
        get
        {
            ObjectTable.TryGetValue(objectID, out var iref);
            return iref;
        }
    }

    /// <summary>
    /// Indicates whether the specified object identifier is in the table.
    /// </summary>
    public bool Contains(PdfObjectID objectID)
    {
        return ObjectTable.ContainsKey(objectID);
    }

    /// <summary>
    /// Returns the next free object number.
    /// </summary>
    public int GetNewObjectNumber()
    {
        // New objects are numbered consecutively. If a document is imported, maxObjectNumber is
        // set to the highest object number used in the document.
        return ++MaxObjectNumber;
    }

    internal int MaxObjectNumber;

    /// <summary>
    /// Writes the xref section in pdf stream.
    /// </summary>
    internal void WriteObject(PdfWriter writer)
    {
        writer.WriteRaw("xref\n");

        var irefs = AllReferences;

        var count = irefs.Length;
        writer.WriteRaw($"0 {count + 1}\n");
        writer.WriteRaw($"{0:0000000000} {65535:00000} {"f"} \n");
        //PdfEncoders.WriteAnsi(stream, text);

        for (var idx = 0; idx < count; idx++)
        {
            var iref = irefs[idx];

            // Acrobat is very pedantic; it must be exactly 20 bytes per line.
            writer.WriteRaw($"{iref.Position:0000000000} {iref.GenerationNumber:00000} {"n"} \n");
        }
    }

    /// <summary>
    /// Gets an array of all object identifiers. For debugging purposes only.
    /// </summary>
    internal PdfObjectID[] AllObjectIDs
    {
        get
        {
            ICollection collection = ObjectTable.Keys;
            var objectIDs = new PdfObjectID[collection.Count];
            collection.CopyTo(objectIDs, 0);
            return objectIDs;
        }
    }

    /// <summary>
    /// Gets an array of all cross-references ordered ascendingly by their object identifier.
    /// </summary>
    internal PdfReference[] AllReferences
    {
        get
        {
            var collection = ObjectTable.Values;
            var list = new List<PdfReference>(collection);
            list.Sort(PdfReference.Comparer);
            var irefs = new PdfReference[collection.Count];
            list.CopyTo(irefs, 0);
            return irefs;
        }
    }

    internal static void HandleOrphanedReferences()
    {
    }

    /// <summary>
    /// Removes all objects that cannot be reached from the trailer.
    /// Returns the number of removed objects.
    /// </summary>
    internal int Compact()
    {
        // TODO: remove PdfBooleanObject, PdfIntegerObject etc.
        var removed = ObjectTable.Count;
        // TODO: Is this really so easy?
        var irefs = TransitiveClosure(_document._trailer);

        foreach (var iref in irefs)
        {
            Debug.Assert(ObjectTable.ContainsKey(iref.ObjectID));
            Debug.Assert(iref.Value != null);
        }

        MaxObjectNumber = 0;
        ObjectTable.Clear();
        foreach (var iref in irefs)
        {
            ObjectTable.Add(iref.ObjectID, iref);
            MaxObjectNumber = Math.Max(MaxObjectNumber, iref.ObjectNumber);
        }

        removed -= ObjectTable.Count;
        return removed;
    }

    /// <summary>
    /// Renumbers the objects starting at 1.
    /// </summary>
    internal void Renumber()
    {
        var irefs = AllReferences;
        ObjectTable.Clear();
        // Give all objects a new number.
        var count = irefs.Length;
        for (var idx = 0; idx < count; idx++)
        {
            var iref = irefs[idx];
            iref.ObjectID = new PdfObjectID(idx + 1);
            // Rehash with new number.
            ObjectTable.Add(iref.ObjectID, iref);
        }

        MaxObjectNumber = count;
    }

    /// <summary>
    /// Calculates the transitive closure of the specified PdfObject, i.e. all indirect objects
    /// recursively reachable from the specified object.
    /// </summary>
    public PdfReference[] TransitiveClosure(PdfObject pdfObject)
    {
        return TransitiveClosure(pdfObject, short.MaxValue);
    }

    /// <summary>
    /// Calculates the transitive closure of the specified PdfObject with the specified depth, i.e. all indirect objects
    /// recursively reachable from the specified object in up to maximally depth steps.
    /// </summary>
    public PdfReference[] TransitiveClosure(PdfObject pdfObject, int depth)
    {
        var objects = new Dictionary<PdfItem, object>();
        _overflow = new Dictionary<PdfItem, object>();
        TransitiveClosureImplementation(objects, pdfObject);
        TryAgain:
        if (_overflow.Count > 0)
        {
            var array = new PdfObject[_overflow.Count];
            _overflow.Keys.CopyTo(array, 0);
            _overflow = new Dictionary<PdfItem, object>();
            for (var idx = 0; idx < array.Length; idx++)
            {
                var obj = array[idx];
                TransitiveClosureImplementation(objects, obj);
            }

            goto TryAgain;
        }

        ICollection collection = objects.Keys;
        var count = collection.Count;
        var irefs = new PdfReference[count];
        collection.CopyTo(irefs, 0);
        return irefs;
    }

    static int _nestingLevel;
    Dictionary<PdfItem, object> _overflow = new();

    void TransitiveClosureImplementation(Dictionary<PdfItem, object> objects,
        PdfObject pdfObject /*, ref int depth*/)
    {
        try
        {
            _nestingLevel++;
            if (_nestingLevel >= 1000)
            {
                if (!_overflow.ContainsKey(pdfObject))
                    _overflow.Add(pdfObject, null);
                return;
            }

            IEnumerable enumerable = null; //(IEnumerator)pdfObject;
            PdfDictionary dict;
            PdfArray array;
            if ((dict = pdfObject as PdfDictionary) != null)
                enumerable = dict.Elements.Values;
            else if ((array = pdfObject as PdfArray) != null)
                enumerable = array.Elements;
            else
                Debug.Assert(false, "Should not come here.");

            if (enumerable != null)
            {
                foreach (PdfItem item in enumerable)
                {
                    if (item is PdfReference iref)
                    {
                        if (!ReferenceEquals(iref.Document, _document))
                            Debug.WriteLine($"Bad iref: {iref.ObjectID.ToString()}");

                        Debug.Assert(ReferenceEquals(iref.Document, _document) || iref.Document == null,
                            "External object detected!");
                        if (!objects.ContainsKey(iref))
                        {
                            var value = iref.Value;

                            // Ignore unreachable objets.
                            if (iref.Document != null)
                            {
                                // ... from trailer hack
                                if (value == null)
                                {
                                    iref = ObjectTable[iref.ObjectID];
                                    Debug.Assert(iref.Value != null);
                                    value = iref.Value;
                                }

                                Debug.Assert(ReferenceEquals(iref.Document, _document));
                                objects.Add(iref, null);
                                //Debug.WriteLine(String.Format("objects.Add('{0}', null);", iref.ObjectID.ToString()));
                                if (value is PdfArray || value is PdfDictionary)
                                    TransitiveClosureImplementation(objects, value /*, ref depth*/);
                            }
                            //else
                            //{
                            //  objects2.Add(this[iref.ObjectID], null);
                            //}
                        }
                    }
                    else
                    {
                        var pdfObject28 = item as PdfObject;
                        //if (pdfObject28 != null)
                        //  Debug.Assert(Object.ReferenceEquals(pdfObject28.Document, _document));
                        if (pdfObject28 != null && (pdfObject28 is PdfDictionary || pdfObject28 is PdfArray))
                            TransitiveClosureImplementation(objects, pdfObject28 /*, ref depth*/);
                    }
                }
            }
        }
        finally
        {
            _nestingLevel--;
        }
    }

    /// <summary>
    /// Gets the cross reference to an objects used for undefined indirect references.
    /// </summary>
    public PdfReference DeadObject
    {
        get
        {
            if (_deadObject == null)
            {
                _deadObject = new PdfDictionary(_document);
                Add(_deadObject);
                _deadObject.Elements.Add("/DeadObjectCount", new PdfInteger());
            }

            return _deadObject.Reference;
        }
    }

    PdfDictionary _deadObject;
}
