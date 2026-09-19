namespace PdfPinata.Drawing.Layout;

/// <summary>
/// A run of text with a font and a brush of its own, as handed to
/// <see cref="XTextSegmentFormatter"/>. Several segments laid out together make a paragraph whose
/// formatting changes part way through, which a single string drawn with one font cannot.
/// </summary>
/// <remarks>
/// The four measurements below the text are filled in by the formatter while it lays the segment
/// out. A caller building a segment sets the font, brush and text and leaves them alone.
/// </remarks>
public class TextSegment
{
	/// <summary>Gets or sets the font this run of text is drawn in.</summary>
	public XFont Font { get; set; }
	/// <summary>Gets or sets the brush this run of text is drawn with.</summary>
	public XBrush Brush { get; set; }
	/// <summary>Gets or sets the text of this run.</summary>
	public string Text { get; set; }
	/// <summary>Gets or sets the indent applied to the line this segment starts.</summary>
	public double LineIndent { get; set; }
	/// <summary>
	/// Gets or sets whether the segment keeps its own position rather than being moved by the
	/// paragraph alignment.
	/// </summary>
	public bool SkipParagraphAlignment { get; set; }

	/// <summary>Gets or sets the line height measured for this segment's font.</summary>
	public double LineSpace { get; set; }
	/// <summary>Gets or sets the ascent measured for this segment's font.</summary>
	public double CyAscent { get; set; }
	/// <summary>Gets or sets the descent measured for this segment's font.</summary>
	public double CyDescent { get; set; }
	/// <summary>Gets or sets the width of a space measured for this segment's font.</summary>
	public double SpaceWidth { get; set; }
}
