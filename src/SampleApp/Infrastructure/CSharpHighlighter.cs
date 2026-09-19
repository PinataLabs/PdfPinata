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
    const string CommentStyle = "grey50";
    const string StringStyle = "darkseagreen4";
    const string KeywordStyle = "steelblue1";
    const string NumberStyle = "wheat4";

    static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "var",
        "virtual", "void", "while", "nameof", "when", "where", "yield", "record", "init", "with",
    };

    /// <summary>Highlights one line. The result is Spectre markup, already escaped.</summary>
    public static string Highlight(string line)
    {
        StringBuilder markup = new StringBuilder(line.Length + 32);
        int index = 0;

        while (index < line.Length)
        {
            char current = line[index];

            if (current == '/' && index + 1 < line.Length && line[index + 1] == '/')
            {
                Append(markup, line.Substring(index), CommentStyle);
                break;
            }

            if (current == '"' || (current == '@' && index + 1 < line.Length && line[index + 1] == '"'))
            {
                int end = EndOfString(line, index);
                Append(markup, line.Substring(index, end - index), StringStyle);
                index = end;
                continue;
            }

            if (current == '\'')
            {
                int end = EndOfChar(line, index);
                Append(markup, line.Substring(index, end - index), StringStyle);
                index = end;
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                int end = index;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
                    end++;

                string word = line.Substring(index, end - index);
                Append(markup, word, Keywords.Contains(word) ? KeywordStyle : null);
                index = end;
                continue;
            }

            if (char.IsDigit(current))
            {
                int end = index;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '.'))
                    end++;

                Append(markup, line.Substring(index, end - index), NumberStyle);
                index = end;
                continue;
            }

            Append(markup, current.ToString(), null);
            index++;
        }

        return markup.ToString();
    }

    static void Append(StringBuilder markup, string text, string? style)
    {
        string escaped = Markup.Escape(text);
        if (style is null)
            markup.Append(escaped);
        else
            markup.Append('[').Append(style).Append(']').Append(escaped).Append("[/]");
    }

    static int EndOfString(string line, int start)
    {
        bool verbatim = line[start] == '@';
        int index = start + (verbatim ? 2 : 1);

        while (index < line.Length)
        {
            if (line[index] == '\\' && !verbatim)
            {
                index += 2;
                continue;
            }

            if (line[index] == '"')
            {
                // In a verbatim string a doubled quote is an escaped quote, not the end of it.
                if (verbatim && index + 1 < line.Length && line[index + 1] == '"')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        // An unterminated literal means the string runs to the end of the line - which happens
        // legitimately for a verbatim string spanning lines, and is the only sensible answer
        // anywhere else too.
        return line.Length;
    }

    static int EndOfChar(string line, int start)
    {
        int index = start + 1;
        while (index < line.Length)
        {
            if (line[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (line[index] == '\'')
                return index + 1;

            index++;
        }

        return line.Length;
    }
}
