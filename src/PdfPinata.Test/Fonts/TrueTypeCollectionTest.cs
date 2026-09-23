using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.Fonts;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
/// Collections used to throw on read - "TrueType collection fonts are not yet supported" - which
/// put most CJK system faces on Windows and macOS out of reach. They are now taken apart in the
/// resolver, so that nothing below it ever meets one.
/// </summary>
/// <remarks>
/// The collection is built here from the Liberation faces shipped with the tests rather than
/// taken off the machine, so the assertions hold on a box with no collection installed. The
/// extracted faces are checked with SixLabors.Fonts rather than with PdfPinata's own parser:
/// an independent reader accepting the output is the claim worth making.
/// </remarks>
public class TrueTypeCollectionTest
{
    private static readonly string[] FaceFiles =
    [
        "LiberationSans-Regular.ttf",
        "LiberationSans-Bold.ttf",
        "LiberationSans-Italic.ttf",
        "LiberationSans-BoldItalic.ttf"
    ];

    private sealed class SkiaProbe : SkiaFontResolver
    {
        public FontMetadata Read(string path, int faceIndex) => ReadFontMetadata(path, faceIndex);
    }

    private sealed class ImageSharpProbe : ImageSharpFontResolver
    {
        public FontMetadata Read(string path, int faceIndex) => ReadFontMetadata(path, faceIndex);
    }

    /// <summary>
    /// Reads a font's tables into tag-to-bytes, straight off the directory.
    /// </summary>
    private static Dictionary<string, byte[]> Tables(byte[] font)
    {
        var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var count = (font[4] << 8) | font[5];

        for (var i = 0; i < count; i++)
        {
            var record = 12 + i * 16;
            var tag = Encoding.ASCII.GetString(font, record, 4);
            var offset = (int)ReadU32(font, record + 8);
            var length = (int)ReadU32(font, record + 12);

            var bytes = new byte[length];
            Buffer.BlockCopy(font, offset, bytes, 0, length);
            tables.Add(tag, bytes);
        }

        return tables;
    }

    private static uint ReadU32(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                                          | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }

    [Fact]
    public void ACollectionReportsTheNumberOfFacesPackedIntoIt()
    {
        var collection = BuildCollection();

        TrueTypeCollection.IsCollection(collection).Should().BeTrue();
        TrueTypeCollection.FaceCount(collection).Should().Be(FaceFiles.Length);
    }

    [Fact]
    public void ASingleFontIsNotACollectionAndHoldsOneFace()
    {
        var font = File.ReadAllBytes(AssetPath(FaceFiles[0]));

        TrueTypeCollection.IsCollection(font).Should().BeFalse();
        TrueTypeCollection.FaceCount(font).Should().Be(1);

        // Face 0 of a single font is the font, handed back untouched.
        TrueTypeCollection.ExtractFace(font, 0).Should().BeSameAs(font);
    }

    [Fact]
    public void EachExtractedFaceIsAStandaloneFontCarryingTheIdentityItWasPackedWith()
    {
        var collection = BuildCollection();

        for (var face = 0; face < FaceFiles.Length; face++)
        {
            var extracted = TrueTypeCollection.ExtractFace(collection, face);

            TrueTypeCollection.IsCollection(extracted).Should()
                .BeFalse("face {0} must come out as a plain font, not a collection", face);

            var expected = FontDescription.LoadDescription(AssetPath(FaceFiles[face]));
            var actual = Describe(extracted);

            actual.FontFamilyInvariantCulture.Should().Be(expected.FontFamilyInvariantCulture);
            actual.Style.Should().Be(expected.Style);
            actual.FontNameInvariantCulture.Should().Be(expected.FontNameInvariantCulture);
        }
    }

    /// <summary>
    /// Reading the name back only proves the 'name' table landed where the directory says it
    /// did. Every table has to, or a font renders from whatever bytes the wrong offset points
    /// at, which no metadata reader would notice.
    /// </summary>
    [Fact]
    public void EveryTableOfAnExtractedFaceHoldsTheBytesItHeldInTheSourceFont()
    {
        var collection = BuildCollection();

        for (var face = 0; face < FaceFiles.Length; face++)
        {
            var expected = Tables(File.ReadAllBytes(AssetPath(FaceFiles[face])));
            var actual = Tables(TrueTypeCollection.ExtractFace(collection, face));

            expected.Should().NotBeEmpty("otherwise the comparison below asserts nothing");
            actual.Keys.Should().BeEquivalentTo(expected.Keys,
                "face {0} must carry over every table it had", face);

            foreach (var table in expected)
                actual[table.Key].Should().Equal(table.Value,
                    "table '{0}' of face {1} must survive extraction byte for byte", table.Key, face);
        }
    }

    [Fact]
    public void BothBackendsAgreeOnTheFacesOfACollection()
    {
        WithCollectionFile(path =>
        {
            var skia = new SkiaProbe();
            var imageSharp = new ImageSharpProbe();

            for (var face = 0; face < FaceFiles.Length; face++)
            {
                var fromSkia = skia.Read(path, face);
                var fromImageSharp = imageSharp.Read(path, face);

                fromSkia.FamilyName.Should().Be(fromImageSharp.FamilyName, "at face {0}", face);
                fromSkia.Style.Should().Be(fromImageSharp.Style, "at face {0}", face);
            }
        });
    }

    [Fact]
    public void ExtractingRejectsAFaceTheCollectionDoesNotHold()
    {
        var collection = BuildCollection();

        Action beyondTheEnd = () => TrueTypeCollection.ExtractFace(collection, FaceFiles.Length);
        beyondTheEnd.Should().Throw<ArgumentOutOfRangeException>();

        Action negative = () => TrueTypeCollection.ExtractFace(collection, -1);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DiscoveryFindsEveryFaceOfACollectionAndServesEachOneStandalone()
    {
        WithCollectionFile(path =>
        {
            var resolver = new SkiaProbe();
            resolver.SetupFontsFiles([path]);

            var fileName = Path.GetFileName(path);

            // Every face of the collection was seen, not just the first.
            var faces = new[]
            {
                new { Bold = false, Italic = false, Style = XFontStyle.Regular },
                new { Bold = true, Italic = false, Style = XFontStyle.Bold },
                new { Bold = false, Italic = true, Style = XFontStyle.Italic },
                new { Bold = true, Italic = true, Style = XFontStyle.BoldItalic }
            };

            foreach (var face in faces)
            {
                var info = resolver.ResolveTypeface("Liberation Sans", face.Bold, face.Italic);

                info.Should().NotBeNull();
                info.FaceName.Should().StartWith(fileName + "#",
                    "a face of a collection is named by the file it came from and its index");

                var bytes = resolver.GetFont(info.FaceName);

                TrueTypeCollection.IsCollection(bytes).Should()
                    .BeFalse("the resolver has to take the collection apart, not hand it on whole");

                var description = Describe(bytes);
                description.FontFamilyInvariantCulture.Should().Be("Liberation Sans");
                description.Style.Should().Be(ToSixLabors(face.Style));
            }
        });
    }

    [Fact]
    public void MetadataCanBeReadForEveryFaceOfACollection()
    {
        WithCollectionFile(path =>
        {
            var probe = new SkiaProbe();

            var styles = Enumerable.Range(0, FaceFiles.Length)
                .Select(face => probe.Read(path, face))
                .ToArray();

            styles.Should().OnlyContain(m => m.FamilyName == "Liberation Sans");
            styles.Select(m => m.Style).Should().BeEquivalentTo([
                XFontStyle.Regular, XFontStyle.Bold, XFontStyle.Italic, XFontStyle.BoldItalic
            ]);
        });
    }

    /// <summary>
    /// A collection is free to declare a face directory that is not in the file. Reading one has
    /// to say so rather than walk off the end of the array, which is what indexing the offset
    /// table without checking it first did.
    /// </summary>
    [Fact]
    public void ReadingMetadataRejectsACollectionPointingOutsideTheFile()
    {
        var collection = BuildCollection();

        // The offset of face 0's table directory sits at 12, just past the collection header.
        WriteU32(collection, 12, (uint)collection.Length + 1024);

        WithFontFile(collection, path =>
        {
            Action read = () => new SkiaProbe().Read(path, 0);

            read.Should().Throw<InvalidOperationException>(
                "a face outside the file is malformed input, not an indexing accident");
        });
    }

    private static void WriteU32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }

    private static void WithCollectionFile(Action<string> body)
    {
        WithFontFile(BuildCollection(), body);
    }

    private static void WithFontFile(byte[] data, Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "PdfPinata-" + Guid.NewGuid().ToString("N") + ".ttc");

        File.WriteAllBytes(path, data);
        try
        {
            body(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] BuildCollection()
    {
        return TrueTypeCollectionBuilder.Build(
            [..FaceFiles.Select(name => File.ReadAllBytes(AssetPath(name)))]);
    }

    private static FontDescription Describe(byte[] font)
    {
        using var stream = new MemoryStream(font);
        return FontDescription.LoadDescription(stream);
    }

    private static FontStyle ToSixLabors(XFontStyle style)
    {
        switch (style)
        {
            case XFontStyle.Bold: return FontStyle.Bold;
            case XFontStyle.Italic: return FontStyle.Italic;
            case XFontStyle.BoldItalic: return FontStyle.BoldItalic;
            default: return FontStyle.Regular;
        }
    }

    private static string AssetPath(string fileName)
    {
        return PathHelper.GetInstance().GetAssetPath("Fonts", fileName);
    }
}
