using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using PinataLayout.DocumentObjectModel.Shapes;
using System;
using System.IO;
using System.Runtime.CompilerServices;
#if !IMAGESHARP4
using SixLabors.ImageSharp.Advanced;
#endif
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace PdfPinata.Utils;

/// <summary>
/// Decodes images with ImageSharp. Register it as <see cref="ImageSource.ImageSourceImpl"/> before
/// loading any image, typically as <c>ImageSharpImageSource&lt;Rgba32&gt;</c>.
/// </summary>
/// <typeparam name="TPixel">The pixel format images are decoded into.</typeparam>
public class ImageSharpImageSource<TPixel> : ImageSource where TPixel : unmanaged, IPixel<TPixel>
{

    /// <summary>
    /// Wraps an image the caller has already decoded with ImageSharp, so that one held in memory need
    /// not be encoded and decoded again to be drawn.
    /// </summary>
    /// <param name="image">The decoded image.</param>
    /// <param name="imgFormat">The format it was decoded from; a PNG is treated as transparent.</param>
    /// <param name="quality">
    /// JPEG quality, 0 to 100, used when the image is written lossily. <c>null</c> means the default, 75.
    /// </param>
    public static IImageSource FromImageSharpImage(Image<TPixel> image, IImageFormat imgFormat, int? quality = 75)
    {
        var _path = "*" + Guid.NewGuid().ToString("B");
        return new ImageSharpImageSourceImpl<TPixel>(_path, image, quality ?? DefaultQuality, imgFormat is PngFormat);
    }

    /// <summary>Decodes an image from bytes fetched on demand.</summary>
    protected override IImageSource FromBinaryImpl(string name, Func<byte[]> imageSource, int? quality = 75)
    {
        var data = imageSource.Invoke();
        Image<TPixel> image;
        bool isPng;
        try
        {
            image = LoadFromBinary(data, out isPng);
        }
        catch (Exception ex) when (ImageSharpVersion.IsBindingFailure(ex))
        {
            throw ImageSharpVersion.Incompatible(ex);
        }
        return new ImageSharpImageSourceImpl<TPixel>(name, image, quality ?? DefaultQuality, isPng);
    }

    /// <summary>Decodes an image from a file.</summary>
    protected override IImageSource FromFileImpl(string path, int? quality = 75)
    {
        Image<TPixel> image;
        bool isPng;
        try
        {
            image = LoadFromFile(path, out isPng);
        }
        catch (Exception ex) when (ImageSharpVersion.IsBindingFailure(ex))
        {
            throw ImageSharpVersion.Incompatible(ex);
        }
        return new ImageSharpImageSourceImpl<TPixel>(path, image, quality ?? DefaultQuality, isPng);
    }

    /// <summary>Decodes an image from a stream opened on demand.</summary>
    protected override IImageSource FromStreamImpl(string name, Func<Stream> imageStream, int? quality = 75)
    {
        using var stream = imageStream.Invoke();
        Image<TPixel> image;
        bool isPng;
        try
        {
            image = LoadFromStream(stream, out isPng);
        }
        catch (Exception ex) when (ImageSharpVersion.IsBindingFailure(ex))
        {
            throw ImageSharpVersion.Incompatible(ex);
        }
        return new ImageSharpImageSourceImpl<TPixel>(name, image, quality ?? DefaultQuality, isPng);
    }

    // The Load overloads differ between the majors: 2.x hands the format back through an out
    // parameter, and 3.0 removed those overloads in favour of the image's own metadata. So each call
    // sits in its own non-inlined method. That way a version mismatch surfaces as a
    // MissingMethodException the caller above can catch and translate, rather than tearing down the
    // calling method at JIT.

#if IMAGESHARP4
    // 4.x can convert an image to sRGB through its embedded ICC profile as it decodes, which 2.x
    // never did, and spec #305 asks that the same file decode to the same pixels in both packages.
    // Preserve keeps the profile and leaves the samples alone, as 2.x did. It is 4.1's default as
    // well, and is said here so that a later 4.x changing the default cannot change a document.
    // ImageSharpDecodingParityTests holds both packages to 2.x's pixels.
    //
    // The options are made per load and not held in a static field. A field's initialiser runs in
    // the type initialiser, where a binding failure - 3.x has DecoderOptions but no
    // ColorProfileHandling - arrives as a TypeInitializationException, on this load and every
    // later one, and the caller above would not recognise it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DecoderOptions Decoding() => new() { ColorProfileHandling = ColorProfileHandling.Preserve };
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromBinary(byte[] data, out bool isPng)
    {
#if IMAGESHARP4
        var image = Image.Load<TPixel>(Decoding(), data);
        isPng = image.Metadata.DecodedImageFormat is PngFormat;
#else
        var image = Image.Load<TPixel>(data, out var imgFormat);
        isPng = imgFormat is PngFormat;
#endif
        return image;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromFile(string path, out bool isPng)
    {
#if IMAGESHARP4
        var image = Image.Load<TPixel>(Decoding(), path);
        isPng = image.Metadata.DecodedImageFormat is PngFormat;
#else
        var image = Image.Load<TPixel>(path, out var imgFormat);
        isPng = imgFormat is PngFormat;
#endif
        return image;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromStream(Stream stream, out bool isPng)
    {
#if IMAGESHARP4
        var image = Image.Load<TPixel>(Decoding(), stream);
        isPng = image.Metadata.DecodedImageFormat is PngFormat;
#else
        var image = Image.Load<TPixel>(stream, out var imgFormat);
        isPng = imgFormat is PngFormat;
#endif
        return image;
    }

    private class ImageSharpImageSourceImpl<TPixel2> : IImageSource, IDisposable where TPixel2 : unmanaged, IPixel<TPixel2>
    {
        private Image<TPixel2> Image { get; }
        private readonly int _quality;

        public int Width => Image.Width;
        public int Height => Image.Height;
        public string Name { get; }
        public bool Transparent { get; internal set; }

        public ImageSharpImageSourceImpl(string name, Image<TPixel2> image, int quality, bool isTransparent)
        {
            Name = name;
            Image = image;
            _quality = quality;
            Transparent = isTransparent;
        }

        public void SaveAsJpeg(MemoryStream ms)
        {
            try
            {
                EncodeJpeg(ms);
            }
            catch (Exception ex) when (ImageSharpVersion.IsBindingFailure(ex))
            {
                throw ImageSharpVersion.Incompatible(ex);
            }
        }

        public void Dispose()
        {
            Image.Dispose();
        }

        public PixelBuffer GetPixels()
        {
            try
            {
                return ReadPixels();
            }
            catch (Exception ex) when (ImageSharpVersion.IsBindingFailure(ex))
            {
                throw ImageSharpVersion.Incompatible(ex);
            }
        }

        // ImageSharp 3.0 turned every encoder property from 'set' into 'init', which changes the
        // setter signature, so these initialisers are isolated for the same reason as the loads above.

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EncodeJpeg(MemoryStream ms)
        {
#if IMAGESHARP4
            Image.SaveAsJpeg(ms, new JpegEncoder() { Quality = _quality, ColorType = EncodingColorType() });
#else
            Image.SaveAsJpeg(ms, new JpegEncoder() { Quality = _quality });
#endif
        }

#if IMAGESHARP4
        /// <summary>
        /// The colour type 2.x's encoder would have written this image in, so that the JPEG is one
        /// <c>PdfImage</c> can label <c>/DeviceRGB</c>.
        /// </summary>
        /// <remarks>
        /// Left to itself, 4.x's encoder writes in the colour type the image was decoded from, and it
        /// can write CMYK and YCCK: a CMYK JPEG came back out as four components under a
        /// <c>/DeviceRGB</c> label. 2.x's encoder kept the source's colour type only when it was a
        /// JPEG's 4:4:4 or 4:2:0 YCbCr, luminance or RGB, the four it could write, and otherwise
        /// wrote YCbCr, 4:4:4 at a quality of 91 or more and 4:2:0 below. Null asks 4.x to keep the
        /// source's. 4.x also reads a colour type out of other formats' metadata, which 2.x never
        /// did - a grayscale TIFF would become a one-component JPEG - so only a JPEG's is kept.
        /// </remarks>
        private JpegColorType? EncodingColorType()
        {
            if (Image.Metadata.DecodedImageFormat is JpegFormat
                && Image.Metadata.GetJpegMetadata().ColorType is JpegColorType.YCbCrRatio444
                    or JpegColorType.YCbCrRatio420 or JpegColorType.Luminance or JpegColorType.Rgb)
            {
                return null;
            }

            return _quality >= 91 ? JpegColorType.YCbCrRatio444 : JpegColorType.YCbCrRatio420;
        }
#endif

        /// <summary>
        /// Converts the decoded image into the one layout <see cref="PixelBuffer"/> describes:
        /// tightly packed, top-down, B, G, R, A.
        /// </summary>
        /// <remarks>
        /// The channel reorder is done here rather than left to an encoder. It used to happen
        /// invisibly inside <c>BmpEncoder</c>, which writes B, G, R[, A] whatever
        /// <typeparamref name="TPixel2"/> holds because that is what a BMP file is; with the BMP
        /// detour gone nothing performs it for free. <c>ToBgra32Bytes</c> is ImageSharp's own bulk
        /// conversion for exactly this byte layout, so a source in any pixel format lands in the
        /// same order Skia decodes into directly.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private PixelBuffer ReadPixels()
        {
            var width = Image.Width;
            var height = Image.Height;
            var stride = width * PixelBuffer.BytesPerPixel;

            var pixels = new byte[stride * height];
            var operations = PixelOperations<TPixel2>.Instance;
#if IMAGESHARP4
            var configuration = Image.Configuration;
#else
            var configuration = Image.GetConfiguration();
#endif

            Image.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < height; y++)
                    operations.ToBgra32Bytes(
                        configuration, accessor.GetRowSpan(y), pixels.AsSpan(y * stride, stride), width);
            });

            return new PixelBuffer(width, height, pixels);
        }
    }
}

/// <summary>
/// Translates the binding failures that occur when this assembly is run against a major version of
/// SixLabors.ImageSharp other than the one it was compiled against: the Apache-2.0 licensed 2.1.x
/// line for PdfPinata.ImageSharp, the 4.x line for PdfPinata.ImageSharp4.
/// </summary>
internal static class ImageSharpVersion
{
    public static bool IsBindingFailure(Exception ex)
    {
        return ex is MissingMemberException or TypeLoadException;
    }

    public static InvalidOperationException Incompatible(Exception inner)
    {
        var loaded = typeof(Image).Assembly.GetName().Version;

#if IMAGESHARP4
        return new InvalidOperationException(
            "PdfPinata.ImageSharp4 is built against SixLabors.ImageSharp 4.x (4.1.3 or later, below "
            + "5.0), but version " + loaded + " was loaded at runtime, and the two are not binary "
            + "compatible. Either pin SixLabors.ImageSharp to a 4.x version, use PdfPinata.ImageSharp "
            + "for the 2.1.x line, or switch to the PdfPinata.Skia backend "
            + "('ImageSource.ImageSourceImpl = new SkiaImageSource();') which has no such constraint.",
            inner);
#else
        return new InvalidOperationException(
            "PdfPinata.ImageSharp is built against SixLabors.ImageSharp 2.1.x, but version "
            + loaded + " was loaded at runtime. ImageSharp 3.0 removed the "
            + "Image.Load(..., out IImageFormat) overloads and changed the encoder properties, so the "
            + "two are not binary compatible. Either pin SixLabors.ImageSharp to a 2.1.x version, or "
            + "switch to the PdfPinata.Skia backend "
            + "('ImageSource.ImageSourceImpl = new SkiaImageSource();') which has no such constraint.",
            inner);
#endif
    }
}
