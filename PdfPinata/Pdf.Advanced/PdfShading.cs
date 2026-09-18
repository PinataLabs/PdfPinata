#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using PdfPinata.Drawing;
using PdfPinata.Drawing.Pdf;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Which of a gradient's two ramps a shading carries.
/// </summary>
/// <remarks>
/// A gradient between translucent colours is drawn twice: once in colour, and once in grey as
/// the group of a luminosity soft mask, where how light the shading is at a point is how much of
/// the colour shows through there. Both are built by the same code from the same brush, so the
/// mask cannot follow a different axis, a different extent or a different interpolation from the
/// colour it masks.
/// </remarks>
internal enum PdfShadingChannel
{
    /// <summary>The colours of the gradient, in the document's colour mode.</summary>
    Color,

    /// <summary>The alpha of the gradient's colours, as grey levels.</summary>
    Alpha
}

/// <summary>
/// Represents a shading dictionary.
/// </summary>
public sealed class PdfShading : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShading"/> class.
    /// </summary>
    public PdfShading(PdfDocument document)
        : base(document)
    { }

    internal void SetupFromBrush(XBaseGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        if (brush is XRadialGradientBrush radialBrush)
            SetupFromBrush(radialBrush, renderer, channel);
        else if (brush is XLinearGradientBrush linearBrush)
            SetupFromBrush(linearBrush, renderer, channel);
        else
            throw new ArgumentException("Unsupoorted XGradientBrush: " + brush);
    }

    internal void SetupFromBrush(XRadialGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        ArgumentNullException.ThrowIfNull(brush);

        PdfColorMode colorMode = _document.Options.ColorMode;
        XColor color1 = ColorSpaceHelper.EnsureColorMode(colorMode, brush._color1);
        XColor color2 = ColorSpaceHelper.EnsureColorMode(colorMode, brush._color2);

        Elements[Keys.ShadingType] = new PdfInteger(3);
        Elements[Keys.ColorSpace] = new PdfName(ColorSpaceOf(colorMode, channel));

        XPoint p1 = renderer.WorldToView(brush._center1);
        XPoint p2 = renderer.WorldToView(brush._center2);

        var rv1 = renderer.WorldToView(new XPoint(brush._r1 + brush._center1.X, brush._center1.Y));
        var rv2 = renderer.WorldToView(new XPoint(brush._r2 + brush._center2.X, brush._center2.Y));

        var dx1 = rv1.X - p1.X;
        var dy1 = rv1.Y - p1.Y;
        var dx2 = rv2.X - p2.X;
        var dy2 = rv2.Y - p2.Y;

        var r1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
        var r2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);

        const string format = Config.SignificantFigures3;
        Elements[Keys.Coords] = new PdfLiteral("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "}]", p1.X, p1.Y, r1, p2.X, p2.Y, r2);

        //Elements[Keys.Background] = new PdfRawItem("[0 1 1]");
        //Elements[Keys.Domain] =
        Elements[Keys.Function] = RampFunction(color1, color2, colorMode, channel);
        //Elements[Keys.Extend] = new PdfRawItem("[true true]");
    }

    /// <summary>
    /// Setups the shading from the specified brush.
    /// </summary>
    internal void SetupFromBrush(XLinearGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        ArgumentNullException.ThrowIfNull(brush);

        PdfColorMode colorMode = _document.Options.ColorMode;
        XColor color1 = ColorSpaceHelper.EnsureColorMode(colorMode, brush._color1);
        XColor color2 = ColorSpaceHelper.EnsureColorMode(colorMode, brush._color2);

        Elements[Keys.ShadingType] = new PdfInteger(2);
        Elements[Keys.ColorSpace] = new PdfName(ColorSpaceOf(colorMode, channel));

        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
        if (brush._useRect)
        {
            XPoint pt1 = renderer.WorldToView(brush._rect.TopLeft);
            XPoint pt2 = renderer.WorldToView(brush._rect.BottomRight);

            switch (brush._linearGradientMode)
            {
                case XLinearGradientMode.Horizontal:
                    x1 = pt1.X;
                    y1 = pt1.Y;
                    x2 = pt2.X;
                    y2 = pt1.Y;
                    break;

                case XLinearGradientMode.Vertical:
                    x1 = pt1.X;
                    y1 = pt1.Y;
                    x2 = pt1.X;
                    y2 = pt2.Y;
                    break;

                case XLinearGradientMode.ForwardDiagonal:
                    x1 = pt1.X;
                    y1 = pt1.Y;
                    x2 = pt2.X;
                    y2 = pt2.Y;
                    break;

                case XLinearGradientMode.BackwardDiagonal:
                    x1 = pt2.X;
                    y1 = pt1.Y;
                    x2 = pt1.X;
                    y2 = pt2.Y;
                    break;
            }
        }
        else
        {
            XPoint pt1 = renderer.WorldToView(brush._point1);
            XPoint pt2 = renderer.WorldToView(brush._point2);

            x1 = pt1.X;
            y1 = pt1.Y;
            x2 = pt2.X;
            y2 = pt2.Y;
        }

        const string format = Config.SignificantFigures3;
        Elements[Keys.Coords] = new PdfLiteral("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}]", x1, y1, x2, y2);

        //Elements[Keys.Background] = new PdfRawItem("[0 1 1]");
        //Elements[Keys.Domain] =
        Elements[Keys.Function] = RampFunction(color1, color2, colorMode, channel);
        //Elements[Keys.Extend] = new PdfRawItem("[true true]");
    }

    /// <summary>
    /// The colour space a shading carrying the given channel is expressed in.
    /// </summary>
    static string ColorSpaceOf(PdfColorMode colorMode, PdfShadingChannel channel)
    {
        // A luminosity mask is read as a single grey level, which is why the group it belongs to
        // is in DeviceGray as well.
        if (channel == PdfShadingChannel.Alpha)
            return "/DeviceGray";

        return colorMode != PdfColorMode.Cmyk ? "/DeviceRGB" : "/DeviceCMYK";
    }

    /// <summary>
    /// The exponential interpolation function that carries one of the gradient's two ramps
    /// between its two stops.
    /// </summary>
    /// <remarks>
    /// The geometry - the shading type and the coordinates - is written by the caller and is the
    /// same whichever channel this is, which is the point of building both through one method: an
    /// alpha ramp that followed a different axis from the colour it masks would fade the gradient
    /// out in the wrong direction.
    /// </remarks>
    static PdfDictionary RampFunction(XColor color1, XColor color2, PdfColorMode colorMode,
        PdfShadingChannel channel)
    {
        const string format = Config.SignificantFigures3;

        PdfItem c0, c1;
        if (channel == PdfShadingChannel.Alpha)
        {
            // One grey component: fully transparent is black, fully opaque is white.
            c0 = new PdfLiteral("[{0:" + format + "}]", color1.A);
            c1 = new PdfLiteral("[{0:" + format + "}]", color2.A);
        }
        else
        {
            // One value per component of the colour space and no more. An RGB ramp used to carry
            // the alpha as a fourth value, which is not a colour component and makes the function
            // wider than the space it feeds: a conformant reader rejects the shading and paints
            // nothing at all, which is why no gradient this library wrote ever appeared in
            // Ghostscript. The alpha now goes where alpha belongs, into the soft mask above.
            c0 = new PdfLiteral("[" + PdfEncoders.ToString(color1, colorMode) + "]");
            c1 = new PdfLiteral("[" + PdfEncoders.ToString(color2, colorMode) + "]");
        }

        PdfDictionary function = new PdfDictionary();
        function.Elements["/FunctionType"] = new PdfInteger(2);
        function.Elements["/C0"] = c0;
        function.Elements["/C1"] = c1;
        function.Elements["/Domain"] = new PdfLiteral("[0 1]");
        function.Elements["/N"] = new PdfInteger(1);
        return function;
    }

    /// <summary>
    /// Common keys for all streams.
    /// </summary>
    internal sealed class Keys : KeysBase
    {
        /// <summary>
        /// (Required) The shading type:
        /// 1 Function-based shading
        /// 2 Axial shading
        /// 3 Radial shading
        /// 4 Free-form Gouraud-shaded triangle mesh
        /// 5 Lattice-form Gouraud-shaded triangle mesh
        /// 6 Coons patch mesh
        /// 7 Tensor-product patch mesh
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string ShadingType = "/ShadingType";

        /// <summary>
        /// (Required) The color space in which color values are expressed. This may be any device, 
        /// CIE-based, or special color space except a Pattern space.
        /// </summary>
        [KeyInfo(KeyType.NameOrArray | KeyType.Required)]
        public const string ColorSpace = "/ColorSpace";

        /// <summary>
        /// (Optional) An array of color components appropriate to the color space, specifying
        /// a single background color value. If present, this color is used, before any painting 
        /// operation involving the shading, to fill those portions of the area to be painted 
        /// that lie outside the bounds of the shading object. In the opaque imaging model, 
        /// the effect is as if the painting operation were performed twice: first with the 
        /// background color and then with the shading.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Background = "/Background";

        /// <summary>
        /// (Optional) An array of four numbers giving the left, bottom, right, and top coordinates, 
        /// respectively, of the shading's bounding box. The coordinates are interpreted in the 
        /// shading's target coordinate space. If present, this bounding box is applied as a temporary 
        /// clipping boundary when the shading is painted, in addition to the current clipping path
        /// and any other clipping boundaries in effect at that time.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Optional)]
        public const string BBox = "/BBox";

        /// <summary>
        /// (Optional) A flag indicating whether to filter the shading function to prevent aliasing 
        /// artifacts. The shading operators sample shading functions at a rate determined by the 
        /// resolution of the output device. Aliasing can occur if the function is not smooth - that
        /// is, if it has a high spatial frequency relative to the sampling rate. Anti-aliasing can
        /// be computationally expensive and is usually unnecessary, since most shading functions
        /// are smooth enough or are sampled at a high enough frequency to avoid aliasing effects.
        /// Anti-aliasing may not be implemented on some output devices, in which case this flag
        /// is ignored.
        /// Default value: false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string AntiAlias = "/AntiAlias";

        // ---- Type 2 ----------------------------------------------------------

        /// <summary>
        /// (Required) An array of four numbers [x0 y0 x1 y1] specifying the starting and
        /// ending coordinates of the axis, expressed in the shading's target coordinate space.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string Coords = "/Coords";

        /// <summary>
        /// (Optional) An array of two numbers [t0 t1] specifying the limiting values of a
        /// parametric variable t. The variable is considered to vary linearly between these
        /// two values as the color gradient varies between the starting and ending points of
        /// the axis. The variable t becomes the input argument to the color function(s).
        /// Default value: [0.0 1.0].
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Domain = "/Domain";

        /// <summary>
        /// (Required) A 1-in, n-out function or an array of n 1-in, 1-out functions (where n
        /// is the number of color components in the shading dictionary's color space). The
        /// function(s) are called with values of the parametric variable t in the domain defined
        /// by the Domain entry. Each function's domain must be a superset of that of the shading
        /// dictionary. If the value returned by the function for a given color component is out
        /// of range, it is adjusted to the nearest valid value.
        /// </summary>
        [KeyInfo(KeyType.Function | KeyType.Required)]
        public const string Function = "/Function";

        /// <summary>
        /// (Optional) An array of two boolean values specifying whether to extend the shading
        /// beyond the starting and ending points of the axis, respectively.
        /// Default value: [false false].
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Extend = "/Extend";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
