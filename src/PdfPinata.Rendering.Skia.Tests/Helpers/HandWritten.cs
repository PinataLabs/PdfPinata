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

        Resource(document, page, "/ExtGState", name, state);
    }

    /// <summary>
    /// An object of the document whose entries are written as PDF text, such as
    /// <c>("/Coords", "[0 0 100 0]")</c>, or are other objects, which are referred to.
    /// </summary>
    public static PdfDictionary Object(PdfDocument document, params (string Key, object Value)[] entries)
    {
        var dictionary = new PdfDictionary(document);
        foreach (var (key, value) in entries)
            dictionary.Elements[key] = value is PdfDictionary other ? other.Reference : new PdfLiteral((string)value);
        document.Internals.AddObject(dictionary);
        return dictionary;
    }

    /// <summary>As <see cref="Object"/>, with a stream: a pattern's cell, or a function's samples or program.</summary>
    public static PdfDictionary Stream(PdfDocument document, string content, params (string Key, object Value)[] entries)
    {
        var dictionary = Object(document, entries);
        dictionary.CreateStream(Encoding.Latin1.GetBytes(content));
        return dictionary;
    }

    /// <summary>
    /// Puts something in a category of the page's resources, such as <c>/Shading</c>, under a name:
    /// an object of the document, which is referred to, or anything else as it is.
    /// </summary>
    public static void Resource(PdfDocument document, PdfPage page, string category, string name, PdfItem value)
    {
        var resources = page.Elements.GetDictionary("/Resources");
        if (resources == null)
        {
            resources = new PdfDictionary(document);
            page.Elements["/Resources"] = resources;
        }

        var entries = resources.Elements.GetDictionary(category);
        if (entries == null)
            resources.Elements[category] = entries = new PdfDictionary(document);
        entries.Elements[name] = value is PdfObject { Reference: not null } indirect ? indirect.Reference : value;
    }
}
