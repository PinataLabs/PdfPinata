namespace PdfPinata.Pdf.Security;

/// <summary>
/// The RC4 stream cipher, which the standard security handler encrypts and decrypts with up to
/// revision 4. Encrypting and decrypting are the same operation.
/// </summary>
/// <remarks>
/// One instance serves many keys: <see cref="SetKey(byte[], int, int)"/> starts the keystream
/// again from the beginning, and <see cref="Apply(byte[], int, int, byte[])"/> carries on from
/// wherever the last call left it.
/// </remarks>
internal sealed class Rc4
{
    private readonly byte[] _state = new byte[256];
    private int _x;
    private int _y;

    /// <summary>
    /// Runs the key schedule over <paramref name="length"/> bytes of <paramref name="key"/>,
    /// starting at <paramref name="offset"/>.
    /// </summary>
    public void SetKey(byte[] key, int offset, int length)
    {
        for (var i = 0; i < 256; i++)
            _state[i] = (byte)i;

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (key[offset + i % length] + _state[i] + j) & 255;
            (_state[i], _state[j]) = (_state[j], _state[i]);
        }
        _x = 0;
        _y = 0;
    }

    /// <summary>Runs the key schedule over the whole of <paramref name="key"/>.</summary>
    public void SetKey(byte[] key) => SetKey(key, 0, key.Length);

    /// <summary>
    /// XORs the next <paramref name="length"/> bytes of keystream with <paramref name="input"/>
    /// from <paramref name="offset"/> on, writing them to the same place in
    /// <paramref name="output"/>, which may be <paramref name="input"/> itself.
    /// </summary>
    public void Apply(byte[] input, int offset, int length, byte[] output)
    {
        for (var i = offset; i < offset + length; i++)
        {
            _x = (_x + 1) & 255;
            _y = (_state[_x] + _y) & 255;
            (_state[_x], _state[_y]) = (_state[_y], _state[_x]);
            output[i] = (byte)(input[i] ^ _state[(_state[_x] + _state[_y]) & 255]);
        }
    }

    /// <summary>Encrypts or decrypts the whole of <paramref name="data"/> in place.</summary>
    public void Apply(byte[] data) => Apply(data, 0, data.Length, data);
}
