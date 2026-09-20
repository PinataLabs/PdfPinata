using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="ListInfo.NestingLevel"/> is the one property docs/specs/nested-lists.md adds: how
///   deep a list item is, one-based, so the tagger can build a list inside a list. This covers the
///   model half - that the property exists, defaults sensibly, round-trips through the markup, and
///   survives a clone and a style, same as any other generated property. The tree half - that the
///   tagger actually reads it - is in PinataLayout.Rendering.Tests.
/// </summary>
public class ListNestingLevelTests
{
    [Fact]
    public void AListItemThatNeverSetsALevelIsLevelOne()
    {
        // The default has to be the outermost level, or every document written before this existed
        // would change what it means without changing what it says.
        var listInfo = new ListInfo();

        listInfo.NestingLevel.Should().Be(1);
    }

    [Fact]
    public void ANestingLevelIsWhateverItWasSetTo()
    {
        var listInfo = new ListInfo { NestingLevel = 3 };

        listInfo.NestingLevel.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANestingLevelBelowOneIsRefused(int level)
    {
        // The scale starts at one, unlike MergeRight or MergeDown where zero is the meaningful
        // default - so there is no sensible reading of a level below it, and the tagger comparing
        // levels to decide deeper-or-shallower would otherwise nest a later, valid item underneath
        // it without anything ever failing.
        var listInfo = new ListInfo();

        var act = () => listInfo.NestingLevel = level;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ANestingLevelRoundTripsThroughTheMarkup()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.ListInfo.ListType = ListType.BulletList1;
        paragraph.Format.ListInfo.NestingLevel = 2;

        var text = DdlWriter.WriteToString(document);
        var again = DdlReader.DocumentFromString(text).LastSection.Elements[0] as Paragraph;

        // ReSharper disable once PossibleNullReferenceException
        again.Format.ListInfo.NestingLevel.Should().Be(2);
    }

    [Fact]
    public void ADocumentThatNeverSetsALevelWritesNothingForIt()
    {
        // A generated property that always wrote its default would grow every existing document's
        // markup for a value nobody asked for.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.ListInfo.ListType = ListType.BulletList1;

        var text = DdlWriter.WriteToString(document);

        text.Should().NotContain("NestingLevel");
    }

    [Fact]
    public void ANestingLevelSurvivesADeepCopy()
    {
        var listInfo = new ListInfo { NestingLevel = 4 };

        var clone = listInfo.Clone();

        clone.NestingLevel.Should().Be(4);
    }

    [Fact]
    public void ANestingLevelSurvivesCloningTheWholeParagraph()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.ListInfo.ListType = ListType.BulletList1;
        paragraph.Format.ListInfo.NestingLevel = 2;

        var clone = paragraph.Clone();

        clone.Format.ListInfo.NestingLevel.Should().Be(2);
    }

    [Fact]
    public void ANestingLevelFlattensDownFromAStyle()
    {
        // The same mechanism every other list property already goes through - VisitorBase.FlattenListInfo -
        // so a nested list can be a style decision rather than something set on every paragraph.
        var document = new Document();
        var style = document.Styles.AddStyle("Nested", "Normal");
        style.ParagraphFormat.ListInfo.ListType = ListType.BulletList2;
        style.ParagraphFormat.ListInfo.NestingLevel = 2;
        var paragraph = document.AddSection().AddParagraph("nested");
        paragraph.Style = "Nested";

        paragraph.Format.ListInfo.NestingLevel.Should().Be(1, "nothing is copied down until it is flattened");

        new PdfFlattenVisitor().Visit(document);

        paragraph.Format.ListInfo.NestingLevel.Should().Be(2);
    }

    [Fact]
    public void AParagraphKeepsItsOwnLevelWhereItDisagreesWithItsStyle()
    {
        var document = new Document();
        var style = document.Styles.AddStyle("Nested", "Normal");
        style.ParagraphFormat.ListInfo.ListType = ListType.BulletList2;
        style.ParagraphFormat.ListInfo.NestingLevel = 2;
        var paragraph = document.AddSection().AddParagraph("nested");
        paragraph.Style = "Nested";
        paragraph.Format.ListInfo.NestingLevel = 3;

        new PdfFlattenVisitor().Visit(document);

        paragraph.Format.ListInfo.NestingLevel.Should().Be(3);
    }
}
