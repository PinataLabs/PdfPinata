using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;

namespace PinataLayout.Rendering;

/// <summary>
/// Turns the dash styles the document object model names into the one a pen is drawn with.
/// </summary>
/// <remarks>
/// <see cref="BorderStyle"/> is not mapped here: two of its values are dash patterns rather than
/// dash styles, which a pen is given through <see cref="XPen.DashPattern"/>, so
/// <see cref="BordersRenderer"/> keeps its own.
/// </remarks>
internal static class DashStyleHelper
{
    /// <summary>
    /// The dash style a line format is drawn with. Anything not named is solid.
    /// </summary>
    public static XDashStyle ToXDashStyle(DashStyle dashStyle) => dashStyle switch
    {
        DashStyle.Dash => XDashStyle.Dash,
        DashStyle.DashDot => XDashStyle.DashDot,
        DashStyle.DashDotDot => XDashStyle.DashDotDot,
        DashStyle.SquareDot => XDashStyle.Dot,
        _ => XDashStyle.Solid
    };

    /// <summary>
    /// The dash style an underline is drawn with. <see cref="Underline.Single"/>,
    /// <see cref="Underline.Words"/> and anything not named are solid.
    /// </summary>
    public static XDashStyle ToXDashStyle(Underline underline) => underline switch
    {
        Underline.Dash => XDashStyle.Dash,
        Underline.DotDash => XDashStyle.DashDot,
        Underline.DotDotDash => XDashStyle.DashDotDot,
        Underline.Dotted => XDashStyle.Dot,
        _ => XDashStyle.Solid
    };

    /// <summary>
    /// The dash style a strikethrough is drawn with. <see cref="Strikethrough.Single"/>,
    /// <see cref="Strikethrough.Words"/> and anything not named are solid.
    /// </summary>
    public static XDashStyle ToXDashStyle(Strikethrough strikethrough) => strikethrough switch
    {
        Strikethrough.Dash => XDashStyle.Dash,
        Strikethrough.DotDash => XDashStyle.DashDot,
        Strikethrough.DotDotDash => XDashStyle.DashDotDot,
        Strikethrough.Dotted => XDashStyle.Dot,
        _ => XDashStyle.Solid
    };
}
