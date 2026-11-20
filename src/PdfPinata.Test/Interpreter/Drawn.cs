using System;
using System.Linq;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// A hand-written content stream, drawn onto a <see cref="RecordingDevice"/>.
/// </summary>
/// <remarks>
/// The page is 200 points wide and 100 high and is drawn at 72 to the inch, so a point of user
/// space (x, y) lands on the device at (x, 100 - y).
/// </remarks>
internal sealed class Drawn
{
    internal Drawn(PageRenderResult result, RecordingDevice device)
    {
        Result = result;
        Device = device;
    }

    public PageRenderResult Result { get; }

    public RecordingDevice Device { get; }

    /// <summary>The problems reported, as <c>Problem subject</c>.</summary>
    public string[] Problems => Result.Diagnostics.Select(d => d.Problem + " " + d.Subject).ToArray();

    /// <summary>Draws a page whose content is <paramref name="content"/>.</summary>
    /// <param name="content">The operators.</param>
    /// <param name="resources">Fills in the page's resources.</param>
    /// <param name="glyphSources">Asked for each font's glyphs, as <see cref="PageRendering.Render"/> takes it.</param>
    public static Drawn Page(string content, Action<PdfDocument, PdfDictionary> resources = null,
        Func<PdfDictionary, PageGlyphSource> glyphSources = null) =>
        Page(Encoding.Latin1.GetBytes(content), resources, glyphSources);

    public static Drawn Page(byte[] content, Action<PdfDocument, PdfDictionary> resources = null,
        Func<PdfDictionary, PageGlyphSource> glyphSources = null)
    {
        var page = PageOf(content, resources);
        var device = new RecordingDevice();
        return new Drawn(PageRendering.Render(page, device, glyphSources: glyphSources), device);
    }

    /// <summary>A page of a new document whose content is <paramref name="content"/>.</summary>
    public static PdfPage PageOf(byte[] content, Action<PdfDocument, PdfDictionary> resources = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(200, 100));
        page.Contents.AppendContent().CreateStream(content);

        var dictionary = new PdfDictionary(document);
        page.Elements["/Resources"] = dictionary;
        resources?.Invoke(document, dictionary);
        return page;
    }

    public static PdfPage PageOf(string content, Action<PdfDocument, PdfDictionary> resources = null) =>
        PageOf(Encoding.Latin1.GetBytes(content), resources);

    /// <summary>Adds an object to a category of the resources, such as <c>/Font</c>, under a name.</summary>
    public static void Add(PdfDocument document, PdfDictionary resources, string category, string name, PdfObject value)
    {
        var entries = resources.Elements.GetDictionary(category);
        if (entries == null)
            resources.Elements[category] = entries = new PdfDictionary(document);

        if (value.Reference == null)
            document.Internals.AddObject(value);
        entries.Elements[name] = value.Reference;
    }

    /// <summary>A form XObject drawing <paramref name="content"/> in a box of 100 by 100.</summary>
    public static PdfDictionary Form(PdfDocument document, string content)
    {
        var form = new PdfDictionary(document);
        document.Internals.AddObject(form);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = new PdfRectangle(new XRect(0, 0, 100, 100));
        form.CreateStream(Encoding.Latin1.GetBytes(content));
        return form;
    }
}
