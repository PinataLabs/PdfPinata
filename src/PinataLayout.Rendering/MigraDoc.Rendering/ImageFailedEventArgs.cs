#nullable enable

using System;
using PinataLayout.DocumentObjectModel.Shapes;

namespace PinataLayout.Rendering;

/// <summary>
/// Says which image could not be drawn, and why.
/// </summary>
/// <remarks>
/// An image that cannot be measured or drawn is replaced by a grey placeholder so that one bad
/// image does not cost the whole document. That leaves the reason it failed nowhere to be seen,
/// which is what this carries.
/// </remarks>
public sealed class ImageFailedEventArgs : EventArgs
{
    internal ImageFailedEventArgs(Image image, ImageFailure failure, Exception? exception)
    {
        Image = image;
        Failure = failure;
        Exception = exception;
    }

    /// <summary>
    /// The image in the document that was replaced by a placeholder.
    /// </summary>
    public Image Image { get; }

    /// <summary>
    /// What went wrong with it.
    /// </summary>
    public ImageFailure Failure { get; }

    /// <summary>
    /// The exception that stopped the image being read, or null when nothing was thrown —
    /// an image whose size works out empty fails without an exception.
    /// </summary>
    public Exception? Exception { get; }
}
