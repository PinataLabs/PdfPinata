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
using System.Diagnostics;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf;

/// <summary>
/// Base class of all composite PDF objects.
/// </summary>
public abstract class PdfObject : PdfItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class.
    /// </summary>
    protected PdfObject()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfObject"/> class.
    /// </summary>
    protected PdfObject(PdfDocument document)
    {
        // Calling a virtual member in a constructor is dangerous.
        // Document is overridden in PdfPage and the code is checked to be save
        // when called for a not completely initialized object.
        // ReSharper disable once VirtualMemberCallInConstructor
        Document = document;
    }

    /// <summary>
    /// Initializes a new instance from an existing object. Used for object type transformation.
    /// </summary>
    protected PdfObject(PdfObject obj)
        : this(obj.Owner)
    {
        // If the object that was transformed to an instance of a derived class was an indirect object
        // set the value of the reference to this.
        if (obj._iref != null)
            obj._iref.Value = this;

        // The object being transformed may already have been changed since it was read, and this
        // is what an incremental save asks from now on - a change forgotten here would be silently
        // left out of the appended revision.
        IsDirty = obj.IsDirty;
    }

    /// <summary>
    /// Creates a copy of this object. The clone does not belong to a document, i.e. its owner and its iref are null.
    /// </summary>
    public new PdfObject Clone()
    {
        return (PdfObject)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism. Must be overridden in derived classes.
    /// </summary>
    protected override object Copy()
    {
        var obj = (PdfObject)base.Copy();
        obj._document = null;
        obj._iref = null;
        return obj;
    }

    /// <summary>
    /// Sets the object and generation number.
    /// Setting the object identifier makes this object an indirect object, i.e. the object gets
    /// a PdfReference entry in the PdfCrossReferenceTable.
    /// </summary>
    internal void SetObjectID(int objectNumber, int generationNumber)
    {
        var objectID = new PdfObjectID(objectNumber, generationNumber);

        _iref ??= _document._irefTable[objectID];
        if (_iref == null)
        {
            // Called for its side effect: the constructor of PdfReference sets itself as this
            // object's reference.
            _ = new PdfReference(this);
            Debug.Assert(_iref != null);
            _iref.ObjectID = objectID;
        }
        _iref.Value = this;
        _iref.Document = _document;
    }

    /// <summary>
    /// Gets the PdfDocument this object belongs to.
    /// </summary>
    public virtual PdfDocument Owner => _document;

    /// <summary>
    /// Sets the PdfDocument this object belongs to.
    /// </summary>
    internal virtual PdfDocument Document
    {
        set
        {
            if (ReferenceEquals(_document, value))
                return;

            if (_document != null)
                throw new InvalidOperationException("Cannot change document.");
            _document = value;
            if (_iref != null)
                _iref.Document = value;
        }
    }
    internal PdfDocument _document;

    /// <summary>
    /// Indicates whether the object is an indirect object.
    /// </summary>
    public bool IsIndirect =>
        // An object is an indirect object if and only if is has an indirect reference value.
        _iref != null;

    /// <summary>
    /// Gets the PdfInternals object of this document, that grants access to some internal structures
    /// which are not part of the public interface of PdfDocument.
    /// </summary>
    public PdfObjectInternals Internals => _internals ?? (_internals = new PdfObjectInternals(this));

    private PdfObjectInternals _internals;

    /// <summary>
    /// When overridden in a derived class, prepares the object to get saved.
    /// </summary>
    internal virtual void PrepareForSave()
    { }

    /// <summary>
    /// Saves the stream position. 2nd Edition.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        Debug.Assert(false, "Must not come here!");
    }

    /// <summary>
    /// Gets the object identifier. Returns PdfObjectID.Empty for direct objects,
    /// i.e. never returns null.
    /// </summary>
    internal PdfObjectID ObjectID => _iref != null ? _iref.ObjectID : PdfObjectID.Empty;

    /// <summary>
    /// Gets the object number.
    /// </summary>
    internal int ObjectNumber => ObjectID.ObjectNumber;

    /// <summary>
    /// Gets the generation number.
    /// </summary>
    internal int GenerationNumber => ObjectID.GenerationNumber;

    ///// <summary>
    ///// Creates a deep copy of the specified value and its transitive closure and adds the
    ///// new objects to the specified owner document.
    ///// </summary>
    /// <param name="owner">The document that owns the cloned objects.</param>
    /// <param name="externalObject">The root object to be cloned.</param>
    /// <returns>The clone of the root object</returns>
    internal static PdfObject DeepCopyClosure(PdfDocument owner, PdfObject externalObject)
    {
        // Get transitive closure.
        var elements = externalObject.Owner.Internals.GetClosure(externalObject);
        var count = elements.Length;
        // 1st loop. Replace all objects by their clones.
        var iot = new PdfImportedObjectTable(owner, externalObject.Owner);
        for (var idx = 0; idx < count; idx++)
        {
            var obj = elements[idx];
            var clone = obj.Clone();
            Debug.Assert(clone.Reference == null);
            clone.Document = owner;
            if (obj.Reference != null)
            {
                // Case: The cloned object was an indirect object.
                // Add clone to new owner document.
                owner._irefTable.Add(clone);
                // The clone gets an iref by adding it to its new owner.
                Debug.Assert(clone.Reference != null);
                // Save an association from old object identifier to new iref.
                iot.Add(obj.ObjectID, clone.Reference);
            }
            else
            {
                // Case: The cloned object was an direct object.
                // Only the root object can be a direct object.
                Debug.Assert(idx == 0);
            }
            // Replace external object by its clone.
            elements[idx] = clone;
        }
        // 2nd loop. Fix up all indirect references that still refers to the import document.
        for (var idx = 0; idx < count; idx++)
        {
            var obj = elements[idx];
            Debug.Assert(obj.Owner == owner);
            FixUpObject(iot, owner, obj);
        }

        // Return the clone of the former root object.
        return elements[0];
    }

    ///// <summary>
    ///// Imports an object and its transitive closure to the specified document.
    ///// </summary>
    /// <param name="importedObjectTable">The imported object table of the owner for the external document.</param>
    /// <param name="owner">The document that owns the cloned objects.</param>
    /// <param name="externalObject">The root object to be cloned.</param>
    /// <returns>The clone of the root object</returns>
    internal static PdfObject ImportClosure(PdfImportedObjectTable importedObjectTable, PdfDocument owner, PdfObject externalObject)
    {
        Debug.Assert(ReferenceEquals(importedObjectTable.Owner, owner), "importedObjectTable does not belong to the owner.");
        Debug.Assert(ReferenceEquals(importedObjectTable.ExternalDocument, externalObject.Owner),
            "The ExternalDocument of the importedObjectTable does not belong to the owner of object to be imported.");

        // Get transitive closure of external object.
        var elements = externalObject.Owner.Internals.GetClosure(externalObject);
        var count = elements.Length;
        // 1st loop. Already imported objects are reused and new ones are cloned.
        for (var idx = 0; idx < count; idx++)
        {
            var obj = elements[idx];
            Debug.Assert(!ReferenceEquals(obj.Owner, owner));

            if (importedObjectTable.Contains(obj.ObjectID))
            {
                // Case: External object was already imported.
                var iref = importedObjectTable[obj.ObjectID];
                Debug.Assert(iref != null);
                Debug.Assert(iref.Value != null);
                Debug.Assert(iref.Document == owner);
                // Replace external object by the already cloned counterpart.
                elements[idx] = iref.Value;
            }
            else
            {
                // Case: External object was not yet imported ealier and must be cloned.
                var clone = obj.Clone();
                Debug.Assert(clone.Reference == null);
                clone.Document = owner;
                if (obj.Reference != null)
                {
                    // Case: The cloned object was an indirect object.
                    // Add clone to new owner document.
                    owner._irefTable.Add(clone);
                    Debug.Assert(clone.Reference != null);
                    // Save an association from old object identifier to new iref.
                    importedObjectTable.Add(obj.ObjectID, clone.Reference);
                }
                else
                {
                    // Case: The cloned object was a direct object.
                    // Only the root object can be a direct object.
                    Debug.Assert(idx == 0);
                }
                // Replace external object by its clone.
                elements[idx] = clone;
            }
        }
        // 2nd loop. Fix up indirect references that still refers to the external document.
        for (var idx = 0; idx < count; idx++)
        {
            var obj = elements[idx];
            Debug.Assert(owner != null);
            FixUpObject(importedObjectTable, importedObjectTable.Owner, obj);
        }

        // Return the imported root object.
        return elements[0];
    }

    /// <summary>
    /// Replace all indirect references to external objects by their cloned counterparts
    /// owned by the importer document.
    /// </summary>
    private static void FixUpObject(PdfImportedObjectTable iot, PdfDocument owner, PdfObject value)
    {
        Debug.Assert(ReferenceEquals(iot.Owner, owner));

        PdfDictionary dict;
        PdfArray array;
        if ((dict = value as PdfDictionary) != null)
        {
            // Case: The object is a dictionary.
            // Set document for cloned direct objects.
            if (dict.Owner == null)
            {
                // If the dictionary has not yet an owner set the owner to the importing document.
                dict.Document = owner;
            }
            else
            {
                // If the dictionary already has an owner it must be the importing document.
                Debug.Assert(dict.Owner == owner);
            }

            // Search for indirect references in all dictionary elements.
            var names = dict.Elements.KeyNames;
            foreach (var name in names)
            {
                var item = dict.Elements[name];
                Debug.Assert(item != null, "A dictionary element cannot be null.");

                // Is item an iref?
                var iref = item as PdfReference;
                if (iref != null)
                {
                    // Case: The item is a reference.
                    // Does the iref already belongs to the new owner?
                    if (iref.Document == owner)
                    {
                        // Yes: fine. Happens when an already cloned object is reused.
                        continue;
                    }

                    // No: Replace with iref of cloned object.
                    // iref.ObjectID is the object's number in the external document. Every indirect
                    // object of the transitive closure has been cloned into the owner, either by
                    // the first loop of DeepCopyClosure or ImportClosure or by an earlier import
                    // through the same table, and iot maps its external ID to the clone's entry in
                    // the owner's cross-reference table. The referenced object is part of that
                    // closure, so the lookup always finds it, and the reference it answers belongs
                    // to the owner, usually under a different number.
                    var newXRef = iot[iref.ObjectID];
                    Debug.Assert(newXRef != null);
                    Debug.Assert(newXRef.Document == owner);
                    dict.Elements[name] = newXRef;
                }
                else
                {
                    // Case: The item is not a reference.
                    // If item is an object recursively fix its inner items.
                    var pdfObject = item as PdfObject;
                    if (pdfObject != null)
                    {
                        // Fix up inner objects, i.e. recursively walk down the object tree.
                        FixUpObject(iot, owner, pdfObject);
                    }
                    // The item is something else, e.g. a name. Nothing to do.
                }
            }
        }
        else if ((array = value as PdfArray) != null)
        {
            // Case: The object is an array.
            // Set document for cloned direct objects.
            if (array.Owner == null)
            {
                // If the array has not yet an owner set the owner to the importing document.
                array.Document = owner;
            }
            else
            {
                // If the array already has an owner it must be the importing document.
                Debug.Assert(array.Owner == owner);
            }

            // Search for indirect references in all array elements.
            var count = array.Elements.Count;
            for (var idx = 0; idx < count; idx++)
            {
                var item = array.Elements[idx];
                Debug.Assert(item != null, "An array element cannot be null.");

                // Is item an iref?
                var iref = item as PdfReference;
                if (iref != null)
                {
                    // Case: The item is a reference.
                    // Does the iref already belongs to the owner?
                    if (iref.Document == owner)
                    {
                        // Yes: fine. Happens when an already cloned object is reused.
                        continue;
                    }

                    // No: replace with iref of cloned object.
                    Debug.Assert(iref.Document == iot.ExternalDocument);
                    var newXRef = iot[iref.ObjectID];
                    Debug.Assert(newXRef != null);
                    Debug.Assert(newXRef.Document == owner);
                    array.Elements[idx] = newXRef;
                }
                else
                {
                    // Case: The item is not a reference.
                    // If item is an object recursively fix its inner items.
                    var pdfObject = item as PdfObject;
                    if (pdfObject != null)
                    {
                        // Fix up inner objects, i.e. recursively walk down the object tree.
                        FixUpObject(iot, owner, pdfObject);
                    }
                    // The item is something else, e.g. a name. Nothing to do.
                }
            }
        }
        else
        {
            // Case: The item is some other indirect object.
            // Indirect integers, booleans, etc. are allowed, but PdfPinata do not create them.
            // If such objects occur in imported PDF files from other producers, nothing more is to do.
            // The owner was already set, which is double checked by the assertions below.
            // An indirect null is one of them: a writer that puts /SMask 6 0 R in a graphics
            // state and null in object six has said the key holds nothing, in a roundabout but
            // perfectly legal way, and there is nothing under it to fix up.
            if (value is PdfNameObject || value is PdfStringObject || value is PdfBooleanObject || value is PdfIntegerObject || value is PdfNumberObject || value is PdfNullObject)
            {
                Debug.Assert(value.IsIndirect);
                Debug.Assert(value.Owner == owner);
            }
            else
            {
                Debug.Assert(false, "Should not come here. Object is neither a dictionary nor an array.");
            }
        }
    }

    /// <summary>
    /// Gets the indirect reference of this object. If the value is null, this object is a direct object.
    /// </summary>
    public PdfReference Reference
    {
        get => _iref;

        // Setting the reference outside PdfPinata is not considered as a valid operation.
        internal set => _iref = value;
    }
    private PdfReference _iref;

    /// <summary>
    /// Gets a value indicating that this object was read out of an object stream rather than
    /// standing on its own in the file.
    /// </summary>
    /// <remarks>
    /// It is asked exactly one question, and only when the document is encrypted: whether to
    /// decrypt this object's strings. An object stream is encrypted as a whole, and the strings
    /// inside it are covered by that and are not separately encrypted — so decrypting them again on
    /// the way in turns every one of them to nonsense. Nothing else depends on where an object came
    /// from, and nothing else should.
    /// </remarks>
    internal bool IsFromObjectStream { get; set; }

    /// <summary>
    /// Gets a value indicating that this object has been changed since the document was read.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="PdfDocument.SaveIncremental(System.IO.Stream)"/> and by nothing else. An
    /// incremental update appends only what has changed, so an object wrongly reported clean is
    /// silently left at its old value — the worst shape a defect can take, because the file opens
    /// and looks right. When in doubt this says dirty; a needlessly rewritten object costs bytes
    /// and nothing else.
    /// </remarks>
    public bool IsDirty { get; internal set; }

    /// <summary>
    /// Marks this object as changed, and everything it is written inside along with it.
    /// </summary>
    /// <remarks>
    /// A caller reaching past the usual API — through <see cref="Advanced.PdfInternals"/>, say —
    /// can use this to say so. The usual API says it for itself.
    /// </remarks>
    public void MarkAsChanged()
    {
        // Up to the nearest indirect object, because that is the one an incremental save writes. A
        // direct array inside a page dictionary is not in the cross-reference table and saying it
        // changed would tell nobody anything; what changed, as far as the file is concerned, is the
        // page. The depth bound is for a graph that has been made to contain itself.
        var owner = this;
        for (var depth = 0; owner != null && depth < 64; depth++)
        {
            owner.IsDirty = true;
            if (owner.Reference != null)
                return;

            owner = owner.Container;
        }
    }

    /// <summary>
    /// The object this one is written inside, when it is a direct value of another rather than an
    /// indirect object in its own right.
    /// </summary>
    /// <remarks>
    /// Recorded when the value is stored, and read only by <see cref="MarkAsChanged"/>. A copy taken
    /// of a dictionary keeps whatever container its children were given by the original, so the
    /// pointer can be stale — which is why nothing depends on it being right. The cost of a wrong
    /// answer here is an object needlessly rewritten into an appended revision, and the cost of not
    /// having it at all is a change silently lost.
    /// </remarks>
    internal PdfObject Container { get; set; }

    /// <summary>
    /// Records that a value now sits inside this object, if it is the kind of value that can carry
    /// changes of its own.
    /// </summary>
    internal static void Contain(PdfItem value, PdfObject container)
    {
        // An indirect object stands on its own and is written on its own, so it has no container in
        // the sense that matters here.
        if (value is PdfObject contained && contained.Reference == null && !ReferenceEquals(contained, container))
            contained.Container = container;
    }
}
