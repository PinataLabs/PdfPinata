using System;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Every key of every dictionary in the library is declared with a <c>KeyInfo</c> attribute
///   saying what type its value has, and <c>KeysMeta.GetValueType</c> turns that declaration into
///   the class to instantiate. It is what lets <c>Elements.GetValue(key, VCF.Create)</c> conjure a
///   missing entry rather than answer null.
///   <para>
///   Only two of the ten types it can name are actually creatable. Everything else - a name, a
///   string, a number, a date, a rectangle - is resolved to a type correctly and then refused by
///   the caller, which handles dictionaries and arrays and throws for the rest. That asymmetry is
///   the interesting half and is pinned below rather than left to be discovered.
///   </para>
/// </summary>
public class KeyValueTypeTests
{
    static PdfPage APage() => new PdfDocument().AddPage();

    // The keys are written out rather than taken from PdfPage.Keys, which is internal to the
    // package. They are the names in the file, so a literal is what a reader of this test wants
    // to see in any case.

    // ----- the two that can be created ---------------------------------------------------------

    [Fact]
    public void AMissingDictionaryKeyIsCreatedAsADictionary()
    {
        var page = APage();

        var created = page.Elements.GetValue("/Group", VCF.Create);

        created.Should().BeAssignableTo<PdfDictionary>();
        page.Elements.ContainsKey("/Group").Should().BeTrue();
    }

    [Fact]
    public void AMissingArrayKeyIsCreatedAsAnArray()
    {
        var page = APage();

        var created = page.Elements.GetValue("/B", VCF.Create);

        created.Should().BeAssignableTo<PdfArray>();
    }

    /// <summary>
    ///   A key whose <c>KeyInfo</c> names a class of its own gets that class rather than the bare
    ///   array or dictionary its type would otherwise give: <c>/Annots</c> is declared
    ///   <c>KeyType.Array</c> with <c>typeof(PdfAnnotations)</c>, and the declared class wins.
    /// </summary>
    [Fact]
    public void AKeyThatNamesItsOwnClassIsCreatedAsThatClass()
    {
        var page = APage();

        page.Elements.GetValue("/Annots", VCF.Create)
            .Should().BeOfType<PdfAnnotations>();
    }

    [Fact]
    public void AskingForAKeyThatIsAlreadyThereReturnsItRatherThanReplacingIt()
    {
        var page = APage();
        var first = page.Elements.GetValue("/Annots", VCF.Create);

        var second = page.Elements.GetValue("/Annots", VCF.Create);

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void AskingNotToCreateCreatesNothing()
    {
        var page = APage();

        page.Elements.GetValue("/Group", VCF.None).Should().BeNull();
        page.Elements.ContainsKey("/Group").Should().BeFalse();
    }

    [Fact]
    public void AnIndirectlyCreatedValueIsRegisteredWithTheDocument()
    {
        var page = APage();

        var created = page.Elements.GetValue("/Annots", VCF.CreateIndirect);

        ((PdfObject)created).Reference.Should().NotBeNull("an indirect object needs an object number");
        page.Elements["/Annots"].Should().BeOfType<PdfReference>();
    }

    // ----- the eight that cannot ---------------------------------------------------------------

    /// <summary>
    ///   The type is worked out correctly and then refused. <c>GetValue</c> handles dictionaries
    ///   and arrays and throws for everything else, so a key declared as a scalar cannot be
    ///   created at all - the caller has to write the value itself. Pinned rather than fixed: what
    ///   a created-but-unset integer or date should contain is a decision, not an oversight.
    /// </summary>
    [Theory]
    [InlineData("/Tabs", "a name")]
    [InlineData("/ID", "a string")]
    [InlineData("/StructParents", "an integer")]
    [InlineData("/Dur", "a real")]
    [InlineData("/LastModified", "a date")]
    [InlineData("/BleedBox", "a rectangle")]
    public void AKeyDeclaredAsAScalarCannotBeCreated(string key, string what)
    {
        var page = APage();

        var create = () => page.Elements.GetValue(key, VCF.Create);

        create.Should().Throw<NotImplementedException>("{0} is {1}", key, what);
    }

    /// <summary>
    ///   A key with no declaration has no type to create, and asking for one to be created
    ///   anyway is refused rather than answered with null. Which is defensible - the caller asked
    ///   for something impossible - though it makes <c>VCF.Create</c> unsafe to use with a key
    ///   the library does not know, such as one of an extension's own.
    /// </summary>
    [Fact]
    public void AKeyNothingKnowsAboutCannotBeCreatedAndSaysWhichKeyItWas()
    {
        var page = APage();

        var create = () => page.Elements.GetValue("/NoSuchKeyIsDeclared", VCF.Create);

        create.Should().Throw<NotImplementedException>()
            .WithMessage("*/NoSuchKeyIsDeclared*");
    }

    [Fact]
    public void AKeyNothingKnowsAboutIsSimplyAbsentWhenNothingIsToBeCreated()
    {
        APage().Elements.GetValue("/NoSuchKeyIsDeclared", VCF.None).Should().BeNull();
    }
}
