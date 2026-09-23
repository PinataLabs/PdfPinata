using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// How the rows and columns of an image are stored against the way the page shows them.
/// </summary>
public enum PdfImageOrientation
{
    /// <summary>The image is shown the way it is stored.</summary>
    Normal,

    /// <summary>The image is shown with its rows in the opposite order: turned upside down.</summary>
    FlipVertical,

    /// <summary>The image is shown with its columns in the opposite order: turned left to right.</summary>
    FlipHorizontal,

    /// <summary>The image is shown turned half way round, which is both flips at once.</summary>
    Rotate180,

    /// <summary>
    /// The image is turned, sheared or otherwise placed in a way none of the above describes.
    /// <see cref="PdfImagePlacement.Transform"/> says what was asked for.
    /// </summary>
    Other
}

/// <summary>
/// One drawing of one image on a page, and the transform the page draws it under.
/// <para>
/// An image is stored with its first row of samples at the top, and the page decides which way
/// up that ends up: a matrix with a negative vertical scale, which is a common enough thing for
/// a writer of PDF to use, stores the image upside down and turns it back over as it is drawn.
/// So the bytes of the stream are not in themselves the picture that the page shows, and code
/// that pulls the stream out and saves it gets an image the wrong way up without being told.
/// </para>
/// <para>
/// The same image may be drawn more than once, in a different place and a different way up each
/// time, so this is a drawing of an image rather than an image.
/// </para>
/// </summary>
public sealed class PdfImagePlacement
{
    internal PdfImagePlacement(string name, PdfDictionary xObject, XMatrix transform)
    {
        _name = name;
        _xObject = xObject;
        _transform = transform;
    }

    private readonly string _name;
    private readonly PdfDictionary _xObject;
    private readonly XMatrix _transform;

    /// <summary>
    /// The name the resources the drawing was made in give the image, such as "/Im0". The same
    /// image may go by different names in different scopes, so this says how it was reached
    /// rather than what it is.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// The image XObject itself, to read the stream and the rest of the entries from.
    /// </summary>
    public PdfDictionary XObject => _xObject;

    /// <summary>
    /// The transform in force where the image was drawn, which maps the unit square onto the
    /// page. The image occupies the unit square with its first row of samples along the top
    /// edge, so the vertical scale being negative is what turns it over.
    /// <para>
    /// This is in the coordinates of the page and takes no account of the page's /Rotate entry,
    /// which turns everything the page holds alike.
    /// </para>
    /// </summary>
    public XMatrix Transform => _transform;

    /// <summary>The width of the image in samples.</summary>
    public int PixelWidth => _xObject.Elements.GetInteger(PdfImage.Keys.Width);

    /// <summary>The height of the image in samples.</summary>
    public int PixelHeight => _xObject.Elements.GetInteger(PdfImage.Keys.Height);

    /// <summary>
    /// Which way round the stored image is against the way the page shows it. Reversing this is
    /// what makes an extracted image match what the page looks like.
    /// </summary>
    public PdfImageOrientation Orientation
    {
        get
        {
            double a = _transform.M11, b = _transform.M12;
            double c = _transform.M21, d = _transform.M22;

            // Judged against the size of the transform rather than against a fixed figure, so
            // that a matrix carrying a rounding error in the off-diagonal is still square.
            var scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)),
                Math.Max(Math.Abs(c), Math.Abs(d)));
            if (scale <= 0)
                return PdfImageOrientation.Normal;

            var tolerance = scale * 1e-6;
            if (Math.Abs(b) > tolerance || Math.Abs(c) > tolerance)
                return PdfImageOrientation.Other;

            if (a >= 0)
                return d >= 0 ? PdfImageOrientation.Normal : PdfImageOrientation.FlipVertical;

            return d >= 0 ? PdfImageOrientation.FlipHorizontal : PdfImageOrientation.Rotate180;
        }
    }

    /// <summary>
    /// Whether the image is shown reflected rather than merely turned. A half turn is not a
    /// reflection; either flip on its own is.
    /// </summary>
    public bool IsMirrored
    {
        get
        {
            var determinant = _transform.M11 * _transform.M22 - _transform.M12 * _transform.M21;
            return determinant < 0;
        }
    }

    /// <summary>
    /// The bytes of the image stream as they are stored, with the filters named by /Filter
    /// still on them. A stream filtered by /DCTDecode is a JPEG file, and one filtered by
    /// /JPXDecode a JPEG 2000 file, so those can be handed to an image library as they stand;
    /// the rest are raw sample data that the entries of <see cref="XObject"/> describe.
    /// <para>
    /// The bytes are the image the way it is stored, which <see cref="Orientation"/> says may
    /// not be the way the page shows it.
    /// </para>
    /// </summary>
    public byte[] GetRawStream()
    {
        return _xObject.Stream == null ? Array.Empty<byte>() : _xObject.Stream.Value;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    public override string ToString()
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0} {1}x{2} {3}", _name, PixelWidth, PixelHeight, Orientation);
    }
}
