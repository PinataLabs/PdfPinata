using System;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// The error correction an ecc200 DataMatrix carries, over the field of 256 elements that
/// ISO/IEC 16022 works in: the one built on x^8 + x^5 + x^3 + x^2 + 1, with 2 for a generator.
/// </summary>
internal static class DataMatrixReedSolomon
{
    /// <summary>The polynomial the field is built on, 0x12D.</summary>
    const int Modulus = 0x12D;

    static readonly byte[] Exponentials = new byte[255];
    static readonly byte[] Logarithms = new byte[256];

    static DataMatrixReedSolomon()
    {
        int value = 1;
        for (int power = 0; power < 255; power++)
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
        byte[] generator = Generator(errorCodewords);
        byte[] remainder = new byte[errorCodewords];

        foreach (byte datum in data)
        {
            byte feedback = (byte)(datum ^ remainder[errorCodewords - 1]);

            for (int at = errorCodewords - 1; at > 0; at--)
                remainder[at] = (byte)(remainder[at - 1] ^ Multiply(feedback, generator[at]));

            remainder[0] = Multiply(feedback, generator[0]);
        }

        // Held highest term first above, and written to the symbol the other way round.
        byte[] correction = new byte[errorCodewords];
        for (int at = 0; at < errorCodewords; at++)
            correction[at] = remainder[errorCodewords - 1 - at];

        return correction;
    }

    /// <summary>
    /// The generator polynomial of the given degree, which is the product of (x - 2^i) for i
    /// from 1 up to the degree. The leading coefficient is 1 and is not held.
    /// </summary>
    static byte[] Generator(int degree)
    {
        byte[] polynomial = new byte[degree + 1];
        polynomial[0] = 1;
        int written = 1;

        for (int root = 1; root <= degree; root++)
        {
            // Multiply by (x - 2^root), which over this field is (x + 2^root).
            polynomial[written] = polynomial[written - 1];
            for (int at = written - 1; at > 0; at--)
                polynomial[at] = (byte)(polynomial[at - 1] ^ Multiply(polynomial[at], Exponentials[root]));

            polynomial[0] = Multiply(polynomial[0], Exponentials[root]);
            written++;
        }

        // Drop the leading 1, leaving the coefficients the division needs.
        byte[] coefficients = new byte[degree];
        Array.Copy(polynomial, coefficients, degree);
        return coefficients;
    }

    static byte Multiply(byte left, byte right)
    {
        if (left == 0 || right == 0)
            return 0;

        return Exponentials[(Logarithms[left] + Logarithms[right]) % 255];
    }
}
