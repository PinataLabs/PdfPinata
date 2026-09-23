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
using System.ComponentModel;
using PdfPinata.Internal;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Represents an OMR code.
/// </summary>
public class CodeOmr : BarCode
{
    /// <summary>
    /// initializes a new OmrCode with the given data.
    /// </summary>
    public CodeOmr(string text, XSize size, CodeDirection direction)
        : base(text, size, direction)
    { }

    /// <summary>
    /// Renders the OMR code.
    /// </summary>
    protected internal override void Render(XGraphics gfx, XBrush brush, XFont font, XPoint position)
    {
        var state = gfx.Save();

        switch (Direction)
        {
            case CodeDirection.RightToLeft:
                gfx.RotateAtTransform(180, position);
                break;

            case CodeDirection.TopToBottom:
                gfx.RotateAtTransform(90, position);
                break;

            case CodeDirection.BottomToTop:
                gfx.RotateAtTransform(-90, position);
                break;
        }

        var pt = position - CalcDistance(AnchorType.TopLeft, Anchor, Size);
        _ = uint.TryParse(Text, out var value);
        // HACK: Project Wallenwein: set LK
        value |= 1;
        _synchronizeCode = true;

        if (_synchronizeCode)
        {
            var rect = new XRect(pt.X, pt.Y, _makerThickness, Size.Height);
            gfx.DrawRectangle(brush, rect);
            pt.X += 2 * _makerDistance;
        }
        for (var idx = 0; idx < 32; idx++)
        {
            if ((value & 1) == 1)
            {
                var rect = new XRect(pt.X + idx * _makerDistance, pt.Y, _makerThickness, Size.Height);
                gfx.DrawRectangle(brush, rect);
            }
            value = value >> 1;
        }
        gfx.Restore(state);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a synchronize mark is rendered.
    /// </summary>
    public bool SynchronizeCode
    {
        get => _synchronizeCode;
        set => _synchronizeCode = value;
    }

    private bool _synchronizeCode;

    /// <summary>
    /// Gets or sets the distance of the markers.
    /// </summary>
    public double MakerDistance
    {
        get => _makerDistance;
        set => _makerDistance = value;
    }

    private double _makerDistance = 12;  // 1/6"

    /// <summary>
    /// Gets or sets the thickness of the makers.
    /// </summary>
    public double MakerThickness
    {
        get => _makerThickness;
        set => _makerThickness = value;
    }

    private double _makerThickness = 1;

    /// <summary>
    /// Gets or sets the distance of the markers as one of the standard distances, or null when
    /// <see cref="MakerDistance"/> is not one of them.
    /// </summary>
    /// <remarks>
    /// A typed way of saying what <see cref="MakerDistance"/> says in points, and it reads and
    /// writes that property rather than keeping a value of its own: assigning
    /// <see cref="MarkDistance.Inch2_6"/> sets <see cref="MakerDistance"/> to 24, and
    /// assigning <see cref="MakerDistance"/> 24 makes this read <see cref="MarkDistance.Inch2_6"/>.
    /// A new code reads <see cref="MarkDistance.Inch1_6"/>, the 12 points
    /// <see cref="MakerDistance"/> has always started at. Null is refused, because there is no
    /// distance it could set.
    /// </remarks>
    public MarkDistance? StandardMarkDistance
    {
        get
        {
            foreach (MarkDistance distance in Enum.GetValues(typeof(MarkDistance)))
            {
                if (DoubleUtil.AreClose(ToUnit(distance).Point, _makerDistance))
                    return distance;
            }
            return null;
        }
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value),
                    "A standard mark distance cannot be cleared; assign MakerDistance for a distance that is not one.");
            _makerDistance = ToUnit(value.Value).Point;
        }
    }

    /// <summary>
    /// Converts a standard mark distance to the length it stands for.
    /// </summary>
    /// <param name="markDistance">The mark distance to convert.</param>
    /// <returns>The distance between two marks.</returns>
    public static XUnit ToUnit(MarkDistance markDistance)
    {
        // In points rather than as fractions of an inch, so that each is exact. A distance
        // assigned through MakerDistance need not be: 25.4 / 6 mm is 12.000000000000002 points,
        // which is why StandardMarkDistance compares within a tolerance rather than exactly.
        switch (markDistance)
        {
            case MarkDistance.Inch1_6:
                return XUnit.FromPoint(12);
            case MarkDistance.Inch2_6:
                return XUnit.FromPoint(24);
            case MarkDistance.Inch2_8:
                return XUnit.FromPoint(18);
            default:
                throw new InvalidEnumArgumentException(nameof(markDistance), (int)markDistance, typeof(MarkDistance));
        }
    }

    /// <summary>
    /// Determines whether the specified string can be used as Text for the OMR code.
    /// </summary>
    protected override void CheckCode(string text)
    { }
}
