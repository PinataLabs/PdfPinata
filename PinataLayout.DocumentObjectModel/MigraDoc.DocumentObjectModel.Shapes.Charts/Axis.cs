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

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// This class represents an axis in a chart.
/// </summary>
public partial class Axis : ChartObject
{
    /// <summary>
    /// Initializes a new instance of the Axis class.
    /// </summary>
    public Axis()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Axis class with the specified parent.
    /// </summary>
    internal Axis(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Axis Clone()
    {
        return (Axis)DeepCopy();
    }

    #endregion

    #region Properties
    /// <summary>
    /// Gets the title of the axis.
    /// </summary>
    public AxisTitle Title
    {
        get
        {
            if (this.title == null)
                this.title = new AxisTitle(this);

            return this.title;
        }
        set
        {
            SetParent(value);
            this.title = value;
        }
    }
    [DV]
    internal AxisTitle title;

    /// <summary>
    /// Gets or sets the minimum value of the axis.
    /// </summary>
    public double MinimumScale
    {
        get => this.minimumScale ?? 0;
        set => this.minimumScale = value;
    }
    [DV]
    internal double? minimumScale;

    /// <summary>
    /// Gets or sets the maximum value of the axis.
    /// </summary>
    public double MaximumScale
    {
        get => this.maximumScale ?? 0;
        set => this.maximumScale = value;
    }
    [DV]
    internal double? maximumScale;

    /// <summary>
    /// Gets or sets the interval of the primary tick.
    /// </summary>
    public double MajorTick
    {
        get => this.majorTick ?? 0;
        set => this.majorTick = value;
    }
    [DV]
    internal double? majorTick;

    /// <summary>
    /// Gets or sets the interval of the secondary tick.
    /// </summary>
    public double MinorTick
    {
        get => this.minorTick ?? 0;
        set => this.minorTick = value;
    }
    [DV]
    internal double? minorTick;

    /// <summary>
    /// Gets or sets the type of the primary tick mark.
    /// </summary>
    public TickMarkType MajorTickMark
    {
        get => this.majorTickMark ?? default;
        set => this.majorTickMark = EnumGuard.Checked(value);
    }
    [DV]
    internal TickMarkType? majorTickMark;

    /// <summary>
    /// Gets or sets the type of the secondary tick mark.
    /// </summary>
    public TickMarkType MinorTickMark
    {
        get => this.minorTickMark ?? default;
        set => this.minorTickMark = EnumGuard.Checked(value);
    }
    [DV]
    internal TickMarkType? minorTickMark;

    /// <summary>
    /// Gets the label of the primary tick.
    /// </summary>
    public TickLabels TickLabels
    {
        get
        {
            if (this.tickLabels == null)
                this.tickLabels = new TickLabels(this);

            return this.tickLabels;
        }
        set
        {
            SetParent(value);
            this.tickLabels = value;
        }
    }
    [DV]
    internal TickLabels tickLabels;

    /// <summary>
    /// Gets the format of the axis line.
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
    /// Gets the primary gridline object.
    /// </summary>
    public Gridlines MajorGridlines
    {
        get
        {
            if (this.majorGridlines == null)
                this.majorGridlines = new Gridlines(this);

            return this.majorGridlines;
        }
        set
        {
            SetParent(value);
            this.majorGridlines = value;
        }
    }
    [DV]
    internal Gridlines majorGridlines;

    /// <summary>
    /// Gets the secondary gridline object.
    /// </summary>
    public Gridlines MinorGridlines
    {
        get
        {
            if (this.minorGridlines == null)
                this.minorGridlines = new Gridlines(this);

            return this.minorGridlines;
        }
        set
        {
            SetParent(value);
            this.minorGridlines = value;
        }
    }
    [DV]
    internal Gridlines minorGridlines;

    /// <summary>
    /// Gets or sets, whether the axis has a primary gridline object.
    /// </summary>
    public bool HasMajorGridlines
    {
        get => this.hasMajorGridlines ?? false;
        set => this.hasMajorGridlines = value;
    }
    [DV]
    internal bool? hasMajorGridlines;

    /// <summary>
    /// Gets or sets, whether the axis has a secondary gridline object.
    /// </summary>
    public bool HasMinorGridlines
    {
        get => this.hasMinorGridlines ?? false;
        set => this.hasMinorGridlines = value;
    }
    [DV]
    internal bool? hasMinorGridlines;
    #endregion

    /// <summary>
    /// Determines whether the specified gridlines object is a MajorGridlines or an MinorGridlines.
    /// </summary>
    internal string CheckGridlines(Gridlines gridlines)
    {
        if ((this.majorGridlines != null) && (gridlines == this.majorGridlines))
            return "MajorGridlines";
        if ((this.minorGridlines != null) && (gridlines == this.minorGridlines))
            return "MinorGridlines";

        return "";
    }

    #region Internal
    /// <summary>
    /// Converts Axis into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        Chart chartObject = this.parent as Chart;

        serializer.WriteLine("\\" + chartObject.CheckAxis(this));
        int pos = serializer.BeginAttributes();

        if (this.minimumScale != null)
            serializer.WriteSimpleAttribute("MinimumScale", this.MinimumScale);
        if (this.maximumScale != null)
            serializer.WriteSimpleAttribute("MaximumScale", this.MaximumScale);
        if (this.majorTick != null)
            serializer.WriteSimpleAttribute("MajorTick", this.MajorTick);
        if (this.minorTick != null)
            serializer.WriteSimpleAttribute("MinorTick", this.MinorTick);
        if (this.hasMajorGridlines != null)
            serializer.WriteSimpleAttribute("HasMajorGridLines", this.HasMajorGridlines);
        if (this.hasMinorGridlines != null)
            serializer.WriteSimpleAttribute("HasMinorGridLines", this.HasMinorGridlines);
        if (this.majorTickMark != null)
            serializer.WriteSimpleAttribute("MajorTickMark", this.MajorTickMark);
        if (this.minorTickMark != null)
            serializer.WriteSimpleAttribute("MinorTickMark", this.MinorTickMark);

        if (!this.IsNull("Title"))
            this.title.Serialize(serializer);

        if (!this.IsNull("LineFormat"))
            this.lineFormat.Serialize(serializer);

        if (!this.IsNull("MajorGridlines"))
            this.majorGridlines.Serialize(serializer);

        if (!this.IsNull("MinorGridlines"))
            this.minorGridlines.Serialize(serializer);

        if (!this.IsNull("TickLabels"))
            this.tickLabels.Serialize(serializer);

        serializer.EndAttributes(pos);
    }

    #endregion
}
