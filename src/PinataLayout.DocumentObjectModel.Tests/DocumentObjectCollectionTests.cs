using System;
using System.Collections;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Tables;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="DocumentObjectCollection"/> is what every list in the DOM is — a section's
///   elements, a table's rows, a paragraph's content. It is an <see cref="IList"/> as well as a
///   typed collection, and the untyped half is the one nothing in the library calls: it is there
///   for a caller binding the DOM to something that wants an <c>IList</c>.
/// </summary>
public class DocumentObjectCollectionTests
{
    private static DocumentElements AnEmptyCollection() => new Document().AddSection().Elements;

    private static DocumentElements ACollectionOf(params string[] paragraphs)
    {
        var section = new Document().AddSection();
        foreach (var text in paragraphs)
            section.AddParagraph(text);
        return section.Elements;
    }

    // ----- the typed half ---------------------------------------------------------------------------

    [Fact]
    public void AnEmptyCollectionHasNeitherAFirstNorALastObject()
    {
        var elements = AnEmptyCollection();

        elements.Count.Should().Be(0);
        elements.First.Should().BeNull();
        elements.LastObject.Should().BeNull();
        elements.IsNull().Should().BeTrue();
    }

    [Fact]
    public void ACollectionAnswersItsFirstAndLastObject()
    {
        var elements = ACollectionOf("one", "two", "three");

        ((Paragraph)elements.First).Elements.Count.Should().Be(1);
        elements.First.Should().BeSameAs(elements[0]);
        elements.LastObject.Should().BeSameAs(elements[2]);
        elements.IsNull().Should().BeFalse();
    }

    [Fact]
    public void AnObjectCanBeReplacedByIndexThroughTheUntypedList()
    {
        var elements = ACollectionOf("one", "two");
        var replacement = new Paragraph();

        ((DocumentObjectCollection)elements)[1] = replacement;

        elements[1].Should().BeSameAs(replacement);
    }

    [Fact]
    public void ACollectionIsClonedWithEverythingInIt()
    {
        var elements = ACollectionOf("one", "two");

        var clone = elements.Clone();
        clone.RemoveObjectAt(0);

        clone.Count.Should().Be(1);
        elements.Count.Should().Be(2, "the clone is a copy, not a view");
        clone[0].Should().NotBeSameAs(elements[1], "and everything in it is a copy too");
    }

    [Fact]
    public void ACollectionOfEmptyObjectsIsItselfEmpty()
    {
        var section = new Document().AddSection();
        section.Elements.Add(new Paragraph());

        section.Elements.IsNull().Should().BeTrue("a paragraph with nothing in it says nothing");
    }

    // ----- the untyped half -------------------------------------------------------------------------

    [Fact]
    public void TheCollectionDescribesItselfAsAWritableListOfNoFixedSize()
    {
        IList list = AnEmptyCollection();
        ICollection collection = AnEmptyCollection();

        list.IsReadOnly.Should().BeFalse();
        list.IsFixedSize.Should().BeFalse();
        collection.IsSynchronized.Should().BeFalse();
        collection.SyncRoot.Should().BeNull();
    }

    [Fact]
    public void TheUntypedListAddsFindsAndRemovesTheSameObjects()
    {
        IList list = AnEmptyCollection();
        var first = new Paragraph();
        var second = new Paragraph();

        list.Add(first).Should().Be(0);
        list.Insert(0, second);

        list.Contains(first).Should().BeTrue();
        list.IndexOf(first).Should().Be(1);
        list[0].Should().BeSameAs(second);

        list.Remove(second);

        list.Count.Should().Be(1);
        list.Contains(second).Should().BeFalse();
    }

    // The untyped members go through the typed ones, so an object reaching the collection through
    // an IList belongs to it exactly as one added directly does, and a collection that does more
    // on Add or InsertObject - Rows giving a row its cells, Styles checking a style - does it here
    // too. They used to go straight at the list underneath and did none of it.

    [Fact]
    public void AnObjectAddedThroughTheUntypedListBelongsToTheCollection()
    {
        var elements = AnEmptyCollection();
        var paragraph = new Paragraph();

        ((IList)elements).Add(paragraph);

        paragraph.Document.Should().BeSameAs(elements.Document, "it hangs from the collection");
    }

    [Fact]
    public void AnObjectInsertedThroughTheUntypedListBelongsToTheCollection()
    {
        var elements = ACollectionOf("one");
        var paragraph = new Paragraph();

        ((IList)elements).Insert(0, paragraph);

        paragraph.Document.Should().BeSameAs(elements.Document, "it hangs from the collection");
        elements[0].Should().BeSameAs(paragraph);
    }

    [Fact]
    public void AnObjectSetByIndexThroughTheUntypedListBelongsToTheCollection()
    {
        var elements = ACollectionOf("one", "two");
        var paragraph = new Paragraph();

        ((IList)elements)[1] = paragraph;

        paragraph.Document.Should().BeSameAs(elements.Document, "it hangs from the collection");
        elements[1].Should().BeSameAs(paragraph);
    }

    [Fact]
    public void TheUntypedListAnswersTheIndexOfWhatItAdded()
    {
        IList list = ACollectionOf("one", "two");

        list.Add(new Paragraph()).Should().Be(2);
        list.Add(new Paragraph()).Should().Be(3);
    }

    [Fact]
    public void ARowAddedThroughTheUntypedListGetsACellForEveryColumn()
    {
        var table = new Document().AddSection().AddTable();
        table.AddColumn("2cm");
        table.AddColumn("2cm");
        table.AddColumn("2cm");
        var row = new Row();

        ((IList)table.Rows).Add(row);

        row.Cells.Count.Should().Be(3, "a row has as many cells as its table has columns");
        row.Table.Should().BeSameAs(table);
    }

    [Fact]
    public void ARowInsertedThroughTheUntypedListGetsACellForEveryColumn()
    {
        var table = new Document().AddSection().AddTable();
        table.AddColumn("2cm");
        table.AddColumn("2cm");
        table.AddRow();
        var row = new Row();

        ((IList)table.Rows).Insert(0, row);

        row.Cells.Count.Should().Be(2);
        table.Rows[0].Should().BeSameAs(row);
    }

    [Fact]
    public void AnObjectAlreadyOwnedElsewhereIsNotTakenOverThroughTheUntypedListEither()
    {
        var paragraph = new Document().AddSection().AddParagraph("x");
        IList list = AnEmptyCollection();

        list.Invoking(l => l.Add(paragraph)).Should().Throw<ArgumentException>();
        list.Invoking(l => l.Insert(0, paragraph)).Should().Throw<ArgumentException>();
        list.Count.Should().Be(0);
    }

    [Fact]
    public void AStyleCollectionChecksWhatReachesItThroughTheUntypedList()
    {
        IList styles = new Document().Styles;

        styles.Invoking(l => l.Add(new Paragraph())).Should().Throw<InvalidOperationException>(
            "Styles.Add refuses anything that is not a style, whichever way it is reached");
    }

    /// <summary>
    ///   Removing a cell moves the ones after it one column to the left, and a cell caches which
    ///   column it is in. The typed removal tells the cells that moved to forget it.
    /// </summary>
    [Fact]
    public void RemovingThroughTheUntypedListTellsTheObjectsAfterItThatTheyMoved()
    {
        var table = new Document().AddSection().AddTable();
        table.AddColumn("1cm");
        table.AddColumn("2cm");
        table.AddColumn("3cm");
        var cells = table.AddRow().Cells;
        var first = cells[0];
        var second = cells[1];
        second.Column.Should().BeSameAs(table.Columns[1]);

        ((IList)cells).Remove(first);

        cells.Count.Should().Be(2);
        ((IList)cells)[0].Should().BeSameAs(second);
        second.Column.Should().BeSameAs(table.Columns[0], "it is in the first column now");
    }

    // ----- what an untyped list can be handed that the typed one cannot ------------------------------

    [Fact]
    public void SomethingThatIsNotADocumentObjectIsRefusedOnTheWayIn()
    {
        IList list = ACollectionOf("one");

        list.Invoking(l => l.Add("text")).Should().Throw<ArgumentException>();
        list.Invoking(l => l.Insert(0, 42)).Should().Throw<ArgumentException>();
        list.Invoking(l => l[0] = "text").Should().Throw<ArgumentException>();
        list.Count.Should().Be(1);
        list[0].Should().BeOfType<Paragraph>();
    }

    [Fact]
    public void SomethingThatIsNotADocumentObjectIsSimplyNotFound()
    {
        IList list = ACollectionOf("one");

        list.Contains("one").Should().BeFalse();
        list.IndexOf("one").Should().Be(-1);
        list.Remove("one");
        list.Count.Should().Be(1);
    }

    [Fact]
    public void RemovingAnObjectTheCollectionDoesNotHoldChangesNothing()
    {
        IList list = ACollectionOf("one");

        list.Remove(new Paragraph());

        list.Count.Should().Be(1);
    }

    [Fact]
    public void TheWholeCollectionCopiesIntoAnArrayOfItsOwn()
    {
        var elements = ACollectionOf("one", "two");

        var copy = new DocumentObject[2];
        elements.CopyTo(copy, 0);

        copy[0].Should().BeSameAs(elements[0]);
        copy[1].Should().BeSameAs(elements[1]);
    }

    [Fact]
    public void ACollectionEnumeratesWhatIsInIt()
    {
        var elements = ACollectionOf("one", "two");

        var seen = 0;
        foreach (var unused in elements)
            seen++;

        seen.Should().Be(2);
    }

    // ----- a typed collection of its own -------------------------------------------------------------

    [Fact]
    public void ATypedCollectionAnswersForItsOwnKindToo()
    {
        var table = new Document().AddSection().AddTable();
        table.AddColumn("2cm");
        table.AddRow();
        table.AddRow();

        table.Rows.Count.Should().Be(2);
        ((Row)table.Rows.First).Index.Should().Be(0);
        ((Row)table.Rows.LastObject).Index.Should().Be(1);
    }

    [Fact]
    public void AnObjectCanBeTakenOutByIndexAndTheRestCloseUp()
    {
        var elements = ACollectionOf("one", "two", "three");

        elements.RemoveObjectAt(1);

        elements.Count.Should().Be(2);
        ((Paragraph)elements[1]).Elements.Count.Should().Be(1);
    }

    [Fact]
    public void AnObjectCanBeInsertedWhereItIsWanted()
    {
        var elements = ACollectionOf("one", "three");
        var inserted = new Paragraph();

        elements.InsertObject(1, inserted);

        elements.Count.Should().Be(3);
        elements[1].Should().BeSameAs(inserted);
    }

    [Fact]
    public void AnObjectAlreadyOwnedElsewhereIsNotTakenOverTwice()
    {
        var first = new Document().AddSection();
        var paragraph = first.AddParagraph("x");
        var second = new Document().AddSection();

        var adding = () => second.Elements.Add(paragraph);

        adding.Should().Throw<ArgumentException>("an object belongs to one parent");
    }
}
