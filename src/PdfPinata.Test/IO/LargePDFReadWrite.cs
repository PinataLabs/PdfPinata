using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using PdfPinata.Test.IO;
using TUnit.Core;

namespace PdfPinata.Test;

public class LargePDFReadWrite : IoBaseTest
{
    // Writing 70,000 pages to reach 2 GB takes minutes; it is run by hand, never by the suite.
    [Test, Skip("Too slow for Unit test runner")]
    public void CanCreatePdfOver2Gb()
    {
        const string outName = "CreateLargePdf.pdf";
        var pageCount = 70000; //2.1gb @ 369sec to create
        ValidateTargetAvailable(outName);

        var document = new PdfDocument();
        var watch = new System.Diagnostics.Stopwatch();
        var font = new XFont("Arial", 10);

        watch.Start();
        for (var i = 0; i < pageCount; i++)
        {
            AddAPage(document, font);
        }

        watch.Stop();

        SaveDocument(document, outName);
        TestContext.Current!.Output.WriteLine($"CreatePDF took {watch.Elapsed.TotalSeconds} sec");
        ValidateFileIsPdf(outName);
        CanReadPdf(outName);
    }

    private static void AddAPage(PdfDocument document, XFont font)
    {
        const int x = 40;
        const int y = 50;
        var page = document.AddPage();
        var renderer = XGraphics.FromPdfPage(page);
        var tf = new XTextFormatter(renderer);
        var width = page.Width.Value - 50 - x;
        var height = page.Height.Value - 50 - y;
        var rect = new XRect(40, 50, width, height);
        renderer.DrawRectangle(XBrushes.SeaShell, rect);
        tf.DrawString(TestData.LoremIpsumText, font, XBrushes.Black, rect);
    }
}
