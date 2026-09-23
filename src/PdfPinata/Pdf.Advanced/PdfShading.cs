#region Copyright
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

    /// <summary>
    /// Sets the shading up from a gradient brush.
    /// </summary>
    /// <returns>
    /// The matrix from the space the shading's coordinates are written in to the space that
    /// <see cref="XGraphicsPdfRenderer.WorldToView"/> answers in, for the caller to prepend to
    /// the pattern matrix. It is the identity wherever the coordinates can be written in that
    /// space directly, which is every gradient with no transform of its own and every radial
    /// gradient whose transform does not stretch its circles into ellipses.
    /// </returns>
    internal XMatrix SetupFromBrush(XBaseGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        if (brush is XRadialGradientBrush radialBrush)
            return SetupFromBrush(radialBrush, renderer, channel);
        if (brush is XLinearGradientBrush linearBrush)
            return SetupFromBrush(linearBrush, renderer, channel);
        throw new ArgumentException("Unsupported gradient brush: " + brush, nameof(brush));
    }

    /// <summary>
    /// Sets the shading up from a radial gradient brush, as a type 3 shading.
    /// </summary>
    /// <remarks>
    /// A type 3 shading is two circles, and a circle stays a circle only under a transform that
    /// scales every direction alike. The transform of the graphics is already in the pattern
    /// matrix; what lies between the brush and the pattern's space is the brush's own transform
    /// and the flip from the top-left space <see cref="XGraphics"/> draws in to the bottom-left
    /// space of the page. Where that mapping scales every direction alike, the circles are
    /// mapped into the pattern's space and written there, as they always were. Where it does
    /// not - a brush stretched wider than it is tall - each circle has become an ellipse that no
    /// type 3 shading can describe in that space. So the circles are written as the brush states
    /// them, and the mapping goes into the pattern matrix, which turns them into the ellipses the
    /// drawing asks for.
    /// </remarks>
    internal XMatrix SetupFromBrush(XRadialGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var colorMode = _document.Options.ColorMode;
        var color1 = ColorSpaceHelper.EnsureColorMode(colorMode, brush.Color1);
        var color2 = ColorSpaceHelper.EnsureColorMode(colorMode, brush.Color2);

        Elements[Keys.ShadingType] = new PdfInteger(3);
        Elements[Keys.ColorSpace] = new PdfName(ColorSpaceOf(colorMode, channel));

        var brushToView = BrushToView(brush, renderer);

        XPoint p1, p2;
        double r1, r2;
        XMatrix shadingToView;
        if (ScalesAlike(brushToView, out var scale))
        {
            p1 = brushToView.Transform(brush.Center1);
            p2 = brushToView.Transform(brush.Center2);
            r1 = brush.R1 * scale;
            r2 = brush.R2 * scale;
            shadingToView = XMatrix.Identity;
        }
        else
        {
            p1 = brush.Center1;
            p2 = brush.Center2;
            r1 = brush.R1;
            r2 = brush.R2;
            shadingToView = brushToView;
        }

        const string format = Config.SignificantFigures3;
        Elements[Keys.Coords] = new PdfLiteral("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "}]", p1.X, p1.Y, r1, p2.X, p2.Y, r2);
        Elements[Keys.Function] = RampFunction(color1, color2, colorMode, channel);
        SetExtend(brush);

        return shadingToView;
    }

    /// <summary>
    /// Sets the shading up from a linear gradient brush, as a type 2 shading.
    /// </summary>
    /// <remarks>
    /// With no transform of the brush's own, the axis is mapped into the pattern's space and
    /// written there, as it always was. With one, the axis is written as the brush states it, and
    /// the brush's transform goes into the pattern matrix together with the graphics' transform.
    /// Mapping the two end points alone would keep the axis but not the angle the bands make
    /// with it, and a transform that scales one direction more than another changes that angle.
    /// </remarks>
    internal XMatrix SetupFromBrush(XLinearGradientBrush brush, XGraphicsPdfRenderer renderer,
        PdfShadingChannel channel = PdfShadingChannel.Color)
    {
        ArgumentNullException.ThrowIfNull(brush);

        var colorMode = _document.Options.ColorMode;
        var color1 = ColorSpaceHelper.EnsureColorMode(colorMode, brush.Color1);
        var color2 = ColorSpaceHelper.EnsureColorMode(colorMode, brush.Color2);

        Elements[Keys.ShadingType] = new PdfInteger(2);
        Elements[Keys.ColorSpace] = new PdfName(ColorSpaceOf(colorMode, channel));

        Func<XPoint, XPoint> map;
        XMatrix shadingToView;
        if (brush.Matrix.IsIdentity)
        {
            map = renderer.WorldToView;
            shadingToView = XMatrix.Identity;
        }
        else
        {
            map = point => point;
            shadingToView = BrushToView(brush, renderer);
        }

        double x1 = 0, y1 = 0, x2 = 0, y2 = 0;
        if (brush.UseRect)
        {
            var pt1 = map(brush.Rect.TopLeft);
            var pt2 = map(brush.Rect.BottomRight);

            switch (brush.LinearGradientMode)
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
            var pt1 = map(brush.Point1);
            var pt2 = map(brush.Point2);

            x1 = pt1.X;
            y1 = pt1.Y;
            x2 = pt2.X;
            y2 = pt2.Y;
        }

        const string format = Config.SignificantFigures3;
        Elements[Keys.Coords] = new PdfLiteral("[{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}]", x1, y1, x2, y2);
        Elements[Keys.Function] = RampFunction(color1, color2, colorMode, channel);
        SetExtend(brush);

        return shadingToView;
    }

    /// <summary>
    /// Writes <c>/Extend</c> when the brush asks for either end to be extended. When it asks for
    /// neither, which is the default, nothing is written, so a document that never sets either
    /// property is written exactly as it was before they had any effect.
    /// </summary>
    private void SetExtend(XBaseGradientBrush brush)
    {
        if (brush.ExtendLeft || brush.ExtendRight)
        {
            Elements[Keys.Extend] = new PdfLiteral("[{0} {1}]",
                brush.ExtendLeft ? "true" : "false", brush.ExtendRight ? "true" : "false");
        }
    }

    /// <summary>
    /// The mapping from the space a brush's points are given in to the renderer's view space:
    /// the brush's own transform first, then the graphics' transform and the flip to the page.
    /// </summary>
    /// <remarks>
    /// <see cref="XGraphicsPdfRenderer.WorldToView"/> is affine, so where it takes three points
    /// is the whole of it.
    /// </remarks>
    private static XMatrix BrushToView(XBaseGradientBrush brush, XGraphicsPdfRenderer renderer)
    {
        var brushMatrix = brush.Matrix;
        var origin = renderer.WorldToView(brushMatrix.Transform(new XPoint(0, 0)));
        var unitX = renderer.WorldToView(brushMatrix.Transform(new XPoint(1, 0)));
        var unitY = renderer.WorldToView(brushMatrix.Transform(new XPoint(0, 1)));
        return new XMatrix(unitX.X - origin.X, unitX.Y - origin.Y,
            unitY.X - origin.X, unitY.Y - origin.Y, origin.X, origin.Y);
    }

    /// <summary>
    /// Whether a matrix scales every direction by the same factor. It may turn or mirror, but it
    /// never squashes, so a circle is still a circle after it.
    /// </summary>
    private static bool ScalesAlike(XMatrix matrix, out double scale)
    {
        double a = matrix.M11, b = matrix.M12, c = matrix.M21, d = matrix.M22;
        scale = Math.Sqrt(a * a + b * b);

        var slack = 1e-9 * Math.Max(1, scale);
        var turns = Math.Abs(a - d) <= slack && Math.Abs(b + c) <= slack;
        var mirrors = Math.Abs(a + d) <= slack && Math.Abs(b - c) <= slack;
        return scale > 0 && (turns || mirrors);
    }

    /// <summary>
    /// The colour space a shading carrying the given channel is expressed in.
    /// </summary>
    private static string ColorSpaceOf(PdfColorMode colorMode, PdfShadingChannel channel)
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
    private static PdfDictionary RampFunction(XColor color1, XColor color2, PdfColorMode colorMode,
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

        var function = new PdfDictionary();
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

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
