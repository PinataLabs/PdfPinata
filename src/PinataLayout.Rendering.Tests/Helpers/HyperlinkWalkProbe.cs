using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PdfPinata.Pdf;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   Asks the paragraph renderer which hyperlink a given leaf sits inside, which is the one
///   question it answers by walking up the document object model rather than by measuring
///   anything.
/// </summary>
/// <remarks>
///   Reflection rather than <c>InternalsVisibleTo</c>, for the reason
///   <see cref="ParagraphIteratorProbe"/> gives. The walk is reachable through a rendered page for
///   every leaf a paragraph really holds - that is what the tests beside this one use - but not for
///   a leaf belonging to no paragraph at all, which is the case the walk's own termination has to
///   answer for and which nothing in a document can arrange.
/// </remarks>
internal static class HyperlinkWalkProbe
{
    private static readonly Assembly Rendering = typeof(PdfDocumentRenderer).Assembly;

    private static readonly Type RendererType = Rendering.GetType("PinataLayout.Rendering.ParagraphRenderer", throwOnError: true);

    private static readonly Type IteratorType = Rendering.GetType("PinataLayout.Rendering.ParagraphIterator", throwOnError: true);

    private const BindingFlags Internals = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    ///   The hyperlink the renderer finds around the first leaf of the given elements, or null
    ///   when it finds none.
    /// </summary>
    internal static Hyperlink Around(ParagraphElements elements)
    {
        using var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        var renderer = Activator.CreateInstance(RendererType, Internals, null,
            new object[] { gfx, new Document().AddSection().AddParagraph(), null }, null);

        var iterator = Activator.CreateInstance(IteratorType, Internals, null,
            new object[] { elements }, null);

        // ReSharper disable once PossibleNullReferenceException
        var leaf = IteratorType.GetMethod("GetFirstLeaf", Internals).Invoke(iterator, null);

        // ReSharper disable once PossibleNullReferenceException
        RendererType.GetField("currentLeaf", Internals).SetValue(renderer, leaf);

        try
        {
            // ReSharper disable once PossibleNullReferenceException
            return (Hyperlink)RendererType.GetMethod("GetHyperlink", Internals).Invoke(renderer, null);
        }
        catch (TargetInvocationException exception)
        {
            // What the walk throws is the whole point of the test that calls this, so it is thrown
            // rather than reported wrapped in the reflection that reached it.
            ExceptionDispatchInfo.Capture(exception.InnerException!).Throw();
            throw;
        }
    }
}
