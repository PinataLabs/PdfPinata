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

namespace PdfPinata.Drawing;

/// <summary>
/// Represents predefined text layouts.
/// </summary>
public static class XStringFormats
{
    /// <summary>
    /// Gets a new XStringFormat object that aligns the text left on the baseline.
    /// This is the same as BaseLineLeft.
    /// </summary>
    public static XStringFormat Default => BaseLineLeft;

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text left on the baseline.
    /// This is the same as Default.
    /// </summary>
    public static XStringFormat BaseLineLeft
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Near,
                LineAlignment = XLineAlignment.BaseLine
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text top left of the layout rectangle.
    /// </summary>
    public static XStringFormat TopLeft
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Near,
                LineAlignment = XLineAlignment.Near
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text center left of the layout rectangle.
    /// </summary>
    public static XStringFormat CenterLeft
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Near,
                LineAlignment = XLineAlignment.Center
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text bottom left of the layout rectangle.
    /// </summary>
    public static XStringFormat BottomLeft
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Near,
                LineAlignment = XLineAlignment.Far
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that centers the text in the middle of the baseline.
    /// </summary>
    public static XStringFormat BaseLineCenter
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.BaseLine
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that centers the text at the top of the layout rectangle.
    /// </summary>
    public static XStringFormat TopCenter
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.Near
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that centers the text in the middle of the layout rectangle.
    /// </summary>
    public static XStringFormat Center
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.Center
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that centers the text at the bottom of the layout rectangle.
    /// </summary>
    public static XStringFormat BottomCenter
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Center,
                LineAlignment = XLineAlignment.Far
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text in right on the baseline.
    /// </summary>
    public static XStringFormat BaseLineRight
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Far,
                LineAlignment = XLineAlignment.BaseLine
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text top right of the layout rectangle.
    /// </summary>
    public static XStringFormat TopRight
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Far,
                LineAlignment = XLineAlignment.Near
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text center right of the layout rectangle.
    /// </summary>
    public static XStringFormat CenterRight
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Far,
                LineAlignment = XLineAlignment.Center
            };
            return format;
        }
    }

    /// <summary>
    /// Gets a new XStringFormat object that aligns the text at the bottom right of the layout rectangle.
    /// </summary>
    public static XStringFormat BottomRight
    {
        get
        {
            // Create new format to allow changes.
            var format = new XStringFormat
            {
                Alignment = XStringAlignment.Far,
                LineAlignment = XLineAlignment.Far
            };
            return format;
        }
    }
}
