using System;
using System.Reflection;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   How wide a border is drawn, worked out from what its own <see cref="Border"/> says and, where it
///   says nothing, from the <see cref="Borders"/> it belongs to.
/// </summary>
/// <remarks>
///   <para>
///     Every case below is one path through <c>BordersRenderer.GetWidth</c>, pinned at the width it
///     answers today. Several of them are not what a reader would guess - a border that exists but
///     says nothing is not drawn even when the collection gives a width, and a diagonal never takes
///     the collection's settings at all - and that is exactly why they are written down.
///   </para>
///   <para>
///     <c>BordersRenderer</c> is internal and this repository carries no <c>InternalsVisibleTo</c>,
///     so it is reached by reflection, the way the other probes in these suites reach theirs.
///   </para>
/// </remarks>
public class BorderWidthResolutionTests
{
    private const double OwnWidth = 3;
    private const double SharedWidth = 2;
    private const double Default = 0.5;

    public enum Own
    {
        Absent,
        SaysNothing,
        Hidden,
        HiddenWithAWidth,
        Width,
        Colour,
        Style,
        Visible
    }

    public enum Shared
    {
        Nothing,
        Hidden,
        HiddenWithAWidth,
        Width,
        Colour,
        Style,
        Visible
    }

    [Theory]
    // A border of its own decides, and the collection is consulted only for a width to draw it at.
    [InlineData(Own.Hidden, Shared.Width, BorderType.Top, 0)]
    [InlineData(Own.HiddenWithAWidth, Shared.Width, BorderType.Top, 0)]
    [InlineData(Own.Width, Shared.Nothing, BorderType.Top, OwnWidth)]
    [InlineData(Own.Width, Shared.Width, BorderType.Top, OwnWidth)]
    [InlineData(Own.Width, Shared.Hidden, BorderType.Top, OwnWidth)]
    [InlineData(Own.Colour, Shared.Width, BorderType.Top, SharedWidth)]
    [InlineData(Own.Colour, Shared.Nothing, BorderType.Top, Default)]
    [InlineData(Own.Style, Shared.Width, BorderType.Top, SharedWidth)]
    [InlineData(Own.Style, Shared.Nothing, BorderType.Top, Default)]
    [InlineData(Own.Visible, Shared.Width, BorderType.Top, SharedWidth)]
    [InlineData(Own.Visible, Shared.Nothing, BorderType.Top, Default)]
    [InlineData(Own.SaysNothing, Shared.Width, BorderType.Top, 0)]
    [InlineData(Own.SaysNothing, Shared.Visible, BorderType.Top, 0)]
    // A diagonal of its own is resolved exactly as any other border is.
    [InlineData(Own.Width, Shared.Nothing, BorderType.DiagonalDown, OwnWidth)]
    [InlineData(Own.Colour, Shared.Width, BorderType.DiagonalUp, SharedWidth)]
    [InlineData(Own.Hidden, Shared.Width, BorderType.DiagonalUp, 0)]
    // With no border of its own, the collection decides - for every edge but a diagonal.
    [InlineData(Own.Absent, Shared.Nothing, BorderType.Top, 0)]
    [InlineData(Own.Absent, Shared.Hidden, BorderType.Top, 0)]
    [InlineData(Own.Absent, Shared.HiddenWithAWidth, BorderType.Top, 0)]
    [InlineData(Own.Absent, Shared.Width, BorderType.Left, SharedWidth)]
    [InlineData(Own.Absent, Shared.Colour, BorderType.Bottom, Default)]
    [InlineData(Own.Absent, Shared.Style, BorderType.Right, Default)]
    [InlineData(Own.Absent, Shared.Visible, BorderType.Top, Default)]
    [InlineData(Own.Absent, Shared.Width, BorderType.DiagonalDown, 0)]
    [InlineData(Own.Absent, Shared.Visible, BorderType.DiagonalUp, 0)]
    public void ABorderIsDrawnAtTheWidthItsSettingsResolveTo(Own own, Shared shared, BorderType type, double expected)
    {
        var borders = new Document().AddSection().AddParagraph().Format.Borders;
        Describe(borders, shared);
        if (own != Own.Absent)
            Describe(BorderOf(borders, type), own);

        WidthOf(borders, type).Should().Be(expected);
    }

    private static void Describe(Borders borders, Shared shared)
    {
        switch (shared)
        {
            case Shared.Hidden:
                borders.Visible = false;
                break;
            case Shared.HiddenWithAWidth:
                borders.Visible = false;
                borders.Width = Unit.FromPoint(SharedWidth);
                break;
            case Shared.Width:
                borders.Width = Unit.FromPoint(SharedWidth);
                break;
            case Shared.Colour:
                borders.Color = Colors.Red;
                break;
            case Shared.Style:
                borders.Style = BorderStyle.Dot;
                break;
            case Shared.Visible:
                borders.Visible = true;
                break;
        }
    }

    private static void Describe(Border border, Own own)
    {
        switch (own)
        {
            case Own.Hidden:
                border.Visible = false;
                break;
            case Own.HiddenWithAWidth:
                border.Visible = false;
                border.Width = Unit.FromPoint(OwnWidth);
                break;
            case Own.Width:
                border.Width = Unit.FromPoint(OwnWidth);
                break;
            case Own.Colour:
                border.Color = Colors.Red;
                break;
            case Own.Style:
                border.Style = BorderStyle.Dot;
                break;
            case Own.Visible:
                border.Visible = true;
                break;
        }
    }

    /// <summary>Reading the property is what creates the border, which is all SaysNothing does.</summary>
    private static Border BorderOf(Borders borders, BorderType type) => type switch
    {
        BorderType.Top => borders.Top,
        BorderType.Left => borders.Left,
        BorderType.Bottom => borders.Bottom,
        BorderType.Right => borders.Right,
        BorderType.DiagonalDown => borders.DiagonalDown,
        BorderType.DiagonalUp => borders.DiagonalUp,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static double WidthOf(Borders borders, BorderType type)
    {
        var rendererType = typeof(PdfDocumentRenderer).Assembly.GetType("PinataLayout.Rendering.BordersRenderer", true)!;
        var renderer = Activator.CreateInstance(rendererType,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [borders, null], null);
        var getWidth = rendererType.GetMethod("GetWidth", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((XUnit)getWidth.Invoke(renderer, [type])!).Point;
    }
}
