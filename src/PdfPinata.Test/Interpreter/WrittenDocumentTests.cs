using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// A document this library writes, saved and read back, drawn onto a device: the content is
/// compressed, and it is what <see cref="XGraphics"/> wrote rather than what a test did.
/// </summary>
/// <remarks>
/// <see cref="XGraphics"/> draws in points from the top left of the page, which is the space a
/// device is drawn in at a scale of 1. So what was drawn is where it was drawn.
/// </remarks>
public class WrittenDocumentTests
{
    private static readonly XFont Font = new("Arial", 12);

    [Test]
    public void WhatXGraphicsDrewIsHandedToTheDeviceWhereItWasDrawn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawRectangle(XBrushes.Red, 10, 20, 30, 40);
            gfx.DrawLine(new XPen(XColors.Blue, 3) { DashStyle = XDashStyle.Dash }, 50, 60, 150, 60);
        }

        var device = new RecordingDevice();
        var result = PageRendering.Render(document.Reopened().Pages[0], device);

        var fill = device.Fills.Single();
        fill.Paint.Should().Be(new PagePaint(1, 0, 0));
        Corners(fill).Should().BeEquivalentTo([new XPoint(10, 20), new XPoint(40, 20), new XPoint(40, 60), new XPoint(10, 60)]);

        var stroke = device.Strokes.Single();
        stroke.Paint.Should().Be(new PagePaint(0, 0, 1));
        stroke.Stroke.Width.Should().Be(3);
        stroke.Stroke.DashPattern.Should().NotBeEmpty();
        Corners(stroke).Should().Equal(new XPoint(50, 60), new XPoint(150, 60));

        result.IsComplete.Should().BeTrue();
        device.Calls.Count(call => call.Kind == "save").Should().Be(device.Calls.Count(call => call.Kind == "restore"));
    }

    [Test]
    public void TextXGraphicsDrewIsReportedBecauseItsFontProgramIsNotReadYet()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawString("Not drawn yet", Font, XBrushes.Black, 20, 40);
            gfx.DrawRectangle(XBrushes.Red, 10, 20, 30, 40);
        }

        var device = new RecordingDevice();
        var result = PageRendering.Render(document.Reopened().Pages[0], device);

        device.Fills.Should().ContainSingle("the page is drawn but for its text");
        var diagnostic = result.Diagnostics.Single();
        diagnostic.Problem.Should().Be(PageRenderProblem.UnsupportedFont);
        diagnostic.Message.Should().Contain("TrueType");
    }

    private static XPoint[] Corners(RecordingDevice.Call call)
    {
        var points = call.Path.Points.ToArray();
        return points.Select(point =>
        {
            var mapped = call.Transform.Transform(point);
            return new XPoint(System.Math.Round(mapped.X, 6), System.Math.Round(mapped.Y, 6));
        }).ToArray();
    }
}
