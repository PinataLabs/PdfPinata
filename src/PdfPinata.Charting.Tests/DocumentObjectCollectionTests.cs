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
///   members that add, insert, remove or search are declared but not implemented, and are not
///   pinned here.
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

    /// <summary>
    ///   A blank is copied as what it is - a null in the same place - rather than dereferenced,
    ///   so the values after it keep their indices in the copy as they do in the original.
    /// </summary>
    [Fact]
    public void ACloneKeepsABlankWhereItWas()
    {
        var elements = new XSeriesElements();
        var a = elements.Add("A");
        elements.AddBlank();
        var c = elements.Add("C");

        var copy = elements.Clone();

        copy.Count.Should().Be(3);
        copy[1].Should().BeNull();
        copy[0].Should().BeOfType<XValue>().And.NotBeSameAs(a);
        copy[2].Should().BeOfType<XValue>().And.NotBeSameAs(c);
    }

    /// <summary>
    ///   And for a series' points, whose values can be read back: the ones either side of the
    ///   blank are copied as they were.
    /// </summary>
    [Fact]
    public void ACloneOfASeriesKeepsABlankBetweenItsValues()
    {
        var series = new Series();
        series.Add(1.0);
        series.AddBlank();
        series.Add(3.0);

        var copy = series.Elements.Clone();

        copy.Cast<Point>().Select(point => point?.Value).Should().Equal(1.0, null, 3.0);
    }

    /// <summary>
    ///   The same through a chart, which is how a caller meets it: a series holding a blank, and
    ///   the chart it belongs to copied whole.
    /// </summary>
    [Fact]
    public void AChartWhoseSeriesHoldsABlankCanStillBeCloned()
    {
        var chart = Charts.Of(ChartType.Line, 1.0, 2.0);
        chart.SeriesCollection[0].AddBlank();
        chart.SeriesCollection[0].Add(4.0);

        var copy = chart.Clone();

        var points = copy.SeriesCollection[0].Elements;
        points.Count.Should().Be(4);
        points[2].Should().BeNull();
        points[3].Value.Should().Be(4.0);
    }

    /// <summary>
    ///   A copied element belongs to the copy, as an added one belongs to the collection it was
    ///   added to - not to nothing, and not to the collection it was copied from.
    /// </summary>
    [Fact]
    public void ACloneIsTheParentOfEveryElementItCopied()
    {
        var elements = new XSeriesElements();
        elements.Add("A", "B");

        var copy = elements.Clone();

        copy.Cast<XValue>().Should().OnlyContain(value => value.Parent == copy);
    }

    [Fact]
    public void ACloneOfAnEmptyCollectionIsEmpty()
    {
        var copy = new XSeriesElements().Clone();

        copy.Count.Should().Be(0);
        copy.First.Should().BeNull();
    }
}
