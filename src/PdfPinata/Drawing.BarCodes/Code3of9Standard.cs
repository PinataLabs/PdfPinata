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

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Imlpementation of the Code 3 of 9 bar code.
/// </summary>
// ReSharper disable once InconsistentNaming
public class Code3of9Standard : TwoWidthBarCode
{
    /// <summary>
    /// Initializes a new instance of Standard3of9.
    /// </summary>
    public Code3of9Standard()
        : base("", XSize.Empty, CodeDirection.LeftToRight)
    { }

    /// <summary>
    /// Initializes a new instance of Standard3of9.
    /// </summary>
    public Code3of9Standard(string code)
        : base(code, XSize.Empty, CodeDirection.LeftToRight)
    { }

    /// <summary>
    /// Initializes a new instance of Standard3of9.
    /// </summary>
    public Code3of9Standard(string code, XSize size)
        : base(code, size, CodeDirection.LeftToRight)
    { }

    /// <summary>
    /// Initializes a new instance of Standard3of9.
    /// </summary>
    public Code3of9Standard(string code, XSize size, CodeDirection direction)
        : base(code, size, direction)
    { }

    /// <summary>
    /// Returns an array of size 9 that represents the wide (true) and narrow (false) lines and spaces
    /// representing the specified digit.
    /// </summary>
    /// <param name="ch">The character to represent.</param>
    private static bool[] WideNarrowLines(char ch)
    {
        return _lines["0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*".IndexOf(ch)];
    }

    private static readonly bool[][] _lines =
    [
        // '0'
        [false, false, false, true, true, false, true, false, false],
        // '1'
        [true, false, false, true, false, false, false, false, true],
        // '2'
        [false, false, true, true, false, false, false, false, true],
        // '3'
        [true, false, true, true, false, false, false, false, false],
        // '4'
        [false, false, false, true, true, false, false, false, true],
        // '5'
        [true, false, false, true, true, false, false, false, false],
        // '6'
        [false, false, true, true, true, false, false, false, false],
        // '7'
        [false, false, false, true, false, false, true, false, true],
        // '8'
        [true, false, false, true, false, false, true, false, false],
        // '9'
        [false, false, true, true, false, false, true, false, false],
        // 'A'
        [true, false, false, false, false, true, false, false, true],
        // 'B'
        [false, false, true, false, false, true, false, false, true],
        // 'C'
        [true, false, true, false, false, true, false, false, false],
        // 'D'
        [false, false, false, false, true, true, false, false, true],
        // 'E'
        [true, false, false, false, true, true, false, false, false],
        // 'F'
        [false, false, true, false, true, true, false, false, false],
        // 'G'
        [false, false, false, false, false, true, true, false, true],
        // 'H'
        [true, false, false, false, false, true, true, false, false],
        // 'I'
        [false, false, true, false, false, true, true, false, false],
        // 'J'
        [false, false, false, false, true, true, true, false, false],
        // 'K'
        [true, false, false, false, false, false, false, true, true],
        // 'L'
        [false, false, true, false, false, false, false, true, true],
        // 'M'
        [true, false, true, false, false, false, false, true, false],
        // 'N'
        [false, false, false, false, true, false, false, true, true],
        // 'O'
        [true, false, false, false, true, false, false, true, false],
        // 'P':
        [false, false, true, false, true, false, false, true, false],
        // 'Q'
        [false, false, false, false, false, false, true, true, true],
        // 'R'
        [true, false, false, false, false, false, true, true, false],
        // 'S'
        [false, false, true, false, false, false, true, true, false],
        // 'T'
        [false, false, false, false, true, false, true, true, false],
        // 'U'
        [true, true, false, false, false, false, false, false, true],
        // 'V'
        [false, true, true, false, false, false, false, false, true],
        // 'W'
        [true, true, true, false, false, false, false, false, false],
        // 'X'
        [false, true, false, false, true, false, false, false, true],
        // 'Y'
        [true, true, false, false, true, false, false, false, false],
        // 'Z'
        [false, true, true, false, true, false, false, false, false],
        // '-'
        [false, true, false, false, false, false, true, false, true],
        // '.'
        [true, true, false, false, false, false, true, false, false],
        // ' '
        [false, true, true, false, false, false, true, false, false],
        // '$'
        [false, true, false, true, false, true, false, false, false],
        // '/'
        [false, true, false, true, false, false, false, true, false],
        // '+'
        [false, true, false, false, false, true, false, true, false],
        // '%'
        [false, false, false, true, false, true, false, true, false],
        // '*'
        [false, true, false, false, true, false, true, false, false]
    ];


    /// <summary>
    /// Calculates the wide and narrow line widths,
    /// taking into account the required rendering size.
    /// </summary>
    internal override void CalcNarrowBarWidth(BarCodeRenderInfo info)
    {
        /*
         * The total width is the sum of the following parts:
         * Starting lines      = 3 * wide + 7 * narrow
         *  +
         * Code Representation = (3 * wide + 7 * narrow) * code.Length
         *  +
         * Stopping lines      =  3 * wide + 6 * narrow
         *
         * with r = relation ( = wide / narrow), this results in
         *
         * Total width = (13 + 6 * r + (3 * r + 7) * code.Length) * narrow
         */
        var narrowLineAmount = 13 + 6 * WideNarrowRatio + (3 * WideNarrowRatio + 7) * Text.Length;
        info.NarrowBarWidth = Size.Width / narrowLineAmount;
    }

    /// <summary>
    /// Checks the code to be convertible into a standard 3 of 9 bar code.
    /// </summary>
    /// <param name="text">The code to be checked.</param>
    protected override void CheckCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
            throw new ArgumentException(BcgSR.Invalid3Of9Code(text));

        foreach (var ch in text)
        {
            if ("0123456789ABCDEFGHIJKLMNOP'QRSTUVWXYZ-. $/+%*".IndexOf(ch) < 0)
                throw new ArgumentException(BcgSR.Invalid3Of9Code(text));
        }
    }

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
        {
            RenderNextChar(info);
            RenderGap(info, false);
        }
        RenderStop(info);
        if (TurboBit)
            RenderTurboBit(info, false);
        if (TextLocation != TextLocation.None)
            RenderText(info);

        gfx.Restore(state);
    }

    private void RenderNextChar(BarCodeRenderInfo info)
    {
        RenderChar(info, Text[info.CurrPosInString]);
        ++info.CurrPosInString;
    }

    private void RenderChar(BarCodeRenderInfo info, char ch)
    {
        var wideNarrowLines = WideNarrowLines(ch);
        var idx = 0;
        while (idx < 9)
        {
            RenderBar(info, wideNarrowLines[idx]);
            if (idx < 8)
                RenderGap(info, wideNarrowLines[idx + 1]);
            idx += 2;
        }
    }

    private void RenderStart(BarCodeRenderInfo info)
    {
        RenderChar(info, '*');
        RenderGap(info, false);
    }

    private void RenderStop(BarCodeRenderInfo info)
    {
        RenderChar(info, '*');
    }
}
