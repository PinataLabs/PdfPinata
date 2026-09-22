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

namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// Carries forward the range check that NEnum's setter used to apply.
/// </summary>
/// <remarks>
/// NEnum stored an int and validated it against the enum type it also carried, throwing
/// ArgumentException for a value Enum.IsDefined rejected. A TEnum? field accepts whatever the cast
/// produces, so the guard has to sit in the public property that writes it. Character.SymbolName
/// applies it only to a value with the top nibble set, because one with it clear is a character
/// rather than a symbol.
/// </remarks>
internal static class EnumGuard
{
  /// <summary>
  /// Returns value if it is a defined member of TEnum, and throws ArgumentException if it is not.
  /// </summary>
  internal static T Checked<T>(T value) where T : struct, Enum
  {
    // ArgumentException rather than the more correct ArgumentOutOfRangeException, because NEnum
    // threw ArgumentException and no caller should be able to tell that NEnum is gone. It names
    // value, which is also what the property setter calling this names its argument.
    if (!Enum.IsDefined(value))
      throw new ArgumentException($"'{value}' is not a defined value of {typeof(T).Name}.", nameof(value));
    return value;
  }
}
