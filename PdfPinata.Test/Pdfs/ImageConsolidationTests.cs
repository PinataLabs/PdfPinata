using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   What <c>ConsolidateImages</c> decides, on a document built in memory rather than one saved,
///   reopened and merged. <c>Merge.cs</c> proves the whole pipeline gives smaller files; these
///   say which images are merged and which are left alone, which a file size cannot.
/// </summary>
public class ImageConsolidationTests
{
    static byte[] Bytes(string content) => Encoding.ASCII.GetBytes(content);

    /// <summary>
    ///   An image XObject of its own, indirect, holding the bytes given.
    /// </summary>
    static PdfDictionary AnImage(PdfDocument document, byte[] content)
    {
        var image = new PdfDictionary(document);
        image.Elements.SetName("/Type", "/XObject");
        image.Elements.SetName("/Subtype", "/Image");
        image.Elements.SetInteger("/Width", 1);
        image.Elements.SetInteger("/Height", 1);
        image.Elements.SetInteger("/BitsPerComponent", 8);
        image.Elements.SetName("/ColorSpace", "/DeviceGray");
        image.CreateStream(content);
        document.Internals.AddObject(image);
        return image;
    }

    /// <summary>
    ///   A page whose resources name the images given, as /Im0, /Im1 and so on.
    /// </summary>
    static PdfPage APageNaming(PdfDocument document, params PdfDictionary[] images)
    {
        var page = document.AddPage();

        var xObjects = new PdfDictionary(document);
        for (var i = 0; i < images.Length; i++)
            xObjects.Elements["/Im" + i] = images[i].Reference;

        var resources = new PdfDictionary(document);
        resources.Elements["/XObject"] = xObjects;
        page.Elements["/Resources"] = resources;
        return page;
    }

    static PdfObjectID ImageNamedBy(PdfPage page, string name) =>
        ((PdfReference)page.Elements.GetDictionary("/Resources")
            .Elements.GetDictionary("/XObject")
            .Elements[name]).ObjectID;

    [Fact]
    public void TwoPagesDrawingIdenticalBytesEndUpSharingOneXObject()
    {
        var document = new PdfDocument();
        var first = AnImage(document, Bytes("the same picture"));
        var second = AnImage(document, Bytes("the same picture"));
        var pageOne = APageNaming(document, first);
        var pageTwo = APageNaming(document, second);
        ImageNamedBy(pageOne, "/Im0").Should().NotBe(ImageNamedBy(pageTwo, "/Im0"),
            "two copies is the state being fixed");

        document.ConsolidateImages();

        ImageNamedBy(pageOne, "/Im0").Should().Be(ImageNamedBy(pageTwo, "/Im0"));
    }

    [Fact]
    public void ImagesDifferingByOneByteAreLeftApart()
    {
        var document = new PdfDocument();
        var pageOne = APageNaming(document, AnImage(document, Bytes("picture A")));
        var pageTwo = APageNaming(document, AnImage(document, Bytes("picture B")));

        document.ConsolidateImages();

        ImageNamedBy(pageOne, "/Im0").Should().NotBe(ImageNamedBy(pageTwo, "/Im0"),
            "only byte-identical images are merged");
    }

    [Fact]
    public void PagesThatAlreadyShareOneXObjectAreLeftAsTheyAre()
    {
        var document = new PdfDocument();
        var shared = AnImage(document, Bytes("the same picture"));
        var pageOne = APageNaming(document, shared);
        var pageTwo = APageNaming(document, shared);

        document.ConsolidateImages();

        ImageNamedBy(pageOne, "/Im0").Should().Be(shared.Reference.ObjectID);
        ImageNamedBy(pageTwo, "/Im0").Should().Be(shared.Reference.ObjectID);
    }

    [Fact]
    public void TwoIdenticalImagesOnOnePageBecomeOne()
    {
        var document = new PdfDocument();
        var page = APageNaming(document,
            AnImage(document, Bytes("the same picture")),
            AnImage(document, Bytes("the same picture")));

        document.ConsolidateImages();

        ImageNamedBy(page, "/Im0").Should().Be(ImageNamedBy(page, "/Im1"));
    }

    [Fact]
    public void ADocumentWithNoImagesIsUntouched()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.AddPage();

        var consolidate = () => document.ConsolidateImages();

        consolidate.Should().NotThrow();
        document.PageCount.Should().Be(2);
    }

    [Fact]
    public void ADocumentWithNoPagesIsUntouched()
    {
        var document = new PdfDocument();

        var consolidate = () => document.ConsolidateImages();

        consolidate.Should().NotThrow();
    }

    /// <summary>
    ///   A resource dictionary names more than images. An entry that is not an image is not a
    ///   candidate for merging and must survive untouched, whatever its bytes.
    /// </summary>
    [Fact]
    public void AnXObjectThatIsNotAnImageIsLeftAlone()
    {
        var document = new PdfDocument();
        var image = AnImage(document, Bytes("the same picture"));
        var form = AnImage(document, Bytes("the same picture"));
        form.Elements.SetName("/Subtype", "/Form");
        var page = APageNaming(document, image, form);

        document.ConsolidateImages();

        ImageNamedBy(page, "/Im1").Should().Be(form.Reference.ObjectID,
            "the form is not an image, identical bytes or not");
        ImageNamedBy(page, "/Im0").Should().Be(image.Reference.ObjectID);
    }
}
