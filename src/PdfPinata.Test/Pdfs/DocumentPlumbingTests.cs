using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Two pieces of document plumbing that no test had reached: the version the catalog claims to
///   conform to, and the table that remembers which external documents a document has imported
///   content from.
/// </summary>
public class DocumentPlumbingTests
{
    // ----- PdfCatalog.Version ---------------------------------------------------------------------

    private static PdfCatalog ACatalog() => new PdfDocument().Internals.Catalog;

    /// <summary>
    ///   The field is declared as "1.3" and the constructor immediately raises it to "1.4", under
    ///   a comment reading "HACK in PdfCatalog". So the declared default is never what a document
    ///   actually claims, and 1.4 is the answer that matters.
    /// </summary>
    [Fact]
    public void ACatalogClaimsVersionOnePointFourUntilItIsToldOtherwise()
    {
        ACatalog().Version.Should().Be("1.4");
    }

    [Theory]
    [InlineData("1.3")]
    [InlineData("1.4")]
    public void TheTwoVersionsTheCatalogAcceptsAreKept(string version)
    {
        var catalog = ACatalog();

        catalog.Version = version;

        catalog.Version.Should().Be(version);
    }

    /// <summary>
    ///   A version this library knows about but will not claim. The three below 1.3 are older than
    ///   anything it writes, and 1.5 and 1.6 are newer than the catalog is willing to say - which
    ///   is a limitation rather than a defect, and is pinned rather than argued with because
    ///   raising it would change what every document declares.
    /// </summary>
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("1.2")]
    [InlineData("1.5")]
    [InlineData("1.6")]
    public void AVersionThisLibraryWillNotClaimIsRefusedAsUnsupported(string version)
    {
        var catalog = ACatalog();

        var assign = () => catalog.Version = version;

        assign.Should().Throw<InvalidOperationException>().WithMessage("*Unsupported*");
    }

    [Theory]
    [InlineData("1.7")]
    [InlineData("2.0")]
    [InlineData("1.30")]
    [InlineData("1,3")]
    [InlineData("1.3 ")]
    [InlineData(" 1.3")]
    [InlineData("PDF 1.3")]
    [InlineData("13")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsNotAVersionAtAll(string version)
    {
        var catalog = ACatalog();

        var assign = () => catalog.Version = version;

        assign.Should().Throw<ArgumentException>().Which.Should().NotBeOfType<InvalidOperationException>(
            "an unreadable version and an unsupported one are different complaints");
    }

    [Fact]
    public void ARefusedVersionLeavesTheCatalogSayingWhatItSaidBefore()
    {
        var catalog = ACatalog();
        catalog.Version = "1.4";

        catalog.Invoking(c => c.Version = "9.9").Should().Throw<ArgumentException>();

        catalog.Version.Should().Be("1.4");
    }

    // ----- PdfFormXObjectTable.DetachDocument ------------------------------------------------------

    /// <summary>
    ///   Reaches the form table, which is <c>internal sealed</c>, as is
    ///   <c>PdfDocument.DocumentHandle</c>. There is no public route: the only caller is
    ///   <c>PdfDocument.OnExternalDocumentFinalized</c>, which runs when an imported document is
    ///   finalized, and a test cannot ask for that to happen. This repository carries no
    ///   <c>InternalsVisibleTo</c>, so reflection it is.
    /// </summary>
    private static class FormTableProbe
    {
        private static readonly Type TableType = typeof(PdfDocument).Assembly
            .GetType("PdfPinata.Pdf.Advanced.PdfFormXObjectTable", throwOnError: true);

        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        internal static object For(PdfDocument owner) =>
            Activator.CreateInstance(TableType, Any, null, [owner], null);

        // ReSharper disable PossibleNullReferenceException
        internal static void Remember(object table, PdfDocument external) =>
            TableType.GetMethod("GetImportedObjectTable", Any, null,
                [typeof(PdfDocument)], null).Invoke(table, [external]);
        // ReSharper restore PossibleNullReferenceException

        // ReSharper disable PossibleNullReferenceException
        internal static void Detach(object table, PdfDocument external) =>
            TableType.GetMethod("DetachDocument", Any)
                .Invoke(table, [HandleOf(external)]);
        // ReSharper restore PossibleNullReferenceException

        // ReSharper disable PossibleNullReferenceException
        internal static int Count(object table) =>
            ((ICollection)TableType.GetField("_forms", Any).GetValue(table)).Count;
        // ReSharper restore PossibleNullReferenceException

        // ReSharper disable PossibleNullReferenceException
        private static object HandleOf(PdfDocument document) =>
            typeof(PdfDocument).GetProperty("Handle", Any).GetValue(document);
        // ReSharper restore PossibleNullReferenceException
    }

    [Fact]
    public void ADocumentImportedFromIsRemembered()
    {
        var table = FormTableProbe.For(new PdfDocument());

        FormTableProbe.Remember(table, new PdfDocument());

        FormTableProbe.Count(table).Should().Be(1);
    }

    [Fact]
    public void RememberingTheSameDocumentTwiceRemembersItOnce()
    {
        var table = FormTableProbe.For(new PdfDocument());
        var external = new PdfDocument();

        FormTableProbe.Remember(table, external);
        FormTableProbe.Remember(table, external);

        FormTableProbe.Count(table).Should().Be(1);
    }

    [Fact]
    public void DetachingADocumentForgetsThatOneAndKeepsTheRest()
    {
        // The point of the method: one imported document going away must not cost the others
        // their imported object tables, which hold the mapping from their object numbers to this
        // document's.
        var table = FormTableProbe.For(new PdfDocument());
        var first = new PdfDocument();
        var second = new PdfDocument();
        FormTableProbe.Remember(table, first);
        FormTableProbe.Remember(table, second);

        FormTableProbe.Detach(table, first);

        FormTableProbe.Count(table).Should().Be(1, "the second is still imported from");
    }

    [Fact]
    public void DetachingADocumentThatWasNeverImportedFromChangesNothing()
    {
        var table = FormTableProbe.For(new PdfDocument());
        FormTableProbe.Remember(table, new PdfDocument());

        FormTableProbe.Detach(table, new PdfDocument());

        FormTableProbe.Count(table).Should().Be(1);
    }

    [Fact]
    public void DetachingFromAnEmptyTableIsNotAnError()
    {
        var table = FormTableProbe.For(new PdfDocument());

        var detach = () => FormTableProbe.Detach(table, new PdfDocument());

        detach.Should().NotThrow();
    }

    [Fact]
    public void DetachingEveryDocumentEmptiesTheTable()
    {
        var table = FormTableProbe.For(new PdfDocument());
        var documents = Enumerable.Range(0, 3).Select(_ => new PdfDocument()).ToList();
        foreach (var document in documents)
            FormTableProbe.Remember(table, document);

        foreach (var document in documents)
            FormTableProbe.Detach(table, document);

        FormTableProbe.Count(table).Should().Be(0);
    }
}
