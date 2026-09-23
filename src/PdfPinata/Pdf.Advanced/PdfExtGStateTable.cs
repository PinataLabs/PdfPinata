#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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

using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Contains all used ExtGState objects of a document.
/// </summary>
public sealed class PdfExtGStateTable : PdfResourceTable
{
    /// <summary>
    /// Initializes a new instance of this class, which is a singleton for each document.
    /// </summary>
    public PdfExtGStateTable(PdfDocument document)
        : base(document)
    { }


    /// <summary>
    /// Gets a PdfExtGState with the key 'CA' set to the specified alpha value.
    /// </summary>
    public PdfExtGState GetExtGStateStroke(double alpha, bool overprint)
    {
        var key = PdfExtGState.MakeKey(alpha, overprint);
        PdfExtGState extGState;
        if (!_strokeAlphaValues.TryGetValue(key, out extGState))
        {
            extGState = new PdfExtGState(Owner);
            extGState.StrokeAlpha = alpha;
            if (overprint)
            {
                extGState.StrokeOverprint = true;
                extGState.Elements.SetInteger(PdfExtGState.Keys.OPM, 1);
            }
            _strokeAlphaValues[key] = extGState;
        }
        return extGState;
    }

    /// <summary>
    /// Gets a PdfExtGState with the key 'ca' set to the specified alpha value.
    /// </summary>
    public PdfExtGState GetExtGStateNonStroke(double alpha, bool overprint)
    {
        var key = PdfExtGState.MakeKey(alpha, overprint);
        PdfExtGState extGState;
        if (!_nonStrokeStates.TryGetValue(key, out extGState))
        {
            extGState = new PdfExtGState(Owner);
            extGState.NonStrokeAlpha = alpha;
            if (overprint)
            {
                extGState.NonStrokeOverprint = true;
                extGState.Elements.SetInteger(PdfExtGState.Keys.OPM, 1);
            }

            _nonStrokeStates[key] = extGState;
        }
        return extGState;
    }

    private readonly Dictionary<string, PdfExtGState> _strokeAlphaValues = new();
    private readonly Dictionary<string, PdfExtGState> _nonStrokeStates = new();
}
