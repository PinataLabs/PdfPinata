#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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


// ReSharper disable InconsistentNaming

namespace PdfPinata.Pdf.Content.Objects;

/// <summary>
/// The names of the op-codes.
/// </summary>
/// <remarks>
/// The names are the operators themselves, so they are case sensitive and unusable from a language
/// that is not — which is what the ReSharper suppression above is for. Where an operator's own
/// spelling is not a legal identifier the name is spelled out: an asterisk becomes a trailing
/// <c>x</c> (<see cref="bx"/> is <c>b*</c>), and the two quote operators are
/// <see cref="QuoteSingle"/> and <see cref="QuoteDbl"/>. The descriptions are those of Table A.1 in
/// the PDF specification.
/// <para>
/// The order of the members is the order they have always had. These values are ordinal, so
/// inserting one anywhere but at the end renumbers the rest.
/// </para>
/// </remarks>
public enum OpCodeName
{
    /// <summary>A name followed by a dictionary. Not an operator; produced by the content parser.</summary>
    Dictionary,

    /// <summary>Close, fill and stroke the path, using the nonzero winding number rule.</summary>
    b,
    /// <summary>Fill and stroke the path, using the nonzero winding number rule.</summary>
    B,
    /// <summary><c>b*</c> — close, fill and stroke the path, using the even-odd rule.</summary>
    bx,
    /// <summary><c>B*</c> — fill and stroke the path, using the even-odd rule.</summary>
    Bx,
    /// <summary>Begin a marked-content sequence with a property list.</summary>
    BDC,
    /// <summary>Begin an inline image object.</summary>
    BI,
    /// <summary>Begin a marked-content sequence.</summary>
    BMC,
    /// <summary>Begin a text object.</summary>
    BT,
    /// <summary>Begin a compatibility section, in which unrecognised operators are ignored.</summary>
    BX,
    /// <summary>Append a cubic Bézier segment to the path, given both control points.</summary>
    c,
    /// <summary>Concatenate a matrix to the current transformation matrix.</summary>
    cm,
    /// <summary>Set the colour space for stroking.</summary>
    CS,
    /// <summary>Set the colour space for filling.</summary>
    cs,
    /// <summary>Set the line dash pattern.</summary>
    d,
    /// <summary>Set the glyph width in a Type 3 font.</summary>
    d0,
    /// <summary>Set the glyph width and bounding box in a Type 3 font.</summary>
    d1,
    /// <summary>Draw the named XObject: an image, or a form drawn as if its content were here.</summary>
    Do,

    /// <summary>Define a marked-content point with a property list.</summary>
    DP,
    /// <summary>End an inline image object.</summary>
    EI,
    /// <summary>End a marked-content sequence.</summary>
    EMC,
    /// <summary>End a text object.</summary>
    ET,
    /// <summary>End a compatibility section.</summary>
    EX,
    /// <summary>Fill the path, using the nonzero winding number rule.</summary>
    f,
    /// <summary>Fill the path. An obsolete synonym for <see cref="f"/>.</summary>
    F,
    /// <summary><c>f*</c> — fill the path, using the even-odd rule.</summary>
    fx,
    /// <summary>Set the grey level for stroking.</summary>
    G,
    /// <summary>Set the grey level for filling.</summary>
    g,
    /// <summary>Set parameters from a graphics state parameter dictionary.</summary>
    gs,
    /// <summary>Close the current subpath by drawing a line back to its start.</summary>
    h,
    /// <summary>Set the flatness tolerance.</summary>
    i,
    /// <summary>Begin the data of an inline image.</summary>
    ID,
    /// <summary>Set the line join style.</summary>
    j,
    /// <summary>Set the line cap style.</summary>
    J,
    /// <summary>Set the CMYK colour for stroking.</summary>
    K,
    /// <summary>Set the CMYK colour for filling.</summary>
    k,
    /// <summary>Append a straight line segment to the path.</summary>
    l,
    /// <summary>Begin a new subpath by moving to the given point.</summary>
    m,
    /// <summary>Set the mitre limit.</summary>
    M,
    /// <summary>Define a marked-content point.</summary>
    MP,

    /// <summary>End the path without filling or stroking it. Used to apply a clip and nothing else.</summary>
    n,
    /// <summary>Save the graphics state onto the stack.</summary>
    q,
    /// <summary>Restore the graphics state from the stack.</summary>
    Q,
    /// <summary>Append a complete rectangle to the path as a new subpath.</summary>
    re,
    /// <summary>Set the RGB colour for stroking.</summary>
    RG,
    /// <summary>Set the RGB colour for filling.</summary>
    rg,
    /// <summary>Set the colour rendering intent.</summary>
    ri,
    /// <summary>Close and stroke the path.</summary>
    s,
    /// <summary>Stroke the path.</summary>
    S,
    /// <summary>Set the colour for stroking, in the current colour space.</summary>
    SC,
    /// <summary>Set the colour for filling, in the current colour space.</summary>
    sc,
    /// <summary>Set the colour for stroking, for ICCBased and special colour spaces.</summary>
    SCN,
    /// <summary>Set the colour for filling, for ICCBased and special colour spaces.</summary>
    scn,
    /// <summary>Paint the area defined by a shading pattern.</summary>
    sh,

    /// <summary><c>T*</c> — move to the start of the next line.</summary>
    Tx,
    /// <summary>Set the character spacing.</summary>
    Tc,
    /// <summary>Move to the start of the next line, offset by the given amount.</summary>
    Td,
    /// <summary>Move to the start of the next line and set the leading to the vertical offset.</summary>
    TD,
    /// <summary>Set the text font and its size.</summary>
    Tf,
    /// <summary>Show a string.</summary>
    Tj,
    /// <summary>Show strings, adjusting the position between them so glyphs can be placed individually.</summary>
    TJ,
    /// <summary>Set the text leading: the distance between the baselines of consecutive lines.</summary>
    TL,
    /// <summary>Set the text matrix and the text line matrix.</summary>
    Tm,
    /// <summary>Set the text rendering mode: filled, stroked, both, invisible or clipping.</summary>
    Tr,
    /// <summary>Set the text rise, which lifts or drops the baseline for superscripts and subscripts.</summary>
    Ts,
    /// <summary>Set the word spacing, which is added to the width of each single-byte space.</summary>
    Tw,
    /// <summary>Set the horizontal text scaling, as a percentage of normal width.</summary>
    Tz,
    /// <summary>Append a cubic Bézier segment to the path, the current point serving as the first control point.</summary>
    v,
    /// <summary>Set the line width.</summary>
    w,
    /// <summary>Intersect the clipping path with the current path, using the nonzero winding number rule.</summary>
    W,
    /// <summary><c>W*</c> — intersect the clipping path with the current path, using the even-odd rule.</summary>
    Wx,
    /// <summary>Append a cubic Bézier segment to the path, the end point serving as the second control point.</summary>
    y,

    /// <summary><c>'</c> — move to the next line and show a string.</summary>
    QuoteSingle,
    /// <summary><c>"</c> — set the word and character spacing, move to the next line and show a string.</summary>
    QuoteDbl,
}
