#region Copyright
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
/// Standard page sizes.
/// </summary>
/// <remarks>
/// The values are numbered explicitly and out of order. The twelve formats this enumeration began
/// with keep the ordinals the compiler gave them, so that a value cast from an integer still means
/// what it did; everything added since is numbered from twelve up, in the order it was added.
/// MDDDL records the format by name, not by number, so the file format is unaffected either way.
/// </remarks>
public enum PageFormat
{
  // ISO 216 A series. Each is the one above it halved across its longer side.

  /// <summary>ISO A0 — 841 mm × 1189 mm.</summary>
  A0 = 0,

  /// <summary>ISO A1 — 594 mm × 841 mm.</summary>
  A1 = 1,

  /// <summary>ISO A2 — 420 mm × 594 mm.</summary>
  A2 = 2,

  /// <summary>ISO A3 — 297 mm × 420 mm.</summary>
  A3 = 3,

  /// <summary>ISO A4 — 210 mm × 297 mm.</summary>
  A4 = 4,

  /// <summary>ISO A5 — 148 mm × 210 mm.</summary>
  A5 = 5,

  /// <summary>ISO A6 — 105 mm × 148 mm.</summary>
  A6 = 6,

  /// <summary>ISO A7 — 74 mm × 105 mm.</summary>
  A7 = 12,

  /// <summary>ISO A8 — 52 mm × 74 mm.</summary>
  A8 = 13,

  /// <summary>ISO A9 — 37 mm × 52 mm.</summary>
  A9 = 14,

  /// <summary>ISO A10 — 26 mm × 37 mm.</summary>
  A10 = 15,

  // DIN 476 oversizes, spelled out because an identifier cannot begin with a digit.

  /// <summary>DIN 2A0 — 1189 mm × 1682 mm, twice A0.</summary>
  TwoA0 = 16,

  /// <summary>DIN 4A0 — 1682 mm × 2378 mm, four times A0.</summary>
  FourA0 = 17,

  // ISO 216 B series, the sizes between the A sheets.

  /// <summary>ISO B0 — 1000 mm × 1414 mm.</summary>
  B0 = 18,

  /// <summary>ISO B1 — 707 mm × 1000 mm.</summary>
  B1 = 19,

  /// <summary>ISO B2 — 500 mm × 707 mm.</summary>
  B2 = 20,

  /// <summary>ISO B3 — 353 mm × 500 mm.</summary>
  B3 = 21,

  /// <summary>ISO B4 — 250 mm × 353 mm.</summary>
  B4 = 22,

  /// <summary>
  /// ISO B5 — 176 mm × 250 mm. Measured 182 mm × 257 mm before the rest of the B series existed,
  /// which is the JIS sheet, now named <see cref="JISB5"/>.
  /// </summary>
  B5 = 7,

  /// <summary>ISO B6 — 125 mm × 176 mm.</summary>
  B6 = 23,

  /// <summary>ISO B7 — 88 mm × 125 mm.</summary>
  B7 = 24,

  /// <summary>ISO B8 — 62 mm × 88 mm.</summary>
  B8 = 25,

  /// <summary>ISO B9 — 44 mm × 62 mm.</summary>
  B9 = 26,

  /// <summary>ISO B10 — 31 mm × 44 mm.</summary>
  B10 = 27,

  /// <summary>JIS B5 — 182 mm × 257 mm, the Japanese B5, wider and taller than the ISO one.</summary>
  JISB5 = 28,

  // ISO 269 C series, the envelopes. A C(n) envelope takes an A(n) sheet unfolded.

  /// <summary>ISO C0 — 917 mm × 1297 mm.</summary>
  C0 = 29,

  /// <summary>ISO C1 — 648 mm × 917 mm.</summary>
  C1 = 30,

  /// <summary>ISO C2 — 458 mm × 648 mm.</summary>
  C2 = 31,

  /// <summary>ISO C3 — 324 mm × 458 mm.</summary>
  C3 = 32,

  /// <summary>ISO C4 — 229 mm × 324 mm, the envelope for an unfolded A4 sheet.</summary>
  C4 = 33,

  /// <summary>ISO C5 — 162 mm × 229 mm, the envelope for an A4 sheet folded once.</summary>
  C5 = 34,

  /// <summary>ISO C6 — 114 mm × 162 mm, the envelope for an A4 sheet folded twice.</summary>
  C6 = 35,

  /// <summary>ISO C7 — 81 mm × 114 mm.</summary>
  C7 = 36,

  /// <summary>ISO C8 — 57 mm × 81 mm.</summary>
  C8 = 37,

  /// <summary>ISO C9 — 40 mm × 57 mm.</summary>
  C9 = 38,

  /// <summary>ISO C10 — 28 mm × 40 mm.</summary>
  C10 = 39,

  // ISO 217 untrimmed stock: RA is trimmed down to an A sheet, SRA leaves room to bleed first.

  /// <summary>ISO RA0 — 860 mm × 1220 mm.</summary>
  RA0 = 40,

  /// <summary>ISO RA1 — 610 mm × 860 mm.</summary>
  RA1 = 41,

  /// <summary>ISO RA2 — 430 mm × 610 mm.</summary>
  RA2 = 42,

  /// <summary>ISO RA3 — 305 mm × 430 mm.</summary>
  RA3 = 43,

  /// <summary>ISO RA4 — 215 mm × 305 mm.</summary>
  RA4 = 44,

  /// <summary>RA5 — 153 mm × 215 mm.</summary>
  RA5 = 45,

  /// <summary>ISO SRA0 — 900 mm × 1280 mm.</summary>
  SRA0 = 46,

  /// <summary>ISO SRA1 — 640 mm × 900 mm.</summary>
  SRA1 = 47,

  /// <summary>ISO SRA2 — 450 mm × 640 mm.</summary>
  SRA2 = 48,

  /// <summary>ISO SRA3 — 320 mm × 450 mm.</summary>
  SRA3 = 49,

  /// <summary>ISO SRA4 — 225 mm × 320 mm.</summary>
  SRA4 = 50,

  // North American sizes, defined in inches.

  /// <summary>Letter — 8.5 inch × 11 inch.</summary>
  Letter = 8,

  /// <summary>Legal — 8.5 inch × 14 inch.</summary>
  Legal = 9,

  /// <summary>Ledger — 17 inch × 11 inch, tabloid turned on its side.</summary>
  Ledger = 10,

  /// <summary>Tabloid — 11 inch × 17 inch. The same sheet as <see cref="P11x17"/>.</summary>
  Tabloid = 55,

  /// <summary>
  /// 11 inch × 17 inch, the name this format has carried since before <see cref="Tabloid"/> was
  /// named. Both are kept: MDDDL records the format by name, and files hold this one.
  /// </summary>
  P11x17 = 11,

  /// <summary>Executive — 7.25 inch × 10.5 inch.</summary>
  Executive = 53,

  /// <summary>Government letter — 8 inch × 10.5 inch.</summary>
  GovernmentLetter = 54,

  /// <summary>Statement, also called half letter — 5.5 inch × 8.5 inch.</summary>
  Statement = 67,

  /// <summary>The same sheet as <see cref="Statement"/> — 5.5 inch × 8.5 inch.</summary>
  STMT = 65,

  /// <summary>Folio — 8.5 inch × 13 inch.</summary>
  Folio = 66,

  /// <summary>10 inch × 14 inch.</summary>
  Size10x14 = 68,

  // Traditional British sizes, defined in inches.

  /// <summary>Quarto — 8 inch × 10 inch.</summary>
  Quarto = 51,

  /// <summary>Foolscap — 8 inch × 13 inch.</summary>
  Foolscap = 52,

  /// <summary>Post — 15.5 inch × 19.25 inch.</summary>
  Post = 56,

  /// <summary>Crown — 20 inch × 15 inch.</summary>
  Crown = 57,

  /// <summary>Large post — 16.5 inch × 21 inch.</summary>
  LargePost = 58,

  /// <summary>Demy — 17.5 inch × 22 inch.</summary>
  Demy = 59,

  /// <summary>Medium — 18 inch × 23 inch.</summary>
  Medium = 60,

  /// <summary>Royal — 20 inch × 25 inch.</summary>
  Royal = 61,

  /// <summary>Elephant — 23 inch × 28 inch.</summary>
  Elephant = 62,

  /// <summary>Double demy — 23.5 inch × 35 inch.</summary>
  DoubleDemy = 63,

  /// <summary>Quad demy — 35 inch × 45 inch.</summary>
  QuadDemy = 64
}
