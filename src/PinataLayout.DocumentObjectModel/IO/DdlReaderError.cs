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

namespace PinataLayout.DocumentObjectModel.IO;

/// <summary>
/// Represents an error or diagnostic message reported by the DDL reader.
/// </summary>
public class DdlReaderError
{
  /// <summary>
  /// Initializes a new instance of the DdlReaderError class.
  /// </summary>
  public DdlReaderError(DdlErrorLevel errorLevel, string errorMessage, int errorNumber,
    string sourceFile, int sourceLine, int sourceColumn)
  {
    ErrorLevel = errorLevel;
    ErrorMessage = errorMessage;
    _errorNumber = errorNumber;
    SourceFile = sourceFile;
    SourceLine = sourceLine;
    SourceColumn = sourceColumn;
  }

  /// <summary>Initializes a new error that names no source position.</summary>
  public DdlReaderError(DdlErrorLevel errorLevel, string errorMessage, int errorNumber)
  {
    ErrorLevel = errorLevel;
    ErrorMessage = errorMessage;
    _errorNumber = errorNumber;
  }

  /// <summary>The number reported for an error that carries none.</summary>
  public const int NoErrorNumber = -1;

  /// <summary>
  /// Returns a String that represents the current DdlReaderError.
  /// </summary>
  public override string ToString()
  {
    return $"[{SourceFile}({SourceLine},{SourceColumn}):] xxx DDL{_errorNumber}: {ErrorMessage}";
  }

  /// <summary>
  /// Specifies the severity of this diagnostic.
  /// </summary>
  public readonly DdlErrorLevel ErrorLevel;

  /// <summary>
  /// Specifies the diagnostic message text.
  /// </summary>
  public readonly string ErrorMessage;

  /// <summary>
  /// Specifies the diagnostic number.
  /// </summary>
  private readonly int _errorNumber;

  /// <summary>
  /// Specifies the filename of the DDL text that caused the diagnostic,
  /// or an empty string ("").
  /// </summary>
  public readonly string SourceFile;

  /// <summary>
  /// Specifies the line of the DDL text that caused the diagnostic (1 based),
  /// or 0 if there is no line information.
  /// </summary>
  public readonly int SourceLine;

  /// <summary>
  /// Specifies the column of the source text that caused the diagnostic (1 based),
  /// or 0 if there is no column information.
  /// </summary>
  public readonly int SourceColumn;
}
