using System;
using System.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Skia;

namespace ConformanceCorpus;

/// <summary>
/// Writes the conformance corpus — one PDF per claim this library can make — for a validator to be
/// pointed at.
/// </summary>
/// <remarks>
/// <para>
/// A separate program rather than a test, because what it produces is an input to something else.
/// A test asserts and returns nothing; the corpus has to exist as files on disk for veraPDF to open,
/// and both CI and a developer running <c>verapdf-check.ps1</c> want it built the same way.
/// </para>
/// <para>
/// Nothing here asserts anything. If a document cannot even be written the process fails, and that
/// is a real failure worth reporting — the writer refuses to save a document that breaks a rule of
/// the profile it claims, so a corpus that cannot be built is the library's own check firing.
/// </para>
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        var output = OutputDirectory(args);

        // The corpus draws text and reads no images, but the image seam is registered anyway: it is
        // one line, and a document added later that draws one would otherwise fail here for a reason
        // that has nothing to do with conformance.
        GlobalFontSettings.FontResolver = new CffFontResolver();
        ImageSource.ImageSourceImpl = new SkiaImageSource();

        Directory.CreateDirectory(output);

        var written = 0;
        foreach (var (name, bytes) in Corpus.Documents())
        {
            var path = Path.Combine(output, name + ".pdf");
            File.WriteAllBytes(path, bytes);
            Console.WriteLine($"{name,-20} {bytes.Length,8:N0} bytes  {path}");
            written++;
        }

        Console.WriteLine($"\n{written} document(s) written to {output}");
        return 0;
    }

    /// <summary>
    /// Where to write, from <c>--out &lt;dir&gt;</c> or a default beside the repository's other build
    /// output.
    /// </summary>
    static string OutputDirectory(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] is "--out" or "-o")
                return Path.GetFullPath(args[index + 1]);
        }

        return Path.GetFullPath(Path.Combine("artifacts", "conformance-corpus"));
    }
}
