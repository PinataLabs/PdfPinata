// The span overloads of string.Concat and Enum.Parse, for the netstandard2.1 leg only. See README.md
// beside this file.

using System.Buffers;

namespace System
{
    internal static class StringConcatPolyfill
    {
        extension(string)
        {
            /// <summary>The concatenation of <paramref name="str0"/> and <paramref name="str1"/>.</summary>
            public static string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
            {
                int length = str0.Length + str1.Length;
                if (length == 0)
                    return string.Empty;

                // The one string returned is the one allocation, as on the runtime: the characters are
                // gathered in a rented buffer rather than in two intermediate strings.
                char[] buffer = ArrayPool<char>.Shared.Rent(length);
                try
                {
                    str0.CopyTo(buffer);
                    str1.CopyTo(buffer.AsSpan(str0.Length));
                    return new string(buffer, 0, length);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
        }
    }

    internal static class EnumParseSpanPolyfill
    {
        extension(Enum)
        {
            /// <summary>Parses the name or value of a constant of <paramref name="enumType"/>.</summary>
            public static object Parse(Type enumType, ReadOnlySpan<char> value, bool ignoreCase)
                => Enum.Parse(enumType, value.ToString(), ignoreCase);
        }
    }
}
