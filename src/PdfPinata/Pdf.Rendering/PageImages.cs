using System;
using PdfPinata.Internal;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.Filters;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Turns an image XObject or an inline image into the samples a device draws. The interpreter
/// already calls both methods and draws what they answer, so decoding images is filling these two
/// in and nothing in the interpreter.
/// </summary>
/// <remarks>
/// What they answer is finished: <see cref="PageImageFormat.Alpha8"/> for an <c>/ImageMask</c>,
/// which the interpreter paints in the fill colour, and otherwise
/// <see cref="PageImageFormat.Rgba32"/> with the colour space, the <c>/Decode</c> array, the
/// <c>/Mask</c> and the <c>/SMask</c> applied. A <c>/DCTDecode</c> stream is handed to
/// <see cref="PageDevice.DecodeImage"/>, because this library carries no imaging dependency. What
/// the device answers is RGB already, and goes through the same masks as any other image. An
/// image's colour space is resolved through <see cref="ColorSpaces.Resolve"/>.
/// </remarks>
internal static class PageImages
{
    /// <summary>
    /// The most samples an image may have, checked before anything is decoded: 2^28, whose
    /// <see cref="PageImageFormat.Rgba32"/> buffer is 1 GiB. A page of A0 scanned at 400 dots to
    /// the inch is 248 million samples and is drawn. Past this a <c>/Width</c> and
    /// <c>/Height</c> a few bytes long would ask for gigabytes, and soon for more than one .NET
    /// array holds.
    /// </summary>
    internal const long MaximumSamples = 1L << 28;

    /// <summary>
    /// The most an image is scaled up to meet its mask, as a multiple of the larger of the image's
    /// and the mask's own sample counts. The drawn size is the larger width by the larger height,
    /// so it equals the mask's own count whenever the mask is at least as wide and as tall as the
    /// image, and a scan's fine mask of text over a coarse colour image is never capped by this.
    /// It bites only when the two cross, one wider and the other taller: a thin image under a
    /// thin mask across it, 16384 by 1 under 1 by 16384, would otherwise become 16384 by 16384,
    /// a gigabyte and a quarter from a few kilobytes, with detail no file had. The limit on the
    /// drawn samples overall is <see cref="MaximumSamples"/>.
    /// </summary>
    internal const long MaximumScaleUp = 64;

    /// <summary>
    /// The samples of an image XObject, or null when it cannot be decoded, having reported why.
    /// </summary>
    /// <param name="image">The image dictionary and its stream.</param>
    /// <param name="name">The name the content draws it by, which is what a report is about.</param>
    /// <param name="resources">For a colour space the image names.</param>
    /// <param name="device">Asked to decode what this library cannot.</param>
    /// <param name="sink">Where a failure is reported.</param>
    internal static PageImage ReadXObject(PdfDictionary image, string name, ResourceScope resources,
        PageDevice device, DiagnosticSink sink)
    {
        var reader = new ImageReader(name, resources, device, sink);
        return reader.Read(image, reader.DataOf(image, "The image"));
    }

    /// <summary>
    /// Whether an image's <c>/ColorSpace</c> is a name looked up in the resources, so that what it
    /// draws depends on the resources it is drawn with and not on the image alone.
    /// </summary>
    internal static bool NamesAResourceSpace(PdfDictionary image) =>
        PdfItemValues.TryGetName(PdfReference.Dereference(image.Elements["/ColorSpace"]), out var name)
        && name is not ("/DeviceGray" or "/DeviceRGB" or "/DeviceCMYK");

    /// <summary>
    /// The samples of an inline image, or null when it cannot be decoded, having reported why.
    /// </summary>
    /// <param name="image">The image, its dictionary as the text between <c>BI</c> and <c>ID</c>.</param>
    /// <param name="resources">For a colour space the image names.</param>
    /// <param name="device">Asked to decode what this library cannot.</param>
    /// <param name="sink">Where a failure is reported.</param>
    internal static PageImage ReadInline(CInlineImage image, ResourceScope resources, PageDevice device,
        DiagnosticSink sink)
    {
        var dictionary = InlineImageDictionary.Read(image.ImageDictionary);
        if (dictionary == null)
        {
            sink.Report(PageRenderProblem.InvalidValue, "BI", "The inline image's dictionary cannot be read.");
            return null;
        }

        return new ImageReader("BI", resources, device, sink).Read(dictionary, image.Data);
    }

    /// <summary>One image read for one draw, and what its reports are about.</summary>
    private sealed class ImageReader(string subject, ResourceScope resources, PageDevice device, DiagnosticSink sink)
    {
        internal byte[] DataOf(PdfDictionary image, string what)
        {
            if (image.Stream?.Value is { } data)
                return data;

            Report(PageRenderProblem.InvalidValue, what + " has no data.");
            return null;
        }

        internal PageImage Read(PdfDictionary image, byte[] encoded)
        {
            if (encoded == null || !TryReadSize(image, "The image", out var width, out var height))
                return null;

            byte[] data;
            bool isJpeg;
            if (PdfItemValues.TryGetBoolean(image.Elements["/ImageMask"], out var isStencil) && isStencil)
            {
                return TryUnfilter(image, encoded, ImageSamples.LengthOf(width, height, 1, 1), "The image", out data, out isJpeg)
                       && ReadStencil(image, data, isJpeg, width, height, "The stencil mask") is { } coverage
                    ? PageImage.FromAlpha8(width, height, coverage)
                    : null;
            }

            // The colour space is read first because the number of its components is what bounds
            // the filters' output.
            var space = ColorSpaceOf(image, out var isIndexed);
            if (space == null
                || !TryUnfilter(image, encoded, ImageSamples.LengthOf(width, height, space.ComponentCount, DeclaredBits(image)),
                    "The image", out data, out isJpeg))
                return null;

            byte[] rgba;
            if (isJpeg)
            {
                rgba = ReadJpeg(image, data, space.ComponentCount, ref width, ref height);
            }
            else
            {
                if (!TryReadBits(image, "The image", out var bits))
                    return null;

                var samples = new ImageSamples(data, width, height, space.ComponentCount, bits);
                if (IsMostlyMissing(samples, "The image"))
                    return null;

                rgba = Colourise(samples, space, DecodeOf(image, samples.Components, bits, isIndexed),
                    ColourKeyOf(image, samples.Components));
            }

            return rgba == null ? null : Masked(image, rgba, width, height, space);
        }

        // ----- The dictionary ------------------------------------------------------------------

        private bool TryReadSize(PdfDictionary image, string what, out int width, out int height)
        {
            if (!TryGetCount(image.Elements["/Width"], out width) || !TryGetCount(image.Elements["/Height"], out height))
            {
                Report(PageRenderProblem.InvalidValue, what + " has no /Width and /Height of at least 1.");
                width = height = 0;
                return false;
            }

            if ((long)width * height > MaximumSamples)
            {
                Report(PageRenderProblem.LimitReached,
                    $"{what} is {width} by {height} samples, more than the {MaximumSamples} drawn.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// A size of at least 1, which may be written as a real if it is whole: <c>2.0</c> is 2.
        /// </summary>
        private static bool TryGetCount(PdfItem item, out int count)
        {
            count = 0;
#pragma warning disable S1244 // Exact on purpose: a size is whole or it is no size.
            if (!PdfItemValues.TryGetNumber(item, out var number) || number < 1 || number > int.MaxValue
                || Math.Floor(number) != number)
                return false;
#pragma warning restore S1244

            count = (int)number;
            return true;
        }

        /// <summary>
        /// The <c>/BitsPerComponent</c> to bound the filters by: the one declared, or 16 where it is
        /// missing or wrong, which is reported later if the samples need it.
        /// </summary>
        private static int DeclaredBits(PdfDictionary image) =>
            PdfItemValues.TryGetInteger(image.Elements["/BitsPerComponent"], out var bits) && bits is 1 or 2 or 4 or 8 or 16
                ? bits
                : 16;

        private bool TryReadBits(PdfDictionary image, string what, out int bits)
        {
            if (PdfItemValues.TryGetInteger(image.Elements["/BitsPerComponent"], out bits) && bits is 1 or 2 or 4 or 8 or 16)
                return true;

            Report(PageRenderProblem.InvalidValue, what + "'s /BitsPerComponent is not 1, 2, 4, 8 or 16.");
            return false;
        }

        /// <summary>
        /// The colour space, or null when it cannot be drawn in, having reported why. An image in a
        /// space that is not resolved yet is not drawn: a picture filled in with a fallback colour
        /// hides more of the page than leaving it out does.
        /// </summary>
        private RenderColorSpace ColorSpaceOf(PdfDictionary image, out bool isIndexed)
        {
            isIndexed = false;
            var definition = PdfReference.Dereference(image.Elements["/ColorSpace"]);
            if (PdfItemValues.IsNull(definition))
            {
                // A JPX image may leave its colour space to the codestream (ISO 32000-1 8.9.5).
                if (Array.Exists(FiltersOf(image), filter => PdfItemValues.TryGetName(filter, out var name) && name == "/JPXDecode"))
                    ReportNotDecodedYet("The image", "/JPXDecode");
                else
                    Report(PageRenderProblem.InvalidValue, "The image has no /ColorSpace.");
                return null;
            }

            // An inline image names a space of the resources by its name, and an XObject may too.
            if (PdfItemValues.TryGetName(definition, out var name)
                && name is not ("/DeviceGray" or "/DeviceRGB" or "/DeviceCMYK"))
            {
                definition = resources.LookupItem("/ColorSpace", name);
                if (definition == null)
                {
                    sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no colour space of that name.");
                    return null;
                }
            }

            isIndexed = definition is PdfArray array && array.Elements.Count > 0
                                                     && PdfItemValues.TryGetName(array.Elements[0], out var family)
                                                     && family == "/Indexed";

            var space = ColorSpaces.Resolve(definition, sink);
            if (space is UnsupportedColorSpace || space.ComponentCount < 1)
            {
                Report(PageRenderProblem.UnsupportedImage, "The image's colour space is not resolved yet, so it is not drawn.");
                return null;
            }

            return space;
        }

        /// <summary>
        /// The <c>/Decode</c> array as a minimum and a maximum for each component: what the
        /// smallest and the largest sample stand for. An indexed image's default is the whole
        /// range of indices, and every other's is 0 to 1.
        /// </summary>
        private double[] DecodeOf(PdfDictionary image, int components, int bits, bool isIndexed)
        {
            var decode = new double[components * 2];
            if (TryReadNumbers(image.Elements["/Decode"], decode))
                return decode;

            if (!PdfItemValues.IsNull(image.Elements["/Decode"]))
                Report(PageRenderProblem.InvalidValue, "The image's /Decode is not two numbers for each component, and is not applied.");

            // A /Decode that failed part way has filled some of the array, which the default replaces whole.
            decode = new double[components * 2];
            for (var component = 0; component < components; component++)
                decode[component * 2 + 1] = isIndexed ? (1 << bits) - 1 : 1;
            return decode;
        }

        /// <summary>A <c>/Mask</c> of ranges of samples: a minimum and a maximum for each component.</summary>
        private int[] ColourKeyOf(PdfDictionary image, int components)
        {
            // An /SMask overrides a /Mask (ISO 32000-1 11.6.5.3).
            if (PdfReference.Dereference(image.Elements["/Mask"]) is not PdfArray array
                || PdfReference.Dereference(image.Elements["/SMask"]) is PdfDictionary)
                return null;

            var ranges = new int[components * 2];
            for (var index = 0; index < ranges.Length; index++)
            {
                if (array.Elements.Count != ranges.Length
                    || !PdfItemValues.TryGetNumber(array.Elements[index], out var bound))
                {
                    Report(PageRenderProblem.InvalidValue, "The image's /Mask is not two numbers for each component, and is not applied.");
                    return null;
                }

                ranges[index] = (int)Math.Round(Math.Max(Math.Min(bound, int.MaxValue), int.MinValue));
            }

            return ranges;
        }

        private static bool TryReadNumbers(PdfItem item, double[] numbers)
        {
            if (PdfReference.Dereference(item) is not PdfArray array || array.Elements.Count != numbers.Length)
                return false;

            for (var index = 0; index < numbers.Length; index++)
            {
                if (!PdfItemValues.TryGetNumber(array.Elements[index], out numbers[index]) || !double.IsFinite(numbers[index]))
                    return false;
            }

            return true;
        }

        // ----- Filters -------------------------------------------------------------------------

        /// <summary>
        /// How much more than its samples a filter before the last may produce, beyond twice them:
        /// what a JPEG's headers and profiles add to a small image.
        /// </summary>
        private const long EncodedAllowance = 16L << 20;

        /// <summary>
        /// Undoes every filter but <c>/DCTDecode</c>, which may only be the last and is left for
        /// the device: <paramref name="isJpeg"/> says the data is a JPEG file still.
        /// </summary>
        /// <remarks>
        /// No filter produces much more than the image can use, because Flate and LZW do not say
        /// how much they will. The filter that produces the samples stops at
        /// <paramref name="sampleBytes"/>, rounded up to the predictor's whole rows and the byte in
        /// front of each a PNG predictor reads, and what the stream holds beyond that is not drawn
        /// anyway. A filter before it
        /// produces encoded data, which can be larger than what it decodes to: RunLength writes a
        /// byte more than it was given for every 128, and two for one at worst, and a JPEG of a
        /// few samples is mostly headers. So each of those may produce twice the samples and
        /// <see cref="EncodedAllowance"/> more, and one that would produce more is reported and
        /// the image not drawn.
        /// </remarks>
        private bool TryUnfilter(PdfDictionary image, byte[] data, long sampleBytes, string what, out byte[] unfiltered,
            out bool isJpeg)
        {
            unfiltered = data;
            isJpeg = false;

            var filters = FiltersOf(image);
            var parameters = filters.Length == 0 || PdfItemValues.IsNull(image.Elements["/DecodeParms"])
                ? new PdfItem[filters.Length]
                : Filtering.ParametersForEach(filters.Length, PdfReference.Dereference(image.Elements["/DecodeParms"]));
            if (parameters == null)
            {
                Report(PageRenderProblem.InvalidValue, what + "'s /DecodeParms cannot be matched to its filters.");
                return false;
            }

            for (var index = 0; index < filters.Length; index++)
            {
                if (!PdfItemValues.TryGetName(filters[index], out var name))
                {
                    Report(PageRenderProblem.InvalidValue, what + "'s /Filter is not a name or an array of names.");
                    return false;
                }

                switch (name)
                {
                    case "/DCTDecode" or "/DCT" when index == filters.Length - 1:
                        // The device is handed the JPEG alone, so a /ColorTransform that overrides
                        // the one the JPEG implies cannot reach it.
                        if (parameters[index] is PdfDictionary dct && !PdfItemValues.IsNull(dct.Elements["/ColorTransform"]))
                            Report(PageRenderProblem.UnsupportedFeature, what + "'s /ColorTransform is not applied. The JPEG is decoded as its own markers say.");
                        isJpeg = true;
                        return true;

                    case "/DCTDecode" or "/DCT":
                        Report(PageRenderProblem.UnsupportedImage, what + " is filtered after /DCTDecode, which a JPEG decoder cannot read.");
                        return false;

                    case "/CCITTFaxDecode" or "/CCF":
                        // A last fax filter decodes the image's /Height in rows, whatever /Rows says.
                        if (!TryReadSize(image, what, out var width, out var height)
                            || !TryDecodeFax(unfiltered, parameters[index] as PdfDictionary, width,
                                index == filters.Length - 1 ? height : null, what, out unfiltered))
                            return false;
                        continue;

                    case "/JBIG2Decode" or "/JPXDecode":
                        ReportNotDecodedYet(what, name);
                        return false;
                }

                // The first filter is handed a copy: /ASCII85Decode compacts the array it is given
                // in place, and the first is the stream's own, which a later render reads again.
                var input = index == 0 ? (byte[])unfiltered.Clone() : unfiltered;
                var decodeParms = parameters[index] as PdfDictionary;
                var producesSamples = index == filters.Length - 1;
                var limit = new DecodeLimit(producesSamples
                    ? WithPredictorBytes(name, sampleBytes, decodeParms)
                    : EarlierFilterBytes(sampleBytes)) { TolerateDamage = true };
                if (!TryApply(name, input, new FilterParms(decodeParms, limit), what, out unfiltered))
                    return false;

                // The last filter's output is the samples, so its report also says what is drawn of
                // them: the shortfall is reported under the same problem, and only the first stays.
                if (limit.Damaged)
                    Report(PageRenderProblem.InvalidValue, $"{what}'s {name} data is damaged, and is read only as far as the damage."
                        + (producesSamples ? Shortfall(unfiltered.Length, sampleBytes) : null));

                if (limit.Truncated && !producesSamples)
                {
                    Report(PageRenderProblem.LimitReached,
                        $"{what}'s {name} data decodes to more than the {limit.MaximumLength} bytes its size can need, and is not drawn.");
                    return false;
                }
            }

            return true;
        }

        /// <summary>The image's <c>/Filter</c> as a list of its entries, which need not be names.</summary>
        private static PdfItem[] FiltersOf(PdfDictionary image)
        {
            var filter = PdfReference.Dereference(image.Elements["/Filter"]);
            if (PdfItemValues.IsNull(filter))
                return [];
            return filter is PdfArray array ? [.. array.Elements] : [filter];
        }

        /// <summary>
        /// What a filter produces before its predictor is undone, for <paramref name="sampleBytes"/>
        /// after: a TIFF or PNG predictor works in whole rows of its own <c>/Columns</c>, and a PNG
        /// one adds a byte in front of each. Only Flate and LZW undo a predictor.
        /// </summary>
        /// <remarks>
        /// The cap always covers one whole row, however much wider than the image a row is declared,
        /// because a predictor drops a part row and would leave nothing to draw. It is held to what
        /// an earlier filter may produce, so that a <c>/Columns</c> a few bytes long cannot lift it
        /// to whatever it names. A value that is not a positive integer is read as its default here,
        /// and refused by the filter itself, where the refusal is reported.
        /// </remarks>
        private static long WithPredictorBytes(string filter, long sampleBytes, PdfDictionary decodeParms)
        {
            var predictor = PositiveOrDefault(decodeParms, "/Predictor", 1);
            if (predictor < 2 || filter is not ("/FlateDecode" or "/Fl" or "/LZWDecode" or "/LZW"))
                return sampleBytes;

            var bits = (double)PositiveOrDefault(decodeParms, "/Columns", 1)
                       * PositiveOrDefault(decodeParms, "/Colors", 1)
                       * PositiveOrDefault(decodeParms, "/BitsPerComponent", 8);
            var tagBytes = predictor >= 10 ? 1 : 0;
            var bound = EarlierFilterBytes(sampleBytes);
            var rowBytes = (long)Math.Min(Math.Ceiling(bits / 8), bound);
            var rows = (sampleBytes + rowBytes - 1) / rowBytes;
            return Math.Min(rows * (rowBytes + tagBytes), bound);
        }

        /// <summary>What a filter before the last may produce for <paramref name="sampleBytes"/>.</summary>
        private static long EarlierFilterBytes(long sampleBytes) => 2 * sampleBytes + EncodedAllowance;

        private static int PositiveOrDefault(PdfDictionary decodeParms, string key, int defaultValue) =>
            decodeParms != null && PdfItemValues.TryGetInteger(decodeParms.Elements[key], out var value) && value >= 1
                ? value
                : defaultValue;

        /// <summary>
        /// The widest <c>/Columns</c> drawn for an image of <paramref name="width"/> pixels: twice
        /// the width, and never under 65536, which no fax is. Viewers decode by <c>/Columns</c> and
        /// not by the width, so a mismatch within this is drawn as they draw it; a 1 pixel image
        /// that names 2 billion columns is not decoded into a 256 MiB buffer for them.
        /// </summary>
        private static long MostFaxColumns(int width) => Math.Max(1L << 16, 2L * width);

        /// <summary>
        /// Facsimile data decoded to <paramref name="rows"/> rows, or to those <c>/Rows</c> says
        /// where that is null. The rows past where damaged or short data stops are white, which is
        /// reported, and the image is drawn.
        /// </summary>
        private bool TryDecodeFax(byte[] data, PdfDictionary parameters, int width, int? rows, string what, out byte[] decoded)
        {
            decoded = null;
            try
            {
                var faxParameters = CcittFaxParameters.Read(parameters);
                if (rows != null && faxParameters.Columns > MostFaxColumns(width))
                {
                    Report(PageRenderProblem.InvalidValue,
                        $"{what}'s /CCITTFaxDecode /Columns of {faxParameters.Columns} is far wider than the image's {width} pixels.");
                    return false;
                }

                var result = CcittFaxDecoder.Decode(data, faxParameters, rows ?? faxParameters.Rows);
                if (result.Problem != null)
                    Report(PageRenderProblem.InvalidValue, $"{what}'s /CCITTFaxDecode data {result.Problem}");
                decoded = result.Data;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Report(PageRenderProblem.InvalidValue, $"{what}'s /CCITTFaxDecode data cannot be decoded: {ex.Message}");
                return false;
            }
        }

        private void ReportNotDecodedYet(string what, string filter) =>
            Report(PageRenderProblem.UnsupportedImage, $"{what} is encoded with {filter}, which is not decoded yet.");

        private bool TryApply(string name, byte[] data, FilterParms parameters, string what, out byte[] decoded)
        {
            decoded = null;
            Filter filter;
            try
            {
                filter = Filtering.GetFilter(name);
            }
            catch (NotImplementedException)
            {
                filter = null;
            }

            try
            {
                decoded = filter?.Decode(data, parameters);
            }
            catch (Exception ex) when (!Unrecoverable.Is(ex))
            {
                Report(PageRenderProblem.UnsupportedImage, $"{what}'s {name} data cannot be decoded: {ex.Message}");
                return false;
            }

            if (decoded == null)
                Report(PageRenderProblem.UnsupportedImage, $"{what} is encoded with {name}, which is not decoded.");
            return decoded != null;
        }

        // ----- Samples to colour ---------------------------------------------------------------

        /// <summary>
        /// The samples as RGBA: each through <c>/Decode</c> to the components it stands for, and
        /// through the colour space to RGB. A sample whose every component falls in the colour
        /// key is transparent.
        /// </summary>
        private static byte[] Colourise(ImageSamples samples, RenderColorSpace space, double[] decode, int[] colourKey)
        {
            var count = samples.Components;
            var max = samples.MaxValue;
            var raw = new int[count];
            var components = new double[count];
            var rgba = new byte[(long)samples.Width * samples.Height * 4];

            // One component of up to 8 bits has at most 256 colours, which are worked out once.
            var table = count == 1 && samples.BitsPerComponent <= 8 ? new byte[(max + 1) * 3] : null;
            if (table != null)
            {
                for (var value = 0; value <= max; value++)
                {
                    components[0] = decode[0] + value * (decode[1] - decode[0]) / max;
                    Write(space.ToRgb(components), table, value * 3);
                }
            }

            var at = 0L;
            for (var y = 0; y < samples.Height; y++)
            {
                for (var x = 0; x < samples.Width; x++, at += 4)
                {
                    for (var component = 0; component < count; component++)
                        raw[component] = samples.Read(x, y, component);

                    if (table != null)
                    {
                        Array.Copy(table, raw[0] * 3, rgba, at, 3);
                    }
                    else
                    {
                        for (var component = 0; component < count; component++)
                            components[component] = decode[component * 2] + raw[component] * (decode[component * 2 + 1] - decode[component * 2]) / max;
                        Write(space.ToRgb(components), rgba, at);
                    }

                    rgba[at + 3] = colourKey != null && IsKeyedOut(raw, colourKey) ? (byte)0 : (byte)255;
                }
            }

            return rgba;
        }

        private static bool IsKeyedOut(ReadOnlySpan<int> samples, int[] ranges)
        {
            for (var component = 0; component < samples.Length; component++)
            {
                if (samples[component] < ranges[component * 2] || samples[component] > ranges[component * 2 + 1])
                    return false;
            }

            return true;
        }

        private static void Write(PagePaint colour, byte[] to, long at)
        {
            to[at] = ToByte(colour.Red);
            to[at + 1] = ToByte(colour.Green);
            to[at + 2] = ToByte(colour.Blue);
        }

        private static byte ToByte(double unit) => (byte)Math.Round(Math.Max(0, Math.Min(1, unit)) * 255);

        // ----- JPEG ----------------------------------------------------------------------------

        /// <summary>
        /// A JPEG decoded by the device, with the image's <c>/Decode</c> and colour key applied to
        /// the RGB it answers where that is exact: an image of one or of three components, whose
        /// RGB is its components. Its size is the JPEG's own.
        /// </summary>
        private byte[] ReadJpeg(PdfDictionary image, byte[] jpeg, int components, ref int width, ref int height)
        {
            if (DecodeJpeg(jpeg, "The image") is not { } decoded)
                return null;

            width = decoded.Width;
            height = decoded.Height;
            var rgba = decoded.Data.ToArray();

            var decode = new double[components * 2];
            var isRead = TryReadNumbers(image.Elements["/Decode"], decode);
            if (!isRead && !PdfItemValues.IsNull(image.Elements["/Decode"]))
                Report(PageRenderProblem.InvalidValue, "The image's /Decode is not two numbers for each component, and is not applied.");

            if (components is not (1 or 3))
            {
                ReportWhereFourComponentsDiffer(jpeg, isRead ? decode : null, PdfReference.Dereference(image.Elements["/Mask"]) is PdfArray);
                return rgba;
            }

            var hasDecode = isRead && !IsDefault(decode);

            var colourKey = ColourKeyOf(image, components);

            var channels = new byte[3][];
            for (var channel = 0; channel < 3; channel++)
            {
                var component = components == 1 ? 0 : channel;
                channels[channel] = new byte[256];
                for (var value = 0; value < 256; value++)
                {
                    channels[channel][value] = hasDecode
                        ? ToByte(decode[component * 2] + value / 255.0 * (decode[component * 2 + 1] - decode[component * 2]))
                        : (byte)value;
                }
            }

            Span<int> samples = stackalloc int[components];
            for (var at = 0L; at < rgba.Length; at += 4)
            {
                for (var component = 0; component < components; component++)
                    samples[component] = rgba[at + component];

                for (var channel = 0; channel < 3; channel++)
                    rgba[at + channel] = channels[channel][rgba[at + channel]];

                if (colourKey != null && IsKeyedOut(samples, colourKey))
                    rgba[at + 3] = 0;
            }

            return rgba;
        }

        /// <summary>
        /// A JPEG as the device decodes it, or null when it did not or answered more samples than
        /// <see cref="MaximumSamples"/>: the size inside a JPEG need not be the size its dictionary
        /// was checked by.
        /// </summary>
        private PageImage DecodeJpeg(byte[] jpeg, string what)
        {
            var decoded = device.DecodeImage(PageImageCodec.Jpeg, jpeg);
            if (decoded == null || decoded.Format != PageImageFormat.Rgba32)
            {
                Report(PageRenderProblem.UnsupportedImage, what + " is a JPEG, and the device did not decode it.");
                return null;
            }

            if ((long)decoded.Width * decoded.Height > MaximumSamples)
            {
                Report(PageRenderProblem.LimitReached,
                    $"{what} is a JPEG of {decoded.Width} by {decoded.Height} samples, more than the {MaximumSamples} drawn.");
                return null;
            }

            return decoded;
        }

        /// <summary>
        /// Reports where what a device answers for a JPEG of four components, whose inks it has
        /// turned to RGB already, cannot be what the image's <c>/Decode</c> and colour key say.
        /// </summary>
        /// <remarks>
        /// A codec reads an Adobe marker as saying the inks are stored inverted, as Photoshop
        /// stores them, and undoes that (<see cref="PageDevice.DecodeImage"/>). A PDF reads the
        /// samples as they are stored, so a file that embeds such a JPEG says
        /// <c>/Decode [1 0 1 0 1 0 1 0]</c> to invert them back, and the device's answer is then
        /// exactly what the file means. Where only one of the two is there, the device's answer is
        /// the inverse of it, and any other <c>/Decode</c> cannot be applied to RGB at all.
        /// </remarks>
        /// <param name="jpeg">The JPEG, for its markers.</param>
        /// <param name="decode">The <c>/Decode</c> array, or null where it is the default.</param>
        /// <param name="hasColourKey">Whether the image's <c>/Mask</c> is a colour key.</param>
        private void ReportWhereFourComponentsDiffer(byte[] jpeg, double[] decode, bool hasColourKey)
        {
            if (hasColourKey)
                Report(PageRenderProblem.UnsupportedFeature, "The colour key of a JPEG of four components is not applied, because the device answers its RGB.");

            var isInverted = decode != null && IsInverted(decode);
            if (decode != null && !isInverted && !IsDefault(decode))
            {
                Report(PageRenderProblem.UnsupportedFeature, "The /Decode of a JPEG of four components is not applied, because the device answers its RGB.");
                return;
            }

            var hasAdobeMarker = HasAdobeMarker(jpeg);
            if (hasAdobeMarker && !isInverted)
                Report(PageRenderProblem.UnsupportedFeature, "The JPEG of four components has an Adobe marker and no /Decode that inverts it. The device undoes the inversion the marker stands for, and the file does not, so its inks are drawn the inverse of what the file says.");
            else if (!hasAdobeMarker && isInverted)
                Report(PageRenderProblem.UnsupportedFeature, "The JPEG of four components has a /Decode that inverts it and no Adobe marker. The device answers the inks as they are stored, so they are drawn the inverse of what the file says.");
        }

        /// <summary>
        /// Whether a JPEG has an Adobe <c>APP14</c> marker among those before its first scan. Each
        /// marker is stepped over by its length, so this reads a few bytes of each and no data.
        /// </summary>
        private static bool HasAdobeMarker(ReadOnlySpan<byte> jpeg)
        {
            if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
                return false;

            var at = 2;
            while (at + 4 <= jpeg.Length && jpeg[at] == 0xFF)
            {
                var marker = jpeg[at + 1];
                if (marker == 0xFF)
                {
                    // A fill byte before the marker.
                    at++;
                    continue;
                }

                if (marker is 0xDA or 0xD9)
                    return false;

                var length = (jpeg[at + 2] << 8) | jpeg[at + 3];
                if (length < 2)
                    return false;

                if (marker == 0xEE && length >= 7 && at + 9 <= jpeg.Length && jpeg.Slice(at + 4, 5).SequenceEqual("Adobe"u8))
                    return true;

                at += 2 + length;
            }

            return false;
        }

        private static bool IsDefault(double[] decode) => IsEach(decode, 0, 1);

        private static bool IsInverted(double[] decode) => IsEach(decode, 1, 0);

        private static bool IsEach(double[] decode, int minimum, int maximum)
        {
            for (var index = 0; index < decode.Length; index++)
            {
#pragma warning disable S1244 // Exact on purpose: only the numbers 0 and 1 written out are the default.
                if (decode[index] != (index % 2 == 0 ? minimum : maximum))
                    return false;
#pragma warning restore S1244
            }

            return true;
        }

        // ----- Masks ---------------------------------------------------------------------------

        /// <summary>
        /// The image with an <c>/SMask</c> or, where there is none, a <c>/Mask</c> that is a stencil
        /// multiplied into its alpha. A mask that cannot be read is reported, and the image is drawn
        /// without it.
        /// </summary>
        /// <remarks>
        /// Where the mask is finer than the image, the image is scaled up to the mask by the nearest
        /// of its samples, so that the mask keeps its own detail. A scan split into layers puts a
        /// coarse colour image under a fine mask of the text, and thinning the mask to the image's
        /// size would draw the text as blocks. A mask coarser than the image is stretched over it.
        /// Neither grows the image past <see cref="MaximumSamples"/>, nor past
        /// <see cref="MaximumScaleUp"/> times the larger of the image's and the mask's own sample
        /// counts: beyond either, the mask is made the size of the image and the loss of its detail
        /// is reported as <see cref="PageRenderProblem.LimitReached"/>.
        /// </remarks>
        private PageImage Masked(PdfDictionary image, byte[] rgba, int width, int height, RenderColorSpace space)
        {
            var softMask = PdfReference.Dereference(image.Elements["/SMask"]) as PdfDictionary;
            int maskWidth = 0, maskHeight = 0;
            byte[] alpha;
            if (softMask != null)
            {
                alpha = ReadSoftMask(softMask, out maskWidth, out maskHeight);
            }
            else if (PdfReference.Dereference(image.Elements["/Mask"]) is PdfDictionary stencil)
            {
                alpha = DataOf(stencil, "The /Mask") is { } encoded
                        && TryReadSize(stencil, "The /Mask", out maskWidth, out maskHeight)
                        && TryUnfilter(stencil, encoded, ImageSamples.LengthOf(maskWidth, maskHeight, 1, 1), "The /Mask",
                            out var data, out var isJpeg)
                    ? ReadStencil(stencil, data, isJpeg, maskWidth, maskHeight, "The /Mask")
                    : null;
            }
            else
            {
                return PageImage.FromRgba32(width, height, rgba);
            }

            if (alpha == null)
                return PageImage.FromRgba32(width, height, rgba);

            var drawnWidth = Math.Max(width, maskWidth);
            var drawnHeight = Math.Max(height, maskHeight);
            var drawnSamples = (long)drawnWidth * drawnHeight;
            var ownSamples = Math.Max((long)width * height, (long)maskWidth * maskHeight);
            if (drawnSamples > MaximumSamples || drawnSamples > MaximumScaleUp * ownSamples)
            {
                Report(PageRenderProblem.LimitReached,
                    $"An image of {width} by {height} samples under a mask of {maskWidth} by {maskHeight} would be drawn at {drawnWidth} by {drawnHeight}, more than the {MaximumSamples} samples drawn or {MaximumScaleUp} times what either has, so the mask is drawn at the image's size and loses its detail.");
                drawnWidth = width;
                drawnHeight = height;
            }

            rgba = Resample(rgba, width, height, drawnWidth, drawnHeight, 4);
            MultiplyAlpha(rgba, Resample(alpha, maskWidth, maskHeight, drawnWidth, drawnHeight, 1));
            if (softMask != null && ReadMatte(softMask, space) is { } matte)
                Unmatte(rgba, matte);
            return PageImage.FromRgba32(drawnWidth, drawnHeight, rgba);
        }

        /// <summary>
        /// The coverage of a stencil: 255 where a sample is painted, which is where it decodes to
        /// 0, and 0 where it decodes to 1.
        /// </summary>
        private byte[] ReadStencil(PdfDictionary stencil, byte[] data, bool isJpeg, int width, int height, string what)
        {
            if (isJpeg)
            {
                Report(PageRenderProblem.UnsupportedImage, what + " is a JPEG, which a stencil cannot be.");
                return null;
            }

            if (PdfItemValues.TryGetInteger(stencil.Elements["/BitsPerComponent"], out var bits) && bits != 1)
            {
                Report(PageRenderProblem.InvalidValue, what + "'s /BitsPerComponent is not 1.");
                return null;
            }

            var decode = new double[2];
            var inverted = TryReadNumbers(stencil.Elements["/Decode"], decode) && decode[0] > decode[1];
            var samples = new ImageSamples(data, width, height, 1, 1);
            if (IsMostlyMissing(samples, what))
                return null;

            var painted = inverted ? 1 : 0;
            var coverage = new byte[(long)width * height];
            var at = 0L;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    coverage[at++] = samples.Read(x, y, 0) == painted ? (byte)255 : (byte)0;
            }

            return coverage;
        }

        /// <summary>An <c>/SMask</c>'s samples as alpha, at the soft mask's own size.</summary>
        private byte[] ReadSoftMask(PdfDictionary mask, out int width, out int height)
        {
            width = height = 0;
            if (DataOf(mask, "The /SMask") is not { } encoded
                || !TryReadSize(mask, "The /SMask", out width, out height)
                || !TryUnfilter(mask, encoded, ImageSamples.LengthOf(width, height, 1, DeclaredBits(mask)), "The /SMask",
                    out var data, out var isJpeg))
                return null;

            var decode = new double[2];
            if (!TryReadNumbers(mask.Elements["/Decode"], decode))
                decode = [0, 1];

            ImageSamples samples;
            if (isJpeg)
            {
                if (DecodeJpeg(data, "The /SMask") is not { } decoded)
                    return null;

                // Gray, as a soft mask is: its red is the sample, and the other three are skipped.
                width = decoded.Width;
                height = decoded.Height;
                samples = new ImageSamples(decoded.Data.ToArray(), width, height, 4, 8);
            }
            else
            {
                if (!TryReadBits(mask, "The /SMask", out var bits))
                    return null;

                samples = new ImageSamples(data, width, height, 1, bits);
                if (IsMostlyMissing(samples, "The /SMask"))
                    return null;
            }

            var alpha = new byte[(long)width * height];
            var at = 0L;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    alpha[at++] = ToByte(decode[0] + samples.Read(x, y, 0) * (decode[1] - decode[0]) / samples.MaxValue);
            }

            return alpha;
        }

        /// <summary>
        /// The <c>/Matte</c> of a soft mask as RGB from 0 to 255: the colour the image's samples
        /// were blended with before they were written, by the alpha the soft mask holds.
        /// </summary>
        private double[] ReadMatte(PdfDictionary mask, RenderColorSpace space)
        {
            if (PdfItemValues.IsNull(mask.Elements["/Matte"]))
                return null;

            var matte = new double[space.ComponentCount];
            if (!TryReadNumbers(mask.Elements["/Matte"], matte))
            {
                Report(PageRenderProblem.InvalidValue, "The /SMask's /Matte is not one number for each component, and is not applied.");
                return null;
            }

            var colour = space.ToRgb(matte);
            return [colour.Red * 255, colour.Green * 255, colour.Blue * 255];
        }

        /// <summary>
        /// Takes the matte colour back out of samples blended with it, which ISO 32000-1 11.6.5.3
        /// gives as c' = m + a(c - m).
        /// </summary>
        private static void Unmatte(byte[] rgba, double[] matte)
        {
            for (var at = 0; at < rgba.Length; at += 4)
            {
                var alpha = rgba[at + 3];
                if (alpha == 0)
                    continue;

                for (var channel = 0; channel < 3; channel++)
                {
                    var colour = matte[channel] + (rgba[at + channel] - matte[channel]) * 255 / alpha;
                    rgba[at + channel] = (byte)Math.Round(Math.Max(0, Math.Min(255, colour)));
                }
            }
        }

        /// <summary>
        /// Samples of <paramref name="size"/> bytes each made another size: the nearest of them to
        /// each sample of the new size.
        /// </summary>
        private static byte[] Resample(byte[] samples, int width, int height, int toWidth, int toHeight, int size)
        {
            if (width == toWidth && height == toHeight)
                return samples;

            var resampled = new byte[(long)toWidth * toHeight * size];
            var at = 0L;
            for (var y = 0; y < toHeight; y++)
            {
                var row = (long)((y + 0.5) * height / toHeight) * width;
                for (var x = 0; x < toWidth; x++)
                {
                    var from = (row + (long)((x + 0.5) * width / toWidth)) * size;
                    for (var index = 0; index < size; index++)
                        resampled[at++] = samples[from + index];
                }
            }

            return resampled;
        }

        private static void MultiplyAlpha(byte[] rgba, byte[] alpha)
        {
            for (var at = 0L; at < alpha.Length; at++)
                rgba[at * 4 + 3] = (byte)((rgba[at * 4 + 3] * alpha[at] + 127) / 255);
        }

        // ----- Reports -------------------------------------------------------------------------

        /// <summary>
        /// Whether data that stops short of the image's size stops so short that it is not drawn,
        /// having reported it either way. A tail that is cut off is drawn as zeros, as viewers draw
        /// it. Less than half the data is refused, so that a few bytes cannot stand for an image of
        /// any size the limit allows and cost all of its samples to draw.
        /// </summary>
        private bool IsMostlyMissing(ImageSamples samples, string what)
        {
            if (samples.Length >= samples.LengthNeeded)
                return false;

            Report(PageRenderProblem.InvalidValue, what + "'s data stops short of its size." + Shortfall(samples.Length, samples.LengthNeeded));
            return samples.Length * 2 < samples.LengthNeeded;
        }

        /// <summary>What becomes of data <paramref name="length"/> bytes long where <paramref name="needed"/> are needed.</summary>
        private static string Shortfall(long length, long needed) =>
            length >= needed ? null
            : length * 2 < needed ? " It is less than half of what its size needs, and is not drawn."
            : " The rest is drawn as if every sample were 0.";

        private void Report(PageRenderProblem problem, string message) => sink.Report(problem, subject, message);
    }
}
