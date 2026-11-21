using System.Text;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// The packed data of a mesh shading, written a value at a time: flags and colour components of
/// 8 bits, coordinates of 16. With a <c>/Decode</c> of <c>[0 65535 0 65535 …]</c> a coordinate
/// is the number written.
/// </summary>
internal sealed class MeshData
{
    private readonly StringBuilder _data = new();

    public MeshData Flag(int flag)
    {
        _data.Append((char)flag);
        return this;
    }

    public MeshData Point(int x, int y)
    {
        _data.Append((char)(x >> 8)).Append((char)(x & 255)).Append((char)(y >> 8)).Append((char)(y & 255));
        return this;
    }

    /// <summary>Colour components, or the one input of the shading's function, each from 0 to 255.</summary>
    public MeshData Color(params int[] components)
    {
        foreach (var component in components)
            _data.Append((char)component);
        return this;
    }

    /// <summary>The data, one character for each byte, as <see cref="HandWritten.Stream"/> takes it.</summary>
    public override string ToString() => _data.ToString();
}
