#region Copyright
//
// Authors:
//   Klaus Potzesny
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
using System.Linq;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Implementation of the Code 2 of 5 bar code.
/// </summary>
// ReSharper disable once InconsistentNaming
public class Code2of5Interleaved : TwoWidthBarCode
{
    /// <summary>
    /// Initializes a new instance of Interleaved2of5.
    /// </summary>
    public Code2of5Interleaved()
        : base("", XSize.Empty, CodeDirection.LeftToRight)
    {}

    /// <summary>
    /// Initializes a new instance of Interleaved2of5.
    /// </summary>
    public Code2of5Interleaved(string code)
        : base(code, XSize.Empty, CodeDirection.LeftToRight)
    {}

    /// <summary>
    /// Initializes a new instance of Interleaved2of5.
    /// </summary>
    public Code2of5Interleaved(string code, XSize size)
        : base(code, size, CodeDirection.LeftToRight)
    {}

    /// <summary>
    /// Initializes a new instance of Interleaved2of5.
    /// </summary>
    public Code2of5Interleaved(string code, XSize size, CodeDirection direction)
        : base(code, size, direction)
    {}

    private static readonly bool[][] _lines =
    [
        [false, false, true, true, false],
        [true, false, false, false, true],
        [false, true, false, false, true],
        [true, true, false, false, false],
        [false, false, true, false, true],
        [true, false, true, false, false],
        [false, true, true, false, false],
        [false, false, false, true, true],
        [true, false, false, true, false],
        [false, true, false, true, false]
    ];

    /// <summary>
    /// Renders the bar code.
    /// </summary>
    protected internal override void Render(XGraphics gfx, XBrush brush, XFont font, XPoint position)
    {
        var state = gfx.Save();

        var info = new BarCodeRenderInfo(gfx, brush, font, position);
        InitRendering(info);
        info.CurrPosInString = 0;
        info.CurrPos = position - CalcDistance(AnchorType.TopLeft, Anchor, Size);

        if (TurboBit)
            RenderTurboBit(info, true);
        RenderStart(info);
        while (info.CurrPosInString < Text.Length)
            RenderNextPair(info);
        RenderStop(info);
        if (TurboBit)
            RenderTurboBit(info, false);
        if (TextLocation != TextLocation.None)
            RenderText(info);

        gfx.Restore(state);
    }

    /// <summary>
    /// Calculates the wide and narrow line widths,
    /// taking into account the required rendering size.
    /// </summary>
    internal override void CalcNarrowBarWidth(BarCodeRenderInfo info)
    {
        /*
         * The total width is the sum of the following parts:
         * Starting lines      = 4 * narrow
         *  +
         * Code Representation = (2 * wide + 3 * narrow) * code.Length
         *  +
         * Stopping lines      =  1 * wide + 2 * narrow
         *
         * with r = relation ( = wide / narrow), this results in
         *
         * Total width = (6 + r + (2 * r + 3) * text.Length) * narrow
         */
        var narrowLineAmount = 6 + WideNarrowRatio + (2 * WideNarrowRatio + 3) * Text.Length;
        info.NarrowBarWidth = Size.Width / narrowLineAmount;
    }

    private void RenderStart(BarCodeRenderInfo info)
    {
        RenderBar(info, false);
        RenderGap(info, false);
        RenderBar(info, false);
        RenderGap(info, false);
    }

    private void RenderStop(BarCodeRenderInfo info)
    {
        RenderBar(info, true);
        RenderGap(info, false);
        RenderBar(info, false);
    }

    /// <summary>
    /// Renders the next digit pair as bar code element.
    /// </summary>
    private void RenderNextPair(BarCodeRenderInfo info)
    {
        var digitForLines = int.Parse(Text[info.CurrPosInString].ToString());
        var digitForGaps = int.Parse(Text[info.CurrPosInString + 1].ToString());
        var linesArray = _lines[digitForLines];
        var gapsArray = _lines[digitForGaps];
        for (var idx = 0; idx < 5; ++idx)
        {
            RenderBar(info, linesArray[idx]);
            RenderGap(info, gapsArray[idx]);
        }
        info.CurrPosInString += 2;
    }

    /// <summary>
    /// Checks the code to be convertible into an interleaved 2 of 5 bar code.
    /// </summary>
    /// <param name="text">The code to be checked.</param>
    /// <remarks>
    /// This was empty, and the message it should have been raising - <c>BcgSR.Invalid2Of5Code</c>,
    /// which describes exactly this rule - was written and never called from anywhere. So a code
    /// this symbology cannot carry was accepted here and failed later inside the renderer, as an
    /// <see cref="System.IndexOutOfRangeException"/> for an odd number of digits and a
    /// <see cref="System.FormatException"/> for anything that is not one. Neither names the code
    /// nor the rule it broke, and both arrive at drawing time rather than where the mistake was.
    /// </remarks>
    protected override void CheckCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Two digits are carried per five bars, so the count has to be even. Zero of them is even
        // and is what the parameterless constructor sets, so it is left to the renderer to refuse
        // as an unset code rather than treated as an invalid one here.
        if (text.Length % 2 != 0)
            throw new ArgumentException(BcgSR.Invalid2Of5Code(text));

        if (text.Any(ch => ch is < '0' or > '9'))
        {
            throw new ArgumentException(BcgSR.Invalid2Of5Code(text));
        }
    }
}
