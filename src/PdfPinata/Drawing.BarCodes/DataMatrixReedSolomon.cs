using System;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// The error correction an ecc200 DataMatrix carries, over the field of 256 elements that
/// ISO/IEC 16022 works in: the one built on x^8 + x^5 + x^3 + x^2 + 1, with 2 for a generator.
/// </summary>
internal static class DataMatrixReedSolomon
{
    /// <summary>The polynomial the field is built on, 0x12D.</summary>
    private const int Modulus = 0x12D;

    private static readonly byte[] Exponentials = new byte[255];
    private static readonly byte[] Logarithms = new byte[256];

    static DataMatrixReedSolomon()
    {
        var value = 1;
        for (var power = 0; power < 255; power++)
        {
            Exponentials[power] = (byte)value;
            Logarithms[value] = (byte)power;

            value <<= 1;
            if (value >= 256)
                value ^= Modulus;
        }
    }

    /// <summary>
    /// The error correction codewords for one block of data codewords.
    /// </summary>
    internal static byte[] Compute(byte[] data, int errorCodewords)
    {
        var generator = Generator(errorCodewords);
        var remainder = new byte[errorCodewords];

        foreach (var datum in data)
        {
            var feedback = (byte)(datum ^ remainder[errorCodewords - 1]);

            for (var at = errorCodewords - 1; at > 0; at--)
                remainder[at] = (byte)(remainder[at - 1] ^ Multiply(feedback, generator[at]));

            remainder[0] = Multiply(feedback, generator[0]);
        }

        // Held highest term first above, and written to the symbol the other way round.
        var correction = new byte[errorCodewords];
        for (var at = 0; at < errorCodewords; at++)
            correction[at] = remainder[errorCodewords - 1 - at];

        return correction;
    }

    /// <summary>
    /// The generator polynomial of the given degree, which is the product of (x - 2^i) for i
    /// from 1 up to the degree. The leading coefficient is 1 and is not held.
    /// </summary>
    private static byte[] Generator(int degree)
    {
        var polynomial = new byte[degree + 1];
        polynomial[0] = 1;
        var written = 1;

        for (var root = 1; root <= degree; root++)
        {
            // Multiply by (x - 2^root), which over this field is (x + 2^root).
            polynomial[written] = polynomial[written - 1];
            for (var at = written - 1; at > 0; at--)
                polynomial[at] = (byte)(polynomial[at - 1] ^ Multiply(polynomial[at], Exponentials[root]));

            polynomial[0] = Multiply(polynomial[0], Exponentials[root]);
            written++;
        }

        // Drop the leading 1, leaving the coefficients the division needs.
        var coefficients = new byte[degree];
        Array.Copy(polynomial, coefficients, degree);
        return coefficients;
    }

    private static byte Multiply(byte left, byte right)
    {
        if (left == 0 || right == 0)
            return 0;

        return Exponentials[(Logarithms[left] + Logarithms[right]) % 255];
    }
}
