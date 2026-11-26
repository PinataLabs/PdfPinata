using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// A PDF function (ISO 32000-1 7.10), read once into something that only computes.
/// </summary>
/// <remarks>
/// Immutable, so one instance serves every evaluation. <see cref="Evaluate"/> never throws: the
/// inputs are clipped to <c>/Domain</c>, the outputs to <c>/Range</c>, and an output that is not
/// a number is 0. <see cref="FunctionReader"/> is the one reader and holds every limit on reading;
/// <see cref="Cost"/> is what a caller bounds evaluating by.
/// </remarks>
internal abstract class PdfFunction
{
    /// <summary>How deep functions may hold one another: a stitching of stitchings, an array of functions.</summary>
    internal const int MaximumDepth = 8;

    /// <summary>The most inputs or outputs a function may have.</summary>
    internal const int MaximumArity = 32;

    /// <summary>The most places one function may jump at that are handed to whoever samples it.</summary>
    internal const int MaximumBreakpoints = 256;

    private readonly double[] _domain;
    private readonly double[] _range;
    private double[] _breakpoints;

    /// <param name="domain">A low and a high for each input.</param>
    /// <param name="range">A low and a high for each output, or null where the outputs are not clipped.</param>
    protected PdfFunction(double[] domain, double[] range)
    {
        _domain = domain;
        _range = range;
    }

    internal int InputCount => _domain.Length / 2;

    internal abstract int OutputCount { get; }

    /// <summary>
    /// The most work one <see cref="Evaluate"/> can be: a step of a calculator program, a sample
    /// of a table or an output of an exponential each count one. Known when the function is read.
    /// </summary>
    internal abstract long Cost { get; }

    /// <summary>The low and the high of an input.</summary>
    internal (double Low, double High) DomainOf(int input) => (_domain[2 * input], _domain[2 * input + 1]);

    /// <param name="input">At least <see cref="InputCount"/> numbers.</param>
    /// <param name="output">At least <see cref="OutputCount"/> numbers.</param>
    internal void Evaluate(ReadOnlySpan<double> input, Span<double> output)
    {
        Span<double> clipped = stackalloc double[InputCount];
        for (var index = 0; index < clipped.Length; index++)
            clipped[index] = Clip(input[index], _domain[2 * index], _domain[2 * index + 1]);

        output = output.Slice(0, OutputCount);
        Compute(clipped, output);

        for (var index = 0; index < output.Length; index++)
        {
            if (!double.IsFinite(output[index]))
                output[index] = 0;
            if (_range != null)
                output[index] = Clip(output[index], _range[2 * index], _range[2 * index + 1]);
        }
    }

    /// <summary>The function itself, of inputs already inside the domain.</summary>
    protected abstract void Compute(ReadOnlySpan<double> input, Span<double> output);

    /// <summary>
    /// The inputs at which a function of one input may jump or turn a corner: the bounds of a
    /// stitching, the knots of a table. Whoever samples the function puts a sample either side
    /// of each, so that a hard edge stays hard.
    /// </summary>
    /// <remarks>
    /// In ascending order, at most <see cref="MaximumBreakpoints"/> of them, and found once: a
    /// stitching whose pieces all name one function, nested a few deep, is searched in as many
    /// steps as it has functions and not in as many as it has paths through them.
    /// </remarks>
    internal double[] Breakpoints
    {
        get
        {
            if (_breakpoints == null)
            {
                var found = new List<double>();
                AddBreakpoints(found);
                found.Sort();
                if (found.Count > MaximumBreakpoints)
                    found.RemoveRange(MaximumBreakpoints, found.Count - MaximumBreakpoints);
                _breakpoints = found.ToArray();
            }

            return _breakpoints;
        }
    }

    /// <summary>Adds the function's own <see cref="Breakpoints"/>, in any order.</summary>
    protected virtual void AddBreakpoints(List<double> into)
    {
    }

    // Written so that NaN, which compares false with everything, falls to the low end.
    protected static double Clip(double value, double low, double high) => value > low ? Math.Min(value, high) : low;

    /// <summary>The value at <paramref name="x"/> of the line through (x0, y0) and (x1, y1).</summary>
    protected static double Interpolate(double x, double x0, double x1, double y0, double y1)
    {
#pragma warning disable S1244 // Exact on purpose: only an interval of no length at all cannot be divided by.
        return x1 == x0 ? y0 : y0 + (x - x0) * (y1 - y0) / (x1 - x0);
#pragma warning restore S1244
    }

    /// <summary>
    /// Reports a function whose stream could not be decoded: because the render has decoded all
    /// it may, or because the stream is broken.
    /// </summary>
    internal static PdfFunction Undecodable(ShadingWork work, DiagnosticSink sink, string subject, string message)
    {
        if (!work.HasDecodedAll)
            return Invalid(sink, subject, message);

        sink.Report(PageRenderProblem.LimitReached, subject,
            "The page's shadings decode more than one render reads. What this function colours is not drawn.");
        return null;
    }

    internal static PdfFunction Invalid(DiagnosticSink sink, string subject, string message)
    {
        sink.Report(PageRenderProblem.InvalidValue, subject, message + " What it colours is not drawn.");
        return null;
    }

    /// <summary>An array of finite numbers, or null where the item is anything else.</summary>
    internal static double[] Numbers(PdfItem item)
    {
        if (PdfReference.Dereference(item) is not PdfArray array)
            return null;

        var numbers = new double[array.Elements.Count];
        for (var index = 0; index < numbers.Length; index++)
        {
            if (!PdfItemValues.TryGetNumber(array.Elements[index], out numbers[index]) || !double.IsFinite(numbers[index]))
                return null;
        }

        return numbers;
    }

    /// <summary>An array of pairs, each a low and a high that is not below it, or null.</summary>
    internal static double[] Intervals(PdfItem item)
    {
        var numbers = Numbers(item);
        if (numbers == null || numbers.Length % 2 != 0)
            return null;

        for (var index = 0; index < numbers.Length; index += 2)
        {
            if (numbers[index] > numbers[index + 1])
                return null;
        }

        return numbers;
    }
}

/// <summary>Type 2: <c>C0 + x^N (C1 - C0)</c>.</summary>
internal sealed class ExponentialFunction : PdfFunction
{
    private readonly double[] _c0;
    private readonly double[] _c1;
    private readonly double _exponent;

    private ExponentialFunction(double[] domain, double[] range, double[] c0, double[] c1, double exponent)
        : base(domain, range)
    {
        _c0 = c0;
        _c1 = c1;
        _exponent = exponent;
    }

    internal override int OutputCount => _c0.Length;

    internal override long Cost => _c0.Length;

    internal static PdfFunction Read(PdfDictionary dictionary, double[] domain, double[] range, DiagnosticSink sink,
        string subject)
    {
        var elements = dictionary.Elements;
        var c0 = PdfItemValues.IsNull(elements["/C0"]) ? [0] : Numbers(elements["/C0"]);
        var c1 = PdfItemValues.IsNull(elements["/C1"]) ? [1] : Numbers(elements["/C1"]);
        if (domain.Length != 2 || c0 == null || c1 == null || c0.Length == 0 || c0.Length != c1.Length
            || c0.Length > MaximumArity || range != null && range.Length != 2 * c0.Length
            || !PdfItemValues.TryGetNumber(elements["/N"], out var exponent) || !double.IsFinite(exponent))
        {
            return Invalid(sink, subject, "An exponential function has one input, an /N, and a /C0 and a /C1 of one length.");
        }

        return new ExponentialFunction(domain, range, c0, c1, exponent);
    }

    protected override void Compute(ReadOnlySpan<double> input, Span<double> output)
    {
        // A negative input under a fractional exponent, or zero under a negative one, is no
        // number. It is taken as the start of the ramp.
        var weight = Math.Pow(input[0], _exponent);
        if (!double.IsFinite(weight))
            weight = 0;

        for (var index = 0; index < output.Length; index++)
            output[index] = _c0[index] + weight * (_c1[index] - _c0[index]);
    }
}

/// <summary>Type 3: functions of one input, each over its own stretch of the domain.</summary>
internal sealed class StitchingFunction : PdfFunction
{
    private readonly PdfFunction[] _functions;

    // The domain's low, each bound, and the domain's high: piece k runs from _edges[k] to _edges[k + 1].
    private readonly double[] _edges;
    private readonly double[] _encode;

    internal StitchingFunction(double[] domain, double[] range, PdfFunction[] functions, double[] edges,
        double[] encode) : base(domain, range)
    {
        _functions = functions;
        _edges = edges;
        _encode = encode;

        // One piece is evaluated, after a search of the bounds.
        foreach (var function in functions)
            Cost = Math.Max(Cost, function.Cost);
        Cost += functions.Length;
    }

    internal override int OutputCount => _functions[0].OutputCount;

    internal override long Cost { get; }

    protected override void Compute(ReadOnlySpan<double> input, Span<double> output)
    {
        // A bound belongs to the piece that begins at it, and the domain's high to the last.
        var piece = _functions.Length - 1;
        for (var index = 1; index < _functions.Length; index++)
        {
            if (input[0] < _edges[index])
            {
                piece = index - 1;
                break;
            }
        }

        Span<double> encoded = stackalloc double[1];
        encoded[0] = Interpolate(input[0], _edges[piece], _edges[piece + 1], _encode[2 * piece], _encode[2 * piece + 1]);
        _functions[piece].Evaluate(encoded, output);
    }

    protected override void AddBreakpoints(List<double> into)
    {
        for (var piece = 0; piece < _functions.Length && into.Count < MaximumBreakpoints; piece++)
        {
            if (piece > 0)
                into.Add(_edges[piece]);

            // The piece's own, taken from its input back to this function's. Only those strictly
            // between the ends of the piece's /Encode land inside the piece.
            var inner = _functions[piece].Breakpoints;
            double from = _encode[2 * piece], to = _encode[2 * piece + 1];
            var high = Math.Max(from, to);
            for (var index = FirstAbove(inner, Math.Min(from, to)); index < inner.Length && inner[index] < high
                                                                     && into.Count < MaximumBreakpoints; index++)
            {
                var x = Interpolate(inner[index], from, to, _edges[piece], _edges[piece + 1]);
                if (x > _edges[piece] && x < _edges[piece + 1])
                    into.Add(x);
            }
        }
    }

    // The index of the first of the ascending values that is above the low.
    private static int FirstAbove(double[] values, double low)
    {
        int start = 0, end = values.Length;
        while (start < end)
        {
            var middle = (start + end) / 2;
            if (values[middle] > low)
                end = middle;
            else
                start = middle + 1;
        }

        return start;
    }
}

/// <summary>An array of functions of one output each, as one function of as many outputs.</summary>
internal sealed class FunctionGroup : PdfFunction
{
    private readonly PdfFunction[] _functions;

    internal FunctionGroup(PdfFunction[] functions) : base(DomainOf(functions[0]), null)
    {
        _functions = functions;
        foreach (var function in functions)
            Cost += function.Cost;
    }

    internal override int OutputCount => _functions.Length;

    internal override long Cost { get; }

    protected override void Compute(ReadOnlySpan<double> input, Span<double> output)
    {
        for (var index = 0; index < _functions.Length; index++)
            _functions[index].Evaluate(input, output.Slice(index, 1));
    }

    protected override void AddBreakpoints(List<double> into)
    {
        foreach (var function in _functions)
        {
            if (into.Count < MaximumBreakpoints)
                into.AddRange(function.Breakpoints);
        }
    }

    // Each member clips its own input, so the group's domain only says how many inputs there are.
    private static double[] DomainOf(PdfFunction function)
    {
        var domain = new double[2 * function.InputCount];
        for (var index = 0; index < function.InputCount; index++)
            (domain[2 * index], domain[2 * index + 1]) = (double.NegativeInfinity, double.PositiveInfinity);
        return domain;
    }
}
