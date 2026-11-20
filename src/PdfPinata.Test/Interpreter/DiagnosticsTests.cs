using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Malformed content: what cannot be drawn is skipped, the reason is on the result, and the rest
/// of the page is drawn. Nothing about the content throws.
/// </summary>
/// <remarks>
/// Each test hands its content to <see cref="Interruptibly"/> under a timeout, because what a
/// change to the lexer or the parser does to malformed content is as likely to be a loop that
/// never ends as a wrong answer.
/// </remarks>
public class DiagnosticsTests
{
    private const string Square = "fill nonzero M0,100 L5,100 L5,95 L0,95 Z ";

    private static Task<Drawn> Page(string content, Action<PdfDocument, PdfDictionary> resources = null) =>
        Interruptibly.Run(() => Drawn.Page(content, resources));

    [Test, Timeout(30_000)]
    public async Task AnOperatorWithTooFewOperandsIsSkippedAndTheRestIsDrawn()
    {
        var drawn = await Page("1 0 rg 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("WrongOperands rg");
    }

    [Test, Timeout(30_000)]
    public async Task AnOperatorWithAnOperandOfTheWrongKindIsSkipped()
    {
        var drawn = await Page("(wide) w 0 /Name 1 rg [1] 2 3 4 re 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("WrongOperands w", "WrongOperands rg", "WrongOperands re");
    }

    // The entries of an inline image used to be read a second time from their start when the first
    // reading met a fault, so a stray delimiter among them was reported more often than elsewhere.
    [Test, Timeout(30_000)]
    [Arguments(")")]
    [Arguments(">")]
    public async Task AStrayDelimiterAmongAnInlineImagesEntriesIsReportedAsOftenAsAnywhereElse(string stray)
    {
        var elsewhere = await Page($"{stray} 0 0 5 5 re f");

        var drawn = await Page($"BI /W 1 {stray} ID a\nEI 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("UnreadableContent page", "UnknownOperator a");
        drawn.Result.Diagnostics[0].Occurrences.Should().Be(elsewhere.Result.Diagnostics.Single().Occurrences);
    }

    [Test, Timeout(30_000)]
    public async Task AnOperatorGivenMoreOperandsThanItTakesIsDrawnFromItsOwn()
    {
        var drawn = await Page("7 7 1 0 0 rg 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(1,0,0)");
    }

    [Test, Timeout(30_000)]
    public async Task AFaultInTheContentIsReadPastAndWhatComesBeforeAndAfterItIsDrawn()
    {
        var drawn = await Page("1 0 0 rg 0 0 5 5 re f } 0 0 1 rg 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(1,0,0)", Square + "rgb(0,0,1)");
        drawn.Problems.Should().Equal("UnreadableContent page");
    }

    [Test, Timeout(30_000)]
    public async Task TheOperandsGatheredBeforeAFaultAreNotHandedToTheNextOperator()
    {
        // The three numbers before the fault would make the rg after it red.
        var drawn = await Page("1 0 0 } rg 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("UnreadableContent page", "WrongOperands rg");
    }

    [Test, Timeout(30_000)]
    public async Task ANameThatIsNoOperatorIsReportedAndReadPast()
    {
        var drawn = await Page("1 0 0 rg 5 frobnicate 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(1,0,0)");
        drawn.Problems.Should().Equal("UnknownOperator frobnicate");
    }

    [Test, Timeout(30_000)]
    public async Task ANameThatIsNoOperatorIsNoProblemBetweenBxAndEx()
    {
        var drawn = await Page("BX 5 frobnicate EX 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task AProblemMetAgainIsCountedAndNotListedAgain()
    {
        var drawn = await Page(string.Concat(Enumerable.Repeat("Q ", 1000)));

        var diagnostic = drawn.Result.Diagnostics.Single();
        diagnostic.Occurrences.Should().Be(1000);
        diagnostic.OperatorIndex.Should().Be(0, "it is where the problem was first met");
    }

    [Test, Timeout(30_000)]
    public async Task APageOfDifferentProblemsCostsABoundedNumberOfEntries()
    {
        var content = new StringBuilder();
        for (var index = 0; index < 300; index++)
            content.Append("/Missing").Append(index).Append(" Do ");

        var drawn = await Page(content.ToString());

        drawn.Result.Diagnostics.Should().HaveCount(PageRenderResult.MaximumDiagnostics);
        drawn.Result.OmittedDiagnostics.Should().Be(300 - PageRenderResult.MaximumDiagnostics);
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test, Timeout(30_000)]
    public async Task AContentStreamOfNothingButFaultsIsGivenUpOn()
    {
        var drawn = await Page("0 0 5 5 re f " + new string('}', 10_000) + " 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("UnreadableContent page", "LimitReached page");
    }

    [Test, Timeout(30_000)]
    public async Task ManyNamesThatAreNoOperatorBetweenBxAndExAreAllReadPast()
    {
        var drawn = await Page("BX " + string.Concat(Enumerable.Repeat("1 newer ", 500)) + "EX 0 0 5 5 re f");

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(30_000)]
    public async Task ContentOfNothingButOpeningBracketsIsGivenUpOnAndDoesNotEndTheProcess()
    {
        var brackets = new string('[', 200_000);

        var drawn = await Page("0 0 5 5 re f " + brackets);

        drawn.Device.Body.Should().Equal(Square + "rgb(0,0,0)");
        drawn.Problems.Should().Equal("UnreadableContent page", "LimitReached page");

        // The same content read the way everything else reads it is refused, not run out of stack on.
        await Interruptibly.Run(() => FluentActions
            .Invoking(() => ContentReader.ReadContent(Encoding.ASCII.GetBytes(brackets)))
            .Should().Throw<ContentReaderException>());
    }

    [Test, Timeout(30_000)]
    public async Task AWidthTableThatGivesEveryCidAWidthOverAndOverIsReadInBoundedTime()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("BT /F1 10 Tf 20 50 Td <00010002> Tj ET",
            (document, resources) =>
            {
                var runs = Enumerable.Repeat(new object[] { 0, 65535, 500 }, 100_000).SelectMany(run => run).ToArray();
                Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Composite(document, "/Identity-H", 1000, runs));
            }, _ => new SquareGlyphs()));

        drawn.Device.Fills.Select(fill => Math.Round(fill.Origin.X, 6)).Should().Equal(20, 25);
    }

    [Test, Timeout(30_000)]
    public async Task APageThatStatesNoBoxIsDrawnAsLetterAndSaysSo()
    {
        var (result, device) = await Interruptibly.Run(() =>
        {
            var page = Drawn.PageOf("0 0 5 5 re f");
            page.Elements.Remove("/MediaBox");
            var recording = new RecordingDevice();
            return (PageRendering.Render(page, recording), recording);
        });

        device.Lines.Should().Equal(
            "save",
            "clip nonzero M0,792 L612,792 L612,0 L0,0 Z",
            "fill nonzero M0,792 L5,792 L5,787 L0,787 Z rgb(0,0,0)",
            "restore");
        result.Diagnostics.Single().ToString().Should().StartWith("InvalidValue page at page operator 0");
    }

    [Test, Timeout(30_000)]
    public async Task ACropBoxThatMissesTheMediaBoxLeavesTheMediaBoxToBeDrawn()
    {
        var view = await Interruptibly.Run(() =>
        {
            var page = Drawn.PageOf("");
            page.CropBox = new PdfRectangle(new XPoint(500, 500), new XPoint(600, 600));
            return PageView.Of(page);
        });

        view.Box.Should().Be(new XRect(0, 0, 200, 100));
    }

    [Test, Timeout(30_000)]
    public async Task AFormWhoseMatrixIsTooLargeToDrawUnderIsNotDrawn()
    {
        var drawn = await Page("1" + new string('0', 200) + " 0 0 1 0 0 cm /Fm1 Do", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            form.Elements.SetMatrix("/Matrix", new XMatrix(1e200, 0, 0, 1, 0, 0));
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Body.Should().BeEmpty();
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
    }

    [Test, Timeout(30_000)]
    public async Task AProblemSaysWhereItWasFirstMet()
    {
        var drawn = await Page("0 0 5 5 re f /Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 m 1 1 l 5 w -1 w")));

        var diagnostic = drawn.Result.Diagnostics.Single();
        diagnostic.Stream.Should().Be("page/Fm1");
        diagnostic.OperatorIndex.Should().Be(3);
        diagnostic.Problem.Should().Be(PageRenderProblem.InvalidValue);
        diagnostic.Subject.Should().Be("w");
        diagnostic.ToString().Should().Be(
            "InvalidValue w at page/Fm1 operator 3: A line width is not negative. The value is left as it was.");
    }

    [Test, Timeout(30_000)]
    public async Task ContentThatCannotBeDecodedIsReportedAndNothingOfItIsDrawn()
    {
        var drawn = await Interruptibly.Run(() =>
        {
            var page = Drawn.PageOf("");
            var content = page.Contents.AppendContent();
            content.CreateStream([1, 2, 3, 4, 5, 6, 7, 8]);
            content.Elements.SetName("/Filter", "/FlateDecode");

            var device = new RecordingDevice();
            return (Result: PageRendering.Render(page, device), Device: device);
        });

        drawn.Device.Body.Should().BeEmpty();
        drawn.Result.Diagnostics.Single().Problem.Should().Be(PageRenderProblem.UnreadableContent);
    }

    [Test, Timeout(30_000)]
    public async Task OneStreamOfSeveralThatCannotBeDecodedIsReportedAndTheOthersAreDrawn()
    {
        var drawn = await Interruptibly.Run(() =>
        {
            var page = Drawn.PageOf("1 0 0 rg 0 0 5 5 re f");
            var broken = page.Contents.AppendContent();
            broken.CreateStream([1, 2, 3, 4, 5, 6, 7, 8]);
            broken.Elements.SetName("/Filter", "/FlateDecode");
            page.Contents.AppendContent().CreateStream(Encoding.Latin1.GetBytes("0 0 1 rg 0 0 5 5 re f"));

            var device = new RecordingDevice();
            return (Result: PageRendering.Render(page, device), Device: device);
        });

        drawn.Device.Body.Should().Equal(Square + "rgb(1,0,0)", Square + "rgb(0,0,1)");
        var diagnostic = drawn.Result.Diagnostics.Single();
        diagnostic.Problem.Should().Be(PageRenderProblem.UnreadableContent);
        diagnostic.Message.Should().StartWith("1 of the streams");
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test, Timeout(20_000)]
    [Arguments("q 2 0 0 2 0 0 cm 1 1 l Q ")]
    [Arguments("2 0 0 2 0 0 cm 1 1 l 0.5 0 0 0.5 0 0 cm ")]
    public async Task ATransformChangedThroughoutOnePathIsDrawnInTimeByItsLength(string repeated)
    {
        // 40,000 changes of transform inside one open path. Rewriting the path at each change
        // took most of a minute; it is now a fraction of a second, and the timeout is the test.
        const int Repeats = 40_000;
        var content = new StringBuilder("0 0 m ");
        for (var index = 0; index < Repeats; index++)
            content.Append(repeated);
        content.Append('S');

        var drawn = await Page(content.ToString());

        var stroke = drawn.Device.Strokes.Single();
        stroke.Path.Verbs.Length.Should().Be(Repeats + 1);
        stroke.DevicePath.Should().StartWith("M0,100 L2,98 L2,98").And.EndWith("L2,98");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(20_000)]
    public async Task AFormReachedThousandsOfTimesThroughOtherFormsIsReadOnce()
    {
        // Twelve forms, each drawing the next twice, reach the last of them some two thousand
        // times before the limit on forms is met. Its stream is long to read and draws nothing,
        // so what this takes is what reading it takes: most of a minute read every time.
        var drawn = await Page("/L0 Do", (document, resources) =>
        {
            var last = new StringBuilder();
            for (var index = 0; index < 20_000; index++)
                last.Append("0 0 1 1 ");

            var next = Drawn.Form(document, last.ToString());
            for (var level = 0; level < 12; level++)
            {
                var form = Drawn.Form(document, "/N Do /N Do");
                var own = new PdfDictionary(document);
                form.Elements["/Resources"] = own;
                Drawn.Add(document, own, "/XObject", "/N", next);
                next = form;
            }

            Drawn.Add(document, resources, "/XObject", "/L0", next);
        });

        drawn.Problems.Should().Equal("LimitReached /N");
        // The page saves once, and each of the 4096 forms that may be entered once more.
        drawn.Device.Calls.Count(call => call.Kind == "save").Should().Be(4096 + 1);
    }

    [Test, Timeout(30_000)]
    [Arguments(1)]
    [Arguments(3)]
    public async Task AFormReadOnceReportsItsFaultsEachTimeItIsDrawn(int times)
    {
        var once = await Page("/Fm1 Do", WithBrokenForm);
        var drawn = await Page(string.Concat(Enumerable.Repeat("/Fm1 Do ", times)), WithBrokenForm);

        drawn.Device.Fills.Should().HaveCount(times);
        var diagnostic = drawn.Result.Diagnostics.Single();
        diagnostic.Problem.Should().Be(PageRenderProblem.UnreadableContent);
        diagnostic.Occurrences.Should().Be(times * once.Result.Diagnostics.Single().Occurrences);

        static void WithBrokenForm(PdfDocument document, PdfDictionary resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f } 1 w"));
    }

    [Test, Timeout(30_000)]
    public async Task APageOfNothingButSavesCostsACounterAndStillBalances()
    {
        var drawn = await Page(string.Concat(Enumerable.Repeat("q ", 100_000)) + "0 0 5 5 re f");

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Device.Calls.Count(call => call.Kind == "save").Should().Be(257);
        drawn.Device.Calls.Count(call => call.Kind == "restore").Should().Be(257);
        drawn.Problems.Should().Equal("LimitReached q");
    }

    [Test, Timeout(30_000)]
    public async Task ASaveThatWasOnlyCountedIsUndoneByTheRestoreThatPairsWithIt()
    {
        var saves = string.Concat(Enumerable.Repeat("q ", 300));
        var restores = string.Concat(Enumerable.Repeat("Q ", 300));

        var drawn = await Page("1 0 0 rg " + saves + "0 0 1 rg " + restores + "0 0 5 5 re f");

        drawn.Device.Body.Last().Should().Be(Square + "rgb(1,0,0)", "every restore pairs with a save, so none is one too many");
        drawn.Problems.Should().Equal("LimitReached q");
    }

    [Test, Timeout(30_000)]
    public async Task ATransformTooLargeToDrawUnderIsLeftAsItWas()
    {
        var drawn = await Page(string.Concat(Enumerable.Repeat("99999999999999999999 0 0 99999999999999999999 0 0 cm ", 40))
                               + "0 0 5 5 re f");

        var fill = drawn.Device.Fills.Single();
        double.IsFinite(fill.Transform.M11).Should().BeTrue();
        drawn.Problems.Should().Equal("InvalidValue cm");
    }

    [Test, Timeout(30_000)]
    public async Task ADeviceThatThrowsCostsTheOperatorItThrewAtAndIsStillRestored()
    {
        var device = new ThrowingOnFill();
        var result = await Interruptibly.Run(() =>
            PageRendering.Render(Drawn.PageOf("q 0 0 5 5 re f 0 0 5 5 re S Q"), device));

        device.Strokes.Should().Be(1);
        device.Saves.Should().Be(device.Restores);
        var diagnostic = result.Diagnostics.Single();
        diagnostic.Problem.Should().Be(PageRenderProblem.OperatorFailed);
        diagnostic.Subject.Should().Be("f");
        diagnostic.Message.Should().Be("The canvas is gone.");
    }

    [Test, Timeout(30_000)]
    [Arguments("(never closed")]
    [Arguments("<48 65")]
    [Arguments("[1 2 3")]
    [Arguments("[1 [2 [3")]
    [Arguments("]]]]")]
    [Arguments("BI /W 1 /H 1 ID")]
    [Arguments("<< /A 1")]
    [Arguments("1..2 w 3.4.5 0 0 rg")]
    [Arguments("0 0 5 5 re f\u0000\u0001\u0002{}{}")]
    [Arguments("BT ET ET BT BT Tj TJ ' \" T* Td")]
    [Arguments("/F1 Tf (text) Tj [(a) (b)] TJ")]
    [Arguments("W W* W n n f S B b")]
    [Arguments("true false null 1 2 R")]
    public async Task ContentCutShortOrOutOfOrderIsDrawnAsFarAsItGoesAndNeverThrows(string content)
    {
        var drawn = await Page(content);

        drawn.Device.Calls[^1].Kind.Should().Be("restore");
        drawn.Device.Calls.Count(call => call.Kind == "save")
            .Should().Be(drawn.Device.Calls.Count(call => call.Kind == "restore"));
    }

    [Test, Timeout(120_000)]
    public async Task ContentThatIsNoiseIsReadToItsEndAndNeverThrows()
    {
        // Operators and operands a real stream holds, put together at random, with the odd byte
        // that belongs in no content stream at all. The seed is fixed, so a failure is one that
        // comes back.
        string[] pieces =
        [
            "q", "Q", "cm", "w", "J", "d", "gs", "re", "m", "l", "c", "v", "y", "h", "f", "f*", "S", "B", "b*", "n",
            "W", "W*", "BT", "ET", "Tf", "Td", "TD", "Tm", "T*", "Tj", "TJ", "'", "\"", "Tr", "Tz", "Do", "BI", "ID",
            "EI", "sh", "cs", "scn", "SCN", "rg", "K", "BX", "EX", "BDC", "EMC", "1", "-2.5", "0", "100", ".5", "/F1",
            "/Fm1", "/GS1", "(text)", "<0041>", "[", "]", "<<", ">>", "(", ")", "}", "%", "\n", "\u0000", "\u00ff"
        ];

        var counts = await Interruptibly.Run(() =>
        {
            var random = new Random(371);
            var balanced = 0;
            for (var run = 0; run < 400; run++)
            {
                var content = new StringBuilder();
                for (var piece = 0; piece < 300; piece++)
                    content.Append(pieces[random.Next(pieces.Length)]).Append(' ');

                var drawn = Drawn.Page(content.ToString(), (document, resources) =>
                {
                    Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 32, 250, 300));
                    Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "q /Fm1 Do 0 0 5 5 re f"));
                    var state = new PdfDictionary(document);
                    state.Elements.SetReal("/LW", 2);
                    Drawn.Add(document, resources, "/ExtGState", "/GS1", state);
                }, _ => new SquareGlyphs());

                if (drawn.Device.Calls.Count(call => call.Kind == "save")
                    == drawn.Device.Calls.Count(call => call.Kind == "restore"))
                {
                    balanced++;
                }
            }

            return balanced;
        });

        counts.Should().Be(400);
    }

    [Test, Timeout(30_000)]
    public async Task ADeviceThatThrowsAtEverythingStillLetsThePageBeReadToItsEnd()
    {
        var device = new ThrowingAtEverything();
        var result = await Interruptibly.Run(() => PageRendering.Render(
            Drawn.PageOf("q 0 0 5 5 re W f Q BT /F1 10 Tf 7 Tr (A) Tj", (document, resources) =>
                Drawn.Add(document, resources, "/Font", "/F1", TestFonts.Simple(document, 65, 600))),
            device, glyphSources: _ => new SquareGlyphs()));

        result.Diagnostics.Should().OnlyContain(d => d.Problem == PageRenderProblem.OperatorFailed);
        result.Diagnostics.Select(d => d.Subject).Should().Equal("page", "q", "f");
        device.Restores.Should().Be(0, "a save the device threw at is not one to restore");
    }

    [Test, Timeout(30_000)]
    public async Task ARestoreReachesTheDeviceOnlyWhereTheSaveItMatchesDid()
    {
        var device = new ThrowingAtEverything { SavesThatSucceed = 1 };
        await Interruptibly.Run(() => PageRendering.Render(Drawn.PageOf("q q Q Q"), device));

        device.Saves.Should().Be(3);
        device.Restores.Should().Be(1);
    }

    private sealed class ThrowingAtEverything : PageDevice
    {
        public int SavesThatSucceed { get; init; }
        public int Saves { get; private set; }
        public int Restores { get; private set; }

        public override void Save()
        {
            if (++Saves > SavesThatSucceed)
                throw new InvalidOperationException("No.");
        }

        public override void Restore() => Restores++;

        public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode) =>
            throw new InvalidOperationException("No.");

        public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint) =>
            throw new InvalidOperationException("No.");

        public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint) =>
            throw new InvalidOperationException("No.");

        public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint) =>
            throw new InvalidOperationException("No.");
    }

    private sealed class ThrowingOnFill : PageDevice
    {
        public int Saves { get; private set; }
        public int Restores { get; private set; }
        public int Strokes { get; private set; }

        public override void Save() => Saves++;

        public override void Restore() => Restores++;

        public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
        {
        }

        public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint) =>
            throw new InvalidOperationException("The canvas is gone.");

        public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint) => Strokes++;

        public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint)
        {
        }
    }
}
