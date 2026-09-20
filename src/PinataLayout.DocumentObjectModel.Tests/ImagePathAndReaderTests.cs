using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Where an <see cref="Image"/> looks for its file, and the four entry points of
///   <see cref="DdlReader"/> that take one.
///   <para>
///   An image in a document names a file, and the name it holds may be relative — to the working
///   directory the renderer was given, or to one of the directories the document's
///   <see cref="Document.ImagePath"/> lists. <c>GetFilePath</c> is where those are resolved, and it
///   answers a path whether or not anything is there.
///   </para>
/// </summary>
public class ImagePathAndReaderTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "pinata-image-" + Guid.NewGuid().ToString("N"));

    public ImagePathAndReaderTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still open elsewhere is not this test's business.
        }
    }

    /// <summary>An image source that knows its name and nothing else, which is all a path needs.</summary>
    sealed class NamedSource : ImageSource.IImageSource
    {
        public NamedSource(string name) => Name = name;

        public int Width => 1;
        public int Height => 1;
        public string Name { get; }
        public bool Transparent => false;
        public void SaveAsJpeg(MemoryStream ms) => throw new NotSupportedException();
        public PixelBuffer GetPixels() => throw new NotSupportedException();
    }

    static Image AnImageNamed(Document document, string name)
    {
        var image = document.AddSection().AddImage(new NamedSource(name));
        return image;
    }

    // ----- where an image looks for its file ---------------------------------------------------------

    [Fact]
    public void AnImagePathIsTakenRelativeToTheWorkingDirectoryItWasGiven()
    {
        var image = AnImageNamed(new Document(), "picture.png");

        var path = image.GetFilePath(_directory);

        path.Should().Be(Path.Combine(_directory, "picture.png"));
    }

    [Fact]
    public void AnImageWithNoWorkingDirectoryLooksInTheCurrentOne()
    {
        var image = AnImageNamed(new Document(), "picture.png");

        var path = image.GetFilePath(null);

        path.Should().EndWith("picture.png");
        path.Should().StartWith(Directory.GetCurrentDirectory());
    }

    [Fact]
    public void AnImageIsFoundInOneOfTheDirectoriesTheDocumentLists()
    {
        var document = new Document { ImagePath = _directory };
        File.WriteAllBytes(Path.Combine(_directory, "found.png"), [1, 2, 3]);
        var image = AnImageNamed(document, "found.png");

        var path = image.GetFilePath(_directory);

        path.Should().Be(Path.Combine(_directory, "found.png"));
    }

    [Fact]
    public void AnImageTheDocumentsPathDoesNotHoldStillAnswersAPath()
    {
        var document = new Document { ImagePath = _directory };
        var image = AnImageNamed(document, "missing.png");

        var path = image.GetFilePath(_directory);

        path.Should().Be(Path.Combine(_directory, "missing.png"));
    }

    // ----- the image itself --------------------------------------------------------------------------

    [Fact]
    public void AnImageWithASourceIsNotEmptyWhateverElseItSays()
    {
        var bare = new Image();
        bare.IsNull().Should().BeTrue();

        bare.Source = new NamedSource("picture.png");
        bare.IsNull().Should().BeFalse();
    }

    [Fact]
    public void APictureFormatCanBeAssignedWholesale()
    {
        var image = new Image();
        var other = new Image();
        other.PictureFormat.CropLeft = "1cm";

        image.PictureFormat = other.PictureFormat.Clone();

        image.PictureFormat.CropLeft.Centimeter.Should().BeApproximately(1, 1e-4);
    }

    [Fact]
    public void AnImageClonesItsFormatWithIt()
    {
        var image = new Image { ScaleWidth = 2, Resolution = 300, LockAspectRatio = true };
        image.PictureFormat.CropTop = "3mm";

        var clone = image.Clone();
        clone.ScaleWidth = 4;

        image.ScaleWidth.Should().Be(2);
        clone.Resolution.Should().Be(300);
        clone.LockAspectRatio.Should().BeTrue();
        clone.PictureFormat.CropTop.Millimeter.Should().BeApproximately(3, 1e-4);
    }

    // ----- the ways a reader is given its DDL ---------------------------------------------------------

    const string ADocument = "\\document{ \\section{ \\paragraph{ hello } } }";

    [Fact]
    public void ADocumentIsReadFromAFile()
    {
        var file = Path.Combine(_directory, "document.mdddl");
        File.WriteAllText(file, ADocument);

        var document = DdlReader.DocumentFromFile(file);

        document.Sections.Count.Should().Be(1);
        document.DdlFile.Should().Be(file, "a document read from a file remembers which one");
    }

    [Fact]
    public void ADocumentIsReadFromAStream()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ADocument));
        using var reader = new DdlReader(stream);

        var document = reader.ReadDocument();

        document.Sections.Count.Should().Be(1);
        document.DdlFile.Should().BeEmpty("nothing read from a stream has a file name");
    }

    [Fact]
    public void AnObjectIsReadFromAStringAndFromAFile()
    {
        const string paragraph = "\\paragraph{ hello }";
        var file = Path.Combine(_directory, "object.mdddl");
        File.WriteAllText(file, paragraph);

        // A paragraph on its own is read as the elements holding it, which is what a fragment of
        // DDL is: a list of document elements rather than the one object it happens to hold.
        DdlReader.ObjectFromString(paragraph).Should().BeOfType<DocumentElements>();
        DdlReader.ObjectFromFile(file).Should().BeOfType<DocumentElements>();
    }

    /// <summary>
    ///   A reader given somewhere to put its errors still throws the first one it cannot carry on
    ///   past; the collection is for the ones it can. An object's DDL has to begin with an object.
    /// </summary>
    [Fact]
    public void DdlThatDoesNotBeginWithAnObjectIsRefused()
    {
        var errors = new DdlReaderErrors();

        var reading = () => DdlReader.ObjectFromString("a" + Marker + "nosuch b", errors);

        reading.Should().Throw<Exception>().Which.Message.Should().Contain("Unexpected symbol");
    }

    /// <summary>The DDL escape character, kept out of the string literals above.</summary>
    const string Marker = "\\";
}
