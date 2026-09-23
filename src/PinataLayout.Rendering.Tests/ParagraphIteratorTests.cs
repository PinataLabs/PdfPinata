using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   The renderer lays a paragraph out by walking its leaves - the runs of text, the symbols, the
///   line breaks - rather than its tree, because a formatted run is a container of leaves and not
///   a thing that is drawn. It walks forwards to fill a line and backwards to break one further
///   back when what it placed did not fit, so the two directions have to agree about what the
///   leaves are and about their order.
/// </summary>
/// <remarks>
///   Promoted from PinataLayout 1.32's TestParagraphIterator, which built the two walks into strings
///   and returned them for a person to read. The walks are the part worth keeping; the reading is
///   what an assertion is for.
///
///   Worth pinning now rather than later: the iterator holds its position in an
///   <c>ArrayList</c> of boxed indices, and <c>docs/specs/legacy-collections-migration.md</c> has
///   it down for migration. These tests are what that change will be checked against.
/// </remarks>
public class ParagraphIteratorTests
{
    [Fact]
    public void TheLeavesOfAParagraphAreItsRunsInTheOrderTheyWereAdded()
    {
        var paragraph = Build();

        Described(ParagraphIteratorProbe.Leaves(paragraph))
            .Should().Equal("Text:once", "Character", "Text:upon", "Text:a", "Character", "Text:time");
    }

    [Fact]
    public void AFormattedRunIsDescendedIntoRatherThanCountedAsOneLeaf()
    {
        var paragraph = Build();

        // "upon" and "a" are the two runs inside the bold FormattedText. A walk that stopped at
        // the container would see one leaf there and lay the whole of it out as a single
        // unbreakable run.
        Described(ParagraphIteratorProbe.Leaves(paragraph))
            .Should().Contain(["Text:upon", "Text:a"]);
    }

    [Fact]
    public void WalkingBackFromTheLastLeafVisitsTheSameLeavesInReverse()
    {
        var paragraph = Build();

        var forwards = Described(ParagraphIteratorProbe.Leaves(paragraph));
        var backwards = Described(ParagraphIteratorProbe.LeavesInReverse(paragraph));

        backwards.Should().Equal(forwards.Reverse());
    }

    [Fact]
    public void AnEmptyParagraphHasNoLeafToStartFrom()
    {
        // Not the same as having one leaf that happens to be empty: the renderer asks for the
        // first leaf and has to be told there is none, rather than handed the collection itself.
        var paragraph = new Document().AddSection().AddParagraph();

        ParagraphIteratorProbe.HasLeaves(paragraph).Should().BeFalse();
    }

    [Fact]
    public void TheFirstLeafKnowsItIsFirstAndTheLastKnowsItIsLast()
    {
        var paragraph = Build();
        var count = ParagraphIteratorProbe.Leaves(paragraph).Count;

        // These two decide whether a line may be broken before or after what the renderer is
        // looking at, and both are answered by walking one step and seeing whether anything is
        // there - so a broken walk makes every leaf look like an end.
        ParagraphIteratorProbe.EndsAt(paragraph, 0).Should().Be((true, false));
        ParagraphIteratorProbe.EndsAt(paragraph, count - 1).Should().Be((false, true));
    }

    [Fact]
    public void ALeafInTheMiddleOfANestedRunIsNeitherFirstNorLast()
    {
        var paragraph = Build();

        // Leaf 2 is the first run inside the FormattedText. It is the first leaf of its own
        // container, which is the case the walk has to climb out of to answer correctly.
        ParagraphIteratorProbe.EndsAt(paragraph, 2).Should().Be((false, false));
    }

    /// <summary>
    ///   A paragraph with a nested run in the middle of it, so that the walk has to descend into
    ///   a container and climb back out again in both directions.
    /// </summary>
    private static Paragraph Build()
    {
        var paragraph = new Document().AddSection().AddParagraph();
        paragraph.AddText("once");
        paragraph.AddCharacter(SymbolName.Blank);

        var bold = paragraph.AddFormattedText("upon", TextFormat.Bold);
        bold.AddText("a");

        paragraph.AddCharacter(SymbolName.Blank);
        paragraph.AddText("time");
        return paragraph;
    }

    private static string[] Described(System.Collections.Generic.IReadOnlyList<DocumentObject> leaves)
    {
        return [..leaves.Select(ParagraphIteratorProbe.Describe)];
    }
}
