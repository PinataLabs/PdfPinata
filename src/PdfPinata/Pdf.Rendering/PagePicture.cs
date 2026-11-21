using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Device calls, kept: what a tiling pattern's cell draws, recorded once and drawn wherever a
/// device wants it.
/// </summary>
/// <remarks>
/// Immutable and in balance: every save in it is restored in it. It holds no encoded PDF. Each
/// call is what a <see cref="PageDevice"/> would have been handed, in the coordinates of the
/// shader the picture belongs to.
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PagePicture
{
    private readonly PictureCall[] _calls;

    internal PagePicture(PictureCall[] calls)
    {
        _calls = calls;
    }

    /// <summary>Whether the picture paints nothing.</summary>
    public bool IsEmpty => Array.TrueForAll(_calls, call => call.Kind <= PictureCallKind.Clip);

    /// <summary>
    /// Makes every call of the picture on a device, in the order it was recorded, each transform
    /// followed by <paramref name="transform"/>.
    /// </summary>
    /// <param name="device">What to draw onto.</param>
    /// <param name="transform">Maps the picture's coordinates to the device's.</param>
    /// <returns>
    /// False where the picture paints with a shader the device answered false to. That part is
    /// drawn in one colour, as page rendering draws it on such a device.
    /// </returns>
    /// <remarks>
    /// The device has been restored as often as it was saved when this returns, whatever it threw.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="device"/> is null.</exception>
    public bool Replay(PageDevice device, XMatrix transform)
    {
        ArgumentNullException.ThrowIfNull(device);

        var complete = true;
        var saved = 0;
        try
        {
            foreach (var call in _calls)
            {
                var placed = XMatrix.Multiply(call.Transform, transform);
                switch (call.Kind)
                {
                    case PictureCallKind.Save:
                        device.Save();
                        saved++;
                        break;

                    case PictureCallKind.Restore:
                        saved--;
                        device.Restore();
                        break;

                    case PictureCallKind.Clip:
                        device.Clip(call.Path, placed, call.FillMode);
                        break;

                    default:
                        complete &= Paint(device, call, placed, transform);
                        break;
                }
            }
        }
        finally
        {
            for (; saved > 0; saved--)
                device.Restore();
        }

        return complete;
    }

    private static bool Paint(PageDevice device, PictureCall call, XMatrix placed, XMatrix transform)
    {
        var shader = call.Shader;
        var drawn = true;
        if (shader != null)
        {
            var paint = new PageShaderPaint(shader, XMatrix.Multiply(call.ShaderTransform, transform), call.Paint.Alpha);
            drawn = call.Kind switch
            {
                PictureCallKind.Fill => device.TryFill(call.Path, placed, call.FillMode, paint),
                PictureCallKind.Stroke => device.TryStroke(call.Path, placed, call.Stroke, paint),
                _ => device.TryDrawStencil(call.Image, placed, paint)
            };

            if (drawn || shader.Fallback == null)
                return drawn;
        }

        var solid = shader == null ? call.Paint : shader.Fallback.Value.WithAlpha(call.Paint.Alpha);
        switch (call.Kind)
        {
            case PictureCallKind.Fill:
                device.Fill(call.Path, placed, call.FillMode, solid);
                break;

            case PictureCallKind.Stroke:
                device.Stroke(call.Path, placed, call.Stroke, solid);
                break;

            default:
                device.DrawImage(call.Image, placed, solid);
                break;
        }

        return drawn;
    }

    /// <summary>
    /// The colour the picture is on the whole: the mean of what it paints with, each call counted
    /// once, or null where it paints nothing.
    /// </summary>
    internal PagePaint? MeanColor
    {
        get
        {
            double red = 0, green = 0, blue = 0;
            var count = 0;
            foreach (var call in _calls)
            {
                if (call.Kind <= PictureCallKind.Clip)
                    continue;

                var color = call.Shader == null ? call.Paint : call.Shader.Fallback;
                if (color == null)
                    continue;

                red += color.Value.Red;
                green += color.Value.Green;
                blue += color.Value.Blue;
                count++;
            }

            return count == 0 ? null : new PagePaint(red / count, green / count, blue / count);
        }
    }
}

// In this order: the three that paint nothing come first.
internal enum PictureCallKind : byte
{
    Save,
    Restore,
    Clip,
    Fill,
    Stroke,
    Image
}

/// <summary>One call of a <see cref="PagePicture"/>, with what the device was handed.</summary>
internal sealed class PictureCall(PictureCallKind kind)
{
    internal PictureCallKind Kind { get; } = kind;
    internal PagePath Path { get; init; }
    internal PageImage Image { get; init; }
    internal XMatrix Transform { get; init; } = XMatrix.Identity;
    internal XFillMode FillMode { get; init; }
    internal PageStroke Stroke { get; init; }

    /// <summary>The colour, or for a shader only the alpha.</summary>
    internal PagePaint Paint { get; init; }

    /// <summary>Null where <see cref="Paint"/> is all there is.</summary>
    internal PageShader Shader { get; init; }

    internal XMatrix ShaderTransform { get; init; }
}

/// <summary>
/// A device that draws nothing and keeps every call it is given as a <see cref="PagePicture"/>.
/// </summary>
/// <remarks>
/// It answers true to every shader, so what it records is what the content asked for and not what
/// some device could draw. <see cref="PagePicture.Replay"/> settles that against the device it is
/// replayed on.
/// </remarks>
internal sealed class PictureRecorder : PageDevice
{
    /// <summary>
    /// The most calls one picture keeps. Past it the rest of the content is not recorded, but for
    /// the restores that keep the picture in balance.
    /// </summary>
    internal const int MaximumCalls = 1 << 16;

    private static readonly PictureCall SaveCall = new(PictureCallKind.Save);
    private static readonly PictureCall RestoreCall = new(PictureCallKind.Restore);

    private readonly List<PictureCall> _calls = [];
    private readonly PageDevice _decoder;
    private int _unrecordedSaves;

    /// <param name="decoder">The device asked to decode what this one is asked to, or null for none.</param>
    internal PictureRecorder(PageDevice decoder = null)
    {
        _decoder = decoder;
    }

    /// <summary>Whether content was left out at <see cref="MaximumCalls"/>.</summary>
    internal bool Overflowed { get; private set; }

    internal PagePicture ToPicture() => new(_calls.ToArray());

    public override void Save()
    {
        if (IsFull())
            _unrecordedSaves++;
        else
            _calls.Add(SaveCall);
    }

    public override void Restore()
    {
        if (_unrecordedSaves > 0)
            _unrecordedSaves--;
        else
            _calls.Add(RestoreCall);
    }

    public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
    {
        if (!IsFull())
            _calls.Add(new PictureCall(PictureCallKind.Clip) { Path = path, Transform = transform, FillMode = fillMode });
    }

    public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint) =>
        Add(new PictureCall(PictureCallKind.Fill) { Path = path, Transform = transform, FillMode = fillMode, Paint = paint });

    public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint) =>
        Add(new PictureCall(PictureCallKind.Stroke) { Path = path, Transform = transform, Stroke = stroke, Paint = paint });

    public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint) =>
        Add(new PictureCall(PictureCallKind.Image) { Image = image, Transform = transform, Paint = paint });

    public override PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data) =>
        _decoder?.DecodeImage(codec, data);

    public override bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint) =>
        Add(new PictureCall(PictureCallKind.Fill)
        {
            Path = path, Transform = transform, FillMode = fillMode, Paint = AlphaOf(paint), Shader = paint.Shader,
            ShaderTransform = paint.Transform
        });

    public override bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint) =>
        Add(new PictureCall(PictureCallKind.Stroke)
        {
            Path = path, Transform = transform, Stroke = stroke, Paint = AlphaOf(paint), Shader = paint.Shader,
            ShaderTransform = paint.Transform
        });

    public override bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint) =>
        Add(new PictureCall(PictureCallKind.Image)
        {
            Image = mask, Transform = transform, Paint = AlphaOf(paint), Shader = paint.Shader,
            ShaderTransform = paint.Transform
        });

    private static PagePaint AlphaOf(PageShaderPaint paint) => new(0, 0, 0, paint.Alpha);

    private bool Add(PictureCall call)
    {
        if (!IsFull())
            _calls.Add(call);
        return true;
    }

    private bool IsFull()
    {
        if (_calls.Count < MaximumCalls)
            return false;

        Overflowed = true;
        return true;
    }
}
