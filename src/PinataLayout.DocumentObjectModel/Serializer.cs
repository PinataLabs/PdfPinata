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
using System.Diagnostics;
using System.IO;
using System.Text;
using PinataLayout.DocumentObjectModel.Internals;
using System.Reflection;
using PdfPinata;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Object to be passed to the Serialize function of a DocumentObject to convert
/// it into DDL.
/// </summary>
internal class Serializer
{
  /// <summary>
  /// A Serializer object for converting MDDOM into DDL.
  /// </summary>
  /// <param name="textWriter">A TextWriter to write DDL in.</param>
  /// <param name="indent">Indent of a new block. Default is 2.</param>
  /// <param name="initialIndent">Initial indent to start with.</param>
  internal Serializer(TextWriter textWriter, int indent, int initialIndent)
  {
    ArgumentNullException.ThrowIfNull(textWriter);

    this.textWriter = textWriter;
    this.indent = indent;
    writeIndent = initialIndent;
    if (textWriter is StreamWriter)
      WriteStamp();
  }

  /// <summary>
  /// Initializes a new instance of the Serializer class with the specified TextWriter.
  /// </summary>
  internal Serializer(TextWriter textWriter) : this(textWriter, 2, 0) { }

  /// <summary>
  /// Initializes a new instance of the Serializer class with the specified TextWriter and indentation.
  /// </summary>
  internal Serializer(TextWriter textWriter, int indent) : this(textWriter, indent, 0) { }

  protected TextWriter textWriter;

  /// <summary>
  /// Gets or sets the indentation for a new indentation level.
  /// </summary>
  internal int Indent
  {
    get => indent;
    set => indent = value;
  }
  protected int indent;

  /// <summary>
  /// Gets or sets the initial indentation which precede every line.
  /// </summary>
  internal int InitialIndent
  {
    get => writeIndent;
    set => writeIndent = value;
  }
  protected int writeIndent;

  /// <summary>
  /// Increases indent of DDL code.
  /// </summary>
  private void IncreaseIndent()
  {
    writeIndent += indent;
  }

  /// <summary>
  /// Decreases indent of DDL code.
  /// </summary>
  private void DecreaseIndent()
  {
    writeIndent -= indent;
  }

  /// <summary>
  /// Writes the header for a DDL file containing copyright and creation time information.
  /// </summary>
  internal void WriteStamp()
  {
    if (!fWriteStamp)
      return;

    WriteComment("Created by PinataLayout Document Object Model");
    WriteComment(string.Format("generated file created {0:d} at {0:t}", GlobalTimeSettings.Now));
  }

  /// <summary>
  /// Appends a string indented without line feed.
  /// </summary>
  internal void Write(string str)
  {
    var wrappedStr = DoWordWrap(str);
    if (wrappedStr.Length < str.Length && wrappedStr != "")
    {
      WriteLineToStream(wrappedStr);
      Write(str[wrappedStr.Length..]);
    }
    else
    {
      WriteToStream(str);
    }
    CommitText();
  }

  /// <summary>
  /// Writes a string indented with line feed.
  /// </summary>
  internal void WriteLine(string str)
  {
    var wrappedStr = DoWordWrap(str);
    if (wrappedStr.Length < str.Length)
    {
      WriteLineToStream(wrappedStr);
      WriteLine(str[wrappedStr.Length..]);
    }
    else
    {
      WriteLineToStream(wrappedStr);
    }
    CommitText();
  }

  /// <summary>
  /// Returns the part of the string str that fits into the line (up to 80 chars).
  /// If Wordwrap is impossible it returns the input-string str itself.
  /// </summary>
  private string DoWordWrap(string str)
  {
    if (str.Length + writeIndent < lineBreakBeyond)
      return str;

    var idxCRLF = str.IndexOf("\x0D\x0A", StringComparison.Ordinal);
    if (idxCRLF > 0 && idxCRLF + writeIndent <= lineBreakBeyond)
      return str[..(idxCRLF + 1)];

    // Where the line runs out of room, kept inside the string at both ends: an indent already
    // past the limit leaves no room at all, and a string reaching the limit exactly leaves
    // nothing beyond it to search.
    var wrapAt = Math.Min(Math.Max(lineBreakBeyond - writeIndent, 0), str.Length);

    var splitIndexBlank = str[..wrapAt].LastIndexOf(' ');
    var splitIndexCRLF = str[..wrapAt].LastIndexOf("\x0D\x0A", StringComparison.Ordinal);
    var splitIndex = Math.Max(splitIndexBlank, splitIndexCRLF);
    if (splitIndex == -1)
      // Nothing to break on within the line, so take the first blank past it. Asking instead for
      // the smaller of the next blank and the next line break answered -1 whenever either kind
      // was absent, since a missing one reads as -1 and no real index is smaller than that; a
      // line holding blanks but no line break - which is most prose - was therefore never
      // wrapped at all. A line break is not looked for out here on purpose: one past the limit
      // already ends the line when it is written, so breaking at it would only hand the line
      // break itself to the next write and end the line twice.
      splitIndex = str.IndexOf(' ', wrapAt);
    return splitIndex > 0 ? str[..splitIndex] : str;
  }

  /// <summary>
  /// Writes an empty line.
  /// </summary>
  internal void WriteLine()
  {
    WriteLine(string.Empty);
  }

  /// <summary>
  /// Write a line without committing (without marking the text as serialized).
  /// </summary>
  internal void WriteLineNoCommit(string str)
  {
    WriteLineToStream(str);
  }

  /// <summary>
  /// Write a line without committing (without marking the text as serialized).
  /// </summary>
  internal void WriteLineNoCommit()
  {
    WriteLineNoCommit(string.Empty);
  }

  /// <summary>
  /// Writes a text as comment and automatically word-wraps it.
  /// </summary>
  internal void WriteComment(string comment)
  {
    if (comment is null or "")
      return;

    // If the comment holds a line end, split it up recursively, one "//" line per line. A CR or
    // an LF on its own ends a line for the scanner just as a CRLF does, so a comment left in one
    // piece across either would end there, and the rest of it be read as MDDDL.
    var lineEnd = comment.IndexOfAny(LineEndChars);
    if (lineEnd != -1)
    {
      var lineEndLength = IsCrLfAt(comment, lineEnd) ? 2 : 1;
      WriteComment(comment[..lineEnd]);
      WriteComment(comment[(lineEnd + lineEndLength)..]);
      return;
    }
    CloseUpLine();
    var chopBeyond = lineBreakBeyond - indent - "// ".Length;
    while (comment.Length > 0)
    {
      WriteLineToStream("// " + ChopCommentLine(ref comment, chopBeyond));
      CommitText();
    }
  }

  private static bool IsCrLfAt(string text, int index)
    => text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n';

  /// <summary>
  /// Takes the first line of a word-wrapped comment off the front of it: the words that fit before
  /// chopBeyond, or else up to the first blank beyond it, or else the whole of it. The blank it
  /// breaks at belongs to neither line.
  /// </summary>
  private static string ChopCommentLine(ref string comment, int chopBeyond)
  {
    if (comment.Length > chopBeyond)
    {
      var idxChop = comment.LastIndexOf(' ', chopBeyond);
      if (idxChop == -1)
        idxChop = comment.IndexOf(' ', chopBeyond);

      if (idxChop != -1)
      {
        var line = comment[..idxChop];
        comment = comment[(idxChop + 1)..];
        return line;
      }
    }

    var whole = comment;
    comment = string.Empty;
    return whole;
  }

  /// <summary>
  /// Writes a line break if the current position is not at the beginning
  /// of a new line.
  /// </summary>
  internal void CloseUpLine()
  {
    if (linePos > 0)
      WriteLine();
  }

  /// <summary>
  /// Effectively writes text to the stream. The text is automatically indented and
  /// word-wrapped. A given text gets never word-wrapped to keep comments or string
  /// literals unbroken.
  /// </summary>
  private void WriteToStream(string text, bool fLineBreak, bool fAutoIndent)
  {
    // if string contains CR/LF, split up recursively
    var crlf = text.IndexOf("\x0D\x0A", StringComparison.Ordinal);
    if (crlf != -1)
    {
      WriteToStream(text[..crlf], true, fAutoIndent);
      WriteToStream(text[(crlf + 2)..], fLineBreak, fAutoIndent);
      return;
    }

    if (text.Length > 0 && WriteOnLine(text, fAutoIndent))
      fLineBreak = true;

    if (!fLineBreak)
      return;

    textWriter.WriteLine(string.Empty);  // what a line break is may depend on encoding
    linePos = 0;
  }

  /// <summary>
  /// Writes text on the current line, indented when it starts the line. Answers whether the line
  /// is now long enough to need a line break.
  /// </summary>
  private bool WriteOnLine(string text, bool fAutoIndent)
  {
    var len = text.Length;
    if (linePos <= 0 && fAutoIndent)
    {
      text = Indentation + text;
      len += writeIndent;
    }
    textWriter.Write(text);
    linePos += len;
    // wordwrap required?
    return linePos > lineBreakBeyond;
  }

  /// <summary>
  /// Write the text into the stream without breaking it and adds an indentation to it.
  /// </summary>
  private void WriteToStream(string text)
  {
    WriteToStream(text, false, true);
  }

  /// <summary>
  /// Write a line to the stream.
  /// </summary>
  private void WriteLineToStream(string text)
  {
    WriteToStream(text, true, true);
  }

  /// <summary>
  /// Start attribute part.
  /// </summary>
  internal int BeginAttributes()
  {
    var pos = Position;
    WriteLineNoCommit("[");
    IncreaseIndent();
    BeginBlock();
    return pos;
  }

  /// <summary>
  /// Start attribute part.
  /// </summary>
  internal int BeginAttributes(string str)
  {
    var pos = Position;
    WriteLineNoCommit(str);
    WriteLineNoCommit("[");
    IncreaseIndent();
    BeginBlock();
    return pos;
  }

  /// <summary>
  /// End attribute part.
  /// </summary>
  internal bool EndAttributes()
  {
    DecreaseIndent();
    WriteLineNoCommit("]");
    return EndBlock();
  }

  /// <summary>
  /// End attribute part.
  /// </summary>
  internal bool EndAttributes(int pos)
  {
    var commit = EndAttributes();
    if (!commit)
      Position = pos;
    return commit;
  }

  /// <summary>
  /// Write attribute of type Unit, Color, int, float, double, bool, string or enum.
  /// </summary>
  internal void WriteSimpleAttribute(string valueName, object value)
  {
    if (value is INullableValue ival)
      value = ival.GetValue();

    var type = value.GetType();
    var text = SimpleAttributeText(value);
    if (text == null)
    {
      var message = $"Type '{type}' of value '{valueName}' not supported";
      Debug.Assert(false, message);
      return;
    }

    WriteLine(valueName + " = " + text);
  }

  /// <summary>
  /// Writes a simple attribute held in a nullable field, or nothing when the field holds no value.
  /// </summary>
  internal void WriteSimpleAttributeIfSet<T>(string valueName, T? value) where T : struct
  {
    if (value.HasValue)
      WriteSimpleAttribute(valueName, value.Value);
  }

  /// <summary>
  /// Serializes the child object an owner holds under a name, unless the owner says it is null.
  /// </summary>
  internal void SerializeUnlessNull(DocumentObject owner, string name, DocumentObject child)
  {
    if (!owner.IsNull(name))
      child.Serialize(this);
  }

  /// <summary>
  /// The DDL a simple attribute's value is written as, or null for a type that has none.
  /// </summary>
  private static string SimpleAttributeText(object value)
  {
    return value switch
    {
      Unit unit => UnitText(unit),
      float single => single.ToString(System.Globalization.CultureInfo.InvariantCulture),
      double real => real.ToString(System.Globalization.CultureInfo.InvariantCulture),
      bool => value.ToString().ToLower(),
      string text => StringLiteral(text),
      int or Enum or Color => value.ToString(),
      _ => null
    };
  }

  /// <summary>
  /// A unit in points as a bare number, any other in quotes.
  /// </summary>
  private static string UnitText(Unit unit)
  {
    var strUnit = unit.ToString();
    return unit.Type == UnitType.Point ? strUnit : "\"" + strUnit + "\"";
  }

  /// <summary>
  /// A string as a quoted DDL literal, with its backslashes and quotes escaped.
  /// </summary>
  private static string StringLiteral(string value)
  {
    var sb = new StringBuilder(value);
    sb.Replace("\\", "\\\\");
    sb.Replace("\"", "\\\"");
    return "\"" + sb + "\"";
  }

  /// <summary>
  /// Start content part.
  /// </summary>
  internal int BeginContent()
  {
    var pos = Position;
    WriteLineNoCommit("{");
    IncreaseIndent();
    BeginBlock();
    return pos;
  }

  /// <summary>
  /// Start content part.
  /// </summary>
  internal int BeginContent(string str)
  {
    var pos = Position;
    WriteLineNoCommit(str);
    WriteLineNoCommit("{");
    IncreaseIndent();
    BeginBlock();
    return pos;
  }

  /// <summary>
  /// End content part.
  /// </summary>
  internal bool EndContent()
  {
    DecreaseIndent();
    WriteLineNoCommit("}");
    return EndBlock();
  }

  /// <summary>
  /// End content part.
  /// </summary>
  internal bool EndContent(int pos)
  {
    var commit = EndContent();
    if (!commit)
      Position = pos;
    return commit;
  }

  /// <summary>
  /// Starts a new nesting block.
  /// </summary>
  internal int BeginBlock()
  {
    var pos = Position;
    if (stackIdx + 1 >= commitTextStack.Length)
      throw new ArgumentException("Block nesting level exhausted.");
    stackIdx += 1;
    commitTextStack[stackIdx] = false;
    return pos;
  }

  /// <summary>
  /// Ends a nesting block.
  /// </summary>
  internal bool EndBlock()
  {
    if (stackIdx <= 0)
      throw new ArgumentException("Block nesting level underflow.");
    stackIdx -= 1;
    if (commitTextStack[stackIdx + 1])
      commitTextStack[stackIdx] = commitTextStack[stackIdx + 1];
    return commitTextStack[stackIdx + 1];
  }

  /// <summary>
  /// Ends a nesting block.
  /// </summary>
  internal bool EndBlock(int pos)
  {
    var commit = EndBlock();
    if (!commit)
      Position = pos;
    return commit;
  }

  /// <summary>
  /// Gets or sets the position within the underlying stream.
  /// </summary>
  private int Position
  {
    get
    {
      textWriter.Flush();
      if (textWriter is StreamWriter streamWriter)
        return (int)streamWriter.BaseStream.Position;
      if (textWriter is StringWriter stringWriter)
        return stringWriter.GetStringBuilder().Length;
      return 0;
    }
    set
    {
      textWriter.Flush();
      if (textWriter is StreamWriter streamWriter)
        streamWriter.BaseStream.SetLength(value);
      else
        (textWriter as StringWriter)?.GetStringBuilder().Length = value;
    }
  }

  /// <summary>
  /// Flushes the buffers of the underlying text writer.
  /// </summary>
  internal void Flush()
  {
    textWriter.Flush();
  }

  /// <summary>
  /// Returns an indent string of blanks.
  /// </summary>
  private static string Ind(int indent)
  {
    return new String(' ', indent);
  }

  /// <summary>
  /// Gets an indent string of current indent.
  /// </summary>
  private string Indentation => Ind(writeIndent);

  /// <summary>
  /// Marks the current block as 'committed'. That means the block contains
  /// serialized data.
  /// </summary>
  private void CommitText()
  {
    commitTextStack[stackIdx] = true;
  }
  private int stackIdx;
  private readonly bool[] commitTextStack = new bool[32];

  private int linePos;
  private readonly int lineBreakBeyond = 200;
  private static readonly char[] LineEndChars = ['\r', '\n'];
  private readonly bool fWriteStamp = false;
}
