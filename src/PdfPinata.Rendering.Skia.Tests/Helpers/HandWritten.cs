using System;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// A one-page document whose content is a hand-written content stream, written by this library.
/// </summary>
internal static class HandWritten
{
    /// <summary>A page <paramref name="width"/> by <paramref name="height"/> points drawing <paramref name="content"/>.</summary>
    /// <param name="content">The operators.</param>
    /// <param name="width">The width of the media box.</param>
    /// <param name="height">The height of the media box.</param>
    /// <param name="page">Anything else to set on the page, such as <c>/Rotate</c> or its resources.</param>
    public static PdfDocument Page(string content, double width = 200, double height = 150,
        Action<PdfDocument, PdfPage> page = null)
    {
        var document = new PdfDocument();
        var added = document.AddPage();
        added.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(width, height));
        added.Contents.AppendContent().CreateStream(Encoding.Latin1.GetBytes(content));
        page?.Invoke(document, added);
        return document;
    }

    /// <summary>A graphics state parameter dictionary named <paramref name="name"/> in the page's resources.</summary>
    public static void ExtGState(PdfDocument document, PdfPage page, string name, Action<PdfDictionary> entries)
    {
        var state = new PdfDictionary(document);
        state.Elements.SetName("/Type", "/ExtGState");
        entries(state);
        Resource(document, page, "/ExtGState", name, state);
    }

    /// <summary>
    /// An image XObject named <paramref name="name"/> in the page's resources, holding
    /// <paramref name="data"/> as it is: its <c>/Filter</c> is among the entries.
    /// </summary>
    public static void Image(PdfDocument document, PdfPage page, string name, byte[] data, Action<PdfDictionary> entries)
    {
        var image = new PdfDictionary(document);
        image.Elements.SetName("/Type", "/XObject");
        image.Elements.SetName("/Subtype", "/Image");
        image.CreateStream(data);
        entries(image);
        Resource(document, page, "/XObject", name, image);
    }

    private static void Resource(PdfDocument document, PdfPage page, string category, string name, PdfDictionary value)
    {
        document.Internals.AddObject(value);

        var resources = page.Elements.GetDictionary("/Resources");
        if (resources == null)
        {
            resources = new PdfDictionary(document);
            page.Elements["/Resources"] = resources;
        }

        var named = resources.Elements.GetDictionary(category);
        if (named == null)
            resources.Elements[category] = named = new PdfDictionary(document);
        named.Elements[name] = value.Reference;
    }
}
