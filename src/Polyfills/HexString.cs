// Convert.ToHexString, for the netstandard2.1 leg only. See README.md beside this file.

namespace System
{
    internal static class ConvertToHexStringPolyfill
    {
        extension(Convert)
        {
            /// <summary>The bytes of <paramref name="inArray"/> as upper-case hexadecimal, two digits to a byte.</summary>
            public static string ToHexString(byte[] inArray)
            {
                if (inArray is null)
                    throw new ArgumentNullException(nameof(inArray));
                return Encode(inArray);
            }

            /// <summary>The bytes of <paramref name="bytes"/> as upper-case hexadecimal, two digits to a byte.</summary>
            public static string ToHexString(ReadOnlySpan<byte> bytes) => Encode(bytes);
        }

        private static string Encode(ReadOnlySpan<byte> bytes)
        {
            if (bytes.IsEmpty)
                return string.Empty;

            const string digits = "0123456789ABCDEF";
            var chars = new char[bytes.Length * 2];
            for (var idx = 0; idx < bytes.Length; idx++)
            {
                chars[2 * idx] = digits[bytes[idx] >> 4];
                chars[2 * idx + 1] = digits[bytes[idx] & 0xF];
            }
            return new string(chars);
        }
    }
}
