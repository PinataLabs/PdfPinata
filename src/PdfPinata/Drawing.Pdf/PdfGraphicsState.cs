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
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Internal;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace PdfPinata.Drawing.Pdf;

/// <summary>
/// Represents the current PDF graphics state.
/// </summary>
/// <remarks>
/// Completely revised for PDFsharp 1.4.
/// </remarks>
internal sealed class PdfGraphicsState : ICloneable
{
    public PdfGraphicsState(XGraphicsPdfRenderer renderer)
    {
        _renderer = renderer;
    }
    readonly XGraphicsPdfRenderer _renderer;

    public PdfGraphicsState Clone()
    {
        var state = (PdfGraphicsState)MemberwiseClone();
        return state;
    }

    object ICloneable.Clone()
    {
        return Clone();
    }

    internal int Level;

    /// <summary>
    /// The state on <see cref="XGraphics"/>'s own stack this state was pushed for.
    /// </summary>
    /// <remarks>
    /// Written once per save, by <see cref="XGraphicsPdfRenderer.Save"/> and
    /// <see cref="XGraphicsPdfRenderer.BeginContainer"/>, and read by reference identity when the
    /// matching restore looks for the frame to pop back to. It is the one link between the two
    /// stacks; nothing else pairs them.
    /// </remarks>
    internal InternalGraphicsState InternalState;

    #region Stroke

    double _realizedLineWith = -1;
    int _realizedLineCap = -1;
    int _realizedLineJoin = -1;
    double _realizedMiterLimit = -1;
    XDashStyle _realizedDashStyle = (XDashStyle)(-1);
    /// <summary>
    /// The stroke colour last written, or null when none has been written at this level yet.
    /// </summary>
    /// <remarks>
    /// Null rather than <see cref="XColor.Empty"/>, whose alpha is 0: a fully transparent first
    /// pen matched that, no ExtGState was written for it, and the line was stroked at the default
    /// alpha of 1. Null always writes the alpha. For the colour itself it still stands for black,
    /// the default stroke colour, so a first black stroke writes no "RG" - see
    /// <see cref="_realizedStrokePattern"/>.
    /// </remarks>
    XColor? _realizedStrokeColor;
    bool _realizedStrokeOverPrint;

    public void RealizePen(XPen pen, PdfColorMode colorMode)
    {
        const string frmt2 = Config.SignificantFigures2;
        const string format = Config.SignificantFigures3;
        var color = pen.Color;
        var overPrint = pen.Overprint;
        var penBrush = pen.Brush;

        // A pen built from a brush has no Color of its own - the constructor never sets one - so
        // pen.Color is XColor.Empty, whose alpha is zero. Left as it stands that reaches the stroke
        // alpha at the foot of this method and paints the stroke completely transparent, which is
        // why a pen made from a brush drew nothing at all.
        //
        // A solid brush is simply a colour, and is treated as one from here on: it wants "RG" like
        // any other pen, where handing it to RealizeBrush below would set the *fill* colour instead.
        // A gradient really does need the pattern, and carries its own transparency through the soft
        // mask RealizeBrush installs, so the stroke stays opaque rather than taking an alpha from a
        // colour the pen never had.
        if (penBrush is XSolidBrush solidPenBrush)
        {
            color = solidPenBrush.Color;
            overPrint = overPrint || solidPenBrush.Overprint;
            penBrush = null;
        }
        else if (penBrush != null)
        {
            color = XColors.Black;
        }

        // A spot colour keeps its own alternate whatever the document's mode: see RealizeFillColor.
        if (color.Spot == null)
            color = ColorSpaceHelper.EnsureColorMode(colorMode, color);

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_realizedLineWith != pen._width)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatArgs("{0:" + format + "} w\n", pen._width);
            _realizedLineWith = pen._width;
        }

        if (_realizedLineCap != (int)pen._lineCap)
        {
            _renderer.AppendFormatArgs("{0} J\n", (int)pen._lineCap);
            _realizedLineCap = (int)pen._lineCap;
        }

        if (_realizedLineJoin != (int)pen._lineJoin)
        {
            _renderer.AppendFormatArgs("{0} j\n", (int)pen._lineJoin);
            _realizedLineJoin = (int)pen._lineJoin;
        }

        // The join, not the cap. This tested _realizedLineCap against a value of XLineJoin, which
        // agreed with itself only because XLineCap.Flat and XLineJoin.Miter are both zero: a pen
        // that mitred its joins and rounded its ends never wrote its miter limit at all, and one
        // with flat ends wrote it whatever its join was.
        if (_realizedLineJoin == (int)XLineJoin.Miter)
        {
            // Written as a real rather than truncated to an integer. A limit is a ratio of the
            // mitre's length to the pen's width, 1.5 is a perfectly ordinary value for it, and
            // rounding that to 1 asks for a bevel on every join that is not perfectly straight.
            #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
            if (_realizedMiterLimit != pen._miterLimit && pen._miterLimit > 0)
            #pragma warning restore S1244
            {
                _renderer.AppendFormatArgs("{0:" + format + "} M\n", pen._miterLimit);
                _realizedMiterLimit = pen._miterLimit;
            }
        }

        if (_realizedDashStyle != pen._dashStyle || pen._dashStyle == XDashStyle.Custom)
        {
            var dot = pen.Width;
            var dash = 3 * dot;

            // Line width 0 is not recommended but valid.
            var dashStyle = pen.DashStyle;
            if (dot == 0)
                dashStyle = XDashStyle.Solid;

            switch (dashStyle)
            {
                case XDashStyle.Solid:
                    _renderer.Append("[]0 d\n");
                    break;

                case XDashStyle.Dash:
                    _renderer.AppendFormatArgs("[{0:" + frmt2 + "} {1:" + frmt2 + "}]0 d\n", dash, dot);
                    break;

                case XDashStyle.Dot:
                    _renderer.AppendFormatArgs("[{0:" + frmt2 + "}]0 d\n", dot);
                    break;

                case XDashStyle.DashDot:
                    _renderer.AppendFormatArgs("[{0:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "}]0 d\n", dash, dot);
                    break;

                case XDashStyle.DashDotDot:
                    _renderer.AppendFormatArgs("[{0:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "} {1:" + frmt2 + "}]0 d\n", dash, dot);
                    break;

                case XDashStyle.Custom:
                {
                    // This branch is the one place a number reaches the content stream without
                    // going through an Append method, because the array is assembled as text a
                    // piece at a time. The check the Append methods make therefore has to be made
                    // here as well, or a dash length that is not a number would be written out.
                    var pdf = new StringBuilder("[", 256);
                    var len = pen._dashPattern == null ? 0 : pen._dashPattern.Length;
                    for (var idx = 0; idx < len; idx++)
                    {
                        if (idx > 0)
                            pdf.Append(' ');
                        // ReSharper disable PossibleNullReferenceException
                        XGraphicsPdfRenderer.EnsureWritable(
                            pen._dashPattern[idx] * pen._width, "a dash pattern");
                        // ReSharper restore PossibleNullReferenceException
                        pdf.Append(PdfEncoders.ToString(pen._dashPattern[idx] * pen._width));
                    }
                    // Make an even number of values look like in GDI+
                    if (len > 0 && len % 2 == 1)
                    {
                        pdf.Append(' ');
                        XGraphicsPdfRenderer.EnsureWritable(0.2 * pen._width, "a dash pattern");
                        pdf.Append(PdfEncoders.ToString(0.2 * pen._width));
                    }
                    XGraphicsPdfRenderer.EnsureWritable(
                        pen._dashOffset * pen._width, "a dash pattern");
                    pdf.AppendFormat(CultureInfo.InvariantCulture, "]{0:" + format + "} d\n", pen._dashOffset * pen._width);
                    var pattern = pdf.ToString();

                    // BUG: drice2@ageone.de reported a realizing problem
                    // HACK: I remove the if clause
                    //if (_realizedDashPattern != pattern)
                    {
                        _renderer.Append(pattern);
                    }
                }
                    break;
            }
            _realizedDashStyle = dashStyle;
        }

        // penBrush rather than pen.Brush: a solid one was turned into a colour above and takes the
        // ordinary stroke-colour path below.
        if (penBrush != null)
        {
            RealizeBrush(penBrush, colorMode, 0, 0, true);
        }
        else if (color.Spot != null)
        {
            RealizeSpotColor(color, _realizedStrokePattern ? null : _realizedStrokeColor, true);
        }
        else if (colorMode != PdfColorMode.Cmyk)
        {
            // Null still stands for the default black here, so only a spot colour realized last
            // forces "RG" - it left the Separation space selected, whatever its components say.
            if (_realizedStrokePattern || _realizedStrokeColor?.Spot != null || (_realizedStrokeColor ?? XColor.Empty).Rgb != color.Rgb)
            {
                _renderer.Append(PdfEncoders.ToString(color, PdfColorMode.Rgb));
                _renderer.Append(" RG\n");
            }
        }
        else
        {
            if (_realizedStrokePattern || _realizedStrokeColor is not { } realizedCmyk || realizedCmyk.Spot != null || !ColorSpaceHelper.IsEqualCmyk(realizedCmyk, color))
            {
                _renderer.Append(PdfEncoders.ToString(color, PdfColorMode.Cmyk));
                _renderer.Append(" K\n");
            }
        }

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_renderer.Owner.Version >= 14 && (_realizedStrokeColor is not { } realized || realized.A != color.A || _realizedStrokeOverPrint != overPrint))
        #pragma warning restore S1244
        {
            var extGState = _renderer.Owner.ExtGStateTable.GetExtGStateStroke(color.A, overPrint);
            var gs = _renderer.Resources.AddExtGState(extGState);
            _renderer.AppendFormatString("{0} gs\n", gs);

            // Must create transparency group.
            if (_renderer.Page != null && color.A < 1)
                _renderer.Page.TransparencyUsed = true;
        }
        _realizedStrokeColor = color;
        _realizedStrokeOverPrint = overPrint;
        _realizedStrokePattern = penBrush != null;
    }

    /// <summary>
    /// True when the last pen realized here strokes with a pattern rather than with a colour.
    /// </summary>
    /// <remarks>
    /// A flag rather than an invalidated <see cref="_realizedStrokeColor"/>, for two reasons. The
    /// colour a gradient pen leaves behind is the black stood in for it, and "no colour realized"
    /// is null, which the colour comparison reads as black - so neither value distinguishes a
    /// pattern from a black stroke. And treating null as "re-emit" for the colour would
    /// write an explicit black at the head of every content stream that opens with a black stroke,
    /// which is a change to every file the library writes for the sake of one that gradients.
    ///
    /// Without it, a pattern pen followed by <c>XPens.Black</c> wrote no "RG" - the remembered
    /// colour matched - and the black line was stroked with the pattern still in the state.
    /// </remarks>
    bool _realizedStrokePattern;

    #endregion

    #region Fill

    /// <summary>
    /// The fill colour last written, or null when none has been, or a pattern has replaced it.
    /// </summary>
    /// <remarks>
    /// Null rather than <see cref="XColor.Empty"/> for the reason given on
    /// <see cref="_realizedStrokeColor"/>: Empty's alpha is 0, so a fully transparent fill matched
    /// it and was painted opaque. Null writes both the colour and the alpha.
    /// </remarks>
    XColor? _realizedFillColor;
    bool _realizedNonStrokeOverPrint;

    /// <summary>
    /// Realizes the colours text or a path is painted with.
    /// </summary>
    /// <param name="brush">The brush the fill colour is taken from.</param>
    /// <param name="colorMode">Whether colours are written as RGB or CMYK.</param>
    /// <param name="renderingMode">
    /// The text rendering mode: 0 fills, 1 strokes, 2 does both. Anything else is treated as a
    /// path rather than as text.
    /// </param>
    /// <param name="fontEmSize">The em size of the font, which scales a simulated stroke width.</param>
    /// <param name="isForPen">True when realizing the stroking colour rather than the filling one.</param>
    /// <param name="textPen">
    /// The pen a stroking text rendering mode strokes with. Null falls back to a pen made from the
    /// brush's own colour, which is how bold simulation fattens a face that has no bold of its own.
    /// Ignored outside the text modes.
    /// </param>
    public void RealizeBrush(XBrush brush, PdfColorMode colorMode, int renderingMode, double fontEmSize, bool isForPen = false, XPen textPen = null)
    {
        // Mode 0 fills, 1 strokes, 2 does both. Mode 3 paints nothing and is not produced here -
        // PDFKit does not produce it either, and nothing asks for invisible text.
        // Reference: TABLE 5.3  Text rendering modes / Page 402
        var fills = renderingMode == 0 || renderingMode == 2;
        var strokes = renderingMode == 1 || renderingMode == 2;

        var solidBrush = brush as XSolidBrush;

        // A stroking mode is answerable either by the caller's pen or, failing that, by a pen made
        // from the brush's colour. A filling one always needs the brush.
        var haveStroke = strokes && (textPen != null || solidBrush != null);
        var haveFill = fills && solidBrush != null;

        if (haveStroke || haveFill)
        {
            if (fills && solidBrush == null)
                throw new InvalidOperationException("A filling text rendering mode needs a solid color brush to fill with.");

            // Nothing here is a gradient, so a mask left over from one that was is taken off.
            RealizeGradientSoftMask(null);

            if (haveFill)
            {
                // Overprint is dropped when the same glyphs are stroked over the fill, as it was
                // when bold simulation was the only thing that ever stroked them.
                RealizeFillColor(solidBrush.Color, strokes ? false : solidBrush.Overprint, colorMode);
            }

            if (haveStroke)
            {
                // Come here for a caller who asked to stroke the text, or for bold simulation,
                // which fattens a face with no bold of its own by stroking it in its own colour.
                //color = XColors.Green;
                RealizePen(textPen ?? new XPen(solidBrush.Color, fontEmSize * Const.BoldEmphasis), colorMode);
            }
        }
        else
        {
            if (renderingMode != 0)
                throw new InvalidOperationException("Rendering modes other than 0 can only be used with solid color brushes.");

            if (brush is XBaseGradientBrush gradientBrush)
            {
                Debug.Assert(UnrealizedCtm.IsIdentity, "Must realize ctm first.");
                var matrix = _renderer.DefaultViewMatrix;
                matrix.Prepend(EffectiveCtm);
                var pattern = new PdfShadingPattern(_renderer.Owner);
                pattern.SetupFromBrush(gradientBrush, matrix, _renderer);
                var name = _renderer.Resources.AddPattern(pattern);

                // A shading carries colour and no alpha, so a gradient between translucent
                // colours is painted under a mask built from the same geometry. A gradient whose
                // colours are both opaque takes this branch and writes nothing.
                RealizeGradientSoftMask(PdfGradientSoftMask.ForBrush(gradientBrush, matrix, _renderer));

                if (isForPen)
                {
                    _renderer.AppendFormatString("/Pattern CS\n", name);
                    _renderer.AppendFormatString("{0} SCN\n", name);
                }
                else
                {
                    _renderer.AppendFormatString("/Pattern cs\n", name);
                    _renderer.AppendFormatString("{0} scn\n", name);
                }
                // Invalidate fill color.
                _realizedFillColor = null;

                // "SCN" replaced the *stroking* colour space, which the line above does not record
                // - so the pen path says so separately. RealizePen reaches this method only for a
                // pattern pen, and sets the flag back to false for every other kind.
                if (isForPen)
                    _realizedStrokePattern = true;
            }
        }
    }

    /// <summary>
    /// True while a gradient's soft mask is in force, so that it can be taken off again.
    /// </summary>
    /// <remarks>
    /// The soft mask is part of the graphics state and applies to everything painted after it,
    /// not only to the gradient it was built for. It is cloned with the rest of this state, so a
    /// q/Q pair takes it off on its own; within one level it has to be taken off by hand.
    /// </remarks>
    bool _realizedSoftMask;

    /// <summary>
    /// Puts a gradient's soft mask into force, or takes the one in force off.
    /// </summary>
    /// <param name="extGState">
    /// The state carrying the mask, or null for painting that needs no mask.
    /// </param>
    void RealizeGradientSoftMask(PdfExtGState extGState)
    {
        if (extGState == null)
        {
            // Nothing to undo. This is the path every document that has never drawn a translucent
            // gradient takes, and it writes not one byte.
            if (!_realizedSoftMask)
                return;

            _renderer.AppendFormatString("{0} gs\n", _renderer.Resources.AddExtGState(_renderer.NoSoftMaskState));
            _realizedSoftMask = false;
            return;
        }

        _renderer.AppendFormatString("{0} gs\n", _renderer.Resources.AddExtGState(extGState));
        _realizedSoftMask = true;

        // A page carrying a soft mask needs the transparency group that says how to composite it.
        if (_renderer.Page != null)
            _renderer.Page.TransparencyUsed = true;
    }

    private void RealizeFillColor(XColor color, bool overPrint, PdfColorMode colorMode)
    {
        if (color.Spot != null)
        {
            // Not converted to the document's colour mode: the mode says how process colour is
            // written, and a spot colour is not process colour. Its alternate is the caller's.
            RealizeSpotColor(color, _realizedFillColor, false);
        }
        else if (colorMode != PdfColorMode.Cmyk)
        {
            color = ColorSpaceHelper.EnsureColorMode(colorMode, color);

            // A spot colour last realized has the Separation space selected, and "rg" is what
            // selects DeviceRGB again - so its components matching this colour's is no reason to
            // skip it.
            if (_realizedFillColor is not { } realizedRgb || realizedRgb.Spot != null || realizedRgb.Rgb != color.Rgb)
            {
                _renderer.Append(PdfEncoders.ToString(color, PdfColorMode.Rgb));
                _renderer.Append(" rg\n");
            }
        }
        else
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            Debug.Assert(colorMode == PdfColorMode.Cmyk);
            color = ColorSpaceHelper.EnsureColorMode(colorMode, color);

            if (_realizedFillColor is not { } realizedCmyk || realizedCmyk.Spot != null || !ColorSpaceHelper.IsEqualCmyk(realizedCmyk, color))
            {
                _renderer.Append(PdfEncoders.ToString(color, PdfColorMode.Cmyk));
                _renderer.Append(" k\n");
            }
        }

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_renderer.Owner.Version >= 14 && (_realizedFillColor is not { } realized || realized.A != color.A || _realizedNonStrokeOverPrint != overPrint))
        #pragma warning restore S1244
        {

            var extGState = _renderer.Owner.ExtGStateTable.GetExtGStateNonStroke(color.A, overPrint);
            var gs = _renderer.Resources.AddExtGState(extGState);
            _renderer.AppendFormatString("{0} gs\n", gs);

            // Must create transparency group.
            if (_renderer.Page != null && color.A < 1)
                _renderer.Page.TransparencyUsed = true;
        }
        _realizedFillColor = color;
        _realizedNonStrokeOverPrint = overPrint;
    }

    /// <summary>
    /// Selects a spot colour's Separation space and sets its tint, for filling or for stroking.
    /// </summary>
    /// <param name="color">A colour whose <see cref="XColor.Spot"/> is set.</param>
    /// <param name="realized">The colour last realized for the same operation, if any.</param>
    /// <param name="stroke">True for "CS"/"SCN", false for "cs"/"scn".</param>
    /// <remarks>
    /// The space is selected again only when the colorant changes: "cs" resets the colour to the
    /// space's initial tint of 1, so a change of tint alone is "scn" and no more. The caller says
    /// whether a pattern replaced the space since, by passing null for <paramref name="realized"/>.
    /// </remarks>
    void RealizeSpotColor(XColor color, XColor? realized, bool stroke)
    {
        var sameSpace = realized is { Spot: { } realizedSpot } && realizedSpot.Equals(color.Spot);

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (sameSpace && realized.Value.Tint == color.Tint)
            return;
        #pragma warning restore S1244

        if (!sameSpace)
        {
            var colorSpace = _renderer.Owner.SpotColorTable.GetColorSpace(color.Spot);
            var name = _renderer.Resources.AddColorSpace(colorSpace);
            _renderer.AppendFormatString(stroke ? "{0} CS\n" : "{0} cs\n", name);
        }

        _renderer.AppendFormatArgs("{0:" + Config.SignificantFigures3 + (stroke ? "} SCN\n" : "} scn\n"), color.Tint);
    }

    internal void RealizeNonStrokeTransparency(double transparency, PdfColorMode colorMode)
    {
        // With nothing realized the colour is still written, as it was when this read Empty.
        var color = _realizedFillColor ?? XColor.Empty;
        color.A = transparency;
        RealizeFillColor(color, _realizedNonStrokeOverPrint, colorMode);
    }

    #endregion

    #region Text

    internal PdfFont RealizedFont;
    string _realizedFontName = String.Empty;
    double _realizedFontSize;
    int _realizedRenderingMode;  // Reference: TABLE 5.2  Text state operators / Page 398
    double _realizedCharSpace;  // Reference: TABLE 5.2  Text state operators / Page 398

    /// <summary>
    /// The text rendering mode the content stream has been told about.
    /// </summary>
    /// <remarks>
    /// Read by the renderer when a string is drawn out of more than one face and the faces do not
    /// agree about bold simulation, so that it can vary the mode per segment and put back what
    /// this state believes at the end. Telling this state what happened instead would be one more
    /// thing to keep in step, and it would have to be undone again for the next string.
    /// </remarks>
    internal int RealizedRenderingMode => _realizedRenderingMode;

    /// <summary>The character spacing the content stream has been told about.</summary>
    /// <inheritdoc cref="RealizedRenderingMode" path="/remarks"/>
    internal double RealizedCharSpace => _realizedCharSpace;
    double _realizedWordSpace;  // Reference: TABLE 5.2  Text state operators / Page 398
    double _realizedTextRise;  // Reference: TABLE 5.2  Text state operators / Page 398

    // Not 0: a content stream starts with a horizontal scaling of 100 percent, and a state that
    // thought otherwise would write a redundant Tz in front of the first string on every page.
    double _realizedHorizontalScaling = 100;  // Reference: TABLE 5.2  Text state operators / Page 398

    /// <summary>
    /// Returns true if the word spacing asked for has to be drawn by spacing the words out
    /// individually, because Tw cannot express it for this font.
    /// </summary>
    /// <remarks>
    /// Tw applies to every occurrence of the <em>single-byte</em> character code 32, and expressly
    /// not to the byte 32 inside a multiple-byte code (PDF 32000-1 section 9.3.3). A font embedded
    /// as Identity-H writes two-byte codes, so Tw is silently inert for it.
    /// <para>
    /// That is the usual case rather than the odd one: GlobalFontSettings.DefaultFontEncoding is
    /// Unicode, so every XFont built without options of its own is a font Tw cannot speak for.
    /// </para>
    /// </remarks>
    public static bool NeedsWordSpacingByHand(XFont font, XStringFormat format)
        => font.Unicode && format.WordSpacing != 0;

    /// <summary>
    /// The text rendering mode that paints text with the brush and pen given: 0 fills, 1 strokes,
    /// 2 does both. Bold simulation strokes whether or not the caller asked for it.
    /// </summary>
    /// <remarks>
    /// This is PDFKit's rule, which never produces mode 3 - text painted no way at all - and so
    /// neither does this. XGraphics.DrawString rejects a call with neither pen nor brush before it
    /// gets this far, the same way DrawRectangle does.
    /// </remarks>
    public static int TextRenderingMode(XBrush brush, XPen pen, bool boldSimulation)
    {
        var strokes = pen != null || boldSimulation;
        if (brush == null)
            return 1;
        return strokes ? 2 : 0;
    }

    public void RealizeFont(XFont font, XBrush brush, XPen pen, bool boldSimulation, XStringFormat format)
    {
        const string numberFormat = Config.SignificantFigures3;

        var renderingMode = TextRenderingMode(brush, pen, boldSimulation);

        RealizeBrush(brush, _renderer.ColorMode, renderingMode, font.Size, false, pen); // _renderer.page.document.Options.ColorMode);

        // Realize rendering mode.
        if (_realizedRenderingMode != renderingMode)
        {
            _renderer.AppendFormatInt("{0} Tr\n", renderingMode);
            _realizedRenderingMode = renderingMode;
        }

        // Realize character spacing. Bold simulation widens every glyph with a spacing of its own,
        // and the caller's spacing is added to that rather than replaced by it - otherwise asking
        // for a spacing on a simulated-bold font would quietly un-bolden it.
        //
        // Keyed on the simulation rather than on the rendering mode: a caller who strokes their
        // text is in mode 2 as well, and owes none of this widening.
        var charSpace = format.CharacterSpacing;
        if (boldSimulation)
            charSpace += font.Size * Const.BoldEmphasis;

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_realizedCharSpace != charSpace)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatDouble("{0:" + numberFormat + "} Tc\n", charSpace);
            _realizedCharSpace = charSpace;
        }

        // Realize word spacing. Held at zero for the fonts Tw cannot speak for, rather than
        // written and silently ignored; DrawString spaces those out with a TJ array instead.
        var wordSpace = NeedsWordSpacingByHand(font, format) ? 0 : format.WordSpacing;
        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_realizedWordSpace != wordSpace)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatDouble("{0:" + numberFormat + "} Tw\n", wordSpace);
            _realizedWordSpace = wordSpace;
        }

        // Realize horizontal scaling.
        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_realizedHorizontalScaling != format.HorizontalScaling)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatDouble("{0:" + numberFormat + "} Tz\n", format.HorizontalScaling);
            _realizedHorizontalScaling = format.HorizontalScaling;
        }

        // Realize text rise. Ts sits in the text rendering matrix rather than the text matrix, so
        // it lifts the glyphs off the baseline without disturbing where Td puts the next one.
        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (_realizedTextRise != format.TextRise)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatDouble("{0:" + numberFormat + "} Ts\n", format.TextRise);
            _realizedTextRise = format.TextRise;
        }

        RealizedFont = null;
        var fontName = _renderer.GetFontName(font, out RealizedFont);
        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        if (fontName != _realizedFontName || _realizedFontSize != font.Size)
        #pragma warning restore S1244
        {
            _renderer.AppendFormatFont("{0} {1:" + numberFormat + "} Tf\n", fontName, font.Size);
            _realizedFontName = fontName;
            _realizedFontSize = font.Size;
        }
    }

    public XPoint RealizedTextPosition;

    /// <summary>
    /// How far the text transformation matrix currently skews to the right, as the tangent of the
    /// angle - the M21 component the last Tm set. Zero for upright text.
    /// </summary>
    /// <remarks>
    /// This was a bool while a 20° italic simulation was the only thing that ever skewed text.
    /// It is a number now because a caller can ask for an oblique angle of their own, and because
    /// two skews compose by adding their tangents, so simulation and request are one value here.
    /// </remarks>
    public double RealizedTextSkew;

    #endregion

    #region Transformation

    /// <summary>
    /// The already realized part of the current transformation matrix.
    /// </summary>
    public XMatrix RealizedCtm;

    /// <summary>
    /// The not yet realized part of the current transformation matrix.
    /// </summary>
    public XMatrix UnrealizedCtm;

    /// <summary>
    /// Product of RealizedCtm and UnrealizedCtm.
    /// </summary>
    public XMatrix EffectiveCtm;

    /// <summary>
    /// Inverse of EffectiveCtm used for transformation.
    /// </summary>
    public XMatrix InverseEffectiveCtm;

    public XMatrix WorldTransform;

    ///// <summary>
    ///// The world transform in PDF world space.
    ///// </summary>
    //public XMatrix EffectiveCtm
    //{
    //  get
    //  {
    //    //if (MustRealizeCtm)
    //    if (!UnrealizedCtm.IsIdentity)
    //    {
    //      XMatrix matrix = RealizedCtm;
    //      matrix.Prepend(UnrealizedCtm);
    //      return matrix;
    //    }
    //    return RealizedCtm;
    //  }
    //  //set
    //  //{
    //  //  XMatrix matrix = realizedCtm;
    //  //  matrix.Invert();
    //  //  matrix.Prepend(value);
    //  //  unrealizedCtm = matrix;
    //  //  MustRealizeCtm = !unrealizedCtm.IsIdentity;
    //  //}
    //}

    public void AddTransform(XMatrix value, XMatrixOrder matrixOrder)
    {
        // A cm operator can only prepend, so XGraphics turns an appended transform into the
        // equivalent prepend before it gets here.
        if (matrixOrder == XMatrixOrder.Append)
            throw new NotImplementedException("XMatrixOrder.Append");
        var transform = value;
        if (_renderer.Gfx.PageDirection == XPageDirection.Downwards)
        {
            // Take chirality into account and
            // invert the direction of rotation.
            transform.M12 = -value.M12;
            transform.M21 = -value.M21;
        }
        UnrealizedCtm.Prepend(transform);

        WorldTransform.Prepend(value);
    }

    /// <summary>
    /// Realizes the CTM.
    /// </summary>
    public void RealizeCtm()
    {
        //if (MustRealizeCtm)
        if (!UnrealizedCtm.IsIdentity)
        {
            Debug.Assert(!UnrealizedCtm.IsIdentity, "mrCtm is unnecessarily set.");

            const string format = Config.SignificantFigures7;

            var matrix = UnrealizedCtm.GetElements();
            // Use up to six decimal digits to prevent round up problems.
            _renderer.AppendFormatArgs("{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "} cm\n",
                matrix[0], matrix[1], matrix[2], matrix[3], matrix[4], matrix[5]);

            RealizedCtm.Prepend(UnrealizedCtm);
            UnrealizedCtm = new XMatrix();
            EffectiveCtm = RealizedCtm;
            InverseEffectiveCtm = EffectiveCtm;
            InverseEffectiveCtm.Invert();
        }
    }
    #endregion

    #region Clip Path

    public void SetAndRealizeClipRect(XRect clipRect)
    {
        var clipPath = new XGraphicsPath();
        clipPath.AddRectangle(clipRect);
        RealizeClipPath(clipPath);
    }

    public void SetAndRealizeClipPath(XGraphicsPath clipPath)
    {
        RealizeClipPath(clipPath);
    }

    void RealizeClipPath(XGraphicsPath clipPath)
    {
        _renderer.BeginGraphicMode();
        RealizeCtm();
        _renderer.AppendPath(clipPath.CorePath);
        _renderer.Append(clipPath.FillMode == XFillMode.Winding ? "W n\n" : "W* n\n");
    }

    #endregion
}
