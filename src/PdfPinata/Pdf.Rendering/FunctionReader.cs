using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Reads the functions of one render (ISO 32000-1 7.10), each dictionary once, however many
/// shadings and other functions name it.
/// </summary>
/// <remarks>
/// A function is kept by the identity of its dictionary, so a stitching whose pieces all name one
/// function, nested a few deep, is read in as many steps as it has objects and not in as many as
/// it has paths through them. One that cannot be read is kept as that, with why, so that a stream
/// which fails to decode is decoded once, however many shadings name it, and each of them is
/// reported for it. A file can still hold many functions that are each different, and
/// <see cref="MaximumReads"/> bounds those; the streams they decode are charged to the render's <see cref="ShadingWork"/>.
/// </remarks>
internal sealed class FunctionReader(DiagnosticSink sink, ShadingWork work)
{
    /// <summary>How many function dictionaries one render reads.</summary>
    internal const int MaximumReads = 4096;

    // Each function read, with how deep it nests below itself: a function read where it nests
    // within the limit may be named again where it would not. Null for one that could not be,
    // with the problem and the reason it was reported with.
    private readonly Dictionary<object, (PdfFunction Function, int Height, (PageRenderProblem Problem, string Message) Failure)> _read =
        new(ReferenceIdentity.Instance);

    // The functions being read, from the outermost in: one named again among them is made of itself.
    private readonly HashSet<object> _active = new(ReferenceIdentity.Instance);
    private int _reads;

    /// <summary>
    /// What a <c>/Function</c> entry describes: one function, or an array of functions of one
    /// output each, read as one function of as many outputs.
    /// </summary>
    /// <returns>The function, or null where it cannot be read, which is reported.</returns>
    internal PdfFunction Read(PdfItem definition, string subject) => Read(definition, 0, subject, out _);

    private PdfFunction Read(PdfItem definition, int depth, string subject, out int height)
    {
        height = 0;
        definition = PdfReference.Dereference(definition);
        if (depth > PdfFunction.MaximumDepth)
            return TooDeep(subject);

        if (definition is PdfArray array)
            return ReadGroup(array, depth, subject, out height);

        if (definition is not PdfDictionary dictionary)
            return PdfFunction.Invalid(sink, subject, "A function is a dictionary, a stream or an array of them.");

        var key = ReferenceIdentity.KeyOf(dictionary);
        if (_read.TryGetValue(key, out var known))
        {
            if (known.Function == null)
            {
                sink.Report(known.Failure.Problem, subject, known.Failure.Message);
                return null;
            }

            if (depth + known.Height > PdfFunction.MaximumDepth)
                return TooDeep(subject);

            height = known.Height;
            return known.Function;
        }

        if (!_active.Add(key))
        {
            sink.Report(PageRenderProblem.LimitReached, subject,
                "A function is made of itself. What it colours is not drawn.");
            return null;
        }

        try
        {
            if (++_reads > MaximumReads)
            {
                sink.Report(PageRenderProblem.LimitReached, subject,
                    "The page has more functions than are read. What they colour is not drawn.");
                return null;
            }

            var function = ReadDictionary(dictionary, depth, subject, out height);

            // A stitching that fails may fail for where it was named, too deep or inside itself,
            // and is read again where it is named next. What it is made of is kept either way.
            // Any other that fails reported why last, as every read that answers null does.
            if (function != null || !IsStitching(dictionary))
                _read.Add(key, (function, height, function == null ? sink.Last : default));
            return function;
        }
        finally
        {
            _active.Remove(key);
        }
    }

    private PdfFunction ReadDictionary(PdfDictionary dictionary, int depth, string subject, out int height)
    {
        height = 0;
        var elements = dictionary.Elements;
        var domain = PdfFunction.Intervals(elements["/Domain"]);
        if (domain == null || domain.Length == 0 || domain.Length / 2 > PdfFunction.MaximumArity)
            return PdfFunction.Invalid(sink, subject, "A function's /Domain is a low and a high for each input.");

        var range = PdfFunction.Intervals(elements["/Range"]);
        if (range == null && !PdfItemValues.IsNull(elements["/Range"]) || range?.Length / 2 > PdfFunction.MaximumArity)
            return PdfFunction.Invalid(sink, subject, "A function's /Range is a low and a high for each output.");

        if (!PdfItemValues.TryGetInteger(elements["/FunctionType"], out var type))
            type = -1;

        return type switch
        {
            0 => SampledFunction.Read(dictionary, domain, range, work, sink, subject),
            2 => ExponentialFunction.Read(dictionary, domain, range, sink, subject),
            3 => ReadStitching(dictionary, domain, range, depth, subject, out height),
            4 => CalculatorFunction.Read(dictionary, domain, range, work, sink, subject),
            _ => PdfFunction.Invalid(sink, subject, "A function's /FunctionType is 0, 2, 3 or 4.")
        };
    }

    private PdfFunction ReadGroup(PdfArray array, int depth, string subject, out int height)
    {
        height = 0;
        var count = array.Elements.Count;
        if (count == 0 || count > PdfFunction.MaximumArity)
            return PdfFunction.Invalid(sink, subject, "An array of functions holds one for each colour component.");

        var functions = new PdfFunction[count];
        for (var index = 0; index < count; index++)
        {
            // An array inside an array is no function, and would nest without end.
            if (PdfReference.Dereference(array.Elements[index]) is PdfArray)
                return PdfFunction.Invalid(sink, subject, "An array of functions holds functions.");

            functions[index] = Read(array.Elements[index], depth + 1, subject, out var below);
            if (functions[index] == null)
                return null;

            if (functions[index].OutputCount != 1 || functions[index].InputCount != functions[0].InputCount)
                return PdfFunction.Invalid(sink, subject, "Each function of an array has one output and the same inputs.");

            height = Math.Max(height, below + 1);
        }

        return count == 1 ? functions[0] : new FunctionGroup(functions);
    }

    private PdfFunction ReadStitching(PdfDictionary dictionary, double[] domain, double[] range, int depth,
        string subject, out int height)
    {
        height = 0;
        var elements = dictionary.Elements;
        if (domain.Length != 2 || PdfReference.Dereference(elements["/Functions"]) is not PdfArray pieces
                               || pieces.Elements.Count == 0 || pieces.Elements.Count > PdfFunction.MaximumBreakpoints)
        {
            return PdfFunction.Invalid(sink, subject, "A stitching function has one input and an array of /Functions.");
        }

        var count = pieces.Elements.Count;
        var bounds = PdfFunction.Numbers(elements["/Bounds"]);
        var encode = PdfFunction.Numbers(elements["/Encode"]);
        if (bounds == null || bounds.Length != count - 1 || encode == null || encode.Length != 2 * count)
            return PdfFunction.Invalid(sink, subject, "A stitching function has a bound between each two functions and an /Encode pair for each.");

        var edges = new double[count + 1];
        edges[0] = domain[0];
        edges[count] = domain[1];
        bounds.CopyTo(edges, 1);
        for (var index = 1; index <= count; index++)
        {
            if (edges[index] < edges[index - 1])
                return PdfFunction.Invalid(sink, subject, "The /Bounds of a stitching function rise, inside its /Domain.");
        }

        var functions = new PdfFunction[count];
        for (var index = 0; index < count; index++)
        {
            functions[index] = Read(pieces.Elements[index], depth + 1, subject, out var below);
            if (functions[index] == null)
                return null;

            if (functions[index].InputCount != 1 || functions[index].OutputCount != functions[0].OutputCount)
                return PdfFunction.Invalid(sink, subject, "Each function of a stitching has one input and the same outputs.");

            height = Math.Max(height, below + 1);
        }

        if (range != null && range.Length != 2 * functions[0].OutputCount)
            return PdfFunction.Invalid(sink, subject, "A function's /Range is a low and a high for each output.");

        return new StitchingFunction(domain, range, functions, edges, encode);
    }

    private static bool IsStitching(PdfDictionary dictionary) =>
        PdfItemValues.TryGetInteger(dictionary.Elements["/FunctionType"], out var type) && type == 3;

    private PdfFunction TooDeep(string subject)
    {
        sink.Report(PageRenderProblem.LimitReached, subject,
            "Functions are nested too deep. What they colour is not drawn.");
        return null;
    }
}
