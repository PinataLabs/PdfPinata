using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   <see cref="FontResolverBase"/> is what both backends' font resolvers are built on: it turns a
///   list of font files into a family lookup and answers a family name and a style with a face.
///   The parts worth pinning are the ones a backend does not override — what happens to a file it
///   cannot read, which face a family answers when the one asked for is not there, and what it
///   does when the family is not there at all.
///   <para>
///   Everything here goes through a resolver of this test's own rather than through
///   <see cref="GlobalFontSettings.FontResolver"/>, which cannot be swapped once a font has been
///   used and is pinned for the whole assembly by <c>PinnedFontResolver</c>.
///   </para>
/// </summary>
public class FontResolverBaseTests
{
    /// <summary>
    ///   A resolver that reads a face's family and style out of its file name rather than out of
    ///   the file, which is all the base class needs of it, and records what it was asked to read
    ///   so a test can see how many times a file was opened.
    /// </summary>
    private sealed class NameBasedResolver : FontResolverBase
    {
        public readonly List<string> Read = [];
        public string Unreadable { get; set; }

        protected override FontMetadata ReadFontMetadata(string fontFilePath)
        {
            Read.Add(fontFilePath);

            var name = Path.GetFileNameWithoutExtension(fontFilePath);
            if (Unreadable != null && name == Unreadable)
                throw new InvalidOperationException("This face cannot be read.");

            var dash = name.IndexOf('-');
            var family = dash < 0 ? name : name[..dash];
            var suffix = dash < 0 ? "Regular" : name[(dash + 1)..];

            var style = suffix switch
            {
                "Bold" => XFontStyle.Bold,
                "Italic" => XFontStyle.Italic,
                "BoldItalic" => XFontStyle.BoldItalic,
                _ => XFontStyle.Regular
            };

            return new FontMetadata(family, style);
        }
    }

    private static string Asset(string name) => PathHelper.GetInstance().GetAssetPath(Path.Combine("Fonts", name));

    private static string[] TheLiberationFamily() =>
    [
        Asset("LiberationSans-Regular.ttf"),
        Asset("LiberationSans-Bold.ttf"),
        Asset("LiberationSans-Italic.ttf"),
        Asset("LiberationSans-BoldItalic.ttf")
    ];

    private static NameBasedResolver AResolverOver(params string[] files)
    {
        var resolver = new NameBasedResolver();
        resolver.SetupFontsFiles(files);
        return resolver;
    }

    // ----- what it makes of the files it is given ---------------------------------------------------

    [Fact]
    public void AFamilyResolvesToTheFaceOfTheStyleAskedFor()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        resolver.ResolveTypeface("LiberationSans", false, false)!.FaceName
            .Should().Be("LiberationSans-Regular.ttf");
        resolver.ResolveTypeface("LiberationSans", true, false)!.FaceName
            .Should().Be("LiberationSans-Bold.ttf");
        resolver.ResolveTypeface("LiberationSans", false, true)!.FaceName
            .Should().Be("LiberationSans-Italic.ttf");
        resolver.ResolveTypeface("LiberationSans", true, true)!.FaceName
            .Should().Be("LiberationSans-BoldItalic.ttf");
    }

    [Fact]
    public void AFamilyNameIsMatchedWhateverItsCase()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        resolver.ResolveTypeface("liberationsans", false, false).Should().NotBeNull();
        resolver.ResolveTypeface("LIBERATIONSANS", false, false).Should().NotBeNull();
    }

    /// <summary>
    ///   A family that ships only a regular face still answers a request for bold, with the regular
    ///   file and an instruction to the renderer to stroke it — which is what keeps a document
    ///   renderable rather than silently unstyled.
    /// </summary>
    [Fact]
    public void AMissingWeightIsAnsweredWithTheFaceThereIsAndASimulation()
    {
        var resolver = AResolverOver(Asset("LiberationSans-Regular.ttf"));

        var bold = resolver.ResolveTypeface("LiberationSans", true, false);

        bold!.FaceName.Should().Be("LiberationSans-Regular.ttf");
        bold.StyleSimulations.Should().Be(XStyleSimulations.BoldSimulation);

        var italic = resolver.ResolveTypeface("LiberationSans", false, true);
        italic!.StyleSimulations.Should().Be(XStyleSimulations.ItalicSimulation);

        var both = resolver.ResolveTypeface("LiberationSans", true, true);
        both!.StyleSimulations.Should().Be(XStyleSimulations.BoldItalicSimulation);
    }

    [Fact]
    public void AFamilyWithOnlyABoldFaceAnswersRegularWithIt()
    {
        var resolver = AResolverOver(Asset("LiberationSans-Bold.ttf"));

        var regular = resolver.ResolveTypeface("LiberationSans", false, false);

        regular!.FaceName.Should().Be("LiberationSans-Bold.ttf");
        regular.StyleSimulations.Should().Be(XStyleSimulations.None,
            "the file is already bold, so stroking it would make it bolder still");
    }

    // ----- what it does with a family it has never heard of -------------------------------------------

    [Fact]
    public void AnUnknownFamilyFallsBackToWhateverIsInstalled()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        resolver.ResolveTypeface("NoSuchFamily", false, false).Should().NotBeNull();
    }

    [Fact]
    public void AnUnknownFamilyAnswersNothingWhenTheResolverWasToldTo()
    {
        var resolver = AResolverOver(TheLiberationFamily());
        resolver.NullIfFontNotFound = true;

        resolver.ResolveTypeface("NoSuchFamily", false, false).Should().BeNull();
    }

    [Fact]
    public void AResolverOverNoFilesAtAllSaysSoRatherThanAnsweringNothing()
    {
        var resolver = AResolverOver();

        var resolving = () => resolver.ResolveTypeface("LiberationSans", false, false);

        resolving.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void TheDefaultFamilyIsArialUnlessABackendSaysOtherwise()
    {
        new NameBasedResolver().DefaultFontName.Should().Be("Arial");
    }

    // ----- the font bytes -----------------------------------------------------------------------------

    [Fact]
    public void AFaceNameReadsBackTheBytesOfTheFileItWasDiscoveredIn()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        var bytes = resolver.GetFont("LiberationSans-Regular.ttf");

        bytes.Should().NotBeNull();
        bytes.LongLength.Should().Be(new FileInfo(Asset("LiberationSans-Regular.ttf")).Length);
    }

    [Fact]
    public void AFaceNameThatWasNeverDiscoveredSaysSo()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        var reading = () => resolver.GetFont("NoSuchFace.ttf");

        reading.Should().Throw<FileNotFoundException>();
    }

    // ----- the files it cannot use --------------------------------------------------------------------

    [Fact]
    public void AFileThatIsNotThereIsLoggedAndSkippedRatherThanFatal()
    {
        var resolver = new NameBasedResolver();

        resolver.SetupFontsFiles([Asset("LiberationSans-Regular.ttf"), Asset("NotThere-Regular.ttf")]);

        resolver.NullIfFontNotFound = true;
        resolver.ResolveTypeface("LiberationSans", false, false).Should().NotBeNull();
        resolver.ResolveTypeface("NotThere", false, false).Should().BeNull();
    }

    [Fact]
    public void AFileWhoseMetadataCannotBeReadIsLoggedAndSkipped()
    {
        var resolver = new NameBasedResolver { Unreadable = "LiberationSans-Bold" };

        resolver.SetupFontsFiles(TheLiberationFamily());

        resolver.ResolveTypeface("LiberationSans", true, false)!.FaceName
            .Should().NotBe("LiberationSans-Bold.ttf", "the face that could not be read is not there to answer");
    }

    [Fact]
    public void TheSameFileNameFoundTwiceIsReadOnce()
    {
        var resolver = new NameBasedResolver();

        resolver.SetupFontsFiles([Asset("LiberationSans-Regular.ttf"), Asset("LiberationSans-Regular.ttf")]);

        resolver.Read.Should().HaveCount(1, "the first font directory a name is found in wins");
    }

    [Fact]
    public void SettingUpAgainReplacesWhatWasDiscoveredBefore()
    {
        var resolver = AResolverOver(TheLiberationFamily());

        resolver.SetupFontsFiles([Asset("SourceCodePro-Regular.otf")]);

        resolver.NullIfFontNotFound = true;
        resolver.ResolveTypeface("LiberationSans", false, false).Should().BeNull();
        resolver.ResolveTypeface("SourceCodePro", false, false).Should().NotBeNull();
    }

    /// <summary>
    ///   A resolver that reads single-font files only says so when asked to describe a face of a
    ///   collection. That is the base class's own answer, which a backend able to read a collection
    ///   overrides.
    /// </summary>
    [Fact]
    public void AResolverThatReadsSingleFontFilesRefusesToDescribeAFaceOfACollection()
    {
        var resolver = new DescribesOneFaceAtATime();

        var describing = () => resolver.DescribeFace(Asset("LiberationSans-Regular.ttf"), 2);

        describing.Should().Throw<NotSupportedException>().WithMessage("*face 2*");
    }

    [Fact]
    public void ACollectionIsDescribedFaceByFaceWhenNothingOverridesIt()
    {
        var resolver = new DescribesOneFaceAtATime();

        var describing = () => resolver.DescribeCollection(Asset("LiberationSans-Regular.ttf"), 3);

        describing.Should().Throw<NotSupportedException>(
            "the default reads a collection one face at a time, and the first face is already refused");
    }

    [Fact]
    public void ACollectionOfOneIsDescribedAsThatOneFace()
    {
        var resolver = new DescribesOneFaceAtATime();

        var metadata = resolver.DescribeCollection(Asset("LiberationSans-Regular.ttf"), 0);

        metadata.Should().BeEmpty();
    }

    /// <summary>Exposes the two protected metadata readers so a test can call them directly.</summary>
    private sealed class DescribesOneFaceAtATime : FontResolverBase
    {
        protected override FontMetadata ReadFontMetadata(string fontFilePath)
        {
            return new FontMetadata("Whatever", XFontStyle.Regular);
        }

        public FontMetadata DescribeFace(string path, int faceIndex) => ReadFontMetadata(path, faceIndex);

        public FontMetadata[] DescribeCollection(string path, int faceCount) =>
            ReadCollectionMetadata(path, faceCount);
    }
}
