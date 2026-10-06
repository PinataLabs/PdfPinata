using AwesomeAssertions;
using PinataLayout.Rendering;
using System.Globalization;
using System.Threading;
using TUnit.Core;
using PinataLayout.DocumentObjectModel;

namespace PdfPinata.Test;

public class PinataLayoutTurkishTest
{

    [Test]
    public void RenderDocument_TurkishCulture_NoCrashing()
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        var cultureInfo = CultureInfo.GetCultureInfo("tr-TR");
        Thread.CurrentThread.CurrentCulture = cultureInfo;
        Thread.CurrentThread.CurrentUICulture = cultureInfo;

        try
        {
            var doc = new Document();
            var printer = new PdfDocumentRenderer() { Document = doc };
            FluentActions.Invoking(printer.RenderDocument).Should().NotThrow();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            CultureInfo.CurrentCulture.ClearCachedData();
            CultureInfo.CurrentUICulture.ClearCachedData();
        }
    }
}
