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

// #??? Clean up

using System;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Pdf;

namespace PdfPinata.Drawing;

/// <summary>
/// Defines an object used to draw text.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public sealed class XFont
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XFont"/> class.
    /// </summary>
    /// <param name="familyName">Name of the font family.</param>
    /// <param name="emSize">The em size.</param>
    public XFont(string familyName, double emSize)
        : this(familyName, emSize, XFontStyle.Regular, new XPdfFontOptions(GlobalFontSettings.DefaultFontEncoding))
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="XFont"/> class.
    /// </summary>
    /// <param name="familyName">Name of the font family.</param>
    /// <param name="emSize">The em size.</param>
    /// <param name="style">The font style.</param>
    public XFont(string familyName, double emSize, XFontStyle style)
        : this(familyName, emSize, style, new XPdfFontOptions(GlobalFontSettings.DefaultFontEncoding))
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="XFont"/> class.
    /// </summary>
    /// <param name="familyName">Name of the font family.</param>
    /// <param name="emSize">The em size.</param>
    /// <param name="style">The font style.</param>
    /// <param name="pdfOptions">Additional PDF options.</param>
    public XFont(string familyName, double emSize, XFontStyle style, XPdfFontOptions pdfOptions)
    {
        FamilyName = familyName;
        Size = emSize;
        Style = style;
        _pdfOptions = pdfOptions;
        Initialize();
    }

    internal XFont(string familyName, double emSize, XFontStyle style, XPdfFontOptions pdfOptions, XStyleSimulations styleSimulations)
    {
        FamilyName = familyName;
        Size = emSize;
        Style = style;
        _pdfOptions = pdfOptions;
        OverrideStyleSimulations = true;
        StyleSimulations = styleSimulations;
        Initialize();
    }

    /// <summary>
    /// Initializes this instance by computing the glyph typeface, font family, font source and TrueType fontface.
    /// (PdfPinata currently only deals with TrueType fonts.)
    /// </summary>
    private void Initialize()
    {
        var fontResolvingOptions = OverrideStyleSimulations
            ? new FontResolvingOptions(Style, StyleSimulations)
            : new FontResolvingOptions(Style);

        // HACK: 'PlatformDefault' is used in unit test code.
        if (StringComparer.OrdinalIgnoreCase.Compare(FamilyName, GlobalFontSettings.DefaultFontName) == 0)
        {
        }

        // In principle an XFont is an XGlyphTypeface plus an em-size.
        _glyphTypeface = XGlyphTypeface.GetOrCreateFrom(FamilyName, fontResolvingOptions);
        CreateDescriptorAndInitializeFontMetrics();
    }

    /// <summary>
    /// Code separated from Metric getter to make code easier to debug.
    /// (Setup properties in their getters caused side effects during debugging because Visual Studio calls a getter
    /// to early to show its value in a debugger window.)
    /// </summary>
    private void CreateDescriptorAndInitializeFontMetrics()
    {
        Debug.Assert(_fontMetrics == null, "InitializeFontMetrics() was already called.");
        _descriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptorFor(this);
        _fontMetrics = new XFontMetrics(_descriptor.FontName, _descriptor.UnitsPerEm, _descriptor.Ascender, _descriptor.Descender,
            _descriptor.Leading, _descriptor.LineSpacing, _descriptor.CapHeight, _descriptor.XHeight, _descriptor.StemV, 0, 0, 0,
            _descriptor.UnderlinePosition, _descriptor.UnderlineThickness, _descriptor.StrikeoutPosition, _descriptor.StrikeoutSize);

        var fm = Metrics;

        UnitsPerEm = _descriptor.UnitsPerEm;
        CellAscent = _descriptor.Ascender;
        CellDescent = _descriptor.Descender;
        CellSpace = _descriptor.LineSpacing;
        Debug.Assert(fm.UnitsPerEm == _descriptor.UnitsPerEm);
    }



    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    /// <summary>
    /// Gets the XFontFamily object associated with this XFont object.
    /// </summary>
    [Browsable(false)]
    public XFontFamily FontFamily => _glyphTypeface.FontFamily;

    /// <summary>
    /// Gets the name of the font family this font belongs to, the same as
    /// <c>FontFamily.Name</c>. It is not the face name: "Arial" rather than "Arial Bold".
    /// </summary>
    public string Name => _glyphTypeface.FontFamily.Name;

    internal string FaceName => _glyphTypeface.FaceName;

    /// <summary>
    /// Gets the em-size of this font measured in the unit of this font object.
    /// </summary>
    public double Size { get; }

    /// <summary>
    /// Gets style information for this Font object.
    /// </summary>
    [Browsable(false)]
    public XFontStyle Style { get; }

    /// <summary>
    /// Indicates whether this XFont object is bold.
    /// </summary>
    public bool Bold => (Style & XFontStyle.Bold) == XFontStyle.Bold;

    /// <summary>
    /// Indicates whether this XFont object is italic.
    /// </summary>
    public bool Italic => (Style & XFontStyle.Italic) == XFontStyle.Italic;

    /// <summary>
    /// Indicates whether this XFont object is stroke out.
    /// </summary>
    public bool Strikeout => (Style & XFontStyle.Strikeout) == XFontStyle.Strikeout;

    /// <summary>
    /// Indicates whether this XFont object is underlined.
    /// </summary>
    public bool Underline => (Style & XFontStyle.Underline) == XFontStyle.Underline;

    /// <summary>
    /// Temporary HACK for XPS to PDF converter.
    /// </summary>
    internal bool IsVertical { get; set; }


    /// <summary>
    /// Gets the PDF options of the font.
    /// </summary>
    public XPdfFontOptions PdfOptions => _pdfOptions ??= new XPdfFontOptions();

    private XPdfFontOptions _pdfOptions;

    /// <summary>
    /// Indicates whether this XFont is encoded as Unicode.
    /// </summary>
    internal bool Unicode => _pdfOptions is { FontEncoding: PdfFontEncoding.Unicode };

    /// <summary>
    /// Gets the cell space for the font. The CellSpace is the line spacing, the sum of CellAscent and CellDescent and optionally some extra space.
    /// </summary>
    public int CellSpace { get; internal set; }

    /// <summary>
    /// Gets the cell ascent, the area above the base line that is used by the font.
    /// </summary>
    public int CellAscent { get; internal set; }

    /// <summary>
    /// Gets the cell descent, the area below the base line that is used by the font.
    /// </summary>
    public int CellDescent { get; internal set; }

    /// <summary>
    /// Gets the font metrics.
    /// </summary>
    /// <value>The metrics.</value>
    public XFontMetrics Metrics
    {
        get
        {
            Debug.Assert(_fontMetrics != null, "InitializeFontMetrics() not yet called.");
            return _fontMetrics;
        }
    }

    private XFontMetrics _fontMetrics;

    /// <summary>
    /// Returns the line spacing, in pixels, of this font. The line spacing is the vertical distance
    /// between the base lines of two consecutive lines of text. Thus, the line spacing includes the
    /// blank space between lines along with the height of the character itself.
    /// </summary>
    public double GetHeight()
    {
        var value = CellSpace * Size / UnitsPerEm;
        return value;
    }

    /// <summary>
    /// Gets the line spacing of this font.
    /// </summary>
    [Browsable(false)]
    public int Height =>
        // Implementation from System.Drawing.Font.cs
        (int)Math.Ceiling(GetHeight());

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    internal XGlyphTypeface GlyphTypeface => _glyphTypeface;

    private XGlyphTypeface _glyphTypeface;

    /// <summary>
    /// This font as a <see cref="T:PdfPinata.Fonts.ITextShaper"/> is shown it: the resolved
    /// face, its bytes, and the size to shape at.
    /// </summary>
    /// <remarks>
    /// A separate type rather than the <see cref="XFont"/> itself because everything a shaper
    /// needs from a font - the typeface, the face name, the font source - lives on types this
    /// assembly keeps internal, and a public seam cannot hand over what a caller cannot read.
    /// Built once and kept, so that a shaper called for every word of a paragraph is handed the
    /// same instance each time and can cache its own face against it.
    /// </remarks>
    internal ShapingFont ShapingFont => field ??= new ShapingFont(
        _glyphTypeface.FamilyName, _glyphTypeface.FaceName, _glyphTypeface.Key,
        _glyphTypeface.IsBold, _glyphTypeface.IsItalic,
        Size, UnitsPerEm, _glyphTypeface.FontSource.Bytes);


    internal OpenTypeDescriptor Descriptor => _descriptor;

    private OpenTypeDescriptor _descriptor;


    internal string FamilyName { get; }


    internal int UnitsPerEm { get; private set; }

    /// <summary>
    /// Override style simulations by using the value of StyleSimulations.
    /// </summary>
    internal bool OverrideStyleSimulations;

    /// <summary>
    /// Used to enforce style simulations by renderer. For development purposes only.
    /// </summary>
    internal XStyleSimulations StyleSimulations;

    /// <summary>
    /// Cache PdfFontTable.FontSelector to speed up finding the right PdfFont
    /// if this font is used more than once.
    /// </summary>
    internal string Selector { get; set; }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSharper disable UnusedMember.Local
    private string DebuggerDisplay => string.Format(CultureInfo.InvariantCulture, "font=('{0}' {1:0.##})", Name, Size); // ReSharper restore UnusedMember.Local
}
