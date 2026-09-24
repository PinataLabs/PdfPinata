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
/// <remarks>
/// A Character is either a named symbol or a plain character, and the two are held in separate
/// fields. <see cref="SymbolName"/> still presents them as one value, because it always has: a
/// character assigned through <see cref="Char"/> reads back through it as its own code, and
/// <c>AddCharacter(char)</c> is the same as <c>AddCharacter((SymbolName)ch)</c>. What tells the two
/// apart is the top nibble, which every defined SymbolName has set and no character has.
/// </remarks>
public partial class Character : DocumentObject
{
  // \space
  /// <summary>A normal space.</summary>
  public static readonly Character Blank = new(SymbolName.Blank);
  /// <summary>A space one en wide, half an em.</summary>
  public static readonly Character En = new(SymbolName.En);
  /// <summary>A space one em wide.</summary>
  public static readonly Character Em = new(SymbolName.Em);
  /// <summary>A space a quarter of an em wide.</summary>
  public static readonly Character EmQuarter = new(SymbolName.EmQuarter);
  /// <summary>A space a quarter of an em wide. Same as <see cref="EmQuarter"/>.</summary>
  public static readonly Character Em4 = new(SymbolName.Em4);

  // used to serialize as \tab, \linebreak
  /// <summary>A tab stop.</summary>
  public static readonly Character Tab = new(SymbolName.Tab);
  /// <summary>A break within a paragraph.</summary>
  public static readonly Character LineBreak = new(SymbolName.LineBreak);

  // \symbol
  /// <summary>The euro sign, U+20AC.</summary>
  public static readonly Character Euro = new(SymbolName.Euro);
  /// <summary>The copyright sign, U+00A9.</summary>
  public static readonly Character Copyright = new(SymbolName.Copyright);
  /// <summary>The trade mark sign, U+2122.</summary>
  public static readonly Character Trademark = new(SymbolName.Trademark);
  /// <summary>The registered sign, U+00AE.</summary>
  public static readonly Character RegisteredTrademark = new(SymbolName.RegisteredTrademark);
  /// <summary>A bullet, U+2022.</summary>
  public static readonly Character Bullet = new(SymbolName.Bullet);
  /// <summary>The not sign, U+00AC.</summary>
  public static readonly Character Not = new(SymbolName.Not);
  /// <summary>An em dash, U+2014.</summary>
  public static readonly Character EmDash = new(SymbolName.EmDash);
  /// <summary>An en dash, U+2013.</summary>
  public static readonly Character EnDash = new(SymbolName.EnDash);
  /// <summary>A space a line may not be broken at, U+00A0.</summary>
  public static readonly Character NonBreakableBlank = new(SymbolName.NonBreakableBlank);
  /// <summary>A space a line may not be broken at. Same as <see cref="NonBreakableBlank"/>.</summary>
  public static readonly Character HardBlank = new(SymbolName.HardBlank);

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
  private Character(SymbolName name)
    : this()
  {
    this.name = name;
  }

  #region Properties
  /// <summary>
  /// Gets or sets the SymbolName. A character defined through <see cref="Char"/> reads back as its
  /// own code cast to SymbolName, and assigning such a value here is the same as assigning
  /// <see cref="Char"/>. Returns 0 if nothing has been assigned.
  /// </summary>
  /// <exception cref="ArgumentException">
  /// The value has its top nibble set, so claims to be a symbol, but is not a defined SymbolName.
  /// </exception>
  public SymbolName SymbolName
  {
    get => symbolName ?? default;
    // A value with the top nibble clear is a character and is let through: it cannot be a defined
    // SymbolName, and AddCharacter(char) arrives here as one. Anything else claims to be a symbol,
    // and an undefined one used to be kept and written out as \symbol(<number>), which the parser
    // cannot read back - so it gets the check every other enum property in the DOM has.
    set => symbolName = IsCharacter(value) ? value : EnumGuard.Checked(value);
  }

  /// <summary>
  /// The name the value model knows this object's content by. It presents the two fields as the
  /// single value they were before they were split, so GetValue("SymbolName"), IsNull and SetNull
  /// answer exactly as they always did. Unchecked, like every generated setter.
  /// </summary>
  [DV]
  internal SymbolName? symbolName
  {
    get => name ?? (SymbolName?)code;
    set
    {
      if (value is { } v && IsCharacter(v))
      {
        code = (uint)v;
        name = null;
      }
      else
      {
        name = value;
        code = null;
      }
    }
  }

  /// <summary>
  /// The symbol, when this is one. Never holds a value with the top nibble clear.
  /// </summary>
  private SymbolName? name;

  /// <summary>
  /// The character, when this is one. Held as the whole code rather than as a char: a value above
  /// U+FFFF assigned through <see cref="SymbolName"/> has always been kept and written out whole,
  /// while <see cref="Char"/> reads back its low 16 bits.
  /// </summary>
  private uint? code;

  private static bool IsCharacter(SymbolName value) => ((uint)value & 0xF0000000) == 0;

  /// <summary>
  /// Gets or sets the character. Returns 0 if the type is defined via an enum.
  /// </summary>
  public char Char
  {
    get => code is { } c ? (char)c : '\0';
    set
    {
      code = value;
      name = null;
    }
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
    var text = count == 1 ? BreakText() : null;
    serializer.Write(text ?? SymbolText((uint)(symbolName ?? default)));
  }

  /// <summary>
  /// The DDL of a tab, line break or paragraph break, or null for anything else.
  /// </summary>
  private string BreakText()
  {
    // An unset name matches none of these, and a character is never held there.
    return name switch
    {
      SymbolName.Tab => "\\tab ",
      SymbolName.LineBreak => "\\linebreak\x0D\x0A",
      SymbolName.ParaBreak => "\x0D\x0A\x0D\x0A",
      _ => null
    };
  }

  /// <summary>
  /// The DDL of a space, a symbol or a (unicode) character.
  /// </summary>
  private string SymbolText(uint raw)
  {
    var isSymbol = (raw & 0xF0000000) == 0xF0000000;
    if (!isSymbol)
    {
      // symbolType is a (unicode) character
      return " \\chr(0x" + ((int)raw).ToString("X") + ")";
    }

    // SymbolName == SpaceType?
    var isSpace = (raw & 0xF1000000) == 0xF1000000;
    if (!isSpace)
      return "\\symbol(" + SymbolName + ")";

    //Note: Don't try to optimize it by leaving away the braces in case a single space is added.
    //This would lead to confusion with '(' in directly following text.
    if (name == SymbolName.Blank)
      return "\\space(" + Count + ")";

    return count == 1
      ? "\\space(" + SymbolName + ")"
      : "\\space(" + SymbolName + ", " + Count + ")";
  }

  #endregion
}
