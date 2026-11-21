using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// A shading dictionary, read once: the shader a device paints with, and what the core does
/// round it.
/// </summary>
internal sealed class ReadShading
{
    internal ReadShading(PageShader plain, PageShader overBackground, PagePath box)
    {
        Plain = plain;
        OverBackground = overBackground;
        Box = box;
    }

    /// <summary>The shading alone, which is what <c>sh</c> paints: it ignores <c>/Background</c>.</summary>
    internal PageShader Plain { get; }

    /// <summary>
    /// The shading over its <c>/Background</c>, which is what a shading pattern paints. The same
    /// instance as <see cref="Plain"/> where there is no background.
    /// </summary>
    internal PageShader OverBackground { get; }

    /// <summary>The <c>/BBox</c> in the shading's coordinates, or null.</summary>
    internal PagePath Box { get; }
}

/// <summary>
/// Turns a shading dictionary (ISO 32000-1 8.7.4) into a <see cref="PageShader"/>: its function
/// evaluated, its colour space taken to RGB. A new kind of shading is added here and nowhere in
/// the interpreter.
/// </summary>
internal static class Shadings
{
    /// <summary>The most stops a gradient is handed over with.</summary>
    internal const int MaximumStops = 256;

    /// <returns>The shading, or null where it cannot be read or is of a kind not drawn, which is reported.</returns>
    internal static ReadShading Read(PdfDictionary shading, bool isShared, RenderContext context, string subject)
    {
        var sink = context.Sink;
        if (context.ShadingWork.IsSpent)
            return Spent(sink, subject);

        var elements = shading.Elements;
        if (!PdfItemValues.TryGetInteger(elements["/ShadingType"], out var type) || type is < 1 or > 7)
            return Invalid(sink, subject, "A shading's /ShadingType is a number from 1 to 7.");

        var space = PdfItemValues.IsNull(elements["/ColorSpace"]) ? null : ColorSpaces.Resolve(elements["/ColorSpace"], sink);
        if (space is null or PatternColorSpace)
            return Invalid(sink, subject, "A shading has a /ColorSpace, which is not a pattern space.");

        PdfFunction function = null;
        if (!PdfItemValues.IsNull(elements["/Function"]))
        {
            function = context.Functions.Read(elements["/Function"], subject);
            if (function == null)
                return null;
        }

        var colors = ShadingColors.Of(space, function, type, context.ShadingWork);
        if (colors == null)
            return Invalid(sink, subject, "A shading's /Function has the inputs its type takes and an output for each colour component.");

        PagePaint? background = null;
        if (PdfFunction.Numbers(elements["/Background"]) is { } components
            && (space.ComponentCount < 0 || components.Length == space.ComponentCount))
        {
            background = space.ToRgb(components);
        }

        Func<PagePaint?, PageShader> make = type switch
        {
            2 or 3 => Gradient(shading, type, colors, isShared, sink, subject),
            _ => MeshReader.Read(shading, type, colors, isShared, sink, subject)
        };
        if (context.ShadingWork.IsSpent)
            return Spent(sink, subject);

        if (make == null)
            return null;

        var plain = make(null);
        return new ReadShading(plain, background == null ? plain : make(background), BoxOf(shading));
    }

    /// <summary>Types 2 and 3, but for the background, which is the one thing two shaders of one shading differ in.</summary>
    private static Func<PagePaint?, PageShader> Gradient(PdfDictionary shading, int type, ShadingColors colors,
        bool isShared, DiagnosticSink sink, string subject)
    {
        var elements = shading.Elements;
        var coords = PdfFunction.Numbers(elements["/Coords"]);
        if (coords == null || coords.Length != (type == 2 ? 4 : 6))
        {
            Invalid(sink, subject, "An axial shading's /Coords are four numbers, and a radial shading's six.");
            return null;
        }

        var domain = PdfFunction.Numbers(elements["/Domain"]);
        if (domain == null || domain.Length != 2)
            domain = [0, 1];

        var extend = PdfReference.Dereference(elements["/Extend"]) as PdfArray;
        var extendStart = extend is { Elements.Count: 2 } && PdfItemValues.TryGetBoolean(extend.Elements[0], out var first) && first;
        var extendEnd = extend is { Elements.Count: 2 } && PdfItemValues.TryGetBoolean(extend.Elements[1], out var last) && last;

        var stops = colors.Ramp(domain[0], domain[1]);
        if (type == 2)
        {
            XPoint start = new(coords[0], coords[1]), end = new(coords[2], coords[3]);
            if (start == end)
            {
                Invalid(sink, subject, "An axial shading's axis has no length.");
                return null;
            }

            return background => new PageAxialShading(start, end, stops, extendStart, extendEnd, background, isShared);
        }

        if (coords[2] < 0 || coords[5] < 0)
        {
            Invalid(sink, subject, "A radial shading's radii are not negative.");
            return null;
        }

        return background => new PageRadialShading(new XPoint(coords[0], coords[1]), coords[2],
            new XPoint(coords[3], coords[4]), coords[5], stops, extendStart, extendEnd, background, isShared);
    }

    private static PagePath BoxOf(PdfDictionary shading)
    {
        var stated = PdfPageResizer.RectangleOf(shading, "/BBox");
        if (stated == null || !double.IsFinite(stated.X1 + stated.Y1 + stated.X2 + stated.Y2))
            return null;

        var box = new PagePathBuilder();
        box.AddRectangle(Math.Min(stated.X1, stated.X2), Math.Min(stated.Y1, stated.Y2),
            Math.Abs(stated.X2 - stated.X1), Math.Abs(stated.Y2 - stated.Y1));
        return box.ToPath();
    }

    private static ReadShading Spent(DiagnosticSink sink, string subject)
    {
        sink.Report(PageRenderProblem.LimitReached, subject,
            "The page's shadings cost more to read than one render spends. This one is not drawn.");
        return null;
    }

    internal static ReadShading Invalid(DiagnosticSink sink, string subject, string message)
    {
        sink.Report(PageRenderProblem.InvalidValue, subject, message + " The shading is not drawn.");
        return null;
    }
}

/// <summary>A shading's colour: its function, where it has one, and then its colour space, to RGB.</summary>
internal sealed class ShadingColors
{
    private readonly RenderColorSpace _space;
    private readonly PdfFunction _function;

    private ShadingColors(RenderColorSpace space, PdfFunction function, ShadingWork work)
    {
        _space = space;
        _function = function;
        Work = work;
    }

    /// <summary>What the render may still spend, which each colour found through the function draws on.</summary>
    internal ShadingWork Work { get; }

    /// <summary>
    /// Whether a mesh's vertices carry the function's one input in place of a colour, so that
    /// what is blended across a triangle is the input and not the colour.
    /// </summary>
    internal bool BlendsInput => _function is { InputCount: 1 };

    /// <summary>How many numbers a colour is given in: one for a function's input, or the colour space's components.</summary>
    internal int ComponentCount => _function != null ? _function.InputCount : _space.ComponentCount;

    /// <summary>The low and the high of the function's input, for a shading that has a function.</summary>
    internal (double Low, double High) DomainOf(int input) => _function.DomainOf(input);

    /// <returns>Null where the function does not fit the shading type and the colour space.</returns>
    internal static ShadingColors Of(RenderColorSpace space, PdfFunction function, int shadingType, ShadingWork work)
    {
        var inputs = shadingType == 1 ? 2 : 1;
        // A mesh with no function states a colour at each vertex, in as many numbers as the space takes.
        if (function == null)
        {
            return shadingType >= 4 && space.ComponentCount is > 0 and <= PdfFunction.MaximumArity
                ? new ShadingColors(space, null, work)
                : null;
        }

        var fits = function.InputCount == inputs
                   && (space.ComponentCount < 0 || function.OutputCount == space.ComponentCount);
        return fits ? new ShadingColors(space, function, work) : null;
    }

    /// <summary>
    /// The colour of what a vertex or a point carries: the function's inputs, or the components
    /// themselves. Black, and the function not run, once <see cref="Work"/> is spent, because the
    /// shading is then not drawn.
    /// </summary>
    internal PagePaint At(ReadOnlySpan<double> values)
    {
        if (_function == null)
            return _space.ToRgb(values);

        Work.Spend(_function.Cost);
        if (Work.IsSpent)
            return default;

        Span<double> components = stackalloc double[_function.OutputCount];
        _function.Evaluate(values, components);
        return _space.ToRgb(components);
    }

    private PagePaint At(double t)
    {
        Span<double> input = stackalloc double[1];
        input[0] = t;
        return At(input);
    }

    /// <summary>
    /// The stops of an axial or radial shading over its <c>/Domain</c>: one at each end, one
    /// either side of each place the function may jump or turn a corner, and then more wherever
    /// the colour halfway between two stops is not the colour halfway along the line between
    /// them, until it is to within one part in 255 or there are <see cref="Shadings.MaximumStops"/>.
    /// </summary>
    internal PageGradientStop[] Ramp(double t0, double t1)
    {
        // Worked in the stops' own offsets, from 0 to 1, so that a domain that runs downwards
        // needs nothing of its own.
        var cuts = new List<double>();
        foreach (var at in _function.Breakpoints)
        {
#pragma warning disable S1244 // Exact on purpose: only a domain of no length at all cannot be divided by.
            var offset = t1 == t0 ? 0 : (at - t0) / (t1 - t0);
#pragma warning restore S1244
            if (offset > 4 * Sliver && offset < 1 - 4 * Sliver)
                cuts.Add(offset);
        }

        cuts.Sort();
        if (cuts.Count > Shadings.MaximumStops / 4)
            cuts.RemoveRange(Shadings.MaximumStops / 4, cuts.Count - Shadings.MaximumStops / 4);
        cuts.Insert(0, 0);
        cuts.Add(1);

        PagePaint Color(double offset) => At(t0 + offset * (t1 - t0));

        var stops = new List<PageGradientStop>();
        for (var piece = 0; piece + 1 < cuts.Count; piece++)
        {
            double from = cuts[piece], to = cuts[piece + 1];
            if (to - from <= 2 * Sliver)
                continue;

            // Just inside each end, so that a jump at a cut is read from the side this piece is on.
            var first = new PageGradientStop(from, Color(piece == 0 ? from : from + Sliver));
            var last = new PageGradientStop(to, Color(piece + 2 == cuts.Count ? to : to - Sliver));
            if (stops.Count == 0 || !Near(stops[^1].Color, first.Color))
                stops.Add(first);

            var start = stops.Count - 1;
            stops.Add(last);
            Refine(stops, start, Color, Shadings.MaximumStops - 2 * (cuts.Count - 2 - piece));
        }

        // A function with no domain to speak of is one colour.
        if (stops.Count < 2)
            return [new PageGradientStop(0, Color(0)), new PageGradientStop(1, Color(1))];

        return stops.ToArray();
    }

    private const double Sliver = 1e-9;

    private const double Tolerance = 1.0 / 255;

    // Halves the stretches from a stop to the end of the list wherever a straight line between
    // two stops is not the function, a pass at a time so that the stops are spent evenly.
    private static void Refine(List<PageGradientStop> stops, int start, Func<double, PagePaint> color, int most)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var split = false;
            for (var index = start; index + 1 < stops.Count && stops.Count < most; index++)
            {
                PageGradientStop a = stops[index], b = stops[index + 1];
                var middle = color((a.Offset + b.Offset) / 2);
                if (IsOnLine(a, b, 0.5, middle) && IsOnLine(a, b, 0.25, color(a.Offset + (b.Offset - a.Offset) / 4))
                                                && IsOnLine(a, b, 0.75, color(a.Offset + (b.Offset - a.Offset) * 3 / 4)))
                {
                    continue;
                }

                stops.Insert(index + 1, new PageGradientStop((a.Offset + b.Offset) / 2, middle));
                index++;
                split = true;
            }

            if (!split)
                return;
        }
    }

    private static bool IsOnLine(PageGradientStop a, PageGradientStop b, double along, PagePaint actual) =>
        Math.Abs(a.Color.Red + along * (b.Color.Red - a.Color.Red) - actual.Red) <= Tolerance
        && Math.Abs(a.Color.Green + along * (b.Color.Green - a.Color.Green) - actual.Green) <= Tolerance
        && Math.Abs(a.Color.Blue + along * (b.Color.Blue - a.Color.Blue) - actual.Blue) <= Tolerance;

    private static bool Near(PagePaint a, PagePaint b) =>
        Math.Abs(a.Red - b.Red) <= Tolerance / 2 && Math.Abs(a.Green - b.Green) <= Tolerance / 2
                                                 && Math.Abs(a.Blue - b.Blue) <= Tolerance / 2;
}
