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

using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Visitors;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents the collection of HeaderFooter objects.
/// </summary>
public partial class HeadersFooters : DocumentObject, IVisitable
{
    /// <summary>
    /// Initializes a new instance of the HeadersFooters class.
    /// </summary>
    public HeadersFooters()
    {
    }

    /// <summary>
    /// Initializes a new instance of the HeadersFooters class with the specified parent.
    /// </summary>
    public HeadersFooters(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new HeadersFooters Clone()
    {
        return (HeadersFooters)DeepCopy();
    }

    #endregion

    #region Properties
    /// <summary>
    /// Returns true if this collection contains headers, false otherwise.
    /// </summary>
    public bool IsHeader
    {
        get
        {
            Section sec = (Section)parent;
            return sec.headers == this;
        }
    }

    /// <summary>
    /// Returns true if this collection contains footers, false otherwise.
    /// </summary>
    public bool IsFooter => !IsHeader;

    /// <summary>
    /// Determines whether a particular header or footer exists.
    /// </summary>
    public bool HasHeaderFooter(HeaderFooterIndex index)
    {
        return !IsNull(index.ToString());
    }

    /// <summary>
    /// Gets or sets the even page HeaderFooter of the HeadersFooters object.
    /// </summary>
    public HeaderFooter EvenPage
    {
        get
        {
            if (evenPage == null)
                evenPage = new HeaderFooter(this);

            return evenPage;
        }
        set
        {
            SetParent(value);
            evenPage = value;
        }
    }
    [DV]
    internal HeaderFooter evenPage;

    /// <summary>
    /// Gets or sets the first page HeaderFooter of the HeadersFooters object.
    /// </summary>
    public HeaderFooter FirstPage
    {
        get
        {
            if (firstPage == null)
                firstPage = new HeaderFooter(this);

            return firstPage;
        }
        set
        {
            SetParent(value);
            firstPage = value;
        }
    }
    [DV]
    internal HeaderFooter firstPage;

    /// <summary>
    /// Gets or sets the primary HeaderFooter of the HeadersFooters object.
    /// </summary>
    public HeaderFooter Primary
    {
        get
        {
            if (primary == null)
                primary = new HeaderFooter(this);

            return primary;
        }
        set
        {
            SetParent(value);
            primary = value;
        }
    }
    [DV]
    internal HeaderFooter primary;
    #endregion

    #region Internal
    /// <summary>
    /// Converts HeadersFooters into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        bool hasPrimary = HasHeaderFooter(HeaderFooterIndex.Primary);
        bool hasEvenPage = HasHeaderFooter(HeaderFooterIndex.EvenPage);
        bool hasFirstPage = HasHeaderFooter(HeaderFooterIndex.FirstPage);

        // \primary...
        if (hasPrimary)
            Primary.Serialize(serializer, "primary");

        // \even... 
        if (hasEvenPage)
            EvenPage.Serialize(serializer, "evenpage");

        // \firstpage...
        if (hasFirstPage)
            FirstPage.Serialize(serializer, "firstpage");
    }

    /// <summary>
    /// Allows the visitor object to visit the document object and it's child objects.
    /// </summary>
    void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
    {
        visitor.VisitHeadersFooters(this);

        if (visitChildren)
        {
            if (HasHeaderFooter(HeaderFooterIndex.Primary))
                ((IVisitable)primary).AcceptVisitor(visitor, visitChildren);
            if (HasHeaderFooter(HeaderFooterIndex.EvenPage))
                ((IVisitable)evenPage).AcceptVisitor(visitor, visitChildren);
            if (HasHeaderFooter(HeaderFooterIndex.FirstPage))
                ((IVisitable)firstPage).AcceptVisitor(visitor, visitChildren);
        }
    }

    #endregion
}
