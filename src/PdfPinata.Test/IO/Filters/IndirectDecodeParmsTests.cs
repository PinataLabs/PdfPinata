using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
///   A stream's <c>/Filter</c> and <c>/DecodeParms</c> may each be an indirect object, and so may
///   every element of either when it is an array. The decoder tested the entries for a name, a
///   dictionary or an array as they stood, a reference is none of those, and a stream whose
///   parameters were kept in an object of their own came back as "Cannot decode filter"
///   (empira/PDFsharp#323).
/// </summary>
public class IndirectDecodeParmsTests
{
    private const string Content = "0 0 m 100 100 l S\n";

    /// <summary>
    ///   The content above run through PNG prediction (one row, filter type None) and deflated,
    ///   so that it decodes only when the /Predictor in its parameters is actually read.
    /// </summary>
    private static byte[] Encoded()
    {
        var predicted = new[] { (byte)0 }.Concat(Encoding.ASCII.GetBytes(Content)).ToArray();
        return Filtering.FlateDecode.Encode(predicted);
    }

    private static string Parms => $"<< /Predictor 12 /Columns {Content.Length} >>";

    /// <summary>
    ///   A one-page file whose content stream, object 4, carries the entries given, with the
    ///   extra objects given numbered from 5.
    /// </summary>
    private static byte[] File(string streamEntries, params string[] extraObjects)
    {
        var data = Encoded();
        var objects = new List<byte[]>
        {
            Latin1("<< /Type /Catalog /Pages 2 0 R >>"),
            Latin1("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Latin1("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>"),
            Latin1($"<< /Length {data.Length} {streamEntries} >>\nstream\n")
                .Concat(data).Concat(Latin1("\nendstream")).ToArray()
        };
        objects.AddRange(extraObjects.Select(Latin1));

        using var file = new MemoryStream();
        void Write(byte[] bytes) => file.Write(bytes, 0, bytes.Length);

        Write(Latin1("%PDF-1.5\n"));
        var offsets = new List<long>();
        for (var idx = 0; idx < objects.Count; idx++)
        {
            offsets.Add(file.Position);
            Write(Latin1($"{idx + 1} 0 obj\n"));
            Write(objects[idx]);
            Write(Latin1("\nendobj\n"));
        }

        var xref = file.Position;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            table.Append($"{offset:D10} 00000 n \n");
        table.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Write(Latin1(table.ToString()));

        return file.ToArray();
    }

    private static byte[] Latin1(string text) => [..text.Select(ch => (byte)ch)];

    private static string DecodedContent(byte[] file)
    {
        using var stream = new MemoryStream(file);
        var page = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages[0];
        return Encoding.ASCII.GetString(PageContent.Of(page));
    }

    [Fact]
    public void TheFileDecodesWithItsParametersDirect()
    {
        // The control: the same stream, nothing indirect.
        DecodedContent(File($"/Filter /FlateDecode /DecodeParms {Parms}")).Should().Be(Content);
    }

    [Fact]
    public void ParametersInAnObjectOfTheirOwnAreFollowed()
    {
        DecodedContent(File("/Filter /FlateDecode /DecodeParms 5 0 R", Parms)).Should().Be(Content);
    }

    [Fact]
    public void AnArrayOfParametersWhoseElementIsAReferenceIsFollowed()
    {
        DecodedContent(File("/Filter [/FlateDecode] /DecodeParms [5 0 R]", Parms)).Should().Be(Content);
    }

    [Fact]
    public void AnArrayOfParametersThatIsItselfAReferenceIsFollowed()
    {
        DecodedContent(File("/Filter [/FlateDecode] /DecodeParms 5 0 R", "[6 0 R]", Parms)).Should().Be(Content);
    }

    [Fact]
    public void AFilterNamedInAnObjectOfItsOwnIsFollowed()
    {
        DecodedContent(File("/Filter 5 0 R /DecodeParms 6 0 R", "[/FlateDecode]", $"[{Parms}]")).Should().Be(Content);
    }

    [Fact]
    public void ANullInAnArrayOfParametersMeansThatFilterTakesItsDefaults()
    {
        // ISO 32000-1 Table 5: a filter with default parameters has null in its place.
        var data = Encoded();
        var hexed = string.Concat(data.Select(b => b.ToString("X2"))) + ">";
        var file = File("/Filter [/ASCIIHexDecode /FlateDecode] /DecodeParms [null 5 0 R]", Parms);

        // Swap the stream data for its hex form, so the chain has two filters to walk.
        var text = new string(file.Select(b => (char)b).ToArray());
        var raw = new string(data.Select(b => (char)b).ToArray());
        text = text.Replace($"/Length {data.Length} ", $"/Length {hexed.Length} ").Replace(raw, hexed);

        DecodedContent(Latin1(Reindexed(text))).Should().Be(Content);
    }

    /// <summary>
    ///   Rewrites the cross-reference table of a file built by <see cref="File"/> after its body has
    ///   been edited, so that the offsets are true again.
    /// </summary>
    private static string Reindexed(string text)
    {
        var body = text.Substring(0, text.IndexOf("xref\n", System.StringComparison.Ordinal));
        var offsets = new List<int>();
        for (var number = 1; ; number++)
        {
            var at = body.IndexOf($"\n{number} 0 obj\n", System.StringComparison.Ordinal);
            if (at < 0)
                break;
            offsets.Add(at + 1);
        }

        var table = new StringBuilder($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            table.Append($"{offset:D10} 00000 n \n");
        table.Append($"trailer\n<< /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{body.Length}\n%%EOF\n");
        return body + table;
    }
}
