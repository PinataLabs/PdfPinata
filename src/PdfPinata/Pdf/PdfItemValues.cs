using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf;

/// <summary>
/// Converting an item to a scalar, the one way every accessor does it. Each conversion follows an
/// indirect reference first, since any object in a file may be indirect, and then takes the
/// simple type and its object twin together - <see cref="PdfInteger"/> with
/// <see cref="PdfIntegerObject"/> and so on - so that the pairing is spelled out here and nowhere
/// else. Each answers false rather than throwing, leaving the caller to say what a value of the
/// wrong type means.
/// </summary>
/// <remarks>
/// The rules, which <c>ScalarConversionTests</c> pins for both collections:
/// <list type="bullet">
/// <item>Asked for a real, every number is one, whatever its width.</item>
/// <item>Asked for an integer, an integer of any width is one when it fits in an int. One that does
/// not is refused rather than wrapped round to some other number, and a real is not one.</item>
/// <item>A name is text only where the caller says so, and keeps its slash.</item>
/// <item>The null object is no value, in every shape it comes in: see <see cref="IsNull"/>.</item>
/// </list>
/// Strings and names are byte strings, one char per byte, and are handed on as they are.
/// </remarks>
internal static class PdfItemValues
{
    /// <summary>
    /// Whether the item is the null object in any of its shapes: no item at all,
    /// <see cref="PdfNull"/>, <see cref="PdfNullObject"/>, or a reference to one of those or to
    /// nothing.
    /// </summary>
    internal static bool IsNull(PdfItem item) => PdfReference.Dereference(item) is null or PdfNull or PdfNullObject;

    internal static bool TryGetBoolean(PdfItem item, out bool value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfBoolean boolean:
                value = boolean.Value;
                return true;

            case PdfBooleanObject booleanObject:
                value = booleanObject.Value;
                return true;

            default:
                value = false;
                return false;
        }
    }

    internal static bool TryGetInteger(PdfItem item, out int value)
    {
        if (TryGetLong(item, out var whole) && whole is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)whole;
            return true;
        }

        value = 0;
        return false;
    }

    internal static bool TryGetNumber(PdfItem item, out double value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfReal real:
                value = real.Value;
                return true;

            case PdfRealObject realObject:
                value = realObject.Value;
                return true;

            default:
                var isWhole = TryGetLong(item, out var whole);
                value = whole;
                return isWhole;
        }
    }

    /// <summary>
    /// The bytes of a string as the file holds them. The lexer takes a string opening FE FF for
    /// UTF-16BE text, decodes it and drops the mark, which is right for text and wrong for bytes
    /// that open so by chance - a palette, a key in an encryption dictionary, a document
    /// identifier - so the mark is put back, and the zero the lexer completed an odd number of
    /// bytes with is taken off. A string read as FF FE keeps the mark in its bytes.
    /// </summary>
    internal static bool TryGetBytes(PdfItem item, out byte[] value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfString text:
                value = AsInFile(text.EncryptionValue, text.Flags);
                return true;

            case PdfStringObject textObject:
                value = AsInFile(textObject.EncryptionValue, textObject.Flags);
                return true;

            default:
                value = null;
                return false;
        }

        static byte[] AsInFile(byte[] bytes, PdfStringFlags flags)
        {
            if ((flags & PdfStringFlags.EncodingMask) == PdfStringFlags.Unicode && !PdfString.IsLittleEndianMark(flags))
                bytes = [0xFE, 0xFF, .. bytes];
            return (flags & PdfStringFlags.OddLength) != 0 ? bytes[..^1] : bytes;
        }
    }

    /// <summary>
    /// The text of a string, or of a name where <paramref name="allowName"/> says a name will do,
    /// in which case it keeps the slash it is written with.
    /// </summary>
    internal static bool TryGetText(PdfItem item, bool allowName, out string value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfString text:
                value = text.Value;
                return true;

            case PdfStringObject textObject:
                value = textObject.Value;
                return true;

            default:
                return allowName ? TryGetName(item, out value) : NoText(out value);
        }
    }

    /// <summary>
    /// The value of a name, with the slash it is written with.
    /// </summary>
    internal static bool TryGetName(PdfItem item, out string value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfName name:
                value = name.Value;
                return true;

            case PdfNameObject nameObject:
                value = nameObject.Value;
                return true;

            default:
                return NoText(out value);
        }
    }

    /// <summary>
    /// An integer of any of the three widths an integer is read or written in.
    /// </summary>
    internal static bool TryGetLong(PdfItem item, out long value)
    {
        switch (PdfReference.Dereference(item))
        {
            case PdfInteger integer:
                value = integer.Value;
                return true;

            case PdfIntegerObject integerObject:
                value = integerObject.Value;
                return true;

            case PdfUInteger uinteger:
                value = uinteger.Value;
                return true;

            case PdfUIntegerObject uintegerObject:
                value = uintegerObject.Value;
                return true;

            case PdfLong longInteger:
                value = longInteger.Value;
                return true;

            case PdfLongObject longObject:
                value = longObject.Value;
                return true;

            default:
                value = 0;
                return false;
        }
    }

    private static bool NoText(out string value)
    {
        value = null;
        return false;
    }
}
