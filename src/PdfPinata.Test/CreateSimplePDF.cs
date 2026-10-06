using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TUnit.Core;

namespace PdfPinata.Test;

public class CreateSimplePdf
{
    private readonly string _rootPath = PathHelper.GetInstance().RootDir;
    private const string OutputDirName = "Out";

    [Test]
    public void CreateTestPdf()
    {
        const string outName = "test1.pdf";

        ValidateTargetAvailable(outName);

        var document = new PdfDocument();

        var pageNewRenderer = document.AddPage();

        var renderer = XGraphics.FromPdfPage(pageNewRenderer);

        renderer.DrawString("Testy Test Test", new XFont("Arial", 12), XBrushes.Black, new XPoint(12, 12));

        SaveDocument(document, outName);
        ValidateFileIsPdf(outName);
    }

    [Test]
    public void CreateTestPdfWithUnicodeMetadata()
    {
        const string data = "English, Ελληνικά, 漢語";

        var document = new PdfDocument();
        document.Info.Title = data;
        document.Info.Subject = data;
        document.Info.Author = data;

        using var ms = new MemoryStream();
        _ = document.AddPage();
        document.Save(ms);
        ms.Position = 0;

        var generatedDocument = Pdf.IO.PdfReader.Open(ms);

        generatedDocument.Info.Title.Should().Be(data);
        generatedDocument.Info.Subject.Should().Be(data);
        generatedDocument.Info.Author.Should().Be(data);
    }

    [Test]
    public void CreateTestPdfWithImage()
    {
        using var stream = new MemoryStream();
        var document = new PdfDocument();

        var pageNewRenderer = document.AddPage();

        var renderer = XGraphics.FromPdfPage(pageNewRenderer);

        renderer.DrawImage(XImage.FromFile(PathHelper.GetInstance().GetAssetPath("lenna.png")), new XPoint(0, 0));

        document.Save(stream);
        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(1);
        ReadStreamAndVerifyPdfHeaderSignature(stream);
    }

    [Test]
    public void CreateTestPdfWithImageViaImageSharp()
    {
        using var stream = new MemoryStream();
        var document = new PdfDocument();

        var pageNewRenderer = document.AddPage();

        var renderer = XGraphics.FromPdfPage(pageNewRenderer);

        // Load image for ImageSharp and apply a simple mutation:
        var image = Image.Load<Rgb24>(PathHelper.GetInstance().GetAssetPath("lenna.png"), out var format);
        image.Mutate(ctx => ctx.Grayscale());

        // create XImage from that same ImageSharp image:
        var source = ImageSharpImageSource<Rgb24>.FromImageSharpImage(image, format);
        var img = XImage.FromImageSource(source);

        renderer.DrawImage(img, new XPoint(0, 0));

        document.Save(stream);
        stream.Position = 0;
        stream.Length.Should().BeGreaterThan(1);
        ReadStreamAndVerifyPdfHeaderSignature(stream);
    }

    private void SaveDocument(PdfDocument document, string name)
    {
        var outFilePath = GetOutFilePath(name);
        var dir = Path.GetDirectoryName(outFilePath);
        if (!Directory.Exists(dir))
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(dir);
        }

        document.Save(outFilePath);
    }

    private void ValidateFileIsPdf(string v)
    {
        var path = GetOutFilePath(v);
        File.Exists(path).Should().BeTrue();
        var fi = new FileInfo(path);
        fi.Length.Should().BeGreaterThan(1);

        using var stream = File.OpenRead(path);
        ReadStreamAndVerifyPdfHeaderSignature(stream);
    }

    private static void ReadStreamAndVerifyPdfHeaderSignature(Stream stream)
    {
        var readBuffer = new byte[5];
        var pdfSignature = "%PDF-"u8.ToArray(); // PDF must start with %PDF-

        var bytesRead = stream.Read(readBuffer, 0, readBuffer.Length);

        bytesRead.Should().Be(readBuffer.Length);
        readBuffer.Should().Equal(pdfSignature);
    }

    private void ValidateTargetAvailable(string file)
    {
        var path = GetOutFilePath(file);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        File.Exists(path).Should().BeFalse();
    }

    private string GetOutFilePath(string name)
    {
        return Path.Combine(_rootPath, OutputDirName, name);
    }
}
