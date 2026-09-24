#region Copyright
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
using PdfPinata.Drawing;

namespace PdfPinata;

/// <summary>
/// Works out the transform that carries one rectangle into another under a set of resize
/// options. This is the whole of the arithmetic behind a page resize, kept apart from the PDF
/// it is applied to so that it can be reasoned about and tested on its own.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is in PDF user space: the origin is the <b>bottom</b> left corner and y runs
/// <b>up</b> the page.
/// </para>
/// <para>
/// <see cref="XRect"/> comes from a world where y runs the other way, so its
/// <see cref="XRect.Top"/> is the side with the smaller y and its <see cref="XRect.Bottom"/> the
/// side with the larger. Reading either of them here would be wrong in a way that looks right.
/// Only <see cref="XRect.X"/>, <see cref="XRect.Y"/>, <see cref="XRect.Width"/> and
/// <see cref="XRect.Height"/> are used, and <c>(X, Y)</c> is taken to be the corner where both
/// coordinates are least - the bottom left.
/// </para>
/// </remarks>
public static class PageFit
{
    /// <summary>
    /// The transform carrying <paramref name="source"/> onto <paramref name="target"/> under the
    /// options given.
    /// </summary>
    /// <param name="source">
    /// The rectangle the content occupies now, in the coordinates the content is drawn in. Need
    /// not be at the origin - a media box is allowed one elsewhere and real documents have them.
    /// </param>
    /// <param name="target">The rectangle the content is to occupy.</param>
    /// <param name="options">How to fit the one into the other. Null is treated as the default.</param>
    public static XMatrix Calculate(XRect source, XRect target, PageResizeOptions options)
    {
        return Calculate(source, target, options, out _);
    }

    /// <summary>
    /// The transform carrying <paramref name="source"/> onto <paramref name="target"/> under the
    /// options given, also reporting whether the content was turned a quarter to get there.
    /// </summary>
    /// <param name="source">The rectangle the content occupies now.</param>
    /// <param name="target">The rectangle the content is to occupy.</param>
    /// <param name="options">How to fit the one into the other. Null is treated as the default.</param>
    /// <param name="turned">
    /// True when <see cref="PageResizeOptions.AutoRotate"/> applied and the content was turned a
    /// quarter clockwise, which is the same direction a /Rotate entry of 90 turns a page.
    /// </param>
    public static XMatrix Calculate(XRect source, XRect target, PageResizeOptions options, out bool turned)
    {
        options ??= PageResizeOptions.Default;

        // A rectangle carrying a NaN passes every comparison below - NaN is neither greater than
        // nor less than anything - and comes out the far end as a transform made of NaNs, which
        // draws a page holding nothing at all and says nothing about why. Refuse it here, where
        // there is still something to say.
        if (!HasFiniteArea(source))
            throw new ArgumentException("The source rectangle has no area to scale from.", nameof(source));

        if (!HasFiniteArea(target))
            throw new ArgumentException("The target rectangle has no area to scale into.", nameof(target));

        // Take the margin off the target first: everything below fits into what is left of it.
        GetMarginBox(target, options, out var boxX, out var boxY, out var boxWidth, out var boxHeight);

        // A quarter turn is worth making only when the two boxes are of opposite shape. A square
        // is of neither shape, so it never provokes one.
        turned = options.AutoRotate && IsLandscape(source.Width, source.Height) != IsLandscape(boxWidth, boxHeight);

        // What the content measures once it has been turned, which is what has to be fitted.
        var fitWidth = turned ? source.Height : source.Width;
        var fitHeight = turned ? source.Width : source.Height;

        GetScale(options.Fit, fitWidth, fitHeight, boxWidth, boxHeight, out var scaleX, out var scaleY);

        // Whatever the box has over after the content is in it. Negative where the content
        // overflows, which Fill and None both allow, and then the alignment says what is cropped
        // rather than where the slack goes.
        var slackX = boxWidth - fitWidth * scaleX;
        var slackY = boxHeight - fitHeight * scaleY;

        var offsetX = slackX * HorizontalFactor(options.Alignment);
        var offsetY = slackY * VerticalFactor(options.Alignment);

        var placedX = boxX + offsetX;
        var placedY = boxY + offsetY;

        return turned
            ? TurnedPlacement(source, scaleX, scaleY, placedX, placedY)
            : Placement(source, scaleX, scaleY, placedX, placedY);
    }

    /// <summary>
    /// The transform that scales the content and puts its corner at the place given.
    /// </summary>
    private static XMatrix Placement(XRect source, double scaleX, double scaleY, double placedX, double placedY)
    {
        // A point of the content at (x, y) is to end up at
        //     (placedX + scaleX * (x - source.X), placedY + scaleY * (y - source.Y))
        // and XMatrix multiplies row vectors, so that
        //     x' = x * M11 + y * M21 + OffsetX
        //     y' = x * M12 + y * M22 + OffsetY
        // which is the same order the six numbers of a PDF cm operator go in.
        return new XMatrix(
            scaleX, 0,
            0, scaleY,
            placedX - scaleX * source.X,
            placedY - scaleY * source.Y);
    }

    /// <summary>
    /// The transform that turns the content a quarter clockwise, scales it and puts its corner at
    /// the place given.
    /// </summary>
    private static XMatrix TurnedPlacement(XRect source, double scaleX, double scaleY, double placedX, double placedY)
    {
        // Turned a quarter clockwise: the corner that was at the top left ends up at the top
        // right, which is where a /Rotate entry of 90 puts it too. Working the composition
        // through - move the source to the origin, turn, push the turned content back into the
        // positive quadrant, scale, then place - leaves:
        //     x' =  scaleX * y + (placedX - scaleX * source.Y)
        //     y' = -scaleY * x + (placedY + scaleY * (source.Width + source.X))
        return new XMatrix(
            0, -scaleY,
            scaleX, 0,
            placedX - scaleX * source.Y,
            placedY + scaleY * (source.Width + source.X));
    }

    /// <summary>
    /// Whether the rectangle is made of real lengths and encloses some area.
    /// </summary>
    private static bool HasFiniteArea(XRect rect)
    {
        return IsFinite(rect.X) && IsFinite(rect.Y) && IsFinite(rect.Width) && IsFinite(rect.Height) &&
               rect.Width > 0 && rect.Height > 0;
    }

    /// <summary>
    /// What is left of the target once the margin is taken off every side of it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The margin is not a length, or leaves no room in the target.
    /// </exception>
    private static void GetMarginBox(XRect target, PageResizeOptions options,
        out double boxX, out double boxY, out double boxWidth, out double boxHeight)
    {
        var margin = options.Margin.Point;
        if (!IsFinite(margin) || margin < 0)
            throw new ArgumentException("The margin is not a length.", nameof(options));

        boxWidth = target.Width - 2 * margin;
        boxHeight = target.Height - 2 * margin;
        if (boxWidth <= 0 || boxHeight <= 0)
        {
            throw new ArgumentException(
                "The margin leaves no room in the target rectangle for the content to go.", nameof(options));
        }

        boxX = target.X + margin;
        boxY = target.Y + margin;
    }

    /// <summary>
    /// Whether the value is a real length rather than a NaN or an infinity.
    /// </summary>
    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// Whether a box of these proportions is wider than it is high. A square is not, so two
    /// squares - or a square and anything else - are never of opposite shape.
    /// </summary>
    private static bool IsLandscape(double width, double height)
    {
        return width > height;
    }

    private static void GetScale(PageFitMode fit, double fitWidth, double fitHeight, double boxWidth, double boxHeight,
        out double scaleX, out double scaleY)
    {
        var byWidth = boxWidth / fitWidth;
        var byHeight = boxHeight / fitHeight;

        switch (fit)
        {
            case PageFitMode.Fit:
                scaleX = scaleY = Math.Min(byWidth, byHeight);
                break;

            case PageFitMode.Fill:
                scaleX = scaleY = Math.Max(byWidth, byHeight);
                break;

            case PageFitMode.Stretch:
                scaleX = byWidth;
                scaleY = byHeight;
                break;

            case PageFitMode.None:
                scaleX = scaleY = 1;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(fit), fit, "Unknown page fit mode.");
        }
    }

    /// <summary>
    /// How much of the horizontal slack goes to the left of the content: none of it when the
    /// content is against the left, half when centred, all of it when against the right.
    /// </summary>
    private static double HorizontalFactor(PageAlignment alignment)
    {
        return alignment switch
        {
            PageAlignment.TopLeft or PageAlignment.MiddleLeft or PageAlignment.BottomLeft => 0,
            PageAlignment.TopCenter or PageAlignment.MiddleCenter or PageAlignment.BottomCenter => 0.5,
            PageAlignment.TopRight or PageAlignment.MiddleRight or PageAlignment.BottomRight => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown page alignment.")
        };
    }

    /// <summary>
    /// How much of the vertical slack goes below the content. Y runs up the page, so the content
    /// is against the top when all of the slack is underneath it.
    /// </summary>
    private static double VerticalFactor(PageAlignment alignment)
    {
        return alignment switch
        {
            PageAlignment.BottomLeft or PageAlignment.BottomCenter or PageAlignment.BottomRight => 0,
            PageAlignment.MiddleLeft or PageAlignment.MiddleCenter or PageAlignment.MiddleRight => 0.5,
            PageAlignment.TopLeft or PageAlignment.TopCenter or PageAlignment.TopRight => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unknown page alignment.")
        };
    }
}
