using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   What <see cref="Saved"/> does. The private copies it replaced came in two shapes - saving into
///   a stream and rewinding it, and reading a second stream made from the first one's bytes - and
///   both handed the reader the same bytes in the mode each named, which is what is pinned here.
/// </summary>
public class SavedTests
{
    private static PdfDocument TwoPages(string title)
    {
        var document = new PdfDocument();
        document.Info.Title = title;
        _ = document.AddPage();
        _ = document.AddPage();
        return document;
    }

    [Fact]
    public void TheBytesAreAWholeFile()
    {
        var bytes = Saved.Bytes(TwoPages("Bytes"));

        var text = Encoding.ASCII.GetString(bytes);
        text.Should().StartWith("%PDF-");
        text.TrimEnd().Should().EndWith("%%EOF");
    }

    [Theory]
    [InlineData(PdfDocumentOpenMode.Modify, false)]
    [InlineData(PdfDocumentOpenMode.Append, false)]
    [InlineData(PdfDocumentOpenMode.Import, true)]
    [InlineData(PdfDocumentOpenMode.ReadOnly, true)]
    public void AReopenedDocumentIsOpenedInTheModeAskedFor(PdfDocumentOpenMode mode, bool readOnly)
    {
        var reopened = TwoPages("Reopened").Reopened(mode);

        reopened.IsImported.Should().BeTrue();
        reopened.IsReadOnly.Should().Be(readOnly);
        reopened.PageCount.Should().Be(2);
        reopened.Info.Title.Should().Be("Reopened");
    }

    [Fact]
    public void AReopenedDocumentIsOpenedToModifyUnlessToldOtherwise()
    {
        var reopened = TwoPages("Default").Reopened();

        reopened.IsReadOnly.Should().BeFalse();
        reopened.Invoking(document => document.AddPage()).Should().NotThrow();
    }

    [Fact]
    public void AnOpenedDocumentIsReadFromTheBytesGiven()
    {
        var bytes = Saved.Bytes(TwoPages("Opened"));

        var opened = Saved.Open(bytes);

        opened.IsReadOnly.Should().BeFalse();
        opened.PageCount.Should().Be(2);
        opened.Info.Title.Should().Be("Opened");
    }

    [Fact]
    public void ADocumentOpenedToAppendToCanStillWriteItsRevision()
    {
        // Append copies the original bytes when it opens; the stream Open made is never disposed
        // either way, and this is what says the revision does not depend on it.
        var appended = Saved.Open(Saved.Bytes(TwoPages("Appended")), PdfDocumentOpenMode.Append);
        appended.Info.Title = "Revised";

        using var output = new MemoryStream();
        appended.SaveIncremental(output);

        Saved.Open(output.ToArray()).Info.Title.Should().Be("Revised");
    }
}
