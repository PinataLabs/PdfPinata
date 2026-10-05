using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Xunit;
using Charting = PdfPinata.Charting;
using Dom = PinataLayout.DocumentObjectModel;
using DomCharts = PinataLayout.DocumentObjectModel.Shapes.Charts;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   The chart mappers carry each of these enumerations from the DOM into PdfPinata.Charting by
///   an integer cast, so the two sides agree only as long as every member has the same name at the
///   same value on both.
/// </summary>
/// <remarks>
///   Nothing else ties them together. A member added to one side, or the members of one reordered,
///   would compile and map silently to the wrong member of the other - a pie drawn as a bar, a
///   marker drawn as the shape beside it. The casts are kept; this is what makes them safe. A new
///   cast across the two assemblies belongs in <see cref="CastAcross"/>.
/// </remarks>
public class ChartEnumParityTests
{
    public static TheoryData<Type, Type> CastAcross => new()
    {
        // ChartMapper.MapFrom; SeriesCollectionMapper's chart type, of a series and of its chart.
        { typeof(DomCharts.ChartType), typeof(Charting.ChartType) },
        // ChartMapper.MapFrom, DisplayBlanksAs.
        { typeof(DomCharts.BlankType), typeof(Charting.BlankType) },
        // AxisMapper, the major and the minor tick mark.
        { typeof(DomCharts.TickMarkType), typeof(Charting.TickMarkType) },
        // AxisMapper, the axis title's alignment.
        { typeof(DomCharts.HorizontalAlignment), typeof(Charting.HorizontalAlignment) },
        // AxisMapper, the axis title's vertical alignment - the DOM's is the table one.
        { typeof(Dom.Tables.VerticalAlignment), typeof(Charting.VerticalAlignment) },
        // DataLabelMapper, the position and the type.
        { typeof(DomCharts.DataLabelPosition), typeof(Charting.DataLabelPosition) },
        { typeof(DomCharts.DataLabelType), typeof(Charting.DataLabelType) },
        // SeriesCollectionMapper, the marker style.
        { typeof(DomCharts.MarkerStyle), typeof(Charting.MarkerStyle) },
        // FontMapper, a chart font's strikethrough and underline.
        { typeof(Dom.Strikethrough), typeof(Charting.Strikethrough) },
        { typeof(Dom.Underline), typeof(Charting.Underline) }
    };

    [Theory]
    [MemberData(nameof(CastAcross))]
    public void BothSidesHaveTheSameMembersAtTheSameValues(Type dom, Type charting)
    {
        // Compared as name and value together, both ways: the same names at different values is
        // the reordering this guards against, and the same values under different names is a
        // member renamed on one side only.
        Members(charting).Should().BeEquivalentTo(Members(dom),
            "{0} is cast by integer to {1}", dom.FullName, charting.FullName);
    }

    private static Dictionary<string, long> Members(Type enumeration) =>
        Enum.GetNames(enumeration).ToDictionary(
            name => name,
            name => Convert.ToInt64(Enum.Parse(enumeration, name)));
}
