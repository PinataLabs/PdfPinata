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

namespace PdfPinata;

/// <summary>
/// Floating point formatting.
/// </summary>
/// <remarks>
/// Each format has three sections - positive, negative and zero - so that a value that rounds to
/// zero from below, and a negative zero itself, is written "0" rather than "-0". A single-section
/// format keeps the sign of whatever it rounds away, and a negative zero is noise in a content
/// stream and a trap for anything comparing two files byte for byte.
/// </remarks>
internal static class Config
{
    public const string SignificantFigures2 = "0.##;-0.##;0";
    public const string SignificantFigures3 = "0.###;-0.###;0";
    public const string SignificantFigures4 = "0.####;-0.####;0";
    public const string SignificantFigures7 = "0.#######;-0.#######;0";
    public const string SignificantFigures10 = "0.##########;-0.##########;0";
    public const string SignificantFigures1Plus9 = "0.0#########;-0.0#########;0.0";
}

internal static class Const
{
    // The degree-to-radian factor lives on Internal.Calc, which is where the arc and Bezier code
    // has always taken it from. It was declared here as well, identically, and the two copies
    // meant a caller could pick either without knowing there was a choice.

    /// <summary>
    /// Sinus of the angle to turn a regular font to look oblique. Used for italic simulation.
    /// </summary>
    public const double ItalicSkewAngleSinus = 0.34202014332566873304409961468226;  // = sin(20°)

    // How much bold simulation strokes and spaces a face is no longer a constant here: it is
    // chosen per face, as the XBoldSimulation on a FontResolverInfo, whose default is the 2% this
    // used to say.

    // The κ (kappa) for drawing a circle or an ellipse with four Bézier splines, specifying the distance of the influence point from the starting or end point of a spline.
    // Petzold: 4/3 * tan(α / 4)
    public const double κ = 0.5522847498307933984022516322796;  // := 4/3 * (1 - cos(-π/4)) / sin(π/4)) <=> 4/3 * (sqrt(2) - 1) <=> 4/3 * tan(π/8)
}
