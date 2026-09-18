using System;
using System.Collections.Generic;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The nine <c>[DV]</c> members of <see cref="Font"/>, each as a setter and a reader, so that a
///   test which must cover all of them - flattening, deep copy, an MDDDL round trip - has one place
///   that names them. A tenth member added to <see cref="Font"/> becomes a tenth case here rather
///   than a gap silently repeated across three test files.
/// </summary>
internal static class FontMemberCases
{
    public static IEnumerable<object[]> All()
    {
        yield return new object[] { "Name", (Action<Font>)(f => f.Name = "Verdana"), (Func<Font, object>)(f => f.Name) };
        yield return new object[] { "Size", (Action<Font>)(f => f.Size = 20), (Func<Font, object>)(f => f.Size.Point) };
        yield return new object[] { "Bold", (Action<Font>)(f => f.Bold = true), (Func<Font, object>)(f => f.Bold) };
        yield return new object[] { "Italic", (Action<Font>)(f => f.Italic = true), (Func<Font, object>)(f => f.Italic) };
        yield return new object[] { "Underline", (Action<Font>)(f => f.Underline = Underline.Single), (Func<Font, object>)(f => f.Underline) };
        yield return new object[] { "Color", (Action<Font>)(f => f.Color = Colors.Purple), (Func<Font, object>)(f => f.Color) };
        yield return new object[] { "Superscript", (Action<Font>)(f => f.Superscript = true), (Func<Font, object>)(f => f.Superscript) };
        yield return new object[] { "Subscript", (Action<Font>)(f => f.Subscript = true), (Func<Font, object>)(f => f.Subscript) };
        yield return new object[] { "Strikethrough", (Action<Font>)(f => f.Strikethrough = Strikethrough.Single), (Func<Font, object>)(f => f.Strikethrough) };
    }
}
