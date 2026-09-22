#region Copyright
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Changes the size of a page that already has content on it, moving the content, the annotations
/// and the destinations that point at the page along with it.
/// <para>
/// The content is not rewritten. It is moved whole into a form XObject and the page is given a
/// single content stream that draws that form under a transform. Doing it that way means the
/// content never has to be understood, and - the reason it is done that way rather than by
/// putting a cm in front of what is already there - a form XObject is a graphics state of its
/// own. Content whose q and Q do not balance, which real documents have, cannot leak out of it
/// and take the transform with it.
/// </para>
/// </summary>
static class PdfPageResizer
{
    /// <summary>
    /// Resizes one page, and fixes up the destinations that point at it unless told not to.
    /// </summary>
    internal static void Resize(PdfPage page, XSize visibleTarget, PageSize size, PageResizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(page);

        var document = page.Owner
                       ?? throw new InvalidOperationException(
                           "The page belongs to no document, so there is nothing to resize it within.");

        options = (options ?? PageResizeOptions.Default).Clone();
        RefuseWhatCannotBeResized(document);

        var matrix = ResizeOnePage(page, visibleTarget, size, options);

        if (options.ScaleDestinations)
        {
            PdfDestinationScaler.Scale(document,
                new Dictionary<PdfPage, XMatrix> { [page] = matrix });
        }
    }

    /// <summary>
    /// Resizes every page of a document, sweeping the destinations once at the end rather than
    /// once for every page.
    /// </summary>
    internal static void ResizeAll(PdfDocument document, XSize visibleTarget, PageSize size,
        PageResizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);

        options = (options ?? PageResizeOptions.Default).Clone();

        // Once, before a single page is touched: a document that is going to be refused must not
        // be left with half its pages resized.
        RefuseWhatCannotBeResized(document);

        var matrices = new Dictionary<PdfPage, XMatrix>();
        foreach (var page in document.Pages)
            matrices[page] = ResizeOnePage(page, visibleTarget, size, options);

        if (options.ScaleDestinations)
            PdfDestinationScaler.Scale(document, matrices);
    }

    /// <summary>
    /// Everything a resize does to one page: the content, the boxes and the annotations. The
    /// destinations are not here, because they are held all over the document and are swept once
    /// however many pages are being resized.
    /// </summary>
    static XMatrix ResizeOnePage(PdfPage page, XSize visibleTarget, PageSize size, PageResizeOptions options)
    {
        if (visibleTarget.Width <= 0 || visibleTarget.Height <= 0)
            throw new ArgumentException("A page cannot be resized to nothing.", nameof(visibleTarget));

        if (page.RenderContent != null)
        {
            throw new InvalidOperationException(
                "An XGraphics object is open on this page. Dispose of it before resizing the page: " +
                "it holds the size the page had when it was created and would go on drawing to that.");
        }

        // A page turned by a quarter is stored across the way the reader sees it, so the box that
        // has to be written is the target with its sides swapped. Everything from here on is in
        // the coordinates the content is really drawn in, which is what the viewer then turns.
        var storedTarget = page.IsTurnedByAQuarter
            ? new XSize(visibleTarget.Height, visibleTarget.Width)
            : visibleTarget;

        var target = new XRect(0, 0, storedTarget.Width, storedTarget.Height);

        // The transform written in front of the content, and the transform that carries the page
        // as it stands now onto the page as it will be. They are the same thing the first time a
        // page is resized, and they part company the second time: the content is then held in a
        // wrapper in its original coordinates, so what goes in front of it is worked out afresh
        // from those - while the boxes, the annotations and the destinations are all in the
        // coordinates of the page as it is now, and move by the difference between the two.
        XMatrix contentMatrix;
        XMatrix delta;

        if (TryFindWrapper(page, out var name, out var wrapped, out var already))
        {
            contentMatrix = PageFit.Calculate(wrapped, target, options);

            var undo = already;
            undo.Invert();
            delta = undo * contentMatrix;

            page.ReplaceContents(ContentDrawing(page.Owner, name, contentMatrix));
        }
        else
        {
            var source = SourceRectOf(page);
            contentMatrix = PageFit.Calculate(source, target, options);
            delta = contentMatrix;

            WrapContent(page, source, contentMatrix);
        }

        SetBoxes(page, target, delta, size);

        if (options.ScaleAnnotations)
            PdfAnnotationTransformer.Transform(page, delta);

        return delta;
    }

    /// <summary>
    /// The rectangle the content of a page occupies, in the coordinates it is drawn in.
    /// <para>
    /// The crop box where the page has one, because that is the part of the page a reader is
    /// shown and therefore the part that "the page" means; the media box otherwise. Either may
    /// sit away from the origin, and real documents have such pages.
    /// </para>
    /// </summary>
    internal static XRect SourceRectOf(PdfPage page)
    {
        // Null for a box the page does not state, where page.CropBox answers the empty rectangle.
        var media = RectangleOf(page, PdfPage.InheritablePageKeys.MediaBox);
        var crop = RectangleOf(page, PdfPage.InheritablePageKeys.CropBox);
        var box = crop ?? media;

        if (box == null || box.Width <= 0 || box.Height <= 0)
        {
            throw new InvalidOperationException(
                "The page has no usable media box, so there is no telling what size it is now.");
        }

        var rect = Normalized(box);

        // A crop box is not allowed to reach outside the media box; where one does, a reader
        // takes the part of it that lies within, and so does this. Without that a page whose
        // crop box is larger than its media box would be resized from a rectangle bigger than
        // the page, and everything would come out too small.
        if (crop != null && media != null && media.Width > 0 && media.Height > 0)
        {
            rect.Intersect(Normalized(media));

            if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException(
                    "The crop box of this page lies outside its media box, so there is nothing of " +
                    "the page left to resize.");
            }
        }

        // A page that PdfPinata is holding as landscape keeps its media box the other way
        // round in memory and turns it over as it writes it, so the box in hand is not the box
        // the content is drawn in. StoredSize is, and this is the same turn applied to the rect.
        if (page.MediaBoxIsTurnedWhenWritten)
            rect = new XRect(rect.X, rect.Y, rect.Height, rect.Width);

        return rect;
    }

    /// <summary>
    /// The rectangle as an XRect whose X and Y are the corner where both coordinates are least,
    /// however the two corners were written down. A rectangle is allowed either way round.
    /// </summary>
    static XRect Normalized(PdfRectangle rect)
    {
        var x = Math.Min(rect.X1, rect.X2);
        var y = Math.Min(rect.Y1, rect.Y2);
        return new XRect(x, y, Math.Abs(rect.X2 - rect.X1), Math.Abs(rect.Y2 - rect.Y1));
    }

    /// <summary>
    /// Puts the content of the page behind a transform, by moving it into a form XObject and
    /// giving the page a content stream that draws that form.
    /// </summary>
    static void WrapContent(PdfPage page, XRect source, XMatrix matrix)
    {
        var form = new PdfFormXObject(page.Owner, page,
            new PdfRectangle(source.X, source.Y, source.X + source.Width, source.Y + source.Height));
        page.Owner._irefTable.Add(form);

        // A dictionary of its own holding nothing but the wrapper. Never the one the page had:
        // that one may be shared with other pages, and it has just been handed to the form.
        var resources = new PdfResources(page.Owner);
        page.Owner._irefTable.Add(resources);
        var name = resources.AddForm(form);

        page.ReplaceResources(resources);
        page.ReplaceContents(ContentDrawing(page.Owner, name, matrix));
    }

    /// <summary>
    /// A content stream that draws the named form under the transform given.
    /// </summary>
    static PdfContent ContentDrawing(PdfDocument document, string name, XMatrix matrix)
    {
        var builder = new StringBuilder();
        builder.Append("q ");
        AppendNumber(builder, matrix.M11);
        AppendNumber(builder, matrix.M12);
        AppendNumber(builder, matrix.M21);
        AppendNumber(builder, matrix.M22);
        AppendNumber(builder, matrix.OffsetX);
        AppendNumber(builder, matrix.OffsetY);
        builder.Append("cm ").Append(name).Append(" Do Q\n");

        var content = new PdfContent(document);
        content.CreateStream(PdfEncoders.RawEncoding.GetBytes(builder.ToString()));
        document._irefTable.Add(content);
        return content;
    }

    /// <summary>
    /// Writes a number the way a content stream wants it: no exponent, no thousands separator and
    /// a full stop for the decimal point whatever the machine is set to.
    /// </summary>
    static void AppendNumber(StringBuilder builder, double value)
    {
        builder.Append(value.ToString("0.########", CultureInfo.InvariantCulture)).Append(' ');
    }

    /// <summary>
    /// Whether the page is one this has resized before and can resize again by rewriting the
    /// transform, and if so what the wrapper is called, the rectangle the content occupied before
    /// any of it happened, and the transform currently in front of it.
    /// <para>
    /// Deliberately unforgiving. Anything at all that is not exactly the content this writes -
    /// a second stream, an operator that should not be there, a form without the marker - is
    /// answered no, and the caller wraps the page again. Wrapping a wrapper costs a few bytes;
    /// rewriting the transform of something that is not one loses the page.
    /// </para>
    /// </summary>
    static bool TryFindWrapper(PdfPage page, out string name, out XRect wrapped, out XMatrix already)
    {
        name = null;
        wrapped = default;
        already = XMatrix.Identity;

        var content = SingleContentStreamOf(page);
        if (content?.Stream == null)
            return false;

        // Measure before decoding. UnfilteredValue runs the filters and hands back a whole copy
        // of the content, and on an ordinary page that is megabytes of work to discover that the
        // page is not a wrapper. A wrapper is a couple of hundred bytes at the outside, so
        // anything larger cannot be one and need not be decoded to prove it. Stream.Value is the
        // bytes as stored, which costs nothing to measure.
        if (content.Stream.Value == null || content.Stream.Value.Length > LongestWrapper)
            return false;

        if (!TryReadWrapperName(content.Stream.UnfilteredValue, out name, out already))
            return false;

        // A transform that cannot be undone cannot be worked back through, so the page is
        // wrapped again rather than guessed at.
        if (!already.HasInverse)
            return false;

        // The name has to lead to a form this made. Read the resources through the element
        // rather than the property so that a page without any does not get given some.
        var resourcesItem = page.Elements[PdfPage.InheritablePageKeys.Resources];
        if (resourcesItem is PdfReference resourcesReference)
            resourcesItem = resourcesReference.Value;
        if (resourcesItem is not PdfDictionary resources)
            return false;

        var xObjectsItem = resources.Elements["/XObject"];
        if (xObjectsItem is PdfReference xObjectsReference)
            xObjectsItem = xObjectsReference.Value;
        if (xObjectsItem is not PdfDictionary xObjects)
            return false;

        var formItem = xObjects.Elements[name];
        if (formItem is PdfReference formReference)
            formItem = formReference.Value;
        if (formItem is not PdfDictionary form)
            return false;

        if (!form.Elements.GetBoolean(PdfFormXObject.ResizeWrapperKey))
            return false;

        // The rectangle the content occupied when it was first wrapped is the form's bounding
        // box. Working the new transform out against that rather than against the transform
        // already in force is what keeps a page from drifting as it is resized over and over.
        var box = RectangleOf(form, "/BBox");
        if (box == null || box.Width <= 0 || box.Height <= 0)
            return false;

        wrapped = Normalized(box);
        return true;
    }

    /// <summary>
    /// The rectangle held under the key of any dictionary, or null where it holds none.
    /// </summary>
    internal static PdfRectangle RectangleOf(PdfDictionary dictionary, string key)
    {
        var item = dictionary.Elements[key];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfRectangle rectangle)
            return rectangle;

        if (item is PdfArray array && array.Elements.Count == 4)
        {
            var corners = new double[4];
            for (var index = 0; index < 4; index++)
            {
                if (!TryNumber(array.Elements[index], out corners[index]))
                    return null;
            }

            return new PdfRectangle(corners[0], corners[1], corners[2], corners[3]);
        }

        return null;
    }

    /// <summary>
    /// The most a wrapper's content stream can run to, as stored.
    /// <para>
    /// What is written is "q" and six numbers and "cm" and a name and "Do" and "Q" - a couple of
    /// hundred bytes with the numbers at their longest and the name generously long, and a
    /// compressed copy of something that short is no smaller. The cap is well above that, and
    /// costs nothing to be wrong about in the safe direction: a wrapper mistaken for ordinary
    /// content is wrapped a second time, which is wasteful and correct.
    /// </para>
    /// </summary>
    const int LongestWrapper = 1024;

    /// <summary>
    /// The one content stream of the page, or null where it has none or has more than one.
    /// </summary>
    static PdfDictionary SingleContentStreamOf(PdfPage page)
    {
        var item = page.Elements[PdfPage.Keys.Contents];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfArray array)
        {
            if (array.Elements.Count != 1)
                return null;

            item = array.Elements[0];
            if (item is PdfReference elementReference)
                item = elementReference.Value;
        }

        return item as PdfDictionary;
    }

    /// <summary>
    /// Reads the name of the form and the transform in front of it out of a content stream,
    /// where the stream is exactly the "q ... cm /Name Do Q" this writes and nothing else.
    /// </summary>
    /// <summary>
    /// The white space a wrapper's tokens are separated by.
    /// </summary>
    static readonly char[] WrapperSeparators = [' ', '\t', '\r', '\n', '\f', '\0'];

    static bool TryReadWrapperName(byte[] content, out string name, out XMatrix matrix)
    {
        name = null;
        matrix = XMatrix.Identity;
        if (content == null)
            return false;

        var tokens = PdfEncoders.RawEncoding.GetString(content, 0, content.Length)
            .Split(WrapperSeparators, StringSplitOptions.RemoveEmptyEntries);

        // q, six numbers, cm, the name, Do, Q. Anything longer or shorter is somebody else's.
        if (tokens.Length != 11)
            return false;

        if (tokens[0] != "q" || tokens[7] != "cm" || tokens[9] != "Do" || tokens[10] != "Q")
            return false;

        var numbers = new double[6];
        for (var index = 0; index < 6; index++)
        {
            if (!double.TryParse(tokens[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture,
                    out numbers[index]))
                return false;
        }

        if (tokens[8].Length < 2 || tokens[8][0] != '/')
            return false;

        matrix = new XMatrix(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]);
        name = tokens[8];
        return true;
    }

    /// <summary>
    /// Gives the page its new media box and moves the other four boxes with the content.
    /// </summary>
    static void SetBoxes(PdfPage page, XRect target, XMatrix matrix, PageSize size)
    {
        // The crop box and the rest describe parts of the content, so they go where it goes.
        // Read the elements rather than the properties, so that a box the page does not state is
        // told apart from one it states as empty, and is left absent.
        foreach (var key in new[]
                 {
                     PdfPage.InheritablePageKeys.CropBox, PdfPage.Keys.BleedBox,
                     PdfPage.Keys.TrimBox, PdfPage.Keys.ArtBox
                 })
        {
            var box = RectangleOf(page, key);
            if (box == null)
                continue;

            var moved = Transformed(Normalized(box), matrix);

            // None of these is allowed to reach outside the media box. Fill and None both let
            // the content overflow the new page on purpose, and a box that travelled with it
            // would overflow too, leaving a page that is malformed even though most readers
            // would quietly clamp it for themselves.
            moved.Intersect(target);
            if (moved.IsEmpty || moved.Width <= 0 || moved.Height <= 0)
            {
                // Nothing of it is on the page any more. Leaving the old rectangle would be
                // worse than having none: it describes a region of a page that no longer exists.
                page.Elements.Remove(key);
                continue;
            }

            page.Elements.SetRectangle(key,
                new PdfRectangle(moved.X, moved.Y, moved.X + moved.Width, moved.Y + moved.Height));
        }

        page.ApplyResizedBox(new PdfRectangle(target.X, target.Y,
            target.X + target.Width, target.Y + target.Height), size);
    }

    /// <summary>
    /// Reads a number out of an item, following a reference to get at it.
    /// <para>
    /// Any object in a PDF is allowed to be indirect, a number in an array of coordinates
    /// included. <c>GetReal</c> does not follow the reference - it throws
    /// <see cref="InvalidCastException"/> on one - so reading coordinates with it turns a legal
    /// if unusual file into a failed resize. Everything that reads a coordinate goes through
    /// here instead, and answers false rather than throwing when the item is not a number at all.
    /// </para>
    /// </summary>
    internal static bool TryNumber(PdfItem item, out double value)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        switch (item)
        {
            case PdfReal real:
                value = real.Value;
                return true;

            case PdfRealObject realObject:
                value = realObject.Value;
                return true;

            case PdfInteger integer:
                value = integer.Value;
                return true;

            case PdfIntegerObject integerObject:
                value = integerObject.Value;
                return true;

            default:
                value = 0;
                return false;
        }
    }

    /// <summary>
    /// The smallest rectangle holding the four transformed corners of the one given. A quarter
    /// turn moves the corners about, so taking two of them would not do.
    /// </summary>
    internal static XRect Transformed(XRect rect, XMatrix matrix)
    {
        XPoint[] corners =
        [
            matrix.Transform(new XPoint(rect.X, rect.Y)),
            matrix.Transform(new XPoint(rect.X + rect.Width, rect.Y)),
            matrix.Transform(new XPoint(rect.X + rect.Width, rect.Y + rect.Height)),
            matrix.Transform(new XPoint(rect.X, rect.Y + rect.Height))
        ];

        double minX = corners[0].X, maxX = corners[0].X;
        double minY = corners[0].Y, maxY = corners[0].Y;
        for (var index = 1; index < corners.Length; index++)
        {
            minX = Math.Min(minX, corners[index].X);
            maxX = Math.Max(maxX, corners[index].X);
            minY = Math.Min(minY, corners[index].Y);
            maxY = Math.Max(maxY, corners[index].Y);
        }

        return new XRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Throws rather than resize a document where resizing would do damage that does not show.
    /// </summary>
    static void RefuseWhatCannotBeResized(PdfDocument document)
    {
        document.EnsureCanModify("resizing a page");

        // What is being refused here is a document that came *out of* an encrypted file, whose
        // streams were decrypted on the way in and have to be encrypted again on the way out.
        // A document built in memory with a password set on it is a different thing: nothing is
        // encrypted yet, the password is an instruction for the save, and resizing it is fine.
        //
        // Both look alike from the trailer, because setting a password reaches SecurityHandler,
        // whose getter resolves /Encrypt with VCF.CreateIndirect and so creates the entry there
        // and then. What tells them apart is where the document came from.
        //
        // Note neither DocumentSecurityLevel nor SecuritySettings.SecurityHandler can be asked
        // instead: the first is the caller's setting rather than a fact about the document, and
        // reading the second would create the very /Encrypt entry it is looking for.
        if (document.IsImported && document._trailer?.Elements[PdfTrailer.Keys.Encrypt] != null)
        {
            throw new InvalidOperationException(
                "This document is encrypted. Resizing a page rewrites its content stream, which " +
                "cannot be done without going back through the security handler.");
        }

        if (HasSignature(document))
        {
            throw new InvalidOperationException(
                "This document is signed. Resizing a page would leave the signature no longer " +
                "verifying against the document it signs.");
        }

        if (document.Catalog.Elements[PdfCatalog.Keys.StructTreeRoot] != null)
        {
            throw new InvalidOperationException(
                "This document is tagged. Resizing a page moves its content into a form XObject, " +
                "which breaks the structure tree's mapping onto that content - and breaks it " +
                "invisibly: the page goes on rendering perfectly while nothing describes it any " +
                "more. Remove the structure tree first if the tagging is not wanted.");
        }
    }

    /// <summary>
    /// Whether the document carries a digital signature, which is to say whether its interactive
    /// form holds a field of type /Sig.
    /// </summary>
    static bool HasSignature(PdfDocument document)
    {
        var formItem = document.Catalog.Elements[PdfCatalog.Keys.AcroForm];
        if (formItem is PdfReference formReference)
            formItem = formReference.Value;
        if (formItem is not PdfDictionary form)
            return false;

        if (form.Elements.GetInteger("/SigFlags") != 0)
            return true;

        var fieldsItem = form.Elements["/Fields"];
        if (fieldsItem is PdfReference fieldsReference)
            fieldsItem = fieldsReference.Value;
        if (fieldsItem is not PdfArray fields)
            return false;

        return HoldsSignatureField(fields, 0);
    }

    /// <summary>
    /// Whether any field of the array, or any field below one, is a signature. Fields nest, so
    /// this has to go down as well as along - with a cap on how far, against a file that points
    /// a field at itself.
    /// </summary>
    static bool HoldsSignatureField(PdfArray fields, int depth)
    {
        if (depth > 16)
            return false;

        foreach (var item in fields.Elements)
        {
            var fieldItem = item;
            if (fieldItem is PdfReference reference)
                fieldItem = reference.Value;
            if (fieldItem is not PdfDictionary field)
                continue;

            if (field.Elements.GetName("/FT") == "/Sig")
                return true;

            var kidsItem = field.Elements["/Kids"];
            if (kidsItem is PdfReference kidsReference)
                kidsItem = kidsReference.Value;
            if (kidsItem is PdfArray kids && HoldsSignatureField(kids, depth + 1))
                return true;
        }

        return false;
    }
}
