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
        document.Internals.AddObject(state);

        var resources = page.Elements.GetDictionary("/Resources");
        if (resources == null)
        {
            resources = new PdfDictionary(document);
            page.Elements["/Resources"] = resources;
        }

        var states = resources.Elements.GetDictionary("/ExtGState");
        if (states == null)
            resources.Elements["/ExtGState"] = states = new PdfDictionary(document);
        states.Elements[name] = state.Reference;
    }
}
