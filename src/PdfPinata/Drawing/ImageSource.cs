
using System;
using System.IO;
using PdfPinata.Fonts;


namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// The seam through which images are decoded. The core package carries no imaging dependency of
/// its own, so a backend must supply an implementation before any image is loaded — either
/// <c>SkiaImageSource</c> from PdfPinata.Skia or <c>ImageSharpImageSource</c> from
/// PdfPinata.ImageSharp.
/// </summary>
/// <remarks>
/// This type ships in the <b>PdfPinata</b> assembly but lives in a PinataLayout namespace, so
/// registering it needs a <c>using PinataLayout.DocumentObjectModel.Shapes;</c> from
/// code that otherwise has nothing to do with PinataLayout.
/// </remarks>
public abstract class ImageSource
{
    /// <summary>
    /// Gets or sets the implementation images are decoded through. Reading any image before this
    /// is set throws an <see cref="InvalidOperationException"/> naming the packages that supply
    /// one. Unlike the font resolver, it may be replaced at any time.
    /// </summary>
    public static ImageSource ImageSourceImpl { get; set; }

    /// <summary>
    /// Whether <see cref="ImageSourceImpl"/> has been set. Reading <see cref="ImageSourceImpl"/>
    /// itself never throws - only decoding an image through it does - so this answers the same
    /// thing <c>ImageSourceImpl != null</c> would, stated here for the same shape as the seams on
    /// <see cref="GlobalFontSettings"/>.
    /// </summary>
    public static bool IsImageSourceImplSet => ImageSourceImpl != null;

    /// <summary>
    /// States that <see cref="ImageSourceImpl"/> may be set, replaced or cleared at any time - see
    /// <see cref="SeamLifecycle.SetAnytime"/>.
    /// </summary>
    public static SeamLifecycle ImageSourceImplLifecycle => SeamLifecycle.SetAnytime;

    /// <summary>
    /// One decoded image, as much of it as the PDF writer needs: its size, a name to identify it
    /// by, whether it has transparency, and the two encodings it can be written out in.
    /// </summary>
    public interface IImageSource
    {
        /// <summary>Gets the width of the image in pixels.</summary>
        int Width { get; }
        /// <summary>Gets the height of the image in pixels.</summary>
        int Height { get; }
        /// <summary>Gets the name the image is identified by, usually its file path.</summary>
        string Name { get; }
        /// <summary>Writes the image to the stream as JPEG, for a lossily compressed XObject.</summary>
        void SaveAsJpeg(MemoryStream ms);
        /// <summary>
        /// Gets whether the image has an alpha channel, which decides whether a soft mask is
        /// written alongside it.
        /// </summary>
        bool Transparent { get; }
        /// <summary>
        /// Gets the decoded pixels, for a losslessly stored XObject. See <see cref="PixelBuffer"/>
        /// for the one layout they come in - tightly packed, top-down, BGRA, straight alpha.
        /// </summary>
        PixelBuffer GetPixels();
    }

    /// <summary>
    /// The JPEG quality an image is written with when the caller passes none: what every
    /// <c>quality</c> parameter defaults to, and what a <c>null</c> passed to one means.
    /// </summary>
    protected const int DefaultQuality = 75;

    /// <summary>Decodes an image from a file. Implemented by the backend.</summary>
    protected abstract IImageSource FromFileImpl(string path, int? quality = 75);
    /// <summary>Decodes an image from bytes fetched on demand. Implemented by the backend.</summary>
    protected abstract IImageSource FromBinaryImpl(string name, Func<byte[]> imageSource, int? quality = 75);
    /// <summary>Decodes an image from a stream opened on demand. Implemented by the backend.</summary>
    protected abstract IImageSource FromStreamImpl(string name, Func<Stream> imageStream, int? quality = 75);


    private static ImageSource RequireImpl()
    {
        if (ImageSourceImpl == null)
            throw new InvalidOperationException(
                "No ImageSource implementation has been configured. Set ImageSource.ImageSourceImpl before "
                + "loading images, e.g. 'ImageSource.ImageSourceImpl = new SkiaImageSource();' from the "
                + "PdfPinata.Skia package.");

        return ImageSourceImpl;
    }

    /// <summary>
    /// Decodes an image from a file through the registered implementation.
    /// </summary>
    /// <param name="path">Path to the image file.</param>
    /// <param name="quality">
    /// JPEG quality, 0 to 100, used when the image is written lossily. <c>null</c> means the default, 75.
    /// </param>
    /// <exception cref="InvalidOperationException">No implementation has been registered.</exception>
    public static IImageSource FromFile(string path, int? quality = 75)
    {
        return RequireImpl().FromFileImpl(path, quality ?? DefaultQuality);
    }

    /// <summary>
    /// Decodes an image from bytes through the registered implementation. The bytes are fetched
    /// by calling <paramref name="imageSource"/>, so a caller holding the image already need not
    /// write it to a file first.
    /// </summary>
    /// <param name="name">A name to identify the image by.</param>
    /// <param name="imageSource">Returns the encoded bytes of the image.</param>
    /// <param name="quality">
    /// JPEG quality, 0 to 100, used when the image is written lossily. <c>null</c> means the default, 75.
    /// </param>
    /// <exception cref="InvalidOperationException">No implementation has been registered.</exception>
    public static IImageSource FromBinary(string name, Func<byte[]> imageSource, int? quality = 75)
    {
        return RequireImpl().FromBinaryImpl(name, imageSource, quality ?? DefaultQuality);
    }

    /// <summary>
    /// Decodes an image from a stream through the registered implementation. The stream is opened
    /// by calling <paramref name="imageStream"/>.
    /// </summary>
    /// <param name="name">A name to identify the image by.</param>
    /// <param name="imageStream">Opens a stream over the encoded image.</param>
    /// <param name="quality">
    /// JPEG quality, 0 to 100, used when the image is written lossily. <c>null</c> means the default, 75.
    /// </param>
    /// <exception cref="InvalidOperationException">No implementation has been registered.</exception>
    public static IImageSource FromStream(string name, Func<Stream> imageStream, int? quality = 75)
    {
        return RequireImpl().FromStreamImpl(name, imageStream, quality ?? DefaultQuality);
    }
}
