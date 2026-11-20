using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>The eight text render modes, as <c>Tr</c> numbers them.</summary>
internal enum TextRenderMode
{
    Fill = 0,
    Stroke = 1,
    FillStroke = 2,
    Invisible = 3,
    FillClip = 4,
    StrokeClip = 5,
    FillStrokeClip = 6,
    Clip = 7
}

/// <summary>
/// Everything <c>q</c> saves and <c>Q</c> puts back: the transform, the colours, the line state
/// and the text state. The clip is the one part of the graphics state that is not here. The
/// device keeps it, and <see cref="StateStack"/> saves and restores the two together.
/// </summary>
/// <remarks>
/// Every field is a value or an immutable object, so a copy is a copy of the fields.
/// </remarks>
internal sealed class GraphicsState
{
    /// <summary>
    /// Maps user space to device space. It starts as the <see cref="PageView"/>'s transform, so
    /// there is no second matrix to remember to apply.
    /// </summary>
    internal XMatrix Ctm;

    internal RenderColorSpace FillSpace = ColorSpaces.DeviceGray;
    internal RenderColorSpace StrokeSpace = ColorSpaces.DeviceGray;

    /// <summary>The fill colour as RGB, resolved when it was set and not each time it is painted with.</summary>
    internal PagePaint FillColor = new(0, 0, 0);

    internal PagePaint StrokeColor = new(0, 0, 0);

    /// <summary><c>/ca</c>.</summary>
    internal double FillAlpha = 1;

    /// <summary><c>/CA</c>.</summary>
    internal double StrokeAlpha = 1;

    internal PageStroke Stroke = PageStroke.Initial;

    /// <summary>The font <c>Tf</c> selected, or null when it named none the resources hold.</summary>
    internal RenderFont Font;

    /// <summary>The name <c>Tf</c> selected the font by, or null before any <c>Tf</c>.</summary>
    internal string FontName;

    internal double FontSize;
    internal double CharacterSpacing;
    internal double WordSpacing;

    /// <summary><c>Tz</c> as a factor: 1 where the operand is 100.</summary>
    internal double HorizontalScaling = 1;

    internal double Leading;
    internal double Rise;
    internal TextRenderMode RenderMode;

    internal PagePaint FillPaint => FillColor.WithAlpha(FillAlpha);

    internal PagePaint StrokePaint => StrokeColor.WithAlpha(StrokeAlpha);

    internal GraphicsState Copy() => (GraphicsState)MemberwiseClone();
}

/// <summary>
/// The stack <c>q</c> and <c>Q</c> work on, and the device's own, as one thing: every push saves
/// the device and every pop restores it.
/// </summary>
/// <remarks>
/// Nothing else calls <see cref="PageDevice.Save"/> or <see cref="PageDevice.Restore"/>. That is
/// what makes the balance a device is promised hold whatever the content does: the two depths
/// cannot come apart, and the render ends by popping to nothing.
/// </remarks>
internal sealed class StateStack
{
    private readonly PageDevice _device;
    private readonly List<GraphicsState> _saved = [];

    internal StateStack(PageDevice device, GraphicsState initial)
    {
        _device = device;
        Current = initial;
    }

    /// <summary>The graphics state in force.</summary>
    internal GraphicsState Current { get; private set; }

    /// <summary>How many states are saved.</summary>
    internal int Depth => _saved.Count;

    internal void Push()
    {
        _saved.Add(Current);
        Current = Current.Copy();
        _device.Save();
    }

    /// <summary>Pops down to a depth, restoring the device once for each state popped.</summary>
    internal void PopTo(int depth)
    {
        while (_saved.Count > depth)
        {
            Current = _saved[^1];
            _saved.RemoveAt(_saved.Count - 1);
            _device.Restore();
        }
    }
}
