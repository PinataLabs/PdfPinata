using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Test.Helpers;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   An image that cannot be read is replaced by a grey placeholder so that one bad image does
///   not cost a whole document. What used to go with it was the reason: every exception was
///   swallowed into a Debug.WriteLine, which a release build compiles away, so a decode that ran
///   out of memory and a file in a format nothing supports left exactly the same trace — none.
///   See https://github.com/empira/PDFsharp/issues/366.
/// </summary>
public class ImageFailureReportingTests
{
    [Fact]
    public void AnImageThatCannotBeMeasuredIsReportedWithTheExceptionThatStoppedIt()
    {
        var thrown = new InvalidDataException("the pixels are not there");

        var failures = Render(new FailingImageSource(FailingImageSource.Stage.Measuring, thrown));

        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.NotRead);
        failures[0].Exception.Should().BeSameAs(thrown);
    }

    [Fact]
    public void AnImageThatCannotBeDrawnIsReportedWithTheExceptionThatStoppedIt()
    {
        var thrown = new InvalidDataException("the encoder gave up");

        var failures = Render(new FailingImageSource(FailingImageSource.Stage.Drawing, thrown));

        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.NotRead);
        failures[0].Exception.Should().BeSameAs(thrown);
    }

    [Fact]
    public void AnImageSourceThatCannotBeOpenedIsReportedAsAnInvalidType()
    {
        var thrown = new InvalidOperationException("no backend understands this");

        var failures = Render(new FailingImageSource(FailingImageSource.Stage.Opening, thrown));

        // The kind worked out here used to be thrown away: measuring the image that was never
        // opened threw in turn, and the NotRead that came of that overwrote it every time.
        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.InvalidType);
        failures[0].Exception.Should().BeSameAs(thrown);
    }

    [Fact]
    public void TheImageThatFailedIsTheOneReported()
    {
        var thrown = new InvalidDataException("boom");
        var reported = new List<ImageFailedEventArgs>();

        var document = new Document();
        var section = document.AddSection();
        var image = section.AddImage(new FailingImageSource(FailingImageSource.Stage.Measuring, thrown));
        image.Width = Unit.FromCentimeter(4);

        RenderTo(document, reported);

        reported.Should().ContainSingle();
        reported[0].Image.Should().BeSameAs(image);
    }

    [Theory]
    [InlineData(FailingImageSource.Stage.Opening)]
    [InlineData(FailingImageSource.Stage.Measuring)]
    [InlineData(FailingImageSource.Stage.Drawing)]
    public void AnOutOfMemoryExceptionIsNotSwallowed(FailingImageSource.Stage stage)
    {
        // Running out of memory says nothing about the image and everything about the process
        // rendering it. Turning that into a grey box leaves the caller none the wiser and the
        // process to fall over somewhere else.
        var render = () => Render(new FailingImageSource(stage, new OutOfMemoryException()));

        render.Should().Throw<OutOfMemoryException>();
    }

    [Fact]
    public void ADocumentWithAnUnreadableImageStillRenders()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Above the image");
        section.AddImage(new FailingImageSource(
            FailingImageSource.Stage.Measuring, new InvalidDataException("boom")));
        section.AddParagraph("Below the image");

        var renderer = RenderTo(document, []);

        renderer.PdfDocument.PageCount.Should().Be(1);
    }

    [Fact]
    public void AnImageThatReadsFineIsNotReported()
    {
        var reported = new List<ImageFailedEventArgs>();
        var document = new Document();
        document.AddSection().AddImage(
            FromFile(PathHelper.GetInstance().GetAssetPath("lenna.png")));

        RenderTo(document, reported);

        reported.Should().BeEmpty();
    }

    private static IReadOnlyList<ImageFailedEventArgs> Render(IImageSource source)
    {
        var reported = new List<ImageFailedEventArgs>();
        var document = new Document();
        var image = document.AddSection().AddImage(source);
        // A size the document gives, so that the placeholder is not sized from the image that
        // could not be read.
        image.Width = Unit.FromCentimeter(4);

        RenderTo(document, reported);

        return reported;
    }

    private static PdfDocumentRenderer RenderTo(Document document, List<ImageFailedEventArgs> reported)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.DocumentRenderer.ImageFailed += (_, e) => reported.Add(e);
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);

        return renderer;
    }

    /// <summary>
    ///   An image source that fails at whichever point of reading an image it is told to.
    /// </summary>
    public sealed class FailingImageSource : IImageSource
    {
        public enum Stage
        {
            /// <summary>Turning the source into an image at all.</summary>
            Opening,

            /// <summary>Asking the image how big it is, to lay it out.</summary>
            Measuring,

            /// <summary>Encoding the image into the page.</summary>
            Drawing
        }

        private readonly Stage _stage;
        private readonly Exception _exception;

        public FailingImageSource(Stage stage, Exception exception)
        {
            _stage = stage;
            _exception = exception;
        }

        public string Name => "*failing-image-source";

        // Read while the XImage is being constructed.
        public bool Transparent => FailAt(Stage.Opening) ? throw _exception : false;

        public int Width => FailAt(Stage.Measuring) ? throw _exception : 100;

        public int Height => FailAt(Stage.Measuring) ? throw _exception : 100;

        public void SaveAsJpeg(MemoryStream ms)
        {
            if (FailAt(Stage.Drawing))
                throw _exception;
        }

        public PixelBuffer GetPixels()
        {
            if (FailAt(Stage.Drawing))
                throw _exception;

            return default;
        }

        private bool FailAt(Stage stage)
        {
            return _stage == stage;
        }
    }
}
