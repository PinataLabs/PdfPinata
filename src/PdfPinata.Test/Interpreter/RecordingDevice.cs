using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// A device that draws nothing and writes down every call it is given, so that a test can say
/// what a content stream came to.
/// </summary>
internal sealed class RecordingDevice : PageDevice
{
    public List<Call> Calls { get; } = [];

    /// <summary>Every call, one line each, with its path in device space.</summary>
    public IEnumerable<string> Lines => Calls.Select(call => call.ToString());

    /// <summary>
    /// The calls the content itself came to: all of them but the save and the clip to the page's
    /// box every render opens with, and the restore it closes with.
    /// </summary>
    public IEnumerable<string> Body => Lines.Skip(2).Take(Calls.Count - 3);

    public IEnumerable<Call> Fills => Calls.Where(call => call.Kind == "fill");

    public IEnumerable<Call> Strokes => Calls.Where(call => call.Kind == "stroke");

    public IEnumerable<Call> Clips => Calls.Where(call => call.Kind == "clip").Skip(1);

    public override void Save() => Calls.Add(new Call("save"));

    public override void Restore() => Calls.Add(new Call("restore"));

    public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode) =>
        Calls.Add(new Call("clip") { Path = path, Transform = transform, FillMode = fillMode });

    public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint) =>
        Calls.Add(new Call("fill") { Path = path, Transform = transform, FillMode = fillMode, Paint = paint });

    public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint) =>
        Calls.Add(new Call("stroke") { Path = path, Transform = transform, Stroke = stroke, Paint = paint });

    public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint) =>
        Calls.Add(new Call("image") { Transform = transform, Paint = paint });

    /// <summary>
    /// Whether the device draws shaders. Where it does not it answers false to each shader call
    /// and records nothing of it, as a device written before there were any.
    /// </summary>
    public bool DrawsShaders { get; init; } = true;

    public override bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint)
    {
        if (DrawsShaders)
            Calls.Add(new Call("fill") { Path = path, Transform = transform, FillMode = fillMode, Shader = paint });
        return DrawsShaders;
    }

    public override bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint)
    {
        if (DrawsShaders)
            Calls.Add(new Call("stroke") { Path = path, Transform = transform, Stroke = stroke, Shader = paint });
        return DrawsShaders;
    }

    public override bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint)
    {
        if (DrawsShaders)
            Calls.Add(new Call("image") { Transform = transform, Shader = paint });
        return DrawsShaders;
    }

    internal sealed class Call(string kind)
    {
        public string Kind { get; } = kind;
        public PagePath Path { get; init; }
        public XMatrix Transform { get; init; }
        public XFillMode FillMode { get; init; }
        public PagePaint Paint { get; init; }
        public PageStroke Stroke { get; init; }

        /// <summary>What a shader call painted with, or null for a call that painted in one colour.</summary>
        public PageShaderPaint? Shader { get; init; }

        /// <summary>The shader of a shader call, as the kind a test expects it to be.</summary>
        public T ShaderAs<T>() where T : PageShader => (T)Shader!.Value.Shader;

        /// <summary>A gradient's stops, as <c>offset rgb(r,g,b)</c>.</summary>
        public string[] Stops => ShaderAs<PageGradient>().Stops
            .Select(stop => Number(stop.Offset) + " " + ColourOf(stop.Color)).ToArray();

        /// <summary>Where the origin of the path's own coordinates lands on the device.</summary>
        public XPoint Origin => Transform.Transform(new XPoint(0, 0));

        /// <summary>Where a point of the path's own coordinates lands on the device.</summary>
        public XPoint At(double x, double y) => Transform.Transform(new XPoint(x, y));

        /// <summary>The path as the device is to draw it: every point through the transform.</summary>
        public string DevicePath
        {
            get
            {
                var text = new StringBuilder();
                var points = Path.Points;
                var at = 0;
                foreach (var verb in Path.Verbs)
                {
                    if (text.Length > 0)
                        text.Append(' ');

                    switch (verb)
                    {
                        case PagePathVerb.MoveTo:
                            text.Append('M').Append(Point(points[at++]));
                            break;
                        case PagePathVerb.LineTo:
                            text.Append('L').Append(Point(points[at++]));
                            break;
                        case PagePathVerb.CubicTo:
                            text.Append('C').Append(Point(points[at++])).Append(' ').Append(Point(points[at++]))
                                .Append(' ').Append(Point(points[at++]));
                            break;
                        default:
                            text.Append('Z');
                            break;
                    }
                }

                return text.ToString();
            }
        }

        public override string ToString() => Kind switch
        {
            "clip" => $"clip {Mode} {DevicePath}",
            "fill" => $"fill {Mode} {DevicePath} {Colour}",
            "stroke" => $"stroke {DevicePath} {Colour}",
            _ => Kind
        };

        private string Mode => FillMode == XFillMode.Winding ? "nonzero" : "evenodd";

        private string Colour => Shader == null
            ? ColourOf(Paint)
            : ShaderName + (Shader.Value.Alpha < 1 ? " a" + Number(Shader.Value.Alpha) : "");

        private string ShaderName => Shader!.Value.Shader switch
        {
            PageAxialShading => "axial",
            PageRadialShading => "radial",
            PageMeshShading => "mesh",
            PageTiling => "tiling",
            _ => "shader"
        };

        private static string ColourOf(PagePaint paint) =>
            $"rgb({Number(paint.Red)},{Number(paint.Green)},{Number(paint.Blue)})" +
            (paint.Alpha < 1 ? " a" + Number(paint.Alpha) : "");

        private string Point(XPoint point)
        {
            var mapped = Transform.Transform(point);
            return Number(mapped.X) + "," + Number(mapped.Y);
        }

        private static string Number(double value) =>
            (Math.Round(value, 3) + 0.0).ToString("0.###", CultureInfo.InvariantCulture);
    }
}
