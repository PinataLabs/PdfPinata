using System.Collections;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   An <see cref="XSeries"/> is walked by LINQ as well as by <c>foreach</c>.
/// </summary>
/// <remarks>
///   It had a public <c>GetEnumerator</c> and no <see cref="IEnumerable"/>, so <c>foreach</c> bound
///   to it by pattern and every LINQ operator, <c>Cast</c> included, failed to compile. It is
///   non-generic like every other collection in this package, and a blank is a null element.
/// </remarks>
public class XSeriesEnumerationTests
{
    [Fact]
    public void LinqSeesTheValuesOfAnXSeriesInOrder()
    {
        var xSeries = new XSeries();
        var first = xSeries.Add("A");
        var second = xSeries.Add("B");

        xSeries.Cast<XValue>().Should().Equal(first, second);
        xSeries.OfType<XValue>().Should().Equal(first, second);
    }

    [Fact]
    public void ABlankIsANullElementThatOfTypeLeavesOut()
    {
        var xSeries = new XSeries();
        var first = xSeries.Add("A");
        xSeries.AddBlank();
        var third = xSeries.Add("C");

        xSeries.Cast<XValue>().Should().Equal(first, null, third);
        xSeries.OfType<XValue>().Should().Equal(first, third);
    }

    [Fact]
    public void AnXSeriesIsEnumerableThroughTheInterface()
    {
        var xSeries = new XSeries();
        var value = xSeries.Add("A");

        IEnumerable enumerable = xSeries;

        enumerable.Cast<object>().Should().ContainSingle().Which.Should().BeSameAs(value);
    }
}
