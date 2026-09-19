using System;
using System.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using SkiaSharp;

namespace PdfPinata.Skia;

/// <summary>
/// Decodes images with SkiaSharp for use by PdfPinata.
/// Register it once before loading any image:
/// <c>ImageSource.ImageSourceImpl = new SkiaImageSource();</c>
/// </summary>
public class SkiaImageSource
    : ImageSource
{

    /// <summary>
    /// Wraps an already decoded SkiaSharp bitmap. The bitmap must be Bgra8888 with
    /// unpremultiplied alpha, and ownership passes to the returned image source.
    /// </summary>
    public static IImageSource FromSkiaBitmap(SKBitmap bitmap, bool transparent, int? quality = 75)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var name = "*" + Guid.NewGuid().ToString("B");
        // ReSharper disable once PossibleInvalidOperationException
        return new SkiaImageSourceImpl(name, bitmap, (int)quality, transparent);
    }


    /// <summary>Decodes an image from a file.</summary>
    protected override IImageSource FromFileImpl(string path, int? quality = 75)
    {
        using var data = SKData.Create(path);
        return Decode(path, data, quality);
    }


    /// <summary>Decodes an image from bytes fetched on demand.</summary>
    protected override IImageSource FromBinaryImpl(string name, Func<byte[]> imageSource, int? quality = 75)
    {
        using var data = SKData.CreateCopy(imageSource.Invoke());
        return Decode(name, data, quality);
    }


    /// <summary>Decodes an image from a stream opened on demand.</summary>
    protected override IImageSource FromStreamImpl(string name, Func<Stream> imageStream, int? quality = 75)
    {
        using var stream = imageStream.Invoke();
        using var data = SKData.Create(stream);
        return Decode(name, data, quality);
    }


    private static SkiaImageSourceImpl Decode(string name, SKData data, int? quality)
    {
        if (data == null)
            throw new InvalidOperationException("Unable to read image data for '" + name + "'.");

        using var codec = SKCodec.Create(data);
        if (codec == null)
            throw new InvalidOperationException("Unsupported or corrupt image format for '" + name + "'.");

        // Decode to unpremultiplied BGRA. Skia premultiplies by default, which would darken
        // semi-transparent pixels once PdfImage reads the colour and alpha channels separately.
        // Bgra8888 also matches the byte order PdfImage expects: B, G, R, A.
        var info = new SKImageInfo(
            codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        var bitmap = new SKBitmap(info);
        try
        {
            var result = codec.GetPixels(info, bitmap.GetPixels());
            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                throw new InvalidOperationException(
                    "Failed to decode image '" + name + "': " + result + ".");
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }

        // Mirrors the previous ImageSharp behaviour: PNG sources keep their alpha and take the
        // FLATE path, everything else is re-encoded as JPEG.
        var transparent = codec.EncodedFormat == SKEncodedImageFormat.Png;

        // ReSharper disable once PossibleInvalidOperationException
        return new SkiaImageSourceImpl(name, bitmap, (int)quality, transparent);
    }


    private sealed class SkiaImageSourceImpl
        : IImageSource, IDisposable
    {
        private readonly SKBitmap _bitmap;
        private readonly int _quality;

        public int Width => _bitmap.Width;

        public int Height => _bitmap.Height;

        public string Name { get; }

        public bool Transparent { get; }


        public SkiaImageSourceImpl(string name, SKBitmap bitmap, int quality, bool transparent)
        {
            Name = name;
            _bitmap = bitmap;
            _quality = quality;
            Transparent = transparent;
        }


        public void SaveAsJpeg(MemoryStream ms)
        {
            using var image = SKImage.FromBitmap(_bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, _quality);
            if (data == null)
                throw new InvalidOperationException("JPEG encoding failed for '" + Name + "'.");

            data.SaveTo(ms);
        }


        public PixelBuffer GetPixels()
        {
            if (_bitmap.ColorType != SKColorType.Bgra8888)
                throw new InvalidOperationException(
                    $"Expected a Bgra8888 bitmap for '{Name}' but got {_bitmap.ColorType}.");

            // A PixelBuffer promises straight alpha, and premultiplied colour channels are a
            // silent wrong answer rather than a loud one: PdfImage writes the colour into the
            // image stream and the alpha into the /SMask separately, so a semi-transparent pixel
            // comes out darkened with nothing to say why. Opaque is fine - alpha is 255
            // throughout, so the two are the same bytes.
            if (_bitmap.AlphaType == SKAlphaType.Premul)
                throw new InvalidOperationException(
                    $"Expected unpremultiplied alpha for '{Name}' but the bitmap is premultiplied. "
                    + "Decode into SKAlphaType.Unpremul, or unpremultiply before handing the bitmap "
                    + "to SkiaImageSource.FromSkiaBitmap.");

            var width = _bitmap.Width;
            var height = _bitmap.Height;
            var stride = width * PixelBuffer.BytesPerPixel;

            // Skia decodes straight into the layout a PixelBuffer promises - top-down, four bytes
            // per pixel, B, G, R, A - so this is a copy out of native memory and nothing more. Rows
            // are copied one at a time because SKBitmap.RowBytes may exceed the packed width.
            var pixels = new byte[stride * height];
            ReadOnlySpan<byte> source = _bitmap.GetPixelSpan();
            var rowBytes = _bitmap.RowBytes;
            for (var y = 0; y < height; y++)
                source.Slice(y * rowBytes, stride).CopyTo(pixels.AsSpan(y * stride, stride));

            return new PixelBuffer(width, height, pixels);
        }


        public void Dispose()
        {
            _bitmap.Dispose();
        }
    }
}
