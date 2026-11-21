using System.Text;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// The packed data of a triangle mesh whose flags, coordinates and colour components are each of
/// any width, the high bit first, and each vertex padded to a whole byte as ISO 32000-1 8.7.4.5.5
/// lays it out. A flag of no bits is not written, which is a lattice's vertex.
/// </summary>
internal sealed class PackedMesh(int flagBits, int coordinateBits, int componentBits)
{
    private readonly StringBuilder _data = new();
    private int _byte;
    private int _bits;

    public PackedMesh Vertex(int flag, int x, int y, params int[] components)
    {
        Write(flag, flagBits);
        Write(x, coordinateBits);
        Write(y, coordinateBits);
        foreach (var component in components)
            Write(component, componentBits);

        if (_bits > 0)
            Write(0, 8 - _bits);
        return this;
    }

    /// <summary>The data, one character for each byte, as <see cref="HandWritten.Stream"/> takes it.</summary>
    public override string ToString() => _data.ToString();

    private void Write(int value, int width)
    {
        for (var bit = width - 1; bit >= 0; bit--)
        {
            _byte = _byte << 1 | (value >> bit & 1);
            if (++_bits < 8)
                continue;

            _data.Append((char)_byte);
            (_byte, _bits) = (0, 0);
        }
    }
}
