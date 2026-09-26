using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Page labels are what a reader shows for a page instead of its position, so that front
///   matter can be numbered i, ii, iii while the body starts again at 1. They are held in the
///   catalog as a number tree.
///   See https://github.com/ststeiger/PdfSharpCore/issues/358.
/// </summary>
public class PageLabelTests
{
    [Fact]
    public void ADocumentWithoutLabelsHasNoneAndIsGivenNone()
    {
        var document = WithPages(3);

        document.PageLabels.Count.Should().Be(0);
        document.PageLabels.GetLabel(0).Should().BeNull();

        // Asking is not the same as setting: reading the labels of a document that has none
        // must not put an empty tree into its catalog.
        document.Internals.Catalog.Elements.ContainsKey("/PageLabels").Should().BeFalse();
    }

    [Fact]
    public void TheFrontMatterAndTheBodyAreLabelledSeparately()
    {
        var document = WithPages(8);

        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(4, PdfPageLabelStyle.Decimal);

        // Four pages of front matter, then the body starting again at one.
        document.PageLabels.GetLabel(0).Should().Be("i");
        document.PageLabels.GetLabel(1).Should().Be("ii");
        document.PageLabels.GetLabel(3).Should().Be("iv");
        document.PageLabels.GetLabel(4).Should().Be("1");
        document.PageLabels.GetLabel(7).Should().Be("4");
    }

    [Fact]
    public void ARangeRunsUntilTheNextOneBegins()
    {
        var document = WithPages(10);
        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal);
        document.PageLabels.Add(5, PdfPageLabelStyle.Decimal);

        document.PageLabels.GetRange(4).StartPageIndex.Should().Be(0);
        document.PageLabels.GetRange(5).StartPageIndex.Should().Be(5);
        document.PageLabels.GetRange(9).StartPageIndex.Should().Be(5);
    }

    [Fact]
    public void ARangeCanStartCountingFromSomewhereOtherThanOne()
    {
        var document = WithPages(4);

        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal, null, 42);

        document.PageLabels.GetLabel(0).Should().Be("42");
        document.PageLabels.GetLabel(2).Should().Be("44");
    }

    [Fact]
    public void APrefixIsPutInFrontOfTheNumber()
    {
        var document = WithPages(4);

        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal, "A-", 1);

        document.PageLabels.GetLabel(0).Should().Be("A-1");
        document.PageLabels.GetLabel(3).Should().Be("A-4");
    }

    [Fact]
    public void ARangeWithNoStyleIsLabelledByItsPrefixAlone()
    {
        var document = WithPages(3);

        document.PageLabels.Add(0, PdfPageLabelStyle.None, "Cover", 1);

        // Every page of the range carries the same label, which is what a document does for
        // an unnumbered insert.
        document.PageLabels.GetLabel(0).Should().Be("Cover");
        document.PageLabels.GetLabel(2).Should().Be("Cover");
    }

    [Theory]
    [InlineData(1, "I")]
    [InlineData(4, "IV")]
    [InlineData(9, "IX")]
    [InlineData(14, "XIV")]
    [InlineData(40, "XL")]
    [InlineData(90, "XC")]
    [InlineData(400, "CD")]
    [InlineData(900, "CM")]
    [InlineData(1987, "MCMLXXXVII")]
    public void RomanNumeralsAreWrittenTheUsualWay(int number, string expected)
    {
        var document = WithPages(1);
        document.PageLabels.Add(0, PdfPageLabelStyle.UppercaseRoman, null, number);

        document.PageLabels.GetLabel(0).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(52, "ZZ")]
    [InlineData(53, "AAA")]
    public void LettersRepeatRatherThanCountingUpInBaseTwentySix(int number, string expected)
    {
        // The standard asks for A to Z, then AA to ZZ. The twenty-seventh is AA, not AB.
        var document = WithPages(1);
        document.PageLabels.Add(0, PdfPageLabelStyle.UppercaseLetters, null, number);

        document.PageLabels.GetLabel(0).Should().Be(expected);
    }

    [Fact]
    public void LowercaseStylesAreWrittenInLowercase()
    {
        var document = WithPages(2);
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman, null, 4);
        document.PageLabels.Add(1, PdfPageLabelStyle.LowercaseLetters, null, 27);

        document.PageLabels.GetLabel(0).Should().Be("iv");
        document.PageLabels.GetLabel(1).Should().Be("aa");
    }

    [Fact]
    public void LabelsSurviveBeingSavedAndReadBack()
    {
        var document = WithPages(8);
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(4, PdfPageLabelStyle.Decimal, "Part-", 1);

        var reopened = document.Reopened();

        reopened.PageLabels.Count.Should().Be(2);
        reopened.PageLabels.GetLabel(1).Should().Be("ii");
        reopened.PageLabels.GetLabel(4).Should().Be("Part-1");
        reopened.PageLabels.GetLabel(6).Should().Be("Part-3");
    }

    [Fact]
    public void TheRangesOfADocumentCanBeReadBack()
    {
        var document = WithPages(6);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal, "B", 7);
        document.PageLabels.Add(0, PdfPageLabelStyle.UppercaseRoman);

        var range = document.Reopened().PageLabels.GetRange(4);

        range.StartPageIndex.Should().Be(3);
        range.Style.Should().Be(PdfPageLabelStyle.Decimal);
        range.Prefix.Should().Be("B");
        range.Start.Should().Be(7);
    }

    [Fact]
    public void ARangeAddedFurtherInBringsOneAtPageZeroWithIt()
    {
        var document = WithPages(4);

        document.PageLabels.Add(2, PdfPageLabelStyle.Decimal);

        // The standard asks a document with labels to label page zero, so leaving it out is
        // not something a caller should be able to do by accident. The pages before the
        // range asked for are numbered the way a reader would have shown them anyway.
        document.PageLabels.GetRangeStarts().Should().Equal(0, 2);
        document.PageLabels.GetLabel(0).Should().Be("1");
        document.PageLabels.GetLabel(1).Should().Be("2");
        document.PageLabels.GetLabel(2).Should().Be("1");
    }

    [Fact]
    public void ARangeAtPageZeroTakesThePlaceOfTheOneBroughtIn()
    {
        var document = WithPages(6);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal);

        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);

        document.PageLabels.Count.Should().Be(2);
        document.PageLabels.GetLabel(0).Should().Be("i");
        document.PageLabels.GetLabel(3).Should().Be("1");
    }

    [Fact]
    public void ARangeAddedAtPageZeroBringsNoOtherWithIt()
    {
        var document = WithPages(4);

        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);

        document.PageLabels.Count.Should().Be(1);
    }

    [Fact]
    public void TakingAwayTheRangeAtPageZeroPutsOneBackWhileOthersRemain()
    {
        var document = WithPages(6);
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal);

        document.PageLabels.Remove(0).Should().BeTrue();

        // The caller's range is gone, but the document still labels page zero, because one
        // that does not is a document the standard does not describe.
        document.PageLabels.GetRangeStarts().Should().Equal(0, 3);
        document.PageLabels.GetLabel(0).Should().Be("1");
    }

    [Fact]
    public void EveryDocumentWithLabelsLabelsPageZero()
    {
        var document = WithPages(9);
        document.PageLabels.Add(7, PdfPageLabelStyle.UppercaseLetters);
        document.PageLabels.Add(4, PdfPageLabelStyle.Decimal, "B-", 1);

        var reopened = document.Reopened();

        reopened.PageLabels.GetRangeStarts().Should().Contain(0);
        reopened.PageLabels.GetLabel(0).Should().NotBeNull();
    }

    [Fact]
    public void ARangeCanBeTakenAwayAgain()
    {
        var document = WithPages(6);
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal);

        document.PageLabels.Remove(3).Should().BeTrue();
        document.PageLabels.Remove(3).Should().BeFalse();

        document.PageLabels.Count.Should().Be(1);
        document.PageLabels.GetLabel(3).Should().Be("iv");
    }

    [Fact]
    public void TakingAwayTheLastRangeLeavesTheDocumentLabelledByPositionAgain()
    {
        var document = WithPages(4);
        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal);

        document.PageLabels.Remove(0).Should().BeTrue();

        // A tree left holding nothing would say the document has labels and then label no
        // page, which is not a document the standard describes.
        document.Internals.Catalog.Elements.ContainsKey("/PageLabels").Should().BeFalse();
        document.Reopened().PageLabels.Count.Should().Be(0);
    }

    [Fact]
    public void TakingAwayOneOfSeveralRangesLeavesTheRest()
    {
        var document = WithPages(6);
        document.PageLabels.Add(0, PdfPageLabelStyle.LowercaseRoman);
        document.PageLabels.Add(3, PdfPageLabelStyle.Decimal);

        document.PageLabels.Remove(3).Should().BeTrue();

        document.Internals.Catalog.Elements.ContainsKey("/PageLabels").Should().BeTrue();
        document.Reopened().PageLabels.Count.Should().Be(1);
    }

    [Fact]
    public void ClearingLeavesTheDocumentLabelledByPositionAgain()
    {
        var document = WithPages(4);
        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal);

        document.PageLabels.Clear();

        document.PageLabels.Count.Should().Be(0);
        document.Internals.Catalog.Elements.ContainsKey("/PageLabels").Should().BeFalse();
        document.Reopened().PageLabels.Count.Should().Be(0);
    }

    [Fact]
    public void ANegativePageOrAStartBelowOneIsRefused()
    {
        var document = WithPages(2);

        var negativePage = () => document.PageLabels.Add(-1, PdfPageLabelStyle.Decimal);
        var zeroStart = () => document.PageLabels.Add(0, PdfPageLabelStyle.Decimal, null, 0);

        negativePage.Should().Throw<ArgumentOutOfRangeException>();
        zeroStart.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheDefaultStartIsLeftOutOfTheFile()
    {
        var document = WithPages(2);
        document.PageLabels.Add(0, PdfPageLabelStyle.Decimal, null, 1);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        var written = System.Text.Encoding.Latin1.GetString(stream.ToArray());

        // One is the default, so writing it would say nothing that leaving it out does not.
        written.Should().Contain("/PageLabels");
        written.Should().NotContain("/St 1");
    }

    private static PdfDocument WithPages(int count)
    {
        var document = new PdfDocument();
        for (var at = 0; at < count; at++)
            _ = document.AddPage();

        return document;
    }
}
