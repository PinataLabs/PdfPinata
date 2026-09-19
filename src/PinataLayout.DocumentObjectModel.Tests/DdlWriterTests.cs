using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The entry points of <see cref="DdlWriter"/>: a string, a file, a stream or a text writer, for
///   one object or for a collection of them, at the default indent or another. Whatever writes
///   the text, it should be the same text.
/// </summary>
public sealed class DdlWriterTests : IDisposable
{
    readonly string _file = Path.Combine(Path.GetTempPath(), "ddlwriter-" + Guid.NewGuid().ToString("N") + ".mdddl");

    public void Dispose()
    {
        if (File.Exists(_file))
            File.Delete(_file);
    }

    static Document ADocument()
    {
        var document = new Document();
        document.AddSection().AddParagraph("hello");
        return document;
    }

    static DocumentElements SomeElements()
    {
        var elements = new Section().Elements;
        elements.AddParagraph("first");
        elements.AddParagraph("second");
        return elements;
    }

    [Fact]
    public void EveryStringOverloadForAnObjectAgreesWithTheOthers()
    {
        var document = ADocument();

        var byDefault = DdlWriter.WriteToString(document);

        DdlWriter.WriteToString(document, 2).Should().Be(byDefault);
        DdlWriter.WriteToString(document, 2, 0).Should().Be(byDefault);
        DdlReader.DocumentFromString(byDefault).LastSection.Elements.Count.Should().Be(1);
    }

    [Fact]
    public void EveryStringOverloadForACollectionAgreesWithTheOthers()
    {
        var elements = SomeElements();

        var byDefault = DdlWriter.WriteToString(elements);

        byDefault.Should().Contain("first").And.Contain("second");
        DdlWriter.WriteToString(elements, 2).Should().Be(byDefault);
        DdlWriter.WriteToString(elements, 2, 0).Should().Be(byDefault);
    }

    [Fact]
    public void AWiderIndentWritesWiderLines()
    {
        var document = ADocument();

        var narrow = DdlWriter.WriteToString(document, 1);
        var wide = DdlWriter.WriteToString(document, 8, 4);

        wide.Length.Should().BeGreaterThan(narrow.Length);
        wide.Should().StartWith("    ");
    }

    [Fact]
    public void WritingAnObjectToAFileWritesWhatWritingToAStringDoes()
    {
        var document = ADocument();
        var expected = DdlWriter.WriteToString(document);

        DdlWriter.WriteToFile(document, _file);
        File.ReadAllText(_file).Should().Be(expected);

        DdlWriter.WriteToFile(document, _file, 2);
        File.ReadAllText(_file).Should().Be(expected);

        DdlWriter.WriteToFile(document, _file, 2, 0);
        File.ReadAllText(_file).Should().Be(expected);
    }

    [Fact]
    public void WritingACollectionToAFileWritesWhatWritingToAStringDoes()
    {
        var elements = SomeElements();
        var expected = DdlWriter.WriteToString(elements);

        DdlWriter.WriteToFile(elements, _file);
        File.ReadAllText(_file).Should().Be(expected);

        DdlWriter.WriteToFile(elements, _file, 2);
        File.ReadAllText(_file).Should().Be(expected);

        DdlWriter.WriteToFile(elements, _file, 2, 0);
        File.ReadAllText(_file).Should().Be(expected);
    }

    [Fact]
    public void AStreamGetsTheSameTextAsAString()
    {
        var document = ADocument();
        var elements = SomeElements();
        using var stream = new MemoryStream();

        // ReSharper disable once UsingStatementResourceInitialization
        using (var writer = new DdlWriter(stream) { Indent = 2, InitialIndent = 0 })
        {
            writer.Indent.Should().Be(2);
            writer.InitialIndent.Should().Be(0);
            writer.WriteDocument(document);
            writer.WriteDocument(elements);
            writer.Flush();
        }

        Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿')
            .Should().Be(DdlWriter.WriteToString(document) + DdlWriter.WriteToString(elements));
    }

    [Fact]
    public void ATextWriterIsLeftOpenForItsOwner()
    {
        using var text = new StringWriter();

        using (var writer = new DdlWriter(text))
            writer.WriteDocument(ADocument());
        text.Write("after");

        text.ToString().Should().EndWith("after");
    }
}
