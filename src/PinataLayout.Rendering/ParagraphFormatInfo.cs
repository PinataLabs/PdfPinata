#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using System.Collections;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel;

namespace PinataLayout.Rendering;

/// <summary>
/// Vertical measurements of a paragraph line.
/// </summary>
internal struct VerticalLineInfo
{
  internal VerticalLineInfo(XUnit height, XUnit descent, XUnit inherentlineSpace)
  {
    this.height = height;
    this.descent = descent;
    this.inherentlineSpace = inherentlineSpace;
  }
  internal XUnit height;
  internal XUnit descent;
  internal XUnit inherentlineSpace;
}

/// <summary>
/// Line info object used by the paragraph format info.
/// </summary>
internal struct LineInfo
{
  internal ParagraphIterator startIter;
  internal ParagraphIterator endIter;
  internal XUnit wordsWidth;
  internal XUnit lineWidth;
  internal int blankCount;
  internal VerticalLineInfo vertical;
  internal ArrayList tabOffsets;
  internal bool reMeasureLine;
  internal DocumentObject lastTab;

  /// <summary>
  /// The measure this line was broken to, where the area it was broken against had something
  /// standing in it. Null otherwise, and null is the ordinary case.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The rendering phase asks the paragraph's content area for this again, one line at a time.
  /// That works only while the content area answers the same question the formatting area did,
  /// and beside a floating shape it does not: the content area is accumulated by <c>Unite</c>,
  /// which takes bounding boxes, so a paragraph straddling the foot of the shape ends up with a
  /// content area spanning the full measure. Its overlapping lines were then broken narrow and
  /// drawn wide - short lines starting at the margin, drawn across the shape.
  /// </para>
  /// <para>
  /// So the line carries the answer forward, but <b>only for an obstructed area</b>. Everywhere
  /// else the content area is the better source because it is the later one: a table formats its
  /// cells in one place and renders them in another, and a rect kept from formatting would put
  /// the cell's text back where the cell used to be.
  /// </para>
  /// </remarks>
  internal Rectangle fittingRect;
}

/// <summary>
/// Formatting information for a paragraph.
/// </summary>
internal class ParagraphFormatInfo : FormatInfo
{
  private ArrayList lineInfos = new();

  internal LineInfo GetLineInfo(int lineIdx)
  {
    // ReSharper disable once PossibleNullReferenceException
    return (LineInfo)lineInfos[lineIdx];
  }

  internal LineInfo GetLastLineInfo()
  {
    // ReSharper disable once PossibleNullReferenceException
    return (LineInfo)lineInfos[LineCount - 1];
  }

  internal LineInfo GetFirstLineInfo()
  {
    // ReSharper disable once PossibleNullReferenceException
    return (LineInfo)lineInfos[0];
  }

  internal void AddLineInfo(LineInfo lineInfo)
  {
    lineInfos.Add(lineInfo);
  }

  internal int LineCount => lineInfos.Count;

  /// <summary>
  ///
  /// </summary>
  /// <param name="mergeInfo"></param>
  /// <returns></returns>
  internal void Append(FormatInfo mergeInfo)
  {
    var formatInfo = (ParagraphFormatInfo)mergeInfo;
    lineInfos.AddRange(formatInfo.lineInfos);
  }

  /// <summary>
  /// Indicates whether the paragraph is ending.
  /// </summary>
  /// <returns>True if the paragraph is ending.</returns>
  internal override bool IsEnding => isEnding;

  internal bool isEnding;

  /// <summary>
  /// Indicates whether the paragraph is starting.
  /// </summary>
  /// <returns>True if the paragraph is starting.</returns>
  internal override bool IsStarting => isStarting;

  internal bool isStarting;

  internal override bool IsComplete => isStarting && isEnding;

  internal override bool IsEmpty => lineInfos.Count == 0;

  internal override bool StartingIsComplete
  {
    get
    {
      if (widowControl)
        return IsComplete || (isStarting && lineInfos.Count >= 2);
      else
        return isStarting;
    }
  }

  internal bool widowControl;

  internal override bool EndingIsComplete
  {
    get
    {
      if (widowControl)
        return IsComplete || (isEnding && lineInfos.Count >= 2);
      else
        return isEnding;
    }
  }

  internal void RemoveEnding()
  {
    if (IsEmpty)
      return;

    if (widowControl && isEnding && LineCount >= 2)
      lineInfos.RemoveAt(LineCount - 2);
    if (LineCount > 0)
      lineInfos.RemoveAt(LineCount - 1);

    isEnding = false;
  }

  internal string listSymbol;
  internal XFont listFont;
  internal Hashtable imageRenderInfos;
}
