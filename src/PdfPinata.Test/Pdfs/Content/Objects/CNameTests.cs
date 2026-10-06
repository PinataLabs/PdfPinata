using System;
using AwesomeAssertions;
using PdfPinata.Pdf.Content.Objects;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs.Content.Objects;

public class CNameTests
{
    [Test]
    [Arguments("/Foo")]
    public void SetNameTests(string name)
    {
        var cName = new CName
        {
            Name = name
        };

        cName.Name.Should().Be(name);
    }

    [Test]
    public void SetNameNullThrowsException()
    {
        Action act = () => _ = new CName
        {
            Name = null
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    [Arguments("Foo")]
    [Arguments("")]
    public void SetNameWithoutPrefixThrowsException(string name)
    {
        Action act = () => _ = new CName
        {
            Name = name
        };
        act.Should().Throw<ArgumentException>();
    }
}
