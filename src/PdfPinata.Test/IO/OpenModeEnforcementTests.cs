using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   What each open mode lets a caller do, asserted one cell at a time.
///   <para>
///   <see cref="PdfDocument.CanModify"/> used to answer true unconditionally, with the real check
///   commented out beside it, so the operations that guarded on it enforced nothing: a caller who
///   opened a document <see cref="PdfDocumentOpenMode.ReadOnly"/> and added a page got the page and
///   found out only later, if at all, that none of it would be written. This is the matrix that
///   makes the mode a specified thing rather than an emergent one, and the place a newly guarded
///   operation is added. The fifth mode that used to be in
///   it, <c>InformationOnly</c>, was removed from the enum rather than specified; the number it had
///   is pinned vacant by <c>IncrementalUpdateTests</c>.
///   </para>
///   <para>
///   Each refusal is asserted on its message as well as its type. Naming both the mode the document
///   was opened with and the operation that needed a different one is the point of the change; a
///   test that checked only for <see cref="InvalidOperationException"/> would pass against the
///   message this replaced, which named neither.
///   </para>
/// </summary>
public class OpenModeEnforcementTests
{
    /// <summary>An operation that changes a document, and the words its refusal has to use.</summary>
    private sealed class Mutation
    {
        internal Mutation(string operation, Action<PdfDocument, PdfPage> act)
        {
            Operation = operation;
            Act = act;
        }

        /// <summary>The gerund phrase the message names, as the caller wrote the call.</summary>
        internal string Operation { get; }

        /// <summary>The document under test, and a page belonging to another document.</summary>
        internal Action<PdfDocument, PdfPage> Act { get; }
    }

    /// <summary>
    ///   A mutation that adds a page returns it, and the matrix looks only at whether the call was allowed. A lambda
    ///   whose own parameter is <c>_</c> cannot say <c>_ =</c>: that would assign to the parameter.
    /// </summary>
    private static void Discard(PdfPage _)
    {
    }

    /// <summary>
    ///   Everything that changes a document, keyed by the call a caller would write. Reached by name
    ///   rather than passed as a delegate so that each cell of the matrix is named in the test run.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Mutation> Mutations = new Dictionary<string, Mutation>
    {
        ["PdfDocument.AddPage()"] =
            new("adding a page", (document, _) => Discard(document.AddPage())),
        ["PdfDocument.AddPage(page)"] =
            new("adding a page", (document, foreign) => _ = document.AddPage(foreign)),
        ["PdfDocument.InsertPage(index)"] =
            new("inserting a page", (document, _) => Discard(document.InsertPage(0))),
        ["PdfDocument.PlacePage"] =
            new("placing a page", (document, _) => document.PlacePage(0, new PdfPage(document))),
        ["PdfDocument.ImportPage"] =
            new("importing a page", (document, foreign) => _ = document.ImportPage(0, foreign)),
        ["PdfDocument.DuplicatePage"] =
            new("duplicating a page", (document, _) => Discard(document.DuplicatePage(0, 1))),
        ["PdfDocument.MovePage"] =
            new("moving a page", (document, _) => document.MovePage(0, 1)),
        ["PdfDocument.Pages.Add()"] =
            new("adding a page", (document, _) => Discard(document.Pages.Add())),
        ["PdfDocument.Pages.RemoveAt"] =
            new("removing a page", (document, _) => document.Pages.RemoveAt(0)),
        ["PdfDocument.Pages.Remove"] =
            new("removing a page", (document, _) => document.Pages.Remove(document.Pages[0])),
        ["PdfDocument.Pages.InsertRange"] =
            new("inserting a range of pages",
                (document, foreign) => document.Pages.InsertRange(0, foreign.Owner, 0, 1)),
        ["XGraphics.FromPdfPage"] =
            new("drawing on a page", (document, _) => XGraphics.FromPdfPage(document.Pages[0]).Dispose()),
        ["PdfDocument.Save"] =
            new("saving the document", (document, _) => document.Save(new MemoryStream(), false)),
        ["PdfDocument.Version"] =
            new("setting the PDF version", (document, _) => document.Version = 17),
        ["PdfDocument.PageLayout"] =
            new("setting the page layout",
                (document, _) => document.PageLayout = PdfPageLayout.TwoColumnLeft),
        ["PdfDocument.PageMode"] =
            new("setting the page mode",
                (document, _) => document.PageMode = PdfPageMode.UseOutlines),
        ["PdfDocument.Language"] =
            new("setting the document language", (document, _) => document.Language = "en-GB"),
        ["PdfDocument.ResizePages"] =
            new("resizing a page", (document, _) => document.ResizePages(PageSize.A5)),
        ["PdfDocument.GetOrCreateAcroForm"] =
            new("creating an interactive form", (document, _) => document.GetOrCreateAcroForm())
    };

    /// <summary>The modes that let a document be changed.</summary>
    private static readonly PdfDocumentOpenMode[] Modifiable =
    [
        PdfDocumentOpenMode.Modify,
        PdfDocumentOpenMode.Append
    ];

    /// <summary>The modes that do not.</summary>
    private static readonly PdfDocumentOpenMode[] NotModifiable =
    [
        PdfDocumentOpenMode.ReadOnly,
        PdfDocumentOpenMode.Import
    ];

    public static TheoryData<PdfDocumentOpenMode, string> RefusedCells() => Cells(NotModifiable);

    public static TheoryData<PdfDocumentOpenMode, string> AllowedCells() => Cells(Modifiable);

    private static TheoryData<PdfDocumentOpenMode, string> Cells(PdfDocumentOpenMode[] modes)
    {
        var cells = new TheoryData<PdfDocumentOpenMode, string>();
        foreach (var mode in modes)
            foreach (var call in Mutations.Keys)
                cells.Add(mode, call);
        return cells;
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void AModeThatCannotModifyRefusesEveryOperationThatWould(PdfDocumentOpenMode mode, string call)
    {
        var mutation = Mutations[call];
        var document = OpenedWith(mode);

        var act = () => mutation.Act(document, AForeignPage());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*PdfDocumentOpenMode.{mode}*")
            .And.Message.Should().Contain(mutation.Operation);
    }

    [Theory]
    [MemberData(nameof(AllowedCells))]
    public void AModeThatCanModifyAllowsEveryOneOfThem(PdfDocumentOpenMode mode, string call)
    {
        var mutation = Mutations[call];
        var document = OpenedWith(mode);

        var act = () => mutation.Act(document, AForeignPage());

        act.Should().NotThrow();
    }

    /// <summary>
    ///   The refusal says which mode was used and which are needed, because the mistake is nearly
    ///   always at the call to Open rather than at the operation that reports it.
    /// </summary>
    [Fact]
    public void ARefusalNamesTheModeUsedAndTheModesNeeded()
    {
        var document = OpenedWith(PdfDocumentOpenMode.ReadOnly);

        Action act = () => _ = document.AddPage();

        act.Should().Throw<InvalidOperationException>().WithMessage(
            "This document was opened with PdfDocumentOpenMode.ReadOnly and adding a page needs a "
            + "document opened with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.");
    }

    /// <summary>
    ///   Closing is not changing. It is deliberately outside the matrix above: this writes only when
    ///   the document was constructed on an output stream, which a document read by PdfReader never
    ///   is, so there is nothing here for a read-only document to be refused.
    /// </summary>
    [Theory]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.Append)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    [InlineData(PdfDocumentOpenMode.Import)]
    public void CloseIsAllowedInEveryMode(PdfDocumentOpenMode mode)
    {
        var document = OpenedWith(mode);

        var act = () => document.Close();

        act.Should().NotThrow();
    }

    /// <summary>
    ///   Incremental save asks a narrower question than the rest: Modify can change a document and
    ///   still cannot be appended to, because opening that way renumbers every object.
    /// </summary>
    [Theory]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    [InlineData(PdfDocumentOpenMode.Import)]
    public void OnlyAppendCanBeSavedIncrementally(PdfDocumentOpenMode mode)
    {
        var document = OpenedWith(mode);

        var act = () => document.SaveIncremental(new MemoryStream());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*PdfDocumentOpenMode.{mode}*")
            .And.Message.Should().Contain("PdfDocumentOpenMode.Append");
    }

    [Fact]
    public void AppendCanBeSavedIncrementally()
    {
        var document = OpenedWith(PdfDocumentOpenMode.Append);
        document.Info.Title = "Changed";

        var act = () => document.SaveIncremental(new MemoryStream());

        act.Should().NotThrow();
    }

    /// <summary>
    ///   A document built in memory was never opened at all, so a message naming the mode it was
    ///   opened with would name the enum's default and be a lie.
    /// </summary>
    [Fact]
    public void ADocumentThatWasCreatedRatherThanOpenedIsToldSo()
    {
        var document = new PdfDocument();
        _ = document.AddPage();

        var act = () => document.SaveIncremental(new MemoryStream());

        act.Should().Throw<InvalidOperationException>().WithMessage("*created rather than opened*");
    }

    /// <summary>
    ///   Reading is what the read-only modes are for, and none of it is affected.
    /// </summary>
    [Theory]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    [InlineData(PdfDocumentOpenMode.Import)]
    public void AModeThatCannotModifyStillReads(PdfDocumentOpenMode mode)
    {
        var document = OpenedWith(mode);

        document.PageCount.Should().Be(2);
        document.Pages[0].Width.Point.Should().BeApproximately(595, 1);
        document.Info.Title.Should().Be("Two pages");
        document.Version.Should().BeGreaterThan(0);
        document.PageMode.Should().Be(PdfPageMode.UseNone);
    }

    /// <summary>
    ///   Extraction is what Import is for, and it is the one mode that permits it. The page comes
    ///   out; what may not happen is a change to the document it came out of.
    /// </summary>
    [Fact]
    public void ImportExtractsPagesIntoADocumentThatMayBeChanged()
    {
        var source = OpenedWith(PdfDocumentOpenMode.Import);
        var target = new PdfDocument();

        _ = target.AddPage(source.Pages[0]);

        target.PageCount.Should().Be(1);
    }

    /// <summary>
    ///   The other half of the same rule, and the one that was already enforced: a page can only be
    ///   imported <em>from</em> a document opened with Import.
    /// </summary>
    [Fact]
    public void APageCannotBeImportedFromADocumentNotOpenedForImport()
    {
        var source = OpenedWith(PdfDocumentOpenMode.ReadOnly);
        var target = new PdfDocument();

        Action act = () => _ = target.AddPage(source.Pages[0]);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PdfDocumentOpenMode.Import*");
    }

    private static PdfDocument OpenedWith(PdfDocumentOpenMode mode) => Saved.Open(TwoPageDocument(), mode);

    /// <summary>A page of a separate document opened for import, for the operations that take one.</summary>
    private static PdfPage AForeignPage()
    {
        return OpenedWith(PdfDocumentOpenMode.Import).Pages[0];
    }

    private static byte[] TwoPageDocument()
    {
        var document = new PdfDocument();
        document.Info.Title = "Two pages";
        for (var index = 0; index < 2; index++)
        {
            var page = document.AddPage();
            page.Size = PageSize.A4;
        }

        return Saved.Bytes(document);
    }
}
