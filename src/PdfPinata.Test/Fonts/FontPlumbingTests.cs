using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   The plumbing between "the caller asked for Arial bold" and "here are the bytes of a font
///   file": what a resolver is told, what it hands back, and how the answer is keyed so the same
///   request is not resolved twice. None of it draws anything, and all of it is between the
///   drawing code and whichever backend is installed.
/// </summary>
public class FontPlumbingTests
{
    // ----- what a resolver is asked for ----------------------------------------------------------

    /// <summary>
    ///   <c>FontResolvingOptions</c> is internal, and this repository carries no
    ///   <c>InternalsVisibleTo</c>, so it is reached the way the other probes in this suite reach
    ///   what they need.
    /// </summary>
    static object ResolvingOptions(params object[] arguments) =>
        Activator.CreateInstance(
            // ReSharper disable once AssignNullToNotNullAttribute
            typeof(XPoint).Assembly.GetType("PdfPinata.Fonts.FontResolvingOptions", throwOnError: true),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null);

    static object MemberOf(object instance, string name)
    {
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetProperty(name, flags) is { } property
            ? property.GetValue(instance)
            : type.GetField(name, flags).GetValue(instance);
    }

    [Theory]
    [InlineData(XFontStyle.Regular, false, false, false)]
    [InlineData(XFontStyle.Bold, true, false, false)]
    [InlineData(XFontStyle.Italic, false, true, false)]
    [InlineData(XFontStyle.BoldItalic, true, true, true)]
    public void TheStyleAskedForIsReadableOneBitAtATime(
        XFontStyle style, bool bold, bool italic, bool boldItalic)
    {
        var options = ResolvingOptions(style);

        MemberOf(options, "FontStyle").Should().Be(style);
        MemberOf(options, "IsBold").Should().Be(bold);
        MemberOf(options, "IsItalic").Should().Be(italic);
        MemberOf(options, "IsBoldItalic").Should().Be(boldItalic);
        MemberOf(options, "OverrideStyleSimulations").Should().Be(false,
            "a resolver decides, unless told otherwise");
        MemberOf(options, "MustSimulateBold").Should().Be(false);
        MemberOf(options, "MustSimulateItalic").Should().Be(false);
    }

    [Fact]
    public void SimulationCanBeDemandedRatherThanLeftToTheResolver()
    {
        // The second constructor is how a caller says "I know there is no bold file, stroke it" -
        // which is what the style-simulation path in the renderer needs.
        var options = ResolvingOptions(XFontStyle.Bold, XStyleSimulations.BoldItalicSimulation);

        MemberOf(options, "OverrideStyleSimulations").Should().Be(true);
        MemberOf(options, "MustSimulateBold").Should().Be(true);
        MemberOf(options, "MustSimulateItalic").Should().Be(true);
    }

    // ----- what a resolver hands back ------------------------------------------------------------

    [Fact]
    public void AResolvedFaceIsNamedAndSimulatesNothingUnlessItSaysSo()
    {
        var info = new FontResolverInfo("LiberationSans-Regular.ttf");

        info.FaceName.Should().Be("LiberationSans-Regular.ttf");
        info.MustSimulateBold.Should().BeFalse();
        info.MustSimulateItalic.Should().BeFalse();
        info.StyleSimulations.Should().Be(XStyleSimulations.None);
    }

    [Theory]
    [InlineData(false, false, XStyleSimulations.None)]
    [InlineData(true, false, XStyleSimulations.BoldSimulation)]
    [InlineData(false, true, XStyleSimulations.ItalicSimulation)]
    [InlineData(true, true, XStyleSimulations.BoldItalicSimulation)]
    public void TheTwoSimulationFlagsAndTheSimulationEnumSayTheSameThing(
        bool bold, bool italic, XStyleSimulations expected)
    {
        var byFlags = new FontResolverInfo("face.ttf", bold, italic);
        var byEnum = new FontResolverInfo("face.ttf", expected);

        byFlags.StyleSimulations.Should().Be(expected);
        byEnum.MustSimulateBold.Should().Be(bold);
        byEnum.MustSimulateItalic.Should().Be(italic);
    }

    [Fact]
    public void AResolvedFaceMustBeNamedSomething()
    {
        var nothing = () => new FontResolverInfo(null);
        var empty = () => new FontResolverInfo("");

        nothing.Should().Throw<ArgumentNullException>();
        empty.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TwoFacesThatDifferOnlyInCaseShareAKeyAndSimulationsDoNot()
    {
        // The key is what the font cache is indexed by. Face names come from a resolver written by
        // a consumer, so the case they arrive in is not something PDFsharp controls - but a face
        // stroked for bold is a different face from the same file drawn plainly.
        string KeyOf(FontResolverInfo info) => (string)typeof(FontResolverInfo)
            .GetProperty("Key", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(info);

        KeyOf(new FontResolverInfo("Face.ttf")).Should().Be(KeyOf(new FontResolverInfo("face.TTF")));
        KeyOf(new FontResolverInfo("face.ttf", true, false))
            .Should().NotBe(KeyOf(new FontResolverInfo("face.ttf", false, false)));
        KeyOf(new FontResolverInfo("face.ttf", false, true))
            .Should().NotBe(KeyOf(new FontResolverInfo("face.ttf", false, false)));
    }

    [Fact]
    public void AFaceFromAFontCollectionIsNotSupportedYetAndSaysSo()
    {
        // The collection index is vestigial: a collection is taken apart by the resolver, which
        // names each face separately, so nothing downstream ever needs an index.
        var constructor = typeof(FontResolverInfo).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(string), typeof(bool), typeof(bool), typeof(int) }, null);

        var act = () => constructor.Invoke(new object[] { "face.ttf", false, false, 1 });

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<NotImplementedException>();
    }

    // ----- a glyph's outline ---------------------------------------------------------------------

    [Fact]
    public void EachKindOfOutlineSegmentCarriesWhatItNeedsAndNothingElse()
    {
        var start = XGlyphSegment.StartAt(new XPoint(1, 2));
        var line = XGlyphSegment.LineTo(new XPoint(3, 4));
        var curve = XGlyphSegment.CurveTo(new XPoint(5, 6), new XPoint(7, 8), new XPoint(9, 10));
        var close = XGlyphSegment.Close();

        start.Kind.Should().Be(XGlyphSegmentKind.Start);
        start.End.Should().Be(new XPoint(1, 2));

        line.Kind.Should().Be(XGlyphSegmentKind.Line);
        line.End.Should().Be(new XPoint(3, 4));

        curve.Kind.Should().Be(XGlyphSegmentKind.Curve);
        curve.Control1.Should().Be(new XPoint(5, 6));
        curve.Control2.Should().Be(new XPoint(7, 8));
        curve.End.Should().Be(new XPoint(9, 10));

        close.Kind.Should().Be(XGlyphSegmentKind.Close);
        close.End.Should().Be(new XPoint(), "a close carries no point");
    }

    [Fact]
    public void AnOutlineKeepsItsSegmentsInTheOrderTheyAreDrawn()
    {
        var segments = new[]
        {
            XGlyphSegment.StartAt(new XPoint(0, 0)),
            XGlyphSegment.LineTo(new XPoint(10, 0)),
            XGlyphSegment.Close()
        };

        var outline = new XGlyphOutline(segments);

        outline.Segments.Should().HaveCount(3);
        outline.Segments[0].Kind.Should().Be(XGlyphSegmentKind.Start);
        outline.Segments[2].Kind.Should().Be(XGlyphSegmentKind.Close);
    }

    [Fact]
    public void AnOutlineCopiesItsSegmentsInSoTheCallersListCanChangeAfterwards()
    {
        var segments = new List<XGlyphSegment> { XGlyphSegment.StartAt(new XPoint(0, 0)) };

        var outline = new XGlyphOutline(segments);
        segments.Add(XGlyphSegment.LineTo(new XPoint(10, 0)));

        outline.Segments.Should().HaveCount(1);
    }

    [Fact]
    public void AGlyphWithNoOutlineIsAnOutlineWithNoSegments()
    {
        // A space, or a character the font draws with a bitmap. Empty rather than null, so that
        // AddString can walk it without asking.
        new XGlyphOutline(Array.Empty<XGlyphSegment>()).Segments.Should().BeEmpty();
    }

    [Fact]
    public void AnOutlineOfNothingAtAllIsRefused()
    {
        var act = () => new XGlyphOutline(null);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- the global seams, on a copy of the library that has none set --------------------------

    /// <summary>
    ///   <see cref="GlobalFontSettings"/> is per application domain and this test assembly sets it
    ///   up once for every test in it, so the unset behaviour cannot be reached in the ordinary
    ///   way. A second copy of the library in its own load context has its own statics, which is
    ///   where these questions can still be asked. See <c>ColdColourTableTests</c> for the same
    ///   technique applied to the colour table.
    /// </summary>
    static object OnAColdCopyOfTheLibrary(Func<Assembly, object> work)
    {
        var context = new AssemblyLoadContext("cold PdfPinata fonts", isCollectible: true);
        try
        {
            return work(context.LoadFromAssemblyPath(typeof(XPoint).Assembly.Location));
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
        finally
        {
            context.Unload();
        }
    }

    static object SettingsProperty(Assembly assembly, string name) =>
        assembly.GetType("PdfPinata.Fonts.GlobalFontSettings", throwOnError: true)
            .GetProperty(name).GetValue(null);

    [Fact]
    public void ReadingTheFontResolverBeforeOneIsSetSaysHowToSetOne()
    {
        var act = () => OnAColdCopyOfTheLibrary(assembly => SettingsProperty(assembly, "FontResolver"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FontResolver*")
            .WithMessage("*PdfPinata.Skia*");
    }

    [Fact]
    public void ReadingTheGlyphOutlineProviderBeforeOneIsSetSaysHowToSetOne()
    {
        var act = () => OnAColdCopyOfTheLibrary(
            assembly => SettingsProperty(assembly, "GlyphOutlineProvider"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*GlyphOutlineProvider*")
            .WithMessage("*PdfPinata.Skia*");
    }

    [Fact]
    public void TheFontResolverCannotBeTakenAway()
    {
        var act = () => OnAColdCopyOfTheLibrary(assembly =>
        {
            assembly.GetType("PdfPinata.Fonts.GlobalFontSettings", throwOnError: true)
                .GetProperty("FontResolver").SetValue(null, null);
            return null;
        });

        act.Should().Throw<ArgumentNullException>("nothing works without one");
    }

    [Fact]
    public void TheDefaultFontEncodingIsUnicodeUntilSomebodySaysOtherwise()
    {
        var encoding = OnAColdCopyOfTheLibrary(
            assembly => SettingsProperty(assembly, "DefaultFontEncoding"));

        encoding.ToString().Should().Be("Unicode");
    }

    [Fact]
    public void TheDefaultFontEncodingCannotBeChangedOnceItHasBeenRead()
    {
        // Reading it settles it, so a caller who measures a string and then asks for Windows-1252
        // is refused - which is the whole reason the property documents "must be set only once".
        var act = () => OnAColdCopyOfTheLibrary(assembly =>
        {
            var settings = assembly.GetType("PdfPinata.Fonts.GlobalFontSettings", throwOnError: true);
            var property = settings.GetProperty("DefaultFontEncoding");
            _ = property.GetValue(null);
            property.SetValue(null, Enum.Parse(
                // ReSharper disable once AssignNullToNotNullAttribute
                assembly.GetType("PdfPinata.Pdf.PdfFontEncoding", throwOnError: true), "WinAnsi"));
            return null;
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*DefaultFontEncoding*");
    }

    [Fact]
    public void SettingTheDefaultFontEncodingToWhatItAlreadyIsIsAllowed()
    {
        // A web application sets it on every request, so setting the same value twice must not be
        // an error - only changing it is.
        var act = () => OnAColdCopyOfTheLibrary(assembly =>
        {
            var settings = assembly.GetType("PdfPinata.Fonts.GlobalFontSettings", throwOnError: true);
            var property = settings.GetProperty("DefaultFontEncoding");
            var unicode = Enum.Parse(
                // ReSharper disable once AssignNullToNotNullAttribute
                assembly.GetType("PdfPinata.Pdf.PdfFontEncoding", throwOnError: true), "Unicode");
            property.SetValue(null, unicode);
            property.SetValue(null, unicode);
            return null;
        });

        act.Should().NotThrow();
    }

    // ----- asking whether a seam is set, without catching -----------------------------------------

    [Fact]
    public void IsFontResolverSetIsFalseOnACopyOfTheLibraryNothingHasTouchedYet()
    {
        var isSet = OnAColdCopyOfTheLibrary(assembly => SettingsProperty(assembly, "IsFontResolverSet"));

        isSet.Should().Be(false, "reading this without catching an exception is the point of it");
    }

    [Fact]
    public void IsFontResolverSetIsTrueInThisProcessBecauseTheTestAssemblyAlreadyRegisteredOne()
    {
        // Unlike the cold-copy tests above, this asks the question of the resolver the whole suite
        // runs against - PinnedFontResolver, registered once by TestBackendSetup - rather than
        // trying to flip it, which the seam refuses after the first font it resolved.
        GlobalFontSettings.IsFontResolverSet.Should().BeTrue();
    }

    [Fact]
    public void FontResolverLifecycleSaysItRefusesASecondWrite()
    {
        GlobalFontSettings.FontResolverLifecycle.Should().Be(SeamLifecycle.SetOnce,
            "changing it once a font has been resolved would disagree with what is already cached");
    }

    [Fact]
    public void IsGlyphOutlineProviderSetIsFalseOnACopyOfTheLibraryNothingHasTouchedYet()
    {
        var isSet = OnAColdCopyOfTheLibrary(
            assembly => SettingsProperty(assembly, "IsGlyphOutlineProviderSet"));

        isSet.Should().Be(false);
    }

    [Fact]
    public void GlyphOutlineProviderLifecycleSaysItMayBeSetAtAnyTime()
    {
        GlobalFontSettings.GlyphOutlineProviderLifecycle.Should().Be(SeamLifecycle.SetAnytime,
            "only XGraphicsPath.AddString reads it, so nothing is cached against it");
    }

    [Fact]
    public void IsDefaultFontEncodingSetIsFalseUntilTheDefaultOrAChoiceIsRead()
    {
        var isSet = OnAColdCopyOfTheLibrary(assembly => SettingsProperty(assembly, "IsDefaultFontEncodingSet"));

        isSet.Should().Be(false,
            "even though reading DefaultFontEncoding itself never throws, this is the only way to " +
            "tell its default apart from a value a caller chose");
    }

    [Fact]
    public void IsDefaultFontEncodingSetBecomesTrueOnceItHasBeenRead()
    {
        // DefaultFontEncoding answers its own default the first time it is read, and settles itself
        // to that default as a side effect - which is exactly the side effect IsDefaultFontEncodingSet
        // exists to let a caller find out about without triggering it.
        var isSet = OnAColdCopyOfTheLibrary(assembly =>
        {
            var settings = assembly.GetType("PdfPinata.Fonts.GlobalFontSettings", throwOnError: true);
            _ = settings.GetProperty("DefaultFontEncoding").GetValue(null);
            return settings.GetProperty("IsDefaultFontEncodingSet").GetValue(null);
        });

        isSet.Should().Be(true);
    }

    [Fact]
    public void DefaultFontEncodingLifecycleSaysItRefusesASecondWriteToADifferentValue()
    {
        GlobalFontSettings.DefaultFontEncodingLifecycle.Should().Be(SeamLifecycle.SetOnce);
    }

    [Fact]
    public void IsImageSourceImplSetIsFalseOnACopyOfTheLibraryNothingHasTouchedYet()
    {
        var isSet = OnAColdCopyOfTheLibrary(assembly =>
        {
            var imageSourceType = assembly.GetType(
                "PinataLayout.DocumentObjectModel.Shapes.ImageSource",
                throwOnError: true);
            return imageSourceType.GetProperty("IsImageSourceImplSet").GetValue(null);
        });

        isSet.Should().Be(false);
    }

    [Fact]
    public void IsImageSourceImplSetIsTrueInThisProcessBecauseTheTestAssemblyAlreadyRegisteredOne()
    {
        ImageSource.IsImageSourceImplSet.Should().BeTrue();
    }

    [Fact]
    public void ImageSourceImplLifecycleSaysItMayBeSetAtAnyTime()
    {
        ImageSource.ImageSourceImplLifecycle.Should().Be(SeamLifecycle.SetAnytime,
            "unlike the font resolver, it may be replaced at any time");
    }
}
