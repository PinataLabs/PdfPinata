#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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
using System.Collections.Generic;
using PdfPinata.Internal;
using PdfPinata.Pdf;
using PdfPinata.Drawing.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;

// ReSharper disable UseNullPropagation
// ReSharper disable RedundantNameQualifier
// ReSharper disable UseNameofExpression

// PdfPinata.Drawing.Pdf shadows the name Pdf in here, so the structure namespace
// needs an alias rather than a relative reference.
using PdfStructure = PdfPinata.Pdf.Structure;

namespace PdfPinata.Drawing; // #??? aufräumen

/// <summary>
/// Holds information about the current state of the XGraphics object.
/// </summary>
[Flags]
enum InternalGraphicsMode
{
    DrawingGdiGraphics,
    DrawingPdfContent,
    DrawingBitmap
}


/// <summary>
/// Represents a drawing surface for a fixed size page.
/// </summary>
public sealed class XGraphics : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the XGraphics class for drawing on a PDF page.
    /// </summary>
    private XGraphics(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit pageUnit, XPageDirection pageDirection)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Owner == null)
            throw new ArgumentException(@"You cannot draw on a page that is not owned by a PdfDocument object.", nameof(page));

        if (page.RenderContent != null)
            throw new InvalidOperationException("An XGraphics object already exists for this page and must be disposed before a new one can be created.");

        // The same question the page tree and the save path ask, asked the same way and answered
        // with the same message, so that a caller who tries to draw and a caller who tries to add
        // a page are told the same thing about the mode they chose.
        page.Owner.EnsureCanModify("drawing on a page");

        _gsStack = new GraphicsStateStack();
        PdfContent content = null;
        switch (options)
        {
            case XGraphicsPdfPageOptions.Replace:
                page.Contents.Elements.Clear();
                goto case XGraphicsPdfPageOptions.Append;

            case XGraphicsPdfPageOptions.Prepend:
                content = page.Contents.PrependContent();
                break;

            case XGraphicsPdfPageOptions.Append:
                content = page.Contents.AppendContent();
                break;
        }
        page.RenderContent = content;
        _renderer = new XGraphicsPdfRenderer(page, this, options);
        switch (pageUnit)
        {
            case XGraphicsUnit.Point:
                _pageSize = new XSize(page.Width, page.Height);
                break;

            case XGraphicsUnit.Inch:
                _pageSize = new XSize(XUnit.FromPoint(page.Width).Inch, XUnit.FromPoint(page.Height).Inch);
                break;

            case XGraphicsUnit.Millimeter:
                _pageSize = new XSize(XUnit.FromPoint(page.Width).Millimeter, XUnit.FromPoint(page.Height).Millimeter);
                break;

            case XGraphicsUnit.Centimeter:
                _pageSize = new XSize(XUnit.FromPoint(page.Width).Centimeter, XUnit.FromPoint(page.Height).Centimeter);
                break;

            case XGraphicsUnit.Presentation:
                _pageSize = new XSize(XUnit.FromPoint(page.Width).Presentation, XUnit.FromPoint(page.Height).Presentation);
                break;

            default:
                throw new NotImplementedException("unit");
        }
        _pageUnit = pageUnit;
        _pageDirection = pageDirection;

        Initialize();
    }

    private XGraphics(XSize size, XGraphicsUnit pageUnit, XPageDirection pageDirection)
    {
        _gsStack = new GraphicsStateStack();
        switch (pageUnit)
        {
            case XGraphicsUnit.Point:
                _pageSize = new XSize(size.Width, size.Height);
                break;

            case XGraphicsUnit.Inch:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Inch, XUnit.FromPoint(size.Height).Inch);
                break;

            case XGraphicsUnit.Millimeter:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Millimeter, XUnit.FromPoint(size.Height).Millimeter);
                break;

            case XGraphicsUnit.Centimeter:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Centimeter, XUnit.FromPoint(size.Height).Centimeter);
                break;

            case XGraphicsUnit.Presentation:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Presentation, XUnit.FromPoint(size.Height).Presentation);
                break;

            default:
                throw new NotImplementedException("unit");
        }
        _pageUnit = pageUnit;
        _pageDirection = pageDirection;

        Initialize();
    }

    private XGraphics(IXGraphicsRenderer renderer, XSize size, XGraphicsUnit pageUnit, XPageDirection pageDirection)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _gsStack = new GraphicsStateStack();
        switch (pageUnit)
        {
            case XGraphicsUnit.Point:
                _pageSize = new XSize(size.Width, size.Height);
                break;

            case XGraphicsUnit.Inch:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Inch, XUnit.FromPoint(size.Height).Inch);
                break;

            case XGraphicsUnit.Millimeter:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Millimeter, XUnit.FromPoint(size.Height).Millimeter);
                break;

            case XGraphicsUnit.Centimeter:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Centimeter, XUnit.FromPoint(size.Height).Centimeter);
                break;

            case XGraphicsUnit.Presentation:
                _pageSize = new XSize(XUnit.FromPoint(size.Width).Presentation, XUnit.FromPoint(size.Height).Presentation);
                break;

            default:
                throw new NotImplementedException($"{nameof(pageUnit)}: {pageUnit}");
        }
        _pageUnit = pageUnit;
        _pageDirection = pageDirection;

        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the XGraphics class used for drawing on a form.
    /// </summary>
    private XGraphics(XForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        _form = form;
        form.AssociateGraphics(this);

        _gsStack = new GraphicsStateStack();
        _drawGraphics = false;
        if (form.Owner != null)
            _renderer = new XGraphicsPdfRenderer(form, this);
        _pageSize = form.Size;
        Initialize();
    }

    /// <summary>
    /// Creates the measure context. This is a graphics context created only for querying measures of text.
    /// Drawing on a measure context has no effect.
    /// </summary>
    public static XGraphics CreateMeasureContext(XSize size, XGraphicsUnit pageUnit, XPageDirection pageDirection)
    {
        return new XGraphics(size, pageUnit, pageDirection);
    }

    /// <summary>Creates a drawing surface that emits to the given renderer.</summary>
    public static XGraphics FromRenderer(IXGraphicsRenderer renderer, XSize size, XGraphicsUnit pageUnit, XPageDirection pageDirection)
    {
        return new XGraphics(renderer, size, pageUnit, pageDirection);
    }

    /// <summary>
    /// Creates the surface for a page and tells the document it exists, through
    /// <see cref="PdfDocument.PageGraphicsCreated"/>, before handing it to the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raised here rather than in the constructor, so that a handler that throws cannot leave the
    /// page with a surface nobody holds: the surface is disposed and the exception goes to the
    /// caller, who can then draw on the page again.
    /// </para>
    /// <para>
    /// The handler draws inside a saved graphics state, so a transform or clip it sets and does not
    /// undo is undone for it and the caller starts where it would have without the handler.
    /// </para>
    /// </remarks>
    private static XGraphics FromPdfPageCore(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit unit, XPageDirection pageDirection)
    {
        var gfx = new XGraphics(page, options, unit, pageDirection);

        // Nothing is allocated for a document nobody is listening to, which is almost every one -
        // PinataLayout draws every page through here.
        if (!page.Owner.HasPageGraphicsCreatedHandlers)
            return gfx;

        try
        {
            var state = gfx.Save();
            page.Owner.OnPageGraphicsCreated(page, gfx);
            gfx.Restore(state);
        }
        catch
        {
            gfx.Dispose();
            throw;
        }

        return gfx;
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page)
    {
        return FromPdfPageCore(page, XGraphicsPdfPageOptions.Append, XGraphicsUnit.Point, XPageDirection.Downwards);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XGraphicsUnit unit)
    {
        return FromPdfPageCore(page, XGraphicsPdfPageOptions.Append, unit, XPageDirection.Downwards);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XPageDirection pageDirection)
    {
        return FromPdfPageCore(page, XGraphicsPdfPageOptions.Append, XGraphicsUnit.Point, pageDirection);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options)
    {
        return FromPdfPageCore(page, options, XGraphicsUnit.Point, XPageDirection.Downwards);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XPageDirection pageDirection)
    {
        return FromPdfPageCore(page, options, XGraphicsUnit.Point, pageDirection);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit unit)
    {
        return FromPdfPageCore(page, options, unit, XPageDirection.Downwards);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Pdf.PdfPage object.
    /// </summary>
    public static XGraphics FromPdfPage(PdfPage page, XGraphicsPdfPageOptions options, XGraphicsUnit unit, XPageDirection pageDirection)
    {
        return FromPdfPageCore(page, options, unit, pageDirection);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Drawing.XPdfForm object.
    /// </summary>
    public static XGraphics FromPdfForm(XPdfForm form)
    {
        return form.Gfx ?? new XGraphics(form);
    }

    /// <summary>
    /// Creates a new instance of the XGraphics class from a PdfPinata.Drawing.XForm object.
    /// </summary>
    public static XGraphics FromForm(XForm form)
    {
        return form.Gfx ?? new XGraphics(form);
    }

    /// <summary>
    /// Internal setup.
    /// </summary>
    private void Initialize()
    {
        _pageOrigin = new XPoint();

        var pageHeight = _pageSize.Height;
        var targetPage = PdfPage;
        var trimOffset = new XPoint();
        if (targetPage != null && targetPage.TrimMargins.AreSet)
        {
            pageHeight += targetPage.SheetExtraHeight;
            trimOffset = targetPage.SheetOffset;
        }

        var matrix = new XMatrix();
        if (_pageDirection != XPageDirection.Downwards)
            matrix.Prepend(new XMatrix(1, 0, 0, -1, 0, pageHeight));

        if (trimOffset != new XPoint())
            matrix.TranslatePrepend(trimOffset.X, -trimOffset.Y);

        DefaultViewMatrix = matrix;
        _transform = new XMatrix();
    }

    /// <summary>
    /// Releases all resources used by this object.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;
            if (disposing)
            {
                // Dispose managed resources.
                if (_associatedImage != null)
                {
                    _associatedImage.DisassociateWithGraphics(this);
                    _associatedImage = null;
                }
            }

            if (_form != null)
                _form.Finish();

            _drawGraphics = false;

            if (_renderer != null)
            {
                _renderer.Close();
                _renderer = null;
            }
        }
    }

    private bool _disposed;

    /// <summary>
    /// Internal hack for PinataLayout. Will be removed in further releases.
    /// Unicode support requires a global refactoring of MigraDoc and will be done in further releases.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once ConvertToAutoProperty
    public PdfFontEncoding MUH  // PinataLayout Unicode Hack...
    {
        get => _muh;
        set => _muh = value;
    }

    private PdfFontEncoding _muh;

    /// <summary>
    /// Gets or sets the unit of measure used for page coordinates.
    /// CURRENTLY ONLY POINT IS IMPLEMENTED.
    /// </summary>
    public XGraphicsUnit PageUnit => _pageUnit;

    private readonly XGraphicsUnit _pageUnit;

    /// <summary>
    /// Gets or sets the value indicating in which direction y-value grow.
    /// </summary>
    public XPageDirection PageDirection
    {
        get => _pageDirection;
        #pragma warning disable S4275 // The setter only refuses a value; the one it accepts is the one there already.
        set
        {
            // Is there really anybody who needes the concept of XPageDirection.Upwards?
            if (value != XPageDirection.Downwards)
                throw new NotImplementedException("PageDirection must be XPageDirection.Downwards in current implementation.");
        }
        #pragma warning restore S4275
    }

    private readonly XPageDirection _pageDirection;

    /// <summary>
    /// Gets the current page origin. Setting the origin is not yet implemented.
    /// </summary>
    public XPoint PageOrigin
    {
        get => _pageOrigin;
        #pragma warning disable S4275 // The setter only refuses a value; the one it accepts is the one there already.
        set
        {
            // Is there really anybody who needes to set the page origin?
            if (value != new XPoint())
                throw new NotImplementedException("PageOrigin cannot be modified in current implementation.");
        }
        #pragma warning restore S4275
    }

    private XPoint _pageOrigin;

    /// <summary>
    /// Gets the current size of the page.
    /// </summary>
    public XSize PageSize => _pageSize;

    private XSize _pageSize;

    #region Drawing

    /// <summary>
    /// Draws a line connecting two XPoint structures.
    /// </summary>
    public void DrawLine(XPen pen, XPoint pt1, XPoint pt2)
    {
        DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);
    }

    /// <summary>
    /// Draws a line connecting the two points specified by coordinate pairs.
    /// </summary>
    public void DrawLine(XPen pen, double x1, double y1, double x2, double y2)
    {
        ArgumentNullException.ThrowIfNull(pen);

        if (_renderer != null)
            _renderer.DrawLines(pen, [new(x1, y1), new(x2, y2)]);
    }

    // ----- DrawLines ----------------------------------------------------------------------------
    /// <summary>
    /// Draws a series of line segments that connect an array of points.
    /// </summary>
    public void DrawLines(XPen pen, XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException(PSSR.PointArrayAtLeast(2), nameof(points));

        if (_renderer != null)
            _renderer.DrawLines(pen, points);
    }

    /// <summary>
    /// Draws a series of line segments that connect an array of x and y pairs.
    /// </summary>
    public void DrawLines(XPen pen, double x, double y, params double[] value)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(value);

        var length = value.Length;
        var points = new XPoint[length / 2 + 1];
        points[0].X = x;
        points[0].Y = y;
        for (var idx = 0; idx < length / 2; idx++)
        {
            points[idx + 1].X = value[2 * idx];
            points[idx + 1].Y = value[2 * idx + 1];
        }
        DrawLines(pen, points);
    }

    // ----- DrawBezier ---------------------------------------------------------------------------
    /// <summary>
    /// Draws a Bézier spline defined by four points.
    /// </summary>
    public void DrawBezier(XPen pen, XPoint pt1, XPoint pt2, XPoint pt3, XPoint pt4)
    {
        DrawBezier(pen, pt1.X, pt1.Y, pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
    }

    /// <summary>
    /// Draws a Bézier spline defined by four points.
    /// </summary>
    public void DrawBezier(XPen pen, double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4)
    {
        ArgumentNullException.ThrowIfNull(pen);

        if (_renderer != null)
            _renderer.DrawBeziers(pen,
                [new(x1, y1), new(x2, y2), new(x3, y3), new(x4, y4)]);
    }

    // ----- DrawBeziers --------------------------------------------------------------------------

    /// <summary>
    /// Draws a series of Bézier splines from an array of points.
    /// </summary>
    public void DrawBeziers(XPen pen, XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);

        var count = points.Length;
        if (count == 0)
            return;

        if ((count - 1) % 3 != 0)
            throw new ArgumentException(@"Invalid number of points for bezier curves. Number must fulfil 4+3n.", nameof(points));

        if (_renderer != null)
            _renderer.DrawBeziers(pen, points);
    }

    // ----- DrawCurve ----------------------------------------------------------------------------

    /// <summary>
    /// Draws a cardinal spline through a specified array of points.
    /// </summary>
    public void DrawCurve(XPen pen, XPoint[] points)
    {
        DrawCurve(pen, points, 0.5);
    }

    /// <summary>
    /// Draws a cardinal spline through a specified array of point using a specified tension.
    /// The drawing begins offset from the beginning of the array.
    /// </summary>
    public void DrawCurve(XPen pen, XPoint[] points, int offset, int numberOfSegments, double tension)
    {
        var points2 = new XPoint[numberOfSegments];
        Array.Copy(points, offset, points2, 0, numberOfSegments);
        DrawCurve(pen, points2, tension);
    }

    /// <summary>
    /// Draws a cardinal spline through a specified array of points using a specified tension.
    /// </summary>
    public void DrawCurve(XPen pen, XPoint[] points, double tension)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);

        var count = points.Length;
        if (count < 2)
            throw new ArgumentException(@"DrawCurve requires two or more points.", nameof(points));

        if (_renderer != null)
            _renderer.DrawCurve(pen, points, tension);
    }

    // ----- DrawArc ------------------------------------------------------------------------------

    /// <summary>
    /// Draws an arc representing a portion of an ellipse.
    /// </summary>
    public void DrawArc(XPen pen, XRect rect, double startAngle, double sweepAngle)
    {
        DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Draws an arc representing a portion of an ellipse.
    /// </summary>
    public void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        ArgumentNullException.ThrowIfNull(pen);

        if (Math.Abs(sweepAngle) >= 360)
        {
            DrawEllipse(pen, x, y, width, height);
        }
        else
        {
            if (_renderer != null)
                _renderer.DrawArc(pen, x, y, width, height, startAngle, sweepAngle);
        }
    }

    // ----- DrawRectangle ------------------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XPen pen, XRect rect)
    {
        DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XPen pen, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(pen);

        if (_drawGraphics)
        {

        }

        if (_renderer != null)
            _renderer.DrawRectangle(pen, null, x, y, width, height);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XBrush brush, XRect rect)
    {
        DrawRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XBrush brush, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (_renderer != null)
            _renderer.DrawRectangle(null, brush, x, y, width, height);
    }

    // ----- stroke and fill -----


    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XPen pen, XBrush brush, XRect rect)
    {
        DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws a rectangle.
    /// </summary>
    public void DrawRectangle(XPen pen, XBrush brush, double x, double y, double width, double height)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawRectangle(pen, brush, x, y, width, height);
    }

    // ----- DrawRectangles -----------------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws a series of rectangles.
    /// </summary>
    public void DrawRectangles(XPen pen, XRect[] rectangles)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(rectangles);

        DrawRectangles(pen, null, rectangles);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a series of rectangles.
    /// </summary>
    public void DrawRectangles(XBrush brush, XRect[] rectangles)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(rectangles);

        DrawRectangles(null, brush, rectangles);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a series of rectangles.
    /// </summary>
    public void DrawRectangles(XPen pen, XBrush brush, XRect[] rectangles)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);
        ArgumentNullException.ThrowIfNull(rectangles);

        var count = rectangles.Length;

        if (_renderer != null)
        {
            for (var idx = 0; idx < count; idx++)
            {
                var rect = rectangles[idx];
                _renderer.DrawRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }
    }

    // ----- DrawRoundedRectangle -----------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XPen pen, XRect rect, XSize ellipseSize)
    {
        DrawRoundedRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
    }

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XPen pen, double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
    {
        ArgumentNullException.ThrowIfNull(pen);

        DrawRoundedRectangle(pen, null, x, y, width, height, ellipseWidth, ellipseHeight);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XBrush brush, XRect rect, XSize ellipseSize)
    {
        DrawRoundedRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
    }

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XBrush brush, double x, double y, double width, double height, double ellipseWidth, double ellipseHeight)
    {
        ArgumentNullException.ThrowIfNull(brush);

        DrawRoundedRectangle(null, brush, x, y, width, height, ellipseWidth, ellipseHeight);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XPen pen, XBrush brush, XRect rect, XSize ellipseSize)
    {
        DrawRoundedRectangle(pen, brush, rect.X, rect.Y, rect.Width, rect.Height, ellipseSize.Width, ellipseSize.Height);
    }

    /// <summary>
    /// Draws a rectangles with round corners.
    /// </summary>
    public void DrawRoundedRectangle(XPen pen, XBrush brush, double x, double y, double width, double height,
        double ellipseWidth, double ellipseHeight)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawRoundedRectangle(pen, brush, x, y, width, height, ellipseWidth, ellipseHeight);
    }

    // ----- DrawEllipse --------------------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XPen pen, XRect rect)
    {
        DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XPen pen, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(pen);

        // No DrawArc defined?
        if (_drawGraphics)
        {

        }

        if (_renderer != null)
            _renderer.DrawEllipse(pen, null, x, y, width, height);
    }

    // ----- fill -----

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XBrush brush, XRect rect)
    {
        DrawEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XBrush brush, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(brush);

        if (_drawGraphics)
        {

        }

        if (_renderer != null)
            _renderer.DrawEllipse(null, brush, x, y, width, height);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XPen pen, XBrush brush, XRect rect)
    {
        DrawEllipse(pen, brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws an ellipse defined by a bounding rectangle.
    /// </summary>
    public void DrawEllipse(XPen pen, XBrush brush, double x, double y, double width, double height)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawEllipse(pen, brush, x, y, width, height);
    }

    /// <summary>
    /// Draws a polygon defined by an array of points.
    /// </summary>
    public void DrawPolygon(XPen pen, XPoint[] points)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException(PSSR.PointArrayAtLeast(2), nameof(points));

        if (_renderer != null)
            _renderer.DrawPolygon(pen, null, points, XFillMode.Alternate);  // XFillMode is ignored
    }

    // ----- fill -----

    /// <summary>
    /// Draws a polygon defined by an array of points.
    /// </summary>
    public void DrawPolygon(XBrush brush, XPoint[] points, XFillMode fillmode)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException(PSSR.PointArrayAtLeast(2), nameof(points));

        if (_renderer != null)
            _renderer.DrawPolygon(null, brush, points, fillmode);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a polygon defined by an array of points.
    /// </summary>
    public void DrawPolygon(XPen pen, XBrush brush, XPoint[] points, XFillMode fillmode)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2)
            throw new ArgumentException(PSSR.PointArrayAtLeast(2), nameof(points));

        if (_renderer != null)
            _renderer.DrawPolygon(pen, brush, points, fillmode);
    }

    // ----- DrawPie ------------------------------------------------------------------------------

    // ----- stroke -----


    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XPen pen, XRect rect, double startAngle, double sweepAngle)
    {
        DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        if (pen == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawPie(pen, null, x, y, width, height, startAngle, sweepAngle);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XBrush brush, XRect rect, double startAngle, double sweepAngle)
    {
        DrawPie(brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XBrush brush, double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        if (brush == null)
            throw new ArgumentNullException(nameof(brush), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawPie(null, brush, x, y, width, height, startAngle, sweepAngle);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XPen pen, XBrush brush, XRect rect, double startAngle, double sweepAngle)
    {
        DrawPie(pen, brush, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);
    }

    /// <summary>
    /// Draws a pie defined by an ellipse.
    /// </summary>
    public void DrawPie(XPen pen, XBrush brush, double x, double y, double width, double height, double startAngle, double sweepAngle)
    {
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        if (_renderer != null)
            _renderer.DrawPie(pen, brush, x, y, width, height, startAngle, sweepAngle);
    }

    // ----- DrawClosedCurve ----------------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XPen pen, XPoint[] points)
    {
        DrawClosedCurve(pen, null, points, XFillMode.Alternate, 0.5);
    }


    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XPen pen, XPoint[] points, double tension)
    {
        DrawClosedCurve(pen, null, points, XFillMode.Alternate, tension);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XBrush brush, XPoint[] points)
    {
        DrawClosedCurve(null, brush, points, XFillMode.Alternate, 0.5);
    }

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XBrush brush, XPoint[] points, XFillMode fillmode)
    {
        DrawClosedCurve(null, brush, points, fillmode, 0.5);
    }

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XBrush brush, XPoint[] points, XFillMode fillmode, double tension)
    {
        DrawClosedCurve(null, brush, points, fillmode, tension);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XPen pen, XBrush brush, XPoint[] points)
    {
        DrawClosedCurve(pen, brush, points, XFillMode.Alternate, 0.5);
    }

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XPen pen, XBrush brush, XPoint[] points, XFillMode fillmode)
    {
        DrawClosedCurve(pen, brush, points, fillmode, 0.5);
    }

    /// <summary>
    /// Draws a closed cardinal spline defined by an array of points.
    /// </summary>
    public void DrawClosedCurve(XPen pen, XBrush brush, XPoint[] points, XFillMode fillmode, double tension)
    {
        if (pen == null && brush == null)
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);
        }

        var count = points.Length;
        if (count == 0)
            return;
        if (count < 2)
            throw new ArgumentException(@"Not enough points.", nameof(points));

        if (_renderer != null)
            _renderer.DrawClosedCurve(pen, brush, points, tension, fillmode);
    }

    // ----- DrawPath -----------------------------------------------------------------------------

    // ----- stroke -----

    /// <summary>
    /// Draws a graphical path.
    /// </summary>
    public void DrawPath(XPen pen, XGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(pen);
        ArgumentNullException.ThrowIfNull(path);

        if (_renderer != null)
            _renderer.DrawPath(pen, null, path);
    }

    // ----- fill -----

    /// <summary>
    /// Draws a graphical path.
    /// </summary>
    public void DrawPath(XBrush brush, XGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(path);

        if (_renderer != null)
            _renderer.DrawPath(null, brush, path);
    }

    // ----- stroke and fill -----

    /// <summary>
    /// Draws a graphical path.
    /// </summary>
    public void DrawPath(XPen pen, XBrush brush, XGraphicsPath path)
    {
        if (pen == null && brush == null)
        {
            // ReSharper disable once NotResolvedInText
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);
        }
        ArgumentNullException.ThrowIfNull(path);

        if (_renderer != null)
            _renderer.DrawPath(pen, brush, path);
    }

    // ----- DrawString ---------------------------------------------------------------------------
    //
    // Every overload below draws one line. A line break in the string is not a line break here:
    // a tab is drawn as the single space it is measured as, and every other character below 32 -
    // \n and \r among them - is dropped, so that the glyphs drawn are the glyphs MeasureString
    // measured. Wrapping, breaking, tab stops and justification belong to XTextFormatter and to
    // PinataLayout's ParagraphRenderer, both of which split a paragraph into lines before any of this
    // is called. See PdfPinata/Fonts/TextNormalization.cs for the filtering rule itself.

    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string s, XFont font, XBrush brush, XPoint point)
    {
        DrawString(s, font, brush, new XRect(point.X, point.Y, 0, 0), XStringFormats.Default);
    }


    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string s, XFont font, XBrush brush, XPoint point, XStringFormat format)
    {
        DrawString(s, font, brush, new XRect(point.X, point.Y, 0, 0), format);
    }

    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string s, XFont font, XBrush brush, double x, double y)
    {
        DrawString(s, font, brush, new XRect(x, y, 0, 0), XStringFormats.Default);
    }

    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string s, XFont font, XBrush brush, double x, double y, XStringFormat format)
    {
        DrawString(s, font, brush, new XRect(x, y, 0, 0), format);
    }


    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string s, XFont font, XBrush brush, XRect layoutRectangle)
    {
        DrawString(s, font, brush, layoutRectangle, XStringFormats.Default);
    }

    /// <summary>
    /// Draws the specified text string as a single line.
    /// </summary>
    public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle, XStringFormat format)
    {
        ArgumentNullException.ThrowIfNull(brush);

        DrawString(text, font, null, brush, layoutRectangle, format);
    }

    // ----- outlined text ------------------------------------------------------------------------
    //
    // The overloads below take a pen as well as a brush, as every other Draw method here does.
    // A brush alone fills the glyphs, a pen alone outlines them, and both does both; passing
    // neither is an error, the same way it is for DrawRectangle.

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    public void DrawString(string s, XFont font, XPen pen, XBrush brush, XPoint point)
    {
        DrawString(s, font, pen, brush, new XRect(point.X, point.Y, 0, 0), XStringFormats.Default);
    }

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    public void DrawString(string s, XFont font, XPen pen, XBrush brush, XPoint point, XStringFormat format)
    {
        DrawString(s, font, pen, brush, new XRect(point.X, point.Y, 0, 0), format);
    }

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    public void DrawString(string s, XFont font, XPen pen, XBrush brush, double x, double y)
    {
        DrawString(s, font, pen, brush, new XRect(x, y, 0, 0), XStringFormats.Default);
    }

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    public void DrawString(string s, XFont font, XPen pen, XBrush brush, double x, double y, XStringFormat format)
    {
        DrawString(s, font, pen, brush, new XRect(x, y, 0, 0), format);
    }

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    public void DrawString(string s, XFont font, XPen pen, XBrush brush, XRect layoutRectangle)
    {
        DrawString(s, font, pen, brush, layoutRectangle, XStringFormats.Default);
    }

    /// <summary>
    /// Draws the specified text string as a single line, filled with the brush and outlined with
    /// the pen. Either may be null, but not both.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other <c>DrawString</c> overload comes through here, and none of them draws more than
    /// one line. A tab in <paramref name="text"/> is drawn as the single space
    /// <see cref="MeasureString(string, XFont, XStringFormat)"/> measures it as, and every other
    /// character below 32 - a line feed and a carriage return among them - is dropped rather than
    /// drawn as whatever the font keeps for a character it has no glyph for.
    /// </para>
    /// <para>
    /// For text that wraps, breaks at a line feed, honours tab stops or is justified, use
    /// <see cref="PdfPinata.Drawing.Layout.XTextFormatter"/>, or render a PinataLayout document.
    /// Both split a paragraph into lines and place each one themselves, and both call this once
    /// per line.
    /// </para>
    /// </remarks>
    public void DrawString(string text, XFont font, XPen pen, XBrush brush, XRect layoutRectangle, XStringFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        if (pen == null && brush == null)
            throw new ArgumentNullException(nameof(pen), PSSR.NeedPenOrBrush);

        // A BaseLine line alignment anchors the text to the top edge of the layout rectangle and
        // reads nothing else from it, so a height is surplus rather than contradictory. This used
        // to be refused, which made XStringFormats.Default - which is BaseLineLeft - throw on the
        // most natural overload there is.

        if (text.Length == 0)
            return;

        if (format == null)
            format = XStringFormats.Default;

        if (_renderer != null)
            _renderer.DrawString(text, font, pen, brush, layoutRectangle, format);
    }

    // ----- MeasureString ------------------------------------------------------------------------

    /// <summary>
    /// Measures the specified string when drawn with the specified font.
    /// </summary>
    #pragma warning disable CA1822 // Public API: making it static would break every caller that measures through an XGraphics.
    public XSize MeasureString(string text, XFont font, XStringFormat stringFormat)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(stringFormat);

        var size = FontHelper.MeasureString(text, font, stringFormat);
        return size;
    }
    #pragma warning restore CA1822

    /// <summary>
    /// Measures the specified string when drawn with the specified font.
    /// </summary>
    public XSize MeasureString(string text, XFont font)
    {
        return MeasureString(text, font, XStringFormats.Default);
    }

    // ----- DrawImage ----------------------------------------------------------------------------

    /// <summary>
    /// Draws the specified image.
    /// </summary>
    public void DrawImage(XImage image, XPoint point)
    {
        DrawImage(image, point.X, point.Y);
    }

    /// <summary>
    /// Draws the specified image.
    /// </summary>
    public void DrawImage(XImage image, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(image);

        CheckXPdfFormConsistence(image);


        if (_renderer != null)
            _renderer.DrawImage(image, x, y, image.PointWidth, image.PointHeight);
    }

    /// <summary>
    /// Draws the specified image.
    /// </summary>
    public void DrawImage(XImage image, XRect rect)
    {
        DrawImage(image, rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    /// Draws the specified image.
    /// </summary>
    public void DrawImage(XImage image, double x, double y, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(image);

        CheckXPdfFormConsistence(image);

        if (_renderer != null)
            _renderer.DrawImage(image, x, y, width, height);
    }

    /// <summary>
    /// Draws the specified image.
    /// </summary>
    public void DrawImage(XImage image, XRect destRect, XRect srcRect, XGraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);

        CheckXPdfFormConsistence(image);

        if (_renderer != null)
            _renderer.DrawImage(image, destRect, srcRect, srcUnit);
    }

    /// <summary>
    /// Checks whether drawing is allowed and disposes the XGraphics object, if necessary.
    /// </summary>
    private void CheckXPdfFormConsistence(XImage image)
    {
        var xForm = image as XForm;
        if (xForm != null)
        {
            // Force disposing of XGraphics that draws the content
            xForm.Finish();

            // ReSharper disable once MergeSequentialChecks
            if (_renderer != null && (_renderer as XGraphicsPdfRenderer) != null)
            {
                if (xForm.Owner != null && xForm.Owner != ((XGraphicsPdfRenderer)_renderer).Owner)
                    throw new InvalidOperationException(
                        "A XPdfForm object is always bound to the document it was created for and cannot be drawn in the context of another document.");

                if (xForm == ((XGraphicsPdfRenderer)_renderer).Form)
                    throw new InvalidOperationException(
                        "A XPdfForm cannot be drawn on itself.");
            }
        }
    }

    // ----- DrawBarCode --------------------------------------------------------------------------

    /// <summary>
    /// Draws the specified bar code.
    /// </summary>
    public void DrawBarCode(BarCodes.BarCode barcode, XPoint position)
    {
        barcode.Render(this, XBrushes.Black, null, position);
    }

    /// <summary>
    /// Draws the specified bar code.
    /// </summary>
    public void DrawBarCode(BarCodes.BarCode barcode, XBrush brush, XPoint position)
    {
        barcode.Render(this, brush, null, position);
    }

    /// <summary>
    /// Draws the specified bar code.
    /// </summary>
    public void DrawBarCode(BarCodes.BarCode barcode, XBrush brush, XFont font, XPoint position)
    {
        barcode.Render(this, brush, font, position);
    }

    // ----- DrawMatrixCode -----------------------------------------------------------------------

    /// <summary>
    /// Draws the specified data matrix code.
    /// </summary>
    public void DrawMatrixCode(BarCodes.MatrixCode matrixcode, XPoint position)
    {
        matrixcode.Render(this, XBrushes.Black, position);
    }

    /// <summary>
    /// Draws the specified data matrix code.
    /// </summary>
    public void DrawMatrixCode(BarCodes.MatrixCode matrixcode, XBrush brush, XPoint position)
    {
        matrixcode.Render(this, brush, position);
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Marked content

    /// <summary>
    /// Marks everything drawn until the returned scope is disposed as a piece of content of the
    /// given kind, and adds it to the document's structure tree.
    /// </summary>
    /// <remarks>
    /// <code>
    /// using (gfx.BeginMarkedContent(PdfTag.H1))
    ///     gfx.DrawString("Invoice", headingFont, XBrushes.Black, 40, 60);
    /// </code>
    /// Scopes nest, and a scope opened inside another becomes its child in the tree. Everything on
    /// a page should be inside one of these or inside <see cref="BeginArtifact"/>; content that is
    /// neither is what a PDF/UA validator objects to first.
    /// </remarks>
    /// <param name="tag">What the content is.</param>
    /// <param name="alternateText">
    /// Text standing in for the content for a reader who cannot see it. A figure without it says
    /// nothing at all, so tagging an image and leaving this null is worse than making it an artifact.
    /// </param>
    public IDisposable BeginMarkedContent(PdfStructure.PdfTag tag, string alternateText = null)
    {
        var renderer = PdfRenderer("Marked content can only be written to a PDF page.");
        var page = renderer.Page
            ?? throw new InvalidOperationException(
                "Marked content can only be written to a PDF page, not to a form.");

        var structure = page.Owner.Structure;
        var parent = _markedContent.Count > 0 ? _markedContent.Peek() : null;

        // Suspended before the child element is created, not after. Closing the parent may take its
        // last content item back, and what "last" means has to be the identifier this sequence was
        // opened with rather than whatever the parent's kids happen to end with - creating the child
        // first puts the child element there instead.
        if (parent != null)
            CloseMarkedContent(renderer, page, parent);

        var element = structure.CreateElement(tag, parent);
        if (alternateText != null)
            element.AlternateText = alternateText;

        var mcid = structure.AddMarkedContent(page, element);
        renderer.BeginMarkedContent(tag.Name, mcid);
        _markedContent.Push(element);

        return new MarkedContentScope(this, true, parent != null);
    }

    /// <summary>
    /// Marks everything drawn until the returned scope is disposed as belonging to a structure
    /// element that already exists, rather than to a new one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The overload taking a <see cref="PdfStructure.PdfTag"/> creates an element and nests it inside
    /// whichever scope is open, which is what a caller drawing a page in reading order wants. Anything
    /// building the tree first and drawing afterwards wants this one instead: the shape of a table is
    /// rows and cells, but the order it is drawn in is shading, then content, then borders, and the
    /// two cannot both be expressed by nesting <c>using</c> blocks.
    /// </para>
    /// <para>
    /// Called more than once with the same element, the marks accumulate — which is what makes a
    /// paragraph broken over two pages one paragraph rather than two.
    /// </para>
    /// </remarks>
    /// <param name="element">
    /// An element from <see cref="PdfStructure.PdfStructureBuilder.CreateElement"/>, belonging to the
    /// document being drawn into.
    /// </param>
    public IDisposable BeginMarkedContent(PdfStructure.PdfStructureElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        var renderer = PdfRenderer("Marked content can only be written to a PDF page.");
        var page = renderer.Page
            ?? throw new InvalidOperationException(
                "Marked content can only be written to a PDF page, not to a form.");

        // A structural sequence already open is closed before this one opens and reopened after it,
        // rather than this one being nested inside it. Two marked-content sequences each carrying an
        // MCID, one inside the other, claim the same glyphs for two structure elements: the marks of
        // a link inside a paragraph belong to the link and to the paragraph both, and a reader has no
        // way to decide which. veraPDF warns "Nested MCID" about exactly this.
        //
        // Reopening is what keeps the paragraph whole. An element may own as many content items as it
        // likes - that is already how a paragraph broken over two pages is one paragraph - so the
        // text before the link and the text after it are two items of the same element.
        var suspendedParent = _markedContent.Count > 0;
        if (suspendedParent)
            CloseMarkedContent(renderer, page, _markedContent.Peek());

        var mcid = page.Owner.Structure.AddMarkedContent(page, element);
        renderer.BeginMarkedContent(element.Tag.Name, mcid, actualText: ActualTextForThisSequence(element));
        _markedContent.Push(element);

        return new MarkedContentScope(this, true, suspendedParent);
    }

    /// <summary>
    /// The element's own <c>/ActualText</c>, or null when it never declared one — which is not the
    /// same question as whether the string is empty. An explicit empty replacement is itself a
    /// PDF/UA idiom, and <see cref="PdfStructure.PdfStructureElement.ActualText"/> answers "" for
    /// both that and for never having been set at all, so the distinction has to be asked of
    /// <c>Elements</c> directly.
    /// </summary>
    private static string ActualTextOf(PdfStructure.PdfStructureElement element) =>
        element.Elements.ContainsKey(PdfStructure.PdfStructureElement.Keys.ActualText)
            ? element.ActualText
            : null;

    /// <summary>
    /// What to write as this content item's own inline <c>/ActualText</c>: the element's declared
    /// text the first time this element opens a sequence on this page, and an explicit empty string
    /// - not null - every later time.
    /// </summary>
    /// <remarks>
    /// An element with several content items - a word broken at a hyphen is two, one per line - had
    /// each of them repeat the whole word inline, and a page-scoped reader with no way to know two
    /// sequences name the same element read the word twice. Writing it once and an empty string
    /// after relies on the extractor's own rule for a declared sequence whose text is empty: it
    /// contributes nothing, the same as an artifact, rather than falling back to the glyphs the
    /// later content item actually draws. <see cref="_actualTextEmitted"/> is keyed by object
    /// identity and is a field of this <see cref="XGraphics"/> instance, which draws exactly one
    /// page for its whole lifetime - so nothing further has to reset it at a page boundary.
    /// </remarks>
    private string ActualTextForThisSequence(PdfStructure.PdfStructureElement element)
    {
        var declared = ActualTextOf(element);
        if (declared == null)
            return null;

        return _actualTextEmitted.Add(element) ? declared : "";
    }

    /// <summary>
    /// Which elements have already had their own <c>/ActualText</c> written inline this page. See
    /// <see cref="ActualTextForThisSequence"/>.
    /// </summary>
    private readonly HashSet<PdfStructure.PdfStructureElement> _actualTextEmitted = [];

    /// <summary>
    /// Marks everything drawn until the returned scope is disposed as an artifact: on the page, but
    /// not part of what the page says.
    /// </summary>
    /// <remarks>
    /// Running heads, folios, rules, the shading behind a table. An artifact joins no structure
    /// element and is skipped by anything reading the document aloud, which is the point — a page
    /// number read out between every paragraph is worse than no page number.
    /// </remarks>
    public IDisposable BeginArtifact()
    {
        PdfRenderer("Artifacts can only be written to a PDF page.").BeginArtifact();
        return new MarkedContentScope(this, false);
    }

    private Drawing.Pdf.XGraphicsPdfRenderer PdfRenderer(string message) =>
        _renderer as Drawing.Pdf.XGraphicsPdfRenderer
        ?? throw new InvalidOperationException(message);

    /// <summary>
    /// Closes a structural sequence, and gives its identifier back when the renderer found it empty
    /// and removed it - or the tree would name marks the content stream does not hold.
    /// </summary>
    private static void CloseMarkedContent(Drawing.Pdf.XGraphicsPdfRenderer renderer, PdfPage page,
        PdfStructure.PdfStructureElement element)
    {
        if (renderer.EndMarkedContent())
            return;

        if (page != null && element != null)
            page.Owner.Structure.RemoveLastMarkedContent(page, element);
    }

    /// <summary>
    /// Opens another content item for an element whose sequence was suspended around a nested one.
    /// </summary>
    private void ResumeMarkedContent(PdfStructure.PdfStructureElement element)
    {
        var renderer = PdfRenderer("Marked content can only be written to a PDF page.");
        var page = renderer.Page;
        if (page == null)
            return;

        var mcid = page.Owner.Structure.AddMarkedContent(page, element);
        renderer.BeginMarkedContent(element.Tag.Name, mcid, removableIfEmpty: true,
            actualText: ActualTextForThisSequence(element));
    }

    private readonly Stack<PdfStructure.PdfStructureElement> _markedContent = new();

    /// <summary>
    /// Closes a marked-content sequence when it is disposed, so that a scope cannot be left open by
    /// an early return or an exception.
    /// </summary>
    private sealed class MarkedContentScope : IDisposable
    {
        private readonly XGraphics _gfx;
        private readonly bool _isStructural;
        private readonly bool _resumesParent;
        private bool _closed;

        public MarkedContentScope(XGraphics gfx, bool isStructural, bool resumesParent = false)
        {
            _gfx = gfx;
            _isStructural = isStructural;
            _resumesParent = resumesParent;
        }

        public void Dispose()
        {
            if (_closed)
                return;

            _closed = true;
            var renderer = (Drawing.Pdf.XGraphicsPdfRenderer)_gfx._renderer;

            if (_isStructural)
            {
                var element = _gfx._markedContent.Pop();
                CloseMarkedContent(renderer, renderer.Page, element);

                // The sequence this one interrupted picks up where it left off, in a content item of
                // its own. Left unresumed, everything the parent draws after its child would be
                // outside the structure tree altogether, which is a PDF/UA failure rather than a
                // warning.
                if (_resumesParent && _gfx._markedContent.Count > 0)
                    _gfx.ResumeMarkedContent(_gfx._markedContent.Peek());
            }
            else
            {
                // An artifact that turned out to hold nothing is taken back rather than written. A
                // structural one is not: its identifier is already in the tree, and an element
                // pointing at marks that are no longer there is worse than an empty pair of them.
                renderer.EndArtifact();
            }
        }
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Save and Restore

    /// <summary>
    /// Saves the current state of this XGraphics object and identifies the saved state with the
    /// returned XGraphicsState object.
    /// </summary>
    public XGraphicsState Save()
    {
        var xState = new XGraphicsState();
        var iState = new InternalGraphicsState(xState);
        iState.Transform = _transform;
        _gsStack.Push(iState);

        if (_renderer != null)
            _renderer.Save(xState);

        return xState;
    }

    /// <summary>
    /// Restores the state of this XGraphics object to the state represented by the specified
    /// XGraphicsState object.
    /// </summary>
    public void Restore(XGraphicsState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _gsStack.Restore(state.InternalState);
        _transform = state.InternalState.Transform;

        if (_renderer != null)
            _renderer.Restore(state);
    }

    /// <summary>
    /// Restores the state of this XGraphics object to the state before the most recently call of Save.
    /// </summary>
    public void Restore()
    {
        if (_gsStack.Count == 0)
            throw new InvalidOperationException("Cannot restore without preceding save operation.");
        Restore(_gsStack.Current.State);
    }

    /// <summary>
    /// Saves a graphics container with the current state of this XGraphics and
    /// opens and uses a new graphics container.
    /// </summary>
    public XGraphicsContainer BeginContainer()
    {
        return BeginContainer(new XRect(0, 0, 1, 1), new XRect(0, 0, 1, 1), XGraphicsUnit.Point);
    }

    /// <summary>
    /// Saves a graphics container with the current state of this XGraphics and
    /// opens and uses a new graphics container.
    /// </summary>
    /// <param name="dstrect">The rectangle, in points, the source rectangle is mapped onto.</param>
    /// <param name="srcrect">The rectangle, measured in <paramref name="unit"/>, mapped onto the destination.</param>
    /// <param name="unit">The unit the source rectangle is measured in. What is drawn inside the container
    /// is still measured in points; the unit says only how big the source rectangle is.</param>
    public XGraphicsContainer BeginContainer(XRect dstrect, XRect srcrect, XGraphicsUnit unit)
    {
        if (!Enum.IsDefined(unit))
            throw new ArgumentException(@"The unit is not a member of XGraphicsUnit.", nameof(unit));

        if (unit != XGraphicsUnit.Point)
        {
            srcrect = new XRect(new XUnit(srcrect.X, unit).Point, new XUnit(srcrect.Y, unit).Point,
                new XUnit(srcrect.Width, unit).Point, new XUnit(srcrect.Height, unit).Point);
            unit = XGraphicsUnit.Point;
        }

        var xContainer = new XGraphicsContainer();

        var iState = new InternalGraphicsState(xContainer);
        iState.Transform = _transform;

        _gsStack.Push(iState);

        if (_renderer != null)
            _renderer.BeginContainer(xContainer, dstrect, srcrect, unit);

        var matrix = new XMatrix();
        var scaleX = dstrect.Width / srcrect.Width;
        var scaleY = dstrect.Height / srcrect.Height;
        // A point is moved so the source corner is the origin, scaled, then moved onto the
        // destination corner. Each prepend comes before the ones already there, so they are
        // written in the reverse of that order.
        matrix.TranslatePrepend(dstrect.X, dstrect.Y);
        matrix.ScalePrepend(scaleX, scaleY);
        matrix.TranslatePrepend(-srcrect.X, -srcrect.Y);
        AddTransform(matrix, XMatrixOrder.Prepend);

        return xContainer;
    }

    /// <summary>
    /// Closes the current graphics container and restores the state of this XGraphics
    /// to the state saved by a call to the BeginContainer method.
    /// </summary>
    public void EndContainer(XGraphicsContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        _gsStack.Restore(container.InternalState);
        _transform = container.InternalState.Transform;

        if (_renderer != null)
            _renderer.EndContainer(container);
    }

    /// <summary>
    /// Gets the current graphics state level. The default value is 0. Each call of Save or BeginContainer
    /// increased and each call of Restore or EndContainer decreased the value by 1.
    /// </summary>
    public int GraphicsStateLevel => _gsStack.Count;

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Properties

    /// <summary>
    /// Gets or sets the smoothing mode.
    /// </summary>
    /// <value>The smoothing mode.</value>
    public XSmoothingMode SmoothingMode
    {
        get => _smoothingMode;
        set => _smoothingMode = value;
    }

    private XSmoothingMode _smoothingMode;

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Transformation

    /// <summary>
    /// Applies the specified translation operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void TranslateTransform(double dx, double dy)
    {
        AddTransform(XMatrix.CreateTranslation(dx, dy), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified translation operation to the transformation matrix of this object
    /// in the specified order.
    /// </summary>
    public void TranslateTransform(double dx, double dy, XMatrixOrder order)
    {
        var matrix = new XMatrix();
        matrix.TranslatePrepend(dx, dy);
        AddTransform(matrix, order);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void ScaleTransform(double scaleX, double scaleY)
    {
        AddTransform(XMatrix.CreateScaling(scaleX, scaleY), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object
    /// in the specified order.
    /// </summary>
    public void ScaleTransform(double scaleX, double scaleY, XMatrixOrder order)
    {
        var matrix = new XMatrix();
        matrix.ScalePrepend(scaleX, scaleY);
        AddTransform(matrix, order);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public void ScaleTransform(double scaleXY)
    {
        ScaleTransform(scaleXY, scaleXY);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object
    /// in the specified order.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public void ScaleTransform(double scaleXY, XMatrixOrder order)
    {
        ScaleTransform(scaleXY, scaleXY, order);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void ScaleAtTransform(double scaleX, double scaleY, double centerX, double centerY)
    {
        AddTransform(XMatrix.CreateScaling(scaleX, scaleY, centerX, centerY), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified scaling operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void ScaleAtTransform(double scaleX, double scaleY, XPoint center)
    {
        AddTransform(XMatrix.CreateScaling(scaleX, scaleY, center.X, center.Y), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified rotation operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void RotateTransform(double angle)
    {
        AddTransform(XMatrix.CreateRotationRadians(angle * Calc.Deg2Rad), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified rotation operation to the transformation matrix of this object
    /// in the specified order. The angle unit of measure is degree.
    /// </summary>
    public void RotateTransform(double angle, XMatrixOrder order)
    {
        var matrix = new XMatrix();
        matrix.RotatePrepend(angle);
        AddTransform(matrix, order);
    }

    /// <summary>
    /// Applies the specified rotation operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void RotateAtTransform(double angle, XPoint point)
    {
        AddTransform(XMatrix.CreateRotationRadians(angle * Calc.Deg2Rad, point.X, point.Y), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified rotation operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// </summary>
    public void RotateAtTransform(double angle, XPoint point, XMatrixOrder order)
    {
        AddTransform(XMatrix.CreateRotationRadians(angle * Calc.Deg2Rad, point.X, point.Y), order);
    }

    /// <summary>
    /// Applies the specified shearing operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// ShearTransform is a synonym for SkewAtTransform.
    /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
    /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
    /// </summary>
    public void ShearTransform(double shearX, double shearY)
    {
        AddTransform(XMatrix.CreateSkewRadians(shearX * Calc.Deg2Rad, shearY * Calc.Deg2Rad), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified shearing operation to the transformation matrix of this object
    /// in the specified order.
    /// ShearTransform is a synonym for SkewAtTransform.
    /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
    /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
    /// </summary>
    public void ShearTransform(double shearX, double shearY, XMatrixOrder order)
    {
        AddTransform(XMatrix.CreateSkewRadians(shearX * Calc.Deg2Rad, shearY * Calc.Deg2Rad), order);
    }

    /// <summary>
    /// Applies the specified shearing operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// ShearTransform is a synonym for SkewAtTransform.
    /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
    /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
    /// </summary>
    public void SkewAtTransform(double shearX, double shearY, double centerX, double centerY)
    {
        AddTransform(XMatrix.CreateSkewRadians(shearX * Calc.Deg2Rad, shearY * Calc.Deg2Rad, centerX, centerY), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Applies the specified shearing operation to the transformation matrix of this object by
    /// prepending it to the object's transformation matrix.
    /// ShearTransform is a synonym for SkewAtTransform.
    /// Parameter shearX specifies the horizontal skew which is measured in degrees counterclockwise from the y-axis.
    /// Parameter shearY specifies the vertical skew which is measured in degrees counterclockwise from the x-axis.
    /// </summary>
    public void SkewAtTransform(double shearX, double shearY, XPoint center)
    {
        AddTransform(XMatrix.CreateSkewRadians(shearX * Calc.Deg2Rad, shearY * Calc.Deg2Rad, center.X, center.Y), XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Multiplies the transformation matrix of this object and specified matrix.
    /// </summary>
    public void MultiplyTransform(XMatrix matrix)
    {
        AddTransform(matrix, XMatrixOrder.Prepend);
    }

    /// <summary>
    /// Multiplies the transformation matrix of this object and specified matrix in the specified order.
    /// </summary>
    public void MultiplyTransform(XMatrix matrix, XMatrixOrder order)
    {
        AddTransform(matrix, order);
    }

    /// <summary>
    /// Gets the current transformation matrix.
    /// The transformation matrix canot be set. Insted use Save/Restore or BeginContainer/EndContainer to
    /// save the state before Transform is called and later restore to the previous transform.
    /// </summary>
    public XMatrix Transform => _transform;

    /// <summary>
    /// Applies a new transformation to the current transformation matrix.
    /// </summary>
    /// <remarks>
    /// The renderer is only ever handed a prepend, because that is all the PDF <c>cm</c> operator
    /// can say: it applies its matrix in the current user space, before everything already there.
    /// An appended transform is therefore passed on as the prepend with the same effect,
    /// <c>W · T · W⁻¹</c>, which is what makes <see cref="Transform"/> and the page agree. A
    /// prepend is passed on untouched, so no existing document changes by a byte.
    /// </remarks>
    private void AddTransform(XMatrix transform, XMatrixOrder order)
    {
        var prepended = transform;
        if (order == XMatrixOrder.Append)
        {
            if (!_transform.HasInverse)
                throw new InvalidOperationException(
                    "A transformation cannot be appended to a transformation matrix that has no inverse.");

            var inverse = _transform;
            inverse.Invert();
            prepended = _transform * transform * inverse;
        }

        var matrix = _transform;
        matrix.Multiply(transform, order);
        _transform = matrix;
        if (_renderer != null)
            _renderer.AddTransform(prepended, XMatrixOrder.Prepend);
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Clipping

    /// <summary>
    /// Updates the clip region of this XGraphics to the intersection of the
    /// current clip region and the specified rectangle.
    /// </summary>
    public void IntersectClip(XRect rect)
    {
        var path = new XGraphicsPath();
        path.AddRectangle(rect);
        IntersectClip(path);
    }

    /// <summary>
    /// Updates the clip region of this XGraphics to the intersection of the
    /// current clip region and the specified graphical path.
    /// </summary>
    public void IntersectClip(XGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (_renderer != null)
            _renderer.SetClip(path, XCombineMode.Intersect);
    }

    #endregion

    // --------------------------------------------------------------------------------------------

    #region Miscellaneous

    /// <summary>
    /// Writes a comment to the output stream. Comments have no effect on the rendering of the output.
    /// They may be useful to mark a position in a content stream of a PDF document.
    /// </summary>
    public void WriteComment(string comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        if (_renderer != null)
            _renderer.WriteComment(comment);
    }

    /// <summary>
    /// (Under construction. May change in future versions.)
    /// </summary>
    public SpaceTransformer Transformer => _transformer ??= new SpaceTransformer(this);

    private SpaceTransformer _transformer;

    #endregion

    // --------------------------------------------------------------------------------------------

    internal void DisassociateImage()
    {
        if (_associatedImage == null)
            throw new InvalidOperationException("No image associated.");

        Dispose();
    }

    internal InternalGraphicsMode InternalGraphicsMode
    {
        get => _internalGraphicsMode;
        set => _internalGraphicsMode = value;
    }

    private InternalGraphicsMode _internalGraphicsMode;

    internal XImage AssociatedImage
    {
        get => _associatedImage;
        set => _associatedImage = value;
    }

    private XImage _associatedImage;

    /// <summary>
    /// The transformation matrix from the XGraphics page space to the Graphics world space.
    /// (The name 'default view matrix' comes from Microsoft OS/2 Presentation Manager. I choose
    /// this name because I have no better one.)
    /// </summary>
    internal XMatrix DefaultViewMatrix;

    /// <summary>
    /// Indicates whether to send drawing operations to _gfx or _dc.
    /// </summary>
    private bool _drawGraphics;

    private readonly XForm _form;

    /// <summary>
    /// Interface to an (optional) renderer. Currently it is the XGraphicsPdfRenderer, if defined.
    /// </summary>
    private IXGraphicsRenderer _renderer;

    /// <summary>
    /// The transformation matrix from XGraphics world space to page unit space.
    /// </summary>
    private XMatrix _transform;

    /// <summary>
    /// The graphics state stack.
    /// </summary>
    private readonly GraphicsStateStack _gsStack;

    /// <summary>
    /// Gets the PDF page that serves as drawing surface if PDF is rendered,
    /// or null, if no such object exists.
    /// </summary>
    public PdfPage PdfPage
    {
        get
        {
            var renderer = _renderer as XGraphicsPdfRenderer;
            return renderer != null ? renderer.Page : null;
        }
    }

    // ----- linking what has been drawn -----------------------------------------------------------
    //
    // An annotation is placed in default page space, measured from the bottom left of the page,
    // and everything drawn here is placed in world space, which is usually measured from the top
    // left and may have been turned or scaled since. The methods below are the conversion, which
    // is the whole of what stood between drawing a piece of text and linking it.

    /// <summary>
    /// Links a rectangle of what has been drawn to a web address.
    /// </summary>
    /// <param name="worldRect">The area to link, in the coordinates the drawing methods take.</param>
    /// <param name="url">The address to link to.</param>
    public PdfLinkAnnotation AddWebLink(XRect worldRect, string url)
    {
        return PageForAnnotation().AddWebLink(PageRectangleOf(worldRect), url);
    }

    /// <summary>
    /// Links a rectangle of what has been drawn to a page of the same document.
    /// </summary>
    /// <param name="worldRect">The area to link, in the coordinates the drawing methods take.</param>
    /// <param name="destinationPage">The one-based destination page number.</param>
    public PdfLinkAnnotation AddDocumentLink(XRect worldRect, int destinationPage)
    {
        return PageForAnnotation().AddDocumentLink(PageRectangleOf(worldRect), destinationPage);
    }

    /// <summary>
    /// Links a rectangle of what has been drawn to a named destination of the same document.
    /// </summary>
    /// <param name="worldRect">The area to link, in the coordinates the drawing methods take.</param>
    /// <param name="destinationName">The name, as given to <see cref="PdfDocument.NamedDestinations"/>.</param>
    public PdfLinkAnnotation AddNamedLink(XRect worldRect, string destinationName)
    {
        return PageForAnnotation().AddNamedLink(PageRectangleOf(worldRect), destinationName);
    }

    /// <summary>
    /// Names the place on this page that a point of what has been drawn sits at, so that it can be
    /// linked to by that name.
    /// </summary>
    /// <param name="name">The name to give it.</param>
    /// <param name="worldPoint">The place, in the coordinates the drawing methods take.</param>
    public void AddNamedDestination(string name, XPoint worldPoint)
    {
        var page = PageForAnnotation();
        var onPage = Transformer.WorldToDefaultPage(new XRect(worldPoint.X, worldPoint.Y, 0, 0));

        // The top of the window, which PDF measures up the page from the bottom.
        page.Owner.NamedDestinations.Add(name, page, onPage.Y);
    }

    private PdfPage PageForAnnotation()
    {
        return PdfPage ?? throw new InvalidOperationException("Annotations can only be added to an XGraphics that draws onto a PDF page.");
    }

    private PdfRectangle PageRectangleOf(XRect worldRect)
    {
        return new PdfRectangle(Transformer.WorldToDefaultPage(worldRect));
    }

    /// <summary>
    /// (This class is under construction.)
    /// Currently used in PinataLayout
    /// </summary>
    public class SpaceTransformer
    {
        internal SpaceTransformer(XGraphics gfx)
        {
            _gfx = gfx;
        }

        private readonly XGraphics _gfx;

        /// <summary>
        /// Gets the point in default page space units that the specified point in world space
        /// units falls on.
        /// </summary>
        /// <remarks>
        /// The rectangle overload cannot answer this: a rectangle is enclosed rather than mapped,
        /// so which of its corners a given point became is lost. Annotations placed by a point
        /// rather than by a box - a <see cref="PdfPinata.Pdf.Annotations.PdfLineAnnotation"/>'s
        /// two ends, above all - need the point itself.
        /// </remarks>
        public XPoint WorldToDefaultPage(XPoint point)
        {
            XPoint[] points = [point];

            var matrix = _gfx.Transform;
            matrix.TransformPoints(points);

            return new XPoint(points[0].X, _gfx.PageSize.Height - points[0].Y);
        }

        /// <summary>
        /// Gets the smallest rectangle in default page space units that completely encloses the specified rect
        /// in world space units.
        /// </summary>
        public XRect WorldToDefaultPage(XRect rect)
        {
            var points = new XPoint[4];
            points[0] = new XPoint(rect.X, rect.Y);
            points[1] = new XPoint(rect.X + rect.Width, rect.Y);
            points[2] = new XPoint(rect.X, rect.Y + rect.Height);
            points[3] = new XPoint(rect.X + rect.Width, rect.Y + rect.Height);

            var matrix = _gfx.Transform;
            matrix.TransformPoints(points);

            var height = _gfx.PageSize.Height;
            points[0].Y = height - points[0].Y;
            points[1].Y = height - points[1].Y;
            points[2].Y = height - points[2].Y;
            points[3].Y = height - points[3].Y;

            var xmin = Math.Min(Math.Min(points[0].X, points[1].X), Math.Min(points[2].X, points[3].X));
            var xmax = Math.Max(Math.Max(points[0].X, points[1].X), Math.Max(points[2].X, points[3].X));
            var ymin = Math.Min(Math.Min(points[0].Y, points[1].Y), Math.Min(points[2].Y, points[3].Y));
            var ymax = Math.Max(Math.Max(points[0].Y, points[1].Y), Math.Max(points[2].Y, points[3].Y));

            return new XRect(xmin, ymin, xmax - xmin, ymax - ymin);
        }
    }
}
