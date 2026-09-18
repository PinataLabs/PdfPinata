#region MigraDoc - Creating Documents on the Fly
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Character table by name.
/// </summary>
public sealed class Chars
{
    /// <summary>U+0000, the null character. The scanner reads it as end of input.</summary>
    public const char Null = '\0';
    /// <summary>U+000D, carriage return. The scanner ignores it and takes <see cref="LF"/> as the line break.</summary>
    public const char CR = '\x0D';
    /// <summary>U+000A, line feed.</summary>
    public const char LF = '\x0A';
    /// <summary>U+0007, bell.</summary>
    public const char BEL = '\a';
    /// <summary>U+0008, backspace.</summary>
    public const char BS = '\b';
    /// <summary>U+000C, form feed.</summary>
    public const char FF = '\f';
    /// <summary>U+0009, horizontal tab.</summary>
    public const char HT = '\t';
    /// <summary>U+000B, vertical tab.</summary>
    public const char VT = '\v';
    /// <summary>U+00A0, no-break space: a space a line may not be broken at.</summary>
    public const char NonBreakableSpace = (char)160;

    // The following names come from "PDF Reference Third Edition"
    // Appendix D.1, Latin Character Set and Encoding
    /// <summary>U+0020, space.</summary>
    public const char Space = ' ';
    /// <summary>U+0022, quotation mark.</summary>
    public const char QuoteDbl = '"';
    /// <summary>U+0027, apostrophe.</summary>
    public const char QuoteSingle = '\'';
    /// <summary>U+0028, left parenthesis.</summary>
    public const char ParenLeft = '(';
    /// <summary>U+0029, right parenthesis.</summary>
    public const char ParenRight = ')';
    /// <summary>U+007B, left curly bracket.</summary>
    public const char BraceLeft = '{';
    /// <summary>U+007D, right curly bracket.</summary>
    public const char BraceRight = '}';
    /// <summary>U+005B, left square bracket.</summary>
    public const char BracketLeft = '[';
    /// <summary>U+005D, right square bracket.</summary>
    public const char BracketRight = ']';
    /// <summary>U+003C, less-than sign.</summary>
    public const char Less = '<';
    /// <summary>U+003E, greater-than sign.</summary>
    public const char Greater = '>';
    /// <summary>U+003D, equals sign.</summary>
    public const char Equal = '=';
    /// <summary>U+002E, full stop.</summary>
    public const char Period = '.';
    /// <summary>U+003B, semicolon.</summary>
    public const char Semicolon = ';';
    /// <summary>U+003A, colon.</summary>
    public const char Colon = ':';
    /// <summary>U+002F, solidus.</summary>
    public const char Slash = '/';
    /// <summary>U+007C, vertical line.</summary>
    public const char Bar = '|';
    /// <summary>U+005C, reverse solidus.</summary>
    public const char BackSlash = '\\';
    /// <summary>U+0025, percent sign.</summary>
    public const char Percent = '%';
    /// <summary>U+0024, dollar sign.</summary>
    public const char Dollar = '$';
    /// <summary>U+0040, commercial at.</summary>
    public const char At = '@';
    /// <summary>U+0023, number sign.</summary>
    public const char NumberSign = '#';
    /// <summary>U+003F, question mark.</summary>
    public const char Question = '?';
    /// <summary>U+002D, hyphen-minus: a hyphen that is always drawn.</summary>
    public const char Hyphen = '-';
    /// <summary>U+00AD, soft hyphen: a place a word may be broken at, drawn only when the break is taken.</summary>
    public const char SoftHyphen = '\u00ad';
    /// <summary>U+00A4, currency sign.</summary>
    public const char Currency = '¤';
    /// <summary>U+200B, zero-width space: a place a line may be broken at, drawn as nothing either way.</summary>
    public const char ZeroWidthSpace = '\u200b';
}
