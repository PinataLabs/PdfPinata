using System;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The nine <c>[DV]</c> members of <see cref="Font"/>, each as a setter and a reader, so that a
///   test which must cover all of them - flattening, deep copy, an MDDDL round trip - has one place
///   that names them. A tenth member added to <see cref="Font"/> becomes a tenth case here rather
///   than a gap silently repeated across three test files.
/// </summary>
internal static class FontMemberCases
{
    public static TheoryData<string, Action<Font>, Func<Font, object>> All() => new()
    {
        { "Name", f => f.Name = "Verdana", f => f.Name },
        { "Size", f => f.Size = 20, f => f.Size.Point },
        { "Bold", f => f.Bold = true, f => f.Bold },
        { "Italic", f => f.Italic = true, f => f.Italic },
        { "Underline", f => f.Underline = Underline.Single, f => f.Underline },
        { "Color", f => f.Color = Colors.Purple, f => f.Color },
        { "Superscript", f => f.Superscript = true, f => f.Superscript },
        { "Subscript", f => f.Subscript = true, f => f.Subscript },
        { "Strikethrough", f => f.Strikethrough = Strikethrough.Single, f => f.Strikethrough }
    };
}
