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
            if (encoded == null || !TryReadSize(image, "The image", out var width, out var height)
                                || !TryUnfilter(image, encoded, "The image", out var data, out var isJpeg))
                return null;

            if (PdfItemValues.TryGetBoolean(image.Elements["/ImageMask"], out var isStencil) && isStencil)
            {
                return ReadStencil(image, data, isJpeg, width, height, "The stencil mask") is { } coverage
                    ? PageImage.FromAlpha8(width, height, coverage)
                    : null;
            }

            var space = ColorSpaceOf(image, out var isIndexed);
            if (space == null)
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

            if (rgba == null)
                return null;

            ApplyMask(image, rgba, width, height, space);
            return PageImage.FromRgba32(width, height, rgba);
        }

        // ----- The dictionary ------------------------------------------------------------------

        private bool TryReadSize(PdfDictionary image, string what, out int width, out int height)
        {
            if (!PdfItemValues.TryGetInteger(image.Elements["/Width"], out width) || width < 1
                || !PdfItemValues.TryGetInteger(image.Elements["/Height"], out height) || height < 1)
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
        /// Undoes every filter but <c>/DCTDecode</c>, which may only be the last and is left for
        /// the device: <paramref name="isJpeg"/> says the data is a JPEG file still.
        /// </summary>
        private bool TryUnfilter(PdfDictionary image, byte[] data, string what, out byte[] unfiltered, out bool isJpeg)
        {
            unfiltered = data;
            isJpeg = false;

            var filter = PdfReference.Dereference(image.Elements["/Filter"]);
            PdfItem[] filters;
            if (PdfItemValues.IsNull(filter))
                filters = [];
            else if (filter is PdfArray array)
                filters = [.. array.Elements];
            else
                filters = [filter];

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
                        isJpeg = true;
                        return true;

                    case "/DCTDecode" or "/DCT":
                        Report(PageRenderProblem.UnsupportedImage, what + " is filtered after /DCTDecode, which a JPEG decoder cannot read.");
                        return false;

                    case "/CCITTFaxDecode" or "/CCF" or "/JBIG2Decode" or "/JPXDecode":
                        Report(PageRenderProblem.UnsupportedImage, $"{what} is encoded with {name}, which is not decoded yet.");
                        return false;
                }

                if (!TryApply(name, unfiltered, parameters[index] as PdfDictionary, what, out unfiltered))
                    return false;
            }

            return true;
        }

        private bool TryApply(string name, byte[] data, PdfDictionary parameters, string what, out byte[] decoded)
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
            var decoded = device.DecodeImage(PageImageCodec.Jpeg, jpeg);
            if (decoded == null || decoded.Format != PageImageFormat.Rgba32)
            {
                Report(PageRenderProblem.UnsupportedImage, "The image is a JPEG, and the device did not decode it.");
                return null;
            }

            width = decoded.Width;
            height = decoded.Height;
            var rgba = decoded.Data.ToArray();

            var decode = new double[components * 2];
            var hasDecode = TryReadNumbers(image.Elements["/Decode"], decode) && !IsDefault(decode);
            if (components is not (1 or 3))
            {
                if (hasDecode || PdfReference.Dereference(image.Elements["/Mask"]) is PdfArray)
                    Report(PageRenderProblem.UnsupportedFeature, "The /Decode or colour key of a JPEG of four components is not applied, because the device answers its RGB.");
                return rgba;
            }

            if (!hasDecode && !PdfItemValues.IsNull(image.Elements["/Decode"]) && !TryReadNumbers(image.Elements["/Decode"], decode))
                Report(PageRenderProblem.InvalidValue, "The image's /Decode is not two numbers for each component, and is not applied.");

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

        private static bool IsDefault(double[] decode)
        {
            for (var index = 0; index < decode.Length; index++)
            {
#pragma warning disable S1244 // Exact on purpose: only the numbers 0 and 1 written out are the default.
                if (decode[index] != index % 2)
                    return false;
#pragma warning restore S1244
            }

            return true;
        }

        // ----- Masks ---------------------------------------------------------------------------

        /// <summary>
        /// Applies an <c>/SMask</c> or, where there is none, a <c>/Mask</c> that is a stencil: each
        /// made the size of the image and multiplied into its alpha. A mask that cannot be read is
        /// reported, and the image is drawn without it.
        /// </summary>
        private void ApplyMask(PdfDictionary image, byte[] rgba, int width, int height, RenderColorSpace space)
        {
            if (PdfReference.Dereference(image.Elements["/SMask"]) is PdfDictionary softMask)
            {
                if (ReadSoftMask(softMask, out var maskWidth, out var maskHeight) is not { } alpha)
                    return;

                MultiplyAlpha(rgba, Resample(alpha, maskWidth, maskHeight, width, height));
                if (ReadMatte(softMask, space) is { } matte)
                    Unmatte(rgba, matte);
                return;
            }

            if (PdfReference.Dereference(image.Elements["/Mask"]) is PdfDictionary stencil)
            {
                if (DataOf(stencil, "The /Mask") is not { } encoded
                    || !TryReadSize(stencil, "The /Mask", out var maskWidth, out var maskHeight)
                    || !TryUnfilter(stencil, encoded, "The /Mask", out var data, out var isJpeg)
                    || ReadStencil(stencil, data, isJpeg, maskWidth, maskHeight, "The /Mask") is not { } coverage)
                    return;

                MultiplyAlpha(rgba, Resample(coverage, maskWidth, maskHeight, width, height));
            }
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
                || !TryUnfilter(mask, encoded, "The /SMask", out var data, out var isJpeg))
                return null;

            var decode = new double[2];
            if (!TryReadNumbers(mask.Elements["/Decode"], decode))
                decode = [0, 1];

            ImageSamples samples;
            if (isJpeg)
            {
                var decoded = device.DecodeImage(PageImageCodec.Jpeg, data);
                if (decoded == null || decoded.Format != PageImageFormat.Rgba32)
                {
                    Report(PageRenderProblem.UnsupportedImage, "The /SMask is a JPEG, and the device did not decode it. The image is drawn without it.");
                    return null;
                }

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
        /// A mask made the size of the image: the nearest of its samples to each of the image's.
        /// </summary>
        private static byte[] Resample(byte[] mask, int maskWidth, int maskHeight, int width, int height)
        {
            if (maskWidth == width && maskHeight == height)
                return mask;

            var resampled = new byte[(long)width * height];
            var at = 0L;
            for (var y = 0; y < height; y++)
            {
                var row = (long)((y + 0.5) * maskHeight / height) * maskWidth;
                for (var x = 0; x < width; x++)
                    resampled[at++] = mask[row + (long)((x + 0.5) * maskWidth / width)];
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

            if (samples.Length * 2 < samples.LengthNeeded)
            {
                Report(PageRenderProblem.InvalidValue, what + "'s data is less than half of what its size needs, and is not drawn.");
                return true;
            }

            Report(PageRenderProblem.InvalidValue, what + "'s data stops short of its size. The rest is drawn as if every sample were 0.");
            return false;
        }

        private void Report(PageRenderProblem problem, string message) => sink.Report(problem, subject, message);
    }
}
