using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Marks a test, or a class of them, that rasterizes a PDF.
/// </summary>
/// <remarks>
/// ImageMagick drives Ghostscript in process, and one process holds one Ghostscript. A second
/// rasterization started while the first is running finds it taken and falls back to running
/// Ghostscript as a command, which is not there to run on a machine without an installation of
/// its own. A <see cref="NotInParallelAttribute"/> with no key runs each of these tests alone:
/// not beside one another, and not beside anything else either, which is what the xUnit
/// collection this replaces did by disabling parallelization.
/// </remarks>
public sealed class RasterizingAttribute : NotInParallelAttribute;
