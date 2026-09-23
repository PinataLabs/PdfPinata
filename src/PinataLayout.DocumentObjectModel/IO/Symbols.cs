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

using System.Diagnostics;
using System.Collections;

namespace PinataLayout.DocumentObjectModel.IO;

internal class KeyWords
{
  static KeyWords()
  {
    _enumToName.Add(Symbol.True, "true");
    _enumToName.Add(Symbol.False, "false");
    _enumToName.Add(Symbol.Null, "null");

    _enumToName.Add(Symbol.Styles, @"\styles");
    _enumToName.Add(Symbol.Document, @"\document");
    _enumToName.Add(Symbol.Section, @"\section");
    _enumToName.Add(Symbol.Paragraph, @"\paragraph");
    _enumToName.Add(Symbol.Header, @"\header");
    _enumToName.Add(Symbol.Footer, @"\footer");
    _enumToName.Add(Symbol.PrimaryHeader, @"\primaryheader");
    _enumToName.Add(Symbol.PrimaryFooter, @"\primaryfooter");
    _enumToName.Add(Symbol.FirstPageHeader, @"\firstpageheader");
    _enumToName.Add(Symbol.FirstPageFooter, @"\firstpagefooter");
    _enumToName.Add(Symbol.EvenPageHeader, @"\evenpageheader");
    _enumToName.Add(Symbol.EvenPageFooter, @"\evenpagefooter");
    _enumToName.Add(Symbol.Table, @"\table");
    _enumToName.Add(Symbol.Columns, @"\columns");
    _enumToName.Add(Symbol.Column, @"\column");
    _enumToName.Add(Symbol.Rows, @"\rows");
    _enumToName.Add(Symbol.Row, @"\row");
    _enumToName.Add(Symbol.Cell, @"\cell");
    _enumToName.Add(Symbol.Image, @"\image");
    _enumToName.Add(Symbol.TextFrame, @"\textframe");
    _enumToName.Add(Symbol.PageBreak, @"\pagebreak");
    _enumToName.Add(Symbol.Barcode, @"\barcode");
    _enumToName.Add(Symbol.Chart, @"\chart");
    _enumToName.Add(Symbol.HeaderArea, @"\headerarea");
    _enumToName.Add(Symbol.FooterArea, @"\footerarea");
    _enumToName.Add(Symbol.TopArea, @"\toparea");
    _enumToName.Add(Symbol.BottomArea, @"\bottomarea");
    _enumToName.Add(Symbol.LeftArea, @"\leftarea");
    _enumToName.Add(Symbol.RightArea, @"\rightarea");
    _enumToName.Add(Symbol.PlotArea, @"\plotarea");
    _enumToName.Add(Symbol.Legend, @"\legend");
    _enumToName.Add(Symbol.XAxis, @"\xaxis");
    _enumToName.Add(Symbol.YAxis, @"\yaxis");
    _enumToName.Add(Symbol.ZAxis, @"\zaxis");
    _enumToName.Add(Symbol.Series, @"\series");
    _enumToName.Add(Symbol.XValues, @"\xvalues");
    _enumToName.Add(Symbol.Point, @"\point");

    _enumToName.Add(Symbol.Bold, @"\bold");
    _enumToName.Add(Symbol.Italic, @"\italic");
    _enumToName.Add(Symbol.Underline, @"\underline");
    _enumToName.Add(Symbol.FontSize, @"\fontsize");
    _enumToName.Add(Symbol.FontColor, @"\fontcolor");
    _enumToName.Add(Symbol.Font, @"\font");
    //
    _enumToName.Add(Symbol.Field, @"\field");
    _enumToName.Add(Symbol.Symbol, @"\symbol");
    _enumToName.Add(Symbol.Chr, @"\chr");
    //
    _enumToName.Add(Symbol.Footnote, @"\footnote");
    _enumToName.Add(Symbol.Hyperlink, @"\hyperlink");
    //
    _enumToName.Add(Symbol.SoftHyphen, @"\-");
    _enumToName.Add(Symbol.Tab, @"\tab");
    _enumToName.Add(Symbol.LineBreak, @"\linebreak");
    _enumToName.Add(Symbol.Space, @"\space");
    _enumToName.Add(Symbol.NoSpace, @"\nospace");

    //
    //
    _enumToName.Add(Symbol.BraceLeft, "{");
    _enumToName.Add(Symbol.BraceRight, "}");
    _enumToName.Add(Symbol.BracketLeft, "[");
    _enumToName.Add(Symbol.BracketRight, "]");
    _enumToName.Add(Symbol.ParenLeft, "(");
    _enumToName.Add(Symbol.ParenRight, ")");
    _enumToName.Add(Symbol.Colon, ":");
    _enumToName.Add(Symbol.Semicolon, ";");  //??? id DDL?
    _enumToName.Add(Symbol.Dot, ".");
    _enumToName.Add(Symbol.Comma, ",");
    _enumToName.Add(Symbol.Percent, "%");  //??? id DDL?
    _enumToName.Add(Symbol.Dollar, "$");  //??? id DDL?
    _enumToName.Add(Symbol.Hash, "#");  //??? id DDL?
    _enumToName.Add(Symbol.Assign, "=");
    _enumToName.Add(Symbol.Slash, "/");  //??? id DDL?
    _enumToName.Add(Symbol.BackSlash, "\\");
    _enumToName.Add(Symbol.Plus, "+");  //??? id DDL?
    _enumToName.Add(Symbol.PlusAssign, "+=");
    _enumToName.Add(Symbol.Minus, "-");  //??? id DDL?
    _enumToName.Add(Symbol.MinusAssign, "-=");
    _enumToName.Add(Symbol.Blank, " ");

    //---------------------------------------------------------------
    //---------------------------------------------------------------
    //---------------------------------------------------------------

    _nameToEnum.Add("true", Symbol.True);
    _nameToEnum.Add("false", Symbol.False);
    _nameToEnum.Add("null", Symbol.Null);
    //
    _nameToEnum.Add(@"\styles", Symbol.Styles);
    _nameToEnum.Add(@"\document", Symbol.Document);
    _nameToEnum.Add(@"\section", Symbol.Section);
    _nameToEnum.Add(@"\paragraph", Symbol.Paragraph);
    _nameToEnum.Add(@"\header", Symbol.Header);
    _nameToEnum.Add(@"\footer", Symbol.Footer);
    _nameToEnum.Add(@"\primaryheader", Symbol.PrimaryHeader);
    _nameToEnum.Add(@"\primaryfooter", Symbol.PrimaryFooter);
    _nameToEnum.Add(@"\firstpageheader", Symbol.FirstPageHeader);
    _nameToEnum.Add(@"\firstpagefooter", Symbol.FirstPageFooter);
    _nameToEnum.Add(@"\evenpageheader", Symbol.EvenPageHeader);
    _nameToEnum.Add(@"\evenpagefooter", Symbol.EvenPageFooter);
    _nameToEnum.Add(@"\table", Symbol.Table);
    _nameToEnum.Add(@"\columns", Symbol.Columns);
    _nameToEnum.Add(@"\column", Symbol.Column);
    _nameToEnum.Add(@"\rows", Symbol.Rows);
    _nameToEnum.Add(@"\row", Symbol.Row);
    _nameToEnum.Add(@"\cell", Symbol.Cell);
    _nameToEnum.Add(@"\image", Symbol.Image);
    _nameToEnum.Add(@"\textframe", Symbol.TextFrame);
    _nameToEnum.Add(@"\pagebreak", Symbol.PageBreak);
    _nameToEnum.Add(@"\barcode", Symbol.Barcode);
    _nameToEnum.Add(@"\chart", Symbol.Chart);
    _nameToEnum.Add(@"\headerarea", Symbol.HeaderArea);
    _nameToEnum.Add(@"\footerarea", Symbol.FooterArea);
    _nameToEnum.Add(@"\toparea", Symbol.TopArea);
    _nameToEnum.Add(@"\bottomarea", Symbol.BottomArea);
    _nameToEnum.Add(@"\leftarea", Symbol.LeftArea);
    _nameToEnum.Add(@"\rightarea", Symbol.RightArea);
    _nameToEnum.Add(@"\plotarea", Symbol.PlotArea);
    _nameToEnum.Add(@"\legend", Symbol.Legend);
    _nameToEnum.Add(@"\xaxis", Symbol.XAxis);
    _nameToEnum.Add(@"\yaxis", Symbol.YAxis);
    _nameToEnum.Add(@"\zaxis", Symbol.ZAxis);
    _nameToEnum.Add(@"\series", Symbol.Series);
    _nameToEnum.Add(@"\xvalues", Symbol.XValues);
    _nameToEnum.Add(@"\point", Symbol.Point);
    _nameToEnum.Add(@"\bold", Symbol.Bold);
    _nameToEnum.Add(@"\italic", Symbol.Italic);
    _nameToEnum.Add(@"\underline", Symbol.Underline);
    _nameToEnum.Add(@"\fontsize", Symbol.FontSize);
    _nameToEnum.Add(@"\fontcolor", Symbol.FontColor);
    _nameToEnum.Add(@"\font", Symbol.Font);
    //
    _nameToEnum.Add(@"\field", Symbol.Field);
    _nameToEnum.Add(@"\symbol", Symbol.Symbol);
    _nameToEnum.Add(@"\chr", Symbol.Chr);
    //
    _nameToEnum.Add(@"\footnote", Symbol.Footnote);
    _nameToEnum.Add(@"\hyperlink", Symbol.Hyperlink);
    //
    _nameToEnum.Add(@"\-", Symbol.SoftHyphen); //??? \( ist auch was spezielles
    _nameToEnum.Add(@"\tab", Symbol.Tab);
    _nameToEnum.Add(@"\linebreak", Symbol.LineBreak);
    _nameToEnum.Add(@"\space", Symbol.Space);
    _nameToEnum.Add(@"\nospace", Symbol.NoSpace);
  }

  /// <summary>
  /// Returns Symbol value from name, or Symbol.None if no such Symbol exists.
  /// </summary>
  internal static Symbol SymbolFromName(string name)
  {
    Symbol docsym;
    var obj = _nameToEnum[name];
    if (obj == null)
    {
      // Check for case-sensitive keywords. Allow first character upper case only.
      if (string.Equals(name, "True", System.StringComparison.Ordinal))
        docsym = Symbol.True;
      else if (string.Equals(name, "False", System.StringComparison.Ordinal))
        docsym = Symbol.False;
      else if (string.Equals(name, "Null", System.StringComparison.Ordinal))
        docsym = Symbol.Null;
      else
        docsym = Symbol.None;
    }
    else
    {
      docsym = (Symbol)obj;
    }
    return docsym;
  }

  /// <summary>
  /// Returns string from Symbol value.
  /// </summary>
  internal static string NameFromSymbol(Symbol symbol)
  {
    var name = (string)_enumToName[symbol];
    Debug.Assert(name != null);
    return name;
  }

  private static readonly Hashtable _enumToName = new();
  private static readonly Hashtable _nameToEnum = new();
}
