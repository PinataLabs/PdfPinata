#region MigraDoc - Creating Documents on the Fly
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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

namespace PinataLayout.Rendering;

/// <summary>
/// Abstract base class for all areas to render in.
/// </summary>
public abstract class Area
{
    internal Area()
    {
    }

    /// <summary>
    /// Gets the left boundary of the area.
    /// </summary>
    public abstract XUnit X
    {
        get;
        set;
    }

    /// <summary>
    /// Gets the top boundary of the area.
    /// </summary>
    public abstract XUnit Y
    {
        get;
        set;
    }

    /// <summary>
    /// Gets the largest fitting rect with the given y position and height.
    /// </summary>
    /// <param name="yPosition">Top bound of the searched rectangle.</param>
    /// <param name="height">Height of the searched rectangle.</param>
    /// <returns>
    /// The largest clear rectangle of that height whose top is at that position, or <c>null</c>
    /// where the area has no room for one.
    /// </returns>
    /// <remarks>
    /// <b>Null means "there is nowhere here to put a line of that height".</b> For a
    /// <see cref="Rectangle"/> that happens in one way only - the band runs off the bottom - which is
    /// why it has been rare enough to leave half-handled. An area with something standing in it can
    /// answer null for a second reason: the band is there, and every part of it is taken.
    /// <para>
    /// A caller that receives null must move on rather than fail. In the formatting phase that means
    /// asking for a new area; in the rendering phase, where the line has already been placed, it
    /// means falling back to the area's own bounds.
    /// </para>
    /// </remarks>
    internal abstract Rectangle GetFittingRect(XUnit yPosition, XUnit height);

    /// <summary>
    /// Gets or sets the height of the smallest rectangle containing the area. 
    /// </summary>
    public abstract XUnit Height
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the width of the smallest rectangle containing the area. 
    /// </summary>
    public abstract XUnit Width
    {
        get;
        set;
    }

    /// <summary>
    /// Returns the union of this area snd the given one.
    /// </summary>
    /// <param name="area">The area to unite with.</param>
    /// <returns>The union of the two areas.</returns>
    internal abstract Area Unite(Area area);

    /// <summary>
    /// Lowers the area and makes it smaller.
    /// </summary>
    /// <param name="verticalOffset">The measure of lowering.</param>
    /// <returns>The lowered Area.</returns>
    internal abstract Area Lower(XUnit verticalOffset);

    /// <summary>
    /// Raises the area's bottom, leaving its top where it is.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="Lower"/>, which moves the top and leaves the bottom. This is what
    /// sets room aside at the foot of a page: the content already placed keeps its position and
    /// everything laid out afterwards has less room to run into.
    /// </remarks>
    /// <param name="verticalOffset">How much to take off the bottom.</param>
    internal abstract Area Shorten(XUnit verticalOffset);
}

internal class Rectangle : Area
{
    /// <summary>
    /// Initializes a new rectangle object.
    /// </summary>
    /// <param name="x">Left bound of the rectangle.</param>
    /// <param name="y">Upper bound of the rectangle.</param>
    /// <param name="width">Width of the rectangle.</param>
    /// <param name="height">Height of the rectangle.</param>
    internal Rectangle(XUnit x, XUnit y, XUnit width, XUnit height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }

    /// <summary>
    /// Initializes a new Rectangle by copying its values.
    /// </summary>
    /// <param name="rect">The rectangle to copy.</param>
    internal Rectangle(Rectangle rect)
    {
        x = rect.x;
        y = rect.y;
        width = rect.width;
        height = rect.height;
    }

    /// <summary>
    /// Gets the largest fitting rect with the given y position and height.
    /// </summary>
    /// <param name="yPosition">Top bound of the searched rectangle.</param>
    /// <param name="height">Height of the searched rectangle.</param>
    /// <returns>The largest fitting rect with the given y position and height</returns>
    internal override Rectangle GetFittingRect(XUnit yPosition, XUnit height)
    {
        // Null past the bottom, which is the only way a rectangle can have nowhere to put a line.
        // The callers honour it: see the remarks on Area.GetFittingRect.
        if (yPosition + height > y + this.height + Renderer.Tolerance)
            return null;
        return new Rectangle(x, yPosition, width, height);
    }

    /// <summary>
    /// Gets or sets the left boundary of the rectangle. 
    /// </summary>
    public override XUnit X
    {
        get => x;
        set => x = value;
    }
    XUnit x;

    /// <summary>
    /// Gets or sets the top boundary of the rectangle. 
    /// </summary>
    public override XUnit Y
    {
        get => y;
        set => y = value;
    }
    XUnit y;

    /// <summary>
    /// Gets or sets the top boundary of the rectangle. 
    /// </summary>
    public override XUnit Width
    {
        get => width;
        set => width = value;
    }
    XUnit width;

    /// <summary>
    /// Gets or sets the height of the rectangle. 
    /// </summary>
    public override XUnit Height
    {
        get => height;
        set => height = value;
    }
    XUnit height;

    /// <summary>
    /// Returns the union of the rectangle and the given area.
    /// </summary>
    /// <param name="area">The area to unite with.</param>
    /// <returns>The union of the two areas.</returns>
    internal override Area Unite(Area area)
    {
        if (area == null)
            return this;
        //This implementation is of course not correct, but it works for our purposes.
        XUnit minTop = Math.Min(y, area.Y);
        XUnit minLeft = Math.Min(x, area.X);
        XUnit maxRight = Math.Max(x + width, area.X + area.Width);
        XUnit maxBottom = Math.Max(y + height, area.Y + area.Height);
        return new Rectangle(minLeft, minTop, maxRight - minLeft, maxBottom - minTop);
    }

    internal override Area Lower(XUnit verticalOffset)
    {
        return new Rectangle(x, y + verticalOffset, width, height - verticalOffset);
    }

    internal override Area Shorten(XUnit verticalOffset)
    {
        return new Rectangle(x, y, width, height - verticalOffset);
    }
}
