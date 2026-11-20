using System.IO;
using System.Threading.Tasks;
using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Marks a test that reads a PDF of several gigabytes, which is never committed, and skips it
///   unless the environment variable <c>PDFPINATA_LARGE_PDF</c> names such a file that exists.
/// </summary>
public sealed class LargePdfAttribute() : SkipAttribute("PDFPINATA_LARGE_PDF does not name a file.")
{
    /// <summary>The file the environment variable names, or null.</summary>
    internal static string Path => System.Environment.GetEnvironmentVariable("PDFPINATA_LARGE_PDF");

    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(string.IsNullOrEmpty(Path) || !File.Exists(Path));
}
