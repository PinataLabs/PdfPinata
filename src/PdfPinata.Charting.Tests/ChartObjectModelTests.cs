using System;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The charting package's own object model - the one a caller builds directly, as distinct from
///   PinataLayout's, which is mapped onto this one before anything is drawn.
///   <para>
///   Every class here is the same shape: a child object created the first time it is asked for, a
///   handful of settings over it, and a <c>DeepCopy</c> that clones each child by hand and
///   reparents it. The hand-written copy is the part worth pinning - a child left out of one is
///   shared between the copy and the original, and nothing says so until one of them is written
///   to. <see cref="ChartCloneAndLineFormatTests"/> pins the chart's own nine; these are the
///   children's children, each of which copies one or two of its own.
///   </para>
/// </summary>
public class ChartObjectModelTests
{
    // ----- each piece can be built on its own -------------------------------------------------

    [Fact]
    public void EachPieceOfTheModelCanBeBuiltWithNoChartAroundIt()
    {
        new TickLabels().Font.Should().NotBeNull();
        new Gridlines().LineFormat.Should().NotBeNull();
        new AxisTitle().Font.Should().NotBeNull();
        new DataLabel().Font.Should().NotBeNull();
        new Legend().LineFormat.Should().NotBeNull();
        new Series().Name.Should().BeEmpty();
        new Font().Name.Should().BeEmpty();
        new Point(3.5).Value.Should().Be(3.5);
    }

    /// <summary>
    ///   A point built from a string is a category rather than a number, and its value is zero -
    ///   a category has a name and no height of its own.
    /// </summary>
    [Fact]
    public void APointBuiltFromAWordHasNoValueOfItsOwn()
    {
        new Point("Monday").Value.Should().Be(0);
    }

    [Fact]
    public void AFontCanBeBuiltFromANameAndASizeTogether()
    {
        var font = new Font("Palatino", XUnit.FromPoint(9));

        font.Name.Should().Be("Palatino");
        font.Size.Point.Should().Be(9);
    }

    // ----- every setting reads back -----------------------------------------------------------

    /// <summary>
    ///   Each of these is a plain field behind a property, and each is read by a renderer that will
    ///   quietly draw the default if the property answers it. Reading each back is the whole of the
    ///   claim, and it is a claim nothing else in this suite makes: the renderers are driven
    ///   through a drawn chart, which only ever sets what that test varies.
    /// </summary>
    [Fact]
    public void AFontReadsBackEverythingItWasTold()
    {
        var font = new Font
        {
            Name = "Palatino",
            Size = XUnit.FromPoint(11),
            Bold = true,
            Italic = true,
            Underline = Underline.Single,
            Strikethrough = Strikethrough.Single,
            Color = XColors.Azure,
            Superscript = true
        };

        font.Name.Should().Be("Palatino");
        font.Size.Point.Should().Be(11);
        font.Bold.Should().BeTrue();
        font.Italic.Should().BeTrue();
        font.Underline.Should().Be(Underline.Single);
        font.Strikethrough.Should().Be(Strikethrough.Single);
        font.Color.Should().Be(XColors.Azure);
        font.Superscript.Should().BeTrue();

        font.Subscript = true;
        font.Subscript.Should().BeTrue();
    }

    [Fact]
    public void ADataLabelReadsBackItsPositionAndItsType()
    {
        var label = new DataLabel
        {
            Position = DataLabelPosition.OutsideEnd,
            Type = DataLabelType.Percent,
            Format = "0.0"
        };

        label.Position.Should().Be(DataLabelPosition.OutsideEnd);
        label.Type.Should().Be(DataLabelType.Percent);
        label.Format.Should().Be("0.0");
    }

    /// <summary>
    ///   A position or a type that is not one of the enum's own members is refused where it is set.
    ///   A renderer reading one would fall through its switch and draw the label nowhere, which is
    ///   indistinguishable from a chart that was told not to label its points.
    /// </summary>
    [Fact]
    public void ADataLabelRefusesAPositionOrATypeThatIsNotOneOfTheNamedOnes()
    {
        var label = new DataLabel();

        var position = () => label.Position = (DataLabelPosition)99;
        var type = () => label.Type = (DataLabelType)99;

        position.Should().Throw<ArgumentException>();
        type.Should().Throw<ArgumentException>();
    }

    // ----- copying ----------------------------------------------------------------------------

    /// <summary>
    ///   Each of these clones the one or two children it has, so a copy written to leaves the
    ///   original alone. The original is changed after the copy is taken, which is the only way a
    ///   shared child shows up at all.
    /// </summary>
    [Fact]
    public void ACopiedTickLabelBlockKeepsItsOwnFont()
    {
        var labels = new TickLabels { Format = "0.00" };
        labels.Font.Name = "Palatino";

        var copy = labels.Clone();
        labels.Font.Name = "Baskerville";
        labels.Format = "0";

        copy.Font.Should().NotBeSameAs(labels.Font);
        copy.Font.Name.Should().Be("Palatino");
        copy.Format.Should().Be("0.00");
    }

    [Fact]
    public void ACopiedAxisTitleKeepsItsOwnFont()
    {
        var title = new AxisTitle { Caption = "across" };
        title.Font.Name = "Palatino";

        var copy = title.Clone();
        title.Font.Name = "Baskerville";

        copy.Caption.Should().Be("across");
        copy.Font.Should().NotBeSameAs(title.Font);
        copy.Font.Name.Should().Be("Palatino");
    }

    [Fact]
    public void ACopiedDataLabelKeepsItsOwnFont()
    {
        var label = new DataLabel { Format = "0.0" };
        label.Font.Name = "Palatino";

        var copy = label.Clone();
        label.Font.Name = "Baskerville";

        copy.Format.Should().Be("0.0");
        copy.Font.Should().NotBeSameAs(label.Font);
        copy.Font.Name.Should().Be("Palatino");
    }

    [Fact]
    public void ACopiedGridlineBlockKeepsItsOwnLineFormat()
    {
        var gridlines = new Gridlines();
        gridlines.LineFormat.Width = 3;

        var copy = gridlines.Clone();
        gridlines.LineFormat.Width = 1;

        copy.LineFormat.Should().NotBeSameAs(gridlines.LineFormat);
        copy.LineFormat.Width.Point.Should().Be(3);
    }

    [Fact]
    public void ACopiedPointKeepsItsOwnLineAndFillFormats()
    {
        var point = new Point(2.5);
        point.LineFormat.Width = 3;
        point.FillFormat.Color = XColors.Azure;

        var copy = point.Clone();
        point.LineFormat.Width = 1;
        point.FillFormat.Color = XColors.Beige;

        copy.Value.Should().Be(2.5);
        copy.LineFormat.Should().NotBeSameAs(point.LineFormat);
        copy.LineFormat.Width.Point.Should().Be(3);
        copy.FillFormat.Color.Should().Be(XColors.Azure);
    }

    [Fact]
    public void ACopiedFontIsAFontOfItsOwn()
    {
        var font = new Font("Palatino", XUnit.FromPoint(9)) { Bold = true };

        var copy = font.Clone();
        font.Name = "Baskerville";

        copy.Should().NotBeSameAs(font);
        copy.Name.Should().Be("Palatino");
        copy.Bold.Should().BeTrue();
    }

    // ----- a chart still made of them ---------------------------------------------------------

    /// <summary>
    ///   The same pieces reached through a chart rather than built on their own, so that the
    ///   lazily created children are created by the chart and the parent each one is given is the
    ///   chart's rather than none.
    /// </summary>
    [Fact]
    public void AChartCreatesEachOfThesePiecesTheFirstTimeItIsAskedForOne()
    {
        var chart = Charts.Empty(ChartType.Column2D);

        chart.XAxis.MajorGridlines.LineFormat.Should().NotBeNull();
        chart.XAxis.MinorGridlines.LineFormat.Should().NotBeNull();
        chart.XAxis.TickLabels.Font.Should().NotBeNull();
        chart.XAxis.Title.Font.Should().NotBeNull();
        chart.DataLabel.Font.Should().NotBeNull();
        chart.Legend.LineFormat.Should().NotBeNull();
        chart.PlotArea.FillFormat.Should().NotBeNull();
    }
}
