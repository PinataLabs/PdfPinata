using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
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
