using System;
using AwesomeAssertions;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="Font"/> declares no <c>DeepCopy</c> of its own - it has no member that is itself a
///   DocumentObject, so <c>DocumentObject.DeepCopy</c>'s MemberwiseClone already copies every one of
///   its nine <c>[DV]</c> members by value. This runs the same theory <c>FlatteningTests</c> runs
///   for flattening, so a tenth member added to <see cref="Font"/> is checked here too rather than
///   only where it happens to be inherited from a style.
/// </summary>
public class FontCloneTests
{
    [Theory]
    [MemberData(nameof(FontMemberCases.All), MemberType = typeof(FontMemberCases))]
    public void ClonePreservesEveryFontMember(string member, Action<Font> set, Func<Font, object> read)
    {
        var font = new Font();
        set(font);

        var copy = font.Clone();

        read(copy).Should().Be(read(font), $"{member} should have survived the copy");
    }
}
