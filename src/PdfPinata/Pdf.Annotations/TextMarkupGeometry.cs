using System;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// The measurements the ruled text markup annotations share.
/// </summary>
internal static class TextMarkupGeometry
{
    /// <summary>
    /// How thick a rule drawn over a quadrilateral of the given height should be.
    /// </summary>
    /// <remarks>
    /// A fourteenth of the height of the line, which is close to the weight of the stem of a
    /// letter set at that size, so the rule reads as belonging to the text rather than to the
    /// page. The floor keeps a rule over a very small quadrilateral from thinning to nothing:
    /// a hairline is a line of whatever width the device draws, which is a different thing on
    /// screen from in print.
    /// </remarks>
    public static double RuleThickness(PdfRectangle quad)
    {
        return Math.Max((quad.Y2 - quad.Y1) / 14, 0.25);
    }

    /// <summary>
    /// The operators that fill a rectangle, each number to three decimal places.
    /// </summary>
    public static string FilledRectangle(double x, double y, double width, double height) =>
        PdfEncoders.Format("{0} {1} {2} {3} re f\n",
            PdfEncoders.ToString(x), PdfEncoders.ToString(y), PdfEncoders.ToString(width), PdfEncoders.ToString(height));
}
