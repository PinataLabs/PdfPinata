// The generic Enum members, for the netstandard2.1 leg only. See README.md beside this file.

namespace System
{
    internal static class EnumPolyfill
    {
        extension(Enum)
        {
            /// <summary>Whether <paramref name="value"/> is a named constant of <typeparamref name="TEnum"/>.</summary>
            public static bool IsDefined<TEnum>(TEnum value) where TEnum : struct, Enum
                => Enum.IsDefined(typeof(TEnum), value);

            /// <summary>Parses the name or value of a constant of <typeparamref name="TEnum"/>.</summary>
            public static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum
                => (TEnum)Enum.Parse(typeof(TEnum), value);

            /// <summary>Parses the name or value of a constant of <typeparamref name="TEnum"/>.</summary>
            public static TEnum Parse<TEnum>(string value, bool ignoreCase) where TEnum : struct, Enum
                => (TEnum)Enum.Parse(typeof(TEnum), value, ignoreCase);

            /// <summary>The names of the constants of <typeparamref name="TEnum"/>.</summary>
            public static string[] GetNames<TEnum>() where TEnum : struct, Enum
                => Enum.GetNames(typeof(TEnum));

            /// <summary>The values of the constants of <typeparamref name="TEnum"/>.</summary>
            public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum
                => (TEnum[])Enum.GetValues(typeof(TEnum));
        }
    }
}
