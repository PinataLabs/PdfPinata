using System;
using System.Collections.Generic;
using ImageMagick;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   The pages a test class has rasterized, each set written out for a person to look at and all
///   of them held until the class is disposed. <b>Dispose it</b>, from the test class's own
///   <c>Dispose</c>.
/// </summary>
/// <remarks>
///   A test asserts on the pages it is handed, often comparing two of them, so they cannot be freed
///   the moment they are drawn - but nor can they be left to a finalizer, because the bitmaps are in
///   unmanaged memory the collector cannot see the size of, and enough of them end the test host
///   with no failing test at all (see <see cref="RasterizeOutput"/>). One instance per test class,
///   created as a field: xUnit builds the class afresh for every test and disposes it after, so
///   what one test drew is freed before the next begins.
///
///   A user of this belongs to <see cref="RasterizingCollection"/>, like anything that rasterizes.
/// </remarks>
internal sealed class Rasterizations(string outDir) : IDisposable
{
    private readonly List<MagickImageCollection> _held = [];

    /// <summary>
    ///   Every page of <paramref name="document"/>, rasterized, written to the output directory as
    ///   <c><paramref name="name"/>_1.png</c> and on, and held until this is disposed.
    /// </summary>
    internal MagickImageCollection Of(PdfDocument document, string name)
    {
        var images = PdfHelper.Rasterize(document).ImageCollection;
        _held.Add(images);
        PdfHelper.WriteImageCollection(images, outDir, name);
        return images;
    }

    /// <summary>The first page of <paramref name="document"/>, as <see cref="Of"/> draws it.</summary>
    internal IMagickImage<byte> FirstPageOf(PdfDocument document, string name) => Of(document, name)[0];

    public void Dispose()
    {
        foreach (var collection in _held)
            collection.Dispose();

        _held.Clear();
    }
}
