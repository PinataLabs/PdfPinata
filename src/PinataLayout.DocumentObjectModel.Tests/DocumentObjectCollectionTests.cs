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
    static DocumentElements AnEmptyCollection() => new Document().AddSection().Elements;

    static DocumentElements ACollectionOf(params string[] paragraphs)
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
