using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Border.Clear marks a border as cleared, which is not the same as leaving it unset: a cleared
///   border writes "Top = null" into the DDL so that it overrides whatever it would otherwise
///   inherit, while an unset one writes nothing at all.
///
///   The flag behind it, Border.fClear, is a plain bool rather than one of the DOM's nullable
///   values - it carries no [DV] attribute, so the reflection layer never sees it, and the only
///   assignment anywhere sets it true.
///
///   Borders.ClearAll and Shading.Clear work the same way and had the same defect, so they are
///   covered here too.
/// </summary>
public class BorderClearedTests
{
    static Borders ABordersObject() =>
        new Document().AddSection().AddParagraph("Hello").Format.Borders;

    [Fact]
    public void ABorderStartsOutNotCleared()
    {
        ABordersObject().Top.BorderCleared.Should().BeFalse();
    }

    [Fact]
    public void ClearingABorderMarksIt()
    {
        var borders = ABordersObject();

        borders.Top.Clear();

        borders.Top.BorderCleared.Should().BeTrue();
    }

    [Fact]
    public void ClearingOneBorderLeavesTheOthersAlone()
    {
        var borders = ABordersObject();

        borders.Top.Clear();

        borders.Bottom.BorderCleared.Should().BeFalse();
        borders.Left.BorderCleared.Should().BeFalse();
        borders.Right.BorderCleared.Should().BeFalse();
    }

    [Fact]
    public void AClearedBorderIsWrittenAsNullWhenItAlsoCarriesAValue()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Borders.Top.Width = 1;
        paragraph.Format.Borders.Top.Clear();

        DdlWriter.WriteToString(document).Should().Contain("Top = null");
    }

    /// <summary>
    ///   Being cleared is the whole of what a cleared border has to say, so it has to be written
    ///   even when it carries nothing else.
    ///
    ///   Everything that decides whether to serialize a border asks IsNull first, and that question
    ///   was answered from the border's value descriptors - Visible, Style, Width and Color. fClear
    ///   carries no [DV] attribute, so it is not among them and could not make the border look
    ///   non-null; a border that had only been cleared reported itself null, was skipped, and
    ///   Clear() did nothing. Border.IsNull now accounts for it, the way TabStops.IsNull always has.
    /// </summary>
    [Fact]
    public void AClearedBorderCarryingNothingElseIsStillWritten()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Borders.Top.Clear();

        DdlWriter.WriteToString(document).Should().Contain("Top = null");
    }

    [Fact]
    public void AClearedBorderCarryingNothingElseWritesNoEmptyBlock()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Borders.Top.Clear();

        // EndContent rolls the block back when nothing inside it was committed.
        DdlWriter.WriteToString(document).Should().NotContain("Top\r\n").And.NotContain("Top\n{");
    }

    [Fact]
    public void ClearingEachBorderInTurnIsWritten()
    {
        foreach (var name in new[] { "Top", "Left", "Bottom", "Right" })
        {
            var document = new Document();
            var borders = document.AddSection().AddParagraph("Hello").Format.Borders;

            ((Border)borders.GetValue(name)).Clear();

            DdlWriter.WriteToString(document).Should().Contain($"{name} = null", $"{name} was cleared");
        }
    }

    [Fact]
    public void ABorderThatWasNeverClearedIsNotWrittenAsNull()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello");

        DdlWriter.WriteToString(document).Should().NotContain("Top = null");
    }

    [Fact]
    public void AClearedBorderSurvivesADdlRoundTrip()
    {
        var document = new Document();
        document.AddSection().AddParagraph("Hello").Format.Borders.Top.Clear();

        var written = DdlWriter.WriteToString(document);
        var reread = DdlReader.DocumentFromString(written);

        // The parser answers "Top = null" by calling Clear() on the border, so the flag comes back.
        var paragraph = (Paragraph)reread.LastSection.Elements[0];
        paragraph.Format.Borders.Top.BorderCleared.Should().BeTrue();
        DdlWriter.WriteToString(reread).Should().Be(written);
    }

    [Fact]
    public void AClearedBorderIsNotNull()
    {
        var borders = ABordersObject();

        borders.Top.Clear();

        borders.Top.IsNull().Should().BeFalse("being cleared is a value in itself");
        borders.IsNull().Should().BeFalse("so the borders around it are not null either");
    }

    [Fact]
    public void SetNullClearsTheClearedFlagToo()
    {
        var borders = ABordersObject();
        borders.Top.Clear();

        borders.Top.SetNull();

        // DocumentObject.SetNull is documented as making IsNull() true afterwards.
        borders.Top.BorderCleared.Should().BeFalse();
        borders.Top.IsNull().Should().BeTrue();
    }

    [Fact]
    public void ClearingSurvivesACloneOfTheBorder()
    {
        var borders = ABordersObject();
        borders.Top.Clear();

        var clone = borders.Top.Clone();

        clone.BorderCleared.Should().BeTrue("Clone is a MemberwiseClone, which copies the flag");
    }

    // ------------------------------------------------- the same defect in Borders and Shading

    [Fact]
    public void ClearingAllBordersIsWritten()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Borders.ClearAll();

        paragraph.Format.Borders.BordersCleared.Should().BeTrue();
        paragraph.Format.Borders.IsNull().Should().BeFalse();
        DdlWriter.WriteToString(document).Should().Contain("Borders = null");
    }

    [Fact]
    public void ClearingShadingIsWritten()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("Hello");

        paragraph.Format.Shading.Clear();

        paragraph.Format.Shading.IsCleared.Should().BeTrue();
        paragraph.Format.Shading.IsNull().Should().BeFalse();
        DdlWriter.WriteToString(document).Should().Contain("Shading = null");
    }

    [Fact]
    public void ClearedBordersAndShadingSurviveADdlRoundTrip()
    {
        var document = new Document();
        var format = document.AddSection().AddParagraph("Hello").Format;
        format.Borders.ClearAll();
        format.Shading.Clear();

        var written = DdlWriter.WriteToString(document);

        DdlWriter.WriteToString(DdlReader.DocumentFromString(written)).Should().Be(written);
    }
}
