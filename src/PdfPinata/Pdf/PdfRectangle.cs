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
using System.Globalization;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents a PDF rectangle value, that is internally an array with 4 real values.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class PdfRectangle : PdfItem
{
    // This class must behave like a value type. Therefore it cannot be changed (like System.String).

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class.
    /// </summary>
    public PdfRectangle()
    { }

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class with two points specifying
    /// two diagonally opposite corners. Notice that in contrast to GDI+ convention the
    /// 3rd and the 4th parameter specify a point and not a width. This is so much confusing
    /// that this function is for internal use only.
    /// </summary>
    internal PdfRectangle(double x1, double y1, double x2, double y2)
    {
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class with two points specifying
    /// two diagonally opposite corners.
    /// </summary>
    public PdfRectangle(XPoint pt1, XPoint pt2)
    {
        X1 = pt1.X;
        Y1 = pt1.Y;
        X2 = pt2.X;
        Y2 = pt2.Y;
    }

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class with the specified location and size.
    /// </summary>
    public PdfRectangle(XPoint pt, XSize size)
    {
        X1 = pt.X;
        Y1 = pt.Y;
        X2 = pt.X + size.Width;
        Y2 = pt.Y + size.Height;
    }

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class with the specified XRect.
    /// </summary>
    public PdfRectangle(XRect rect)
    {
        X1 = rect.X;
        Y1 = rect.Y;
        X2 = rect.X + rect.Width;
        Y2 = rect.Y + rect.Height;
    }

    /// <summary>
    /// Initializes a new instance of the PdfRectangle class with the specified PdfArray.
    /// </summary>
    internal PdfRectangle(PdfItem item)
    {
        if (item is null or PdfNull)
            return;

        if (item is PdfReference reference)
            item = reference.Value;

        if (item is not PdfArray array)
            throw new InvalidOperationException(PSSR.UnexpectedTokenInPdfFile);

        X1 = array.Elements.GetReal(0);
        Y1 = array.Elements.GetReal(1);
        X2 = array.Elements.GetReal(2);
        Y2 = array.Elements.GetReal(3);
    }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    public new PdfRectangle Clone()
    {
        return (PdfRectangle)Copy();
    }

    /// <summary>
    /// Implements cloning this instance.
    /// </summary>
    protected override object Copy()
    {
        var rect = (PdfRectangle)base.Copy();
        return rect;
    }

    /// <summary>
    /// Tests whether all coordinate are zero.
    /// </summary>
    public bool IsEmpty =>
        // ReSharper disable CompareOfFloatsByEqualityOperator
        X1 == 0 && Y1 == 0 && X2 == 0 && Y2 == 0;
    // ReSharper restore CompareOfFloatsByEqualityOperator

    /// <summary>
    /// Tests whether the specified object is a PdfRectangle and has equal coordinates.
    /// </summary>
    public override bool Equals(object obj)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        var rectangle = obj as PdfRectangle;
        if (rectangle == null)
            return false;

        var rect = rectangle;
        #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
        return rect.X1 == X1 && rect.Y1 == Y1 && rect.X2 == X2 && rect.Y2 == Y2;
        #pragma warning restore S1244
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    public override int GetHashCode()
    {
        // This code is from System.Drawing...
        return (int)((uint)X1 ^ (((uint)Y1 << 13) |
                                      ((uint)Y1 >> 0x13)) ^ (((uint)X2 << 0x1a) |
                                                                 ((uint)X2 >> 6)) ^ (((uint)Y2 << 7) |
            ((uint)Y2 >> 0x19)));
    }

    /// <summary>
    /// Tests whether two structures have equal coordinates.
    /// </summary>
    #pragma warning disable S3875 // Public API: a rectangle is an immutable value compared by its coordinates, and removing the operator would turn callers' comparisons into reference equality.
    public static bool operator ==(PdfRectangle left, PdfRectangle right)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        // use: if (Object.ReferenceEquals(left, null))
        if ((object)left != null)
        {
            #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
            if ((object)right != null)
                return left.X1 == right.X1 && left.Y1 == right.Y1 && left.X2 == right.X2 && left.Y2 == right.Y2;
                #pragma warning restore S1244
            return false;
        }
        return (object)right == null;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }
    #pragma warning restore S3875

    /// <summary>
    /// Tests whether two structures differ in one or more coordinates.
    /// </summary>
    public static bool operator !=(PdfRectangle left, PdfRectangle right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Gets or sets the x-coordinate of the first corner of this PdfRectangle.
    /// </summary>
    public double X1 { get; }

    /// <summary>
    /// Gets or sets the y-coordinate of the first corner of this PdfRectangle.
    /// </summary>
    public double Y1 { get; }

    /// <summary>
    /// Gets or sets the x-coordinate of the second corner of this PdfRectangle.
    /// </summary>
    public double X2 { get; }

    /// <summary>
    /// Gets or sets the y-coordinate of the second corner of this PdfRectangle.
    /// </summary>
    public double Y2 { get; }

    /// <summary>
    /// Gets X2 - X1.
    /// </summary>
    public double Width => X2 - X1;

    /// <summary>
    /// Gets Y2 - Y1.
    /// </summary>
    public double Height => Y2 - Y1;

    /// <summary>
    /// Gets or sets the coordinates of the first point of this PdfRectangle.
    /// </summary>
    public XPoint Location => new(X1, Y1);

    /// <summary>
    /// Gets or sets the size of this PdfRectangle.
    /// </summary>
    public XSize Size => new(X2 - X1, Y2 - Y1);

    /// <summary>
    /// Determines if the specified point is contained within this PdfRectangle.
    /// </summary>
    public bool Contains(XPoint pt)
    {
        return Contains(pt.X, pt.Y);
    }

    /// <summary>
    /// Determines if the specified point is contained within this PdfRectangle.
    /// </summary>
    public bool Contains(double x, double y)
    {
        // Treat rectangle inclusive/inclusive.
        return X1 <= x && x <= X2 && Y1 <= y && y <= Y2;
    }

    /// <summary>
    /// Determines if the rectangular region represented by rect is entirely contained within this PdfRectangle.
    /// </summary>
    public bool Contains(XRect rect)
    {
        return X1 <= rect.X && rect.X + rect.Width <= X2 &&
               Y1 <= rect.Y && rect.Y + rect.Height <= Y2;
    }

    /// <summary>
    /// Determines if the rectangular region represented by rect is entirely contained within this PdfRectangle.
    /// </summary>
    public bool Contains(PdfRectangle rect)
    {
        return X1 <= rect.X1 && rect.X2 <= X2 &&
               Y1 <= rect.Y1 && rect.Y2 <= Y2;
    }

    /// <summary>
    /// Returns the rectangle as an XRect object.
    /// </summary>
    public XRect ToXRect()
    {
        return new XRect(X1, Y1, Width, Height);
    }

    /// <summary>
    /// Returns the rectangle as a string in the form «[x1 y1 x2 y2]».
    /// </summary>
    public override string ToString()
    {
        const string format = Config.SignificantFigures3;
        return PdfEncoders.Format("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}]", X1, Y1, X2, Y2);
    }

    /// <summary>
    /// Writes the rectangle.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        writer.Write(this);
    }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSharper disable UnusedMember.Local
    private string DebuggerDisplay
        // ReSharper restore UnusedMember.Local
    {
        get
        {
            const string format = Config.SignificantFigures10;
            return string.Format(CultureInfo.InvariantCulture,
                "X1={0:" + format + "}, X2={1:" + format + "}, Y1={2:" + format + "}, Y2={3:" + format + "}", X1, Y1, X2, Y2);
        }
    }

    /// <summary>
    /// Represents an empty PdfRectangle.
    /// </summary>
    public static readonly PdfRectangle Empty = new();
}
