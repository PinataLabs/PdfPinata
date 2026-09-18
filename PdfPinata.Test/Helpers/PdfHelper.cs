using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

public class PdfHelper
{
    private static readonly string _rootPath = PathHelper.GetInstance().RootDir;

    /// <summary>
    /// The resolution pages are rasterized at, unless the document is too big to be drawn at it.
    /// </summary>
    private const int Dpi = 300;

    /// <summary>
    /// The most pixels a rasterized document may come to, counting every page of it.
    /// </summary>
    /// <remarks>
    /// A page is drawn by Ghostscript, which on Windows is loaded into the test host rather than
    /// run as a command, and which ends the process outright rather than reporting an error it
    /// cannot recover from. A big enough page is such an error: FamilyTree.pdf is 58 inches by 23,
    /// which at 300 dpi is 122 megapixels and takes the process to about 940MB for the one page.
    /// On its own that succeeds; landing on a test host that is already carrying the rest of the
    /// suite it sometimes does not, and the host exits with status 1, no dump, no message and no
    /// failing test.
    ///
    /// This limit was once per page, which left a gap: Rasterize reads *every* page of a document
    /// into one collection, so a document of many ordinary pages was never reduced at all.
    /// test.pdf is four pages of 9.7 megapixels - each one comfortably inside a per-page limit,
    /// and 38.7 megapixels together, two and a half times the single page the limit was written
    /// for. PageResizeRenderingTests renders it three times and holds the first alongside the
    /// others to compare them, so that one test asked for something over a hundred megapixels at
    /// once, and with two target frameworks running it was two of those at the same time.
    ///
    /// --blame-crash named TestDocumentSurvivesBeingResized as the test that never finished, on
    /// both framework legs of the same run. The limit is therefore on the whole document now.
    /// </remarks>
    internal const double MaxPixelsPerDocument = 16e6;

    /// <summary>
    ///   Rasterize all pages within a PDF to PNG images
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static RasterizeOutput Rasterize(PdfDocument document)
    {
        var dpi = ResolutionFor(document);
        var readerSettings = new MagickReadSettings
        {
            Density = new Density(dpi, dpi),
            BackgroundColor = MagickColors.White
        };
        var images = new MagickImageCollection();

        // Nothing owns the collection until it is handed back inside a RasterizeOutput, so
        // anything that throws on the way there has to free it first. On a machine with no
        // Ghostscript that is every rasterizing test in the run.
        try
        {
            // Add all pages to the collection
            using var ms = new MemoryStream();
            document.Save(ms);

            try
            {
                images.Read(ms, readerSettings);
            }
            catch (MagickDelegateErrorException ex)
            {
                throw new Exception("Ghostscript is not installed or is an incompatible version, unable to rasterize PDF", ex);
            }

            // Composite onto white, to guarantee a standard background. Remove rather than
            // Deactivate: Deactivate merely drops the alpha channel and leaves whatever colour was
            // underneath it, so every pixel of a transparency group that was never painted comes
            // out black. A page carrying an annotation drawn under a blend mode is such a page.
            foreach (var img in images)
            {
                img.BackgroundColor = MagickColors.White;
                img.Alpha(AlphaOption.Remove);
            }
        }
        catch
        {
            images.Dispose();
            throw;
        }

        return new RasterizeOutput
        {
            ImageCollection = images,
        };
    }
        
    /// <summary>
    /// What to draw a document at: <see cref="Dpi"/>, or as much less as it takes to keep its
    /// whole document inside <see cref="MaxPixelsPerDocument"/>.
    /// </summary>
    /// <remarks>
    /// One resolution for the whole document, because a reader is given one and the pages of a
    /// document are compared with each other. The answer is a whole number of dots per inch so
    /// that the same document rasterizes to the same size every time it is asked for, whatever
    /// arithmetic the caller did to get here.
    /// </remarks>
    internal static int ResolutionFor(PdfDocument document)
    {
        var pixelsAtFullResolution = PixelsIn(document, Dpi);
        if (pixelsAtFullResolution <= MaxPixelsPerDocument)
            return Dpi;

        // Halving the resolution quarters the pixels, so the resolution scales by the square root.
        var reduced = (int)(Dpi * Math.Sqrt(MaxPixelsPerDocument / pixelsAtFullResolution));

        // A resolution of zero is not a resolution, and Ghostscript is being asked for one number.
        //
        // This used to be a floor - Math.Max(reduced, 1) - and that was sound while the limit was
        // per page: readers stop at 14400 points a side, and even that whole page is 40 thousand
        // pixels at 1 dpi, far inside the limit, so nothing real could reach it. Counting every
        // page makes it reachable, because enough pages of any size will get there. A floor would
        // then hand Ghostscript a document over the limit anyway, which is the one thing this
        // exists to prevent, and it would do it silently.
        if (reduced < 1)
        {
            throw new InvalidOperationException(
                $"A document of {document.PageCount} pages cannot be rasterized within "
                + $"{MaxPixelsPerDocument:N0} pixels even at 1 dpi.");
        }

        return reduced;
    }

    /// <summary>
    /// How many pixels every page of a document comes to together when drawn at a resolution.
    /// </summary>
    internal static double PixelsIn(PdfDocument document, int dpi)
    {
        var area = 0.0;

        // Summed rather than maxed. Every page is held in memory at once, so what the host has to
        // carry is the whole document rather than its biggest page.
        foreach (var page in document.Pages)
            area += page.Width.Point * page.Height.Point;

        // Points are 1/72 inch, so a page is (points / 72 * dpi) pixels along each side.
        return area * dpi * dpi / (72.0 * 72.0);
    }

    public static List<string> WriteImageCollection(MagickImageCollection images, string outDir, string filePrefix)
    {
        var outPaths = new List<string>();
        for (var pageNum = 0; pageNum < images.Count; pageNum++)
        {
            var outPath = GetOutFilePath(outDir, $"{filePrefix}_{pageNum+1}.png");
            images[pageNum].Write(outPath);
            outPaths.Add(outPath);
        }

        return outPaths;
    }
        
    public static string WriteImage(IMagickImage image, string outDir, string fileNameWithoutExtension)
    {
        var outPath = GetOutFilePath(outDir, $"{fileNameWithoutExtension}.png");
        image.Write(outPath);
        return outPath;
    }

    // Note: For diff to function properly, it requires the underlying image to be in the proper format
    //   For instance, actual and expected must both be sourced from .png files
    /// <summary>
    /// How much the pages are shrunk before they are compared. Two rasterizers disagree about
    /// the pixels along the edge of a glyph, and that disagreement is as fine as the pixels
    /// themselves, so shrinking the pages averages it away. Text that sits in the wrong place
    /// is a difference the width of a line or the height of one, which survives.
    /// </summary>
    private const int ComparedAtPercent = 25;

    // Note: For diff to function properly, it requires the underlying image to be in the proper format
    //   For instance, actual and expected must both be sourced from .png files
    public static DiffOutput Diff(string actualImagePath, string expectedImagePath, string outputPath = null, string filePrefix = null)
    {
        // Every one of these three holds a bitmap in unmanaged memory that the garbage collector
        // cannot see the size of. Left to a finalizer they pile up until the test host is killed
        // outright - see the remarks on RasterizeOutput.
        using var actual = new MagickImage(actualImagePath);
        using var expected = new MagickImage(expectedImagePath);

        actual.Resize(new Percentage(ComparedAtPercent));
        expected.Resize(new Percentage(ComparedAtPercent));

        // Root mean squared rather than a count, so the answer is a share of how far a page
        // can differ at all, and does not grow with the size of the page.
        using var diffImg = actual.Compare(expected, ErrorMetric.RootMeanSquared, out var diffVal);

        if (diffVal > 0 && outputPath != null && filePrefix != null)
        {
            WriteImage(diffImg, outputPath, $"{filePrefix}_diff");
        }

        return new DiffOutput
        {
            DiffValue = diffVal
        };
    }
        
    private static string GetOutFilePath(string outDir, string name)
    {
        var dir = Path.Combine(_rootPath, outDir);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, name);
    }
}

/// <summary>
/// The rasterized pages of a document. <b>Dispose it.</b>
/// </summary>
/// <remarks>
/// A page rasterized at 300 dpi is some tens of megabytes of bitmap, and that bitmap lives in
/// unmanaged memory. The garbage collector sees only the small managed wrapper, so nothing about
/// the pressure of holding a dozen of them makes a collection happen any sooner - the process
/// simply runs out of memory and dies. In a test run that shows up as
/// <c>Test host process crashed</c> and <c>Test Run Aborted</c> with <b>no failing test</b> and a
/// passing count quietly short of the total, which reads like flakiness and is not.
/// </remarks>
public class RasterizeOutput : IDisposable
{
    public List<string> OutputPaths;
    public MagickImageCollection ImageCollection;

    public void Dispose()
    {
        ImageCollection?.Dispose();
        ImageCollection = null;
    }
}

public class DiffOutput
{
    /// <summary>
    /// How far the two images stand apart, from 0 for a pair that matches to 1 for black
    /// against white. Text that moved shows up here in the percents, while the edges of glyphs
    /// drawn by one rasterizer rather than another stay far below.
    /// </summary>
    public double DiffValue;
}
