using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
///   RunLengthDecode (ISO 32000-1 7.4.5) used to be one of the names <see cref="Filtering"/>
///   recognised and answered null for, so a stream compressed with it came back from
///   <c>UnfilteredValue</c> as the text «Cannot decode filter». Each run is a length byte and then
///   either the bytes themselves or one byte to repeat; 128 ends the data.
/// </summary>
public class RunLengthDecodeTests
{
    private static byte[] Decode(params byte[] data) => Filtering.RunLengthDecode.Decode(data, (FilterParms)null);

    [Fact]
    public void ALengthBelow128CopiesThatManyPlusOneBytes()
    {
        Decode(2, (byte)'a', (byte)'b', (byte)'c', 128).Should().Equal("abc"u8.ToArray());
        Decode(0, (byte)'x', 128).Should().Equal("x"u8.ToArray());
    }

    [Fact]
    public void ALengthAbove128RepeatsTheNextByte257MinusThatManyTimes()
    {
        Decode(254, (byte)'z', 128).Should().Equal("zzz"u8.ToArray());
        Decode(255, (byte)'z', 128).Should().Equal("zz"u8.ToArray());
        Decode(129, 7, 128).Should().Equal(Enumerable.Repeat((byte)7, 128));
    }

    [Fact]
    public void TheLongestLiteralRunCarries128Bytes()
    {
        var literal = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();

        Decode([..new byte[] { 127 }.Concat(literal).Append((byte)128)]).Should().Equal(literal);
    }

    [Fact]
    public void RunsOfBothKindsFollowOneAnother()
    {
        Decode(1, (byte)'a', (byte)'b', 253, (byte)'-', 0, (byte)'c', 128)
            .Should().Equal("ab----c"u8.ToArray());
    }

    [Fact]
    public void NothingAfterTheEndOfDataMarkerIsRead()
    {
        Decode(0, (byte)'a', 128, 0, (byte)'b').Should().Equal("a"u8.ToArray());
        Decode(128).Should().BeEmpty();
        Decode().Should().BeEmpty();
    }

    [Fact]
    public void DataThatEndsWithoutTheMarkerGivesBackWhatItHeld()
    {
        Decode(1, (byte)'a', (byte)'b').Should().Equal("ab"u8.ToArray());
        Decode(254, (byte)'z').Should().Equal("zzz"u8.ToArray());
    }

    [Fact]
    public void ALiteralRunCutShortKeepsTheBytesThatAreThere()
    {
        Decode(0, (byte)'a', 9, (byte)'b', (byte)'c').Should().Equal("abc"u8.ToArray());
    }

    [Fact]
    public void ARepeatMissingItsByteAddsNothing()
    {
        Decode(0, (byte)'a', 200).Should().Equal("a"u8.ToArray());
    }

    public static TheoryData<byte[]> Samples => [
        Array.Empty<byte>(),
        new byte[] { 42 },
        new byte[] { 1, 1 },
        new byte[] { 1, 2 },
        "aaaaabcdeeeeeeeeefgh"u8.ToArray(),
        Enumerable.Repeat((byte)'q', 1000).ToArray(),
        Enumerable.Range(0, 1000).Select(value => (byte)(value * 37 + 11)).ToArray(),
        Enumerable.Range(0, 1000).Select(value => (byte)(value / 3)).ToArray(),
    ];

    [Theory]
    [MemberData(nameof(Samples))]
    public void WhatIsEncodedDecodesBackToItself(byte[] data)
    {
        var encoded = Filtering.RunLengthDecode.Encode(data);

        encoded[^1].Should().Be(128, "the encoder ends its output with the end-of-data marker");
        Filtering.RunLengthDecode.Decode(encoded, (FilterParms)null).Should().Equal(data);
    }

    [Fact]
    public void ALongRunOfOneByteIsEncodedAsRepeats()
    {
        // Seven repeats of 128 and one of 104, two bytes each, and the marker.
        Filtering.RunLengthDecode.Encode([..Enumerable.Repeat((byte)'q', 1000)])
            .Length.Should().Be(17);
    }

    [Theory]
    [InlineData("RunLengthDecode")]
    [InlineData("RL")]
    [InlineData("/RunLengthDecode")]
    public void TheFilterIsFoundByItsNameAndItsAbbreviation(string name)
    {
        Filtering.GetFilter(name).Should().BeOfType<RunLengthDecode>()
            .And.BeSameAs(Filtering.RunLengthDecode);
    }

    // ----- through a stream ----------------------------------------------------------------------

    private const string Content = "0 0 m 100 100 l S\n";

    /// <summary>
    ///   <see cref="Content"/> encoded by hand rather than by the encoder, so that reading it does
    ///   not depend on the other half of what is being tested: "0 0 m 1" literally, the two zeros
    ///   of "100" as a repeat, " 1" literally, the zeros again, and " l S\n" literally.
    /// </summary>
    private static byte[] EncodedContent() => [..new byte[] { 6 }.Concat(Latin1("0 0 m 1"))
        .Concat(new byte[] { 255, (byte)'0', 1 }).Concat(Latin1(" 1"))
        .Concat(new byte[] { 255, (byte)'0', 4 }).Concat(Latin1(" l S\n"))
        .Append((byte)128)];

    [Fact]
    public void AStreamFilteredWithItDecodesThroughTheStreamItself()
    {
        var document = new PdfDocument();
        var dictionary = new PdfDictionary(document);
        dictionary.CreateStream(EncodedContent());
        dictionary.Elements["/Filter"] = new PdfName("/RunLengthDecode");

        Encoding.ASCII.GetString(dictionary.Stream.UnfilteredValue).Should().Be(Content);

        dictionary.Stream.TryUnfilter().Should().BeTrue();
        dictionary.Elements.ContainsKey("/Filter").Should().BeFalse();
        Encoding.ASCII.GetString(dictionary.Stream.Value).Should().Be(Content);
    }

    [Fact]
    public void APageWhoseContentIsRunLengthEncodedIsReadFromAFile()
    {
        using var stream = new MemoryStream(File(EncodedContent(), "/Filter /RunLengthDecode"));
        var page = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import).Pages[0];

        Encoding.ASCII.GetString(PageContent.Of(page)).Should().Be(Content);
    }

    /// <summary>A one-page file whose content stream, object 4, holds the data given.</summary>
    private static byte[] File(byte[] data, string streamEntries)
    {
        var objects = new List<byte[]>
        {
            Latin1("<< /Type /Catalog /Pages 2 0 R >>"),
            Latin1("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Latin1("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R >>"),
            Latin1($"<< /Length {data.Length} {streamEntries} >>\nstream\n")
                .Concat(data).Concat(Latin1("\nendstream")).ToArray(),
        };

        using var file = new MemoryStream();
        void Write(byte[] bytes) => file.Write(bytes, 0, bytes.Length);

        Write(Latin1("%PDF-1.4\n"));
        var offsets = new List<long>();
        for (var idx = 0; idx < objects.Count; idx++)
        {
            offsets.Add(file.Position);
            Write(Latin1($"{idx + 1} 0 obj\n"));
            Write(objects[idx]);
            Write(Latin1("\nendobj\n"));
        }

        var xref = file.Position;
        var table = new StringBuilder($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            table.Append($"{offset:D10} 00000 n \n");
        table.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Write(Latin1(table.ToString()));

        return file.ToArray();
    }

    private static byte[] Latin1(string text) => [..text.Select(ch => (byte)ch)];
}
