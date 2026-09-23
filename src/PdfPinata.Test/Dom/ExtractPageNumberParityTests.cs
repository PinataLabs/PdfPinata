using System;
using AwesomeAssertions;
using Xunit;
using ImageHelper = PinataLayout.DocumentObjectModel.ImageHelper;
using XPdfForm = PdfPinata.Drawing.XPdfForm;

namespace PdfPinata.Test.Dom;

/// <summary>
///   <c>file.pdf#3</c> means page three of that file, and the same sixteen-branch parse of it
///   exists twice - once in <c>PdfPinata.Drawing.XPdfForm</c> and once in PinataLayout's
///   <c>ImageHelper</c>, whose copy carries the comment "duplicated from class XPdfForm". Nothing
///   makes them agree.
///   <para>
///   This is the pattern <c>CLAUDE.md</c> warns about for the two lexers and the category axis
///   renderers, so every case here is asserted against both implementations from one table. If
///   they ever disagree, both halves of the theory name the same path and one of them fails.
///   </para>
///   <para>
///   They agree today, and they were wrong together: the loop walking back over the digits was
///   written <c>while (char.IsDigit(path, length) &amp;&amp; length >= 0)</c>, testing the
///   character before the bound. A path that is nothing but digits walked <c>length</c> down to
///   -1 and asked whether <c>path[-1]</c> was a digit, which throws. Fixed in both; see the
///   backlog spec's finding F12.
///   </para>
/// </summary>
public class ExtractPageNumberParityTests
{
    /// <summary>
    ///   The path each implementation returns and the page number it reports, as one string, so
    ///   that a single assertion covers both halves of the answer.
    /// </summary>
    private static string ByXPdfForm(string path)
    {
        var rest = XPdfForm.ExtractPageNumber(path, out var pageNumber);
        return rest + " | " + pageNumber;
    }

    private static string ByImageHelper(string path)
    {
        var rest = ImageHelper.ExtractPageNumber(path, out var pageNumber);
        return rest + " | " + pageNumber;
    }

    [Theory]
    // A page reference, which is the whole point of the syntax.
    [InlineData("file.pdf#3", "file.pdf | 3")]
    [InlineData("file.pdf#123", "file.pdf | 123")]
    [InlineData("C:\\docs\\file.pdf#7", "C:\\docs\\file.pdf | 7")]
    // No fragment at all: the path is the path and the page are nought.
    [InlineData("file.pdf", "file.pdf | 0")]
    [InlineData("", " | 0")]
    // A hash with nothing after it is not a page number, and neither is a hash with a
    // non-number after it.
    [InlineData("file.pdf#", "file.pdf# | 0")]
    [InlineData("file.pdf#abc", "file.pdf#abc | 0")]
    [InlineData("file.pdf#3a", "file.pdf#3a | 0")]
    // The dot is what tells a path with a fragment from a fragment on its own, which is the
    // reason the check is there.
    [InlineData("#123", "#123 | 0")]
    [InlineData("file#123", "file#123 | 0")]
    // A page of nought and a leading nought are both read as written; neither is refused.
    [InlineData("file.pdf#0", "file.pdf | 0")]
    [InlineData("file.pdf#007", "file.pdf | 7")]
    // A minus sign is not a digit, so a negative page is not a page reference.
    [InlineData("file.pdf#-3", "file.pdf#-3 | 0")]
    // Nor is a space, either side of the number.
    [InlineData("file.pdf# 3", "file.pdf# 3 | 0")]
    [InlineData("file.pdf#3 ", "file.pdf#3  | 0")]
    // A name that is nothing but digits is a name. This is the case that used to throw.
    [InlineData("123", "123 | 0")]
    [InlineData("0", "0 | 0")]
    [InlineData("1.2", "1.2 | 0")]
    // And one that only ends in digits.
    [InlineData("report2024", "report2024 | 0")]
    [InlineData("report.2024", "report.2024 | 0")]
    public void BothCopiesReadThePathTheSameWay(string path, string expected)
    {
        ByXPdfForm(path).Should().Be(expected, "XPdfForm reads '{0}' this way", path);
        ByImageHelper(path).Should().Be(expected, "and ImageHelper must agree about '{0}'", path);
    }

    [Fact]
    public void NeitherCopyAcceptsANullPath()
    {
        var byForm = () => XPdfForm.ExtractPageNumber(null, out _);
        var byHelper = () => ImageHelper.ExtractPageNumber(null, out _);

        byForm.Should().Throw<ArgumentNullException>();
        byHelper.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    ///   Stated on its own because it is the defect the table above closes: every character being
    ///   a digit is what walked the index off the front of the string.
    /// </summary>
    [Theory]
    [InlineData("1")]
    [InlineData("42")]
    [InlineData("000000")]
    public void APathOfNothingButDigitsIsReadRatherThanThrownAt(string path)
    {
        var byForm = () => XPdfForm.ExtractPageNumber(path, out _);
        var byHelper = () => ImageHelper.ExtractPageNumber(path, out _);

        byForm.Should().NotThrow<ArgumentOutOfRangeException>();
        byHelper.Should().NotThrow<ArgumentOutOfRangeException>();
    }
}
