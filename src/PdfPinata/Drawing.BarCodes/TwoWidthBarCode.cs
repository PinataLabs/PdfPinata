//
// PDFsharp - A library for processing PDF
//
// Authors:
//   Klaus Potzesny
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using PdfPinata.Fonts;
using System;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Base class for the two-width bar codes, whose bars and gaps are each either narrow or wide.
/// </summary>
public abstract class TwoWidthBarCode : BarCode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TwoWidthBarCode"/> class.
    /// </summary>
    public TwoWidthBarCode(string code, XSize size, CodeDirection direction)
        : base(code, size, direction)
    { }

    internal override void InitRendering(BarCodeRenderInfo info)
    {
        base.InitRendering(info);
        CalcNarrowBarWidth(info);
        info.BarHeight = Size.Height;
        // HACK in TwoWidthBarCode
        if (TextLocation != TextLocation.None)
            info.BarHeight *= 4.0 / 5;
        switch (Direction)
        {
            case CodeDirection.RightToLeft:
                info.Gfx.RotateAtTransform(180, info.Position);
                break;

            case CodeDirection.TopToBottom:
                info.Gfx.RotateAtTransform(90, info.Position);
                break;

            case CodeDirection.BottomToTop:
                info.Gfx.RotateAtTransform(-90, info.Position);
                break;
        }
    }

    /// <summary>
    /// Gets or sets the ratio between wide and narrow lines. Must be between 2 and 3.
    /// Optimal and also default value is 2.6.
    /// </summary>
    public override double WideNarrowRatio
    {
        get => _wideNarrowRatio;
        set
        {
            if (value is > 3 or < 2)
                throw new ArgumentOutOfRangeException(nameof(value), BcgSR.InvalidWideNarrowRatio);
            _wideNarrowRatio = value;
        }
    }

    private double _wideNarrowRatio = 2.6;

    /// <summary>
    /// Renders a wide or narrow line for the bar code.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="isWide">Determines whether a wide or a narrow line is about to be rendered.</param>
    internal void RenderBar(BarCodeRenderInfo info, bool isWide)
    {
        var barWidth = GetBarWidth(info, isWide);
        var height = Size.Height;
        var xPos = info.CurrPos.X;
        var yPos = info.CurrPos.Y;

        switch (TextLocation)
        {
            case TextLocation.AboveEmbedded:
                height -= info.Gfx.MeasureString(Text, info.Font).Height;
                yPos += info.Gfx.MeasureString(Text, info.Font).Height;
                break;
            case TextLocation.BelowEmbedded:
                height -= info.Gfx.MeasureString(Text, info.Font).Height;
                break;
        }

        var rect = new XRect(xPos, yPos, barWidth, height);
        info.Gfx.DrawRectangle(info.Brush, rect);
        info.CurrPos.X += barWidth;
    }

    /// <summary>
    /// Renders a wide or narrow gap for the bar code.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="isWide">Determines whether a wide or a narrow gap is about to be rendered.</param>
    internal void RenderGap(BarCodeRenderInfo info, bool isWide)
    {
        info.CurrPos.X += GetBarWidth(info, isWide);
    }

    /// <summary>
    /// Renders a wide bar before or behind the code.
    /// </summary>
    internal void RenderTurboBit(BarCodeRenderInfo info, bool startBit)
    {
        if (startBit)
            info.CurrPos.X -= 0.5 + GetBarWidth(info, true);
        else
            info.CurrPos.X += 0.5; //GetBarWidth(info, true);

        RenderBar(info, true);

        if (startBit)
            info.CurrPos.X += 0.5; //GetBarWidth(info, true);
    }

    internal void RenderText(BarCodeRenderInfo info)
    {
        info.Font ??= new XFont(GlobalFontSettings.FontResolver.DefaultFontName, Size.Height / 6);
        var center = info.Position + CalcDistance(Anchor, AnchorType.TopLeft, Size);

        switch (TextLocation)
        {
            case TextLocation.Above:
                center = new XPoint(center.X, center.Y - info.Gfx.MeasureString(Text, info.Font).Height);
                info.Gfx.DrawString(Text, info.Font, info.Brush, new XRect(center, Size), XStringFormats.TopCenter);
                break;
            case TextLocation.AboveEmbedded:
                info.Gfx.DrawString(Text, info.Font, info.Brush, new XRect(center, Size), XStringFormats.TopCenter);
                break;
            case TextLocation.Below:
                center = new XPoint(center.X, info.Gfx.MeasureString(Text, info.Font).Height + center.Y);
                info.Gfx.DrawString(Text, info.Font, info.Brush, new XRect(center, Size), XStringFormats.BottomCenter);
                break;
            case TextLocation.BelowEmbedded:
                info.Gfx.DrawString(Text, info.Font, info.Brush, new XRect(center, Size), XStringFormats.BottomCenter);
                break;
        }
    }

    /// <summary>
    /// Gets the width of a wide or a narrow line (or gap). CalcNarrowBarWidth must have been called before.
    /// </summary>
    /// <param name="info"></param>
    /// <param name="isWide">Determines whether the width of a wide line (or gap) is returned.</param>
    internal double GetBarWidth(BarCodeRenderInfo info, bool isWide)
    {
        if (isWide)
            return info.NarrowBarWidth * _wideNarrowRatio;
        return info.NarrowBarWidth;
    }

    internal abstract void CalcNarrowBarWidth(BarCodeRenderInfo info);
}
