using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   What a footnote may hold. A note is laid out as block content of its own, so an image or a
///   text frame in one is drawn like any other; a note inside a note is not, because the inner one
///   has no page of its own to go at the foot of.
/// </summary>
public class NestedFootnoteTests
{
    [Fact]
    public void AFootnoteInsideAFootnoteIsRefusedRatherThanDropped()
    {
        var document = new Document();
        var note = document.AddSection().AddParagraph("A claim").AddFootnote("The support");
        ((Paragraph)note.Elements[0]).AddFootnote("The support's support.");

        Action render = () => Rendered.Of(document);

        // The same refusal as a note in a table cell or a header, from the same place: the inner
        // note is formatted by the outer one's own formatter, which owns no page. The DDL reader
        // warns about it as it reads, because it has a line number to give and this does not.
        render.Should().Throw<NotSupportedException>()
            .WithMessage("*another footnote*");
    }

    [Fact]
    public void AnImageInAFootnoteIsDrawn()
    {
        var document = new Document();
        var note = document.AddSection().AddParagraph("A claim").AddFootnote("The support.");
        note.AddImage(ImageSource.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "lenna.png")))
            .Width = "1cm";

        var page = Rendered.FirstPageOf(document);

        page.Resources.Elements.GetDictionary("/XObject").Should().NotBeNull("the image is drawn on the page");
        Glyphs.On(page).Should().ContainInOrder(Glyphs.For("The support."));
    }

    [Fact]
    public void ATextFrameInAFootnoteIsDrawn()
    {
        var document = new Document();
        var note = document.AddSection().AddParagraph("A claim").AddFootnote("The support.");
        var frame = note.Elements.AddTextFrame();
        frame.Width = "4cm";
        frame.Height = "1cm";
        frame.AddParagraph("Framed");

        var page = Rendered.FirstPageOf(document);

        Glyphs.On(page).Should().ContainInOrder(Glyphs.For("Framed"));
    }
}
