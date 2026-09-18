#region PDFsharp - A .NET library for processing PDF
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

namespace PdfPinata;

/// <summary>
/// Identifies the most popular predefined page sizes.
/// </summary>
public enum PageSize
{
    /// <summary>
    /// The width or height of the page are set manually and override the PageSize property.
    /// </summary>
    Undefined = 0,

    // ISO formats (link is dead in the meantime)
    // see http://www.engineeringtoolbox.com/drawings-paper-sheets-sizes-25_349.html

    /// <summary>
    /// Identifies a paper sheet size of 841 mm times 1189 mm or 33.11 inch times 46.81 inch.
    /// </summary>
    A0 = 1,

    /// <summary>
    /// Identifies a paper sheet size of 594 mm times 841 mm or 23.39 inch times 33.1 inch.
    /// </summary>
    A1 = 2,

    /// <summary>
    /// Identifies a paper sheet size of 420 mm times 594 mm or 16.54 inch times 23.29 inch.
    /// </summary>
    A2 = 3,

    /// <summary>
    /// Identifies a paper sheet size of 297 mm times 420 mm or 11.69 inch times 16.54 inch.
    /// </summary>
    A3 = 4,

    /// <summary>
    /// Identifies a paper sheet size of 210 mm times 297 mm or 8.27 inch times 11.69 inch.
    /// </summary>
    A4 = 5,

    /// <summary>
    /// Identifies a paper sheet size of 148 mm times 210 mm or 5.83 inch times 8.27 inch.
    /// </summary>
    A5 = 6,

    /// <summary>
    /// Identifies a paper sheet size of 860 mm times 1220 mm.
    /// </summary>
    RA0 = 7,

    /// <summary>
    /// Identifies a paper sheet size of 610 mm times 860 mm.
    /// </summary>
    RA1 = 8,

    /// <summary>
    /// Identifies a paper sheet size of 430 mm times 610 mm.
    /// </summary>
    RA2 = 9,

    /// <summary>
    /// Identifies a paper sheet size of 305 mm times 430 mm.
    /// </summary>
    RA3 = 10,

    /// <summary>
    /// Identifies a paper sheet size of 215 mm times 305 mm.
    /// </summary>
    RA4 = 11,

    /// <summary>
    /// Identifies a paper sheet size of 153 mm times 215 mm.
    /// </summary>
    RA5 = 12,

    /// <summary>
    /// Identifies a paper sheet size of 1000 mm times 1414 mm or 39.37 inch times 55.67 inch.
    /// </summary>
    B0 = 13,

    /// <summary>
    /// Identifies a paper sheet size of 707 mm times 1000 mm or 27.83 inch times 39.37 inch.
    /// </summary>
    B1 = 14,

    /// <summary>
    /// Identifies a paper sheet size of 500 mm times 707 mm or 19.68 inch times 27.83 inch.
    /// </summary>
    B2 = 15,

    /// <summary>
    /// Identifies a paper sheet size of 353 mm times 500 mm or 13.90 inch times 19.68 inch.
    /// </summary>
    B3 = 16,

    /// <summary>
    /// Identifies a paper sheet size of 250 mm times 353 mm or 9.84 inch times 13.90 inch.
    /// </summary>
    B4 = 17,

    /// <summary>
    /// Identifies a paper sheet size of 176 mm times 250 mm or 6.93 inch times 9.84 inch.
    /// </summary>
    B5 = 18,

    /// <summary>
    /// Identifies a paper sheet size of 105 mm times 148 mm or 4.1 inch times 5.8 inch.
    /// </summary>
    A6 = 19,

    /// <summary>
    /// Identifies a paper sheet size of 74 mm times 105 mm or 2.91 inch times 4.13 inch.
    /// </summary>
    A7 = 20,

    /// <summary>
    /// Identifies a paper sheet size of 52 mm times 74 mm or 2.05 inch times 2.91 inch.
    /// </summary>
    A8 = 21,

    /// <summary>
    /// Identifies a paper sheet size of 37 mm times 52 mm or 1.46 inch times 2.05 inch.
    /// </summary>
    A9 = 22,

    /// <summary>
    /// Identifies a paper sheet size of 26 mm times 37 mm or 1.02 inch times 1.46 inch.
    /// </summary>
    A10 = 23,

    /// <summary>
    /// Identifies the DIN 476 oversize 2A0, a paper sheet size of 1189 mm times 1682 mm or
    /// 46.81 inch times 66.22 inch. Named TwoA0 because an identifier cannot start with a digit.
    /// </summary>
    TwoA0 = 24,

    /// <summary>
    /// Identifies the DIN 476 oversize 4A0, a paper sheet size of 1682 mm times 2378 mm or
    /// 66.22 inch times 93.62 inch. Named FourA0 because an identifier cannot start with a digit.
    /// </summary>
    FourA0 = 25,

    /// <summary>
    /// Identifies a paper sheet size of 125 mm times 176 mm or 4.92 inch times 6.93 inch.
    /// </summary>
    B6 = 26,

    /// <summary>
    /// Identifies a paper sheet size of 88 mm times 125 mm or 3.46 inch times 4.92 inch.
    /// </summary>
    B7 = 27,

    /// <summary>
    /// Identifies a paper sheet size of 62 mm times 88 mm or 2.44 inch times 3.46 inch.
    /// </summary>
    B8 = 28,

    /// <summary>
    /// Identifies a paper sheet size of 44 mm times 62 mm or 1.73 inch times 2.44 inch.
    /// </summary>
    B9 = 29,

    /// <summary>
    /// Identifies a paper sheet size of 31 mm times 44 mm or 1.22 inch times 1.73 inch.
    /// </summary>
    B10 = 30,

    // The C series holds envelopes, sized so that a C(n) envelope takes an A(n) sheet unfolded.

    /// <summary>
    /// Identifies an envelope size of 917 mm times 1297 mm or 36.10 inch times 51.06 inch.
    /// </summary>
    C0 = 31,

    /// <summary>
    /// Identifies an envelope size of 648 mm times 917 mm or 25.51 inch times 36.10 inch.
    /// </summary>
    C1 = 32,

    /// <summary>
    /// Identifies an envelope size of 458 mm times 648 mm or 18.03 inch times 25.51 inch.
    /// </summary>
    C2 = 33,

    /// <summary>
    /// Identifies an envelope size of 324 mm times 458 mm or 12.76 inch times 18.03 inch.
    /// </summary>
    C3 = 34,

    /// <summary>
    /// Identifies an envelope size of 229 mm times 324 mm or 9.02 inch times 12.76 inch.
    /// </summary>
    C4 = 35,

    /// <summary>
    /// Identifies an envelope size of 162 mm times 229 mm or 6.38 inch times 9.02 inch.
    /// </summary>
    C5 = 36,

    /// <summary>
    /// Identifies an envelope size of 114 mm times 162 mm or 4.49 inch times 6.38 inch.
    /// </summary>
    C6 = 37,

    /// <summary>
    /// Identifies an envelope size of 81 mm times 114 mm or 3.19 inch times 4.49 inch.
    /// </summary>
    C7 = 38,

    /// <summary>
    /// Identifies an envelope size of 57 mm times 81 mm or 2.24 inch times 3.19 inch.
    /// </summary>
    C8 = 39,

    /// <summary>
    /// Identifies an envelope size of 40 mm times 57 mm or 1.57 inch times 2.24 inch.
    /// </summary>
    C9 = 40,

    /// <summary>
    /// Identifies an envelope size of 28 mm times 40 mm or 1.10 inch times 1.57 inch.
    /// </summary>
    C10 = 41,

    // The SRA series is untrimmed stock, larger again than the RA series above it.

    /// <summary>
    /// Identifies a paper sheet size of 900 mm times 1280 mm or 35.43 inch times 50.39 inch.
    /// </summary>
    SRA0 = 42,

    /// <summary>
    /// Identifies a paper sheet size of 640 mm times 900 mm or 25.20 inch times 35.43 inch.
    /// </summary>
    SRA1 = 43,

    /// <summary>
    /// Identifies a paper sheet size of 450 mm times 640 mm or 17.72 inch times 25.20 inch.
    /// </summary>
    SRA2 = 44,

    /// <summary>
    /// Identifies a paper sheet size of 320 mm times 450 mm or 12.60 inch times 17.72 inch.
    /// </summary>
    SRA3 = 45,

    /// <summary>
    /// Identifies a paper sheet size of 225 mm times 320 mm or 8.86 inch times 12.60 inch.
    /// </summary>
    SRA4 = 46,

    // Current U.S. loose paper sizes
    // see http://www.reference.com/browse/wiki/Paper_size

    /// <summary>
    /// Identifies a paper sheet size of 10 inch times 8 inch or 254 mm times 203 mm.
    /// </summary>
    Quarto = 100,

    /// <summary>
    /// Identifies a paper sheet size of 13 inch times 8 inch or 330 mm times 203 mm.
    /// </summary>
    Foolscap = 101,

    /// <summary>
    ///  Identifies a paper sheet size of 7.25 inch times 10.5 inch or 184 mm times 267 mm.
    /// </summary>
    Executive = 102,

    /// <summary>
    /// Identifies a paper sheet size of 10.5 inch times 8 inch 267 mm times 203 mm.
    /// </summary>
    GovernmentLetter = 103,

    /// <summary>
    /// Identifies a paper sheet size of 11 inch times 8.5 inch 279 mm times 216 mm.
    /// </summary>
    Letter = 104,

    /// <summary>
    /// Identifies a paper sheet size of 14 inch times 8.5 inch 356 mm times 216 mm.
    /// </summary>
    Legal = 105,

    /// <summary>
    /// Identifies a paper sheet size of 17 inch times 11 inch or 432 mm times 279 mm.
    /// </summary>
    Ledger = 106,

    /// <summary>
    /// Identifies a paper sheet size of 17 inch times 11 inch or 432 mm times 279 mm.
    /// </summary>
    Tabloid = 107,

    /// <summary>
    /// Identifies a paper sheet size of 19.25 inch times 15.5 inch 489 mm times 394 mm.
    /// </summary>
    Post = 108,

    /// <summary>
    /// 20 ×Identifies a paper sheet size of 20 inch times 15 inch or 508 mm times 381 mm.
    /// </summary>
    Crown = 109,

    /// <summary>
    /// Identifies a paper sheet size of 21 inch times 16.5 inch 533 mm times 419 mm.
    /// </summary>
    LargePost = 110,

    /// <summary>
    /// Identifies a paper sheet size of 22.5 inch times 17.5 inch 572 mm times 445 mm.
    /// </summary>
    Demy = 111,

    /// <summary>
    /// Identifies a paper sheet size of 23 inch times 18 inch or 584 mm times 457 mm.
    /// </summary>
    Medium = 112,

    /// <summary>
    /// Identifies a paper sheet size of 25 inch times 20 inch or 635 mm times 508 mm.
    /// </summary>
    Royal = 113,

    /// <summary>
    /// Identifies a paper sheet size of 28 inch times 23 inch or 711 mm times 584 mm.
    /// </summary>
    Elephant = 114,

    /// <summary>
    /// Identifies a paper sheet size of 35 inch times 23.5 inch or 889 mm times 597 mm.
    /// </summary>
    DoubleDemy = 115,

    /// <summary>
    /// Identifies a paper sheet size of 45 inch times 35 inch 1143 times 889 mm.
    /// </summary>
    QuadDemy = 116,

    /// <summary>
    /// Identifies a paper sheet size of 8.5 inch times 5.5 inch or 216 mm times 396 mm.
    /// </summary>
    STMT = 117,

    /// <summary>
    /// Identifies a paper sheet size of 8.5 inch times 13 inch or 216 mm times 330 mm.
    /// </summary>
    Folio = 120,

    /// <summary>
    /// Identifies a paper sheet size of 5.5 inch times 8.5 inch or 396 mm times 216 mm.
    /// </summary>
    Statement = 121,

    /// <summary>
    /// Identifies a paper sheet size of 10 inch times 14 inch.
    /// </summary>
    Size10x14 = 122,

    //A 11 × 8.5 279 × 216
    //B 17 × 11 432 × 279
    //C 22 × 17 559 × 432
    //D 34 × 22 864 × 559
    //E 44 × 34 1118 × 864
}
