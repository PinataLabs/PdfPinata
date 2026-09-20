using System;
using System.ComponentModel;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The parts of <see cref="PdfDocument"/> and <see cref="PdfPage"/> a caller reaches directly:
///   the constructor that writes to a stream when the document is closed, the four page boxes
///   beyond the media box, the links a page can carry, and what each of them refuses.
/// </summary>
public class DocumentAndPageSurfaceTests
{
    // ----- the document -----------------------------------------------------------------------------

    /// <summary>
    ///   This constructor wrote an unreadable file. It is the only one of the three that never set
    ///   the PDF version, so the field stayed 0 and the header read "%PDF-0.0" — enough bytes to
    ///   look like a save had worked, and refused by every reader including this one.
    /// </summary>
    [Fact]
    public void ADocumentBuiltOnAStreamWritesItselfWhenItIsClosed()
    {
        var output = new MemoryStream();
        var document = new PdfDocument(output);
        document.AddPage();

        document.Close();

        // Close disposes the stream it wrote to, so what it wrote has to be read back out of the
        // buffer rather than out of the stream.
        var written = output.ToArray();

        written.Length.Should().BeGreaterThan(0);
        System.Text.Encoding.ASCII.GetString(written, 0, 8).Should().Be("%PDF-1.4");
        Reader.Open(new MemoryStream(written), PdfDocumentOpenMode.ReadOnly).PageCount.Should().Be(1);
    }

    [Fact]
    public void ClosingADocumentThatWasNotGivenAStreamWritesNothingAndDoesNotThrow()
    {
        var document = new PdfDocument();
        document.AddPage();

        var closing = () => document.Close();

        closing.Should().NotThrow();
    }

    [Fact]
    public void ADocumentBuiltOnAStreamWithNoPagesSaysWhichConstructorWasWrong()
    {
        var document = new PdfDocument(new MemoryStream());

        var closing = () => document.Close();

        closing.Should().Throw<InvalidOperationException>()
            .WithMessage("*PdfReader.Open*");
    }

    [Fact]
    public void ADocumentBuiltForAFileIsNotImplemented()
    {
        var building = () => new PdfDocument("nowhere.pdf");

        building.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void ADocumentCarriesATagForItsCaller()
    {
        var document = new PdfDocument();
        var tag = new object();

        document.Tag = tag;

        document.Tag.Should().BeSameAs(tag);
        document.FullPath.Should().BeEmpty("a document that was never read from a file has no path");
    }

    [Fact]
    public void AVersionOutsideWhatThisLibraryWritesIsRefused()
    {
        var document = new PdfDocument();

        var setting = () => document.Version = 11;

        setting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ADocumentEncryptedWithNoPasswordAtAllCannotBeSaved()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;

        var message = "";
        document.CanSave(ref message).Should().BeFalse();
        message.Should().NotBeEmpty();

        var saving = () => document.Save(new MemoryStream(), false);
        saving.Should().Throw<PdfSharpException>();
    }

    [Fact]
    public void CustomValuesOnADocumentCanOnlyBeClearedByAssigningNothing()
    {
        var document = new PdfDocument();
        document.AddPage();

        document.CustomValues.Should().NotBeNull();

        var assigning = () => document.CustomValues = document.CustomValues;
        assigning.Should().Throw<ArgumentException>();

        document.CustomValues = null;
        document.CustomValues.Should().NotBeNull("asking again builds a fresh one");
    }

    [Fact]
    public void ResizingEveryPageToAPageSizeThatDoesNotExistIsRefused()
    {
        var document = new PdfDocument();
        document.AddPage();

        var resizing = () => document.ResizePages((PageSize)999);

        resizing.Should().Throw<InvalidEnumArgumentException>();
    }

    [Fact]
    public void EveryFieldOfAFormCanBeMadeReadOnlyAtOnce()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        var field = new PdfTextField(document) { Name = "name" };
        form.Fields.Add(field);
        field.AddWidget(page, new PdfRectangle(new XRect(10, 10, 100, 20)));

        document.MakeAcroFormsReadOnly();

        form.Fields[0].ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void ADocumentWithNoFormAtAllIsStillHappyToBeMadeReadOnly()
    {
        var document = new PdfDocument();
        document.AddPage();

        var making = () => document.MakeAcroFormsReadOnly();

        making.Should().NotThrow();
    }

    // ----- the page ---------------------------------------------------------------------------------

    [Fact]
    public void APageCarriesATagForItsCaller()
    {
        var page = new PdfDocument().AddPage();
        var tag = new object();

        page.Tag = tag;

        page.Tag.Should().BeSameAs(tag);
    }

    [Fact]
    public void APageAnswersTheSizeAndOrientationItWasGiven()
    {
        var page = new PdfDocument().AddPage();

        page.Size = PageSize.A5;
        page.Orientation = PageOrientation.Landscape;

        page.Size.Should().Be(PageSize.A5);
        page.Orientation.Should().Be(PageOrientation.Landscape);
        page.Width.Point.Should().BeGreaterThan(page.Height.Point);
    }

    [Fact]
    public void APageSizeThatDoesNotExistIsRefused()
    {
        var page = new PdfDocument().AddPage();

        ((Action)(() => page.Size = (PageSize)999)).Should().Throw<InvalidEnumArgumentException>();
        ((Action)(() => page.Resize((PageSize)999))).Should().Throw<InvalidEnumArgumentException>();
    }

    [Fact]
    public void ARotationThatIsNotAQuarterTurnIsRefused()
    {
        var page = new PdfDocument().AddPage();

        var setting = () => page.Rotate = 45;

        setting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheThreeBoxesBesideTheMediaAndCropBoxAreReadAndWritten()
    {
        var page = new PdfDocument().AddPage();
        var box = new PdfRectangle(new XRect(10, 10, 200, 300));

        page.BleedBox = box;
        page.TrimBox = box;
        page.ArtBox = box;

        page.BleedBox.X1.Should().Be(10);
        page.TrimBox.Y2.Should().Be(310);
        page.ArtBox.X2.Should().Be(210);
    }

    /// <summary>
    ///   Closing a page is documented as optional and as freeing resources. Nothing in the library
    ///   reads the flag it sets, so what is worth pinning is that it is harmless: a page still
    ///   saves after it, and closing twice is not an error.
    /// </summary>
    [Fact]
    public void ClosingAPageLeavesTheDocumentStillSaveable()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawLine(XPens.Black, 0, 0, 10, 10);

        page.Close();
        page.Close();

        var output = new MemoryStream();
        document.Save(output, false);

        output.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AFileLinkIsAddedToThePageAsAnAnnotation()
    {
        var page = new PdfDocument().AddPage();

        var link = page.AddFileLink(new PdfRectangle(new XRect(10, 10, 100, 20)), "attachment.pdf");

        link.Should().NotBeNull();
        page.Annotations.Count.Should().Be(1);
        page.HasAnnotations.Should().BeTrue();
    }

    [Fact]
    public void CustomValuesOnAPageCanOnlyBeClearedByAssigningNothing()
    {
        var page = new PdfDocument().AddPage();

        page.CustomValues.Should().NotBeNull();

        var assigning = () => page.CustomValues = page.CustomValues;
        assigning.Should().Throw<ArgumentException>();

        page.CustomValues = null;
        page.CustomValues.Should().NotBeNull();
    }
}
