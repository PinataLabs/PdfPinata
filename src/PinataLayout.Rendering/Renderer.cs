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

using System;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;

namespace PinataLayout.Rendering;

/// <summary>
/// Abstract base class for all renderers.
/// </summary>
internal abstract class Renderer
{
  internal readonly static XUnit Tolerance = XUnit.FromPoint(0.001);
  private XUnit _maxElementHeight = -1;

  /// <summary>
  /// Determines the maximum height a single element may have.
  /// </summary>
  internal XUnit MaxElementHeight
  {
    get => _maxElementHeight;
    set => _maxElementHeight = value;
  }

  internal Renderer(XGraphics gfx, DocumentObject documentObject, FieldInfos fieldInfos)
  {
    this.DocumentObject = documentObject;
    this.Gfx = gfx;
    this.fieldInfos = fieldInfos;
  }

  internal Renderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
  {
    DocumentObject = renderInfo.DocumentObject;
    this.Gfx = gfx;
    this.renderInfo = renderInfo;
    this.fieldInfos = fieldInfos;
  }
  /// <summary>
  /// In inherited classes, gets a layout info with only margin and break information set.
  /// It can be taken before the documen object is formatted.
  /// </summary>
  /// <remarks>
  /// In inherited classes, the following parts are set properly:
  /// MarginTop, MarginLeft, MarginRight, MarginBottom,
  /// KeepTogether, KeepWithNext, PagebreakBefore, Floating,
  /// VerticalReference, HorizontalReference.
  /// </remarks>
  internal abstract LayoutInfo InitialLayoutInfo
  {
    get;
  }

  /// <summary>
  /// Renders the contents shifted to the given Coordinates.
  /// </summary>
  /// <param name="xShift">The x shift.</param>
  /// <param name="yShift">The y shift.</param>
  /// <param name="renderInfos">The render infos.</param>
  protected void RenderByInfos(XUnit xShift, XUnit yShift, RenderInfo[] renderInfos)
  {
    if (renderInfos == null)
      return;

    foreach (var info in renderInfos)
    {
      var savedX = info.LayoutInfo.ContentArea.X;
      var savedY = info.LayoutInfo.ContentArea.Y;
      info.LayoutInfo.ContentArea.X += xShift;
      info.LayoutInfo.ContentArea.Y += yShift;
      var renderer = Create(Gfx, DocumentRenderer, info, fieldInfos);
      renderer.Render();
      info.LayoutInfo.ContentArea.X = savedX;
      info.LayoutInfo.ContentArea.Y = savedY;
    }
  }

  protected void RenderByInfos(RenderInfo[] renderInfos)
  {
    RenderByInfos(0, 0, renderInfos);
  }


  /// <summary>
  /// Gets the render information necessary to render and position the object.
  /// </summary>
  internal RenderInfo RenderInfo => renderInfo;

  protected RenderInfo renderInfo;

  /// <summary>
  /// Sets the field infos object.
  /// </summary>
  /// <remarks>This property is set by the AreaProvider.</remarks>
  internal FieldInfos FieldInfos
  {
    set => fieldInfos = value;
  }
  protected FieldInfos fieldInfos;

  /// <summary>
  /// Renders (draws) the object to the Graphics object.
  /// </summary>
  internal abstract void Render();

  /// <summary>
  /// Formats the object by calculating distances and linebreaks and stopping when the area is filled.
  /// </summary>
  /// <param name="area">The area to render into.</param>
  /// <param name="previousFormatInfo">An information object received from a previous call of Format().
  /// Null for the first call.</param>
  internal abstract void Format(Area area, FormatInfo previousFormatInfo);

  /// <summary>
  /// Creates a fitting renderer for the given document object for formatting.
  /// </summary>
  /// <param name="gfx">The XGraphics object to do measurements on.</param>
  /// <param name="documentRenderer">The document renderer.</param>
  /// <param name="documentObject">the document object to format.</param>
  /// <param name="fieldInfos">The field infos.</param>
  /// <returns>The fitting Renderer.</returns>
  internal static Renderer Create(XGraphics gfx, DocumentRenderer documentRenderer, DocumentObject documentObject, FieldInfos fieldInfos)
  {
    Renderer renderer = null;
    if (documentObject is Paragraph)
      renderer = new ParagraphRenderer(gfx, (Paragraph)documentObject, fieldInfos);
    else if (documentObject is Table)
      renderer = new TableRenderer(gfx, (Table)documentObject, fieldInfos);
    else if (documentObject is PageBreak)
      renderer = new PageBreakRenderer(gfx, (PageBreak)documentObject, fieldInfos);
    else if (documentObject is TextFrame)
      renderer = new TextFrameRenderer(gfx, (TextFrame)documentObject, fieldInfos);
    else if (documentObject is Chart)
      renderer = new ChartRenderer(gfx, (Chart)documentObject, fieldInfos);
    else if (documentObject is Image)
      renderer = new ImageRenderer(gfx, (Image)documentObject, fieldInfos);
    else if (documentObject is Barcode)
      throw NoBarcodeRenderer();

    if (renderer != null)
      renderer.DocumentRenderer = documentRenderer;

    return renderer;
  }

  /// <summary>
  /// Creates a fitting renderer for the render info to render and layout with.
  /// </summary>
  /// <param name="gfx">The XGraphics object to render on.</param>
  /// <param name="documentRenderer">The document renderer.</param>
  /// <param name="renderInfo">The RenderInfo object stored after a previous call of Format().</param>
  /// <param name="fieldInfos">The field infos.</param>
  /// <returns>The fitting Renderer.</returns>
  internal static Renderer Create(XGraphics gfx, DocumentRenderer documentRenderer, RenderInfo renderInfo, FieldInfos fieldInfos)
  {
    Renderer renderer = renderInfo.DocumentObject switch
    {
      Paragraph => new ParagraphRenderer(gfx, renderInfo, fieldInfos),
      Table => new TableRenderer(gfx, renderInfo, fieldInfos),
      PageBreak => new PageBreakRenderer(gfx, renderInfo, fieldInfos),
      TextFrame => new TextFrameRenderer(gfx, renderInfo, fieldInfos),
      Chart => new ChartRenderer(gfx, renderInfo, fieldInfos),
      Image => new ImageRenderer(gfx, renderInfo, fieldInfos),
      Barcode => throw NoBarcodeRenderer(),
      _ => null
    };

    if (renderer != null)
      renderer.DocumentRenderer = documentRenderer;

    return renderer;
  }

  /// <summary>
  /// Reports a bar code that PinataLayout can hold but cannot draw.
  /// </summary>
  /// <remarks>
  /// The DOM has carried <see cref="Barcode"/> since it was forked and this assembly has never had a
  /// renderer for it, so a bar code added to a document produced a null renderer here and was
  /// dropped without a word - no exception, no warning, and a property that read back exactly as it
  /// was set. Null is a legitimate answer for two element kinds, a legend and a bookmark, which is
  /// why the callers cannot simply refuse it and why the refusal belongs here instead.
  /// </remarks>
  static NotSupportedException NoBarcodeRenderer() =>
    new NotSupportedException(
      "PinataLayout has no renderer for the Barcode shape, so one added to a document would be dropped "
      + "from the page without a word. Draw bar codes on the PdfSharp surface instead: build a "
      + "PdfPinata.Drawing.BarCodes.BarCode and pass it to XGraphics.DrawBarCode, or a "
      + "CodeDataMatrix and XGraphics.DrawMatrixCode.");

  /// <summary>
  /// The tagger this pass is building the structure tree with.
  /// </summary>
  /// <remarks>
  /// Shared by every renderer of one pass, because an element belongs to a document object and not
  /// to a renderer: a paragraph broken over two pages is drawn by two renderers and is one paragraph.
  /// A renderer built without a document renderer behind it — which the measuring passes and
  /// <see cref="DocumentRenderer.RenderObject"/> both do — gets a disabled one, so that every scope
  /// it hands back closes nothing and no call site needs to ask whether tagging is on.
  /// </remarks>
  internal StructureTagger Tagger =>
    DocumentRenderer?.Tagger ?? (field ??= new StructureTagger { Enabled = false });

  #region fields

  protected DocumentObject DocumentObject;
  protected DocumentRenderer DocumentRenderer;
  protected XGraphics Gfx;

  #endregion
}
