using PinataLayout.Rendering;
using System.Globalization;
using System.Threading;
using Xunit;
using PinataLayout.DocumentObjectModel;

namespace PdfPinata.Test;

public class PinataLayoutTurkishTest
{
    private CultureInfo _originalCulture;
    private CultureInfo _originalUiCulture;

    [Fact]
    public void RenderDocument_TurkishCulture_NoCrashing()
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        _originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var cultureInfo = CultureInfo.GetCultureInfo("tr-TR");
        Thread.CurrentThread.CurrentCulture = cultureInfo;
        Thread.CurrentThread.CurrentUICulture = cultureInfo;

        try
        {
            var doc = new Document();
            var printer = new PdfDocumentRenderer() { Document = doc };
            var exception = Record.Exception(printer.RenderDocument);

            Assert.Null(exception);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = _originalCulture;
            Thread.CurrentThread.CurrentUICulture = _originalUiCulture;
            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }
    }
}
