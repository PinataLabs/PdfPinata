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
using System.Diagnostics;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Abstract base class for all classes that store rendering information.
/// </summary>
public abstract class RenderInfo
{
  internal abstract FormatInfo FormatInfo
  {
    get;
  }

  /// <summary>Gets the layout information worked out for the object being rendered.</summary>
  public LayoutInfo LayoutInfo { get; } = new();

  /// <summary>Gets the document object this render information describes.</summary>
  public abstract DocumentObject DocumentObject
  {
    get;
  }

  internal virtual void RemoveEnding()
  {
    Debug.Assert(false, "Unexpected call of RemoveEnding");
  }

  /// <summary>
  /// The render infos an area provider was handed by its formatter, copied into an array.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Not <see cref="ArrayList.ToArray(System.Type)"/>: it builds the array type at run time, which
  /// carries RequiresDynamicCode and an AOT compiler cannot always have code for.
  /// </para>
  /// <para>
  /// It takes no null. A provider that was never handed any says for itself what it answers, and
  /// they do not agree: <see cref="FormattedHeaderFooter"/> and <see cref="FormattedFootnote"/>
  /// answer an empty array, the others null, and their callers are written for the one they get.
  /// </para>
  /// </remarks>
  internal static RenderInfo[] ToArray(ArrayList renderInfos)
  {
    var result = new RenderInfo[renderInfos.Count];
    renderInfos.CopyTo(result);
    return result;
  }

  internal static XUnit GetTotalHeight(RenderInfo[] renderInfos)
  {
    if (renderInfos == null || renderInfos.Length == 0)
      return 0;

    var lastIdx = renderInfos.Length - 1;
    var firstRenderInfo = renderInfos[0];
    var lastRenderInfo = renderInfos[lastIdx];
    var firstLayoutInfo = firstRenderInfo.LayoutInfo;
    var lastLayoutInfo = lastRenderInfo.LayoutInfo;
    XUnit top = firstLayoutInfo.ContentArea.Y - firstLayoutInfo.MarginTop;
    XUnit bottom = lastLayoutInfo.ContentArea.Y + lastLayoutInfo.ContentArea.Height;
    bottom += lastLayoutInfo.MarginBottom;
    return bottom - top;
  }
}
