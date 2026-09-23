using System;
using System.Collections.Generic;
using System.Reflection;
using PinataLayout.DocumentObjectModel;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   Reaches PinataLayout's paragraph iterator, which is internal to the rendering assembly, so that
///   its traversal can be tested directly rather than inferred from a rendered page.
/// </summary>
/// <remarks>
///   Reflection rather than <c>InternalsVisibleTo</c>: this repository does not use one, and
///   <c>CLAUDE.md</c> records that the netstandard polyfills are duplicated across two assemblies
///   precisely because there is none. <c>PdfPinata.Test</c>'s AreaProbe reaches the areas the
///   same way and for the same reason. The shipped assembly is left alone and the awkwardness is
///   kept here, where it is one file and the tests above it read as though it were not there.
/// </remarks>
internal static class ParagraphIteratorProbe
{
    private static readonly Type IteratorType = typeof(PdfDocumentRenderer).Assembly
        .GetType("PinataLayout.Rendering.ParagraphIterator", throwOnError: true);

    private const BindingFlags Internals = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    ///   The leaves of the paragraph from the first to the last, as the renderer walks them when
    ///   it lays a line out.
    /// </summary>
    internal static IReadOnlyList<DocumentObject> Leaves(Paragraph paragraph)
    {
        return Walk(paragraph, "GetFirstLeaf", "GetNextLeaf");
    }

    /// <summary>
    ///   The leaves of the paragraph from the last back to the first, which is how the renderer
    ///   retreats when a line does not fit and has to be broken further back.
    /// </summary>
    internal static IReadOnlyList<DocumentObject> LeavesInReverse(Paragraph paragraph)
    {
        return Walk(paragraph, "GetLastLeaf", "GetPreviousLeaf");
    }

    /// <summary>Whether the paragraph has any leaf to start from at all.</summary>
    internal static bool HasLeaves(Paragraph paragraph)
    {
        return Call(New(paragraph), "GetFirstLeaf") != null;
    }

    /// <summary>
    ///   Whether the iterator standing on that leaf says it is the first, and the last, of the
    ///   paragraph - the two questions the renderer asks to decide whether a line may be broken
    ///   before or after what it is looking at.
    /// </summary>
    internal static (bool IsFirst, bool IsLast) EndsAt(Paragraph paragraph, int leafIndex)
    {
        var iterator = Call(New(paragraph), "GetFirstLeaf");
        for (var idx = 0; idx < leafIndex; idx++)
            iterator = Call(iterator, "GetNextLeaf");

        return ((bool)Read(iterator, "IsFirstLeaf"), (bool)Read(iterator, "IsLastLeaf"));
    }

    /// <summary>
    ///   A leaf as an assertion can read it: the kind of element, and for a run of text the text
    ///   itself. "Text:once" for a Text carrying "once", "Character" for a symbol or a blank.
    /// </summary>
    internal static string Describe(DocumentObject leaf)
    {
        return leaf is Text text
            ? "Text:" + text.Content
            : leaf.GetType().Name;
    }

    private static IReadOnlyList<DocumentObject> Walk(Paragraph paragraph, string seek, string step)
    {
        var leaves = new List<DocumentObject>();

        var iterator = Call(New(paragraph), seek);
        while (iterator != null)
        {
            leaves.Add((DocumentObject)Read(iterator, "Current"));
            iterator = Call(iterator, step);
        }

        return leaves;
    }

    private static object New(Paragraph paragraph)
    {
        return Activator.CreateInstance(IteratorType, Internals, null,
            new object[] { paragraph.Elements }, null);
    }

    private static object Call(object iterator, string methodName)
    {
        return iterator == null
            ? null
            // ReSharper disable once PossibleNullReferenceException
            : IteratorType.GetMethod(methodName, Internals).Invoke(iterator, null);
    }

    private static object Read(object iterator, string propertyName)
    {
        // ReSharper disable once PossibleNullReferenceException
        return IteratorType.GetProperty(propertyName, Internals).GetValue(iterator);
    }
}
