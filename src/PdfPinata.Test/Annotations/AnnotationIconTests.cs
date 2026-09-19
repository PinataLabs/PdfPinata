using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using Xunit;

namespace PdfPinata.Test.Annotations;

/// <summary>
///   The three annotations that name their icon in <c>/Name</c>, and what they read back.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="PdfFileAttachmentAnnotation.Icon"/>'s getter could never succeed. It parsed what
///     <c>Elements.GetName</c> returned, which is <c>PdfName.Value</c> and carries the solidus, so
///     <c>Enum.Parse</c> was handed <c>"/Paperclip"</c> and threw; and it guarded against a missing
///     entry with a null check, where <c>GetName</c> answers a missing key with
///     <see cref="string.Empty"/>, so an attachment with no icon threw as well.
///   </para>
///   <para>
///     The property existed three times over and the other two stripped the solidus and checked the
///     member existed. All three now share <c>PdfAnnotation.IconFromName</c>, so there is one
///     implementation to be right rather than three to drift.
///   </para>
/// </remarks>
public class AnnotationIconTests
{
    public static TheoryData<PdfTextAnnotationIcon> TextIcons => Icons<PdfTextAnnotationIcon>();

    public static TheoryData<PdfRubberStampAnnotationIcon> StampIcons =>
        Icons<PdfRubberStampAnnotationIcon>();

    public static TheoryData<PdfFileAttachmentAnnotation.IconType> AttachmentIcons =>
        Icons<PdfFileAttachmentAnnotation.IconType>();

    [Theory]
    [MemberData(nameof(AttachmentIcons))]
    public void AnAttachmentIconReadsBackAsItWasSet(PdfFileAttachmentAnnotation.IconType icon)
    {
        var attachment = OnAPage(new PdfFileAttachmentAnnotation());

        attachment.Icon = icon;

        // Every one of these threw before: ArgumentException, "Requested value '/PushPin' was
        // not found."
        attachment.Icon.Should().Be(icon);
        attachment.Elements.GetName("/Name").Should().Be("/" + icon);
    }

    [Fact]
    public void AnAttachmentWithNoIconIsAPushPin()
    {
        var attachment = OnAPage(new PdfFileAttachmentAnnotation());

        // The default ISO 32000-1 Table 184 gives the entry. The old guard tested the name
        // against null, which GetName never returns, so this threw rather than defaulting.
        attachment.Elements.ContainsKey("/Name").Should().BeFalse();
        attachment.Icon.Should().Be(PdfFileAttachmentAnnotation.IconType.PushPin);
    }

    [Fact]
    public void AnAttachmentNamingAnIconThisEnumerationLacksIsAPushPin()
    {
        var attachment = OnAPage(new PdfFileAttachmentAnnotation());
        attachment.Elements.SetName("/Name", "/Sellotape");

        attachment.Icon.Should().Be(PdfFileAttachmentAnnotation.IconType.PushPin);
    }

    [Fact]
    public void AnAttachmentIconOutsideTheEnumerationIsNotWritten()
    {
        var attachment = OnAPage(new PdfFileAttachmentAnnotation());
        attachment.Icon = PdfFileAttachmentAnnotation.IconType.Tag;

        attachment.Icon = (PdfFileAttachmentAnnotation.IconType)42;

        // Better no entry, and the reader's default, than /42 - which a reader does not know and
        // so draws nothing for.
        attachment.Elements.ContainsKey("/Name").Should().BeFalse();
        attachment.Icon.Should().Be(PdfFileAttachmentAnnotation.IconType.PushPin);
    }

    [Theory]
    [MemberData(nameof(TextIcons))]
    public void ANoteIconReadsBackAsItWasSet(PdfTextAnnotationIcon icon)
    {
        var note = OnAPage(new PdfTextAnnotation());

        note.Icon = icon;

        note.Icon.Should().Be(icon);
    }

    [Theory]
    [MemberData(nameof(StampIcons))]
    public void AStampIconReadsBackAsItWasSet(PdfRubberStampAnnotationIcon icon)
    {
        var stamp = OnAPage(new PdfRubberStampAnnotation());

        stamp.Icon = icon;

        stamp.Icon.Should().Be(icon);
    }

    [Fact]
    public void ANoteOrStampNamingAnIconThatIsNotKnownHasNone()
    {
        var note = OnAPage(new PdfTextAnnotation());
        note.Elements.SetName("/Name", "/Semaphore");
        note.Icon.Should().Be(PdfTextAnnotationIcon.NoIcon);

        var stamp = OnAPage(new PdfRubberStampAnnotation());
        stamp.Elements.SetName("/Name", "/Semaphore");
        stamp.Icon.Should().Be(PdfRubberStampAnnotationIcon.NoIcon);
    }

    [Fact]
    public void AnIconNamedAsANumberIsNotReadAsTheMemberWithThatValue()
    {
        // Enum.TryParse would accept "1" and hand back whichever member is 1, so a document
        // naming its icon /1 would come back as a real icon. Enum.IsDefined checks the name.
        var note = OnAPage(new PdfTextAnnotation());
        note.Elements.SetName("/Name", "/1");

        note.Icon.Should().Be(PdfTextAnnotationIcon.NoIcon);
    }

    [Fact]
    public void EveryIconNameIsWrittenWithItsSolidus()
    {
        // The thing the broken getter tripped over, pinned from the writing side: what goes into
        // the dictionary is a PDF name, and a PDF name starts with a solidus.
        var note = OnAPage(new PdfTextAnnotation());
        note.Icon = PdfTextAnnotationIcon.Key;
        note.Elements.GetName("/Name").Should().Be("/Key");

        var stamp = OnAPage(new PdfRubberStampAnnotation());
        stamp.Icon = PdfRubberStampAnnotationIcon.Draft;
        stamp.Elements.GetName("/Name").Should().Be("/Draft");

        var attachment = OnAPage(new PdfFileAttachmentAnnotation());
        attachment.Icon = PdfFileAttachmentAnnotation.IconType.Paperclip;
        attachment.Elements.GetName("/Name").Should().Be("/Paperclip");
    }

    static TheoryData<T> Icons<T>() where T : struct, Enum
    {
        var data = new TheoryData<T>();

        // NoIcon is the absence of one - its setter removes the entry rather than writing a name -
        // so it is not a round trip and is covered on its own above.
        var icons = Enum.GetValues<T>()
            .Where(icon => icon.ToString() != "NoIcon");

        foreach (var icon in icons)
            data.Add(icon);

        return data;
    }

    static T OnAPage<T>(T annotation) where T : PdfAnnotation
    {
        var document = new PdfDocument();
        document.AddPage().Annotations.Add(annotation);
        return annotation;
    }
}
