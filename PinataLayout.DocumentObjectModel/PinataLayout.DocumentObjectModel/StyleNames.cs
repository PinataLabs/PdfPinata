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
/// Enumerates the predefined style names.
/// </summary>
public class StyleNames
{
  /// <summary>The style every character style ultimately derives from.</summary>
  public const string DefaultParagraphFont = "DefaultParagraphFont";
  /// <summary>The style every paragraph style ultimately derives from, and the document default.</summary>
  public const string Normal = "Normal";
  /// <summary>The style of a first-level heading.</summary>
  public const string Heading1 = "Heading1";
  /// <summary>The style of a second-level heading.</summary>
  public const string Heading2 = "Heading2";
  /// <summary>The style of a third-level heading.</summary>
  public const string Heading3 = "Heading3";
  /// <summary>The style of a fourth-level heading.</summary>
  public const string Heading4 = "Heading4";
  /// <summary>The style of a fifth-level heading.</summary>
  public const string Heading5 = "Heading5";
  /// <summary>The style of a sixth-level heading.</summary>
  public const string Heading6 = "Heading6";
  /// <summary>The style of a seventh-level heading.</summary>
  public const string Heading7 = "Heading7";
  /// <summary>The style of an eighth-level heading.</summary>
  public const string Heading8 = "Heading8";
  /// <summary>The style of a ninth-level heading.</summary>
  public const string Heading9 = "Heading9";
  /// <summary>The style of footnote text.</summary>
  public const string Footnote = "Footnote";
  /// <summary>The style of a page header.</summary>
  public const string Header = "Header";
  /// <summary>The style of a page footer.</summary>
  public const string Footer = "Footer";
  /// <summary>The style of hyperlink text.</summary>
  public const string Hyperlink = "Hyperlink";
  /// <summary>The name reported for a style that does not exist.</summary>
  public const string InvalidStyleName = "InvalidStyleName";
}
