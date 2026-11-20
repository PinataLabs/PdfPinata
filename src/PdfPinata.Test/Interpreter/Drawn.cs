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
    private Drawn(PageRenderResult result, RecordingDevice device)
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

    /// <summary>
    /// Draws a page that has been saved and read back, so that its resources may be written as
    /// PDF text with <see cref="Object"/>: what is drawn is what a reader meets in a file.
    /// </summary>
    /// <param name="content">The operators.</param>
    /// <param name="resources">Fills in the page's resources.</param>
    /// <param name="drawsShaders">Whether the device draws shadings and patterns, or answers that it cannot.</param>
    /// <param name="glyphSources">Asked for each font's glyphs, as <see cref="PageRendering.Render"/> takes it.</param>
    public static Drawn Saved(string content, Action<PdfDocument, PdfDictionary> resources = null,
        bool drawsShaders = true, Func<PdfDictionary, PageGlyphSource> glyphSources = null)
    {
        var page = Helpers.Saved.Reopened(PageOf(content, resources).Owner).Pages[0];
        var device = new RecordingDevice { DrawsShaders = drawsShaders };
        return new Drawn(PageRendering.Render(page, device, glyphSources: glyphSources), device);
    }

    /// <summary>
    /// An object of the document whose entries are written as PDF text, such as
    /// <c>("/Coords", "[0 0 100 0]")</c>, or are other objects, which are referred to. For a
    /// page drawn by <see cref="Saved"/>.
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
    public static PdfDictionary Stream(PdfDocument document, byte[] content, params (string Key, object Value)[] entries)
    {
        var dictionary = Object(document, entries);
        dictionary.CreateStream(content);
        return dictionary;
    }

    public static PdfDictionary Stream(PdfDocument document, string content, params (string Key, object Value)[] entries) =>
        Stream(document, Encoding.Latin1.GetBytes(content), entries);

    /// <summary>Adds PDF text to a category of the resources under a name: a colour space written as an array.</summary>
    public static void AddText(PdfDocument document, PdfDictionary resources, string category, string name, string text)
    {
        var entries = resources.Elements.GetDictionary(category);
        if (entries == null)
            resources.Elements[category] = entries = new PdfDictionary(document);
        entries.Elements[name] = new PdfLiteral(text);
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
