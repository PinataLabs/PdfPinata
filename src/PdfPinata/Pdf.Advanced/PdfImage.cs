#region Copyright
//
// Authors:
//   Stefan Lange
//   Thomas Hövel
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

using System.Diagnostics;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Filters;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents an image.
/// </summary>
public sealed class PdfImage : PdfXObject
{
    /// <summary>
    /// Initializes a new instance of PdfImage from an XImage.
    /// </summary>
    public PdfImage(PdfDocument document, XImage image)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/XObject");
        Elements.SetName(Keys.Subtype, "/Image");

        _image = image;

        switch (_image.Format.Guid.ToString("B").ToUpper())
        {
            // Pdf supports Jpeg, therefore we can write what we've read:
            case "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}":  //XImageFormat.Jpeg
                InitializeJpeg();
                break;

            // All other image formats are converted to PDF bitmaps:
            case "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}":  //XImageFormat.Png
            case "{B96B3CB0-0728-11D3-9D7B-0000F81EF32E}":  //XImageFormat.Gif
            case "{B96B3CB1-0728-11D3-9D7B-0000F81EF32E}":  //XImageFormat.Tiff
            case "{B96B3CB5-0728-11D3-9D7B-0000F81EF32E}":  //XImageFormat.Icon
                InitializeNonJpeg();
                break;

            case "{84570158-DBF0-4C6B-8368-62D6A3CA76E0}":  //XImageFormat.Pdf:
                Debug.Assert(false, "XPdfForm not expected here.");
                break;

            default:
                Debug.Assert(false, "Unexpected image type.");
                break;
        }
    }

    /// <summary>
    /// Gets the underlying XImage object.
    /// </summary>
    public XImage Image => _image;

    private readonly XImage _image;

    /// <summary>
    /// Returns 'Image'.
    /// </summary>
    public override string ToString()
    {
        return "Image";
    }

    /// <summary>
    /// Creates the keys for a JPEG image.
    /// </summary>
    private void InitializeJpeg()
    {
        byte[] imageBits;

        using (var memory = _image.AsJpeg())
        {
            imageBits = memory.ToArray();
        }

        var tryFlateDecode = _document.Options.UseFlateDecoderForJpegImages == PdfUseFlateDecoderForJpegImages.Automatic;
        var useFlateDecode = _document.Options.UseFlateDecoderForJpegImages == PdfUseFlateDecoderForJpegImages.Always;

        var fd = new FlateDecode();
        var imageDataCompressed = useFlateDecode || tryFlateDecode ? fd.Encode(imageBits, _document.Options.FlateEncodeMode) : null;
        if (useFlateDecode || tryFlateDecode && imageDataCompressed.Length < imageBits.Length)
        {
            Stream = new PdfStream(imageDataCompressed, this);
            Elements[PdfStream.Keys.Length] = new PdfInteger(imageDataCompressed.Length);
            var arrayFilters = new PdfArray(_document);
            arrayFilters.Elements.Add(new PdfName("/FlateDecode"));
            arrayFilters.Elements.Add(new PdfName("/DCTDecode"));
            Elements[PdfStream.Keys.Filter] = arrayFilters;
        }
        else
        {
            Stream = new PdfStream(imageBits, this);
            Elements[PdfStream.Keys.Length] = new PdfInteger(imageBits.Length);
            Elements[PdfStream.Keys.Filter] = new PdfName("/DCTDecode");
        }
        if (_image.Interpolate)
            Elements[Keys.Interpolate] = PdfBoolean.True;
        Elements[Keys.Width] = new PdfInteger(_image.PixelWidth);
        Elements[Keys.Height] = new PdfInteger(_image.PixelHeight);
        Elements[Keys.BitsPerComponent] = new PdfInteger(8);
        Elements[Keys.ColorSpace] = new PdfName("/DeviceRGB");
    }

    /// <summary>
    /// Creates the keys for a FLATE image, from the pixels the backend decoded.
    /// </summary>
    /// <remarks>
    /// The pixels arrive tightly packed, top-down and BGRA - see <see cref="PixelBuffer"/> - which
    /// is exactly the layout written out here, one channel reordering aside. Nothing about the
    /// buffer is validated: the producer and this consumer are two ends of one call, so there is no
    /// magic number, declared length or compression field between them to disagree about.
    /// <para>
    /// Every image is written as 8-bit <c>/DeviceRGB</c>. Grayscale and CMYK are still unsupported,
    /// deliberately: a <see cref="PixelBuffer"/> says BGRA and nothing else, and neither backend's
    /// decode step produces CMYK samples in the first place. Both wait on a second buffer shape
    /// actually existing rather than on a parameter written in advance of one.
    /// </para>
    /// </remarks>
    private void InitializeNonJpeg()
    {
        var pdfVersion = Owner.Version;
        var pixels = _image.GetPixels();

        Debug.Assert(!pixels.IsEmpty, "Image decoding produced no pixels.");
        if (pixels.IsEmpty)
            return;

        var width = pixels.Width;
        var height = pixels.Height;
        var source = pixels.Pixels.Span;

        var imageData = new byte[3 * width * height];

        var hasMask = false;
        var hasAlphaMask = false;
        var alphaMask = new byte[width * height];
        var mask = new MonochromeMask(width, height);

        // Row r of the source is row r of the output: both are top-down and neither pads.
        var read = 0;
        var write = 0;
        var writeAlpha = 0;
        for (var y = 0; y < height; ++y)
        {
            mask.StartLine(y);

            for (var x = 0; x < width; ++x)
            {
                // BGRA in, RGB out.
                imageData[write] = source[read + 2];
                imageData[write + 1] = source[read + 1];
                imageData[write + 2] = source[read];

                var alpha = source[read + 3];
                mask.AddPel(alpha);
                alphaMask[writeAlpha] = alpha;
                if (alpha != 255)
                {
                    hasMask = true;
                    if (alpha != 0)
                        hasAlphaMask = true;
                }

                ++writeAlpha;
                read += PixelBuffer.BytesPerPixel;
                write += 3;
            }
        }

        var fd = new FlateDecode();

        // The soft mask carries alpha exactly; the stencil rounds it to transparent or opaque
        // at 128. Where the soft mask goes the stencil is therefore redundant at best, and it
        // is worse than that: ISO 32000-1 Table 89 has /SMask override an image's /Mask, and
        // pdf.js obeys that, but Ghostscript and macOS Quartz apply both - so a picture whose
        // alpha lay wholly below 128, a watermark say, was embedded, referenced, and
        // invisible. So the two are alternatives rather than a pair. That leaves the stencil
        // the two cases where it is the whole answer: transparency that is already binary,
        // where it loses nothing, and a document too old to be read a soft mask.
        var hasSoftMask = hasMask && hasAlphaMask && pdfVersion >= 14;

        if (hasMask && !hasSoftMask)
        {
            // Either binary transparency, which the stencil states exactly, or a pre-1.4
            // document, where it is all a reader of that vintage could have been given.
            var maskDataCompressed = fd.Encode(mask.MaskData, _document.Options.FlateEncodeMode);
            var pdfMask = new PdfDictionary(_document);
            pdfMask.Elements.SetName(Keys.Type, "/XObject");
            pdfMask.Elements.SetName(Keys.Subtype, "/Image");

            Owner._irefTable.Add(pdfMask);
            pdfMask.Stream = new PdfStream(maskDataCompressed, pdfMask);
            pdfMask.Elements[PdfStream.Keys.Length] = new PdfInteger(maskDataCompressed.Length);
            pdfMask.Elements[PdfStream.Keys.Filter] = new PdfName("/FlateDecode");
            pdfMask.Elements[Keys.Width] = new PdfInteger(width);
            pdfMask.Elements[Keys.Height] = new PdfInteger(height);
            pdfMask.Elements[Keys.BitsPerComponent] = new PdfInteger(1);
            pdfMask.Elements[Keys.ImageMask] = new PdfBoolean(true);
            Elements[Keys.Mask] = pdfMask.Reference;
        }
        if (hasSoftMask)
        {
            // The image provides an alpha mask (requires Arcrobat 5.0 or higher)
            var alphaMaskCompressed = fd.Encode(alphaMask, _document.Options.FlateEncodeMode);
            var smask = new PdfDictionary(_document);
            smask.Elements.SetName(Keys.Type, "/XObject");
            smask.Elements.SetName(Keys.Subtype, "/Image");

            Owner._irefTable.Add(smask);
            smask.Stream = new PdfStream(alphaMaskCompressed, smask);
            smask.Elements[PdfStream.Keys.Length] = new PdfInteger(alphaMaskCompressed.Length);
            smask.Elements[PdfStream.Keys.Filter] = new PdfName("/FlateDecode");
            smask.Elements[Keys.Width] = new PdfInteger(width);
            smask.Elements[Keys.Height] = new PdfInteger(height);
            smask.Elements[Keys.BitsPerComponent] = new PdfInteger(8);
            smask.Elements[Keys.ColorSpace] = new PdfName("/DeviceGray");
            Elements[Keys.SMask] = smask.Reference;
        }

        var imageDataCompressed = fd.Encode(imageData, _document.Options.FlateEncodeMode);

        Stream = new PdfStream(imageDataCompressed, this);
        Elements[PdfStream.Keys.Length] = new PdfInteger(imageDataCompressed.Length);
        Elements[PdfStream.Keys.Filter] = new PdfName("/FlateDecode");
        Elements[Keys.Width] = new PdfInteger(width);
        Elements[Keys.Height] = new PdfInteger(height);
        Elements[Keys.BitsPerComponent] = new PdfInteger(8);
        Elements[Keys.ColorSpace] = new PdfName("/DeviceRGB");
        if (_image.Interpolate)
            Elements[Keys.Interpolate] = PdfBoolean.True;
    }

    /// <summary>
    /// Common keys for all streams.
    /// </summary>
    public sealed new class Keys : PdfXObject.Keys
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes;
        /// if present, must be XObject for an image XObject.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) The type of XObject that this dictionary describes;
        /// must be Image for an image XObject.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Subtype = "/Subtype";

        /// <summary>
        /// (Required) The width of the image, in samples.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string Width = "/Width";

        /// <summary>
        /// (Required) The height of the image, in samples.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string Height = "/Height";

        /// <summary>
        /// (Required for images, except those that use the JPXDecode filter; not allowed for image masks)
        /// The color space in which image samples are specified; it can be any type of color space except
        /// Pattern. If the image uses the JPXDecode filter, this entry is optional:
        /// • If ColorSpace is present, any color space specifications in the JPEG2000 data are ignored.
        /// • If ColorSpace is absent, the color space specifications in the JPEG2000 data are used.
        ///   The Decode array is also ignored unless ImageMask is true.
        /// </summary>
        [KeyInfo(KeyType.NameOrArray | KeyType.Required)]
        public const string ColorSpace = "/ColorSpace";

        /// <summary>
        /// (Required except for image masks and images that use the JPXDecode filter)
        /// The number of bits used to represent each color component. Only a single value may be specified;
        /// the number of bits is the same for all color components. Valid values are 1, 2, 4, 8, and
        /// (in PDF 1.5) 16. If ImageMask is true, this entry is optional, and if specified, its value
        /// must be 1.
        /// If the image stream uses a filter, the value of BitsPerComponent must be consistent with the
        /// size of the data samples that the filter delivers. In particular, a CCITTFaxDecode or JBIG2Decode
        /// filter always delivers 1-bit samples, a RunLengthDecode or DCTDecode filter delivers 8-bit samples,
        /// and an LZWDecode or FlateDecode filter delivers samples of a specified size if a predictor function
        /// is used.
        /// If the image stream uses the JPXDecode filter, this entry is optional and ignored if present.
        /// The bit depth is determined in the process of decoding the JPEG2000 image.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string BitsPerComponent = "/BitsPerComponent";

        /// <summary>
        /// (Optional; PDF 1.1) The name of a color rendering intent to be used in rendering the image.
        /// Default value: the current rendering intent in the graphics state.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Intent = "/Intent";

        /// <summary>
        /// (Optional) A flag indicating whether the image is to be treated as an image mask.
        /// If this flag is true, the value of BitsPerComponent must be 1 and Mask and ColorSpace should
        /// not be specified; unmasked areas are painted using the current nonstroking color.
        /// Default value: false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string ImageMask = "/ImageMask";

        /// <summary>
        /// (Optional except for image masks; not allowed for image masks; PDF 1.3)
        /// An image XObject defining an image mask to be applied to this image, or an array specifying
        /// a range of colors to be applied to it as a color key mask. If ImageMask is true, this entry
        /// must not be present.
        /// </summary>
        [KeyInfo(KeyType.StreamOrArray | KeyType.Optional)]
        public const string Mask = "/Mask";

        /// <summary>
        /// (Optional) An array of numbers describing how to map image samples into the range of values
        /// appropriate for the image’s color space. If ImageMask is true, the array must be either
        /// [0 1] or [1 0]; otherwise, its length must be twice the number of color components required
        /// by ColorSpace. If the image uses the JPXDecode filter and ImageMask is false, Decode is ignored.
        /// Default value: see “Decode Arrays”.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Decode = "/Decode";

        /// <summary>
        /// (Optional) A flag indicating whether image interpolation is to be performed.
        /// Default value: false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string Interpolate = "/Interpolate";

        /// <summary>
        /// (Optional; PDF 1.3) An array of alternate image dictionaries for this image. The order of
        /// elements within the array has no significance. This entry may not be present in an image
        /// XObject that is itself an alternate image.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Alternates = "/Alternates";

        /// <summary>
        /// (Optional; PDF 1.4) A subsidiary image XObject defining a soft-mask image to be used as a
        /// source of mask shape or mask opacity values in the transparent imaging model. The alpha
        /// source parameter in the graphics state determines whether the mask values are interpreted as
        /// shape or opacity. If present, this entry overrides the current soft mask in the graphics state,
        /// as well as the image’s Mask entry, if any. (However, the other transparency related graphics
        /// state parameters — blend mode and alpha constant — remain in effect.) If SMask is absent, the
        /// image has no associated soft mask (although the current soft mask in the graphics state may
        /// still apply).
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string SMask = "/SMask";

        /// <summary>
        /// (Optional for images that use the JPXDecode filter, meaningless otherwise; PDF 1.5)
        /// A code specifying how soft-mask information encoded with image samples should be used:
        /// 0 If present, encoded soft-mask image information should be ignored.
        /// 1 The image’s data stream includes encoded soft-mask values. An application can create
        ///   a soft-mask image from the information to be used as a source of mask shape or mask
        ///   opacity in the transparency imaging model.
        /// 2 The image’s data stream includes color channels that have been preblended with a
        ///   background; the image data also includes an opacity channel. An application can create
        ///   a soft-mask image with a Matte entry from the opacity channel information to be used as
        ///   a source of mask shape or mask opacity in the transparency model. If this entry has a
        ///   nonzero value, SMask should not be specified.
        /// Default value: 0.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string SMaskInData = "/SMaskInData";

        /// <summary>
        /// (Required in PDF 1.0; optional otherwise) The name by which this image XObject is
        /// referenced in the XObject subdictionary of the current resource dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Name = "/Name";

        /// <summary>
        /// (Required if the image is a structural content item; PDF 1.3) The integer key of the
        /// image’s entry in the structural parent tree.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string StructParent = "/StructParent";

        /// <summary>
        /// (Optional; PDF 1.3; indirect reference preferred) The digital identifier of the image’s
        /// parent Web Capture content set.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Optional)]
        public const string ID = "/ID";

        /// <summary>
        /// (Optional; PDF 1.2) An OPI version dictionary for the image. If ImageMask is true,
        /// this entry is ignored.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string OPI = "/OPI";

        /// <summary>
        /// (Optional; PDF 1.4) A metadata stream containing metadata for the image.
        /// </summary>
        [KeyInfo(KeyType.Stream | KeyType.Optional)]
        public const string Metadata = "/Metadata";

        /// <summary>
        /// (Optional; PDF 1.5) An optional content group or optional content membership dictionary,
        /// specifying the optional content properties for this image XObject. Before the image is
        /// processed, its visibility is determined based on this entry. If it is determined to be
        /// invisible, the entire image is skipped, as if there were no Do operator to invoke it.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string OC = "/OC";

        // ReSharper restore InconsistentNaming
    }
}

/// <summary>
/// Helper class for creating bitmap masks (8 pels per byte).
/// </summary>
internal class MonochromeMask
{
    /// <summary>
    /// Returns the bitmap mask that will be written to PDF.
    /// </summary>
    public byte[] MaskData => _maskData;

    private readonly byte[] _maskData;

    /// <summary>
    /// Creates a bitmap mask.
    /// </summary>
    public MonochromeMask(int sizeX, int sizeY)
    {
        _sizeX = sizeX;
        var byteSize = (sizeX + 7) / 8 * sizeY;
        _maskData = new byte[byteSize];
        StartLine(0);
    }

    /// <summary>
    /// Starts a new line, counted from the top of the image as the pixels are.
    /// </summary>
    public void StartLine(int newCurrentLine)
    {
        _bitsWritten = 0;
        _byteBuffer = 0;
        _writeOffset = (_sizeX + 7) / 8 * newCurrentLine;
    }

    /// <summary>
    /// Adds a pel to the current line.
    /// </summary>
    /// <param name="isTransparent"></param>
    public void AddPel(bool isTransparent)
    {
        if (_bitsWritten >= _sizeX)
            return;

        // Mask: 0: opaque, 1: transparent (default mapping)
        if (isTransparent)
            _byteBuffer = (_byteBuffer << 1) + 1;
        else
            _byteBuffer = _byteBuffer << 1;
        ++_bitsWritten;
        if ((_bitsWritten & 7) == 0)
        {
            _maskData[_writeOffset] = (byte)_byteBuffer;
            ++_writeOffset;
            _byteBuffer = 0;
        }
        else if (_bitsWritten == _sizeX)
        {
            var n = 8 - (_bitsWritten & 7);
            _byteBuffer = _byteBuffer << n;
            _maskData[_writeOffset] = (byte)_byteBuffer;
        }
    }

    /// <summary>
    /// Adds a pel from an alpha mask value.
    /// </summary>
    public void AddPel(int shade)
    {
        // NYI: dithering!!!
        AddPel(shade < 128);
    }

    private readonly int _sizeX;
    private int _writeOffset;
    private int _byteBuffer;
    private int _bitsWritten;
}
