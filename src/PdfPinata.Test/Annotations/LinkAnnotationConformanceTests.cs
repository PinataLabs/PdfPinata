using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   A link annotation carried no <c>/F</c> at all, which every PDF/A part but the earliest and
///   PDF/UA-2 alike refuse: an annotation dictionary — other than a Popup — has to say it prints and
///   is neither hidden, invisible nor kept out of view. Found by veraPDF on the first corpus document
///   to combine an archival or PDF/UA-2 claim with a hyperlink, which is what
///   <c>docs/specs/conformance-completeness.md</c>'s new corpus documents are the first to do.
/// </summary>
public class LinkAnnotationConformanceTests
{
    [Fact]
    public void ADocumentLinkPrintsAndIsNeitherHiddenNorInvisibleNorKeptOutOfView()
    {
        var link = PdfLinkAnnotation.CreateDocumentLink(new PdfRectangle(new XRect(0, 0, 10, 10)), 1);

        AssertsPdfARequiresOf(link);
    }

    [Fact]
    public void AWebLinkPrintsAndIsNeitherHiddenNorInvisibleNorKeptOutOfView()
    {
        var link = PdfLinkAnnotation.CreateWebLink(new PdfRectangle(new XRect(0, 0, 10, 10)), "https://example.org");

        AssertsPdfARequiresOf(link);
    }

    [Fact]
    public void AFileLinkPrintsAndIsNeitherHiddenNorInvisibleNorKeptOutOfView()
    {
        var link = PdfLinkAnnotation.CreateFileLink(new PdfRectangle(new XRect(0, 0, 10, 10)), "attachment.txt");

        AssertsPdfARequiresOf(link);
    }

    [Fact]
    public void ANamedLinkPrintsAndIsNeitherHiddenNorInvisibleNorKeptOutOfView()
    {
        var link = PdfLinkAnnotation.CreateNamedLink(new PdfRectangle(new XRect(0, 0, 10, 10)), "chapter-3");

        AssertsPdfARequiresOf(link);
    }

    /// <summary>
    ///   ISO 19005-2/3 clause 6.3.2-1 and ISO 19005-1 clause 6.5.3-2: <c>F != null &amp;&amp;
    ///   (F &amp; Print) == Print &amp;&amp; (F &amp; Hidden) == 0 &amp;&amp; (F &amp; Invisible) == 0
    ///   &amp;&amp; (F &amp; NoView) == 0</c>.
    /// </summary>
    private static void AssertsPdfARequiresOf(PdfLinkAnnotation link)
    {
        link.Flags.Should().HaveFlag(PdfAnnotationFlags.Print);
        link.Flags.Should().NotHaveFlag(PdfAnnotationFlags.Hidden);
        link.Flags.Should().NotHaveFlag(PdfAnnotationFlags.Invisible);
        link.Flags.Should().NotHaveFlag(PdfAnnotationFlags.NoView);
    }
}
