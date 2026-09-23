using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.BarCodes;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using ZXing;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Draws a DataMatrix code onto a page and reads the modules back out of what was drawn, so
///   that the tests judge the code a caller actually gets rather than anything held behind the
///   drawing.
/// </summary>
internal static class DataMatrixModules
{
    /// <summary>
    ///   The modules of the code, indexed by row then column from the top left, true where
    ///   the module is dark.
    /// </summary>
    internal static bool[,] Of(string text, int rows, int columns, string encoding = "")
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            // One point to a module, drawn from the top left corner, so that the rectangles
            // in the content stream fall on whole numbers and map straight back to modules.
            var code = new CodeDataMatrix(text, encoding, rows, columns, 0, new XSize(columns, rows));
            code.Anchor = AnchorType.TopLeft;
            gfx.DrawMatrixCode(code, new XPoint(0, 0));
        }

        return ReadModules(document, rows, columns);
    }

    private static bool[,] ReadModules(PdfDocument document, int rows, int columns)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        var reopened = Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
        var page = reopened.Pages[0];

        var item = page.Elements["/Contents"];
        if (item is PdfReference reference)
            item = reference.Value;

        var content = Encoding.Latin1.GetString(((PdfDictionary)item).Stream.UnfilteredValue);
        var pageHeight = page.Height.Point;

        var modules = new bool[rows, columns];
        foreach (Match match in Regex.Matches(content, @"(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) re"))
        {
            var x = Number(match, 1);
            var y = Number(match, 2);
            var width = Number(match, 3);
            var height = Number(match, 4);

            // PDF counts up from the foot of the page; the grid counts down from the top.
            var row = (int)Math.Round(pageHeight - y - height);
            var column = (int)Math.Round(x);

            for (var across = 0; across < (int)Math.Round(width); across++)
            {
                if (row >= 0 && row < rows && column + across >= 0 && column + across < columns)
                    modules[row, column + across] = true;
            }
        }

        return modules;
    }

    private static double Number(Match match, int group)
    {
        return double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///   What an independent reader makes of the modules, or null where it cannot read them.
    ///   ZXing is a separate implementation of the standard, so agreeing with it is worth more
    ///   than agreeing with anything this library could be asked about itself.
    /// </summary>
    internal static string Read(bool[,] modules)
    {
        var rows = modules.GetLength(0);
        var columns = modules.GetLength(1);
        const int quietZone = 4;
        const int scale = 4;

        var width = (columns + 2 * quietZone) * scale;
        var height = (rows + 2 * quietZone) * scale;

        var luminance = new byte[width * height];
        for (var at = 0; at < luminance.Length; at++)
            luminance[at] = 255;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (!modules[row, column])
                    continue;

                for (var down = 0; down < scale; down++)
                for (var across = 0; across < scale; across++)
                    luminance[((row + quietZone) * scale + down) * width
                              + (column + quietZone) * scale + across] = 0;
            }
        }

        var source = new RGBLuminanceSource(luminance, width, height,
            RGBLuminanceSource.BitmapFormat.Gray8);
        var reader = new BarcodeReaderGeneric
        {
            Options = new ZXing.Common.DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.DATA_MATRIX],
                TryHarder = true
            }
        };

        var result = reader.Decode(source);
        return result?.Text;
    }
}
