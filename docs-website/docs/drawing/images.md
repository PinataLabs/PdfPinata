---
title: Images
description: Load raster images with XImage, then size, fit, crop and rotate them on a page, and find out why an image failed to load.
demos: [Images, ImageFailures]
---

`XImage` puts a raster image, such as a photograph, a logo or a scan, on a page. You load it once and
draw it with `XGraphics.DrawImage` as many times as you like. The core `PdfPinata` package cannot
decode images by itself. The backend you registered at startup does that work, through
`ImageSource.ImageSourceImpl` (see [Installation](../installation.md)). Loading an image before a
backend is registered throws an `InvalidOperationException`. Note that `ImageSource` is in the
namespace `PinataLayout.DocumentObjectModel.Shapes`, although it ships in the `PdfPinata` package.

## Load an image

`XImage.FromFile` reads a file. `XImage.FromStream` takes a function that opens a stream, not the
stream itself, because the library may open it more than once:

```csharp demo=Images snippet=load
```

`Assets.Open` is a helper of the demo app. In your own code, pass something like
`() => File.OpenRead(path)`.

To choose the JPEG quality, or to supply an image you have already decoded, build the image source
yourself and pass it to `XImage.FromImageSource`:

```csharp
XImage photo = XImage.FromImageSource(ImageSource.FromFile("photo.jpg", quality: 90));
```

`SkiaImageSource.FromSkiaBitmap` and `ImageSharpImageSource<TPixel>.FromImageSharpImage` wrap an
image that is already in memory, so it is not encoded and decoded again.

If you pass `XImage.FromFile` a PDF file, you get an `XPdfForm`, which draws a page of that PDF. See
[Forms, stamps and imposition](./forms-stamps-and-imposition.md).

## Formats and how they are stored

The backend decides which files it can read, because it is the backend's image library (SkiaSharp or
ImageSharp) that decodes them. PNG and JPEG work with both. For any other format, check the
documentation of the image library behind the backend you chose.

The backend decodes every image to pixels. What PdfPinata writes to the PDF then depends only on
whether the source was a PNG:

- **A PNG is stored without loss**, with its alpha channel as a soft mask. This is true of palette
  PNGs and truecolour PNGs alike.
- **Every other format is re-encoded as JPEG**, at quality 75 unless you choose otherwise. This
  includes JPEG files: their bytes are not copied into the PDF as they are. Any transparency in a GIF
  or WebP file is lost.

Every image is written in RGB. A CMYK or greyscale JPEG is converted.

## Size and place an image

`DrawImage(image, x, y)` draws the image at its natural size. `DrawImage(image, x, y, width, height)`
and the `XRect` overload stretch it to the rectangle you give.

```csharp demo=Images snippet=sizes
```

`PixelWidth` and `PixelHeight` are the image's size in pixels. `PointWidth` and `PointHeight` are its
natural size on the page, in points. PdfPinata always treats an image as 96 pixels to the inch, and
ignores any resolution stored in the file.

Drawing an image smaller does not resample it. The file keeps every pixel. To make a PDF smaller,
scale the image down before you load it.

### Fit an image in a box

There is no helper that fits an image to a box. To fit the whole image and keep its proportions, use
the smaller of the two scale factors, then centre the result:

```csharp demo=Images snippet=fit
```

### Fill a box and crop the rest

To cover the box completely, use the larger of the two scale factors. The image then overflows the
box on two sides. Clip to the box to cut the overflow off:

```csharp
XRect box = new XRect(300, 92, 200, 200);
double scale = Math.Max(box.Width / image.PointWidth, box.Height / image.PointHeight);
double width = image.PointWidth * scale;
double height = image.PointHeight * scale;

XGraphicsState state = gfx.Save();
gfx.IntersectClip(box);
gfx.DrawImage(image, box.X + (box.Width - width) / 2, box.Y + (box.Height - height) / 2,
    width, height);
gfx.Restore(state);
```

The clip hides the overflow, but the whole image is still in the file.

:::warning
`XGraphics` has a `DrawImage` overload that takes a source rectangle as well as a destination
rectangle. It ignores the source rectangle and draws the whole image into the destination.
Crop with a clip, as above.
:::

## Rotate an image

`RotateAtTransform` turns the page's coordinates about a point. The image is then drawn square onto
the turned coordinates. Put the turn inside `Save` and `Restore`, or it applies to everything drawn
after it:

```csharp demo=Images snippet=rotate
```

## Smoothing when an image is enlarged

`XImage.Interpolate` asks the PDF reader to smooth an image that is drawn larger than its own pixels.
It is `true` by default. Set it to `false` for images that must stay sharp-edged, such as pixel art
or a scanned barcode:

```csharp demo=Images snippet=interpolate
```

This is only a request that PdfPinata writes into the file. The reader decides what to do with it,
and some readers ignore it.

## When an image cannot be read

`XImage.FromFile` and `XImage.FromStream` throw when the backend cannot decode the image. The
exception comes from the backend and says what went wrong.

PinataLayout behaves differently. A document with an unreadable image still renders: PinataLayout
draws a grey placeholder where the image would be, and carries on. To find out what failed, handle
the `ImageFailed` event of the `DocumentRenderer`:

```csharp demo=ImageFailures snippet=report-failures
```

The event arguments carry:

- `Image`: the PinataLayout image that failed. Its `Source` is the `IImageSource` it was given.
- `Failure`: an `ImageFailure` value. `FileNotFound` means the file does not exist. `InvalidType`
  means the image could not be decoded at all. `NotRead` means it could not be measured or drawn.
  `EmptySize` means its size has no area.
- `Exception`: the exception that was thrown, or `null` for `EmptySize`, where nothing throws.

Attach the handler before you call `RenderDocument`. Reading `PdfDocumentRenderer.DocumentRenderer`
creates the renderer, so attaching to it first is safe.

### Supply images from anywhere

`ImageSource.IImageSource` has six members. Implement it to supply images from a database, a web
response or a bitmap you generate:

```csharp demo=ImageFailures snippet=image-source-members
```

`GetPixels` returns a `PixelBuffer`: the pixels packed row by row from the top, four bytes each in
blue, green, red, alpha order, with alpha not premultiplied. `Transparent` decides whether the image
is stored losslessly with an alpha channel (`true`) or as a JPEG from `SaveAsJpeg` (`false`). In
PinataLayout, pass your source to `AddImage`.

## Things to know

- **A backend must be registered first.** Without `ImageSource.ImageSourceImpl`, every image load
  throws.
- **Load once, draw many times.** Each `XImage` object is stored in the file once, however often you
  draw it. Loading the same file twice gives two objects, and two copies in the file.
- **Only PNGs keep transparency and full quality.** Other formats become JPEGs at quality 75.
- **Natural size assumes 96 dpi.** A 300 dpi scan drawn with `DrawImage(image, x, y)` comes out more
  than three times its printed size. Give it a width and height.
- **The source-rectangle overload does not crop.** Use `IntersectClip`.
- **PinataLayout reports; it does not throw.** Without an `ImageFailed` handler, the reason for a grey
  box is lost. A failure found while measuring gives a placeholder of the size the document asked for,
  or 2.5 cm square. A failure found while drawing keeps the size the image would have had, so the
  page layout does not change.
- **Running out of memory is not caught.** An `OutOfMemoryException` while loading an image in
  PinataLayout is thrown, not turned into a placeholder.

## See it in action

[The Images demo](../demos.mdx#images) places one photograph at natural size, scaled, stretched,
fitted and rotated, and draws two transparent PNGs. [The ImageFailures
demo](../demos.mdx#imagefailures) renders four images that fail in four different ways, and lists what
the `ImageFailed` handler received for each.

<details>
<summary>The full Images demo</summary>

```csharp demo=Images
```

</details>

<details>
<summary>The full ImageFailures demo</summary>

```csharp demo=ImageFailures
```

</details>
