#region MigraDoc - Creating Documents on the Fly
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
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel.Fields;

/// <summary>
/// BookmarkField is used as target for Hyperlinks or PageRefs.
/// </summary>
public partial class BookmarkField : DocumentObject
{
    /// <summary>
    /// Initializes a new instance of the BookmarkField class.
    /// </summary>
    internal BookmarkField()
    {
    }

    /// <summary>
    /// Initializes a new instance of the BookmarkField class with the specified parent.
    /// </summary>
    internal BookmarkField(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Initializes a new instance of the BookmarkField class with the necessary bookmark name.
    /// </summary>
    public BookmarkField(string name)
      : this()
    {
        this.Name = name;
    }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new BookmarkField Clone()
    {
        return (BookmarkField)DeepCopy();
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the name of the bookmark.
    /// Used to reference the bookmark from a Hyperlink or PageRef.
    /// </summary>
    public string Name
    {
        get => this.name ?? "";
        set => this.name = value;
    }
    [DV]
    internal string name;
    #endregion

    #region Internal
    /// <summary>
    /// Converts BookmarkField into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        if ((this.name ?? "") == string.Empty)
            throw new InvalidOperationException(DomSR.MissingObligatoryProperty("Name", "BookmarkField"));

        serializer.Write("\\field(Bookmark)[Name = \"" + this.Name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"]");
    }

    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    public override bool IsNull()
    {
        return false;
    }

    #endregion
}
