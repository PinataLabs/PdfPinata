using System.IO;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Test.Interpreter;

/// <summary>Font dictionaries written by hand around a TrueType font program embedded as <c>/FontFile2</c>.</summary>
internal static class EmbeddedFonts
{
    /// <summary>Liberation Sans Regular, whole: 2048 units to the em.</summary>
    public static byte[] LiberationSans() =>
        File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf"));

    /// <summary>
    /// A simple TrueType font with every code 500 thousandths wide, whatever the font program says.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="program">The font program.</param>
    /// <param name="flags">The descriptor's <c>/Flags</c>: 32 nonsymbolic, 4 symbolic.</param>
    /// <param name="encoding">The <c>/Encoding</c>, or null for none.</param>
    public static PdfDictionary Simple(PdfDocument document, byte[] program, int flags, PdfItem encoding)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/TrueType");
        font.Elements.SetName("/BaseFont", "/Embedded");
        font.Elements.SetInteger("/FirstChar", 0);
        font.Elements.SetInteger("/LastChar", 255);
        font.Elements["/Widths"] = new PdfArray(document, Enumerable.Repeat((PdfItem)new PdfReal(500), 256).ToArray());
        if (encoding != null)
            font.Elements["/Encoding"] = encoding;
        font.Elements["/FontDescriptor"] = Descriptor(document, program, flags).Reference;
        return font;
    }

    /// <summary>An <c>/Encoding</c> dictionary: a base encoding, or none, and <c>/Differences</c>.</summary>
    public static PdfDictionary Differences(PdfDocument document, string baseEncoding, params object[] differences)
    {
        var encoding = new PdfDictionary(document);
        encoding.Elements.SetName("/Type", "/Encoding");
        if (baseEncoding != null)
            encoding.Elements.SetName("/BaseEncoding", baseEncoding);
        encoding.Elements["/Differences"] = new PdfArray(document, differences
            .Select(item => item is int code ? (PdfItem)new PdfReal(code) : new PdfName((string)item)).ToArray());
        return encoding;
    }

    /// <summary>
    /// A Type 0 font whose descendant is a <c>CIDFontType2</c> with every CID 1000 thousandths
    /// wide.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="program">The font program.</param>
    /// <param name="cidToGid">The <c>/CIDToGIDMap</c> stream's bytes, or null for <c>/Identity</c>.</param>
    public static PdfDictionary Composite(PdfDocument document, byte[] program, byte[] cidToGid = null)
    {
        var descendant = new PdfDictionary(document);
        descendant.Elements.SetName("/Type", "/Font");
        descendant.Elements.SetName("/Subtype", "/CIDFontType2");
        descendant.Elements.SetName("/BaseFont", "/Embedded");
        descendant.Elements.SetInteger("/DW", 1000);
        descendant.Elements["/FontDescriptor"] = Descriptor(document, program, 4).Reference;
        if (cidToGid == null)
        {
            descendant.Elements.SetName("/CIDToGIDMap", "/Identity");
        }
        else
        {
            var map = new PdfDictionary(document);
            map.CreateStream(cidToGid);
            document.Internals.AddObject(map);
            descendant.Elements["/CIDToGIDMap"] = map.Reference;
        }

        document.Internals.AddObject(descendant);

        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type0");
        font.Elements.SetName("/BaseFont", "/Embedded");
        font.Elements.SetName("/Encoding", "/Identity-H");
        font.Elements["/DescendantFonts"] = new PdfArray(document, descendant.Reference);
        return font;
    }

    private static PdfDictionary Descriptor(PdfDocument document, byte[] program, int flags)
    {
        var file = new PdfDictionary(document);
        file.CreateStream(program);
        file.Elements.SetInteger("/Length1", program.Length);
        document.Internals.AddObject(file);

        var descriptor = new PdfDictionary(document);
        descriptor.Elements.SetName("/Type", "/FontDescriptor");
        descriptor.Elements.SetName("/FontName", "/Embedded");
        descriptor.Elements.SetInteger("/Flags", flags);
        descriptor.Elements["/FontFile2"] = file.Reference;
        document.Internals.AddObject(descriptor);
        return descriptor;
    }

    /// <summary>
    /// The smallest box around every point of a path, control points included, each side rounded
    /// to nine places so that a test can compare it exactly.
    /// </summary>
    public static Box Bounds(PagePath path)
    {
        var points = path.Points.ToArray();
        return new Box(Round(points.Min(p => p.X)), Round(points.Min(p => p.Y)),
            Round(points.Max(p => p.X)), Round(points.Max(p => p.Y)));
    }

    private static double Round(double value) => System.Math.Round(value, 9) + 0.0;

    /// <summary>How many figures a path has: one for each contour of a glyph.</summary>
    public static int Figures(PagePath path) => path.Verbs.ToArray().Count(verb => verb == PagePathVerb.MoveTo);
}

/// <summary>A box in a path's own coordinates, y upwards.</summary>
internal readonly record struct Box(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
}
