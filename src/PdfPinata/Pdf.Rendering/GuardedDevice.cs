using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Internal;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The caller's device as the interpreter calls it: whatever the device throws is recorded
/// against the operator being drawn and goes no further, and a <see cref="Restore"/> reaches the
/// device only where the <see cref="Save"/> it matches did.
/// </summary>
/// <remarks>
/// Every call the interpreter makes to a device goes through this, by way of
/// <see cref="PaintRouter"/>, so that there is one place a device's exception is caught rather
/// than a guard to remember at each call. A page is drawn but
/// for what the device could not draw, as it is drawn but for what the file got wrong. Being out
/// of memory is passed on, as it is everywhere.
/// </remarks>
internal sealed class GuardedDevice : PageDevice
{
    private readonly PageDevice _device;
    private readonly DiagnosticSink _sink;

    /// <summary>For each save the interpreter made, whether the device made it.</summary>
    private readonly Stack<bool> _saved = new();

    internal GuardedDevice(PageDevice device, DiagnosticSink sink)
    {
        _device = device;
        _sink = sink;
    }

    /// <summary>
    /// Whether the content being drawn is in optional content that is switched off. Nothing is
    /// painted while it is, but a clip still reaches the device and the interpreter keeps its
    /// graphics state, because ISO 32000-1 8.11.3.2 has hidden content change the state as
    /// visible content does.
    /// </summary>
    internal bool Hidden { get; set; }

    public override void Save()
    {
        var saved = false;
        try
        {
            _device.Save();
            saved = true;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }

        _saved.Push(saved);
    }

    public override void Restore()
    {
        // A save the device threw at left it nothing to restore.
        if (_saved.Count == 0 || !_saved.Pop())
            return;

        try
        {
            _device.Restore();
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }
    }

    public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
    {
        try
        {
            _device.Clip(path, transform, fillMode);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }
    }

    public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint)
    {
        if (Hidden)
            return;

        try
        {
            _device.Fill(path, transform, fillMode, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }
    }

    public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint)
    {
        if (Hidden)
            return;

        try
        {
            _device.Stroke(path, transform, stroke, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }
    }

    public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint)
    {
        if (Hidden)
            return;

        try
        {
            _device.DrawImage(image, transform, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
        }
    }

    public override PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data)
    {
        try
        {
            return _device.DecodeImage(codec, data);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
            return null;
        }
    }

    // A shader call that throws has begun to draw, so it answers true: nothing is drawn a second
    // time in a fallback colour.
    public override bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint)
    {
        try
        {
            return _device.TryFill(path, transform, fillMode, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
            return true;
        }
    }

    public override bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint)
    {
        try
        {
            return _device.TryStroke(path, transform, stroke, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
            return true;
        }
    }

    public override bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint)
    {
        try
        {
            return _device.TryDrawStencil(mask, transform, paint);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            Report(ex);
            return true;
        }
    }

    private void Report(Exception ex) =>
        _sink.Report(PageRenderProblem.OperatorFailed, _sink.Operator ?? _sink.Stream, ex.Message);
}
