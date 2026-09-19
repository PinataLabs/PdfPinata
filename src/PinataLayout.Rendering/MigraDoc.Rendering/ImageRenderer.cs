#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering.MigraDoc.Rendering.Resources;
using PdfPinata.Fonts;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders images.
/// </summary>
internal class ImageRenderer : ShapeRenderer
{
    internal ImageRenderer(XGraphics gfx, Image image, FieldInfos fieldInfos)
        : base(gfx, image, fieldInfos)
    {
        this.image = image;
        ImageRenderInfo renderInfo = new ImageRenderInfo();
        renderInfo.shape = shape;
        this.renderInfo = renderInfo;
    }

    internal ImageRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
        : base(gfx, renderInfo, fieldInfos)
    {
        image = (Image)renderInfo.DocumentObject;
    }

    internal override void Format(Area area, FormatInfo previousFormatInfo)
    {
        ImageFormatInfo formatInfo = (ImageFormatInfo)renderInfo.FormatInfo;
        formatInfo.ImageSource = image.Source;
        formatInfo.Failure = ImageFailure.None;
        formatInfo.FailureException = null;
        CalculateImageDimensions();
        base.Format(area, previousFormatInfo);
    }

    protected override XUnit ShapeHeight
    {
        get
        {
            ImageFormatInfo formatInfo = (ImageFormatInfo)renderInfo.FormatInfo;
            return formatInfo.Height + lineFormatRenderer.GetWidth();
        }
    }

    protected override XUnit ShapeWidth
    {
        get
        {
            ImageFormatInfo formatInfo = (ImageFormatInfo)renderInfo.FormatInfo;
            return formatInfo.Width + lineFormatRenderer.GetWidth();
        }
    }

    internal override void Render()
    {
        using (Tagger.Artifact(gfx))
            RenderFilling();

        ImageFormatInfo formatInfo = (ImageFormatInfo)renderInfo.FormatInfo;
        Area contentArea = renderInfo.LayoutInfo.ContentArea;
        XRect destRect = new XRect(contentArea.X, contentArea.Y, formatInfo.Width, formatInfo.Height);

        using (BeginStructure())
        {
            if (formatInfo.Failure == ImageFailure.None)
            {
                try
                {
                    XRect srcRect = new XRect(formatInfo.CropX, formatInfo.CropY, formatInfo.CropWidth, formatInfo.CropHeight);
                    using (var xImage = XImage.FromImageSource(formatInfo.ImageSource))
                        gfx.DrawImage(xImage, destRect, srcRect, XGraphicsUnit.Point); //Pixel.
                }
                catch (Exception ex) when (!IsUnrecoverable(ex))
                {
                    Debug.WriteLine(string.Format(AppResources.ImageNotReadable, image.Source, ex.Message));
                    formatInfo.Failure = ImageFailure.NotRead;
                    formatInfo.FailureException = ex;
                    RenderFailureImage(destRect);
                }
            }
            else
                RenderFailureImage(destRect);
        }

        using (Tagger.Artifact(gfx))
            RenderLine();
    }

    /// <summary>
    /// Opens the scope the image is drawn in: a figure when it has been described, an artifact when
    /// it has not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An undescribed figure is worse than no figure. It announces to a reader that something is
    /// there and then cannot say what, which leaves them knowing only that they have missed
    /// something. Marked as decoration instead, it is passed over silently — which is right for the
    /// rule above a letterhead and is at least honest for everything else.
    /// </para>
    /// <para>
    /// So the alternate text is not optional-with-a-default: supplying it makes the image content,
    /// and not supplying it makes the image furniture. Nothing here guesses at a description. What an
    /// image is for is a fact about the document rather than about the pixels, and a library inventing
    /// one would be writing something plausible into the single field a reader cannot check.
    /// </para>
    /// </remarks>
    IDisposable BeginStructure()
    {
        if (image.IsNull("AlternativeText") || string.IsNullOrEmpty(image.AlternativeText))
            return Tagger.Artifact(gfx);

        Tagger.EndList();

        var scope = Tagger.Block(gfx, image, PdfTag.Figure, out var element);
        if (element != null)
            element.AlternateText = image.AlternativeText;

        return scope;
    }

    /// <summary>
    ///   Whether an exception is one there is no carrying on from. Drawing a placeholder in place
    ///   of an image that would not load keeps a document with one bad image renderable, but an
    ///   OutOfMemoryException says nothing about the image and everything about the process it is
    ///   being rendered in. Swallowing it turns a memory problem into a page of grey boxes and
    ///   leaves the process to fail somewhere else, with nothing in the log pointing back here.
    /// </summary>
    static bool IsUnrecoverable(Exception ex)
    {
        // InsufficientMemoryException derives from OutOfMemoryException, so it is covered too.
        return ex is OutOfMemoryException;
    }

    void RenderFailureImage(XRect destRect)
    {
        gfx.DrawRectangle(XBrushes.LightGray, destRect);
        string failureString;
        ImageFormatInfo formatInfo = (ImageFormatInfo)RenderInfo.FormatInfo;

        documentRenderer?.OnImageFailed(image, formatInfo.Failure, formatInfo.FailureException);

        switch (formatInfo.Failure)
        {
            case ImageFailure.EmptySize:
                failureString = AppResources.DisplayEmptyImageSize;
                break;

            case ImageFailure.FileNotFound:
                failureString = AppResources.DisplayImageFileNotFound;
                break;

            case ImageFailure.InvalidType:
                failureString = AppResources.DisplayInvalidImageType;
                break;

            case ImageFailure.NotRead:
            default:
                failureString = AppResources.DisplayImageNotRead;
                break;
        }

        // Create stub font
        XFont font = FitWithin(failureString, destRect.Width);
        gfx.DrawString(failureString, font, XBrushes.Red, destRect, XStringFormats.Center);
    }

    /// <summary>
    /// The largest of the usual sizes at which the placeholder's message fits the box it belongs
    /// to, or 4 point - below the smallest of them - where none of them fits.
    /// </summary>
    /// <remarks>
    /// The size used to be a fixed 8 point whatever the box measured, so a placeholder narrower
    /// than its message - which is what an image of a tall aspect ratio gets - drew the message out
    /// through both sides and across whatever was beside it. A caller looking at that sees the
    /// library scribbling on their page, which is a poor way to be told an image would not load.
    /// </remarks>
    XFont FitWithin(string text, double width)
    {
        string family = GlobalFontSettings.FontResolver.DefaultFontName;

        foreach (double size in new[] { 8.0, 7.0, 6.0, 5.0 })
        {
            XFont candidate = new XFont(family, size);
            if (gfx.MeasureString(text, candidate).Width <= width)
                return candidate;
        }

        return new XFont(family, 4);
    }

    private void CalculateImageDimensions()
    {
        ImageFormatInfo formatInfo = (ImageFormatInfo)renderInfo.FormatInfo;

        if (formatInfo.Failure == ImageFailure.None)
        {
            XImage xImage = null;
            try
            {
                xImage = XImage.FromImageSource(formatInfo.ImageSource);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine(string.Format(AppResources.InvalidImageType, ex.Message));
                formatInfo.Failure = ImageFailure.InvalidType;
                formatInfo.FailureException = ex;
                // Measuring an image there is none of would throw on the very first line below,
                // and the NotRead that came of that used to bury the reason worked out here.
                SetFallbackDimensions(formatInfo);
                return;
            }

            try
            {
                XUnit usrWidth = image.Width.Point;
                XUnit usrHeight = image.Height.Point;
                bool usrWidthSet = !image.IsNull("Width");
                bool usrHeightSet = !image.IsNull("Height");

                XUnit resultWidth = usrWidth;
                XUnit resultHeight = usrHeight;

                double xPixels = xImage.PixelWidth;
                bool usrResolutionSet = !image.IsNull("Resolution");

                double horzRes = usrResolutionSet ? (double)image.Resolution : xImage.HorizontalResolution;
                XUnit inherentWidth = XUnit.FromInch(xPixels / horzRes);
                double yPixels = xImage.PixelHeight;
                double vertRes = usrResolutionSet ? (double)image.Resolution : xImage.VerticalResolution;
                XUnit inherentHeight = XUnit.FromInch(yPixels / vertRes);

                bool lockRatio = image.IsNull("LockAspectRatio") ? true : image.LockAspectRatio;

                double scaleHeight = image.ScaleHeight;
                double scaleWidth = image.ScaleWidth;
                bool scaleHeightSet = !image.IsNull("ScaleHeight");
                bool scaleWidthSet = !image.IsNull("ScaleWidth");

                if (lockRatio)
                {
                    if (usrWidthSet && usrHeightSet)
                    {
                        if (inherentHeight / usrHeight > inherentWidth / usrWidth)
                        {
                            usrWidthSet = false;
                        }
                        else
                        {
                            usrHeightSet = false;
                        }
                    }
                    if (usrWidthSet && !usrHeightSet)
                    {
                        resultHeight = inherentHeight / inherentWidth * usrWidth;
                    }
                    else if (usrHeightSet && !usrWidthSet)
                    {
                        resultWidth = inherentWidth / inherentHeight * usrHeight;
                    }
                    else if (!usrHeightSet && !usrWidthSet)
                    {
                        resultHeight = inherentHeight;
                        resultWidth = inherentWidth;
                    }

                    // With the ratio locked, a scale height wins over a scale width whenever both are set.
                    if (scaleHeightSet)
                    {
                        resultHeight = resultHeight * scaleHeight;
                        resultWidth = resultWidth * scaleHeight;
                    }
                    else if (scaleWidthSet)
                    {
                        resultHeight = resultHeight * scaleWidth;
                        resultWidth = resultWidth * scaleWidth;
                    }
                }
                else
                {
                    if (!usrHeightSet)
                        resultHeight = inherentHeight;

                    if (!usrWidthSet)
                        resultWidth = inherentWidth;

                    if (scaleHeightSet)
                        resultHeight = resultHeight * scaleHeight;
                    if (scaleWidthSet)
                        resultWidth = resultWidth * scaleWidth;
                }

                formatInfo.CropWidth = (int)xPixels;
                formatInfo.CropHeight = (int)yPixels;
                if (!image.IsNull("PictureFormat"))
                {
                    PictureFormat picFormat = image.PictureFormat;
                    //Cropping in pixels.
                    XUnit cropLeft = picFormat.CropLeft.Point;
                    XUnit cropRight = picFormat.CropRight.Point;
                    XUnit cropTop = picFormat.CropTop.Point;
                    XUnit cropBottom = picFormat.CropBottom.Point;
                    formatInfo.CropX = (int)(horzRes * cropLeft.Inch);
                    formatInfo.CropY = (int)(vertRes * cropTop.Inch);
                    formatInfo.CropWidth -= (int)(horzRes * ((XUnit)(cropLeft + cropRight)).Inch);
                    formatInfo.CropHeight -= (int)(vertRes * ((XUnit)(cropTop + cropBottom)).Inch);

                    //Scaled cropping of the height and width.
                    double xScale = resultWidth / inherentWidth;
                    double yScale = resultHeight / inherentHeight;

                    cropLeft = xScale * cropLeft;
                    cropRight = xScale * cropRight;
                    cropTop = yScale * cropTop;
                    cropBottom = yScale * cropBottom;

                    resultHeight = resultHeight - cropTop - cropBottom;
                    resultWidth = resultWidth - cropLeft - cropRight;
                }
                // Not "<= 0", which lets a NaN through: every comparison against NaN is false, so
                // a size that is not a number counted as a good one. An image reporting no pixels
                // divides zero by zero in the aspect-ratio arithmetic above and arrives here as
                // NaN, and what followed was an element of no known height - the page broke around
                // it, the next one came out blank, no placeholder was drawn and no failure was
                // reported, because this branch was never taken.
                if (!IsUsableSize(resultHeight) || !IsUsableSize(resultWidth))
                {
                    Debug.WriteLine(AppResources.EmptyImageSize);
                    // The field this used to be assigned to had already been copied into the
                    // format info by Format, so the placeholder this asks for was never drawn.
                    formatInfo.Failure = ImageFailure.EmptySize;
                }
                else
                {
                    formatInfo.Width = resultWidth;
                    formatInfo.Height = resultHeight;
                }
            }
            catch (Exception ex) when (!IsUnrecoverable(ex))
            {
                Debug.WriteLine(string.Format(AppResources.ImageNotReadable, image.Source.ToString(), ex.Message));
                formatInfo.Failure = ImageFailure.NotRead;
                formatInfo.FailureException = ex;
            }
            finally
            {
                if (xImage != null)
                    xImage.Dispose();
            }
        }
        if (formatInfo.Failure != ImageFailure.None)
            SetFallbackDimensions(formatInfo);
    }

    /// <summary>
    /// Whether a measured extent is one an image can actually be laid out at.
    /// </summary>
    /// <remarks>
    /// Written as "greater than zero" rather than "not less than or equal to zero" on purpose.
    /// The two are not the same for NaN, which is not greater than zero and not less than or equal
    /// to it either, and it is NaN that this exists to catch.
    /// </remarks>
    static bool IsUsableSize(XUnit size)
    {
        double points = size.Point;
        return points > 0 && !double.IsInfinity(points);
    }

    /// <summary>
    ///   Sizes the placeholder that stands in for an image that could not be drawn: what the
    ///   document asked for where it asked for anything, and a square inch or so where it did not.
    /// </summary>
    private void SetFallbackDimensions(ImageFormatInfo formatInfo)
    {
        // A size of nothing would hide the placeholder, which defeats the point of drawing one,
        // so anything the document does not give a positive size for falls back to an inch or so.
        formatInfo.Width = Positive(image.IsNull("Width") ? 0 : image.Width.Point);
        formatInfo.Height = Positive(image.IsNull("Height") ? 0 : image.Height.Point);

        static XUnit Positive(double points)
        {
            return points > 0 ? XUnit.FromPoint(points) : XUnit.FromCentimeter(2.5);
        }
    }

    Image image;
}
