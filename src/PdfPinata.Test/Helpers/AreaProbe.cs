using System;
using System.Reflection;
using PinataLayout.Rendering;
using PdfPinata.Drawing;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Reaches PinataLayout's areas, which are internal to the rendering assembly, so that they can be
///   tested directly rather than through a rendered page.
/// </summary>
/// <remarks>
///   Reflection rather than <c>InternalsVisibleTo</c>: this repository does not use one, and
///   <c>CLAUDE.md</c> records that the polyfills are duplicated across two assemblies precisely
///   because there is none. The shipped assembly is left alone and the awkwardness is kept here,
///   where it is one file and the tests above it read as though it were not there.
/// </remarks>
internal static class AreaProbe
{
    private static readonly Assembly Rendering = typeof(Area).Assembly;

    private static readonly Type RectangleType =
        Rendering.GetType("PinataLayout.Rendering.Rectangle", throwOnError: true);

    private static readonly Type ObstructedType =
        Rendering.GetType("PinataLayout.Rendering.ObstructedArea", throwOnError: true);

    private const BindingFlags Internals = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>A plain rectangular area, in points.</summary>
    internal static Area Rectangle(double x, double y, double width, double height)
    {
        return (Area)Activator.CreateInstance(RectangleType, Internals, null,
            [
                XUnit.FromPoint(x), XUnit.FromPoint(y), XUnit.FromPoint(width), XUnit.FromPoint(height)
            ],
            null);
    }

    /// <summary>A rectangular area with things standing in it, all in points.</summary>
    internal static Area Obstructed(Area bounds, params Area[] obstacles)
    {
        // The constructor takes IEnumerable<Rectangle>, so the array has to be of that element
        // type rather than of Area.
        var typed = Array.CreateInstance(RectangleType, obstacles.Length);
        for (var idx = 0; idx < obstacles.Length; idx++)
            typed.SetValue(obstacles[idx], idx);

        return (Area)Activator.CreateInstance(ObstructedType, Internals, null,
            [bounds, typed], null);
    }

    /// <summary>
    ///   The widest clear rectangle of that height at that position, or null where the area has
    ///   no room for one.
    /// </summary>
    internal static Area FittingRect(this Area area, double yPosition, double height)
    {
        var method = typeof(Area).GetMethod("GetFittingRect", Internals);
        // ReSharper disable once PossibleNullReferenceException
        return (Area)method.Invoke(area, [XUnit.FromPoint(yPosition), XUnit.FromPoint(height)]);
    }

    /// <summary>The union of two areas, which is always a plain rectangle.</summary>
    internal static Area UnitedWith(this Area area, Area other)
    {
        var method = typeof(Area).GetMethod("Unite", Internals);
        // ReSharper disable once PossibleNullReferenceException
        return (Area)method.Invoke(area, [other]);
    }

    /// <summary>The same area, lowered and made shorter by that much.</summary>
    internal static Area Lowered(this Area area, double verticalOffset)
    {
        var method = typeof(Area).GetMethod("Lower", Internals);
        // ReSharper disable once PossibleNullReferenceException
        return (Area)method.Invoke(area, [XUnit.FromPoint(verticalOffset)]);
    }

    /// <summary>Whether the area is one that carries obstacles.</summary>
    internal static bool IsObstructed(this Area area) => ObstructedType.IsInstanceOfType(area);

    /// <summary>The area's bounds as four points, for readable assertions.</summary>
    internal static (double X, double Y, double Width, double Height) Bounds(this Area area)
    {
        return (area.X.Point, area.Y.Point, area.Width.Point, area.Height.Point);
    }
}
