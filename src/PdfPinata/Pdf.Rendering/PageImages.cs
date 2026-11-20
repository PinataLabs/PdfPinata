using PdfPinata.Pdf.Content.Objects;

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
        sink.Report(PageRenderProblem.UnsupportedImage, name, "Image XObjects are not drawn yet.");
        return null;
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
        sink.Report(PageRenderProblem.UnsupportedImage, "BI", "Inline images are not drawn yet.");
        return null;
    }
}
