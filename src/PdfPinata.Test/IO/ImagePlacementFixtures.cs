using System.Collections.Generic;

namespace PdfPinata.Test.IO;

/// <summary>
///   Pages drawing images the ways a writer of PDF draws them, including the way that stores an
///   image upside down and turns it back over as it is drawn.
///   See https://github.com/ststeiger/PdfSharpCore/issues/448.
/// </summary>
internal static class ImagePlacementFixtures
{
    /// <summary>
    ///   One page drawing one image under the matrix given.
    /// </summary>
    internal static byte[] PageDrawingAnImage(string matrix)
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R>>",
            "q " + matrix + " cm /Im0 Do Q",
            Image());
    }

    /// <summary>
    ///   One page drawing an image inside a form, each under a matrix of its own. What the page
    ///   shows is the two together, so a form storing an image upside down turns over an image
    ///   drawn upright within it.
    /// </summary>
    internal static byte[] PageDrawingAnImageInsideAForm(string formMatrix, string imageMatrix)
    {
        return OnePageDocument(
            "/XObject<</Fm0 5 0 R>>",
            "q 1 0 0 1 0 0 cm /Fm0 Do Q",
            Form("/Matrix[" + formMatrix + "]/Resources<</XObject<</Im0 6 0 R>>>>",
                "q " + imageMatrix + " cm /Im0 Do Q"),
            Image());
    }

    /// <summary>
    ///   One page drawing an image after restoring the state a flipping matrix was set in, so
    ///   that the flip is no part of what the image is drawn under.
    /// </summary>
    internal static byte[] PageRestoringTheStateBeforeDrawing()
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R>>",
            "q 100 0 0 -100 10 110 cm Q q 100 0 0 100 10 10 cm /Im0 Do Q",
            Image());
    }

    /// <summary>
    ///   One page whose content is written as three streams with a token of the drawing broken
    ///   across each break.
    /// </summary>
    internal static byte[] PageWhoseContentIsSplitAcrossStreams()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<</XObject<</Im0 7 0 R>>>>/Contents[4 0 R 5 0 R 6 0 R]"),
            RawPdf.Stream("", "q 100 0 0 -100"),
            RawPdf.Stream("", "10 110 cm"),
            RawPdf.Stream("", "/Im0 Do Q"),
            Image()
        });
    }

    /// <summary>
    ///   One page whose form draws itself, under a matrix that would flip the image once more
    ///   every time round.
    /// </summary>
    internal static byte[] PageWithAFormDrawingItself()
    {
        return OnePageDocument(
            "/XObject<</Fm0 5 0 R>>",
            "/Fm0 Do",
            Form("/Matrix[1 0 0 -1 0 0]/Resources<</XObject<</Fm0 5 0 R/Im0 6 0 R>>>>",
                "q 100 0 0 100 10 10 cm /Im0 Do Q /Fm0 Do"),
            Image());
    }

    /// <summary>
    ///   One page drawing an image after an inline image, which cannot be read over reliably.
    /// </summary>
    internal static byte[] PageWithAnInlineImage()
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R>>",
            "BI /W 4 /H 4 /CS /G /BPC 8 ID xxxxxxxxxxxxxxxx EI q 100 0 0 100 10 10 cm /Im0 Do Q",
            Image());
    }

    /// <summary>
    ///   One page whose content is filtered in a way that cannot be undone.
    /// </summary>
    internal static byte[] PageWhoseContentCannotBeRead()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<</XObject<</Im0 5 0 R>>>>/Contents 4 0 R"),
            RawPdf.Stream("/Filter/JPXDecode", "not a JPEG 2000 codestream"),
            Image()
        });
    }

    /// <summary>
    ///   One page drawing the same image twice, once each way up.
    /// </summary>
    internal static byte[] PageDrawingOneImageTwice()
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R>>",
            "q 100 0 0 100 10 10 cm /Im0 Do Q q 100 0 0 -100 10 150 cm /Im0 Do Q",
            Image());
    }

    /// <summary>
    ///   One page whose content gives an operator the wrong number of operands, as documents
    ///   written by real software do, before drawing an image.
    /// </summary>
    internal static byte[] PageWithATruncatedOperator()
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R>>",
            "0 0 RG q 100 0 0 -100 10 110 cm /Im0 Do Q",
            Image());
    }

    /// <summary>
    ///   A single page document whose page names the resources given and draws the content
    ///   given. The objects that follow the page are numbered from five.
    /// </summary>
    private static byte[] OnePageDocument(string resources, string content, params string[] rest)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<<" + resources + ">>/Contents 4 0 R"),
            RawPdf.Stream("", content)
        };
        objects.AddRange(rest);

        return RawPdf.Build(objects);
    }

    private static string Page(string entries)
    {
        return "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + ">>";
    }

    private static string Form(string entries, string content)
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" + entries, content);
    }

    private static string Image()
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Image/Width 40/Height 30" +
                             "/ColorSpace/DeviceGray/BitsPerComponent 8",
            new string('A', 1200));
    }
}
