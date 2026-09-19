using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   What the renderer does with an image it cannot use, and what it tells the caller about it.
/// </summary>
/// <remarks>
///   <para>
///     Every image here is an <c>IImageSource</c> written to fail, which needs no imaging backend
///     and no file on disk - the interface is six members and a test can implement it. That is also
///     how an application supplies images from a database or an HTTP response, so nothing about
///     these arrangements is artificial except the failing.
///   </para>
///   <para>
///     The one that matters most is the image of no pixels. Its extent used to be computed as NaN
///     by the aspect-ratio arithmetic - zero divided by zero - and the guard meant to catch an empty
///     size read <c>&lt;= 0</c>, which is false for NaN as every comparison against NaN is. So the
///     failure was never recorded: no placeholder was drawn, no event was raised, and an element of
///     no known height broke the page and left the next one blank.
///   </para>
/// </remarks>
public class ImageFailureTests
{
    [Fact]
    public void AnImageOfNoPixelsIsReportedRatherThanLeavingABlankPage()
    {
        var failures = Render(Failing.OfNoPixels());

        failures.Should().ContainSingle()
            .Which.Failure.Should().Be(ImageFailure.EmptySize);
    }

    [Fact]
    public void AnImageOfNoPixelsStillDrawsItsPlaceholder()
    {
        var document = Document(Failing.OfNoPixels());

        var page = Rendered.FirstPageOf(document);

        // The placeholder is a light grey rectangle with a message in it, so a page carrying one is
        // a page with something on it. A blank page is what this used to produce.
        PageContent.Of(page).Length.Should().BeGreaterThan(0);
        Rendered.Of(Document(Failing.OfNoPixels())).PageCount.Should().Be(1);
    }

    [Fact]
    public void AnImageThatWillNotDecodeIsReportedAsAnInvalidType()
    {
        var failures = Render(Failing.OfAnUnreadableType());

        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.InvalidType);
        failures[0].Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AnImageThatThrowsWhileBeingMeasuredIsReportedAsNotRead()
    {
        var failures = Render(Failing.WhenMeasured());

        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.NotRead);
        failures[0].Exception.Should().BeOfType<InvalidDataException>();
    }

    [Fact]
    public void AnImageThatThrowsWhileBeingDrawnIsReportedAsNotRead()
    {
        // Measured perfectly and failed only when its bytes were asked for, which is the one case
        // detected during the render pass rather than during formatting.
        var failures = Render(Failing.WhenDrawn());

        failures.Should().ContainSingle();
        failures[0].Failure.Should().Be(ImageFailure.NotRead);
        failures[0].Exception.Should().BeOfType<IOException>();
    }

    [Fact]
    public void TheExceptionReportedIsTheOneThatWasThrown()
    {
        var thrown = new InvalidDataException("this exact instance");

        var failures = Render(Failing.WhenMeasured(thrown));

        failures.Should().ContainSingle()
            .Which.Exception.Should().BeSameAs(thrown);
    }

    [Fact]
    public void EachFailingImageIsReportedExactlyOnce()
    {
        var document = new Document();
        var section = document.AddSection();
        foreach (var source in new[]
        {
            Failing.OfNoPixels(), Failing.OfAnUnreadableType(),
            Failing.WhenMeasured(), Failing.WhenDrawn()
        })
        {
            var image = section.AddImage(source);
            image.Width = Unit.FromCentimeter(4);
            image.Height = Unit.FromCentimeter(2.5);
        }

        Collect(document).Should().HaveCount(4);
    }

    [Fact]
    public void AnImageWithARealSizeIsNotCalledEmpty()
    {
        // The guard has to let a measurable image through. This one has no bytes behind it and so
        // may well fail later for that reason; what it must not do is fail here, which is the only
        // thing the size check decides.
        var failures = Render(Failing.OfARealSize());

        failures.Should().NotContain(failure => failure.Failure == ImageFailure.EmptySize);
    }

    // ----- arrangements -----

    static Document Document(IImageSource source)
    {
        var document = new Document();
        var image = document.AddSection().AddImage(source);
        image.Width = Unit.FromCentimeter(4);
        image.Height = Unit.FromCentimeter(2.5);
        return document;
    }

    static IReadOnlyList<ImageFailedEventArgs> Render(IImageSource source) =>
        Collect(Document(source));

    static IReadOnlyList<ImageFailedEventArgs> Collect(Document document)
    {
        var failures = new List<ImageFailedEventArgs>();

        var renderer = new PdfDocumentRenderer(true) { Document = document };

        // Reading the property is what builds the DocumentRenderer, and attaching before
        // RenderDocument is what catches everything - the events are raised as the placeholders
        // are drawn.
        renderer.DocumentRenderer.ImageFailed += (sender, e) => failures.Add(e);
        renderer.RenderDocument();

        return failures;
    }

    /// <summary>
    ///   An image source that fails at a point of the test's choosing, or not at all.
    /// </summary>
    sealed class Failing : IImageSource
    {
        readonly Func<int> _size;
        readonly Func<bool> _transparent;
        readonly Action _write;

        Failing(string name, Func<int> size, Func<bool> transparent, Action write)
        {
            Name = name;
            _size = size;
            _transparent = transparent;
            _write = write;
        }

        public string Name { get; }
        public int Width => _size();
        public int Height => _size();
        public bool Transparent => _transparent();
        public void SaveAsJpeg(MemoryStream ms) => _write();

        public PixelBuffer GetPixels()
        {
            _write();
            return default;
        }

        /// <summary>No pixels. Nothing throws; the arithmetic divides zero by zero.</summary>
        internal static IImageSource OfNoPixels() =>
            new Failing("empty.png", () => 0, () => false, () => { });

        /// <summary>
        ///   Throws while XImage is being built, which reads Transparent to choose a format.
        ///   ImageRenderer catches an InvalidOperationException from there specifically.
        /// </summary>
        internal static IImageSource OfAnUnreadableType() => new Failing(
            "unsupported.xyz", () => 64,
            () => throw new InvalidOperationException("no decoder"), () => { });

        /// <summary>Throws while being measured - XImage.PixelWidth reads through to Width.</summary>
        internal static IImageSource WhenMeasured(Exception thrown = null) => new Failing(
            "truncated.png",
            () => throw (thrown ?? new InvalidDataException("ends mid-header")),
            () => false, () => { });

        /// <summary>Measures fine and throws only when its bytes are asked for.</summary>
        internal static IImageSource WhenDrawn() => new Failing(
            "unreadable.png", () => 64, () => false,
            () => throw new IOException("the stream was closed"));

        /// <summary>Measures to a real extent, so the size check has to let it past.</summary>
        internal static IImageSource OfARealSize() => new Failing(
            "fine.png", () => 96, () => false, () => { });
    }
}
