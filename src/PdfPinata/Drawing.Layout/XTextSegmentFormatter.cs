using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf.IO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// Lays out a sequence of <see cref="TextSegment"/> into a rectangle, wrapping between segments as
/// well as within them, so that a paragraph whose font or colour changes part way through is drawn
/// as one flow.
/// </summary>
public class XTextSegmentFormatter
{
    private readonly XGraphics _gfx;

    /// <summary>
    /// Initializes a new instance of the <see cref="XTextSegmentFormatter"/> class.
    /// </summary>
    public XTextSegmentFormatter(XGraphics gfx)
    {
        ArgumentNullException.ThrowIfNull(gfx);

        _gfx = gfx;
    }

    /// <summary>
    /// Gets or sets the alignment of the text.
    /// </summary>
    public XParagraphAlignment Alignment { get; set; }

    /// <summary>
    /// Draws the text.
    /// </summary>
    /// <param name="text">The text to be drawn.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The text brush.</param>
    /// <param name="layoutRectangle">The layout rectangle.</param>
    public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle)
    {
        var textSegments = new List<TextSegment>
        {
            new() { Font = font, Brush = brush, Text = text }
        };

        DrawString(textSegments, layoutRectangle, XStringFormats.TopLeft);
    }

    /// <summary>
    /// Draws the text.
    /// </summary>
    /// <param name="text">The text to be drawn.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The text brush.</param>
    /// <param name="layoutRectangle">The layout rectangle.</param>
    /// <param name="format">The format. Must be <c>XStringFormat.TopLeft</c></param>
    public void DrawString(string text, XFont font, XBrush brush, XRect layoutRectangle, XStringFormat format)
    {
        var textSegments = new List<TextSegment>
        {
            new() { Font = font, Brush = brush, Text = text }
        };

        DrawString(textSegments, layoutRectangle, format);
    }

    /// <summary>
    /// Draws the text.
    /// </summary>
    /// <param name="textSegments">The texts to be drawn with font and color information</param>
    /// <param name="layoutRectangle">The layout rectangle.</param>
    public void DrawString(IEnumerable<TextSegment> textSegments, XRect layoutRectangle)
    {
        DrawString(textSegments, layoutRectangle, XStringFormats.TopLeft);
    }

    /// <summary>
    /// Draws the text.
    /// </summary>
    /// <param name="textSegments">The texts to be drawn with font and color information.</param>
    /// <param name="layoutRectangle">The layout rectangle.</param>
    /// <param name="format">The format. Must be <c>XStringFormat.TopLeft</c></param>
    public void DrawString(IEnumerable<TextSegment> textSegments, XRect layoutRectangle, XStringFormat format)
    {
        ProcessTextSegments(
            textSegments,
            layoutRectangle,
            format,
            (block, dx, dy) => _gfx.DrawString(block.Text, block.Environment.Font, block.Environment.Brush,
                dx + block.Location.X, dy + block.Location.Y),
            false
        );
    }

    /// <summary>
    /// Calculates the size of the given text
    /// </summary>
    /// <param name="text">The text to be drawn.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The text brush.</param>
    /// <param name="width">Max text width</param>
    /// <returns></returns>
    public XSize CalculateTextSize(string text, XFont font, XBrush brush, double width)
    {
        return CalculateTextSize(text, font, brush, width, XStringFormats.TopLeft);
    }

    /// <summary>
    /// Calculates the size of the given text
    /// </summary>
    /// <param name="text">The text to be drawn.</param>
    /// <param name="font">The font.</param>
    /// <param name="brush">The text brush.</param>
    /// <param name="width">Max text width</param>
    /// <param name="format">The format. Must be <c>XStringFormat.TopLeft</c>.</param>
    /// <returns></returns>
    public XSize CalculateTextSize(string text, XFont font, XBrush brush, double width, XStringFormat format)
    {
        var textSegments = new List<TextSegment>
        {
            new() { Font = font, Brush = brush, Text = text }
        };

        return CalculateTextSize(textSegments, width, format);
    }

    /// <summary>
    /// Calculates the size of the given text
    /// </summary>
    /// <param name="textSegments">The texts to be drawn with font and color information.</param>
    /// <param name="width">Max text width</param>
    /// <returns></returns>
    public XSize CalculateTextSize(IEnumerable<TextSegment> textSegments, double width)
    {
        return CalculateTextSize(textSegments, width, XStringFormats.TopLeft);
    }

    /// <summary>
    /// Calculates the size of the given text
    /// </summary>
    /// <param name="textSegments">The texts to be drawn with font and color information.</param>
    /// <param name="width">Max text width</param>
    /// <param name="format">The format. Must be <c>XStringFormat.TopLeft</c>.</param>
    /// <returns></returns>
    public XSize CalculateTextSize(IEnumerable<TextSegment> textSegments, double width, XStringFormat format)
    {
        var layoutRectangle = new XRect(0, 0, width, 100000000);
        var blocks = new List<Block>();

        ProcessTextSegments(textSegments, layoutRectangle, format, (block, _, _) => blocks.Add(block), true);

        var height = blocks.Count > 0
            ? blocks.Max(b => b.Location.Y)
            : 0;
        var maxLineHeight = 0.0;
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (blocks[i].Type == BlockType.LineBreak)
            {
                break;
            }

            maxLineHeight = Math.Max(maxLineHeight, blocks[i].Environment.LineSpace);
        }

        var calculatedWith = blocks.Count > 0
            ? blocks.Max(b => b.Location.X + b.Width)
            : width;

        if (width < calculatedWith)
        {
            calculatedWith = width;
        }

        return new XSize(calculatedWith, height + maxLineHeight);
    }

    private void ProcessTextSegments(IEnumerable<TextSegment> textSegments, XRect layoutRectangle, XStringFormat format,
        Action<Block, double, double> applyBlock, bool applyBlockIfLineBreak)
    {
        TextSegment[] segments = [..textSegments];

        if (segments.All(ts => string.IsNullOrEmpty(ts.Text)))
        {
            return;
        }

        Validate(segments, format);

        foreach (var segment in segments)
        {
            SetFontSpacings(segment);
        }

        var blockUnits = GroupIntoBlockUnits(CreateBlocks(segments));

        CreateLayout(blockUnits, layoutRectangle);

        for (var index = 0; index < blockUnits.Count; index++)
        {
            var blockUnit = blockUnits[index];
            var maxCyAscend = blockUnit.Max(b => b.Environment.CyAscent);
            var dx = layoutRectangle.Location.X;
            var dy = layoutRectangle.Location.Y + maxCyAscend;

            ShiftLaterBlockUnitsDown(blockUnits, index, maxCyAscend);

            ApplyBlocks(blockUnit, dx, dy, applyBlock, applyBlockIfLineBreak);
        }
    }

    /// <summary>
    /// Hands each block of a unit to <paramref name="applyBlock"/>, up to the first that did not fit.
    /// </summary>
    private static void ApplyBlocks(List<Block> blockUnit, double dx, double dy, Action<Block, double, double> applyBlock,
        bool applyBlockIfLineBreak)
    {
        foreach (var block in blockUnit)
        {
            if (block.Stop)
            {
                break;
            }

            if (block.Type == BlockType.LineBreak && !applyBlockIfLineBreak)
            {
                continue;
            }

            applyBlock(block, dx, dy);
        }
    }

    private static void Validate(TextSegment[] textSegments, XStringFormat format)
    {
        if (textSegments.Any(ts => ts.Font == default))
        {
            throw new ArgumentNullException(nameof(textSegments), "Every text segment needs a font.");
        }

        if (textSegments.Any(ts => ts.Brush == default))
        {
            throw new ArgumentNullException(nameof(textSegments), "Every text segment needs a brush.");
        }

        if (format.Alignment != XStringAlignment.Near || format.LineAlignment != XLineAlignment.Near)
        {
            throw new ArgumentException("Only TopLeft alignment is currently implemented.");
        }
    }

    /// <summary>
    /// Cuts the blocks into units, each ending at a line break the text itself asked for.
    /// </summary>
    private static List<List<Block>> GroupIntoBlockUnits(List<Block> blocks)
    {
        var blockUnits = new List<List<Block>>();
        var currentBlockUnit = new List<Block>();
        foreach (var block in blocks)
        {
            currentBlockUnit.Add(block);

            if (!EndsBlockUnit(block))
                continue;

            blockUnits.Add(currentBlockUnit);
            currentBlockUnit = [];
        }

        if (!EndsBlockUnit(blocks.Last()))
        {
            blockUnits.Add(currentBlockUnit);
        }

        return blockUnits;
    }

    private static bool EndsBlockUnit(Block block) => block.Stop || block.Type == BlockType.LineBreak;

    /// <summary>
    /// Moves every later unit down when the first block of this one does not have the largest
    /// ascent on its line.
    /// </summary>
    private static void ShiftLaterBlockUnitsDown(List<List<Block>> blockUnits, int index, double maxCyAscend)
    {
        var blockUnit = blockUnits[index];

        // Check all blocks of the current line in order to move all blocks of the next lines down,
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        // when the first block of the current line has not the max cy ascent of the whole line
#pragma warning disable S1244 // Exact on purpose: compared with a maximum or minimum taken from these same values.
        if (!blockUnit.Any(b => b.Environment.CyAscent != maxCyAscend))
#pragma warning restore S1244
            return;

        for (var indexSiblings = index + 1; indexSiblings < blockUnits.Count; indexSiblings++)
        {
            blockUnits[indexSiblings].ForEach(b =>
                b.Location += new XVector(0, maxCyAscend - blockUnit.First().Environment.CyAscent));
        }
    }

    private List<Block> CreateBlocks(IEnumerable<TextSegment> textSegments)
    {
        var blocks = new List<Block>();

        foreach (var textSegment in textSegments)
        {
            if (string.IsNullOrEmpty(textSegment.Text))
            {
                continue;
            }

            // Check whether the current block belongs to the last block
            if (blocks.Count > 0 && !textSegment.Text.StartsWith(' '))
            {
                blocks.Last().NextBlockBelongsToMe = true;
            }

            AddBlocksOf(textSegment, blocks);
        }

        return blocks;
    }

    /// <summary>
    /// Cuts one segment into words and line breaks. A run of white space at the start of the
    /// segment is kept at the front of the first word rather than dropped.
    /// </summary>
    private void AddBlocksOf(TextSegment textSegment, List<Block> blocks)
    {
        var text = textSegment.Text;
        var length = text.Length;
        var inNonWhiteSpace = false;
        var startIndex = 0;
        var blockLength = 0;

        for (var idx = 0; idx < length; idx++)
        {
            var ch = ReadCharacter(text, ref idx);

            if (ch == Chars.LF)
            {
                AddTextBlockIfAny(textSegment, startIndex, blockLength, blocks);

                startIndex = idx + 1;
                blockLength = 0;

                blocks.Add(LineBreakBlock(textSegment));
            }
            else if (!char.IsWhiteSpace(ch))
            {
                inNonWhiteSpace = true;
                blockLength++;
            }
            else if (inNonWhiteSpace)
            {
                blocks.Add(TextBlock(text.Substring(startIndex, blockLength).Trim(), textSegment));
                startIndex = idx + 1;
                blockLength = 0;
            }
            else
            {
                blockLength++;
            }
        }

        AddTextBlockIfAny(textSegment, startIndex, blockLength, blocks);
    }

    private void AddTextBlockIfAny(TextSegment textSegment, int startIndex, int blockLength, List<Block> blocks)
    {
        if (blockLength != 0)
        {
            blocks.Add(TextBlock(textSegment.Text.Substring(startIndex, blockLength), textSegment));
        }
    }

    /// <summary>
    /// The character at <paramref name="idx"/>, with CR and CRLF read as LF - a CRLF moving the
    /// index on past its LF.
    /// </summary>
    private static char ReadCharacter(string text, ref int idx)
    {
        var ch = text[idx];
        if (ch != Chars.CR)
            return ch;

        if (idx < text.Length - 1 && text[idx + 1] == Chars.LF)
        {
            idx++;
        }

        return Chars.LF;
    }

    private Block TextBlock(string token, TextSegment textSegment)
    {
        var block = new Block(token, BlockType.Text, _gfx.MeasureString(token, textSegment.Font).Width)
        {
            LineIndent = textSegment.LineIndent,
            SkipParagraphAlignment = textSegment.SkipParagraphAlignment
        };
        SetFormatterEnvironment(block, textSegment);
        return block;
    }

    private Block LineBreakBlock(TextSegment textSegment)
    {
        var lineBreakBlock = new Block(BlockType.LineBreak);
        SetFormatterEnvironment(lineBreakBlock, textSegment);
        return lineBreakBlock;
    }

    /// <summary>
    /// Where the pen is while the lines are being laid out. The position runs on from one block
    /// unit to the next; the rest is started afresh for each unit.
    /// </summary>
    private sealed class LayoutState
    {
        internal LayoutState(double rectWidth, double rectHeight)
        {
            RectWidth = rectWidth;
            RectHeight = rectHeight;
        }

        internal double RectWidth { get; }
        internal double RectHeight { get; }

        internal double X { get; set; }
        internal double Y { get; set; }

        /// <summary>The index of the first block of the line being built.</summary>
        internal int FirstIndex { get; set; }

        internal double StartLineSpace { get; set; }
        internal double CurrentMaxLineSpace { get; set; }
        internal double CurrentMaxCyDescent { get; set; }
        internal List<Block> CurrentLineBlocks { get; private set; }

        internal void StartBlockUnit(double startLineSpace)
        {
            FirstIndex = 0;
            CurrentMaxLineSpace = 0.0;
            CurrentMaxCyDescent = 0.0;
            CurrentLineBlocks = [];
            StartLineSpace = startLineSpace;
        }
    }

    private void CreateLayout(List<List<Block>> blockUnits, XRect layoutRectangle)
    {
        var state = new LayoutState(
            layoutRectangle.Width,
            layoutRectangle.Height - blockUnits.First().First().Environment.CyAscent -
            blockUnits.Last().Last().Environment.CyDescent);

        foreach (var blockUnit in blockUnits)
        {
            LayOutBlockUnit(blockUnit, state);
        }
    }

    private void LayOutBlockUnit(List<Block> blockUnit, LayoutState state)
    {
        var count = blockUnit.Count;
        state.StartBlockUnit(blockUnit[0].Environment.LineSpace);

        for (var idx = 0; idx < count; idx++)
        {
            var block = blockUnit[idx];
            var stopped = block.Type == BlockType.LineBreak
                ? BreakLine(blockUnit, idx, state)
                : PlaceTextBlock(blockUnit, ref idx, state);

            if (stopped)
            {
                break;
            }
        }

        if (state.FirstIndex < count && Alignment != XParagraphAlignment.Justify)
        {
            AlignLine(blockUnit, state.FirstIndex, count - 1, state.RectWidth);
        }
    }

    /// <summary>
    /// Ends the line at a line break the text asked for. Answers true when the next line would
    /// not fit in the rectangle, having marked the break as where drawing stops.
    /// </summary>
    private bool BreakLine(List<Block> blockUnit, int idx, LayoutState state)
    {
        var block = blockUnit[idx];

        if (Alignment == XParagraphAlignment.Justify)
        {
            blockUnit[state.FirstIndex].Alignment = XParagraphAlignment.Left;
        }

        AlignLine(blockUnit, state.FirstIndex, idx - 1, state.RectWidth);
        state.FirstIndex = idx + 1;
        state.X = 0;

        var nextLineStartsWith = idx + 1 < blockUnit.Count ? blockUnit[idx + 1] : block;
        state.StartLineSpace = nextLineStartsWith.Environment.LineSpace;
        var startCyDescent = nextLineStartsWith.Environment.CyDescent;

        state.CurrentMaxLineSpace = state.StartLineSpace;
        state.CurrentMaxCyDescent = startCyDescent;

        state.Y += state.CurrentMaxLineSpace;
        state.CurrentLineBlocks.Clear();

        if (state.Y > state.RectHeight)
        {
            block.Stop = true;

            return true;
        }

        // necessary to correctly calculate closing line breaks
        block.Location = new XPoint(0, state.Y);
        return false;
    }

    /// <summary>
    /// Puts a word on the current line if it fits, and on a new one if it does not. Answers true
    /// when the new line would not fit in the rectangle.
    /// </summary>
    private bool PlaceTextBlock(List<Block> blockUnit, ref int idx, LayoutState state)
    {
        var block = blockUnit[idx];
        var width = block.Width;

        if (state.X == 0.0)
        {
            state.X += block.LineIndent;
        }

        var fitsOnThisLine = state.X + width <= state.RectWidth || state.X == 0.0;
        if (!fitsOnThisLine)
        {
            return WrapLine(blockUnit, ref idx, state);
        }

        // if the font style is set to "underline", we don't want a underlined space character
        PlaceOnLine(block, state.X, width, state);

        state.CurrentMaxLineSpace = Math.Max(block.Environment.LineSpace, state.CurrentMaxLineSpace);
        state.CurrentMaxCyDescent = Math.Max(block.Environment.CyDescent, state.CurrentMaxCyDescent);
        return false;
    }

    /// <summary>
    /// Starts a new line with the word at <paramref name="idx"/>, or with the first of the words
    /// linked to it. Answers true when the new line would not fit in the rectangle.
    /// </summary>
    private bool WrapLine(List<Block> blockUnit, ref int idx, LayoutState state)
    {
        // if the previous blocks are linked to the current block, all linked blocks have to be moved to the next line
        while (idx > 0 && blockUnit[idx - 1].NextBlockBelongsToMe)
        {
            idx--;
            state.CurrentLineBlocks.RemoveAt(state.CurrentLineBlocks.Count - 1);
        }

        var block = blockUnit[idx];

        AlignLine(blockUnit, state.FirstIndex, idx - 1, state.RectWidth);
        state.FirstIndex = idx;
// ReSharper disable once CompareOfFloatsByEqualityOperator

#pragma warning disable S1244 // Exact on purpose: unchanged unless a larger value replaced it.
        if (state.CurrentMaxLineSpace != state.StartLineSpace)
#pragma warning restore S1244
        {
            state.Y += -state.StartLineSpace + state.CurrentMaxLineSpace;
            state.CurrentLineBlocks.ForEach(b => b.Location = new XPoint(b.Location.X, state.Y));
        }

        state.StartLineSpace = block.Environment.LineSpace;
        var startCyDescent = block.Environment.CyDescent;

        if (state.StartLineSpace < state.CurrentMaxLineSpace)
        {
            var cyDescentDiff = state.CurrentMaxCyDescent - startCyDescent;
            state.Y += cyDescentDiff;
        }

        state.CurrentMaxLineSpace = state.StartLineSpace;
        state.CurrentMaxCyDescent = startCyDescent;

        state.Y += state.CurrentMaxLineSpace;
        state.CurrentLineBlocks.Clear();

        if (state.Y > state.RectHeight)
        {
            block.Stop = true;

            return true;
        }

        // A new line must not start with a space character
        PlaceOnLine(block, block.LineIndent, block.Width, state);
        return false;
    }

    /// <summary>
    /// Puts a block on the current line at <paramref name="x"/>, without any space it starts
    /// with, and moves the pen past it - and past the space after it, unless the next block is
    /// joined to it.
    /// </summary>
    private static void PlaceOnLine(Block block, double x, double width, LayoutState state)
    {
        width = RemovedLeadingSpace(block, width);
        block.Location = new XPoint(x, state.Y);
        state.X = x + width;
        if (!block.NextBlockBelongsToMe)
        {
            // The current and the next block are treated as one unit, so there is no space between them
            state.X += block.Environment.SpaceWidth;
        }

        state.CurrentLineBlocks.Add(block);
    }

    private static double RemovedLeadingSpace(Block block, double width)
    {
        while (block.Text.StartsWith(' '))
        {
            block.Text = block.Text[1..];
            block.Width -= block.Environment.SpaceWidth;
            width -= block.Environment.SpaceWidth;
        }

        return width;
    }

    /// <summary>
    /// Align center, right, or justify.
    /// </summary>
    private void AlignLine(List<Block> blockUnit, int firstIndex, int lastIndex, double layoutWidth)
    {
        var firstBlock = blockUnit[firstIndex];
        var blockAlignment = firstBlock.Alignment;

        if (Alignment == XParagraphAlignment.Left || blockAlignment == XParagraphAlignment.Left)
        {
            return;
        }

        var count = lastIndex - firstIndex + 1;
        if (count == 0)
        {
            return;
        }

        var totalWidth = firstBlock.LineIndent;
        if (Alignment == XParagraphAlignment.Justify)
        {
            int skipped;
            (firstIndex, skipped, layoutWidth) = SkipUnmovableLeadingBlocks(blockUnit, firstIndex, lastIndex, layoutWidth);
            count -= skipped;
        }

        // Remove not movable blocks from space calculation
        totalWidth = LineWidth(blockUnit, firstIndex, lastIndex, totalWidth);
        count -= CountLinked(blockUnit, firstIndex, lastIndex);

        var dx = Math.Max(layoutWidth - totalWidth, 0);

        if (Alignment != XParagraphAlignment.Justify)
        {
            // right or center
            ShiftLine(blockUnit, firstIndex, lastIndex, Alignment == XParagraphAlignment.Center ? dx / 2 : dx);
        }
        else if (count > 1) // case: justify
        {
            SpreadLine(blockUnit, firstIndex, lastIndex, dx / (count - 1));
        }
    }

    /// <summary>
    /// The width a block takes on its line: itself, and the space after it unless the next block
    /// is joined to it.
    /// </summary>
    private static double AdvanceOf(Block block) => block.Width + SpaceAfter(block);

    private static double SpaceAfter(Block block) => block.NextBlockBelongsToMe ? 0 : block.Environment.SpaceWidth;

    /// <summary>
    /// Skips the blocks at the start of a justified line that must not move, and takes their
    /// width off what there is to spread across.
    /// </summary>
    private static (int FirstIndex, int Skipped, double LayoutWidth) SkipUnmovableLeadingBlocks(
        List<Block> blockUnit, int firstIndex, int lastIndex, double layoutWidth)
    {
        var skipped = 0;
        for (var idx = firstIndex; idx <= lastIndex; idx++)
        {
            var block = blockUnit[idx];
            if (!block.SkipParagraphAlignment && !block.NextBlockBelongsToMe)
            {
                return (idx, skipped, layoutWidth);
            }

            skipped++;
            layoutWidth -= AdvanceOf(block);
        }

        return (firstIndex, skipped, layoutWidth);
    }

    /// <summary>
    /// The width of the blocks from <paramref name="firstIndex"/> to <paramref name="lastIndex"/>,
    /// added to <paramref name="totalWidth"/>, without the space after the last of them.
    /// </summary>
    private static double LineWidth(List<Block> blockUnit, int firstIndex, int lastIndex, double totalWidth)
    {
        for (var idx = firstIndex; idx <= lastIndex; idx++)
        {
            totalWidth += AdvanceOf(blockUnit[idx]);
            if (idx == lastIndex)
            {
                totalWidth -= SpaceAfter(blockUnit[idx]);
            }
        }

        return totalWidth;
    }

    private static int CountLinked(List<Block> blockUnit, int firstIndex, int lastIndex)
    {
        var linked = 0;
        for (var idx = firstIndex; idx <= lastIndex; idx++)
        {
            if (blockUnit[idx].NextBlockBelongsToMe)
            {
                linked++;
            }
        }

        return linked;
    }

    private static void ShiftLine(List<Block> blockUnit, int firstIndex, int lastIndex, double dx)
    {
        for (var idx = firstIndex; idx <= lastIndex; idx++)
        {
            var block = blockUnit[idx];
            block.Location += new XVector(dx, 0);
        }
    }

    /// <summary>
    /// Justifies a line: every gap between words, but not between joined blocks, gets the same
    /// share of <paramref name="gap"/>.
    /// </summary>
    private static void SpreadLine(List<Block> blockUnit, int firstIndex, int lastIndex, double gap)
    {
        var spaceCounter = 1;

        for (var idx = firstIndex + 1; idx <= lastIndex; idx++)
        {
            var block = blockUnit[idx];
            block.Location += new XVector(gap * spaceCounter, 0);
            if (!block.NextBlockBelongsToMe)
            {
                spaceCounter++;
            }
        }
    }

    private void SetFormatterEnvironment(Block block, TextSegment textSegment)
    {
        block.Alignment = Alignment;
        block.Environment = new FormatterEnvironment
        {
            Font = textSegment.Font,
            Brush = textSegment.Brush,
            LineSpace = textSegment.LineSpace,
            CyAscent = textSegment.CyAscent,
            CyDescent = textSegment.CyDescent,
            SpaceWidth = textSegment.SpaceWidth
        };
    }

    private void SetFontSpacings(TextSegment segment)
    {
        if (segment.Font == null)
        {
            throw new ArgumentNullException(nameof(segment), "The text segment has no font.");
        }

        segment.LineSpace = segment.Font.GetHeight();
        segment.CyAscent = segment.LineSpace * segment.Font.CellAscent / segment.Font.CellSpace;
        segment.CyDescent = segment.LineSpace * segment.Font.CellDescent / segment.Font.CellSpace;

        // HACK in XTextSegmentFormatter
        segment.SpaceWidth = _gfx.MeasureString("x x", segment.Font).Width;
        segment.SpaceWidth -= _gfx.MeasureString("xx", segment.Font).Width;
    }
}
