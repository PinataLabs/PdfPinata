#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Diagnostics;
using PdfPinata.Drawing.Pdf;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents the content of a page. PdfPinata supports only one content stream per page.
/// If an imported page has an array of content streams, the streams are concatenated to
/// one single stream.
/// </summary>
public sealed class PdfContent : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContent"/> class.
    /// </summary>
    public PdfContent(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContent"/> class.
    /// </summary>
    internal PdfContent(PdfPage page)
        : base(page != null ? page.Owner : null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfContent"/> class.
    /// </summary>
    /// <param name="dict">The dict.</param>
    public PdfContent(PdfDictionary dict) // HACK PdfContent
        : base(dict)
    {
        // A PdfContent dictionary is always unfiltered.
        Decode();
    }

    /// <summary>
    /// Sets a value indicating whether the content is compressed with the ZIP algorithm.
    /// </summary>
    public bool Compressed
    {
        set
        {
            if (!value)
                return;

            var filter = Elements[PdfStream.Keys.Filter];
            if (filter != null)
                return;

            Stream.SetContent(Stream.Value, compress: true);
        }
    }

    /// <summary>
    /// Unfilters the stream. A filter that cannot be decoded leaves the stream as it was.
    /// </summary>
    /// <remarks>
    /// <see cref="PdfDictionary.PdfStream.TryUnfilter"/> writes <c>/Length</c> through the
    /// <see cref="PdfDictionary.PdfStream.Value"/> setter.
    /// </remarks>
    private void Decode() => Stream?.TryUnfilter();

    /// <summary>
    /// Surround content with q/Q operations if necessary.
    /// </summary>
    internal void PreserveGraphicsState()
    {
        // If a content stream is touched by PdfPinata it is typically because graphical operations are
        // prepended or appended. Some nasty PDF tools does not preserve the graphical state correctly.
        // Therefore we try to relieve the problem by surrounding the content stream with push/restore
        // graphic state operation.
        if (Stream == null)
            return;

        var value = Stream.Value;
        var length = value.Length;
        if (length == 0 || (value[0] == (byte)'q' && value[1] == (byte)'\n'))
            return;

        var newValue = new byte[length + 2 + 3];
        newValue[0] = (byte)'q';
        newValue[1] = (byte)'\n';
        Array.Copy(value, 0, newValue, 2, length);
        newValue[length + 2] = (byte)' ';
        newValue[length + 3] = (byte)'Q';
        newValue[length + 4] = (byte)'\n';
        Stream.Value = newValue;
        Elements.SetInteger("/Length", Stream.Length);
    }

    internal override void WriteObject(PdfWriter writer)
    {
        if (PdfRenderer != null)
        {
            PdfRenderer.Close();
            Debug.Assert(PdfRenderer == null);
        }

        if (Stream != null)
        {
            // SetContent keeps the deflated form only where it is shorter: an empty stream
            // deflated is eight bytes of framing around nothing, which Acrobat reports as an
            // error on the page.
            if (Owner.Options.CompressContentStreams && Elements.GetName("/Filter").Length == 0)
                Stream.SetContent(Stream.Value, compress: true);
            Elements.SetInteger("/Length", Stream.Length);
        }

        base.WriteObject(writer);
    }

    internal XGraphicsPdfRenderer PdfRenderer;

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed class Keys : PdfStream.Keys
    {
        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
