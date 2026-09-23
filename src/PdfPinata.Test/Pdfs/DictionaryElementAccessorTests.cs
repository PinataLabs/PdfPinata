using System;
using System.Collections;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using Xunit;
using PdfInt = PdfPinata.Pdf.PdfInteger;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The typed accessors of <see cref="PdfDictionary.DictionaryElements"/>. Every one of them has
///   the same four cases to answer for — the entry is missing, it holds the simple type, it holds
///   an indirect reference to the object form of that type, or it holds something else entirely —
///   and a <c>create</c> overload that writes a default rather than answering one.
///   <para>
///   <see cref="TypedElementAccessorTests"/> covers the three accessors that once disagreed with
///   their array siblings; this covers the rest of the surface, including the collection itself.
///   </para>
/// </summary>
public class DictionaryElementAccessorTests
{
    private static PdfDocument ADocument() => new();

    private static PdfDictionary ADictionary(PdfDocument document) => new(document);

    /// <summary>An indirect reference to a simple value, which is the third case throughout.</summary>
    private static PdfReference IndirectTo(PdfDocument document, PdfObject value)
    {
        document.Internals.AddObject(value);
        return value.Reference;
    }

    // ----- GetBoolean ---------------------------------------------------------------------------

    [Fact]
    public void AMissingBooleanIsFalseAndIsNotWrittenUnlessAskedFor()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetBoolean("/Flag").Should().BeFalse();
        dictionary.Elements.ContainsKey("/Flag").Should().BeFalse("reading must not write");
    }

    [Fact]
    public void CreatingAMissingBooleanWritesADefaultOne()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetBoolean("/Flag", create: true).Should().BeFalse();

        dictionary.Elements.ContainsKey("/Flag").Should().BeTrue();
        dictionary.Elements["/Flag"].Should().BeOfType<PdfBoolean>();
    }

    [Fact]
    public void AnIndirectBooleanObjectIsFollowedToItsValue()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements["/Flag"] = IndirectTo(document, new PdfBooleanObject(document, true));

        dictionary.Elements.GetBoolean("/Flag").Should().BeTrue();
    }

    [Fact]
    public void ABooleanThatIsSomethingElseIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/Flag", 1);

        var reading = () => dictionary.Elements.GetBoolean("/Flag");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void SetBooleanWritesADirectBoolean()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetBoolean("/Flag", true);

        dictionary.Elements["/Flag"].Should().BeOfType<PdfBoolean>();
        dictionary.Elements.GetBoolean("/Flag").Should().BeTrue();
    }

    // ----- GetInteger ---------------------------------------------------------------------------

    [Fact]
    public void CreatingAMissingIntegerWritesAZero()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetInteger("/Count", create: true).Should().Be(0);

        dictionary.Elements["/Count"].Should().BeOfType<PdfInt>();
    }

    [Fact]
    public void AnIndirectIntegerObjectIsFollowedToItsValue()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements["/Count"] = IndirectTo(document, new PdfIntegerObject(document, 42));

        dictionary.Elements.GetInteger("/Count").Should().Be(42);
    }

    [Fact]
    public void AnUnsignedIntegerIsReadAsAnInteger()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements["/Count"] = new PdfUInteger(7);

        dictionary.Elements.GetInteger("/Count").Should().Be(7);
    }

    [Fact]
    public void AnIntegerThatIsSomethingElseIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetName("/Count", "Two");

        var reading = () => dictionary.Elements.GetInteger("/Count");

        reading.Should().Throw<InvalidCastException>();
    }

    // ----- GetReal ------------------------------------------------------------------------------

    [Fact]
    public void CreatingAMissingRealWritesAZero()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetReal("/Width", create: true).Should().Be(0);

        dictionary.Elements["/Width"].Should().BeOfType<PdfReal>();
    }

    [Fact]
    public void AnIndirectRealObjectIsFollowedToItsValue()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements["/Width"] = IndirectTo(document, new PdfRealObject(document, 2.5));

        dictionary.Elements.GetReal("/Width").Should().Be(2.5);
    }

    [Fact]
    public void AnIntegerIsAPerfectlyGoodReal()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements.SetInteger("/Width", 3);
        dictionary.Elements["/Height"] = IndirectTo(document, new PdfIntegerObject(document, 4));

        dictionary.Elements.GetReal("/Width").Should().Be(3);
        dictionary.Elements.GetReal("/Height").Should().Be(4);
    }

    [Fact]
    public void ARealThatIsSomethingElseIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetName("/Width", "Wide");

        var reading = () => dictionary.Elements.GetReal("/Width");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void SetRealWritesADirectReal()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetReal("/Width", 1.25);

        dictionary.Elements["/Width"].Should().BeOfType<PdfReal>();
        dictionary.Elements.GetReal("/Width").Should().Be(1.25);
    }

    // ----- GetString and TryGetString -----------------------------------------------------------

    [Fact]
    public void AMissingStringIsEmptyAndCreatingItWritesAnEmptyOne()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetString("/Title").Should().BeEmpty();
        dictionary.Elements.ContainsKey("/Title").Should().BeFalse();

        dictionary.Elements.GetString("/Title", create: true).Should().BeEmpty();
        dictionary.Elements["/Title"].Should().BeOfType<PdfString>();
    }

    [Fact]
    public void AStringIsAlsoReadFromAnIndirectObjectAndFromANameEitherWay()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements["/Indirect"] = IndirectTo(document, new PdfStringObject(document, "text"));
        dictionary.Elements.SetName("/Name", "Helvetica");
        dictionary.Elements["/NameObject"] = IndirectTo(document, new PdfNameObject(document, "/Times"));

        dictionary.Elements.GetString("/Indirect").Should().Be("text");
        dictionary.Elements.GetString("/Name").Should().Be("/Helvetica");
        dictionary.Elements.GetString("/NameObject").Should().Be("/Times");
    }

    [Fact]
    public void AStringThatIsSomethingElseIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/Title", 1);

        var reading = () => dictionary.Elements.GetString("/Title");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void TryGetStringAnswersForEveryShapeAStringCanTake()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements.SetString("/Direct", "one");
        dictionary.Elements["/Indirect"] = IndirectTo(document, new PdfStringObject(document, "two"));
        dictionary.Elements.SetName("/Name", "Three");
        dictionary.Elements["/NameObject"] = IndirectTo(document, new PdfNameObject(document, "/Four"));
        dictionary.Elements.SetInteger("/Number", 5);

        dictionary.Elements.TryGetString("/Direct", out var direct).Should().BeTrue();
        direct.Should().Be("one");

        dictionary.Elements.TryGetString("/Indirect", out var indirect).Should().BeTrue();
        indirect.Should().Be("two");

        dictionary.Elements.TryGetString("/Name", out var name).Should().BeTrue();
        name.Should().Be("/Three");

        dictionary.Elements.TryGetString("/NameObject", out var nameObject).Should().BeTrue();
        nameObject.Should().Be("/Four");

        dictionary.Elements.TryGetString("/Missing", out var missing).Should().BeFalse();
        missing.Should().BeNull();

        dictionary.Elements.TryGetString("/Number", out var number).Should().BeFalse("a number is not a string");
        number.Should().BeNull();
    }

    [Fact]
    public void SetStringTakesAnEncodingOfItsOwn()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetString("/Raw", "abc", PdfStringEncoding.RawEncoding);

        dictionary.Elements["/Raw"].Should().BeOfType<PdfString>();
        dictionary.Elements.GetString("/Raw").Should().Be("abc");
    }

    // ----- GetName ------------------------------------------------------------------------------

    [Fact]
    public void AMissingNameIsEmpty()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetName("/Subtype").Should().BeEmpty();
    }

    [Fact]
    public void ANameIsReadThroughAReferenceAndOutOfANameObject()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        dictionary.Elements["/Subtype"] = IndirectTo(document, new PdfNameObject(document, "/Form"));

        dictionary.Elements.GetName("/Subtype").Should().Be("/Form");
    }

    [Fact]
    public void ANameThatIsSomethingElseIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/Subtype", 1);

        var reading = () => dictionary.Elements.GetName("/Subtype");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void SetNameAddsTheSlashItWasNotGiven()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetName("/A", "Plain");
        dictionary.Elements.SetName("/B", "/Slashed");
        dictionary.Elements.SetName("/C", "");

        dictionary.Elements.GetName("/A").Should().Be("/Plain");
        dictionary.Elements.GetName("/B").Should().Be("/Slashed");
        dictionary.Elements.GetName("/C").Should().Be("/");
    }

    [Fact]
    public void SetNameRefusesANullValue()
    {
        var dictionary = ADictionary(ADocument());

        var setting = () => dictionary.Elements.SetName("/A", null!);

        setting.Should().Throw<ArgumentNullException>();
    }

    // ----- GetRectangle -------------------------------------------------------------------------

    [Fact]
    public void AMissingRectangleIsEmptyAndCreatingItWritesOne()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetRectangle("/MediaBox").Should().NotBeNull();
        dictionary.Elements.ContainsKey("/MediaBox").Should().BeFalse();

        dictionary.Elements.GetRectangle("/MediaBox", create: true).Should().NotBeNull();
        dictionary.Elements.ContainsKey("/MediaBox").Should().BeTrue();
    }

    [Fact]
    public void ARectangleWrittenAsAnArrayOfFourIsReadBackAndReplacedInPlace()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var array = new PdfArray(document);
        array.Elements.Add(new PdfInt(1));
        array.Elements.Add(new PdfInt(2));
        array.Elements.Add(new PdfInt(11));
        array.Elements.Add(new PdfInt(22));
        dictionary.Elements["/MediaBox"] = array;

        var rectangle = dictionary.Elements.GetRectangle("/MediaBox");

        rectangle.X1.Should().Be(1);
        rectangle.Y1.Should().Be(2);
        rectangle.X2.Should().Be(11);
        rectangle.Y2.Should().Be(22);
        dictionary.Elements["/MediaBox"].Should().BeOfType<PdfRectangle>("the array is replaced by what it meant");
    }

    [Fact]
    public void ARectangleReadThroughAReferenceIsFollowed()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var array = new PdfArray(document);
        array.Elements.Add(new PdfInt(0));
        array.Elements.Add(new PdfInt(0));
        array.Elements.Add(new PdfInt(595));
        array.Elements.Add(new PdfInt(842));
        document.Internals.AddObject(array);
        dictionary.Elements["/MediaBox"] = array.Reference;

        dictionary.Elements.GetRectangle("/MediaBox").X2.Should().Be(595);
    }

    [Fact]
    public void SetRectangleWritesTheRectangleItself()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetRectangle("/MediaBox", new PdfRectangle(new XRect(0, 0, 100, 200)));

        dictionary.Elements.GetRectangle("/MediaBox").X2.Should().Be(100);
    }

    // ----- GetMatrix ----------------------------------------------------------------------------

    [Fact]
    public void AMissingMatrixIsTheIdentityAndCreatingItWritesALiteral()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetMatrix("/Matrix").Should().Be(new XMatrix());
        dictionary.Elements.ContainsKey("/Matrix").Should().BeFalse();

        dictionary.Elements.GetMatrix("/Matrix", create: true).Should().Be(new XMatrix());
        dictionary.Elements["/Matrix"].Should().BeOfType<PdfLiteral>();
    }

    [Fact]
    public void AMatrixWrittenAsAnArrayOfSixIsReadBack()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var array = new PdfArray(document);
        foreach (var value in new[] { 2, 0, 0, 2, 10, 20 })
            array.Elements.Add(new PdfInt(value));
        dictionary.Elements["/Matrix"] = array;

        dictionary.Elements.GetMatrix("/Matrix").Should().Be(new XMatrix(2, 0, 0, 2, 10, 20));
    }

    [Fact]
    public void AMatrixIsReadBackOutOfTheLiteralSetMatrixWrote()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetMatrix("/Matrix", new XMatrix(1, 2, 3, 4, 5, 6));

        dictionary.Elements["/Matrix"].Should().BeOfType<PdfLiteral>();
        dictionary.Elements.GetMatrix("/Matrix").Should().Be(new XMatrix(1, 2, 3, 4, 5, 6));
    }

    [Fact]
    public void AMatrixLiteralWithTooFewNumbersIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements["/Matrix"] = new PdfLiteral("[1 2 3]");

        var reading = () => dictionary.Elements.GetMatrix("/Matrix");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AMatrixLiteralWithSomethingThatIsNotANumberIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements["/Matrix"] = new PdfLiteral("[1 2 3 4 5 six]");

        var reading = () => dictionary.Elements.GetMatrix("/Matrix");

        reading.Should().Throw<InvalidCastException>();
    }

    [Fact]
    public void AMatrixThatIsNeitherAnArrayNorALiteralIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/Matrix", 1);

        var reading = () => dictionary.Elements.GetMatrix("/Matrix");

        reading.Should().Throw<InvalidCastException>();
    }

    // ----- GetDateTime --------------------------------------------------------------------------

    [Fact]
    public void AMissingDateIsTheDefaultOneItWasGiven()
    {
        var dictionary = ADictionary(ADocument());
        var fallback = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        dictionary.Elements.GetDateTime("/ModDate", fallback).Should().Be(fallback);
    }

    [Fact]
    public void ADateIsReadFromADateFromAStringAndFromAStringObject()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var written = new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        dictionary.Elements.SetDateTime("/Date", written);
        dictionary.Elements["/Text"] = new PdfString("D:20210203040506Z");
        dictionary.Elements["/Object"] = IndirectTo(document, new PdfStringObject(document, "D:20210203040506Z"));

        var fallback = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        dictionary.Elements.GetDateTime("/Date", fallback).Should().Be(written);
        dictionary.Elements.GetDateTime("/Text", fallback).Year.Should().Be(2021);
        dictionary.Elements.GetDateTime("/Object", fallback).Year.Should().Be(2021);
    }

    [Fact]
    public void ADateThatCannotBeParsedFallsBackRatherThanThrowing()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements["/ModDate"] = new PdfString("not a date at all");
        var fallback = new DateTime(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        dictionary.Elements.GetDateTime("/ModDate", fallback).Should().Be(fallback);
    }

    [Fact]
    public void AnEmptyDateStringIsTheFallbackToo()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements["/ModDate"] = new PdfString("");
        var fallback = new DateTime(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        dictionary.Elements.GetDateTime("/ModDate", fallback).Should().Be(fallback);
    }

    [Fact]
    public void ADateThatIsSomethingElseEntirelyIsRefused()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/ModDate", 1);
        var fallback = new DateTime(2000, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var reading = () => dictionary.Elements.GetDateTime("/ModDate", fallback);

        reading.Should().Throw<InvalidCastException>();
    }

    // ----- GetValue and the object accessors ----------------------------------------------------

    [Fact]
    public void CreatingAValueBuildsTheTypeTheMetaInformationNames()
    {
        var document = ADocument();

        var resources = document.AddPage().Elements.GetValue("/Resources", VCF.Create);

        resources.Should().BeOfType<PdfResources>();
    }

    [Fact]
    public void CreatingAValueIndirectlyPutsAReferenceInTheDictionary()
    {
        var document = ADocument();
        var page = document.AddPage();

        var annotations = page.Elements.GetValue("/Annots", VCF.CreateIndirect);

        annotations.Should().BeOfType<PdfAnnotations>();
        page.Elements["/Annots"].Should().BeOfType<PdfReference>();
    }

    [Fact]
    public void AValueWithNoMetaInformationCannotBeCreated()
    {
        var dictionary = ADictionary(ADocument());

        var creating = () => dictionary.Elements.GetValue("/Whatever", VCF.Create);

        creating.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void AskingForAMissingValueWithoutCreatingItAnswersNothing()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.GetValue("/Whatever").Should().BeNull();
    }

    [Fact]
    public void GetObjectFollowsAReferenceAndGetReferenceDoesNot()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var inner = ADictionary(document);
        document.Internals.AddObject(inner);
        dictionary.Elements["/Inner"] = inner.Reference;

        dictionary.Elements.GetObject("/Inner").Should().BeSameAs(inner);
        dictionary.Elements.GetDictionary("/Inner").Should().BeSameAs(inner);
        dictionary.Elements.GetReference("/Inner").Should().BeSameAs(inner.Reference);
        dictionary.Elements.GetArray("/Inner").Should().BeNull("a dictionary is not an array");
        dictionary.Elements.GetReference("/Missing").Should().BeNull();
    }

    [Fact]
    public void SetObjectRefusesAnIndirectObjectAndSetReferenceInsistsOnOne()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var indirect = ADictionary(document);
        document.Internals.AddObject(indirect);
        var direct = ADictionary(document);

        var settingIndirectAsObject = () => dictionary.Elements.SetObject("/A", indirect);
        settingIndirectAsObject.Should().Throw<ArgumentException>();

        var settingDirectAsReference = () => dictionary.Elements.SetReference("/B", direct);
        settingDirectAsReference.Should().Throw<ArgumentException>();

        dictionary.Elements.SetObject("/C", direct);
        dictionary.Elements["/C"].Should().BeSameAs(direct);

        dictionary.Elements.SetReference("/D", indirect);
        dictionary.Elements["/D"].Should().BeSameAs(indirect.Reference);

        dictionary.Elements.SetReference("/E", indirect.Reference);
        dictionary.Elements["/E"].Should().BeSameAs(indirect.Reference);

        var settingANullReference = () => dictionary.Elements.SetReference("/F", (PdfReference)null!);
        settingANullReference.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SetValueWritesTheItemItWasGiven()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.SetValue("/A", new PdfInt(3));

        dictionary.Elements.GetInteger("/A").Should().Be(3);
    }

    // ----- the collection itself ----------------------------------------------------------------

    [Fact]
    public void AKeyMustBeANameToBeAdded()
    {
        var dictionary = ADictionary(ADocument());

        var noKey = () => dictionary.Elements.Add(null!, new PdfInt(1));
        noKey.Should().Throw<ArgumentNullException>();

        var emptyKey = () => dictionary.Elements.Add("", new PdfInt(1));
        emptyKey.Should().Throw<ArgumentNullException>();

        var unslashedKey = () => dictionary.Elements.Add("A", new PdfInt(1));
        unslashedKey.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddingAnIndirectObjectStoresItsReferenceInstead()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var indirect = ADictionary(document);
        document.Internals.AddObject(indirect);

        dictionary.Elements.Add("/Inner", indirect);

        dictionary.Elements["/Inner"].Should().BeSameAs(indirect.Reference);
    }

    [Fact]
    public void APairCanBeAddedAsOne()
    {
        var dictionary = ADictionary(ADocument());

        dictionary.Elements.Add(new KeyValuePair<string, PdfItem>("/A", new PdfInt(1)));

        dictionary.Elements.GetInteger("/A").Should().Be(1);
    }

    [Fact]
    public void ClearEmptiesTheDictionary()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/A", 1);
        dictionary.Elements.SetInteger("/B", 2);

        dictionary.Elements.Clear();

        dictionary.Elements.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveSaysWhetherItRemovedAnything()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/A", 1);

        dictionary.Elements.Remove("/A").Should().BeTrue();
        dictionary.Elements.Remove("/A").Should().BeFalse();
    }

    [Fact]
    public void TryGetValueAnswersWhatIsThereAndNothingForWhatIsNot()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/A", 1);

        dictionary.Elements.TryGetValue("/A", out var present).Should().BeTrue();
        present.Should().BeOfType<PdfInt>();

        dictionary.Elements.TryGetValue("/B", out var absent).Should().BeFalse();
        absent.Should().BeNull();
    }

    [Fact]
    public void KeysNamesAndValuesAreEachACopyOfWhatIsThere()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/A", 1);
        dictionary.Elements.SetInteger("/B", 2);

        dictionary.Elements.Keys.Should().BeEquivalentTo(["/A", "/B"]);
        dictionary.Elements.KeyNames.Should().HaveCount(2);
        dictionary.Elements.Values.Should().HaveCount(2);
        dictionary.Elements.ContainsKey("/A").Should().BeTrue();
        dictionary.Elements.Count.Should().Be(2);
    }

    [Fact]
    public void TheCollectionDescribesItselfAsAWritableDictionaryOfNoFixedSize()
    {
        var elements = ADictionary(ADocument()).Elements;

        elements.IsReadOnly.Should().BeFalse();
        elements.IsFixedSize.Should().BeFalse();
        elements.IsSynchronized.Should().BeFalse();
        elements.SyncRoot.Should().BeNull();
    }

    [Fact]
    public void ThreeMembersOfTheCollectionInterfacesAreNotImplemented()
    {
        var elements = ADictionary(ADocument()).Elements;
        var pair = new KeyValuePair<string, PdfItem>("/A", new PdfInt(1));

        var removing = () => elements.Remove(pair);
        removing.Should().Throw<NotImplementedException>();

        var containing = () => elements.Contains(pair);
        containing.Should().Throw<NotImplementedException>();

        var copying = () => elements.CopyTo(new KeyValuePair<string, PdfItem>[1], 0);
        copying.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void TheDictionaryEnumeratesItsPairsEitherWayRound()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/A", 1);
        dictionary.Elements.SetInteger("/B", 2);

        var throughTheDictionary = new List<string>();
        foreach (var pair in dictionary)
            throughTheDictionary.Add(pair.Key);

        var throughTheElements = new List<string>();
        foreach (var pair in dictionary.Elements)
            throughTheElements.Add(pair.Key);

        var throughTheOldInterface = new List<object>();
        foreach (var entry in (IEnumerable)dictionary.Elements)
            throughTheOldInterface.Add(entry);

        throughTheDictionary.Should().BeEquivalentTo(["/A", "/B"]);
        throughTheElements.Should().BeEquivalentTo(["/A", "/B"]);
        throughTheOldInterface.Should().HaveCount(2);
        ((IEnumerable)dictionary).GetEnumerator().Should().NotBeNull();
    }

    [Fact]
    public void ADictionaryPrintsItselfWithItsKeysSorted()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.Elements.SetInteger("/B", 2);
        dictionary.Elements.SetInteger("/A", 1);

        var text = dictionary.ToString();

        text.Should().StartWith("<< ").And.EndWith(">>");
        text.IndexOf("/A", StringComparison.Ordinal).Should().BeLessThan(text.IndexOf("/B", StringComparison.Ordinal));
    }

    [Fact]
    public void ADictionaryIndexedByNameRefusesADirectDictionaryThatHasAStream()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var withStream = ADictionary(document);
        withStream.CreateStream([1, 2, 3]);

        var setting = () => dictionary.Elements[new PdfName("/Inner")] = withStream;

        setting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ADictionaryIndexedByNameTakesAnIndirectStreamAsAReference()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        var withStream = ADictionary(document);
        withStream.CreateStream([1, 2, 3]);
        document.Internals.AddObject(withStream);

        dictionary.Elements[new PdfName("/Inner")] = withStream;

        dictionary.Elements[new PdfName("/Inner")].Should().BeSameAs(withStream.Reference);
    }

    [Fact]
    public void NeitherIndexerTakesANullValue()
    {
        var dictionary = ADictionary(ADocument());

        var byString = () => dictionary.Elements["/A"] = null!;
        byString.Should().Throw<ArgumentNullException>();

        var byName = () => dictionary.Elements[new PdfName("/A")] = null!;
        byName.Should().Throw<ArgumentNullException>();
    }

    // ----- the stream ---------------------------------------------------------------------------

    [Fact]
    public void ADictionaryWillNotBeGivenASecondStream()
    {
        var dictionary = ADictionary(ADocument());
        dictionary.CreateStream([1, 2, 3]);

        var again = () => dictionary.CreateStream([4, 5, 6]);

        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ZippingAStreamWritesTheFilterAndTheNewLength()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        document.Internals.AddObject(dictionary);
        dictionary.CreateStream(new byte[500]);

        dictionary.Stream.Zip();

        dictionary.Elements.GetName(PdfDictionary.PdfStream.Keys.Filter).Should().Be("/FlateDecode");
        dictionary.Elements.GetInteger(PdfDictionary.PdfStream.Keys.Length).Should().Be(dictionary.Stream.Length);
        dictionary.Stream.Length.Should().BeLessThan(500);
    }

    [Fact]
    public void ZippingAnAlreadyFilteredStreamLeavesItAlone()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        document.Internals.AddObject(dictionary);
        dictionary.CreateStream(new byte[500]);
        dictionary.Stream.Zip();
        var once = dictionary.Stream.Length;

        dictionary.Stream.Zip();

        dictionary.Stream.Length.Should().Be(once);
    }

    [Fact]
    public void AStreamPrintsItsDecodedContent()
    {
        var document = ADocument();
        var dictionary = ADictionary(document);
        document.Internals.AddObject(dictionary);
        dictionary.CreateStream("BT ET"u8.ToArray());

        dictionary.Stream.ToString().Should().Be("BT ET");

        dictionary.Stream.Zip();
        dictionary.Stream.ToString().Should().Be("BT ET", "printing a stream unfilters it first");
    }
}
