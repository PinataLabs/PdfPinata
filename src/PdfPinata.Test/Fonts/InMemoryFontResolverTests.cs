using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   <see cref="InMemoryFontResolver"/> serves fonts from bytes the app hands it, which is the only
///   source of fonts a browser has. Every test reads a face back out of what the resolver answers -
///   the bytes <see cref="InMemoryFontResolver.GetFont"/> hands over for the face name it chose -
///   rather than out of the face name, which is the resolver's own business.
///   <para>
///   Each test builds a resolver of its own and calls it directly, because
///   <see cref="GlobalFontSettings.FontResolver"/> is pinned for the whole assembly by
///   <c>PinnedFontResolver</c> and cannot be swapped once a font has been used.
///   </para>
/// </summary>
public class InMemoryFontResolverTests
{
    private static byte[] Font(string name) => File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", name));

    private static InMemoryFontResolver AResolverWith(params string[] fonts)
    {
        var resolver = new InMemoryFontResolver();
        foreach (var font in fonts)
            resolver.AddFont(Font(font));
        return resolver;
    }

    private static InMemoryFontResolver TheLiberationFamily() => AResolverWith(
        "LiberationSans-Regular.ttf",
        "LiberationSans-Bold.ttf",
        "LiberationSans-Italic.ttf",
        "LiberationSans-BoldItalic.ttf");

    /// <summary>What the face a resolver answered says it is, read out of the bytes it serves for it.</summary>
    private static FontMetadata Served(InMemoryFontResolver resolver, FontResolverInfo info)
    {
        info.Should().NotBeNull();
        return OpenTypeFontMetadata.Read(resolver.GetFont(info.FaceName));
    }

    // ----- choosing a face ------------------------------------------------------------------------------

    [Test]
    public void EachStyleResolvesToTheFaceOfThatStyle()
    {
        var resolver = TheLiberationFamily();

        foreach (var (bold, italic, style) in new[]
                 {
                     (false, false, XFontStyle.Regular),
                     (true, false, XFontStyle.Bold),
                     (false, true, XFontStyle.Italic),
                     (true, true, XFontStyle.BoldItalic)
                 })
        {
            var info = resolver.ResolveTypeface("Liberation Sans", bold, italic);

            var served = Served(resolver, info);
            served.FamilyName.Should().Be("Liberation Sans");
            served.Style.Should().Be(style);
            info.StyleSimulations.Should().Be(XStyleSimulations.None, "the family ships a face for every style");
        }
    }

    /// <summary>
    ///   A CFF face cannot be subsetted and is embedded whole, but it is matched exactly as a
    ///   TrueType face is: Source Code Pro ships a regular face alone, so bold is drawn by stroking it.
    /// </summary>
    [Test]
    public void AFamilyWithOnlyARegularFaceAnswersBoldWithItAndABoldSimulation()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf");

        var info = resolver.ResolveTypeface("Source Code Pro", true, false);

        Served(resolver, info).Style.Should().Be(XFontStyle.Regular);
        info.StyleSimulations.Should().Be(XStyleSimulations.BoldSimulation);
    }

    [Test]
    public void ASimulatedBoldIsDrawnTheWayTheResolverIsTold()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf");
        resolver.BoldSimulation = XBoldSimulation.WordCompatible;

        resolver.ResolveTypeface("Source Code Pro", true, false).BoldSimulation
            .Should().Be(XBoldSimulation.WordCompatible);
    }

    [Test]
    public void AFamilyNameIsMatchedWhateverItsCase()
    {
        var resolver = TheLiberationFamily();
        resolver.NullIfFontNotFound = true;

        Served(resolver, resolver.ResolveTypeface("LIBERATION SANS", true, false)).Style.Should().Be(XFontStyle.Bold);
    }

    [Test]
    public void ACollectionAddsEveryFaceItHolds()
    {
        var collection = TrueTypeCollectionBuilder.Build(
        [
            ..new[] { "LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf", "LiberationSans-Italic.ttf", "LiberationSans-BoldItalic.ttf" }
                .Select(Font)
        ]);
        var resolver = new InMemoryFontResolver();

        resolver.AddFont(collection);

        foreach (var (bold, italic, style) in new[]
                 {
                     (false, false, XFontStyle.Regular),
                     (true, false, XFontStyle.Bold),
                     (false, true, XFontStyle.Italic),
                     (true, true, XFontStyle.BoldItalic)
                 })
        {
            var info = resolver.ResolveTypeface("Liberation Sans", bold, italic);

            Served(resolver, info).Style.Should().Be(style);
            info.StyleSimulations.Should().Be(XStyleSimulations.None);
            TrueTypeCollection.IsCollection(resolver.GetFont(info.FaceName))
                .Should().BeFalse("nothing below the resolver understands a collection");
        }
    }

    /// <summary>
    ///   What <see cref="InMemoryFontResolver.GetFont"/> hands over is what the library parses, so
    ///   it is parsed here by the same class - internal, and so reached by reflection.
    /// </summary>
    [Test]
    [Arguments("LiberationSans-Bold.ttf", "Liberation Sans", true)]
    [Arguments("SourceCodePro-Regular.otf", "Source Code Pro", false)]
    public void TheBytesServedParseAsAFontFace(string file, string family, bool bold)
    {
        var resolver = AResolverWith(file);
        var faceName = resolver.ResolveTypeface(family, bold, false).FaceName;

        var parse = () => Activator.CreateInstance(
            typeof(XPoint).Assembly.GetType("PdfPinata.Fonts.OpenType.OpenTypeFontface", throwOnError: true)!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [resolver.GetFont(faceName), faceName], null);

        parse.Should().NotThrow();
    }

    [Test]
    public void AFontCanBeReadFromAStream()
    {
        var resolver = new InMemoryFontResolver();
        using (var stream = new MemoryStream(Font("LiberationSans-Italic.ttf")))
            resolver.AddFont(stream);

        Served(resolver, resolver.ResolveTypeface("Liberation Sans", false, true)).Style.Should().Be(XFontStyle.Italic);
    }

    [Test]
    public void TheResolverKeepsItsOwnCopyOfTheBytes()
    {
        var bytes = Font("LiberationSans-Regular.ttf");
        var resolver = new InMemoryFontResolver();
        resolver.AddFont(bytes);

        Array.Clear(bytes, 0, bytes.Length);

        Served(resolver, resolver.ResolveTypeface("Liberation Sans", false, false)).FamilyName
            .Should().Be("Liberation Sans");
    }

    [Test]
    public void AddingTheSameFaceTwiceKeepsTheFirst()
    {
        var resolver = AResolverWith("LiberationSans-Regular.ttf");

        resolver.AddFont(Font("LiberationSans-Regular.ttf"));

        Served(resolver, resolver.ResolveTypeface("Liberation Sans", false, false)).Style.Should().Be(XFontStyle.Regular);
    }

    // ----- a family it does not have --------------------------------------------------------------------

    [Test]
    public void AnAliasAnswersWithTheFamilyItNames()
    {
        var resolver = TheLiberationFamily();
        resolver.NullIfFontNotFound = true;

        resolver.AddAlias("Arial", "Liberation Sans");

        var served = Served(resolver, resolver.ResolveTypeface("arial", true, true));
        served.FamilyName.Should().Be("Liberation Sans");
        served.Style.Should().Be(XFontStyle.BoldItalic);
    }

    [Test]
    public void AFamilyAddedUnderAnAliasedNameIsPreferredToTheAlias()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf", "LiberationSans-Regular.ttf");

        resolver.AddAlias("Liberation Sans", "Source Code Pro");

        Served(resolver, resolver.ResolveTypeface("Liberation Sans", false, false)).FamilyName
            .Should().Be("Liberation Sans");
    }

    [Test]
    public void TheDefaultFamilyIsTheFirstOneAdded()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf", "LiberationSans-Regular.ttf");

        resolver.DefaultFontName.Should().Be("Source Code Pro");
    }

    [Test]
    public void AnUnknownFamilyFallsBackToTheDefaultFamilyInTheStyleAskedFor()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf", "LiberationSans-Regular.ttf", "LiberationSans-Bold.ttf");
        resolver.DefaultFontName = "Liberation Sans";

        var served = Served(resolver, resolver.ResolveTypeface("Comic Sans MS", true, false));
        served.FamilyName.Should().Be("Liberation Sans");
        served.Style.Should().Be(XFontStyle.Bold);
    }

    [Test]
    public void ADefaultNamingAnAliasFallsBackThroughIt()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf", "LiberationSans-Regular.ttf");
        resolver.AddAlias("Arial", "Liberation Sans");
        resolver.DefaultFontName = "Arial";

        Served(resolver, resolver.ResolveTypeface("Comic Sans MS", false, false)).FamilyName
            .Should().Be("Liberation Sans");
    }

    [Test]
    public void ADefaultNamingNoFamilyFallsBackToTheFirstFamilyAdded()
    {
        var resolver = AResolverWith("SourceCodePro-Regular.otf", "LiberationSans-Regular.ttf");
        resolver.DefaultFontName = "Arial";

        Served(resolver, resolver.ResolveTypeface("Comic Sans MS", false, false)).FamilyName
            .Should().Be("Source Code Pro");
    }

    [Test]
    public void AnUnknownFamilyAnswersNullWhenToldTo()
    {
        var resolver = TheLiberationFamily();
        resolver.NullIfFontNotFound = true;

        resolver.ResolveTypeface("Comic Sans MS", false, false).Should().BeNull();
    }

    // ----- no fonts at all ------------------------------------------------------------------------------

    [Test]
    public void AResolverWithNoFontsSaysToAddOne()
    {
        var resolver = new InMemoryFontResolver();

        var resolve = () => resolver.ResolveTypeface("Liberation Sans", false, false);
        var defaultName = () => resolver.DefaultFontName;

        resolve.Should().Throw<InvalidOperationException>().WithMessage("*AddFont*");
        defaultName.Should().Throw<InvalidOperationException>().WithMessage("*AddFont*");
    }

    [Test]
    public void ADefaultSetBeforeAnyFontIsAddedIsAnsweredAsSet()
    {
        var resolver = new InMemoryFontResolver { DefaultFontName = "Liberation Sans" };

        resolver.DefaultFontName.Should().Be("Liberation Sans");
    }

    [Test]
    public void AFaceNameItNeverHandedOutIsRefused()
    {
        var resolver = TheLiberationFamily();

        var get = () => resolver.GetFont("Comic Sans MS");

        get.Should().Throw<ArgumentException>().WithMessage("*Comic Sans MS*");
    }
}
