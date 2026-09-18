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


namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// Base Class for all positionable Classes.
/// </summary>
public partial class Shape : DocumentObject
{
    /// <summary>
    /// Initializes a new instance of the Shape class.
    /// </summary>
    public Shape()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Shape class with the specified parent.
    /// </summary>
    internal Shape(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Shape Clone()
    {
        return (Shape)DeepCopy();
    }

    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the wrapping format of the shape.
    /// </summary>
    public WrapFormat WrapFormat
    {
        get
        {
            if (this.wrapFormat == null)
                this.wrapFormat = new WrapFormat(this);

            return this.wrapFormat;
        }
        set
        {
            SetParent(value);
            this.wrapFormat = value;
        }
    }
    [DV]
    internal WrapFormat wrapFormat;

    /// <summary>
    /// Gets or sets the reference point of the Top property.
    /// </summary>
    public RelativeVertical RelativeVertical
    {
        get => this.relativeVertical ?? default;
        set => this.relativeVertical = EnumGuard.Checked(value);
    }
    [DV]
    internal RelativeVertical? relativeVertical;

    /// <summary>
    /// Gets or sets the reference point of the Left property.
    /// </summary>
    public RelativeHorizontal RelativeHorizontal
    {
        get => this.relativeHorizontal ?? default;
        set => this.relativeHorizontal = EnumGuard.Checked(value);
    }
    [DV]
    internal RelativeHorizontal? relativeHorizontal;

    /// <summary>
    /// Gets or sets the position of the top side of the shape.
    /// </summary>
    public TopPosition Top
    {
        get => this.top;
        set => this.top = value;
    }
    [DV]
    internal TopPosition top = TopPosition.NullValue;

    /// <summary>
    /// Gets or sets the position of the left side of the shape.
    /// </summary>
    public LeftPosition Left
    {
        get => this.left;
        set => this.left = value;
    }
    [DV]
    internal LeftPosition left = LeftPosition.NullValue;

    /// <summary>
    /// Gets the line format of the shape's border.
    /// </summary>
    public LineFormat LineFormat
    {
        get
        {
            if (this.lineFormat == null)
                this.lineFormat = new LineFormat(this);

            return this.lineFormat;
        }
        set
        {
            SetParent(value);
            this.lineFormat = value;
        }
    }
    [DV]
    internal LineFormat lineFormat;

    /// <summary>
    /// Gets the background filling format of the shape.
    /// </summary>
    public FillFormat FillFormat
    {
        get
        {
            if (this.fillFormat == null)
                this.fillFormat = new FillFormat(this);

            return this.fillFormat;
        }
        set
        {
            SetParent(value);
            this.fillFormat = value;
        }
    }
    [DV]
    internal FillFormat fillFormat;

    /// <summary>
    /// Gets or sets the height of the shape.
    /// </summary>
    public Unit Height
    {
        get => this.height;
        set => this.height = value;
    }
    [DV]
    internal Unit height = Unit.NullValue;

    /// <summary>
    /// Gets or sets the width of the shape.
    /// </summary>
    public Unit Width
    {
        get => this.width;
        set => this.width = value;
    }
    [DV]
    internal Unit width = Unit.NullValue;

    /// <summary>
    /// Gets or sets the text that stands in for this shape for a reader who cannot see it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What a tagged document writes as the <c>/Alt</c> of the shape's <c>/Figure</c> element. It is
    /// what decides whether the shape is tagged at all: described, it is a figure; left unset, it is
    /// drawn as an artifact and passed over in silence.
    /// </para>
    /// <para>
    /// That is the right way round. An undescribed figure tells a reader that something is there and
    /// then cannot say what, which leaves them knowing only that they have missed something; marked as
    /// decoration it is at least honest, and for the rule above a letterhead it is also correct.
    /// Nothing invents a description — what a picture is for is a fact about the document rather than
    /// about the pixels, and a guess would go into the one field nobody can check.
    /// </para>
    /// <para>
    /// Ignored by <see cref="TextFrame"/>, whose contents are paragraphs and tables that describe
    /// themselves.
    /// </para>
    /// </remarks>
    public string AlternativeText
    {
        get => this.alternativeText ?? "";
        set => this.alternativeText = value;
    }
    [DV]
    internal string alternativeText;
    #endregion

    #region Internal
    /// <summary>
    /// Converts Shape into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        if (!this.height.IsNull)
            serializer.WriteSimpleAttribute("Height", this.Height);
        if (!this.width.IsNull)
            serializer.WriteSimpleAttribute("Width", this.Width);
        if (this.relativeHorizontal != null)
            serializer.WriteSimpleAttribute("RelativeHorizontal", this.RelativeHorizontal);
        if (this.relativeVertical != null)
            serializer.WriteSimpleAttribute("RelativeVertical", this.RelativeVertical);
        if (this.alternativeText != null)
            serializer.WriteSimpleAttribute("AlternativeText", this.AlternativeText);
        if (!this.IsNull("Left"))
            this.left.Serialize(serializer);
        if (!this.IsNull("Top"))
            this.top.Serialize(serializer);
        if (!this.IsNull("WrapFormat"))
            this.wrapFormat.Serialize(serializer);
        if (!this.IsNull("LineFormat"))
            this.lineFormat.Serialize(serializer);
        if (!this.IsNull("FillFormat"))
            this.fillFormat.Serialize(serializer);
    }

    #endregion
}
