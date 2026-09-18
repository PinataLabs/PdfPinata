using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XBitmapImage.CreateBitmap"/> threw its size away, and <see cref="XBitmapSource"/>
///   answered <c>PixelWidth</c> and <c>PixelHeight</c> by reading themselves - so asking a bitmap
///   how big it was overflowed the stack and took the process with it.
/// </summary>
public class XBitmapImageTests
{
    [Fact]
    public void ABitmapIsThePixelSizeItWasCreatedAt()
    {
        var bitmap = XBitmapImage.CreateBitmap(12, 34);

        bitmap.PixelWidth.Should().Be(12);
        bitmap.PixelHeight.Should().Be(34);
    }
}
