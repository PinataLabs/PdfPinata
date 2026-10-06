using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using SampleApp.Infrastructure;
using TUnit.Core;

namespace PdfPinata.Test.Demos;

/// <summary>
///   The Inspect demo lists a page's operators with their operands, and an operand that is a
///   number is a PDF number - written with a full stop whatever the machine's language. It used to
///   be formatted in the current culture, so on a German machine the demo printed <c>0,5</c> for
///   an operand of <c>0.5</c>, and readers copy the demo app.
/// </summary>
public class InspectDemoCultureTests
{
    [Test]
    public void AnOperandIsShownWithAFullStopWhateverTheCurrentCulture()
    {
        DemoRegistry.TryGet("Inspect", out var demo).Should().BeTrue();

        // CurrentCulture follows the async flow and every test runs in a flow of its own, so this
        // does not reach a test running beside it.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        string listing;
        try
        {
            var directory = Path.Combine(PathHelper.GetInstance().RootDir, "Out", "Demos", "Inspect-de-DE");
            var result = demo.Run(new DemoContext(directory));

            using var opened = Pdf.IO.PdfReader.Open(result.OutputPath, PdfDocumentOpenMode.Import);
            listing = PdfTextExtractor.ExtractText(opened.Pages[1]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        // The listing has real operands in it, and every one of them uses a full stop.
        Regex.IsMatch(listing, @"\d\.\d").Should().BeTrue("the operator listing shows real operands");
        Regex.IsMatch(listing, @"\d,\d").Should().BeFalse("a PDF number has no decimal comma");
    }
}
