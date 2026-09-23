using System;
using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// An area provider that can set room aside at the foot of the page for footnotes.
/// </summary>
/// <remarks>
/// Only <see cref="FormattedDocument"/> implements it, and that is the whole of the answer to
/// "what happens to a footnote in a table cell". A cell, a text frame and a header each format
/// through an area provider of their own, none of which owns a page, so none of them can put
/// anything at the foot of one. <see cref="TopDownFormatter"/> refuses the note there rather than
/// dropping it, which is the behaviour this work exists to remove.
/// </remarks>
internal interface IFootnoteAreaProvider
{
    /// <summary>
    /// Lays out the footnotes the element carries, records the page they belong to, and returns how
    /// much more of the page has to be set aside for them than was already.
    /// </summary>
    XUnit ReserveFootnotes(DocumentObject element, XUnit width, XGraphics gfx);
}

/// <summary>
/// Finding the footnotes an element carries.
/// </summary>
internal static class Footnotes
{
    /// <summary>
    /// The notes attached to this element, in reading order, or an empty list.
    /// </summary>
    /// <remarks>
    /// Only a paragraph can carry one - <c>Footnote</c> is a paragraph element - and the descent
    /// goes through <c>FormattedText</c> and <c>Hyperlink</c> and nothing else, which is exactly
    /// what <see cref="ParagraphIterator"/> descends into. A note's own content is *not* walked:
    /// it is block content laid out separately, and walking it here would put a note's text into
    /// the running text it belongs beneath.
    /// </remarks>
    internal static IReadOnlyList<Footnote> In(DocumentObject element)
    {
        if (!(element is Paragraph paragraph) || paragraph.IsNull("Elements"))
            return Empty;

        List<Footnote> found = null;
        Collect(paragraph.Elements, ref found);
        return (IReadOnlyList<Footnote>)found ?? Empty;
    }

    private static void Collect(ParagraphElements elements, ref List<Footnote> found)
    {
        foreach (DocumentObject element in elements)
        {
            switch (element)
            {
                case Footnote footnote:
                    found ??= [];
                    found.Add(footnote);
                    break;

                case FormattedText formatted when !formatted.IsNull("Elements"):
                    Collect(formatted.Elements, ref found);
                    break;

                case Hyperlink hyperlink when !hyperlink.IsNull("Elements"):
                    Collect(hyperlink.Elements, ref found);
                    break;
            }
        }
    }

    private static readonly IReadOnlyList<Footnote> Empty = [];
}
