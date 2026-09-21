using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf;

/// <summary>
/// A page placed in or taken out of a document's page tree - the argument of
/// <see cref="PdfDocument.PageAdded"/> and <see cref="PdfDocument.PageRemoved"/>.
/// </summary>
public sealed class PdfPageEventArgs : EventArgs
{
    internal PdfPageEventArgs(PdfPage page, int index)
    {
        Page = page;
        Index = index;
    }

    /// <summary>
    /// The page. For an imported page, the copy this document holds rather than the page it was
    /// imported from.
    /// </summary>
    public PdfPage Page { get; }

    /// <summary>
    /// Where the page was placed, counted from zero - or, for a page removed, where it was.
    /// </summary>
    /// <remarks>
    /// A handler that inserts pages ahead of this one moves it; <see cref="PdfPages.IndexOf"/>
    /// says where it is after that.
    /// </remarks>
    public int Index { get; }
}

/// <summary>
/// A drawing surface just made for a page - the argument of
/// <see cref="PdfDocument.PageGraphicsCreated"/>.
/// </summary>
public sealed class PdfPageGraphicsEventArgs : EventArgs
{
    internal PdfPageGraphicsEventArgs(PdfPage page, XGraphics graphics)
    {
        Page = page;
        Graphics = graphics;
    }

    /// <summary>
    /// The page being drawn on.
    /// </summary>
    public PdfPage Page { get; }

    /// <summary>
    /// The surface, which the handler may draw on before whoever asked for it does. It is not the
    /// handler's to dispose.
    /// </summary>
    public XGraphics Graphics { get; }
}
