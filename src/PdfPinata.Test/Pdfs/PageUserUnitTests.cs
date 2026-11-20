using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A page's /UserUnit is the size of a default user space unit in 1/72 inch. It is 1 when the
///   page says nothing usable, and what is written comes back from a reopened file.
/// </summary>
public class PageUserUnitTests
{
    [Test]
    public void IsOneWhenThePageHasNoEntry()
    {
        var page = ReopenedPageWith(null);

        page.UserUnit.Should().Be(1);
    }

    [Test]
    [Arguments(2.0)]
    [Arguments(0.5)]
    [Arguments(75000.0)]
    public void ComesBackAsWrittenFromAReopenedDocument(double unit)
    {
        var page = ReopenedPageWith(p => p.UserUnit = unit);

        page.UserUnit.Should().Be(unit);
    }

    [Test]
    public void ReadsAnIntegerEntry()
    {
        var page = ReopenedPageWith(p => p.Elements.SetInteger("/UserUnit", 3));

        page.UserUnit.Should().Be(3);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-2)]
    public void ReadsAnEntryThatIsNotPositiveAsOne(int entry)
    {
        var page = ReopenedPageWith(p => p.Elements.SetInteger("/UserUnit", entry));

        page.UserUnit.Should().Be(1);
    }

    [Test]
    public void ReadsAnEntryThatIsNotANumberAsOne()
    {
        var page = ReopenedPageWith(p => p.Elements.SetName("/UserUnit", "/Big"));

        page.UserUnit.Should().Be(1);
    }

    [Test]
    [Arguments(0.0)]
    [Arguments(-1.5)]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    public void RefusesToWriteAValueThatIsNotFiniteAndPositive(double value)
    {
        var page = new PdfDocument().AddPage();

        var setting = () => page.UserUnit = value;

        setting.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static PdfPage ReopenedPageWith(Action<PdfPage> change)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        change?.Invoke(page);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages[0];
    }
}
