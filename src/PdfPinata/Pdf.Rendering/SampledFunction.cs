using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Type 0: a table of samples, read between its entries in a straight line along every input.
/// </summary>
/// <remarks>
/// The samples stay packed as the stream holds them and are unpacked as they are asked for, so
/// nothing the size of <c>/Size</c> is allocated beyond the stream itself. <c>/Order 3</c> is read
/// as 1, and reported: a cubic spline between samples is not computed.
/// </remarks>
internal sealed class SampledFunction : PdfFunction
{
    /// <summary>The most bytes a table may take, by its <c>/Size</c>, outputs and <c>/BitsPerSample</c>.</summary>
    internal const long MaximumSampleBytes = 1L << 24;

    /// <summary>The most inputs a table may have. A lookup reads two samples along each, so 2 to this power.</summary>
    internal const int MaximumInputs = 8;

    private readonly byte[] _samples;
    private readonly int[] _size;
    private readonly int _bitsPerSample;
    private readonly double[] _encode;
    private readonly double[] _decode;

    private SampledFunction(double[] domain, double[] range, byte[] samples, int[] size, int bitsPerSample,
        double[] encode, double[] decode) : base(domain, range)
    {
        _samples = samples;
        _size = size;
        _bitsPerSample = bitsPerSample;
        _encode = encode;
        _decode = decode;
    }

    internal override int OutputCount => _decode.Length / 2;

    // Two samples along each input, for each output.
    internal override long Cost => (1L << _size.Length) * OutputCount;

    internal static PdfFunction Read(PdfDictionary dictionary, double[] domain, double[] range, ShadingWork work,
        DiagnosticSink sink, string subject)
    {
        var elements = dictionary.Elements;
        var inputs = domain.Length / 2;
        var sizes = Numbers(elements["/Size"]);
        if (range == null || range.Length == 0 || inputs > MaximumInputs || sizes == null || sizes.Length != inputs
            || !PdfItemValues.TryGetInteger(elements["/BitsPerSample"], out var bits)
            || bits is not (1 or 2 or 4 or 8 or 12 or 16 or 24 or 32))
        {
            return Invalid(sink, subject, "A sampled function has a /Range, a /Size for each input and a /BitsPerSample.");
        }

        var outputs = range.Length / 2;
        var size = new int[inputs];
        var mostEntries = MaximumSampleBytes * 8 / ((long)outputs * bits);
        long entries = 1;
        for (var index = 0; index < inputs; index++)
        {
            if (!(sizes[index] >= 1 && sizes[index] <= int.MaxValue))
                return Invalid(sink, subject, "Each /Size of a sampled function is at least 1.");

            size[index] = (int)sizes[index];
            entries *= size[index];

            // Asked at each input and before the stream is decoded, so that the product cannot
            // overflow and a table too large costs nothing.
            if (entries > mostEntries)
            {
                sink.Report(PageRenderProblem.LimitReached, subject,
                    "A sampled function's table is too large. What it colours is not drawn.");
                return null;
            }
        }

        // Decoded no further than the table, which is all that is read of it.
        var needed = (entries * outputs * bits + 7) / 8;
        var samples = work.Decode(dictionary, needed);
        if (samples == null || samples.Length < needed)
            return Undecodable(work, sink, subject, "A sampled function's stream is shorter than its /Size, or cannot be decoded.");

        var encode = Numbers(elements["/Encode"]);
        if (encode == null || encode.Length != 2 * inputs)
        {
            encode = new double[2 * inputs];
            for (var index = 0; index < inputs; index++)
                encode[2 * index + 1] = size[index] - 1;
        }

        var decode = Numbers(elements["/Decode"]);
        if (decode == null || decode.Length != range.Length)
            decode = range;

        if (PdfItemValues.TryGetInteger(elements["/Order"], out var order) && order == 3)
        {
            sink.Report(PageRenderProblem.UnsupportedFeature, "/Order",
                "A sampled function of /Order 3 is read between its samples in a straight line, not along a cubic spline.");
        }

        return new SampledFunction(domain, range, samples, size, bits, encode, decode);
    }

    protected override void Compute(ReadOnlySpan<double> input, Span<double> output)
    {
        var inputs = _size.Length;
        Span<int> low = stackalloc int[inputs];
        Span<double> along = stackalloc double[inputs];
        for (var index = 0; index < inputs; index++)
        {
            var (from, to) = DomainOf(index);
            var position = Clip(Interpolate(input[index], from, to, _encode[2 * index], _encode[2 * index + 1]),
                0, _size[index] - 1);
            low[index] = Math.Min((int)position, Math.Max(_size[index] - 2, 0));
            along[index] = position - low[index];
        }

        output.Clear();
        var outputs = output.Length;
        var top = Math.Pow(2, _bitsPerSample) - 1;

        // Every corner of the cell the point is in, weighted by how near the point is to it.
        for (var corner = 0; corner < 1 << inputs; corner++)
        {
            var weight = 1.0;
            long entry = 0;
            long stride = 1;
            for (var index = 0; index < inputs; index++)
            {
                var high = (corner >> index & 1) == 1;
                weight *= high ? along[index] : 1 - along[index];
                entry += stride * Math.Min(low[index] + (high ? 1 : 0), _size[index] - 1);
                stride *= _size[index];
            }

#pragma warning disable S1244 // Exact on purpose: a corner of no weight at all adds nothing.
            if (weight == 0)
                continue;
#pragma warning restore S1244

            for (var index = 0; index < outputs; index++)
                output[index] += weight * Sample((entry * outputs + index) * _bitsPerSample);
        }

        for (var index = 0; index < outputs; index++)
            output[index] = Interpolate(output[index], 0, top, _decode[2 * index], _decode[2 * index + 1]);
    }

    protected override void AddBreakpoints(List<double> into)
    {
        // The function turns a corner at each sample. A table with more samples than are handed
        // on is sampled like any curve.
        if (_size.Length != 1 || _size[0] > MaximumBreakpoints)
            return;

        var (from, to) = DomainOf(0);
        for (var sample = 1; sample < _size[0] - 1 && into.Count < MaximumBreakpoints; sample++)
        {
            var x = Interpolate(sample, _encode[0], _encode[1], from, to);
            if (x > from && x < to)
                into.Add(x);
        }
    }

    // The sample that starts at a bit, the high bit first.
    private double Sample(long bit)
    {
        var at = (int)(bit >> 3);
        if (_bitsPerSample >= 8 && _bitsPerSample != 12)
        {
            uint value = 0;
            for (var index = 0; index < _bitsPerSample / 8; index++)
                value = value << 8 | _samples[at + index];
            return value;
        }

        // 1, 2, 4 and 12 bits: no sample reaches past the second byte it touches.
        var pair = _samples[at] << 8 | (at + 1 < _samples.Length ? _samples[at + 1] : 0);
        var shift = 16 - (int)(bit & 7) - _bitsPerSample;
        return pair >> shift & (1 << _bitsPerSample) - 1;
    }
}
