using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;
using static PdfPinata.Test.IO.SharedResourceFixtures;

namespace PdfPinata.Test.IO;

/// <summary>
///   Two places a page draws from that are not its content stream: the glyph procedures of a
///   Type 3 font, and the appearance streams of its annotations. Both are content streams in
///   their own right, both can name resources of the page, and neither is reached by following
///   the page's contents.
///   <para>
///   Pruning too much here is silent - the resource is gone and the page still opens - so every
///   test says what survived as well as what went, which is the only way to tell a pruner that
///   kept the right thing from one that kept everything.
///   </para>
/// </summary>
public class PruneCharProcsAndAppearancesTests
{
    // ----- Type 3 glyph procedures ------------------------------------------------------------

    [Fact]
    public void AnImageDrawnOnlyByAGlyphOfATypeThreeFontSurvives()
    {
        // The glyph is the only thing that draws Im1: the page's own content stream just sets a
        // font and shows a character. A pruner that read the page alone would drop it.
        var document = Open(PageDrawingAGlyphThatDrawsAnImage());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Im1");
    }

    [Fact]
    public void TheFontTheGlyphBelongsToSurvivesToo()
    {
        var document = Open(PageDrawingAGlyphThatDrawsAnImage());

        document.PruneUnusedResources();

        FontsOf(document.Pages[0]).Should().Equal("/T3");
    }

    [Fact]
    public void AnImageNoGlyphDrawsIsStillPruned()
    {
        // Said separately from the survival of Im1, because a pruner that gave up on the font and
        // kept everything would pass that test and fail this one.
        var document = Open(PageDrawingAGlyphThatDrawsAnImage());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().NotContain("/Im2");
    }

    // ----- annotation appearance streams -------------------------------------------------------

    [Fact]
    public void AnImageDrawnOnlyByAnAnnotationsAppearanceSurvives()
    {
        // The page's content stream draws nothing at all; everything visible comes from the
        // annotation, and the appearance has no resources of its own so it draws with the page's.
        var document = Open(PageWithAnAnnotationAppearance());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Contain("/Im1");
    }

    [Fact]
    public void AnImageNoAppearanceDrawsIsStillPruned()
    {
        var document = Open(PageWithAnAnnotationAppearance());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().NotContain("/Im2");
    }

    /// <summary>
    ///   An appearance that changes with the state of the annotation is a dictionary of one
    ///   stream per state rather than a single stream - which is what every tick box and radio
    ///   button in a form carries. Every state has to be read, because the one that is not
    ///   showing today is the one that shows tomorrow.
    /// </summary>
    [Fact]
    public void EveryStateOfAVaryingAppearanceIsRead()
    {
        var document = Open(PageWithAnAnnotationAppearancePerState());

        document.PruneUnusedResources();

        var kept = XObjectsOf(document.Pages[0]).ToList();
        kept.Should().Contain("/Im1", "the on state draws it");
        kept.Should().Contain("/Im2", "and the off state draws this one");
        kept.Should().NotContain("/Im3", "which nothing draws in any state");
    }

    [Fact]
    public void APageWithNoAnnotationsAtAllIsPrunedAsBefore()
    {
        // The early return, and a check that reading appearances did not change the ordinary case.
        var document = Open(PageDrawingThroughAFormWithoutResources());

        document.PruneUnusedResources();

        XObjectsOf(document.Pages[0]).Should().Equal("/Fm0", "/Im1");
    }

    // ----- helpers -----------------------------------------------------------------------------

    static PdfDocument Open(byte[] document) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(document), PdfDocumentOpenMode.Modify);

    static IEnumerable<string> XObjectsOf(PdfPage page) => NamesOf(page, "/XObject");

    static IEnumerable<string> FontsOf(PdfPage page) => NamesOf(page, "/Font");

    static IEnumerable<string> NamesOf(PdfPage page, string category)
    {
        var entries = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary(category);
        return entries == null
            ? Enumerable.Empty<string>()
            : entries.Elements.KeyNames.Select(name => name.Value).OrderBy(name => name);
    }
}
