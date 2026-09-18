using System.Collections;
using System.Reflection;
using PdfPinata.Charting;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   Reaches the chart inside a <see cref="ChartFrame"/>. The frame takes charts through
///   <see cref="ChartFrame.Add"/> and offers no way to read them back, so the result of a mapping
///   cannot otherwise be looked at without drawing it.
/// </summary>
/// <remarks>
///   Reflection rather than <c>InternalsVisibleTo</c>, for the reason
///   <see cref="ParagraphIteratorProbe"/> gives: this repository does not use one. The field is
///   private rather than internal, so an <c>InternalsVisibleTo</c> would not have reached it
///   either. The awkwardness is kept here, in one file, and the tests above it read as though the
///   frame had a property.
/// </remarks>
internal static class MappedChartProbe
{
    static readonly FieldInfo ChartList = typeof(ChartFrame)
        .GetField("chartList", BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>The single chart the mapper put into the frame.</summary>
    internal static Chart In(ChartFrame frame)
    {
        var charts = (ArrayList)ChartList.GetValue(frame);

        return charts is { Count: 1 }
            ? (Chart)charts[0]
            : throw new AssertionException($"the frame holds {charts?.Count ?? 0} charts rather than one");
    }
}

/// <summary>Raised when the frame does not hold the one chart the mapper is expected to add.</summary>
internal sealed class AssertionException : System.Exception
{
    internal AssertionException(string message) : base(message)
    {
    }
}
