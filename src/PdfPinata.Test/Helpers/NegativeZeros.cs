using System.Collections.Generic;
using System.Text;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Finds the number tokens spelled as a negative zero - <c>-0</c>, <c>-0.0</c>, <c>-.0</c> - in
///   every content stream of a document: its pages', and every form XObject's, which is what an
///   annotation's appearance and a template are, and every tiling pattern's. Issue #336.
/// </summary>
/// <remarks>
///   The content-stream parser hands back a number as its value, and an integer zero has no sign
///   to keep, so this reads the tokens themselves. String literals, hexadecimal strings, comments
///   and the data of an inline image are passed over, so that a "-0" in the text a page shows is
///   not taken for a number.
/// </remarks>
internal static class NegativeZeros
{
    /// <summary>Each negative zero found, with where it was found.</summary>
    internal static List<string> In(PdfDocument document)
    {
        var found = new List<string>();
        for (var index = 0; index < document.PageCount; index++)
            Collect(PageContent.Of(document.Pages[index]), $"page {index + 1}", found);

        foreach (var item in document.Internals.GetAllObjects())
        {
            if (item is not PdfDictionary { Stream: not null } stream)
                continue;
            var isForm = stream.Elements.GetName("/Subtype") == "/Form";
            var isTilingPattern = stream.Elements.ContainsKey("/PatternType") && stream.Elements.GetInteger("/PatternType") == 1;
            if (isForm || isTilingPattern)
                Collect(stream.Stream.UnfilteredValue, $"{(isForm ? "form" : "pattern")} {stream.Reference?.ObjectNumber}", found);
        }

        return found;
    }

    private static void Collect(byte[] content, string where, List<string> found)
    {
        foreach (var token in Tokens(content))
        {
            if (IsNegativeZero(token))
                found.Add($"{where}: {token}");
        }
    }

    private static bool IsNegativeZero(string token)
    {
        if (token.Length < 2 || token[0] != '-')
            return false;
        var digits = 0;
        var points = 0;
        for (var i = 1; i < token.Length; i++)
        {
            if (token[i] == '0')
                digits++;
            else if (token[i] == '.')
                points++;
            else
                return false;
        }
        return digits > 0 && points <= 1;
    }

    private static IEnumerable<string> Tokens(byte[] content)
    {
        var token = new StringBuilder();
        var position = 0;
        while (position < content.Length)
        {
            var ch = (char)content[position];
            if (IsWhiteSpace(ch) || IsDelimiter(ch))
            {
                if (token.Length > 0)
                {
                    var finished = token.ToString();
                    token.Clear();
                    yield return finished;
                    if (finished == "ID")
                    {
                        position = AfterInlineImage(content, position);
                        continue;
                    }
                }

                if (ch == '/')
                {
                    // A name keeps its slash, so that /-0 is never taken for a number.
                    token.Append(ch);
                    position++;
                    continue;
                }

                position = ch switch
                {
                    '(' => AfterStringLiteral(content, position),
                    '<' when position + 1 < content.Length && content[position + 1] != '<' => AfterHexString(content, position),
                    '%' => AfterComment(content, position),
                    _ => position + 1
                };
                continue;
            }

            token.Append(ch);
            position++;
        }

        if (token.Length > 0)
            yield return token.ToString();
    }

    private static int AfterStringLiteral(byte[] content, int position)
    {
        var depth = 0;
        for (; position < content.Length; position++)
        {
            switch ((char)content[position])
            {
                case '\\':
                    position++;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    if (--depth == 0)
                        return position + 1;
                    break;
            }
        }
        return position;
    }

    private static int AfterHexString(byte[] content, int position)
    {
        while (position < content.Length && content[position] != '>')
            position++;
        return position + 1;
    }

    private static int AfterComment(byte[] content, int position)
    {
        while (position < content.Length && content[position] is not ((byte)'\n' or (byte)'\r'))
            position++;
        return position;
    }

    /// <summary>The position after the EI that ends an inline image's data.</summary>
    private static int AfterInlineImage(byte[] content, int position)
    {
        for (position++; position + 2 < content.Length; position++)
        {
            if (IsWhiteSpace((char)content[position]) && content[position + 1] == 'E' && content[position + 2] == 'I'
                && (position + 3 == content.Length || IsWhiteSpace((char)content[position + 3])))
                return position + 3;
        }
        return content.Length;
    }

    private static bool IsWhiteSpace(char ch) => ch is ' ' or '\n' or '\r' or '\t' or '\f' or '\0';

    private static bool IsDelimiter(char ch) => ch is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%';
}
