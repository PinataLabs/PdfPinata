using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The 141 named colours are spelled out three times over: once as an <c>XKnownColor</c> value,
///   once as an ARGB in <c>XKnownColorTable</c>, and once again as a property on each of
///   <see cref="XColors"/>, <see cref="XPens"/> and <see cref="XBrushes"/>. Nothing in the library
///   checks that the four lists agree, and they are exactly the sort of hand-maintained parallel
///   tables that quietly drift - so the tests here walk all of them by reflection rather than
///   naming a handful of colours and hoping the rest are fine.
/// </summary>
public class XKnownColorTests
{
    /// <summary>Every value of the enum, which is the list the other three are checked against.</summary>
    static readonly XKnownColor[] AllKnownColors =
        Enum.GetValues<XKnownColor>();

    static IEnumerable<PropertyInfo> PropertiesOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Static);

    [Fact]
    public void ThereAreAsManyNamedColoursAsTheDocumentationClaims()
    {
        AllKnownColors.Should().HaveCount(141);
    }

    [Fact]
    public void EveryNamedColourHasAPropertyOnXColorsThatIsThatColour()
    {
        var properties = PropertiesOf(typeof(XColors)).ToDictionary(property => property.Name);

        foreach (var known in AllKnownColors)
        {
            properties.Should().ContainKey(known.ToString());

            // ReSharper disable once PossibleNullReferenceException
            var fromProperty = (XColor)properties[known.ToString()].GetValue(null);
            fromProperty.Should().Be(XColor.FromKnownColor(known),
                $"XColors.{known} is meant to be the colour of the same name");
        }
    }

    [Fact]
    public void XColorsOffersNothingBeyondTheNamedColours()
    {
        var names = AllKnownColors.Select(known => known.ToString()).ToHashSet();

        PropertiesOf(typeof(XColors)).Select(property => property.Name)
            .Should().OnlyContain(name => names.Contains(name));
    }

    [Fact]
    public void EveryNamedColourHasAPenOfTheSameColourAWholeUnitWide()
    {
        var properties = PropertiesOf(typeof(XPens)).ToDictionary(property => property.Name);

        foreach (var known in AllKnownColors)
        {
            properties.Should().ContainKey(known.ToString());

            var pen = (XPen)properties[known.ToString()].GetValue(null);
            // ReSharper disable once PossibleNullReferenceException
            pen.Color.Should().Be(XColor.FromKnownColor(known), $"XPens.{known} draws in {known}");
            pen.Width.Should().Be(1);
        }
    }

    [Fact]
    public void EveryNamedColourHasABrushOfTheSameColour()
    {
        var properties = PropertiesOf(typeof(XBrushes)).ToDictionary(property => property.Name);

        foreach (var known in AllKnownColors)
        {
            properties.Should().ContainKey(known.ToString());

            var brush = (XSolidBrush)properties[known.ToString()].GetValue(null);
            // ReSharper disable once PossibleNullReferenceException
            brush.Color.Should().Be(XColor.FromKnownColor(known), $"XBrushes.{known} fills in {known}");
        }
    }

    [Fact]
    public void EveryNamedColourExceptTransparentIsOpaque()
    {
        foreach (var known in AllKnownColors.Where(known => known != XKnownColor.Transparent))
            XColor.FromKnownColor(known).A.Should().Be(1, $"{known} is a solid colour");

        XColor.FromKnownColor(XKnownColor.Transparent).A.Should().Be(0);
    }

    [Fact]
    public void TheTwoPairsOfSynonymsAreTheSameColourTwice()
    {
        // Left in for compatibility with GDI+, which has both spellings of each.
        XColors.Aqua.Should().Be(XColors.Cyan);
        XColors.Fuchsia.Should().Be(XColors.Magenta);
    }

    [Fact]
    public void EveryNamedColourIsRecognisedAsOne()
    {
        foreach (var known in AllKnownColors)
            XColor.FromKnownColor(known).IsKnownColor.Should().BeTrue($"{known} is in the table");
    }

    [Fact]
    public void AColourThatIsNotInTheTableIsNotAKnownColour()
    {
        XColors.Black.IsKnownColor.Should().BeTrue();
        XColor.FromArgb(1, 2, 3).IsKnownColor.Should().BeFalse();
    }

    [Fact]
    public void AKnownColourCanBeFoundAgainFromItsArgbValue()
    {
        XColorResourceManager.GetKnownColor(0xFFFF0000).Should().Be(XKnownColor.Red);
        XColorResourceManager.GetKnownColor(0xFF000000).Should().Be(XKnownColor.Black);
    }

    [Fact]
    public void AnArgbValueThatNamesNoColourIsRefused()
    {
        var act = () => XColorResourceManager.GetKnownColor(0xFF010203);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheListOfKnownColoursCanBeHadWithOrWithoutTransparent()
    {
        var withTransparent = XColorResourceManager.GetKnownColors(true);
        var withoutTransparent = XColorResourceManager.GetKnownColors(false);

        withTransparent.Should().Contain(XKnownColor.Transparent);
        withoutTransparent.Should().NotContain(XKnownColor.Transparent);
        withoutTransparent.Should().HaveCount(withTransparent.Length - 1);
        withTransparent.Should().Contain(XKnownColor.Red);
        withoutTransparent.Should().Contain(XKnownColor.Red);
    }

    [Fact]
    public void EveryColourTheResourceManagerListsCanBeNamedInBothItsLanguages()
    {
        // The manager translates into German and falls back to English for everything else, and
        // it looks the colour up by walking a table that need not hold every known colour. What
        // it does hold has to be nameable, or the lookup throws.
        var english = new XColorResourceManager(CultureInfo.InvariantCulture);
        var german = new XColorResourceManager(CultureInfo.GetCultureInfo("de-DE"));

        foreach (var known in XColorResourceManager.GetKnownColors(true))
        {
            english.ToColorName(known).Should().NotBeNullOrWhiteSpace();
            german.ToColorName(known).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void TheGermanNameIsUsedForAGermanCultureAndTheEnglishOneOtherwise()
    {
        new XColorResourceManager(CultureInfo.GetCultureInfo("de-DE")).ToColorName(XKnownColor.Black)
            .Should().Be("Schwarz");
        new XColorResourceManager(CultureInfo.GetCultureInfo("de-AT")).ToColorName(XKnownColor.Black)
            .Should().Be("Schwarz", "the language is what decides, not the country");
        new XColorResourceManager(CultureInfo.GetCultureInfo("fr-FR")).ToColorName(XKnownColor.Black)
            .Should().Be("Black", "there is no French translation, so it falls back to English");
    }

    [Fact]
    public void AManagerWithNoCultureUsesTheOneTheMachineIsSetTo()
    {
        // Only that it works and names the colour - which name it picks depends on the machine,
        // and asserting either would make the test pass or fail by locale.
        new XColorResourceManager().ToColorName(XKnownColor.Black).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AColourTheTableDoesNotListCannotBeNamed()
    {
        var act = () => new XColorResourceManager(CultureInfo.InvariantCulture)
            .ToColorName((XKnownColor)(-1));

        act.Should().Throw<InvalidEnumArgumentException>();
    }

    [Fact]
    public void AColourIsNamedWhenItIsKnownAndSpelledOutWhenItIsNot()
    {
        var manager = new XColorResourceManager(CultureInfo.InvariantCulture);

        manager.ToColorName(XColors.Black).Should().Be("Black");
        manager.ToColorName(XColor.FromArgb(1, 2, 3)).Should().Be("255, 1, 2, 3",
            "an unnamed colour is given as alpha and its three channels rather than left blank");
    }
}
