using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace SampleApp.Infrastructure;

/// <summary>
///   Turns C# into Spectre markup. Line based and approximate: it colours comments, string and
///   character literals, numbers and a fixed set of keywords, and leaves everything else alone.
/// </summary>
/// <remarks>
///   Not a parser and not trying to be. What it must get right is escaping - demo source is full of
///   <c>[</c> from attributes and array types, and an unescaped one is a Spectre parse error at
///   run time rather than a compile error. Every piece of text therefore goes through
///   <see cref="Markup.Escape"/> before any tag is put round it.
/// </remarks>
public static class CSharpHighlighter
{
    private const string CommentStyle = "grey50";
    private const string StringStyle = "darkseagreen4";
    private const string KeywordStyle = "steelblue1";
    private const string NumberStyle = "wheat4";

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "var",
        "virtual", "void", "while", "nameof", "when", "where", "yield", "record", "init", "with"
    };

    /// <summary>Highlights one line. The result is Spectre markup, already escaped.</summary>
    public static string Highlight(string line)
    {
        var markup = new StringBuilder(line.Length + 32);
        var index = 0;

        while (index < line.Length)
        {
            var (end, style) = NextToken(line, index);
            Append(markup, line.Substring(index, end - index), style);
            index = end;
        }

        return markup.ToString();
    }

    /// <summary>
    ///   The token that begins at <paramref name="index"/>: where it ends, and the style it is
    ///   coloured in, or null for text left as it is. Anything unrecognised is one character long.
    /// </summary>
    private static (int End, string? Style) NextToken(string line, int index)
    {
        var current = line[index];

        // A comment runs to the end of the line.
        if (current == '/' && NextIs(line, index, '/'))
            return (line.Length, CommentStyle);

        if (IsStringStart(line, index))
            return (EndOfString(line, index), StringStyle);

        if (current == '\'')
            return (EndOfChar(line, index), StringStyle);

        if (char.IsLetter(current) || current == '_')
            return Word(line, index);

        if (char.IsDigit(current))
            return (EndOf(line, index, c => char.IsLetterOrDigit(c) || c == '.'), NumberStyle);

        return (index + 1, null);
    }

    private static bool IsStringStart(string line, int index) =>
        line[index] == '"' || (line[index] == '@' && NextIs(line, index, '"'));

    /// <summary>An identifier or keyword, coloured only if it is a keyword.</summary>
    private static (int End, string? Style) Word(string line, int index)
    {
        var end = EndOf(line, index, c => char.IsLetterOrDigit(c) || c == '_');
        return (end, Keywords.Contains(line.Substring(index, end - index)) ? KeywordStyle : null);
    }

    private static bool NextIs(string line, int index, char expected) =>
        index + 1 < line.Length && line[index + 1] == expected;

    /// <summary>Where the run of characters that <paramref name="belongs"/> accepts, starting at <paramref name="start"/>, ends.</summary>
    private static int EndOf(string line, int start, Func<char, bool> belongs)
    {
        var end = start;
        while (end < line.Length && belongs(line[end]))
            end++;
        return end;
    }

    private static void Append(StringBuilder markup, string text, string? style)
    {
        var escaped = Markup.Escape(text);
        if (style is null)
            markup.Append(escaped);
        else
            markup.Append('[').Append(style).Append(']').Append(escaped).Append("[/]");
    }

    private static int EndOfString(string line, int start)
    {
        var verbatim = line[start] == '@';
        var index = start + (verbatim ? 2 : 1);

        while (index < line.Length)
        {
            var escape = EscapeLength(line, index, verbatim);
            if (escape > 0)
            {
                index += escape;
                continue;
            }

            if (line[index] == '"')
                return index + 1;

            index++;
        }

        // An unterminated literal means the string runs to the end of the line - which happens
        // legitimately for a verbatim string spanning lines, and is the only sensible answer
        // anywhere else too.
        return line.Length;
    }

    /// <summary>How many characters the escape sequence at <paramref name="index"/> takes, or 0 for none.</summary>
    private static int EscapeLength(string line, int index, bool verbatim)
    {
        if (!verbatim)
            return line[index] == '\\' ? 2 : 0;

        // In a verbatim string a doubled quote is an escaped quote, not the end of it.
        return line[index] == '"' && NextIs(line, index, '"') ? 2 : 0;
    }

    private static int EndOfChar(string line, int start)
    {
        var index = start + 1;
        while (index < line.Length)
        {
            switch (line[index])
            {
                case '\\':
                    index += 2;
                    continue;
                case '\'':
                    return index + 1;
                default:
                    index++;
                    break;
            }
        }

        return line.Length;
    }
}
