using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Builds a page of gradients and reads back everything the gradient path wrote to it: the
///   content stream byte for byte, and every pattern, shading and extended graphics state the
///   page names.
/// </summary>
/// <remarks>
///   This is what "an opaque gradient is written exactly as it is today" is asserted against.
///   Pinning the whole saved file instead would pin its creation date and its producer string
///   as well, neither of which has anything to do with a gradient.
///   <para>
///   The dictionaries are read back from the saved document rather than inspected in memory, so
///   what is pinned is what a reader will find. Their keys come out in name order rather than in
///   the order they were written, which is the one thing here that is not byte-exact.
///   </para>
/// </remarks>
internal static class GradientOutput
{
    /// <summary>
    ///   A page carrying one of every gradient this library can draw, all of them fully opaque.
    /// </summary>
    internal static PdfDocument OpaqueGradients()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var first = new XRect(20, 20, 200, 100);
            gfx.DrawRectangle(
                new XLinearGradientBrush(first, XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal),
                first);

            var second = new XRect(20, 140, 200, 100);
            gfx.DrawRectangle(
                new XLinearGradientBrush(second, XColors.Green, XColors.Yellow, XLinearGradientMode.Vertical),
                second);

            var third = new XRect(20, 260, 200, 100);
            gfx.DrawRectangle(
                new XLinearGradientBrush(third, XColors.Black, XColors.White, XLinearGradientMode.ForwardDiagonal),
                third);

            var fourth = new XRect(20, 380, 200, 100);
            gfx.DrawRectangle(
                new XLinearGradientBrush(new XPoint(20, 380), new XPoint(220, 480), XColors.Cyan, XColors.Magenta),
                fourth);

            var fifth = new XRect(20, 500, 200, 200);
            gfx.DrawEllipse(
                new XRadialGradientBrush(new XPoint(120, 600), new XPoint(120, 600), 0, 100,
                    XColors.White, XColors.DarkBlue),
                fifth);
        }
        return document;
    }

    /// <summary>
    ///   Everything the gradient path wrote to the first page of the document.
    /// </summary>
    internal static string Of(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        var saved = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        var page = saved.Pages[0];

        var report = new StringBuilder();
        report.Append("--- content ---\n");
        report.Append(Text(PageContent.Of(page)).Replace("\r\n", "\n"));

        var resources = page.Elements.GetDictionary("/Resources");
        foreach (var kind in new[] { "/Pattern", "/Shading", "/ExtGState" })
        {
            report.Append("--- " + kind + " ---\n");
            foreach (var (name, dictionary) in EntriesOf(resources, kind))
            {
                report.Append(name + " " + dictionary + "\n");

                // A shading pattern's whole point is the shading it names, which is an
                // indirect object and so shows up as a reference above.
                var shading = dictionary.Elements.GetDictionary("/Shading");
                if (shading != null)
                    report.Append(name + "/Shading " + shading + "\n");
            }
        }

        return report.ToString();
    }

    static IEnumerable<(string Name, PdfDictionary Dictionary)> EntriesOf(PdfDictionary resources, string kind)
    {
        var map = resources?.Elements.GetDictionary(kind);
        if (map == null)
            yield break;

        foreach (var name in map.Elements.KeyNames.Select(key => key.Value).OrderBy(key => key, StringComparer.Ordinal))
            yield return (name, map.Elements.GetDictionary(name));
    }

    static string Text(byte[] bytes) => Encoding.ASCII.GetString(bytes);
}
