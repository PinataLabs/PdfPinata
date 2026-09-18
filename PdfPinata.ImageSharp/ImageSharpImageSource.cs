using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Advanced;
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
    public static IImageSource FromImageSharpImage(Image<TPixel> image, IImageFormat imgFormat, int? quality = 75)
    {
        var _path = "*" + Guid.NewGuid().ToString("B");
        return new ImageSharpImageSourceImpl<TPixel>(_path, image, (int)quality, imgFormat is PngFormat);
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
        return new ImageSharpImageSourceImpl<TPixel>(name, image, (int)quality, isPng);
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
        return new ImageSharpImageSourceImpl<TPixel>(path, image, (int)quality, isPng);
    }

    /// <summary>Decodes an image from a stream opened on demand.</summary>
    protected override IImageSource FromStreamImpl(string name, Func<Stream> imageStream, int? quality = 75)
    {
        using (var stream = imageStream.Invoke())
        {
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
            return new ImageSharpImageSourceImpl<TPixel>(name, image, (int)quality, isPng);
        }
    }

    // The Load(..., out IImageFormat) overloads exist only in ImageSharp 2.x, so each call sits in
    // its own non-inlined method. That way a version mismatch surfaces as a MissingMethodException
    // the caller above can catch and translate, rather than tearing down the calling method at JIT.

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromBinary(byte[] data, out bool isPng)
    {
        var image = Image.Load<TPixel>(data, out IImageFormat imgFormat);
        isPng = imgFormat is PngFormat;
        return image;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromFile(string path, out bool isPng)
    {
        var image = Image.Load<TPixel>(path, out IImageFormat imgFormat);
        isPng = imgFormat is PngFormat;
        return image;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Image<TPixel> LoadFromStream(Stream stream, out bool isPng)
    {
        var image = Image.Load<TPixel>(stream, out IImageFormat imgFormat);
        isPng = imgFormat is PngFormat;
        return image;
    }

    private class ImageSharpImageSourceImpl<TPixel2> : IImageSource where TPixel2 : unmanaged, IPixel<TPixel2>
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
            Image.SaveAsJpeg(ms, new JpegEncoder() { Quality = this._quality });
        }

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
            int width = Image.Width;
            int height = Image.Height;
            int stride = width * PixelBuffer.BytesPerPixel;

            byte[] pixels = new byte[stride * height];
            var operations = PixelOperations<TPixel2>.Instance;
            var configuration = Image.GetConfiguration();

            Image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                    operations.ToBgra32Bytes(
                        configuration, accessor.GetRowSpan(y), pixels.AsSpan(y * stride, stride), width);
            });

            return new PixelBuffer(width, height, pixels);
        }
    }
}

/// <summary>
/// Translates the binding failures that occur when this assembly, compiled against the
/// Apache-2.0 licensed SixLabors.ImageSharp 2.1.x line, is run against a later major version.
/// </summary>
internal static class ImageSharpVersion
{
    public static bool IsBindingFailure(Exception ex)
    {
        return ex is MissingMemberException || ex is TypeLoadException;
    }

    public static InvalidOperationException Incompatible(Exception inner)
    {
        var loaded = typeof(Image).Assembly.GetName().Version;

        return new InvalidOperationException(
            "PdfPinata.ImageSharp is built against SixLabors.ImageSharp 2.1.x, but version "
            + loaded + " was loaded at runtime. ImageSharp 3.0 removed the "
            + "Image.Load(..., out IImageFormat) overloads and changed the encoder properties, so the "
            + "two are not binary compatible. Either pin SixLabors.ImageSharp to a 2.1.x version, or "
            + "switch to the PdfPinata.Skia backend "
            + "('ImageSource.ImageSourceImpl = new SkiaImageSource();') which has no such constraint.",
            inner);
    }
}
