using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Two documents laid out at once come out the same as one after the other.
/// </summary>
/// <remarks>
///   <para>
///     Written against [empira/PDFsharp#381](https://github.com/empira/PDFsharp/issues/381), which
///     is not a defect here and is pinned so that it stays that way. Upstream's
///     <c>FontHandler.FontToXFont</c> memoises the last (<c>Font</c>, <c>XFont</c>) pair in two
///     unsynchronised static fields, and each mention of a field in the guard is a separate read —
///     so a thread can pass the check against its own font and then be handed the other thread's
///     <c>XFont</c>. Nothing throws. The document is produced, and is silently typeset at the wrong
///     size: wrong string widths, so wrong wrapping, or a <c>Tf</c> at the wrong size mid-line.
///   </para>
///   <para>
///     This fork's <c>FontHandler</c> has no cache at all - it kept the shape it was ported with,
///     where the conversion just builds an <c>XFont</c> - and the caches it sits above,
///     <c>FontFactory</c> and the family and descriptor caches, are held under
///     <c>Lock.EnterFontFactory</c>. So the one unguarded step in an otherwise guarded font path is
///     not here.
///   </para>
///   <para>
///     The tests below are worth their runtime because that is a hard thing to keep. A memo in
///     front of <c>FontToXFont</c> is exactly the optimisation someone reaches for, it is correct
///     on one thread, and no assertion about a single document can see it go wrong. Grafting
///     upstream's cache into this fork and running these gives 38,745 wrong fonts in 200,000 calls
///     and 211 different pages out of 300 - which is how the volumes below were chosen, and they
///     are a fraction of that with room to spare.
///   </para>
/// </remarks>
public class ConcurrentRenderingTests
{
    private const int Renders = 120;

    private const int Conversions = 60_000;

    [Fact(Timeout = 300000)]
    public async Task TheSameDocumentLaidOutAgainAndAgainDrawsTheSamePage()
    {
        // The control. What the test below compares against is this one answer, so a page that is
        // not reproducible on one thread would make that comparison meaningless. Run through
        // Task.Run because xUnit honours Timeout on an async test and on no other.
        var pages = await Task.Run(() =>
            Enumerable.Range(0, Renders).Select(_ => PageOf(Built())).Distinct().ToList());

        pages.Should().ContainSingle();
    }

    [Fact(Timeout = 300000)]
    public async Task LayingItOutOnEightThreadsAtOnceDrawsThatSamePageEveryTime()
    {
        var pages = new ConcurrentBag<string>();

        await Task.Run(() => Parallel.For(0, Renders, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            _ => pages.Add(PageOf(Built()))));

        // Distinct rather than "equal to the sequential answer": what is being asked is whether the
        // threads agreed with each other, and the content stream carries no date or identifier, so
        // two runs of the same document are byte for byte the same page.
        pages.Distinct().Should().ContainSingle();
    }

    [Fact(Timeout = 300000)]
    public async Task TheFontAParagraphIsDrawnWithIsTheFontItAsksFor()
    {
        // The report's own probe, one layer down. Two *stable* Font instances are the whole of the
        // arrangement: a memo keyed on the object never hits for a font allocated per call, so
        // fresh fonts would hide the race rather than test for it.
        var fonts = new[] { new Font("Arial") { Size = 9.0 }, new Font("Arial") { Size = 6.5 } };
        var wrong = 0;

        await Task.Run(() => Parallel.For(0, Conversions, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            index =>
            {
                var font = fonts[index % 2];
                var converted = FontToXFont(font);

                if (Math.Abs(converted.Size - font.Size.Point) > 0.001)
                    Interlocked.Increment(ref wrong);
            }));

        wrong.Should().Be(0);
    }

    /// <summary>
    ///   <c>FontHandler</c> is internal to the rendering assembly, and this repository carries no
    ///   <c>InternalsVisibleTo</c>, so it is reached the way the other probes here reach one.
    /// </summary>
    private static XFont FontToXFont(Font font)
    {
        return (XFont)Conversion.Invoke(null, new object[] { font, null, PdfFontEncoding.Unicode })!;
    }

    private static readonly MethodInfo Conversion =
        typeof(PdfDocumentRenderer).Assembly.GetType("PinataLayout.Rendering.FontHandler", throwOnError: true)!
            .GetMethod("FontToXFont", BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>The page as it was drawn, which is where a font of the wrong size shows up.</summary>
    private static string PageOf(Document document)
    {
        return Encoding.Latin1.GetString(PageContent.Of(Rendered.FirstPageOf(document)));
    }

    /// <summary>
    ///   A document of two sizes and a footer, wrapping at both of them.
    /// </summary>
    /// <remarks>
    ///   Built fresh per render because a document may be laid out once only, and shaped after the
    ///   report: the sizes are the two it names, and the captured divergences were a word's advance
    ///   moving by 0.2778 pt and a footer line wrapping differently.
    /// </remarks>
    private static Document Built()
    {
        var document = new Document();
        var section = document.AddSection();

        var body = section.AddParagraph();
        body.Format.Font.Size = 9;
        body.AddText(string.Join(" ", Enumerable.Repeat(
            "The quick brown fox jumps over the lazy dog and keeps going until the line wraps.", 8)));

        var small = section.AddParagraph();
        small.Format.Font.Size = 6.5;
        small.AddText(string.Join(" ", Enumerable.Repeat(
            "Smaller text set at six and a half points, long enough to wrap more than once.", 8)));

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Font.Size = 6.5;
        footer.AddText("A footer line, set small, which is where the reported divergence landed.");

        return document;
    }
}
