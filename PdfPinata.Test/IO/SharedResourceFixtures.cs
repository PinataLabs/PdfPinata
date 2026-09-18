using System.Collections.Generic;

namespace PdfPinata.Test.IO;

/// <summary>
///   Documents whose pages carry more resources than they draw with, which is what a document
///   written by a report generator looks like and what PdfPinata never writes itself.
/// </summary>
internal static class SharedResourceFixtures
{
    /// <summary>The length of the stream of each image, in bytes.</summary>
    internal const int ImageLength = 20000;

    /// <summary>
    ///   Three pages inheriting one resource dictionary that names all three images, each page
    ///   drawing one of them. The dictionary is object 3, shared by every page.
    /// </summary>
    internal static byte[] PagesSharingOneResourceDictionary()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[4 0 R 5 0 R 6 0 R]/Count 3/Resources 3 0 R>>",
            "<</XObject<</Im0 7 0 R/Im1 8 0 R/Im2 9 0 R>>>>",
            Page("/Contents 10 0 R"),
            Page("/Contents 11 0 R"),
            Page("/Contents 12 0 R"),
            Image(),
            Image(),
            Image(),
            Draw("/Im0 Do"),
            Draw("/Im1 Do"),
            Draw("/Im2 Do"),
        });
    }

    /// <summary>
    ///   One page naming two fonts and two images, drawing nothing itself but a form. The form
    ///   has no resources of its own, so what it draws it draws with the page's: the font F1 and
    ///   the image Im1. F2 and Im2 are named by the page and drawn by nobody.
    /// </summary>
    internal static byte[] PageDrawingThroughAFormWithoutResources()
    {
        return OnePageDocument(
            "/Font<</F1 5 0 R/F2 6 0 R>>/XObject<</Fm0 7 0 R/Im1 8 0 R/Im2 9 0 R>>",
            "/Fm0 Do",
            Font(),
            Font(),
            Form("", "BT /F1 12 Tf (hi) Tj ET /Im1 Do"),
            Image(),
            Image());
    }

    /// <summary>
    ///   One page whose form draws with resources of its own, so that nothing the form names
    ///   keeps the page's entry of the same name alive.
    /// </summary>
    internal static byte[] PageDrawingThroughAFormWithItsOwnResources()
    {
        return OnePageDocument(
            "/XObject<</Fm0 5 0 R/Im1 6 0 R>>",
            "/Fm0 Do",
            Form("/Resources<</XObject<</Im1 7 0 R>>>>", "/Im1 Do"),
            Image(),
            Image());
    }

    /// <summary>One page whose form draws itself.</summary>
    internal static byte[] PageWithAFormDrawingItself()
    {
        return OnePageDocument(
            "/XObject<</Fm0 5 0 R/Im1 6 0 R>>",
            "/Fm0 Do",
            Form("", "/Fm0 Do"),
            Image());
    }

    /// <summary>
    ///   One page whose graphics state carries a soft mask. The mask is painted by a form with
    ///   no resources of its own, so what it draws it draws with the page's: the image Im1. Im2
    ///   is named by the page and drawn by nobody.
    /// </summary>
    internal static byte[] PageDrawingThroughASoftMaskWithoutResources()
    {
        return OnePageDocument(
            "/ExtGState<</GS0 5 0 R>>/XObject<</Im1 7 0 R/Im2 8 0 R>>",
            "/GS0 gs",
            "<</Type/ExtGState/SMask<</Type/Mask/S/Luminosity/G 6 0 R>>>>",
            Form("/Group<</S/Transparency/CS/DeviceGray>>", "/Im1 Do"),
            Image(),
            Image());
    }

    /// <summary>
    ///   One page whose soft mask is painted by a form carrying resources of its own.
    /// </summary>
    internal static byte[] PageDrawingThroughASoftMaskWithItsOwnResources()
    {
        return OnePageDocument(
            "/ExtGState<</GS0 5 0 R>>/XObject<</Im1 8 0 R>>",
            "/GS0 gs",
            "<</Type/ExtGState/SMask<</Type/Mask/S/Luminosity/G 6 0 R>>>>",
            Form("/Resources<</XObject<</Im1 7 0 R>>>>", "/Im1 Do"),
            Image(),
            Image());
    }

    /// <summary>
    ///   One page whose graphics state sets its soft mask to /None, which paints nothing.
    /// </summary>
    internal static byte[] PageTurningTheSoftMaskOff()
    {
        return OnePageDocument(
            "/ExtGState<</GS0 5 0 R>>/XObject<</Im1 6 0 R>>",
            "/GS0 gs",
            "<</Type/ExtGState/SMask/None>>",
            Image());
    }

    /// <summary>
    ///   One page whose soft mask names a form whose content cannot be read.
    /// </summary>
    internal static byte[] PageWhoseSoftMaskCannotBeRead()
    {
        return OnePageDocument(
            "/ExtGState<</GS0 5 0 R>>/XObject<</Im1 7 0 R/Im2 8 0 R>>",
            "/GS0 gs",
            "<</Type/ExtGState/SMask<</Type/Mask/S/Luminosity/G 6 0 R>>>>",
            RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]/Filter/JPXDecode",
                "not a JPEG 2000 codestream"),
            Image(),
            Image());
    }

    /// <summary>One page holding an inline image, which cannot be read over reliably.</summary>
    internal static byte[] PageWithAnInlineImage()
    {
        return OnePageDocument(
            "/XObject<</Im0 5 0 R/Im1 6 0 R>>",
            "BI /W 4 /H 4 /CS /G /BPC 8 ID xxxxxxxxxxxxxxxx EI /Im0 Do",
            Image(),
            Image());
    }

    /// <summary>
    ///   One page whose content is filtered in a way that cannot be undone, so that what it
    ///   draws cannot be told.
    /// </summary>
    internal static byte[] PageWhoseContentCannotBeRead()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<</XObject<</Im0 5 0 R/Im1 6 0 R>>>>/Contents 4 0 R"),
            RawPdf.Stream("/Filter/JPXDecode", "not a JPEG 2000 codestream"),
            Image(),
            Image(),
        });
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
            Draw(content),
        };
        objects.AddRange(rest);

        return RawPdf.Build(objects);
    }

    /// <summary>
    ///   One page naming two images and a Type 3 font, and drawing a character of that font. A
    ///   Type 3 glyph is a content stream of its own, held in the font's /CharProcs, so Im1 is
    ///   drawn only by the glyph and Im2 by nobody at all.
    /// </summary>
    internal static byte[] PageDrawingAGlyphThatDrawsAnImage()
    {
        return OnePageDocument(
            "/Font<</T3 5 0 R>>/XObject<</Im1 8 0 R/Im2 9 0 R>>",
            "BT /T3 12 Tf (a) Tj ET",
            "<</Type/Font/Subtype/Type3/FontBBox[0 0 200 200]/FontMatrix[0.001 0 0 0.001 0 0]"
            + "/CharProcs 6 0 R/Encoding<</Differences[97/a]>>/FirstChar 97/LastChar 97"
            + "/Widths[1000]>>",
            "<</a 7 0 R>>",
            RawPdf.Stream("", "1000 0 0 0 200 200 d1 /Im1 Do"),
            Image(),
            Image());
    }

    /// <summary>
    ///   One page whose annotation has a normal appearance stream drawing Im1, with Im2 named by
    ///   the page and drawn by nobody. The page's own content draws nothing.
    /// </summary>
    internal static byte[] PageWithAnAnnotationAppearance()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<</XObject<</Im1 6 0 R/Im2 7 0 R>>>>/Contents 4 0 R/Annots[5 0 R]"),
            Draw(""),
            "<</Type/Annot/Subtype/Widget/Rect[0 0 100 100]/AP<</N 8 0 R>>>>",
            Image(),
            Image(),
            // No resources of its own, so what it draws it draws with the page's.
            Form("", "/Im1 Do"),
        });
    }

    /// <summary>
    ///   The same, with an appearance that varies by state: /AP /N is a dictionary of one stream
    ///   per state rather than a stream, which is what a tickable field carries. Im1 is drawn by
    ///   the on state and Im2 by the off state, so both survive; Im3 is drawn by nobody.
    /// </summary>
    internal static byte[] PageWithAnAnnotationAppearancePerState()
    {
        return RawPdf.Build(new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            Page("/Resources<</XObject<</Im1 6 0 R/Im2 7 0 R/Im3 8 0 R>>>>"
                 + "/Contents 4 0 R/Annots[5 0 R]"),
            Draw(""),
            "<</Type/Annot/Subtype/Widget/Rect[0 0 100 100]/AP<</N 9 0 R>>>>",
            Image(),
            Image(),
            Image(),
            "<</Yes 10 0 R/Off 11 0 R>>",
            Form("", "/Im1 Do"),
            Form("", "/Im2 Do"),
        });
    }

    private static string Page(string entries)
    {
        return "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + ">>";
    }

    private static string Form(string entries, string content)
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Form/BBox[0 0 200 200]" + entries, content);
    }

    private static string Font()
    {
        return "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>";
    }

    private static string Image()
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Image/Width 100/Height 100" +
                             "/ColorSpace/DeviceGray/BitsPerComponent 8",
            new string('A', ImageLength));
    }

    private static string Draw(string content)
    {
        return RawPdf.Stream("", "q 100 0 0 100 10 10 cm " + content + " Q");
    }
}
