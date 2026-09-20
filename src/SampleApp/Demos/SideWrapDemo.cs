using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Text running beside a shape rather than stopping above it and starting again below.
/// </summary>
/// <remarks>
///   <para>
///     Every page here is one paragraph of prose and one text frame. Nothing measures anything,
///     nothing is split by hand, and no line is placed by arithmetic: the wrap style says which
///     side the text runs down and the renderer breaks the lines to suit. A sidebar, a pull quote
///     and a picture that the copy closes around are all the same page with a different value on
///     <c>WrapFormat.Style</c>.
///   </para>
///   <para>
///     Four pages, because <c>Left</c> and <c>Right</c> name the side the <b>text</b> occupies and
///     the opposite reading is equally natural. Seeing them side by side is the only way to be sure
///     the page is not quietly backwards, and a wrap on the wrong side is a page that looks
///     deliberate.
///   </para>
///   <para>
///     This is PinataLayout, not <c>XTextFormatter</c>. The formatter has no notion of a shape, so the
///     pull quote on the second page of the Magazine demo is still arithmetic and will stay that
///     way - it is drawn on a page rather than laid out in a document. The two engines are
///     separate, which is worth seeing in one place.
///   </para>
/// </remarks>
internal sealed class SideWrapDemo : PdfDemo
{
    public SideWrapDemo() : base() { }

    public override string Name => "SideWrap";

    public override string Summary => "Text flowing beside a shape, on each of the four wrap styles.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "WrapStyle.Right - the frame at the left margin, the text down the right of it",
        "WrapStyle.Left - the mirror of it, and the page that proves the names are not backwards",
        "WrapStyle.Largest - the frame standing clear of both margins, the text taking the roomier side",
        "WrapStyle.Both - the same arrangement, asking for either side rather than the roomier one",
        "The four WrapFormat distances holding the text off all four edges of the frame",
        "Lines above and below the frame running the full measure, with no line drawn across it"
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Prose =
            "This paragraph is not split, measured or placed. It is one AddParagraph call, and the "
            + "frame beside it is one AddTextFrame. The renderer subtracts the frame from the area "
            + "the text is laid out in and breaks each line to whatever room is left on the line's "
            + "own band, so the lines level with the frame are short and the lines above and below "
            + "it run the full measure. Nothing here counts characters, probes a rectangle or adds "
            + "one word at a time until the answer stops fitting. ";

        var document = new Document
        {
            Info =
            {
                Title = "Side wrap"
            }
        };

        document.Styles["Normal"].Font.Name = "Liberation Serif";
        document.Styles["Normal"].Font.Size = 10;
        document.Styles["Normal"].ParagraphFormat.SpaceAfter = Unit.FromPoint(6);

        var caption = document.Styles.AddStyle("Caption", "Normal");
        caption.Font.Name = "Liberation Sans";
        caption.Font.Size = 8;
        caption.Font.Color = Colors.DimGray;

        // The style names the side the TEXT runs down, and the shape is put on the other one.
        //
        // Largest and Both stand the frame away from both margins, and deliberately not in the
        // middle: a frame with equal room either side demonstrates nothing, because whichever
        // side the text takes looks like the right answer. Set 1.2cm from the left margin, the
        // room on its right is nearly four times the room on its left, so a page that puts the
        // text down the left is visibly wrong rather than merely different.
        (WrapStyle Style, ShapePosition? Where, string Title, string Note)[] pages =
        {
            (WrapStyle.Right, ShapePosition.Left, "WrapStyle.Right",
                "The frame is at the left margin and the text runs down its right - the style names "
                + "the side the text occupies, not the side the shape sits on."),
            (WrapStyle.Left, ShapePosition.Right, "WrapStyle.Left",
                "The mirror of the page before. Read the two together: if the names were the other "
                + "way round, each page would still look deliberate."),
            (WrapStyle.Largest, null, "WrapStyle.Largest",
                "The frame stands 1.2cm in from the left margin, so there is nearly four times as "
                + "much room to its right. Each line takes the roomier side, which is why the copy "
                + "runs down the right of it."),
            (WrapStyle.Both, null, "WrapStyle.Both",
                "The same arrangement asking for either side rather than the roomier one. A line is "
                + "given one span rather than every span, so this lays out as Largest does today; "
                + "the two are kept apart because they say different things and would part company "
                + "if that changed.")
        };

        foreach ((var style, var where, var title, var note) in pages)
        {
            var section = document.AddSection();
            section.PageSetup.PageFormat = PageFormat.A5;
            section.PageSetup.TopMargin = Unit.FromCentimeter(2);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
            section.PageSetup.RightMargin = Unit.FromCentimeter(2);

            var heading = section.AddParagraph(title);
            heading.Format.Font.Name = "Liberation Sans";
            heading.Format.Font.Bold = true;
            heading.Format.Font.Size = 13;
            heading.Format.SpaceAfter = Unit.FromPoint(2);

            var explanation = section.AddParagraph(note);
            explanation.Style = "Caption";
            explanation.Format.SpaceAfter = Unit.FromPoint(14);

            // docs:begin wrap-frame
            // The frame is added to the flow like any other element. RelativeVertical.Paragraph is
            // what makes it float at all: a shape anchored to the page or the margin is placed
            // absolutely and the text is laid out as though it were not there.
            var frame = section.AddTextFrame();
            frame.Width = Unit.FromCentimeter(4.5);
            frame.Height = Unit.FromCentimeter(4);
            frame.RelativeVertical = RelativeVertical.Paragraph;
            frame.RelativeHorizontal = RelativeHorizontal.Margin;
            if (where.HasValue)
                frame.Left = where.Value;
            else
                frame.Left = Unit.FromCentimeter(1.2);
            frame.FillFormat.Color = new Color(246, 243, 234);
            frame.LineFormat.Width = 0.75;
            frame.LineFormat.Color = Colors.DarkSlateGray;
            frame.MarginTop = Unit.FromPoint(8);
            frame.MarginLeft = Unit.FromPoint(10);
            frame.MarginRight = Unit.FromPoint(10);

            frame.WrapFormat.Style = style;

            // All four distances mean something for a side-wrapped shape. Left and Right hold the
            // text off horizontally, as they always claimed to; Top and Bottom grow the obstacle
            // vertically, so a line whose box would otherwise clear the frame by a hair is pushed
            // past it instead of grazing it.
            frame.WrapFormat.DistanceLeft = Unit.FromPoint(10);
            frame.WrapFormat.DistanceRight = Unit.FromPoint(10);
            frame.WrapFormat.DistanceTop = Unit.FromPoint(4);
            frame.WrapFormat.DistanceBottom = Unit.FromPoint(4);
            // docs:end wrap-frame

            var inside = frame.AddParagraph("A sidebar");
            inside.Format.Font.Name = "Liberation Sans";
            inside.Format.Font.Bold = true;
            inside.Format.SpaceAfter = Unit.FromPoint(4);

            var insideBody = frame.AddParagraph(
                "Whatever goes in the frame is laid out inside it, independently of the copy "
                + "flowing past outside.");
            insideBody.Format.Font.Size = 8.5;

            var body = section.AddParagraph(string.Concat(Prose, Prose, Prose));
            body.Format.Alignment = ParagraphAlignment.Justify;
            body.Format.FirstLineIndent = Unit.FromPoint(0);
        }

        var renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();
        return renderer.PdfDocument;
        #endregion
    }
}
