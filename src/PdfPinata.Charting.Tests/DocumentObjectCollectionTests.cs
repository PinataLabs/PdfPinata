using System.Collections;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The list underneath every collection in the chart object model.
/// </summary>
/// <remarks>
///   <c>DocumentObjectCollection</c> is what <see cref="SeriesCollection"/>,
///   <see cref="SeriesElements"/>, <see cref="XValues"/> and <see cref="XSeriesElements"/> all are,
///   so its members are how a caller rearranges a chart's data after building it. These reach it
///   through <see cref="XSeriesElements"/>, the one collection a caller can construct on its own,
///   and through a chart's <see cref="SeriesCollection"/>, the one every chart has.
///
///   Only the members the class implements are covered. Its non-generic <see cref="IList"/>
///   members that add, insert, remove or search are declared but not implemented, and a blank -
///   the null a series holds for a missing value - cannot yet be cloned; neither is pinned here.
/// </remarks>
public class DocumentObjectCollectionTests
{
    [Fact]
    public void AddingAnElementMakesTheCollectionItsParent()
    {
        var elements = new XSeriesElements();

        var value = elements.Add("A");

        elements.Count.Should().Be(1);
        elements[0].Should().BeSameAs(value);
        value.Parent.Should().BeSameAs(elements);
    }

    [Fact]
    public void AnEmptyCollectionHasNoFirstOrLastElement()
    {
        var elements = new XSeriesElements();

        elements.Count.Should().Be(0);
        elements.First.Should().BeNull();
        elements.LastObject.Should().BeNull();
    }

    [Fact]
    public void FirstAndLastObjectAreTheEndsOfTheCollection()
    {
        var elements = new XSeriesElements();
        var first = elements.Add("A");
        elements.Add("B");
        var last = elements.Add("C");

        elements.First.Should().BeSameAs(first);
        elements.LastObject.Should().BeSameAs(last);
    }

    /// <summary>
    ///   A blank is a null, and it takes a place in the collection like any value: it is counted,
    ///   and the elements after it keep their indices.
    /// </summary>
    [Fact]
    public void ABlankTakesItsPlaceAsANull()
    {
        var elements = new XSeriesElements();
        elements.Add("A");
        elements.AddBlank();
        var after = elements.Add("C");

        elements.Count.Should().Be(3);
        elements[1].Should().BeNull();
        elements.IndexOf(after).Should().Be(2);
        elements.LastObject.Should().BeSameAs(after);
    }

    [Fact]
    public void InsertingPutsTheElementAtTheIndexGiven()
    {
        var elements = new XSeriesElements();
        var a = elements.Add("A");
        var c = elements.Add("C");
        var b = new XValue("B");

        elements.InsertObject(1, b);

        elements.Cast<XValue>().Should().Equal(a, b, c);
        elements.IndexOf(b).Should().Be(1);
    }

    [Fact]
    public void IndexOfAnElementNotInTheCollectionIsMinusOne()
    {
        var elements = new XSeriesElements();
        elements.Add("A");

        elements.IndexOf(new XValue("A")).Should().Be(-1, "the search is by identity, not by value");
    }

    [Fact]
    public void RemovingAnElementClosesUpTheGap()
    {
        var elements = new XSeriesElements();
        var a = elements.Add("A");
        elements.Add("B");
        var c = elements.Add("C");

        elements.RemoveObjectAt(1);

        elements.Cast<XValue>().Should().Equal(a, c);
    }

    [Fact]
    public void ClearingEmptiesTheCollection()
    {
        var elements = new XSeriesElements();
        elements.Add("A", "B", "C");

        elements.Clear();

        elements.Count.Should().Be(0);
        elements.First.Should().BeNull();
    }

    [Fact]
    public void TheIndexerReplacesAnElementInPlace()
    {
        var elements = new XSeriesElements();
        elements.Add("A", "B");
        var replacement = new XValue("Z");

        elements[1] = replacement;

        elements.Count.Should().Be(2);
        elements[1].Should().BeSameAs(replacement);
    }

    [Fact]
    public void CopyToWritesTheElementsInOrderFromTheIndexGiven()
    {
        var elements = new XSeriesElements();
        var a = elements.Add("A");
        var b = elements.Add("B");
        var array = new object[3];

        elements.CopyTo(array, 1);

        array.Should().Equal(null, a, b);
    }

    [Fact]
    public void EnumeratingVisitsTheElementsInOrder()
    {
        var series = new Series();
        series.Add(1.0, 2.0, 3.0);

        series.Elements.Cast<Point>().Select(point => point.Value).Should().Equal(1.0, 2.0, 3.0);
    }

    /// <summary>
    ///   What <see cref="IList"/> says about the collection, and its indexer, which is the one
    ///   <see cref="IList"/> member that reads and writes the same list the typed members do.
    /// </summary>
    [Fact]
    public void AsAnIListTheCollectionIsWritableAndGrowable()
    {
        var elements = new XSeriesElements();
        var a = elements.Add("A");
        IList list = elements;
        var replacement = new XValue("Z");

        list.IsReadOnly.Should().BeFalse();
        list.IsFixedSize.Should().BeFalse();
        list.IsSynchronized.Should().BeFalse();
        list[0].Should().BeSameAs(a);

        list[0] = replacement;

        elements[0].Should().BeSameAs(replacement);
    }

    // ----- cloning -----

    /// <summary>
    ///   A clone is deep: every element is copied rather than shared, so a change to one side does
    ///   not reach the other, and the copy stands on its own with no parent.
    /// </summary>
    [Fact]
    public void ACloneCopiesEveryElementRatherThanSharingIt()
    {
        var series = new Series();
        series.Add(1.0, 2.0);
        var elements = series.Elements;

        var copy = elements.Clone();

        copy.Should().NotBeSameAs(elements);
        copy.Parent.Should().BeNull();
        copy.Cast<Point>().Select(point => point.Value).Should().Equal(1.0, 2.0);
        copy[0].Should().NotBeSameAs(elements[0]);

        copy[0].Value = 9.0;
        copy.Add(3.0);
        copy.RemoveObjectAt(1);

        elements.Cast<Point>().Select(point => point.Value).Should().Equal(1.0, 2.0);
        copy.Cast<Point>().Select(point => point.Value).Should().Equal(9.0, 3.0);
    }

    /// <summary>
    ///   The same through a chart: the series of a cloned chart are copies, so adding a value to
    ///   one of them leaves the original series as it was.
    /// </summary>
    [Fact]
    public void ACloneOfASeriesCollectionCopiesTheSeriesInIt()
    {
        var chart = Charts.OfSeries(ChartType.Column2D, new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 });
        var original = chart.SeriesCollection;

        var copy = original.Clone();

        copy.Count.Should().Be(2);
        copy[0].Should().NotBeSameAs(original[0]);
        copy[1].Elements[1].Value.Should().Be(4.0);

        copy[0].Add(9.0);

        original[0].Elements.Count.Should().Be(2);
        copy[0].Elements.Count.Should().Be(3);
    }

    /// <summary>
    ///   Cloned through the base type, a collection is still copied as what it really is, so
    ///   code that handles any chart collection gets back one of the same kind.
    /// </summary>
    [Fact]
    public void ACloneThroughTheBaseTypeIsStillTheDerivedCollection()
    {
        var series = new Series();
        series.Add(1.0, 2.0);
        DocumentObjectCollection elements = series.Elements;

        var copy = elements.Clone();

        copy.Should().BeOfType<SeriesElements>();
        copy.Should().NotBeSameAs(elements);
        copy.Cast<Point>().Select(point => point.Value).Should().Equal(1.0, 2.0);
    }

    [Fact]
    public void ACloneOfAnEmptyCollectionIsEmpty()
    {
        var copy = new XSeriesElements().Clone();

        copy.Count.Should().Be(0);
        copy.First.Should().BeNull();
    }
}
