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

using System;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents a special character in paragraph text.
/// </summary>
// TODO: So ändern, dass symbolName und char in unterschiedlichen Feldern gespeichert wird
public partial class Character : DocumentObject
{
  // \space
  /// <summary>A normal space.</summary>
  public static readonly Character Blank = new Character(SymbolName.Blank);
  /// <summary>A space one en wide, half an em.</summary>
  public static readonly Character En = new Character(SymbolName.En);
  /// <summary>A space one em wide.</summary>
  public static readonly Character Em = new Character(SymbolName.Em);
  /// <summary>A space a quarter of an em wide.</summary>
  public static readonly Character EmQuarter = new Character(SymbolName.EmQuarter);
  /// <summary>A space a quarter of an em wide. Same as <see cref="EmQuarter"/>.</summary>
  public static readonly Character Em4 = new Character(SymbolName.Em4);

  // used to serialize as \tab, \linebreak
  /// <summary>A tab stop.</summary>
  public static readonly Character Tab = new Character(SymbolName.Tab);
  /// <summary>A break within a paragraph.</summary>
  public static readonly Character LineBreak = new Character(SymbolName.LineBreak);
  //public static readonly Character MarginBreak         = new Character(SymbolName.MarginBreak);

  // \symbol
  /// <summary>The euro sign, U+20AC.</summary>
  public static readonly Character Euro = new Character(SymbolName.Euro);
  /// <summary>The copyright sign, U+00A9.</summary>
  public static readonly Character Copyright = new Character(SymbolName.Copyright);
  /// <summary>The trade mark sign, U+2122.</summary>
  public static readonly Character Trademark = new Character(SymbolName.Trademark);
  /// <summary>The registered sign, U+00AE.</summary>
  public static readonly Character RegisteredTrademark = new Character(SymbolName.RegisteredTrademark);
  /// <summary>A bullet, U+2022.</summary>
  public static readonly Character Bullet = new Character(SymbolName.Bullet);
  /// <summary>The not sign, U+00AC.</summary>
  public static readonly Character Not = new Character(SymbolName.Not);
  /// <summary>An em dash, U+2014.</summary>
  public static readonly Character EmDash = new Character(SymbolName.EmDash);
  /// <summary>An en dash, U+2013.</summary>
  public static readonly Character EnDash = new Character(SymbolName.EnDash);
  /// <summary>A space a line may not be broken at, U+00A0.</summary>
  public static readonly Character NonBreakableBlank = new Character(SymbolName.NonBreakableBlank);
  /// <summary>A space a line may not be broken at. Same as <see cref="NonBreakableBlank"/>.</summary>
  public static readonly Character HardBlank = new Character(SymbolName.HardBlank);

  /// <summary>
  /// Initializes a new instance of the Character class.
  /// </summary>
  public Character()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Character class with the specified parent.
  /// </summary>
  internal Character(DocumentObject parent) : base(parent) { }

  /// <summary>
  /// Initializes a new instance of the Character class with the specified SymbolName.
  /// </summary>
  Character(SymbolName name)
    : this()
  {
    symbolName = name;
  }

  #region Properties
  /// <summary>
  /// Gets or sets the SymbolName. Returns 0 if the type is defined by a character.
  /// </summary>
  public SymbolName SymbolName
  {
    get => symbolName ?? default;
    // No EnumGuard here, unlike every other enum property in the DOM. Char below writes arbitrary
    // character values through this same field and separates them from symbol names by their top
    // nibble, so most of what this field legitimately holds is not a defined SymbolName. NEnum
    // carved SymbolName out of its own Enum.IsDefined check for exactly this reason.
    set => symbolName = value;
  }
  [DV]
  internal SymbolName? symbolName;

  /// <summary>
  /// Gets or sets the SymbolName as character. Returns 0 if the type is defined via an enum.
  /// </summary>
  public char Char
  {
    get
    {
      uint raw = (uint)(symbolName ?? default);
      return (raw & 0xF0000000) == 0 ? (char)raw : '\0';
    }
    set => symbolName = (SymbolName)value;
  }

  /// <summary>
  /// Gets or sets the number of times the character is repeated.
  /// </summary>
  public int Count
  {
    get => count ?? 0;
    set => count = value;
  }
  [DV]
  internal int? count = 1;
  #endregion

  #region Internal
  /// <summary>
  /// Converts Character into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    string text = String.Empty;
    // No SymbolName is defined as 0, so an unset symbolName matches none of these - which is what
    // the old (SymbolName)symbolName.Value did too, NEnum having read back 0 when null.
    if (count == 1)
    {
      if (symbolName == SymbolName.Tab)
        text = "\\tab ";
      else if (symbolName == SymbolName.LineBreak)
        text = "\\linebreak\x0D\x0A";
      else if (symbolName == SymbolName.ParaBreak)
        text = "\x0D\x0A\x0D\x0A";
      //else if (symbolType == SymbolName.MarginBreak)
      //  text = "\\marginbreak ";

      if (text != "")
      {
        serializer.Write(text);
        return;
      }
    }

    uint raw = (uint)(symbolName ?? default);
    if ((raw & 0xF0000000) == 0xF0000000)
    {
      // SymbolName == SpaceType?
      if ((raw & 0xF1000000) == 0xF1000000)
      {
        if (symbolName == SymbolName.Blank)
        {
          //Note: Don't try to optimize it by leaving away the braces in case a single space is added.
          //This would lead to confusion with '(' in directly following text.
          text = "\\space(" + Count + ")";
        }
        else
        {
          if (count == 1)
            text = "\\space(" + SymbolName + ")";
          else
            text = "\\space(" + SymbolName + ", " + Count + ")";
        }
      }
      else
      {
        text = "\\symbol(" + SymbolName + ")";
      }
    }
    else
    {
      // symbolType is a (unicode) character
      text = " \\chr(0x" + ((int)raw).ToString("X") + ")";
    }

    serializer.Write(text);
  }

  #endregion
}
