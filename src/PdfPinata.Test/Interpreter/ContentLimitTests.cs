using System;
using System.IO;
using System.IO.Compression;
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
/// A page's content stream is inflated to a cap, so a few kilobytes of Flate cannot make a render
/// hold gigabytes.
/// </summary>
public class ContentLimitTests
{
    private const int Megabyte = 1024 * 1024;

    private static byte[] Deflated(long spaces, string tail)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var chunk = Enumerable.Repeat((byte)' ', Megabyte).ToArray();
            for (long done = 0; done < spaces; done += chunk.Length)
                zlib.Write(chunk, 0, (int)Math.Min(chunk.Length, spaces - done));
            zlib.Write(Encoding.Latin1.GetBytes(tail));
        }

        return output.ToArray();
    }

    private static PdfPage FlatePage(byte[] deflated)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 100));
        var stream = page.Contents.AppendContent();
        stream.CreateStream(deflated);
        stream.Elements.SetName("/Filter", "/FlateDecode");
        return page;
    }

    [Test, Timeout(120_000), NotInParallel("LargeAllocation")]
    public async Task ContentThatInflatesPastTheCapIsDrawnToTheCapAndReported()
    {
        var bomb = Deflated(ContentInterpreter_MaximumContentLength + Megabyte, "0 0 5 5 re f");
        bomb.Length.Should().BeLessThan(2 * Megabyte);

        var result = await Interruptibly.Run(() =>
            PageRendering.Render(FlatePage(bomb), new RecordingDevice()));

        result.Diagnostics.Select(d => d.Problem).Should().Equal(PageRenderProblem.UnreadableContent);
        result.Diagnostics[0].Message.Should().Contain("Only the start of it is drawn");
    }

    [Test, Timeout(60_000)]
    public async Task ContentUnderTheCapIsDrawnWholeAndReportsNothing()
    {
        var page = FlatePage(Deflated(1000, "0 0 5 5 re f"));
        var device = new RecordingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        result.Diagnostics.Should().BeEmpty();
        device.Body.Should().NotBeEmpty();
    }

    [Test, Timeout(60_000)]
    public async Task APageWithNoContentIsDrawnCompleteAndReportsNothing()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 100));

        var result = await Interruptibly.Run(() => PageRendering.Render(page, new RecordingDevice()));

        result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(120_000), NotInParallel("LargeAllocation")]
    public async Task WhatAPredictorThrowsAwayStillCountsAgainstTheBudgetTheStreamsShare()
    {
        // Each stream inflates to the whole budget and a predictor with one row wider than that
        // returns nothing, so charging only what comes back would let every stream have the lot.
        var bomb = Deflated(ContentInterpreter_MaximumContentLength + Megabyte, "");
        var document = new PdfDocument();
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 100));
        for (var i = 0; i < 3; i++)
        {
            var stream = page.Contents.AppendContent();
            stream.CreateStream(bomb);
            stream.Elements.SetName("/Filter", "/FlateDecode");
            var parms = new PdfDictionary(document);
            parms.Elements.SetInteger("/Predictor", 15);
            parms.Elements.SetInteger("/Columns", 1 << 27);
            stream.Elements["/DecodeParms"] = parms;
        }

        var (result, allocated) = await Interruptibly.Run(() =>
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var rendered = PageRendering.Render(page, new RecordingDevice());
            return (rendered, GC.GetAllocatedBytesForCurrentThread() - before);
        });

        result.Diagnostics.Should().Contain(d => d.Message.Contains("Only the start of it is drawn"));
        allocated.Should().BeLessThan(1024L * Megabyte,
            "the second and third streams are not inflated once the first has spent the budget");
    }

    private const long ContentInterpreter_MaximumContentLength = 128L * Megabyte;
}
