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

using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders textframes.
/// </summary>
internal class TextFrameRenderer : ShapeRenderer
{
  internal TextFrameRenderer(XGraphics gfx, TextFrame textframe, FieldInfos fieldInfos)
    : base(gfx, textframe, fieldInfos)
  {
    this.textframe = textframe;
    var textFrameRenderInfo = new TextFrameRenderInfo
    {
      shape = shape
    };
    this.renderInfo = textFrameRenderInfo;
  }

  internal TextFrameRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
    : base(gfx, renderInfo, fieldInfos)
  {
    textframe = (TextFrame)renderInfo.DocumentObject;
  }

  internal override void Format(Area area, FormatInfo previousFormatInfo)
  {
    var formattedTextFrame = new FormattedTextFrame(textframe, DocumentRenderer, fieldInfos);
    formattedTextFrame.Format(Gfx);
    ((TextFrameFormatInfo)renderInfo.FormatInfo).formattedTextFrame = formattedTextFrame;
    base.Format(area, previousFormatInfo);
  }

  internal override void Render()
  {
    using (Tagger.Artifact(Gfx))
      RenderFilling();

    // A text frame holds real content — paragraphs and tables, which tag themselves — so it is a
    // section of the document rather than a figure. Its fill and its border are decoration and go
    // out as artifacts; nothing here needs alternate text, because everything inside it is text that
    // a reader can read for itself.
    Tagger.EndList();
    using (Tagger.Container(Gfx, textframe, PdfTag.Section))
      RenderContent();

    using (Tagger.Artifact(Gfx))
      RenderLine();
  }

  private void RenderContent()
  {
    var formattedTextFrame = ((TextFrameFormatInfo)renderInfo.FormatInfo).formattedTextFrame;
    var renderInfos = formattedTextFrame.GetRenderInfos();
    if (renderInfos == null)
      return;

    var state = Transform();
    RenderByInfos(renderInfos);
    ResetTransform(state);
  }

  private XGraphicsState Transform()
  {
    var frameContentArea = renderInfo.LayoutInfo.ContentArea;
    var state = Gfx.Save();
    XUnit xPosition;
    XUnit yPosition;
    switch (textframe.Orientation)
    {
      case TextOrientation.Downward:
      case TextOrientation.Vertical:
      case TextOrientation.VerticalFarEast:
        xPosition = frameContentArea.X + frameContentArea.Width;
        yPosition = frameContentArea.Y;
        Gfx.TranslateTransform(xPosition, yPosition);
        Gfx.RotateTransform(90);
        break;

      case TextOrientation.Upward:
        state = Gfx.Save();
        xPosition = frameContentArea.X;
        yPosition = frameContentArea.Y + frameContentArea.Height;
        Gfx.TranslateTransform(xPosition, yPosition);
        Gfx.RotateTransform(-90);
        break;

      default:
        xPosition = frameContentArea.X;
        yPosition = frameContentArea.Y;
        Gfx.TranslateTransform(xPosition, yPosition);
        break;
    }
    return state;
  }

  private void ResetTransform(XGraphicsState state)
  {
    if (state != null)
      Gfx.Restore(state);
  }
  private TextFrame textframe;
}
