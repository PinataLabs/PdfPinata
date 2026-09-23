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

using System.Diagnostics;
using System.Globalization;
using PdfPinata.Internal;

namespace PdfPinata.Drawing;

/// <summary>
/// Internal implementation class of XFontFamily.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
internal class FontFamilyInternal
{
    // Implementation Notes
    // FontFamilyInternal implements an XFontFamily.
    //
    // * Each XFontFamily object is just a handle to its FontFamilyInternal singleton.
    //
    // * A FontFamilyInternal is is uniquely identified by its name. It
    //    is not possible to use two different fonts that have the same
    //    family name.

    private FontFamilyInternal(string familyName)
    {
        SourceName = Name = familyName;
    }

    internal static FontFamilyInternal GetOrCreateFromName(string familyName, bool createPlatformObject)
    {
        try
        {
            Lock.EnterFontFactory();
            var family = FontFamilyCache.GetFamilyByName(familyName);
            if (family == null)
            {
                family = new FontFamilyInternal(familyName);
                family = FontFamilyCache.CacheOrGetFontFamily(family);
            }
            return family;
        }
        finally { Lock.ExitFontFactory(); }
    }

    /// <summary>
    /// Gets the family name this family was originally created with.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Gets the name that uniquely identifies this font family.
    /// </summary>
    // In WPF this is the Win32FamilyName, not the WPF family name.
    public string Name { get; }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSha rper disable UnusedMember.Local
    internal string DebuggerDisplay => string.Format(CultureInfo.InvariantCulture, "FontFamiliy: '{0}'", Name); // ReShar per restore UnusedMember.Local
}
