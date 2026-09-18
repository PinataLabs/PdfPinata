using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// The collection every test that rasterizes a PDF belongs to.
/// </summary>
/// <remarks>
/// ImageMagick drives Ghostscript in process, and one process holds one Ghostscript. A second
/// rasterization started while the first is running finds it taken and falls back to running
/// Ghostscript as a command, which is not there to run on a machine without an installation of
/// its own. Tests in one collection do not run alongside one another, and disabling
/// parallelization keeps the collection from running alongside the others.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RasterizingCollection
{
    public const string Name = "Rasterizing";
}
