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
using System.Collections;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted header or footer.
/// </summary>
internal class FormattedHeaderFooter : IAreaProvider
{
    internal FormattedHeaderFooter(HeaderFooter headerFooter, DocumentRenderer documentRenderer, FieldInfos fieldInfos)
    {
        _headerFooter = headerFooter;
        _fieldInfos = fieldInfos;
        _documentRenderer = documentRenderer;
    }

    internal void Format(XGraphics gfx)
    {
        _isFirstArea = true;
        _formatter = new TopDownFormatter(this, _documentRenderer, _headerFooter.Elements);
        _formatter.FormatOnAreas(gfx, false);
    }

    Area IAreaProvider.GetNextArea()
    {
        return _isFirstArea ? new Rectangle(ContentRect.X, ContentRect.Y, ContentRect.Width, double.MaxValue) : null;
    }

    Area IAreaProvider.ProbeNextArea()
    {
        return null;
    }

    FieldInfos IAreaProvider.AreaFieldInfos => _fieldInfos;

    void IAreaProvider.StoreRenderInfos(ArrayList renderInfos)
    {
        _renderInfos = renderInfos;
    }

    bool IAreaProvider.IsAreaBreakBefore(LayoutInfo layoutInfo)
    {
        return false;
    }


    internal RenderInfo[] GetRenderInfos()
    {
        if (_renderInfos != null)
        {
            // Not ToArray(Type): it builds the array type at run time, which carries
            // RequiresDynamicCode and an AOT compiler cannot always have code for.
            var result = new RenderInfo[_renderInfos.Count];
            _renderInfos.CopyTo(result);
            return result;
        }

        return Array.Empty<RenderInfo>();
    }

    internal Rectangle ContentRect
    {
        get => _contentRect;
        set => _contentRect = value;
    }

    private Rectangle _contentRect;

    bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
    {
        IAreaProvider formattedDoc = _documentRenderer.FormattedDocument;
        return formattedDoc.PositionVertically(layoutInfo);
    }

    bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
    {
        var formattedDoc = (IAreaProvider)_documentRenderer.FormattedDocument;
        return formattedDoc.PositionHorizontally(layoutInfo);
    }

    private readonly HeaderFooter _headerFooter;
    private readonly FieldInfos _fieldInfos;
    private TopDownFormatter _formatter;
    private ArrayList _renderInfos;
    private bool _isFirstArea;
    private readonly DocumentRenderer _documentRenderer;
}
