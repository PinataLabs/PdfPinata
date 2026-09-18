using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Lays a page of text out through <see cref="XTextFormatter"/> every way the formatter can be
///   asked to lay one out, and reads back the content stream it wrote.
/// </summary>
/// <remarks>
///   This is what "a block with nothing narrowing it is laid out exactly as before" is asserted
///   against. The measure becoming a function of the line's position touches the loop that breaks
///   every line of every document this library has ever written, and a layout regression is silent:
///   the page still has all its words on it, in slightly the wrong places.
///   <para>
///   The content stream rather than the saved file, because the file carries a creation date and a
///   document identifier that have nothing to do with layout.
///   </para>
/// </remarks>
internal static class FormatterOutput
{
    /// <summary>
    ///   Enough prose to break over several lines in a column of any width worth testing.
    /// </summary>
    const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be.\n" +
        "A second paragraph, so that the paragraph gap and the first-line indent both have " +
        "somewhere to show themselves, and so that a line break written into the text is exercised " +
        "beside the ones the formatter works out for itself.";

    /// <summary>
    ///   One page per way of laying text out, and the content of every one of them.
    /// </summary>
    internal static string OfEveryArrangement()
    {
        var document = new PdfDocument();
        var font = new XFont("Arial", 10);
        var area = new XRect(40, 40, 400, 240);

        foreach (var (name, arrange) in Arrangements())
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);

            var formatter = new XTextFormatter(gfx);
            arrange(formatter);
            formatter.DrawString(Prose, font, XBrushes.Black, area);
        }

        return Of(document);
    }

    /// <summary>
    ///   What every arrangement is called, in the order the pages come in.
    /// </summary>
    internal static IReadOnlyList<string> ArrangementNames
    {
        get
        {
            var names = new List<string>();
            foreach (var (name, _) in Arrangements())
                names.Add(name);
            return names;
        }
    }

    /// <summary>
    ///   Every arrangement, named so a failure says which one moved.
    /// </summary>
    static IEnumerable<(string Name, Action<XTextFormatter> Arrange)> Arrangements()
    {
        yield return ("plain", _ => { });
        yield return ("justified", f => f.Alignment = XParagraphAlignment.Justify);
        yield return ("centred", f => f.Alignment = XParagraphAlignment.Center);
        yield return ("right", f => f.Alignment = XParagraphAlignment.Right);
        yield return ("indented", f => f.Indent = 18);
        yield return ("indent every line", f => { f.Indent = 18; f.IndentAllLines = true; });
        yield return ("paragraph gap", f => f.ParagraphGap = 8);
        yield return ("line gap", f => f.LineGap = 4);
        yield return ("two columns", f => f.Columns = 2);
        yield return ("three columns, justified", f =>
        {
            f.Columns = 3;
            f.ColumnGap = 12;
            f.Alignment = XParagraphAlignment.Justify;
        });
        yield return ("ellipsis", f => f.Ellipsis = XTextFormatter.DefaultEllipsis);
        yield return ("no line break", f => f.LineBreak = false);
        yield return ("vertical overflow", f => f.AllowVerticalOverflow = true);
        yield return ("middle", f => f.VerticalAlignment = XVerticalAlignment.Middle);
        yield return ("bottom", f => f.VerticalAlignment = XVerticalAlignment.Bottom);
        yield return ("rotated", f => f.Rotation = 12);
        yield return ("indented and justified in two columns", f =>
        {
            f.Columns = 2;
            f.Indent = 14;
            f.Alignment = XParagraphAlignment.Justify;
        });
    }

    /// <summary>
    ///   The content stream of every page of the document, in order.
    /// </summary>
    internal static string Of(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        var saved = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);

        var report = new StringBuilder();
        var names = new List<string>();
        foreach (var (name, _) in Arrangements())
            names.Add(name);

        for (var page = 0; page < saved.PageCount; page++)
        {
            report.Append("--- ").Append(page < names.Count ? names[page] : "page " + page).Append(" ---\n");
            report.Append(Encoding.ASCII.GetString(PageContent.Of(saved.Pages[page])).Replace("\r\n", "\n"));
            report.Append('\n');
        }

        return report.ToString();
    }
}
