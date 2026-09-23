using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Headings have to descend a level at a time, and a document claiming PDF/UA-1 is now held to it.
/// </summary>
/// <remarks>
///   <para>
///     ISO 14289-1 7.4.2. The heading levels are the outline a reader lets somebody jump around by,
///     so a document going straight from <c>/H1</c> to <c>/H3</c> leaves a hole in it: there is a
///     section three levels deep and nothing two levels deep containing it.
///   </para>
///   <para>
///     In PinataLayout this is <c>ParagraphFormat.OutlineLevel</c>, which is what a heading style sets —
///     so the mistake is nearly always in the styles rather than in anything that draws, and it is
///     made by reaching for Heading3 because of how it looks.
///   </para>
/// </remarks>
public class HeadingLevelTests
{
    /// <summary>A document claiming PDF/UA-1 whose headings are the levels given, in order.</summary>
    private static PdfDocumentRenderer Claiming(params int[] headingLevels)
    {
        var document = new Document();
        var section = document.AddSection();

        foreach (var level in headingLevels)
            section.AddParagraph("Heading at level " + level, "Heading" + level);

        section.AddParagraph("Body text, so that a heading has something under it.");

        var renderer = new PdfDocumentRenderer(true)
        {
            Document = document,
            TagContent = true,
            Language = "en-GB"
        };

        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = "Headings";
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;
        return renderer;
    }

    private static Action Saving(PdfDocumentRenderer renderer)
        => () => renderer.PdfDocument.Save(new MemoryStream(), false);

    // ----- what is refused ----------------------------------------------------------------------

    [Fact]
    public void AHeadingThatSkipsALevelIsRefused()
    {
        // The case the rule exists for, and the one a styles sheet falls into by choosing a heading
        // for its size.
        Saving(Claiming(1, 3)).Should().Throw<InvalidOperationException>()
            .WithMessage("*skips level 2*");
    }

    [Fact]
    public void SkippingSeveralLevelsSaysSoInThePlural()
    {
        // Worth its own test only because the message builds the list, and a message that reads
        // "skips levels 2 to 2" is the kind of thing nobody notices until a customer quotes it.
        Saving(Claiming(1, 4)).Should().Throw<InvalidOperationException>()
            .WithMessage("*skips levels 2 to 3*");
    }

    [Fact]
    public void StartingBelowLevelOneIsRefused()
    {
        // There is nothing before the first heading, so a document opening at /H2 skips /H1. This
        // is the case a walk that only compared adjacent headings would let through.
        Saving(Claiming(2)).Should().Throw<InvalidOperationException>()
            .WithMessage("*skips level 1*");
    }

    [Fact]
    public void TheMessageSaysWhereToFixIt()
    {
        Saving(Claiming(1, 3)).Should().Throw<InvalidOperationException>()
            .WithMessage("*OutlineLevel*", "the caller needs to be told what to change");
    }

    // ----- what is allowed ----------------------------------------------------------------------

    [Fact]
    public void DescendingOneLevelAtATimeIsAllowed()
    {
        Saving(Claiming(1, 2, 3)).Should().NotThrow();
    }

    [Fact]
    public void ComingBackUpAnyDistanceIsNotASkip()
    {
        // /H3 to /H1 closes two sections rather than inventing one, so it is not a skip and must not
        // be refused. A rule written as "the level may not change by more than one" would refuse it.
        Saving(Claiming(1, 2, 3, 1)).Should().NotThrow();
    }

    [Fact]
    public void RepeatingALevelIsNotASkip()
    {
        Saving(Claiming(1, 2, 2, 2)).Should().NotThrow();
    }

    [Fact]
    public void DescendingAgainAfterComingBackUpIsAllowed()
    {
        // The sequence that catches a rule which remembers the deepest level reached rather than
        // the last one: after coming back to /H1, going to /H2 is a descent of one.
        Saving(Claiming(1, 2, 3, 1, 2)).Should().NotThrow();
    }

    [Fact]
    public void ADocumentWithNoHeadingsAtAllIsAllowed()
    {
        Saving(Claiming()).Should().NotThrow();
    }

    [Fact]
    public void ADocumentThatClaimsNothingIsNotHeldToTheRule()
    {
        // The rule belongs to the PDF/UA claim. A document making no claim is not refused for
        // anything here, however its headings run.
        var renderer = Claiming(1, 3);
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.None;

        Saving(renderer).Should().NotThrow();
    }
}
