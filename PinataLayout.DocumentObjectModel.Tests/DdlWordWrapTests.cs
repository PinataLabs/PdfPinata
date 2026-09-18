using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using Xunit;
using static PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The serializer breaks a line of MDDDL once it reaches 200 characters less the current indent,
///   and the interesting lines are the ones it cannot break: a file path, a URL, a word longer than
///   the limit. Looking for somewhere to break past the limit used to start one character beyond
///   where the line ran out, so a string landing on the limit exactly asked
///   <c>String.IndexOf</c> to start one past its end and threw
///   <see cref="ArgumentOutOfRangeException"/> instead of writing anything. Nothing about the
///   document decided whether that happened - the length of the text did, which for an image meant
///   how deep in the filesystem the picture happened to sit.
/// </summary>
public class DdlWordWrapTests
{
    /// <summary>
    ///   The limit less an indent nobody outside the serializer can see, so rather than compute the
    ///   one length that lands on it, every test here sweeps a range wide enough to contain it.
    /// </summary>
    static readonly int[] LengthsAcrossTheLimit = Enumerable.Range(140, 141).ToArray();

    [Fact]
    public void TextOfEveryLengthAroundTheLimitIsWritten()
    {
        foreach (int length in LengthsAcrossTheLimit)
        {
            string word = new string('a', length);
            var document = new Document();
            document.AddSection().AddParagraph(word);

            string ddl = DdlWriter.WriteToString(document);

            ddl.Should().Contain(word, $"a word of {length} characters has to be written out whole");
        }
    }

    [Fact]
    public void AnImagePathOfEveryLengthAroundTheLimitIsWritten()
    {
        foreach (int length in LengthsAcrossTheLimit)
        {
            string path = "/" + new string('a', length);
            var document = new Document();
            document.AddSection().AddImage(new NamedImageSource(path));

            string ddl = DdlWriter.WriteToString(document);

            ddl.Should().Contain(path, $"a path of {length + 1} characters has to survive being written");
        }
    }

    [Fact]
    public void AWordTooLongToBreakIsWrittenWholeRatherThanCutInHalf()
    {
        // Nowhere to break means the line goes out over length. Breaking it anyway would put a
        // line ending in the middle of a path, and reading it back would give a different path.
        string word = new string('a', 400);
        var document = new Document();
        document.AddSection().AddParagraph(word);

        var reread = DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

        Text TextOf(Document d) => ((Paragraph)d.LastSection.Elements[0]).Elements.OfType<Text>().First();
        TextOf(reread).Content.Should().Be(word);
    }

    [Fact]
    public void ALineWhoseFirstBlankIsPastTheLimitIsStillBroken()
    {
        // The break has to be looked for past the limit, and there is a blank there but no line
        // break. Asking for the smaller of the two indexes answered -1 for the missing one and so
        // reported "nowhere to break", leaving all 501 characters on a single line.
        var document = new Document();
        document.AddSection().AddParagraph(new string('a', 250) + " " + new string('b', 250));

        string[] lines = DdlWriter.WriteToString(document).Replace("\r\n", "\n").Split('\n');

        lines.Should().OnlyContain(line => line.Length < 400,
            "the text is broken at the blank rather than written as one 501 character line");
    }

    /// <summary>
    ///   An image source that is nothing but its name. <c>Image.Serialize</c> writes the name of
    ///   the source it was given, and that is the whole of what these tests are about.
    /// </summary>
    sealed class NamedImageSource : IImageSource
    {
        public NamedImageSource(string name) => Name = name;

        public string Name { get; }

        public int Width => 1;

        public int Height => 1;

        public bool Transparent => false;

        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();

        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }
}
