using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   Two font files that call themselves by the same full name (name ID 4) but are not the same
///   bytes - two revisions of one font, say. A resolver may serve them under two face names, and
///   each face has to be drawn from its own file. The font cache used to hold every file under its
///   full name as well, and refused the second with an <see cref="ArgumentException"/> (#329).
/// </summary>
/// <remarks>
///   The files here are Liberation Sans Regular with the advance of one glyph changed, so they
///   keep its full name - and so share it with the face every other test in the assembly draws
///   with - and how wide that glyph measures says which file a font was drawn from.
/// </remarks>
public class SameFullNameFontTests
{
    private const ushort UnitsPerEm = 2048;

    [Test]
    public void TwoFacesWithOneFullNameEachMeasureWithTheirOwnBytes()
    {
        Register("Same Full Name A", 1000);
        Register("Same Full Name B", 1500);

        WidthOfH("Same Full Name A").Should().BeApproximately(1000 * 1000.0 / UnitsPerEm, 1e-6);
        WidthOfH("Same Full Name B").Should().BeApproximately(1500 * 1000.0 / UnitsPerEm, 1e-6);
    }

    [Test]
    public void TwoFacesWithOneFullNameAreEmbeddedAsTwoFontsInOneDocument()
    {
        Register("Same Full Name Embedded A", 1100);
        Register("Same Full Name Embedded B", 1600);

        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawString("H", Font("Same Full Name Embedded A", 12), XBrushes.Black, new XPoint(20, 40));
            gfx.DrawString("H", Font("Same Full Name Embedded B", 12), XBrushes.Black, new XPoint(20, 80));
        }

        // The subset carries no cmap of its own, but keeps every glyph at its original index.
        var glyphOfH = new TrueTypeGlyphs(LiberationSansRegular()).GlyphIndexOf('H');
        var advances = document.Reopened().Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/FontDescriptor")
            .Select(d => d.Elements["/FontFile2"])
            .Select(program => ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
                .Stream.UnfilteredValue)
            .Select(bytes => new TrueTypeGlyphs(bytes))
            .Select(program => program.AdvanceInThousandths(glyphOfH))
            .ToList();

        advances.Should().BeEquivalentTo([1100 * 1000 / UnitsPerEm, 1600 * 1000 / UnitsPerEm],
            "each face is embedded from its own file, not the first file of that name");
    }

    [Test]
    public async Task FontsForManyFamiliesMadeOnManyThreadsAtOnceEachMeasureWithTheirOwnBytes()
    {
        const int families = 40;
        const int threads = 8;
        var prefix = "Concurrent Full Name " + Guid.NewGuid().ToString("N") + " ";
        for (var idx = 0; idx < families; idx++)
            Register(prefix + idx, (ushort)(600 + idx * 20));

        var failures = new ConcurrentBag<string>();
        using var start = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(thread => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            // Each thread walks the families from a different place, so that every family is
            // asked for first by some thread while others are asking for theirs.
            for (var step = 0; step < families; step++)
            {
                var idx = (step + thread * families / threads) % families;
                try
                {
                    var expected = (600 + idx * 20) * 1000.0 / UnitsPerEm;
                    var width = WidthOfH(prefix + idx);
                    if (Math.Abs(width - expected) > 1e-6)
                        failures.Add($"family {idx} measured {width}, not {expected}");
                }
                catch (Exception ex)
                {
                    failures.Add($"family {idx} threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();

        await Task.WhenAll(workers);

        failures.Should().BeEmpty();
    }

    private static void Register(string family, ushort advanceOfH)
    {
        PinnedFontResolver.Register(family,
            new TrueTypeGlyphs(LiberationSansRegular()).WithAdvanceOf('H', advanceOfH));
    }

    private static byte[] LiberationSansRegular() =>
        File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf"));

    private static XFont Font(string family, double size) =>
        new(family, size, XFontStyle.Regular, new XPdfFontOptions(PdfFontEncoding.Unicode));

    private static double WidthOfH(string family)
    {
        var gfx = XGraphics.CreateMeasureContext(new XSize(100, 100), XGraphicsUnit.Point, XPageDirection.Downwards);
        return gfx.MeasureString("H", Font(family, 1000)).Width;
    }
}
