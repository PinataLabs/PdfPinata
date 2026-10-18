using System;
using AwesomeAssertions;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What <see cref="SeamScope"/> refuses to install, which is what makes the rule in
///   <see cref="TextShaperSensitiveAttribute"/>'s remarks something the suite enforces rather than
///   something a comment asks for.
/// </summary>
[TextShaperSensitive]
public class SeamScopeTests
{
    [Test]
    public void ABareHarfBuzzShaperIsRefused()
    {
        // It shapes every run it is given, so installed for the whole process it kerns the Latin text
        // of every unmarked test running beside it (#317).
        using var shaper = new HarfBuzzTextShaper();
        var before = GlobalFontSettings.TextShaper;

        var install = () => SeamScope.TextShaper(shaper).Dispose();

        install.Should().Throw<InvalidOperationException>().WithMessage("*sentinel*");
        GlobalFontSettings.TextShaper.Should().BeSameAs(before, "a refused shaper is never installed");
    }
}
