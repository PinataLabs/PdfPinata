using System;
using System.Collections;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;
using PdfInt = PdfPinata.Pdf.PdfInteger;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfArray.ArrayElements"/> as a list: the bounds each typed accessor checks before
///   it reads, the shapes it refuses, and the <see cref="IList{T}"/> surface the collection offers
///   around them.
/// </summary>
public class ArrayElementCollectionTests
{
    static PdfDocument ADocument() => new();

    static PdfArray AnArray(PdfDocument document, params PdfItem[] items)
    {
        var array = new PdfArray(document);
        foreach (var item in items)
            array.Elements.Add(item);
        return array;
    }

    // ----- the bounds check -----------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void EveryTypedAccessorChecksItsIndexBeforeItReads(int index)
    {
        var array = AnArray(ADocument(), new PdfInt(1));

        ((Action)(() => array.Elements.GetInteger(index))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => array.Elements.GetReal(index))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => array.Elements.GetString(index))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => array.Elements.GetName(index))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => array.Elements.GetObject(index))).Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----- what each accessor makes of what it finds ----------------------------------------------

    [Fact]
    public void ANullIsZeroAnEmptyStringAndAnEmptyName()
    {
        var array = AnArray(ADocument(), PdfNull.Value);

        array.Elements.GetInteger(0).Should().Be(0);
        array.Elements.GetReal(0).Should().Be(0);
        array.Elements.GetString(0).Should().BeEmpty();
        array.Elements.GetName(0).Should().BeEmpty();
    }

    [Fact]
    public void AnIntegerIsRefusedWhenItIsAName()
    {
        var array = AnArray(ADocument(), new PdfName("/Two"));

        var reading = () => array.Elements.GetInteger(0);

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AnIntegerObjectBehindAReferenceIsAPerfectlyGoodReal()
    {
        var document = ADocument();
        var integer = new PdfIntegerObject(document, 5);
        document.Internals.AddObject(integer);
        var array = AnArray(document, integer.Reference);

        array.Elements.GetReal(0).Should().Be(5);
    }

    [Fact]
    public void ARealIsRefusedWhenItIsAName()
    {
        var array = AnArray(ADocument(), new PdfName("/Wide"));

        var reading = () => array.Elements.GetReal(0);

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void ANameIsRefusedWhenItIsANumber()
    {
        var array = AnArray(ADocument(), new PdfInt(1));

        var reading = () => array.Elements.GetName(0);

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void GetArrayAnswersOnlyForAnArray()
    {
        var document = ADocument();
        var inner = new PdfArray(document);
        document.Internals.AddObject(inner);
        var dictionary = new PdfDictionary(document);
        document.Internals.AddObject(dictionary);
        var array = AnArray(document, inner.Reference, dictionary.Reference);

        array.Elements.GetArray(0).Should().BeSameAs(inner);
        array.Elements.GetArray(1).Should().BeNull("a dictionary is not an array");
        array.Elements.GetDictionary(1).Should().BeSameAs(dictionary);
        array.Elements.GetReference(0).Should().BeSameAs(inner.Reference);
    }

    // ----- the list surface -----------------------------------------------------------------------

    [Fact]
    public void AnArrayFindsRemovesAndClearsTheItemsItHolds()
    {
        var document = ADocument();
        var one = new PdfInt(1);
        var two = new PdfInt(2);
        var array = AnArray(document, one, two);

        array.Elements.Contains(one).Should().BeTrue();
        array.Elements.IndexOf(two).Should().Be(1);
        array.Elements.IndexOf(new PdfInt(3)).Should().Be(-1);

        array.Elements.Remove(one).Should().BeTrue();
        array.Elements.Remove(one).Should().BeFalse();
        array.Elements.Count.Should().Be(1);

        array.Elements.Clear();
        array.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void AnItemCanBeInsertedRemovedByIndexAndReplaced()
    {
        var array = AnArray(ADocument(), new PdfInt(1), new PdfInt(3));

        array.Elements.Insert(1, new PdfInt(2));
        array.Elements.GetInteger(1).Should().Be(2);

        array.Elements[1] = new PdfInt(9);
        array.Elements.GetInteger(1).Should().Be(9);

        array.Elements.RemoveAt(1);
        array.Elements.Count.Should().Be(2);
        array.Elements.GetInteger(1).Should().Be(3);
    }

    [Fact]
    public void AnItemCannotBeSetToNothing()
    {
        var array = AnArray(ADocument(), new PdfInt(1));

        var setting = () => array.Elements[0] = null!;

        setting.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddingAnIndirectObjectStoresItsReferenceInstead()
    {
        var document = ADocument();
        var dictionary = new PdfDictionary(document);
        document.Internals.AddObject(dictionary);
        var array = new PdfArray(document);

        array.Elements.Add(dictionary);

        array.Elements[0].Should().BeSameAs(dictionary.Reference);
    }

    [Fact]
    public void TheItemsCanBeCopiedIntoAnArrayOfTheirOwn()
    {
        var array = AnArray(ADocument(), new PdfInt(1), new PdfInt(2));

        var copy = new PdfItem[2];
        array.Elements.CopyTo(copy, 0);

        copy.Should().BeEquivalentTo(array.Elements.Items);
    }

    [Fact]
    public void TheCollectionDescribesItselfAsAWritableListOfNoFixedSize()
    {
        var elements = new PdfArray(ADocument()).Elements;

        elements.IsReadOnly.Should().BeFalse();
        elements.IsFixedSize.Should().BeFalse();
        elements.IsSynchronized.Should().BeFalse();
        elements.SyncRoot.Should().BeNull();
    }

    [Fact]
    public void AnArrayEnumeratesItsItemsEitherWayRound()
    {
        var array = AnArray(ADocument(), new PdfInt(1), new PdfInt(2));

        var typed = new List<PdfItem>();
        foreach (var item in array)
            typed.Add(item);

        var untyped = new List<object>();
        foreach (var item in (IEnumerable)array)
            untyped.Add(item);

        typed.Should().HaveCount(2);
        untyped.Should().HaveCount(2);
    }

    [Fact]
    public void AnArrayClonesItselfDeeplyEnoughToBeEditedApart()
    {
        var array = AnArray(ADocument(), new PdfInt(1), new PdfInt(2));

        var clone = array.Clone();
        clone.Elements.Add(new PdfInt(3));

        clone.Elements.Count.Should().Be(3);
        array.Elements.Count.Should().Be(2);
    }

    [Fact]
    public void AnArrayPrintsItselfInBrackets()
    {
        var array = AnArray(ADocument(), new PdfInt(1), new PdfName("/A"));

        array.ToString().Should().Be("[ 1 /A ]");
    }
}
