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

using System;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents the text layout information.
/// </summary>
public class XStringFormat
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XStringFormat"/> class.
    /// </summary>
    public XStringFormat()
    {
    }

    /// <summary>
    /// Gets or sets horizontal text alignment information.
    /// </summary>
    public XStringAlignment Alignment
    {
        get => _alignment;
        set => _alignment = value;
    }

    private XStringAlignment _alignment;

    /// <summary>
    /// Gets or sets the line alignment.
    /// </summary>
    public XLineAlignment LineAlignment
    {
        get => _lineAlignment;
        set => _lineAlignment = value;
    }

    private XLineAlignment _lineAlignment;

    // ----- Text state ---------------------------------------------------------------------------
    //
    // The properties below are the PDF text state parameters of PDF 32000-1 section 9.3. They are
    // carried here rather than on their own type because DrawString and MeasureString already pass
    // an XStringFormat all the way down to the renderer, so nothing else has to change shape to
    // reach it.
    //
    // All of them widen or move a glyph run, so FontHelper.MeasureString has to apply them or
    // measurement and drawing disagree at every line end.

    /// <summary>
    /// Gets or sets the extra space added after each glyph, in points.
    /// Negative values tighten the text. The default is 0.
    /// This is the Tc operand of PDF 32000-1 section 9.3.2.
    /// </summary>
    public double CharacterSpacing
    {
        get => _characterSpacing;
        set => _characterSpacing = value;
    }

    private double _characterSpacing;

    /// <summary>
    /// Gets or sets the extra space added after each space character, in points.
    /// Negative values tighten the text. The default is 0.
    /// This is the Tw operand of PDF 32000-1 section 9.3.3, and applies on top of
    /// <see cref="CharacterSpacing"/>, which a space receives as well.
    /// </summary>
    public double WordSpacing
    {
        get => _wordSpacing;
        set => _wordSpacing = value;
    }

    private double _wordSpacing;

    /// <summary>
    /// Gets or sets the horizontal scaling of the text, as a percentage. The default is 100.
    /// This is the Tz operand of PDF 32000-1 section 9.3.4, and scales the glyphs, the character
    /// spacing and the word spacing alike.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not greater than zero.</exception>
    public double HorizontalScaling
    {
        get => _horizontalScaling;
        set
        {
            // PDF permits any number here, but a run of zero or negative width has no sensible
            // measurement and would break every line-breaking decision taken from it. Mirrored
            // text is XGraphics.ScaleTransform(-1, 1), not a negative scaling.
            //
            // Asked the way round that rejects NaN as well: NaN fails every comparison, so
            // "value <= 0" would have let it through to be written into the content stream, and a
            // page carrying "NaN Tz" is one no reader will open.
            if (!(value > 0))
                throw new ArgumentOutOfRangeException(nameof(value), value, "HorizontalScaling must be greater than zero.");
            _horizontalScaling = value;
        }
    }

    private double _horizontalScaling = 100;

    /// <summary>
    /// Gets or sets the distance the text is raised above its baseline, in points.
    /// Negative values lower it. The default is 0.
    /// This is the Ts operand of PDF 32000-1 section 9.3.5, and is how superscript and subscript
    /// are expressed. It moves the text without changing how wide it is.
    /// </summary>
    public double TextRise
    {
        get => _textRise;
        set => _textRise = value;
    }

    private double _textRise;

    /// <summary>
    /// Gets or sets which way the text runs. The default is
    /// <see cref="Text.BidiParagraphDirection.Automatic"/>, which reads it off the text itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the paragraph level of the Unicode Bidirectional Algorithm, rules P2 and P3. Left to
    /// itself the algorithm takes the direction from the first strong character in the string, which
    /// is right far more often than not and wrong in the cases that matter: a right-to-left
    /// paragraph opening with a Latin brand name, or a quotation, or a number. Setting it says what
    /// the text is rather than leaving it to be guessed.
    /// </para>
    /// <para>
    /// It does not turn the string round on its own. The characters of a run are ordered by the
    /// algorithm either way; what this changes is where the neutral characters and the runs of the
    /// other direction end up, which is exactly what the paragraph level decides.
    /// </para>
    /// </remarks>
    public Text.BidiParagraphDirection TextDirection
    {
        get => _textDirection;
        set => _textDirection = value;
    }

    private Text.BidiParagraphDirection _textDirection;

    /// <summary>
    /// Gets or sets the angle, in degrees, by which the text is slanted to the right.
    /// Negative values slant it to the left. The default is 0.
    /// This is a skew of the text matrix rather than a text state parameter, and like
    /// <see cref="TextRise"/> it does not change how wide the text is.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The absolute value is 90 or more.</exception>
    public double ObliqueAngle
    {
        get => _obliqueAngle;
        set
        {
            // At 90 degrees the skew is infinite and the glyphs collapse to a line. Asked the way
            // round that rejects NaN too, which would otherwise reach Math.Tan and put NaN into a
            // text matrix.
            if (value is not (> -90 and < 90))
                throw new ArgumentOutOfRangeException(nameof(value), value, "ObliqueAngle must be greater than -90 and less than 90 degrees.");
            _obliqueAngle = value;
        }
    }

    private double _obliqueAngle;

    // ----- decoration ---------------------------------------------------------------------------
    //
    // Underlining and striking out are also settable through XFontStyle, which is where they were
    // before these existed and where they remain. A style set here wins; leaving it at None keeps
    // whatever the font's style asks for, so nothing that already underlines its text stops.

    /// <summary>
    /// Gets or sets the rule drawn under the text. <see cref="XTextDecoration.None"/>, the
    /// default, falls back to <see cref="XFontStyle.Underline"/> on the font.
    /// </summary>
    public XTextDecoration Underline
    {
        get => _underline;
        set => _underline = value;
    }

    private XTextDecoration _underline;

    /// <summary>
    /// Gets or sets the rule drawn through the text. <see cref="XTextDecoration.None"/>, the
    /// default, falls back to <see cref="XFontStyle.Strikeout"/> on the font.
    /// </summary>
    public XTextDecoration Strikeout
    {
        get => _strikeout;
        set => _strikeout = value;
    }

    private XTextDecoration _strikeout;

    /// <summary>
    /// Gets or sets the colour of the underline and strikeout rules. Empty, the default, draws
    /// them in the colour of the text.
    /// </summary>
    /// <remarks>
    /// PinataLayout has never offered this - its underline is always the colour of the font
    /// (<c>ParagraphRenderer</c>) - and neither does PDFKit. It is here because a rule that cannot
    /// be told apart from the text is the one thing a caller cannot achieve by other means.
    /// </remarks>
    public XColor DecorationColor
    {
        get => _decorationColor;
        set => _decorationColor = value;
    }

    private XColor _decorationColor = XColor.Empty;

    /// <summary>
    /// Returns true if every text state property still holds its default, and the text can
    /// therefore be drawn without writing any text state operator.
    /// </summary>
    #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
    internal bool IsDefaultTextState =>
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        _characterSpacing == 0 && _wordSpacing == 0 && _horizontalScaling == 100 &&
        _textRise == 0 && _obliqueAngle == 0;
    #pragma warning restore S1244
}
