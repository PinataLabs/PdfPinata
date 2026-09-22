using System;
using System.Diagnostics;
using System.Text;

namespace PdfPinata.Pdf.Content.Objects;

/// <summary>
/// Represents an inline image in a PDF content stream: the <c>BI</c> operator, the image's own
/// dictionary, <c>ID</c>, the image data and <c>EI</c>, which together draw one image.
/// </summary>
/// <remarks>
/// <para>
/// The dictionary and the data are kept as they were written rather than interpreted, so that a
/// content stream read with <see cref="ContentReader"/> and written back out with
/// <see cref="CSequence.ToContent"/> draws the same image. The parser used to step over an inline
/// image altogether, leaving a bare <c>BI</c> and <c>EI</c> in the sequence and nothing between
/// them, so writing the content back lost every inline image in it.
/// </para>
/// <para>
/// It is a <see cref="COperator"/> whose <see cref="COperator.OpCode"/> is <c>BI</c>, which is
/// what code walking a sequence for operators already recognises an inline image by.
/// </para>
/// </remarks>
[DebuggerDisplay("(BI {ImageDictionary}, data={Data.Length})")]
public class CInlineImage : COperator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CInlineImage"/> class with no entries and no
    /// data.
    /// </summary>
    public CInlineImage() : this("", [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CInlineImage"/> class.
    /// </summary>
    /// <param name="imageDictionary">The entries of the image dictionary, as they are written
    /// between <c>BI</c> and <c>ID</c>.</param>
    /// <param name="data">The image data, as it is written between <c>ID</c> and <c>EI</c>.</param>
    public CInlineImage(string imageDictionary, byte[] data) : base(OpCodes.OperatorFromName("BI").OpCode)
    {
        ImageDictionary = imageDictionary;
        Data = data;
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CInlineImage Clone()
    {
        return (CInlineImage)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var copy = (CInlineImage)base.Copy();
        copy._data = (byte[])_data.Clone();
        return copy;
    }

    /// <summary>
    /// Gets or sets the entries of the image dictionary, as they are written between <c>BI</c> and
    /// <c>ID</c> - for instance <c>/W 4 /H 4 /CS /G /BPC 8</c> - one character per byte.
    /// </summary>
    /// <remarks>
    /// Kept as text rather than as content objects: an inline image dictionary may hold
    /// <c>true</c>, <c>false</c> and <c>null</c>, which the content object model has no type for.
    /// </remarks>
    public string ImageDictionary
    {
        get => _imageDictionary;
        set => _imageDictionary = value ?? throw new ArgumentNullException(nameof(value));
    }

    string _imageDictionary;

    /// <summary>
    /// Gets or sets the image data, as it is written between <c>ID</c> and <c>EI</c>: from after the
    /// single white-space character that follows <c>ID</c> up to the <c>E</c> of <c>EI</c>.
    /// </summary>
    /// <remarks>
    /// Any white space written between the data and <c>EI</c> is part of it, because nothing short
    /// of decoding the image says whether a last blank or line feed is data or a separator.
    /// </remarks>
    public byte[] Data
    {
        get => _data;
        set => _data = value ?? throw new ArgumentNullException(nameof(value));
    }

    byte[] _data;

    /// <summary>
    /// Returns the inline image as it is written in a content stream, its data one character per
    /// byte.
    /// </summary>
    public override string ToString()
    {
        var s = new StringBuilder("BI\n");
        if (_imageDictionary.Length > 0)
            s.Append(_imageDictionary).Append('\n');
        s.Append("ID ");
        foreach (var b in _data)
            s.Append((char)b);
        if (!EndsInWhiteSpace())
            s.Append('\n');
        s.Append("EI");
        return s.ToString();
    }

    /// <summary>
    /// Whether the data already ends in the white space that separates it from <c>EI</c>. Data
    /// read out of a content stream nearly always does; data that does not is given a line feed,
    /// so that <c>EI</c> is not written hard against its last byte.
    /// </summary>
    bool EndsInWhiteSpace() => _data.Length > 0 && CLexer.IsWhiteSpace((char)_data[^1]);

    internal override void WriteObject(ContentWriter writer)
    {
        // Operands before BI are malformed, but they were read, so they are written back out.
        foreach (var operand in Operands)
            operand.WriteObject(writer);

        writer.WriteLineRaw(ToString());
    }
}
