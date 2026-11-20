using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Text: where each glyph is placed by the PDF's own widths and the text state, and what a device
/// is handed for each text render mode.
/// </summary>
/// <remarks>
/// The font <c>/F1</c> is a simple font whose widths start at the space: 250 for it, then 600
/// for <c>A</c> and 500 for <c>B</c>. Its glyphs are unit squares, so the corner of each square
/// is where the glyph was placed, in device space, where y is 100 less the page's.
/// </remarks>
public class TextTests
{
    private static Drawn Text(string content, SquareGlyphs glyphs = null,
        Action<PdfDocument, PdfDictionary> more = null)
    {
        glyphs ??= new SquareGlyphs();
        return Drawn.Page(content, (document, resources) =>
        {
            // Codes 32 to 66: the space, 32 codes nothing is said about, then A and B.
            var widths = new double[35];
            widths[0] = 250;
            widths[33] = 600;
            widths[34] = 500;
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 32, widths));
            more?.Invoke(document, resources);
        }, _ => glyphs);
    }

    private static XPoint[] Origins(Drawn drawn) => drawn.Device.Fills.Select(fill => Rounded(fill.Origin)).ToArray();

    private static XPoint Rounded(XPoint point) => new(Math.Round(point.X, 6), Math.Round(point.Y, 6));

    [Test]
    public void GlyphsArePlacedByTheWidthsThePdfStates()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td (AB) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(26, 50));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AGlyphIsScaledToTheTypeSize()
    {
        var glyph = Text("BT /F1 10 Tf 20 50 Td (A) Tj ET").Device.Fills.Single();

        glyph.ToString().Should().Be("fill nonzero M20,50 L30,50 L30,40 L20,40 Z rgb(0,0,0)");
    }

    [Test]
    public void AGlyphIsFilledInTheFillColourAndAlpha()
    {
        var drawn = Text("1 0 0 rg BT /F1 10 Tf (A) Tj ET");

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0));
    }

    [Test]
    public void CharacterSpacingIsAddedAfterEveryGlyph()
    {
        var drawn = Text("BT /F1 10 Tf 2 Tc 20 50 Td (AB) Tj (A) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(28, 50), new XPoint(35, 50));
    }

    [Test]
    public void WordSpacingIsAddedAfterTheSpaceAndAfterNothingElse()
    {
        var drawn = Text("BT /F1 10 Tf 5 Tw 20 50 Td (A B) Tj ET");

        // A is 6 wide; the space is 2.5 wide and takes the 5 of word spacing; then B.
        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(33.5, 50));
    }

    [Test]
    public void WordSpacingIsNotAddedAfterATwoByteCodeThatHappensToBeThirtyTwo()
    {
        var drawn = Drawn.Page("BT /F1 10 Tf 5 Tw 20 50 Td <002000210021> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Composite(document, "/Identity-H", 1000)),
            _ => new SquareGlyphs());

        // CID 32 draws nothing and advances by the default width of one em, and by no more.
        Origins(drawn).Should().Equal(new XPoint(30, 50), new XPoint(40, 50));
    }

    [Test]
    public void HorizontalScalingNarrowsTheGlyphsAndTheirAdvances()
    {
        var drawn = Text("BT /F1 10 Tf 50 Tz 2 Tc 20 50 Td (AB) Tj ET");

        // A is 6 wide and followed by 2 of spacing, and both are halved.
        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(24, 50));
        var glyph = drawn.Device.Fills.First();
        (glyph.At(1, 0).X - glyph.Origin.X).Should().BeApproximately(5, 1e-9);
        (glyph.Origin.Y - glyph.At(0, 1).Y).Should().BeApproximately(10, 1e-9, "the height is not scaled");
    }

    [Test]
    public void TheRiseLiftsTheGlyphOffTheBaseline()
    {
        var drawn = Text("BT /F1 10 Tf 3 Ts 20 50 Td (A) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(20, 47));
    }

    [Test]
    public void ANumberInATjArrayMovesTheTextBackByThatManyThousandths()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td [(A) -1000 (B) 200 (A)] TJ ET");

        // After A at 26; back by -1000 thousandths is forward by 10; after B at 41; back by 2.
        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(36, 50), new XPoint(39, 50));
    }

    [Test]
    public void TheLineOperatorsMoveToTheStartOfTheNextLine()
    {
        var drawn = Text("BT /F1 10 Tf 12 TL 10 80 Td (AB) Tj T* (A) Tj (A) ' 0 -15 TD (A) Tj T* (A) Tj ET");

        Origins(drawn).Should().Equal(
            new XPoint(10, 20), new XPoint(16, 20), // the first line, at y 80
            new XPoint(10, 32),                     // T*: down by the leading
            new XPoint(10, 44),                     // ': the same, then shows
            new XPoint(10, 59),                     // TD: down 15, and the leading is now 15
            new XPoint(10, 74));
    }

    [Test]
    public void TheDoubleQuoteSetsBothSpacingsMovesToTheNextLineAndShows()
    {
        var drawn = Text("BT /F1 10 Tf 12 TL 10 80 Td 5 2 (A B) \" (A) Tj ET");

        // A, 2 of character spacing; the space with 2 and the 5 of word spacing; B and 2.
        Origins(drawn).Should().Equal(new XPoint(10, 32), new XPoint(27.5, 32), new XPoint(34.5, 32));
    }

    [Test]
    public void TheTextMatrixPlacesAndTurnsTheText()
    {
        var drawn = Text("BT /F1 10 Tf 0 1 -1 0 30 40 Tm (AB) Tj ET");

        // Turned a quarter anticlockwise: the text runs up the page from (30, 40).
        Origins(drawn).Should().Equal(new XPoint(30, 60), new XPoint(30, 54));
    }

    [Test]
    public void TextIsSetUnderTheTransformInForce()
    {
        var drawn = Text("2 0 0 2 0 0 cm BT /F1 10 Tf 20 20 Td (AB) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(40, 60), new XPoint(52, 60));
    }

    [Test]
    public void SavingAndRestoringInsideATextObjectLeavesTheTextWhereItWas()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td q (A) Tj Q (A) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(26, 50));
    }

    [Test]
    public void EachTextObjectStartsAtTheOrigin()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td (A) Tj ET BT (A) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(0, 100));
    }

    [Test]
    public void ACompositeFontIsReadTwoBytesToACodeAndPlacedByItsW()
    {
        var glyphs = new SquareGlyphs();
        var drawn = Drawn.Page("BT /F1 10 Tf 20 50 Td <00010002000300090001> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1",
                TestFonts.Composite(document, "/Identity-H", 800, 1, new[] { 300.0, 400.0 }, 3, 5, 100)),
            _ => glyphs);

        // CID 1 is 3 wide and CID 2 is 4, by the run that lists them; CID 3 is 1, by the run
        // that gives 3 to 5 one width; CID 9 is in neither and takes the default of 8.
        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(23, 50), new XPoint(27, 50),
            new XPoint(28, 50), new XPoint(36, 50));
        glyphs.Asked.Should().Equal(1, 2, 3, 9);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AStringThatOpensWithAByteOrderMarkIsStillCodesAndNotText()
    {
        var glyphs = new SquareGlyphs();
        Drawn.Page("BT /F1 10 Tf <FEFF0041> Tj (\u00fe\u00ff\u0000B) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Composite(document, "/Identity-H", 1000)),
            _ => glyphs);

        glyphs.Asked.Should().Equal(0xFEFF, 0x41, 0x42);
    }

    [Test]
    public void ACMapThatIsNotReadYetIsReportedAndTheTextIsStillSet()
    {
        var drawn = Drawn.Page("BT /F1 10 Tf <0001> Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Composite(document, "/Identity-V", 1000)),
            _ => new SquareGlyphs());

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("UnsupportedFeature /F1");
    }

    [Test]
    public void TheSameGlyphIsHandedOverAsTheSamePathAndSaysSo()
    {
        var fills = Text("BT /F1 10 Tf (ABA) Tj 20 Tf (A) Tj ET").Device.Fills.ToArray();

        fills.Should().HaveCount(4);
        fills.Should().OnlyContain(fill => fill.Path.IsShared);
        fills[2].Path.Should().BeSameAs(fills[0].Path);
        fills[3].Path.Should().BeSameAs(fills[0].Path, "the type size is in the transform and not in the outline");
    }

    [Test]
    public void AGlyphIsAskedOfItsSourceOnce()
    {
        var glyphs = new SquareGlyphs();

        Text("BT /F1 10 Tf (ABAB) Tj (A) Tj ET", glyphs);

        glyphs.Asked.Should().Equal('A', 'B');
    }

    [Test]
    public void AGlyphThatDrawsNothingIsNotHandedOverAndTheTextStillAdvances()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td ( A) Tj ET");

        Origins(drawn).Should().Equal(new XPoint(22.5, 50));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void ACodeTheFontHasNoGlyphForIsReportedAndTheTextStillAdvances()
    {
        var drawn = Text("BT /F1 10 Tf 20 50 Td (\u0000A\u0000) Tj ET");

        // Code 0 is before the first code the widths cover and takes the missing width of 0.
        Origins(drawn).Should().Equal(new XPoint(20, 50));
        drawn.Problems.Should().Equal("MissingGlyph /F1");
        drawn.Result.Diagnostics.Single().Occurrences.Should().Be(2);
    }

    [Test]
    public void AFontThatStatesNoWidthsAdvancesByWhatItsGlyphSourceKnows()
    {
        var drawn = Text("BT /F2 10 Tf 20 50 Td (AB) Tj /F1 10 Tf (AB) Tj ET", new SquareGlyphs { Advance = 0.4 },
            (document, resources) => Drawn.Add(document, resources, "/Font", "/F2", TestFonts.WithoutWidths(document)));

        // /F2 says nothing, so each glyph advances by the source's 0.4 of an em. /F1 says 600
        // and 500, and the source is not asked.
        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(24, 50), new XPoint(28, 50), new XPoint(34, 50));
    }

    [Test]
    public void AFontThatStatesNoWidthsAndWhoseSourceKnowsNoneDoesNotAdvance()
    {
        var drawn = Text("BT /F2 10 Tf 20 50 Td (AB) Tj ET", more: (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F2", TestFonts.WithoutWidths(document)));

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(20, 50));
    }

    [Test]
    [Arguments(0, 1, 0, 0)]
    [Arguments(1, 0, 1, 0)]
    [Arguments(2, 1, 1, 0)]
    [Arguments(3, 0, 0, 0)]
    [Arguments(4, 1, 0, 1)]
    [Arguments(5, 0, 1, 1)]
    [Arguments(6, 1, 1, 1)]
    [Arguments(7, 0, 0, 1)]
    public void EachRenderModeFillsStrokesAndClipsAsItSays(int mode, int fills, int strokes, int clips)
    {
        var drawn = Text($"BT /F1 10 Tf {mode} Tr 20 50 Td (A) Tj ET");

        drawn.Device.Fills.Should().HaveCount(fills);
        drawn.Device.Strokes.Should().HaveCount(strokes);
        drawn.Device.Clips.Should().HaveCount(clips);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AModeThatFillsAndStrokesFillsFirst()
    {
        var drawn = Text("BT /F1 10 Tf 2 Tr (A) Tj ET");

        drawn.Device.Body.Select(line => line.Split(' ')[0]).Should().Equal("fill", "stroke");
    }

    [Test]
    public void AStrokedGlyphIsStrokedWithALineMeasuredInUserSpace()
    {
        var stroke = Text("2 0 0 2 0 0 cm 3 w [6 3] 1.5 d 0 0 1 RG BT /F1 10 Tf 1 Tr 20 20 Td (A) Tj ET").Device.Strokes.Single();

        // The outline is in its own space, a tenth of user space at this type size, and the
        // line state is taken there with it: 3 in user space is 0.3 of the outline's.
        stroke.DevicePath.Should().Be("M40,60 L60,60 L60,40 L40,40 Z");
        stroke.Stroke.Width.Should().BeApproximately(0.3, 1e-12);
        stroke.Stroke.DashPattern.Should().HaveCount(2);
        stroke.Stroke.DashPattern[0].Should().BeApproximately(0.6, 1e-12);
        stroke.Stroke.DashPattern[1].Should().BeApproximately(0.3, 1e-12);
        stroke.Stroke.DashPhase.Should().BeApproximately(0.15, 1e-12);
        (stroke.Stroke.Width * stroke.Transform.M11).Should().BeApproximately(6, 1e-9, "3 wide in a user space drawn at twice the size");
        stroke.Paint.Should().Be(new PagePaint(0, 0, 1));
    }

    [Test]
    public void AStrokedGlyphIsHandedOverAsTheSamePathAsWhenItIsFilled()
    {
        var drawn = Text("BT /F1 10 Tf 2 Tr (AA) Tj 1 Tr /F1 20 Tf 0 1 -1 0 50 50 Tm (A) Tj ET");

        // Set at two sizes and turned a quarter: the outline's space is user space scaled the
        // same in every direction each time, so the outline itself never has to change.
        var strokes = drawn.Device.Strokes.ToArray();
        strokes.Should().HaveCount(3);
        strokes.Should().OnlyContain(stroke => stroke.Path.IsShared);
        strokes.Should().OnlyContain(stroke => ReferenceEquals(stroke.Path, drawn.Device.Fills.First().Path));
        strokes[1].Stroke.Should().BeSameAs(strokes[0].Stroke, "glyphs set at one size share one line state");
        strokes[2].Stroke.Width.Should().BeApproximately(0.05, 1e-12);
    }

    [Test]
    public void AStrokedGlyphStretchedByTzIsCopiedIntoUserSpaceBecauseItsPenIsNoLongerRound()
    {
        var stroke = Text("3 w BT /F1 10 Tf 50 Tz 1 Tr 20 20 Td (A) Tj ET").Device.Strokes.Single();

        stroke.DevicePath.Should().Be("M20,80 L25,80 L25,70 L20,70 Z");
        stroke.Stroke.Width.Should().Be(3);
        stroke.Transform.M11.Should().Be(1, "the outline is in user space, so the transform is the page's");
        stroke.Path.IsShared.Should().BeFalse();
    }

    [Test]
    public void AType3FontsWidthsAreInGlyphSpaceAndAreTakenToTextSpaceByItsFontMatrix()
    {
        // ISO 32000-1 9.6.5. A width of 50 under a matrix of a hundredth is half an em, which
        // is 5 at 10 points and not the 0.5 that reading it as thousandths makes it.
        var drawn = Text("BT /T3 10 Tf (A) Tj /F1 10 Tf (A) Tj ET", more: (document, resources) =>
        {
            var font = new PdfDictionary(document);
            font.Elements.SetName("/Type", "/Font");
            font.Elements.SetName("/Subtype", "/Type3");
            font.Elements.SetInteger("/FirstChar", 65);
            font.Elements.SetInteger("/LastChar", 65);
            font.Elements["/Widths"] = new PdfArray(document, new PdfReal(50));
            font.Elements["/FontMatrix"] = new PdfArray(document, new PdfReal(0.01), new PdfReal(0), new PdfReal(0),
                new PdfReal(0.01), new PdfReal(0), new PdfReal(0));
            Drawn.Add(document, resources, "/Font", "/T3", font);
        });

        Origins(drawn).Should().Equal(new XPoint(0, 100), new XPoint(5, 100));
    }

    [Test]
    public void AType3FontWithNoFontMatrixHasItsWidthsReadAsThousandths()
    {
        var drawn = Text("BT /T3 10 Tf (A) Tj /F1 10 Tf (A) Tj ET", more: (document, resources) =>
        {
            var font = TestFonts.Simple(document, 65, 500);
            font.Elements.SetName("/Subtype", "/Type3");
            Drawn.Add(document, resources, "/Font", "/T3", font);
        });

        Origins(drawn).Should().Equal(new XPoint(0, 100), new XPoint(5, 100));
    }

    [Test]
    public void ACodeOutsideTheWidthsAdvancesByTheDescriptorsMissingWidth()
    {
        // B is before /FirstChar, so it advances by /MissingWidth: 3 at 10 points. A is 6.
        var drawn = Drawn.Page("BT /F2 10 Tf (BAA) Tj ET", (document, resources) =>
        {
            var font = TestFonts.Simple(document, 65, 600);
            var descriptor = new PdfDictionary(document);
            descriptor.Elements.SetReal("/MissingWidth", 300);
            font.Elements["/FontDescriptor"] = descriptor;
            Drawn.Add(document, resources, "/Font", "/F2", font);
        }, _ => new SquareGlyphs());

        Origins(drawn).Should().Equal(new XPoint(0, 100), new XPoint(3, 100), new XPoint(9, 100));
    }

    [Test]
    public void TheGlyphsOfAClipModeNarrowTheClipTogetherWhenTheTextObjectEnds()
    {
        var drawn = Text("BT /F1 10 Tf 7 Tr 20 50 Td (AB) Tj ET 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(
            "clip nonzero M20,50 L30,50 L30,40 L20,40 Z M26,50 L36,50 L36,40 L26,40 Z",
            "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
    }

    [Test]
    public void ATextClipIsUndoneByTheRestoreThatMatchesTheSaveBeforeIt()
    {
        var drawn = Text("q BT /F1 10 Tf 7 Tr (A) Tj ET Q");

        drawn.Device.Body.Select(line => line.Split(' ')[0]).Should().Equal("save", "clip", "restore");
    }

    [Test]
    public void ATextObjectLeftOpenStillClipsWhenTheContentEnds()
    {
        var drawn = Text("BT /F1 10 Tf 7 Tr (A) Tj");

        drawn.Device.Clips.Should().ContainSingle();
    }

    [Test]
    public void AClipModeThatShowsNoGlyphDoesNotClipTheRestOfThePageAway()
    {
        var drawn = Text("BT /F1 10 Tf 7 Tr ( ) Tj ET 0 0 5 5 re f");

        drawn.Device.Clips.Should().BeEmpty();
        drawn.Device.Fills.Should().ContainSingle();
    }

    [Test]
    public void InvisibleTextAsksForNoOutlinesAndIsNoProblemEvenInAFontThatCannotBeDrawn()
    {
        var glyphs = new SquareGlyphs();
        Text("BT /F1 10 Tf 3 Tr (AB) Tj ET", glyphs).Result.IsComplete.Should().BeTrue();
        glyphs.Asked.Should().BeEmpty();

        var undrawable = Drawn.Page("BT /F1 10 Tf 3 Tr (AB) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600, 500)));
        undrawable.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AFontWithNoGlyphSourceIsReportedOnceAndItsTextIsNotDrawn()
    {
        var drawn = Drawn.Page("BT /F1 10 Tf (AB) Tj (A) Tj 7 Tr (A) Tj ET 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600, 500)));

        drawn.Device.Body.Should().Equal("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Problems.Should().Equal("UnsupportedFont /F1");
        drawn.Result.Diagnostics.Single().Occurrences.Should().Be(3);
        drawn.Result.Diagnostics.Single().Message.Should().Contain("no embedded font program");
    }

    [Test]
    [Arguments("/FontFile2", "TrueType")]
    [Arguments("/FontFile3", "CFF")]
    [Arguments("/FontFile", "Type 1")]
    public void AFontWithAFontProgramThatIsNotReadYetSaysWhichKindItIs(string key, string kind)
    {
        var drawn = Drawn.Page("BT /F1 10 Tf (A) Tj ET", (document, resources) =>
        {
            var font = TestFonts.Simple(document, 65, 600);
            var descriptor = new PdfDictionary(document);
            var program = new PdfDictionary(document);
            program.CreateStream([0]);
            document.Internals.AddObject(program);
            descriptor.Elements[key] = program.Reference;
            font.Elements["/FontDescriptor"] = descriptor;
            Drawn.Add(document, resources, "/Font", "/F1", font);
        });

        drawn.Result.Diagnostics.Single().Message.Should().Contain(kind);
    }

    [Test]
    public void AFontTheResourcesDoNotHoldIsReportedWhereItIsSelected()
    {
        var drawn = Text("BT /F9 10 Tf (AB) Tj ET");

        drawn.Device.Fills.Should().BeEmpty();
        drawn.Problems.Should().Equal("MissingResource /F9");
    }

    [Test]
    public void TextShownBeforeAnyFontIsSelectedIsReported()
    {
        Text("BT (AB) Tj ET").Problems.Should().Equal("OperatorOutOfPlace Tj");
    }

    [Test]
    public void TextShownOutsideATextObjectIsReportedAndStillSet()
    {
        var drawn = Text("/F1 10 Tf 20 50 Td (A) Tj");

        Origins(drawn).Should().Equal(new XPoint(20, 50));
        drawn.Problems.Should().Equal("OperatorOutOfPlace Td");
    }

    [Test]
    public void AGraphicsStateDictionaryMaySelectTheFont()
    {
        var drawn = Text("/GS1 gs BT 20 50 Td (AB) Tj ET", more: (document, resources) =>
        {
            var font = TestFonts.Simple(document, 65, 1000, 1000);
            document.Internals.AddObject(font);
            var state = new PdfDictionary(document);
            state.Elements["/Font"] = new PdfArray(document, font.Reference, new PdfReal(20));
            Drawn.Add(document, resources, "/ExtGState", "/GS1", state);
        });

        Origins(drawn).Should().Equal(new XPoint(20, 50), new XPoint(40, 50));
    }

    [Test]
    public void AGlyphSourceThatThrowsIsReportedAsAGlyphTheFontDoesNotHave()
    {
        var drawn = Drawn.Page("BT /F1 10 Tf (A) Tj ET 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600)),
            _ => new Throwing());

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("MissingGlyph /F1");
        drawn.Result.Diagnostics.Single().Message.Should().Be("The glyph table is cut short.");
    }

    [Test]
    public void AFontTheCallersSourcesDeclineIsLeftToTheLibrary()
    {
        var drawn = Drawn.Page("BT /F1 10 Tf (A) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600)),
            _ => null);

        drawn.Problems.Should().Equal("UnsupportedFont /F1");
    }

    [Test]
    public void FontsReadWithTheCallersSourcesAreNotKeptForALaterRenderOfTheDocument()
    {
        var page = Drawn.PageOf("BT /F1 10 Tf (A) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600)));

        var withSquares = new RecordingDevice();
        PageRendering.Render(page, withSquares, glyphSources: _ => new SquareGlyphs());
        var withTheLibrarysOwn = new RecordingDevice();
        var result = PageRendering.Render(page, withTheLibrarysOwn);

        withSquares.Fills.Should().ContainSingle();
        withTheLibrarysOwn.Fills.Should().BeEmpty();
        result.Diagnostics.Single().Problem.Should().Be(PageRenderProblem.UnsupportedFont);
    }

    [Test]
    public void AFontThatCannotBeDrawnIsReportedByEveryRenderThatShowsIt()
    {
        var page = Drawn.PageOf("BT /F1 10 Tf (A) Tj ET", (document, resources) =>
            Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600)));

        var first = PageRendering.Render(page, new RecordingDevice());
        var second = PageRendering.Render(page, new RecordingDevice());

        // The font is read once and kept on the document; what it cannot do is still said each time.
        second.Diagnostics.Select(d => d.ToString()).Should().Equal(first.Diagnostics.Select(d => d.ToString()));
        second.IsComplete.Should().BeFalse();
    }

    private sealed class Throwing : PageGlyphSource
    {
        public override PagePath GetOutline(int code) => throw new InvalidOperationException("The glyph table is cut short.");
    }
}
