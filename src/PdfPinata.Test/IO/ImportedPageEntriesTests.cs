using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Importing a page copied its resources, contents, boxes, rotation and annotations and left
///   every other entry of the page behind, so a page that came in composited as a transparency
///   group, measured in a larger user unit, or timed for a presentation lost it on the way.
///   What belongs to the page itself now comes along; what names a structure of the document it
///   came from does not, because that structure stays behind.
/// </summary>
public class ImportedPageEntriesTests
{
    /// <summary>
    ///   One page, carrying each entry a page import is to copy and one it is not. The page group
    ///   names as its colour space the very object the resources name, object 4, so whether it
    ///   was imported once or twice shows in the output. <paramref name="entries" /> is the rest
    ///   of the page dictionary, the group and the resources aside.
    /// </summary>
    private static byte[] SourceDocument(string entries = AllEntries)
    {
        return RawPdf.Build(
        [
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" +
            "/Resources<</ColorSpace<</CS0 4 0 R>>>>/Contents 5 0 R" +
            "/Group<</Type/Group/S/Transparency/CS 4 0 R/I true>>" + entries + ">>",
            "[/ICCBased 6 0 R]",
            RawPdf.Stream("", "/CS0 cs 1 0 0 sc 10 10 50 50 re f"),
            RawPdf.Stream("/N 3", "not really a profile")
        ]);
    }

    private const string AllEntries =
        "/UserUnit 2.5/Tabs/R/Trans<</Type/Trans/S/Dissolve/D 1.5>>/Dur 3/StructParents 0";

    public static TheoryData<string> ImportPaths => ["Add", "Insert", "InsertRange"];

    private static PdfDocument Imported(string path, Action<PdfPage> drawOn = null, string entries = AllEntries)
    {
        using var input = new MemoryStream(SourceDocument(entries));
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        // Written as PDF 1.4 unless the import raises it, so that a raise shows.
        var target = new PdfDocument { Version = 14 };
        switch (path)
        {
            case "Add":
                _ = target.AddPage(source.Pages[0]);
                break;
            case "Insert":
                _ = target.Pages.Insert(0, source.Pages[0]);
                break;
            default:
                target.Pages.InsertRange(0, source, 0, 1);
                break;
        }

        drawOn?.Invoke(target.Pages[0]);

        using var output = new MemoryStream();
        target.Save(output, false);
        output.Position = 0;
        return Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Modify);
    }

    [Theory]
    [MemberData(nameof(ImportPaths))]
    public void TheTransparencyGroupComesAlongAndSharesTheColourSpaceOfTheResources(string path)
    {
        var page = Imported(path).Pages[0];

        var group = page.Elements.GetDictionary("/Group");
        group.Should().NotBeNull("a page composited as a group looks different without one");
        group.Elements.GetName("/S").Should().Be("/Transparency");
        group.Elements.GetBoolean("/I").Should().BeTrue();

        var colourSpace = group.Elements["/CS"].Should().BeOfType<PdfReference>().Subject;
        var array = colourSpace.Value.Should().BeOfType<PdfArray>().Subject;
        array.Elements.GetName(0).Should().Be("/ICCBased");
        array.Elements.GetReference(1).Value.Should().BeOfType<PdfDictionary>();

        var resourceColourSpace = (PdfReference)page.Resources.Elements.GetDictionary("/ColorSpace")
            .Elements["/CS0"];
        colourSpace.ObjectID.Should().Be(resourceColourSpace.ObjectID,
            "the group and the resources named one colour space, and it is imported once");
    }

    [Theory]
    [MemberData(nameof(ImportPaths))]
    public void TheUserUnitTabOrderAndPresentationEntriesComeAlong(string path)
    {
        var page = Imported(path).Pages[0];

        page.Elements.GetReal("/UserUnit").Should().Be(2.5);
        page.Elements.GetName("/Tabs").Should().Be("/R");
        page.Elements.GetReal("/Dur").Should().Be(3);

        var transition = page.Elements.GetDictionary("/Trans");
        transition.Should().NotBeNull();
        transition.Elements.GetName("/S").Should().Be("/Dissolve");
        transition.Elements.GetReal("/D").Should().Be(1.5);
    }

    [Theory]
    [MemberData(nameof(ImportPaths))]
    public void TheKeyIntoTheStructureTreeOfTheOtherDocumentIsLeftBehind(string path)
    {
        Imported(path).Pages[0].Elements.ContainsKey("/StructParents").Should().BeFalse(
            "it names an entry in a parent tree that belongs to the document the page came from");
    }

    /// <summary>
    ///   A page group is transparency as far as PDF/A-1 is concerned, even on a page whose
    ///   content paints nothing translucent, and the group an imported page now brings with it
    ///   has to be refused under that claim as any other transparency is.
    /// </summary>
    [Theory]
    [InlineData(PdfAConformance.PdfA1B, true)]
    [InlineData(PdfAConformance.PdfA2B, false)]
    public void AnImportedTransparencyGroupIsRefusedUnderPdfA1Alone(PdfAConformance conformance, bool refused)
    {
        // The group alone, since a user unit or a tab order would raise the version past what
        // PDF/A-1 allows and be refused for that instead.
        using var input = new MemoryStream(SourceDocument(""));
        var source = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var target = new PdfDocument();
        target.Info.Title = "Imported page";
        target.Version = 14;
        target.Options.Conformance = conformance;
        _ = target.AddPage(source.Pages[0]);

        var saving = () => target.Save(new MemoryStream(), false);

        if (refused)
            saving.Should().Throw<InvalidOperationException>().WithMessage("*PDF/A-1*transparency group*");
        else
            saving.Should().NotThrow();
    }

    [Theory]
    [InlineData("/UserUnit 2.5", 16)]
    [InlineData("/Tabs/R", 15)]
    [InlineData("/Tabs/C", 15)]
    public void AnEntryNewerThanTheDocumentRaisesItsVersion(string entries, int version)
    {
        Imported("Add", entries: entries).Version.Should().BeGreaterThanOrEqualTo(version);
    }

    [Fact]
    public void AnImportedPageWithNoNewerEntryLeavesTheVersionAlone()
    {
        Imported("Add", entries: "").Version.Should().Be(14);
    }

    [Theory]
    [InlineData("/R", "/R")]
    [InlineData("/C", "/C")]
    [InlineData("/S", null)]
    public void StructureOrderIsDroppedBecauseTheStructureTreeStaysBehind(string tabs, string expected)
    {
        var page = Imported("Add", entries: "/Tabs" + tabs).Pages[0];

        if (expected == null)
            page.Elements.ContainsKey("/Tabs").Should().BeFalse(
                "structure order names a structure tree that importing a page does not bring along");
        else
            page.Elements.GetName("/Tabs").Should().Be(expected);
    }

    [Fact]
    public void DrawingWithTransparencyOnTheImportedPageKeepsTheGroupItBrought()
    {
        var page = Imported("Add", target =>
        {
            using var gfx = XGraphics.FromPdfPage(target);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(128, 255, 0, 0)), new XRect(10, 10, 20, 20));
        }).Pages[0];

        var group = page.Elements.GetDictionary("/Group");
        group.Elements["/CS"].Should().BeOfType<PdfReference>(
            "the page already had a group, so none is made to stand in its place");
        group.Elements.GetBoolean("/I").Should().BeTrue();
    }
}
