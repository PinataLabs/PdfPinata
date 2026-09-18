#region PDFsharp - A .NET library for processing PDF
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
using System.Runtime.InteropServices;
using PdfPinata.Internal;
namespace PdfPinata.Drawing;

/// <summary>
/// Represents a two-dimensional vector specified by x- and y-coordinates.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct XVector : IFormattable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XVector"/> structure with the given components.
    /// </summary>
    public XVector(double x, double y)
    {
        _x = x;
        _y = y;
    }

    /// <summary>
    /// Determines whether two vectors have the same components. Note that this compares doubles exactly; use <see cref="Equals(XVector)"/> for a comparison that treats NaN components as equal.
    /// </summary>
    public static bool operator ==(XVector vector1, XVector vector2)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        return vector1._x == vector2._x && vector1._y == vector2._y;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    /// <summary>
    /// Determines whether two vectors differ in either component.
    /// </summary>
    public static bool operator !=(XVector vector1, XVector vector2)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
        return vector1._x != vector2._x || vector1._y != vector2._y;
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    /// <summary>
    /// Determines whether two vectors are equal component by component, treating two NaN components as equal.
    /// </summary>
    public static bool Equals(XVector vector1, XVector vector2)
    {
        if (vector1.X.Equals(vector2.X))
            return vector1.Y.Equals(vector2.Y);
        return false;
    }

    /// <summary>
    /// Determines whether the given object is an <see cref="XVector"/> equal to this one.
    /// </summary>
    public override bool Equals(object o)
    {
        if (!(o is XVector))
            return false;
        return Equals(this, (XVector)o);
    }

    /// <summary>
    /// Determines whether the given vector is equal to this one.
    /// </summary>
    public bool Equals(XVector value)
    {
        return Equals(this, value);
    }

    /// <summary>
    /// Returns a hash code for this vector.
    /// </summary>
    public override int GetHashCode()
    {
        // ReSharper disable NonReadonlyFieldInGetHashCode
        return _x.GetHashCode() ^ _y.GetHashCode();
        // ReSharper restore NonReadonlyFieldInGetHashCode
    }

    /// <summary>
    /// Parses a vector from a string of the form produced by <see cref="ToString()"/>.
    /// </summary>
    public static XVector Parse(string source)
    {
        TokenizerHelper helper = new TokenizerHelper(source, CultureInfo.InvariantCulture);
        string str = helper.NextTokenRequired();
        XVector vector = new XVector(Convert.ToDouble(str, CultureInfo.InvariantCulture), Convert.ToDouble(helper.NextTokenRequired(), CultureInfo.InvariantCulture));
        helper.LastTokenRequired();
        return vector;
    }

    /// <summary>
    /// Gets or sets the x-component of this vector.
    /// </summary>
    public double X
    {
        get => _x;
        set => _x = value;
    }
    double _x;

    /// <summary>
    /// Gets or sets the y-component of this vector.
    /// </summary>
    public double Y
    {
        get => _y;
        set => _y = value;
    }
    double _y;

    /// <summary>
    /// Returns a string that represents this vector, using the current culture.
    /// </summary>
    public override string ToString()
    {
        return ConvertToString(null, null);
    }

    /// <summary>
    /// Returns a string that represents this vector, formatted with the given provider.
    /// </summary>
    public string ToString(IFormatProvider provider)
    {
        return ConvertToString(null, provider);
    }

    string IFormattable.ToString(string format, IFormatProvider provider)
    {
        return ConvertToString(format, provider);
    }

    internal string ConvertToString(string format, IFormatProvider provider)
    {
        const char numericListSeparator = ',';
        provider = provider ?? CultureInfo.InvariantCulture;
        // ReSharper disable once FormatStringProblem
        return string.Format(provider, "{1:" + format + "}{0}{2:" + format + "}", numericListSeparator, _x, _y);
    }

    /// <summary>
    /// Gets the length of this vector. Prefer <see cref="LengthSquared"/> when only comparing lengths, as it avoids the square root.
    /// </summary>
    public double Length => Math.Sqrt(_x * _x + _y * _y);

    /// <summary>
    /// Gets the square of the length of this vector.
    /// </summary>
    public double LengthSquared => _x * _x + _y * _y;

    /// <summary>
    /// Scales this vector to unit length, keeping its direction.
    /// </summary>
    public void Normalize()
    {
        this = this / Math.Max(Math.Abs(_x), Math.Abs(_y));
        this = this / Length;
    }

    /// <summary>
    /// Returns the cross product of two vectors: the z-component of the 3-D cross product of the two lying in the xy-plane. Its sign says which side of the first the second lies on.
    /// </summary>
    public static double CrossProduct(XVector vector1, XVector vector2)
    {
        return vector1._x * vector2._y - vector1._y * vector2._x;
    }

    /// <summary>
    /// Returns the angle in degrees from the first vector to the second, measured anticlockwise.
    /// </summary>
    public static double AngleBetween(XVector vector1, XVector vector2)
    {
        double y = vector1._x * vector2._y - vector2._x * vector1._y;
        double x = vector1._x * vector2._x + vector1._y * vector2._y;
        return (Math.Atan2(y, x) * 57.295779513082323);
    }

    /// <summary>
    /// Returns a vector of the same length pointing the opposite way.
    /// </summary>
    public static XVector operator -(XVector vector)
    {
        return new XVector(-vector._x, -vector._y);
    }

    /// <summary>
    /// Reverses the direction of this vector, keeping its length.
    /// </summary>
    public void Negate()
    {
        _x = -_x;
        _y = -_y;
    }

    /// <summary>
    /// Adds two vectors.
    /// </summary>
    public static XVector operator +(XVector vector1, XVector vector2)
    {
        return new XVector(vector1._x + vector2._x, vector1._y + vector2._y);
    }

    /// <summary>
    /// Adds two vectors.
    /// </summary>
    public static XVector Add(XVector vector1, XVector vector2)
    {
        return new XVector(vector1._x + vector2._x, vector1._y + vector2._y);
    }

    /// <summary>
    /// Subtracts the second vector from the first.
    /// </summary>
    public static XVector operator -(XVector vector1, XVector vector2)
    {
        return new XVector(vector1._x - vector2._x, vector1._y - vector2._y);
    }

    /// <summary>
    /// Subtracts the second vector from the first.
    /// </summary>
    public static XVector Subtract(XVector vector1, XVector vector2)
    {
        return new XVector(vector1._x - vector2._x, vector1._y - vector2._y);
    }

    /// <summary>
    /// Translates a point by a vector.
    /// </summary>
    public static XPoint operator +(XVector vector, XPoint point)
    {
        return new XPoint(point.X + vector._x, point.Y + vector._y);
    }

    /// <summary>
    /// Translates a point by a vector.
    /// </summary>
    public static XPoint Add(XVector vector, XPoint point)
    {
        return new XPoint(point.X + vector._x, point.Y + vector._y);
    }

    /// <summary>
    /// Scales a vector by a scalar.
    /// </summary>
    public static XVector operator *(XVector vector, double scalar)
    {
        return new XVector(vector._x * scalar, vector._y * scalar);
    }

    /// <summary>
    /// Scales a vector by a scalar.
    /// </summary>
    public static XVector Multiply(XVector vector, double scalar)
    {
        return new XVector(vector._x * scalar, vector._y * scalar);
    }

    /// <summary>
    /// Scales a vector by a scalar.
    /// </summary>
    public static XVector operator *(double scalar, XVector vector)
    {
        return new XVector(vector._x * scalar, vector._y * scalar);
    }

    /// <summary>
    /// Scales a vector by a scalar.
    /// </summary>
    public static XVector Multiply(double scalar, XVector vector)
    {
        return new XVector(vector._x * scalar, vector._y * scalar);
    }

    /// <summary>
    /// Divides a vector by a scalar.
    /// </summary>
    public static XVector operator /(XVector vector, double scalar)
    {
        return vector * (1.0 / scalar);
    }

    /// <summary>
    /// Divides a vector by a scalar.
    /// </summary>
    public static XVector Divide(XVector vector, double scalar)
    {
        return vector * (1.0 / scalar);
    }

    /// <summary>
    /// Transforms a vector by a matrix. A vector carries no position, so the matrix's translation is not applied.
    /// </summary>
    public static XVector operator *(XVector vector, XMatrix matrix)
    {
        return matrix.Transform(vector);
    }

    /// <summary>
    /// Transforms a vector by a matrix. A vector carries no position, so the matrix's translation is not applied.
    /// </summary>
    public static XVector Multiply(XVector vector, XMatrix matrix)
    {
        return matrix.Transform(vector);
    }

    /// <summary>
    /// Returns the dot product of two vectors.
    /// </summary>
    public static double operator *(XVector vector1, XVector vector2)
    {
        return vector1._x * vector2._x + vector1._y * vector2._y;
    }

    /// <summary>
    /// Returns the dot product of two vectors.
    /// </summary>
    public static double Multiply(XVector vector1, XVector vector2)
    {
        return vector1._x * vector2._x + vector1._y * vector2._y;
    }

    /// <summary>
    /// Returns the determinant of the two vectors taken as the rows of a 2x2 matrix, which is the cross product of the pair.
    /// </summary>
    public static double Determinant(XVector vector1, XVector vector2)
    {
        return vector1._x * vector2._y - vector1._y * vector2._x;
    }

    /// <summary>
    /// Converts a vector to a size, taking the absolute value of each component because a size may not be negative.
    /// </summary>
    public static explicit operator XSize(XVector vector)
    {
        return new XSize(Math.Abs(vector._x), Math.Abs(vector._y));
    }

    /// <summary>
    /// Converts a vector to the point it reaches when applied at the origin.
    /// </summary>
    public static explicit operator XPoint(XVector vector)
    {
        return new XPoint(vector._x, vector._y);
    }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    /// <value>The debugger display.</value>
    // ReSharper disable UnusedMember.Local
    string DebuggerDisplay
        // ReSharper restore UnusedMember.Local
    {
        get
        {
            const string format = Config.SignificantFigures10;
            return string.Format(CultureInfo.InvariantCulture, "vector=({0:" + format + "}, {1:" + format + "})", _x, _y);
        }
    }
}
