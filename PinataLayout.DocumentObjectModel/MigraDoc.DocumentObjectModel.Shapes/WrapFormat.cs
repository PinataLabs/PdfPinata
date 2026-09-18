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
/// Define how the shape should be wrapped between the texts.
/// </summary>
public partial class WrapFormat : DocumentObject
{
    /// <summary>
    /// Initializes a new instance of the WrapFormat class.
    /// </summary>
    public WrapFormat()
    {
    }

    /// <summary>
    /// Initializes a new instance of the WrapFormat class with the specified parent.
    /// </summary>
    internal WrapFormat(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new WrapFormat Clone()
    {
        return (WrapFormat)DeepCopy();
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the wrapping style.
    /// </summary>
    public WrapStyle Style
    {
        get => this.style ?? default;
        set => this.style = EnumGuard.Checked(value);
    }
    [DV]
    internal WrapStyle? style;

    /// <summary>
    /// Gets or sets the distance between the top side of the shape with the adjacent text.
    /// </summary>
    public Unit DistanceTop
    {
        get => this.distanceTop;
        set => this.distanceTop = value;
    }
    /// <summary>Backing field for <see cref="DistanceTop"/>.</summary>
    [DV]
    protected Unit distanceTop = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between the bottom side of the shape with the adjacent text.
    /// </summary>
    public Unit DistanceBottom
    {
        get => this.distanceBottom;
        set => this.distanceBottom = value;
    }
    /// <summary>Backing field for <see cref="DistanceBottom"/>.</summary>
    [DV]
    protected Unit distanceBottom = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between the left side of the shape with the adjacent text.
    /// </summary>
    public Unit DistanceLeft
    {
        get => this.distanceLeft;
        set => this.distanceLeft = value;
    }
    /// <summary>Backing field for <see cref="DistanceLeft"/>.</summary>
    [DV]
    protected Unit distanceLeft = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between the right side of the shape with the adjacent text.
    /// </summary>
    public Unit DistanceRight
    {
        get => this.distanceRight;
        set => this.distanceRight = value;
    }
    /// <summary>Backing field for <see cref="DistanceRight"/>.</summary>
    [DV]
    protected Unit distanceRight = Unit.NullValue;
    #endregion

    #region Internal
    /// <summary>
    /// Converts WrapFormat into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        int pos = serializer.BeginContent("WrapFormat");
        if (this.style != null)
            serializer.WriteSimpleAttribute("Style", this.Style);
        if (!this.distanceTop.IsNull)
            serializer.WriteSimpleAttribute("DistanceTop", this.DistanceTop);
        if (!this.distanceLeft.IsNull)
            serializer.WriteSimpleAttribute("DistanceLeft", this.DistanceLeft);
        if (!this.distanceRight.IsNull)
            serializer.WriteSimpleAttribute("DistanceRight", this.DistanceRight);
        if (!this.distanceBottom.IsNull)
            serializer.WriteSimpleAttribute("DistanceBottom", this.DistanceBottom);
        serializer.EndContent();
    }

    #endregion
}
