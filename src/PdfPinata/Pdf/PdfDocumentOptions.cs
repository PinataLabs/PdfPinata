#region Copyright
//
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

// ReSharper disable ConvertToAutoProperty

using System;

namespace PdfPinata.Pdf;

/// <summary>
/// Holds information how to handle the document when it is saved as PDF stream.
/// </summary>
public sealed class PdfDocumentOptions
{
    internal PdfDocumentOptions()
    {
    }

    /// <summary>
    /// Gets or sets the color mode.
    /// </summary>
    public PdfColorMode ColorMode
    {
        get => _colorMode;
        set => _colorMode = value;
    }
    private PdfColorMode _colorMode = PdfColorMode.Rgb;

    /// <summary>
    /// Gets or sets a value indicating whether to compress content streams of PDF pages.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default is <c>true</c>, in every build. It used to be <c>false</c> under <c>#if DEBUG</c>
    /// so that a page's content could be read straight out of the file while debugging, which meant
    /// the same calling code wrote a materially larger PDF from a debug build than from a release
    /// one - a difference that belonged to whoever built the library rather than to whoever called
    /// it, and that made two files impossible to compare without knowing which configuration each
    /// came from. Set it to <c>false</c> to get the readable content stream back.
    /// </para>
    /// </remarks>
    public bool CompressContentStreams
    {
        get => _compressContentStreams;
        set => _compressContentStreams = value;
    }
    private bool _compressContentStreams = true;

    /// <summary>
    /// Gets or sets a value indicating that all objects are not compressed.
    /// </summary>
    public bool NoCompression
    {
        get => _noCompression;
        set => _noCompression = value;
    }
    private bool _noCompression;

    /// <summary>
    /// Gets or sets the flate encode mode. Besides the balanced default mode you can set modes for best compression (slower) or best speed (larger files).
    /// </summary>
    public PdfFlateEncodeMode FlateEncodeMode
    {
        get => _flateEncodeMode;
        set => _flateEncodeMode = value;
    }
    private PdfFlateEncodeMode _flateEncodeMode = PdfFlateEncodeMode.Default;

    /// <summary>
    /// Gets or sets a value indicating whether to compress JPEG images with the FlateDecode filter.
    /// </summary>
    public PdfUseFlateDecoderForJpegImages UseFlateDecoderForJpegImages
    {
        get => _useFlateDecoderForJpegImages;
        set => _useFlateDecoderForJpegImages = value;
    }
    private PdfUseFlateDecoderForJpegImages _useFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Never;

    /// <summary>
    /// Gets or sets how the objects of the document are indexed when it is saved. The default is
    /// <see cref="PdfCrossReferenceFormat.Classic"/>, which every reader understands.
    /// <see cref="PdfCrossReferenceFormat.Stream"/> gathers the objects that may be gathered into
    /// object streams and compresses them together, which is markedly smaller on a document that is
    /// mostly objects rather than mostly content, and requires a reader that understands PDF 1.5.
    /// </summary>
    public PdfCrossReferenceFormat CrossReferenceFormat
    {
        get => _crossReferenceFormat;
        set => _crossReferenceFormat = value;
    }
    private PdfCrossReferenceFormat _crossReferenceFormat = PdfCrossReferenceFormat.Classic;

    /// <summary>
    /// Gets or sets the archival profile the document claims. The default is
    /// <see cref="PdfAConformance.None"/>, which claims nothing and enforces nothing.
    /// <para>
    /// Setting it does more than label the file: the writer refuses to save a document that breaks
    /// a rule of the profile claimed, naming the rule. A conformance claim found to be false by a
    /// validator, or by a customer, is worse than no claim at all.
    /// </para>
    /// </summary>
    public PdfAConformance Conformance
    {
        get => _conformance;
        set => _conformance = value;
    }
    private PdfAConformance _conformance = PdfAConformance.None;

    /// <summary>
    /// Gets or sets the accessibility profile the document claims. The default is
    /// <see cref="PdfUAConformance.None"/>.
    /// <para>
    /// As with <see cref="Conformance"/>, the claim is enforced rather than stamped on: the writer
    /// walks the structure tree and the pages before writing anything and throws naming the first
    /// rule the document breaks. The rules it can settle by looking are listed on
    /// <see cref="Structure.PdfUaValidator"/>, and they are not all of PDF/UA.
    /// </para>
    /// </summary>
    public PdfUAConformance UAConformance
    {
        get => _uaConformance;
        set => _uaConformance = value;
    }
    private PdfUAConformance _uaConformance = PdfUAConformance.None;

    /// <summary>
    /// Gets or sets a value indicating that an XMP metadata packet is written even when no
    /// conformance is claimed. A claimed conformance implies one regardless.
    /// </summary>
    /// <remarks>
    /// The older spelling of <see cref="MetadataStrategy"/>: true is
    /// <see cref="Metadata.PdfMetadataStrategy.AutoGenerate"/>. Setting it false undoes that and
    /// leaves any other strategy as it was.
    /// </remarks>
    public bool WriteXmpMetadata
    {
        get => _metadataStrategy == Metadata.PdfMetadataStrategy.AutoGenerate;
        set
        {
            if (value)
                _metadataStrategy = Metadata.PdfMetadataStrategy.AutoGenerate;
            else if (_metadataStrategy == Metadata.PdfMetadataStrategy.AutoGenerate)
                _metadataStrategy = Metadata.PdfMetadataStrategy.KeepExisting;
        }
    }

    /// <summary>
    /// Gets or sets what a save does with the document's XMP metadata packet. The default,
    /// <see cref="Metadata.PdfMetadataStrategy.KeepExisting"/>, is what saving has always done.
    /// </summary>
    /// <remarks>
    /// A conformance claim always writes a fresh packet, and refuses
    /// <see cref="Metadata.PdfMetadataStrategy.NoMetadata"/> at save time; see
    /// <see cref="Metadata.PdfMetadataStrategy"/>.
    /// </remarks>
    public Metadata.PdfMetadataStrategy MetadataStrategy
    {
        get => _metadataStrategy;
        set
        {
            if (!Enum.IsDefined(value))
                throw new System.ArgumentOutOfRangeException(nameof(value), value, "Not a metadata strategy.");

            _metadataStrategy = value;
        }
    }
    private Metadata.PdfMetadataStrategy _metadataStrategy;

    /// <summary>
    /// Gets or sets the ICC profile embedded as the document's output intent, which every PDF/A
    /// profile requires whenever a device-dependent colour space is used — and
    /// <see cref="PdfColorMode.Rgb"/>, the default, is one.
    /// <para>
    /// The profile is embedded rather than referenced: naming a well-known profile is exactly what
    /// PDF/A exists to stop, since the name means nothing once the machine that understood it is
    /// gone.
    /// </para>
    /// <para>
    /// <b>Left unset, an RGB document is given <see cref="PdfOutputIntents.SrgbProfile"/></b>, and
    /// <see cref="OutputIntentIdentifier"/> the sRGB condition to go with it — colours written as
    /// RGB by a library nobody told otherwise are sRGB, so that is a description rather than a
    /// guess. A <see cref="PdfColorMode.Cmyk"/> or <see cref="PdfColorMode.Undefined"/> document is
    /// refused at save time instead, because nothing true could be supplied for either. Whatever is
    /// set here always wins.
    /// </para>
    /// </summary>
    public byte[] OutputIntentIccProfile
    {
        get => _outputIntentIccProfile;
        set => _outputIntentIccProfile = value;
    }
    private byte[] _outputIntentIccProfile;

    /// <summary>
    /// Gets or sets the name of the output condition the profile describes, such as
    /// "sRGB IEC61966-2.1". Written as <c>/OutputConditionIdentifier</c>.
    /// </summary>
    public string OutputIntentIdentifier
    {
        get => _outputIntentIdentifier;
        set => _outputIntentIdentifier = value;
    }
    private string _outputIntentIdentifier = DefaultOutputIntentIdentifier;

    /// <summary>
    /// What <see cref="OutputIntentIdentifier"/> says when nobody has said anything: a placeholder
    /// naming no condition at all.
    /// </summary>
    /// <remarks>
    /// Named rather than repeated, because the writer has to be able to tell "the caller told me
    /// nothing" from "the caller told me this" — a document given the built-in sRGB profile is
    /// given the sRGB condition to go with it, and only a caller who named a condition themselves
    /// keeps theirs.
    /// </remarks>
    internal const string DefaultOutputIntentIdentifier = "Custom";

    /// <summary>
    /// Gets or sets a value indicating whether the document is written with one copy of each
    /// font, image, form and other resource whose content is identical, rather than one copy for
    /// every file it was imported from. The default is <c>false</c>.
    /// <para>
    /// Merging many documents by importing their pages copies each document's fonts and images
    /// separately, so a logo or a font that every one of them carries is written once per
    /// document. With this set, the save compares the resources of every page - fonts and their
    /// programs, descriptors and widths, images, forms, colour spaces, graphics states, patterns,
    /// shadings, functions and page content streams - and keeps one of each set that is byte for
    /// byte the same, all references included. Objects whose identity matters are never merged:
    /// pages, annotations, form fields, structure elements, optional content groups, signatures.
    /// </para>
    /// <para>
    /// Only identical objects are merged. Two subsets of the same font taken for different text are
    /// different programs and both are kept.
    /// </para>
    /// <para>
    /// A document opened with <see cref="IO.PdfDocumentOpenMode.Append"/> refuses to save with this
    /// set, because an appended revision keeps every object before it and so cannot drop a copy.
    /// </para>
    /// </summary>
    public bool DeduplicateResources { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the document honours the embedding permissions a
    /// font declares in its OS/2 <c>fsType</c>. The default is <c>false</c>, which embeds every font
    /// as this library always has.
    /// <para>
    /// With this set, a font whose licence forbids embedding it is refused with an
    /// <see cref="System.InvalidOperationException"/> naming the face and the restriction: one marked
    /// Restricted License, and one marked Bitmap Embedding Only, because this library embeds outlines
    /// rather than bitmaps. The refusal comes when the font is first drawn in this document and again
    /// at save time, so setting this after drawing still holds. Preview &amp; Print, Editable and
    /// Installable fonts are embedded as before, and a font setting several usage bits, as the oldest
    /// OS/2 tables may, is read by the least restrictive of them, as the OpenType specification says.
    /// </para>
    /// <para>
    /// A font marked No Subsetting is embedded whole rather than refused, as a font with PostScript
    /// outlines always is, and its name carries no subset tag. That costs the size of the whole font
    /// program instead of the glyphs the document draws.
    /// </para>
    /// <para>
    /// It is an option of the document rather than of <see cref="Fonts.GlobalFontSettings"/>
    /// because a font is embedded into one document at a time: the face is read once and shared, but
    /// whether one document may carry it is that document's question, and one caller opting in
    /// should not change what another's documents contain.
    /// </para>
    /// <para>
    /// Every PDF/A profile requires its fonts to be legally embeddable, and claiming
    /// <see cref="Conformance"/> does not turn this on: <c>fsType</c> is what the font says about its
    /// licence, not the licence itself, and a caller who holds an embedding licence for a restricted
    /// font is entitled to use it. Set it alongside a conformance claim to have the writer check what
    /// the font says.
    /// </para>
    /// </summary>
    public bool RespectFontEmbeddingRestrictions { get; set; }

    /// <summary>
    /// Gets or sets how many objects at most are gathered into one object stream. Only meaningful
    /// when <see cref="CrossReferenceFormat"/> is <see cref="PdfCrossReferenceFormat.Stream"/>.
    /// Acrobat uses 200 and so does this; a reader has to decompress a whole object stream to reach
    /// any one object in it, so a larger number trades reading cost for a smaller file.
    /// </summary>
    public int MaxObjectsPerObjectStream
    {
        get => _maxObjectsPerObjectStream;
        set
        {
            if (value < 1)
                throw new System.ArgumentOutOfRangeException(nameof(value),
                    "An object stream has to hold at least one object.");
            _maxObjectsPerObjectStream = value;
        }
    }
    private int _maxObjectsPerObjectStream = 200;
}
