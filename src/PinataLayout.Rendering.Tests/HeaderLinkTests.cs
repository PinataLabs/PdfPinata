using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A hyperlink in a running header, which cannot be reached from the structure tree.
/// </summary>
/// <remarks>
///   <para>
///     A running header is an artifact: it is on the page but it is not part of what the page says,
///     and nothing inside one is tagged. That is right for the text of a header — a page number read
///     out between every paragraph is worse than no page number — but a link is not only text. It is
///     an annotation with a rectangle, and an annotation that no structure element points at is
///     found by a reader hit-testing the page and by nothing else.
///   </para>
///   <para>
///     The two rules genuinely conflict, and this is what the library does about it: it refuses,
///     naming the page, rather than writing a document that claims PDF/UA and quietly contains an
///     unreachable link. Whoever put the link there is the only one who can decide whether it should
///     be in the header at all.
///   </para>
/// </remarks>
public class HeaderLinkTests
{
    [Fact]
    public void ALinkInARunningHeaderIsReportedRatherThanQuietlyWritten()
    {
        var renderer = Claiming(linkInHeader: true);

        Saving(renderer).Should().Throw<InvalidOperationException>()
            .WithMessage("*not reachable from the structure tree*",
                "an artifact holds nothing tagged, so nothing in the tree can point at the link");
    }

    [Fact]
    public void TheSameLinkInTheBodyIsFine()
    {
        // The contrast that makes the refusal above about the header rather than about links.
        Saving(Claiming(linkInHeader: false)).Should().NotThrow();
    }

    [Fact]
    public void AHeaderWithNoLinkInItIsFine()
    {
        // The ordinary running head, which is the case that must not have been made to throw.
        Saving(Claiming(linkInHeader: false, headerText: "Statement of account")).Should().NotThrow();
    }

    private static PdfDocumentRenderer Claiming(bool linkInHeader, string headerText = "Statement")
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Sans";
        normal.Font.Size = 11;

        var section = document.AddSection();

        var header = section.Headers.Primary.AddParagraph();
        if (linkInHeader)
            header.AddHyperlink("https://example.com", HyperlinkType.Web).AddText(headerText);
        else
            header.AddText(headerText);

        var body = section.AddParagraph("Amounts are in pounds sterling. ");
        if (!linkInHeader)
            body.AddHyperlink("https://example.com", HyperlinkType.Web).AddText("Terms");

        var renderer = new PdfDocumentRenderer(true)
        {
            Document = document,
            TagContent = true,
            Language = "en-GB"
        };

        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = "Statement of account";
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;
        return renderer;
    }

    private static Action Saving(PdfDocumentRenderer renderer)
        => () => renderer.PdfDocument.Save(new MemoryStream(), false);
}
