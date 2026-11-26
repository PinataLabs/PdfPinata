using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// A content stream whose Flate data is cut off in its Adler-32 checksum, or carries a wrong one,
/// is drawn as far as it inflated, as pdf.js and MuPDF draw it, and the damage is reported (#440).
/// </summary>
public class DamagedContentTests
{
    private const string Square = "0 0 5 5 re f";

    private static byte[] Damaged(string content, bool cut)
    {
        var flate = Filtering.FlateDecode.Encode(Encoding.Latin1.GetBytes(content));
        if (cut)
            return flate[..^4];

        flate[^1] ^= 0xFF;
        return flate;
    }

    private static PdfDictionary Flate(PdfDictionary stream)
    {
        stream.Elements.SetName("/Filter", "/FlateDecode");
        return stream;
    }

    [Test, Timeout(30_000)]
    [Arguments(true)]
    [Arguments(false)]
    public async Task APageWhoseContentHasItsChecksumMissingOrWrongIsDrawnAndReported(bool cut)
    {
        var page = Drawn.PageOf(Damaged(Square, cut));
        Flate(page.Contents.Elements.GetDictionary(0));
        var device = new RecordingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        device.Fills.Should().ContainSingle("the square inflated before the damage");
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("InvalidValue page");
        result.Diagnostics.Single().Message.Should().Contain("damaged");
    }

    [Test, Timeout(30_000)]
    [Arguments(true)]
    [Arguments(false)]
    public async Task APageWrittenInSeveralStreamsDrawsADamagedOneWithTheRest(bool cut)
    {
        var page = Drawn.PageOf(Damaged(Square, cut));
        Flate(page.Contents.Elements.GetDictionary(0));
        page.Contents.AppendContent().CreateStream(Encoding.Latin1.GetBytes("10 10 5 5 re f"));
        var device = new RecordingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        device.Fills.Should().HaveCount(2, "both streams are drawn");
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("InvalidValue page");
    }

    [Test, Timeout(30_000)]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AFormWhoseContentHasItsChecksumMissingOrWrongIsDrawnAndReported(bool cut)
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            var form = Flate(Drawn.Form(document, ""));
            form.Stream.Value = Damaged(Square, cut);
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        }));

        drawn.Device.Fills.Should().ContainSingle("the form's square inflated before the damage");
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
        drawn.Result.Diagnostics.Single().Message.Should().Contain("damaged");
    }

    [Test, Timeout(30_000)]
    public async Task APageWhoseContentInflatesToNothingIsStillUnreadable()
    {
        var page = Drawn.PageOf(Filtering.FlateDecode.Encode(Encoding.Latin1.GetBytes(Square))[..2]);
        Flate(page.Contents.Elements.GetDictionary(0));
        var device = new RecordingDevice();

        var result = await Interruptibly.Run(() => PageRendering.Render(page, device));

        device.Fills.Should().BeEmpty();
        result.Diagnostics.Select(d => d.Problem + " " + d.Subject).Should().Equal("UnreadableContent page");
    }
}
