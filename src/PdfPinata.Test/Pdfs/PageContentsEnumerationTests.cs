using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   <see cref="PdfContents"/> is a page's array of content streams. It hides
///   <see cref="PdfArray.GetEnumerator"/> behind one typed as <see cref="PdfContent"/>, and every
///   other way of enumerating it - as an array, as <see cref="IEnumerable{T}"/> of
///   <see cref="PdfItem"/>, which is what LINQ sees, and as plain <see cref="IEnumerable"/> - has to
///   yield the same streams rather than the references underneath them.
/// </summary>
public class PageContentsEnumerationTests
{
    private static (PdfContents Contents, PdfContent First, PdfContent Second) TwoContentStreams()
    {
        var page = new PdfDocument().AddPage();
        var first = page.Contents.AppendContent();
        var second = page.Contents.AppendContent();
        return (page.Contents, first, second);
    }

    [Fact]
    public void ForeachYieldsTheContentStreams()
    {
        var (contents, first, second) = TwoContentStreams();

        var seen = new List<PdfContent>();
        foreach (var content in contents)
            seen.Add(content);

        seen.Should().Equal(first, second);
    }

    [Fact]
    public void EnumeratedWithoutATypeItStillYieldsTheContentStreams()
    {
        var (contents, first, second) = TwoContentStreams();

        var untyped = new List<object>();
        foreach (var content in (IEnumerable)contents)
            untyped.Add(content);

        untyped.Should().Equal(first, second);
    }

    [Fact]
    public void EnumeratedAsAnArrayItStillYieldsTheContentStreams()
    {
        var (contents, first, second) = TwoContentStreams();

        var asArray = new List<PdfItem>();
        foreach (var item in (PdfArray)contents)
            asArray.Add(item);

        asArray.Should().Equal(first, second);
    }

    [Fact]
    public void LinqSeesTheContentStreamsRatherThanTheirReferences()
    {
        var (contents, first, second) = TwoContentStreams();

        // Before: IEnumerable<PdfItem> yielded the PdfReferences, so OfType found nothing and Cast
        // threw.
        contents.OfType<PdfContent>().Should().Equal(first, second);
        contents.Cast<PdfContent>().Should().Equal(first, second);
        contents.OfType<PdfReference>().Should().BeEmpty();
        contents.Count().Should().Be(contents.Elements.Count);
    }

    [Fact]
    public void AContentStreamReadFromAFileIsEnumeratedTheSameWayEverywhere()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawLine(XPens.Black, 10, 10, 100, 100);
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawLine(XPens.Black, 100, 10, 10, 100);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var contents = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0].Contents;

        var typed = new List<PdfContent>();
        foreach (var content in contents)
            typed.Add(content);

        typed.Should().HaveCount(2);
        contents.OfType<PdfContent>().Should().Equal(typed);
        ((IEnumerable)contents).Cast<object>().Should().Equal(typed);
    }
}
