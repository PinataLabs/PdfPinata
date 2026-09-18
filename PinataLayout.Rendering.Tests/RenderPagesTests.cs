using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   <see cref="PdfDocumentRenderer.RenderPages"/> on a renderer nobody has prepared. It used to ask
///   the formatted document for its page count before checking there was a formatted document to
///   ask, so the preparation it meant to do on its own was never reached and a caller got a
///   <see cref="NullReferenceException"/> for leaving out <c>PrepareRenderPages</c>.
/// </summary>
public class RenderPagesTests
{
    static Document TwoPages()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("First");
        section.AddPageBreak();
        section.AddParagraph("Second");
        return document;
    }

    [Fact]
    public void RenderPagesPreparesARendererNobodyPrepared()
    {
        var renderer = new PdfDocumentRenderer(true) { Document = TwoPages() };

        renderer.RenderPages(1, 2);

        renderer.PdfDocument.PageCount.Should().Be(2);
    }

    [Fact]
    public void RenderPagesStillRefusesAPageTheDocumentDoesNotHave()
    {
        var renderer = new PdfDocumentRenderer(true) { Document = TwoPages() };

        var act = () => renderer.RenderPages(1, 3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
