using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

// Shadings and patterns: sh, and the colour scn sets in a pattern colour space.
internal sealed partial class ContentInterpreter
{
    /// <summary>
    /// How many copies of a tiling pattern's cell may reach into one tile. A cell larger than
    /// its step overlaps its neighbours, and a tile is drawn from every cell that reaches into it.
    /// </summary>
    internal const int MaximumOverlappingCells = 16;

    /// <summary>
    /// The shortest a tiling pattern's step may be on the device, in pixels. Below it the tiles
    /// cannot be told apart, and the pattern is painted as one colour.
    /// </summary>
    internal const double MinimumTileStep = 0.25;

    /// <summary>
    /// <c>sh</c>: the shading over everything the clip leaves, in the user space in force. It is
    /// a fill of this interpreter's whole surface, so a device needs no member for it.
    /// </summary>
    private void PaintShading(COperator op)
    {
        var name = Operands.NameFromEnd(op, 0);
        if (name == null)
        {
            WrongOperands(op);
            return;
        }

        var shading = _resources.Lookup("/Shading", name);
        if (shading == null)
        {
            _sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no shading of that name.");
            return;
        }

        // Null has been reported.
        var read = _context.ShadingOf(shading, name);
        if (read == null || _surface.IsEmpty || !IsUsable(State.Ctm, name))
            return;

        // sh takes no notice of /Background (ISO 32000-1 8.7.4.3, Table 78).
        _device.Fill(_surface, _surfaceTransform, XFillMode.Winding,
            new RenderPaint(new ShadedPaint(read.Plain, State.Ctm, read.Box)).WithAlpha(State.FillAlpha));
    }

    /// <summary><c>scn</c> and <c>SCN</c> in a pattern colour space: the operands end in a pattern's name.</summary>
    private void SetPatternColor(COperator op, bool stroking, PatternColorSpace space)
    {
        if (_colorsLocked)
            return;

        var name = Operands.NameFromEnd(op, 0);
        if (name == null)
        {
            WrongOperands(op);
            return;
        }

        SetColor(stroking, space, PatternNamed(name, op, space));
    }

    /// <summary>
    /// The paint a pattern's name stands for. A pattern that cannot be painted with has been
    /// reported and is the pattern that paints nothing.
    /// </summary>
    private RenderPaint PatternNamed(string name, COperator op, PatternColorSpace space)
    {
        var pattern = _resources.Lookup("/Pattern", name);
        if (pattern == null)
        {
            _sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no pattern of that name.");
            return RenderPaint.Nothing;
        }

        if (!TryReadMatrix(pattern, out var matrix))
        {
            _sink.Report(PageRenderProblem.InvalidValue, name, "The pattern's /Matrix is not six numbers. The pattern paints nothing.");
            return RenderPaint.Nothing;
        }

        // Over the space the stream began in, and not the one cm has made of it since.
        var toDevice = XMatrix.Multiply(matrix, _streamSpace);
        if (!IsUsable(toDevice, name))
            return RenderPaint.Nothing;

        PdfItemValues.TryGetInteger(pattern.Elements["/PatternType"], out var type);
        switch (type)
        {
            case 1:
                return TilingNamed(name, pattern, op, space, toDevice);

            case 2:
                if (pattern.Elements.GetDictionary("/ExtGState") != null)
                {
                    _sink.Report(PageRenderProblem.UnsupportedFeature, "/ExtGState",
                        "The graphics state a shading pattern names is not applied.");
                }

                var shading = pattern.Elements.GetDictionary("/Shading");
                if (shading == null)
                {
                    _sink.Report(PageRenderProblem.InvalidValue, name, "The shading pattern has no /Shading. It paints nothing.");
                    return RenderPaint.Nothing;
                }

                // Null has been reported.
                var read = _context.ShadingOf(shading, name);
                return read == null
                    ? RenderPaint.Nothing
                    : new RenderPaint(new ShadedPaint(read.OverBackground, toDevice, read.Box));

            default:
                _sink.Report(PageRenderProblem.InvalidValue, name, "A pattern's /PatternType is 1 or 2. The pattern paints nothing.");
                return RenderPaint.Nothing;
        }
    }

    /// <summary>A tiling pattern, as the picture of one tile and the rectangle it repeats by.</summary>
    private RenderPaint TilingNamed(string name, PdfDictionary pattern, COperator op, PatternColorSpace space,
        XMatrix toDevice)
    {
        PdfItemValues.TryGetInteger(pattern.Elements["/PaintType"], out var paintType);
        if (paintType is not (1 or 2) || !TryReadBox(pattern, out var box) || !(box.Width > 0 && box.Height > 0)
            || !PdfItemValues.TryGetNumber(pattern.Elements["/XStep"], out var xStep)
            || !PdfItemValues.TryGetNumber(pattern.Elements["/YStep"], out var yStep)
            || !IsStep(xStep) || !IsStep(yStep))
        {
            _sink.Report(PageRenderProblem.InvalidValue, name,
                "A tiling pattern has a /PaintType of 1 or 2, a /BBox with an area, and an /XStep and a /YStep that are not zero. The pattern paints nothing.");
            return RenderPaint.Nothing;
        }

        // The tiles of a step and of its negative are the same tiles.
        xStep = Math.Abs(xStep);
        yStep = Math.Abs(yStep);

        PagePaint? tint = null;
        if (paintType == 2)
        {
            tint = TintOf(op, space);
            if (tint == null)
                return RenderPaint.Nothing;
        }

        var tiling = _context.TilingOf(pattern, tint) ?? RecordTiling(name, pattern, box, xStep, yStep, tint);
        if (tiling == null)
            return RenderPaint.Nothing;

        if (!_isRecording && (Length(toDevice, xStep, 0) < MinimumTileStep || Length(toDevice, 0, yStep) < MinimumTileStep))
        {
            _sink.Report(PageRenderProblem.LimitReached, name,
                "The tiling pattern's tiles are too small to tell apart at this scale. It is painted as one colour.");
            return tiling.Fallback ?? RenderPaint.Nothing;
        }

        return new RenderPaint(new ShadedPaint(tiling, toDevice, null));
    }

    /// <summary>
    /// The colour an uncoloured pattern is painted in: the operands before the pattern's name,
    /// in the space the pattern space is over. Null, which is reported, where there is none.
    /// </summary>
    private PagePaint? TintOf(COperator op, PatternColorSpace space)
    {
        var underlying = space.Underlying;
        var count = underlying == null ? 0 : underlying.ComponentCount < 0 ? op.Operands.Count - 1 : underlying.ComponentCount;
        var components = new double[Math.Max(count, 0)];
        if (underlying == null || count == 0 || !Operands.TryNumbers(op, components, 1))
        {
            _sink.Report(PageRenderProblem.InvalidValue, op.Name,
                "An uncoloured tiling pattern is painted in a colour of the space its pattern space is over, and none was given. The pattern paints nothing.");
            return null;
        }

        return underlying.ToRgb(components);
    }

    /// <summary>
    /// Records a tiling pattern's cell, in pattern space, and makes of it the tile that repeats.
    /// Null where the cell cannot be drawn, which is reported.
    /// </summary>
    private PageTiling RecordTiling(string name, PdfDictionary pattern, XRect box, double xStep, double yStep,
        PagePaint? tint)
    {
        // A cell counts as a form does: it is content that draws content, and it may paint with itself.
        if (!_context.Forms.TryEnter(pattern))
        {
            _sink.Report(PageRenderProblem.LimitReached, name,
                "The pattern paints with itself, or patterns and forms are nested too deep or drawn too often. It paints nothing.");
            return null;
        }

        PagePicture cell;
        try
        {
            var content = ContentOf(name, pattern);
            if (content == null)
            {
                _sink.Report(PageRenderProblem.UnreadableContent, name, "The pattern's content could not be decoded. It paints nothing.");
                return null;
            }

            var initial = new GraphicsState { Ctm = XMatrix.Identity };
            if (tint != null)
                initial.FillColor = initial.StrokeColor = tint.Value;

            cell = Record(_context, content, _resources.ForForm(pattern), _sink.Stream + name, initial, box,
                colorsLocked: tint != null);
        }
        finally
        {
            _context.Forms.Leave(pattern);
        }

        // Asked after the cell is recorded: a pattern the cell paints with is kept before this one.
        var kept = _context.KeepsTiling(pattern);
        var tiling = new PageTiling(Periodic(cell, box, xStep, yStep, name), new XRect(box.X, box.Y, xStep, yStep),
            tint ?? cell.MeanColor, kept);
        if (kept)
            _context.Keep(pattern, tint, tiling);
        return tiling;
    }

    /// <summary>
    /// The cell as one tile of its step: where the step is shorter than the cell, the
    /// neighbouring cells that reach into the tile are drawn into it as well, and all of it is
    /// clipped to the tile.
    /// </summary>
    private PagePicture Periodic(PagePicture cell, XRect box, double xStep, double yStep, string name)
    {
        var across = Math.Ceiling(box.Width / xStep - 1e-9);
        var up = Math.Ceiling(box.Height / yStep - 1e-9);
        if (across <= 1 && up <= 1)
            return cell;

        if (across * up > MaximumOverlappingCells)
        {
            _sink.Report(PageRenderProblem.LimitReached, name,
                "The tiling pattern's cells overlap too many times over. Each tile is drawn from one cell.");
            across = up = 1;
        }

        var tile = new PagePathBuilder();
        tile.AddRectangle(box.X, box.Y, xStep, yStep);

        var recorder = new PictureRecorder();
        recorder.Save();
        recorder.Clip(tile.ToPath(), XMatrix.Identity, XFillMode.Winding);

        // From the cell furthest back, so that the tile's own cell is painted last.
        for (var row = (int)up - 1; row >= 0; row--)
        {
            for (var column = (int)across - 1; column >= 0; column--)
                cell.Replay(recorder, new XMatrix(1, 0, 0, 1, -column * xStep, -row * yStep));
        }

        recorder.Restore();
        return recorder.ToPicture();
    }

    /// <summary>Whether a shader may be laid on the device by a transform: reported where it may not.</summary>
    private bool IsUsable(XMatrix toDevice, string name)
    {
        if (IsFinite(toDevice) && toDevice.HasInverse)
            return true;

        _sink.Report(PageRenderProblem.InvalidValue, name,
            "The shading or pattern lies under a transform that has no area or is too large. It paints nothing.");
        return false;
    }

    private static bool IsStep(double step) => double.IsFinite(step) && Math.Abs(step) > 0;

    /// <summary>How long a vector of the shader's space is on the device.</summary>
    private static double Length(XMatrix toDevice, double x, double y)
    {
        var dx = toDevice.M11 * x + toDevice.M21 * y;
        var dy = toDevice.M12 * x + toDevice.M22 * y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
