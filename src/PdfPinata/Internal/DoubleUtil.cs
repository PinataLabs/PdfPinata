#region Copyright
// Authors:
//   Microsoft
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
using System.Runtime.InteropServices;

namespace PdfPinata.Internal;

/// <summary>
/// Some floating point utilities. Partially reflected from WPF, later equalized with original source code.
/// </summary>
internal static class DoubleUtil
{
    private const double Epsilon = 2.2204460492503131E-16; // smallest such that 1.0 + Epsilon != 1.0
    private const double TenTimesEpsilon = 10.0 * Epsilon;

    /// <summary>
    /// Indicates whether the values are so close that they can be considered as equal.
    /// </summary>
    public static bool AreClose(double value1, double value2)
    {
#pragma warning disable S1244 // Exact on purpose: the exact case of the tolerant comparison, taken before the tolerance.
        if (value1.Equals(value2))
            return true;
#pragma warning restore S1244
        // This computes (|value1-value2| / (|value1| + |value2| + 10.0)) < Epsilon
        var eps = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * Epsilon;
        var delta = value1 - value2;
        return (-eps < delta) && (eps > delta);
    }

    /// <summary>
    /// Indicates whether value1 is greater than value2 or the values are close to each other.
    /// </summary>
    public static bool GreaterThanOrClose(double value1, double value2)
    {
        return value1 > value2 || AreClose(value1, value2);
    }

    /// <summary>
    /// Indicates whether value1 is less than value2 or the values are close to each other.
    /// </summary>
    public static bool LessThanOrClose(double value1, double value2)
    {
        return value1 < value2 || AreClose(value1, value2);
    }

    /// <summary>
    /// Indicates whether the value is not a number.
    /// </summary>
    public static bool IsNaN(double value)
    {
        var t = new NanUnion();
        t.DoubleValue = value;

        var exp = t.UintValue & 0xfff0000000000000;
        var man = t.UintValue & 0x000fffffffffffff;

        return (exp == 0x7ff0000000000000 || exp == 0xfff0000000000000) && (man != 0);
    }

    /// <summary>
    /// Indicates whether the value is 0 or close to 0.
    /// </summary>
    public static bool IsZero(double value)
    {
        return Math.Abs(value) < TenTimesEpsilon;
    }

    /// <summary>
    /// Converts a double to integer.
    /// </summary>
    public static int DoubleToInt(double value)
    {
        return 0 < value ? (int)(value + 0.5) : (int)(value - 0.5);
    }

    [StructLayout(LayoutKind.Explicit)]
    struct NanUnion
    {
        [FieldOffset(0)] internal double DoubleValue;
        [FieldOffset(0)] internal readonly ulong UintValue;
    }
}
