using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   <see cref="OpenTypeFontMetadata"/> reads a face's family and style from bytes as well as from
///   a path, for a resolver that holds its fonts in memory and has no file to name. The two must
///   never disagree, so every test here reads the same face both ways and compares them.
/// </summary>
public class OpenTypeFontMetadataBytesTests
{
    private static readonly string[] CollectionFaces =
    [
        "LiberationSans-Regular.ttf",
        "LiberationSans-Bold.ttf",
        "LiberationSans-Italic.ttf",
        "LiberationSans-BoldItalic.ttf"
    ];

    private static string Asset(string name) => PathHelper.GetInstance().GetAssetPath(Path.Combine("Fonts", name));

    /// <summary>
    ///   A TrueType face and a face with PostScript (CFF) outlines, which share the tables the
    ///   reader looks at but nothing else.
    /// </summary>
    [Test]
    [Arguments("LiberationSans-Regular.ttf", "Liberation Sans", XFontStyle.Regular)]
    [Arguments("LiberationSans-BoldItalic.ttf", "Liberation Sans", XFontStyle.BoldItalic)]
    [Arguments("SourceCodePro-Regular.otf", "Source Code Pro", XFontStyle.Regular)]
    public void ASingleFaceReadsTheSameFromBytesAsFromItsFile(string file, string family, XFontStyle style)
    {
        var path = Asset(file);
        var bytes = File.ReadAllBytes(path);

        var fromBytes = OpenTypeFontMetadata.Read(bytes);

        fromBytes.Should().Be(OpenTypeFontMetadata.Read(path));
        OpenTypeFontMetadata.Read(bytes, 0).Should().Be(OpenTypeFontMetadata.Read(path, 0));
        OpenTypeFontMetadata.ReadAll(bytes, 1).Should().Equal(OpenTypeFontMetadata.ReadAll(path, 1));

        fromBytes.FamilyName.Should().Be(family);
        fromBytes.Style.Should().Be(style);
    }

    [Test]
    public void EachFaceOfACollectionReadsTheSameFromBytesAsFromTheFile()
    {
        var bytes = TrueTypeCollectionBuilder.Build([..CollectionFaces.Select(name => File.ReadAllBytes(Asset(name)))]);

        WithTemporaryFile(bytes, path =>
        {
            for (var face = 0; face < CollectionFaces.Length; face++)
                OpenTypeFontMetadata.Read(bytes, face).Should().Be(OpenTypeFontMetadata.Read(path, face),
                    "face {0} is the same face whichever way it is read", face);

            OpenTypeFontMetadata.Read(bytes).Should().Be(OpenTypeFontMetadata.Read(path));

            var all = OpenTypeFontMetadata.ReadAll(bytes, CollectionFaces.Length);

            all.Should().Equal(OpenTypeFontMetadata.ReadAll(path, CollectionFaces.Length));
            all.Select(metadata => metadata.Style).Should().Equal(
                XFontStyle.Regular, XFontStyle.Bold, XFontStyle.Italic, XFontStyle.BoldItalic);
            all.Should().AllSatisfy(metadata => metadata.FamilyName.Should().Be("Liberation Sans"));
        });
    }

    [Test]
    public void ACollectionReadFromBytesWithNoFaceIndexIsItsFirstFace()
    {
        var bytes = TrueTypeCollectionBuilder.Build(
            File.ReadAllBytes(Asset("LiberationSans-Bold.ttf")),
            File.ReadAllBytes(Asset("LiberationSans-Regular.ttf")));

        OpenTypeFontMetadata.Read(bytes).Style.Should().Be(XFontStyle.Bold);
    }

    [Test]
    public void BytesRefuseAFaceTheyDoNotHoldAsTheFileDoes()
    {
        var bytes = File.ReadAllBytes(Asset("LiberationSans-Regular.ttf"));

        var reading = () => OpenTypeFontMetadata.Read(bytes, 1);
        var readingAll = () => OpenTypeFontMetadata.ReadAll(bytes, 2);

        reading.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*face 1*");
        readingAll.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void NoBytesAtAllAreRefusedByName()
    {
        var reading = () => OpenTypeFontMetadata.Read((byte[])null!);
        var readingFace = () => OpenTypeFontMetadata.Read((byte[])null!, 0);
        var readingAll = () => OpenTypeFontMetadata.ReadAll((byte[])null!, 1);

        reading.Should().Throw<ArgumentNullException>().WithParameterName("data");
        readingFace.Should().Throw<ArgumentNullException>().WithParameterName("data");
        readingAll.Should().Throw<ArgumentNullException>().WithParameterName("data");
    }

    private static void WithTemporaryFile(byte[] data, Action<string> body)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ttc");
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
}
