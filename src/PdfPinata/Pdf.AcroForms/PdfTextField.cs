#region Copyright
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#endregion

using System;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the text field.
/// </summary>
public sealed class PdfTextField : PdfAcroField
{
    /// <summary>
    /// Initializes a new instance of PdfTextField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    public PdfTextField(PdfDocument document)
        : base(document, "/Tx")
    {
    }

    internal PdfTextField(PdfDictionary dict)
        : base(dict)
    {
    }

    /// <summary>
    /// Gets or sets the text value of the text field.
    /// </summary>
    public string Text
    {
        get => Elements.GetString(PdfAcroField.Keys.V);
        set
        {
            EnsureCanBeFilled();

            Elements.SetString(PdfAcroField.Keys.V, value);
            RenderAppearance();
        } //HACK in PdfTextField
    }

    /// <summary>
    /// Gets or sets the font used to draw the text of the field. Unset, it is the resolver's
    /// default font at the size the field's <c>/DA</c> names, or 10 points when that names none
    /// or asks for auto-sizing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The size comes from <c>/DA</c> because <c>/DA</c> is what a viewer edits the field in: a
    /// drawing in any other size makes the text jump as the field gains and loses the focus. It
    /// used to be a fixed 10 points whatever <c>/DA</c> said (issue #155).
    /// </para>
    /// <para>
    /// This and the colours redraw the field, as <see cref="Text"/> does. They used not to: the
    /// appearance was drawn from them when the value changed and at no other time, so setting a
    /// colour on a field whose value was already in place did nothing at all, and setting one on
    /// a field that never gets a value did nothing ever.
    /// </para>
    /// </remarks>
    public XFont Font
    {
        get => _font ?? FontFromDefaultAppearance();
        set
        {
            _font = value;
            RenderAppearance();
        }
    }

    private XFont _font;

    /// <summary>
    /// Gets or sets the colour of the text. Unset, or set to <see cref="XColor.Empty"/>, it is the
    /// colour the field's <c>/DA</c> names, or black.
    /// </summary>
    public XColor ForeColor
    {
        get => _foreColor.IsEmpty ? ColorFromDefaultAppearance() : _foreColor;
        set
        {
            _foreColor = value;
            RenderAppearance();
        }
    }

    private XColor _foreColor = XColor.Empty;

    internal override void OnDefaultAppearanceChanged() => RenderAppearance();

    // BackColor and BorderColor are PdfAcroField's: they are drawn here, and written to each
    // widget's /MK for a viewer that builds its own field instead.
    internal override void OnAppearanceCharacteristicsChanged() => RenderAppearance();

    /// <summary>
    /// Gets or sets the maximum length of the field.
    /// </summary>
    /// <value>The length of the max.</value>
    public int MaxLength
    {
        get => Elements.GetInteger(Keys.MaxLen);
        set => Elements.SetInteger(Keys.MaxLen, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the field has multiple lines.
    /// </summary>
    public bool MultiLine
    {
        get => (Flags & PdfAcroFieldFlags.Multiline) != 0;
        set
        {
            if (value)
                SetFlags |= PdfAcroFieldFlags.Multiline;
            else
                SetFlags &= ~PdfAcroFieldFlags.Multiline;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this field is used for passwords.
    /// </summary>
    public bool Password
    {
        get => (Flags & PdfAcroFieldFlags.Password) != 0;
        set
        {
            if (value)
                SetFlags |= PdfAcroFieldFlags.Password;
            else
                SetFlags &= ~PdfAcroFieldFlags.Password;
        }
    }

    /// <summary>
    /// Creates the normal appearance form X object for each annotation that represents this acro
    /// form text field.
    /// </summary>
    /// <remarks>
    /// A field merged with its single widget carries the rectangle itself, and is its own
    /// annotation; a field whose widgets are separate objects carries none, and its annotations
    /// are the dictionaries under <c>/Kids</c>. This used to read <c>/Rect</c> off the field
    /// whatever its shape, so an unmerged field - which is every field
    /// <see cref="PdfAcroField.AddWidget"/> builds, and plenty that other software writes - drew
    /// its value into a form of no size at all and hung it on the field, where no reader looks.
    /// </remarks>
    private void RenderAppearance()
    {
        if (Elements.ContainsKey(PdfAnnotation.Keys.Rect))
        {
            RenderAppearanceOn(this);
            return;
        }

        // The widgets alone. /Kids can hold nested fields as well, each with a value of its own,
        // and drawing this field's value into them drew it over theirs.
        foreach (var widget in Widgets)
        {
            if (widget.Elements.ContainsKey(PdfAnnotation.Keys.Rect))
                RenderAppearanceOn(widget);
        }
    }

    internal override void OnWidgetAdded()
    {
        // A field is usually described before it is placed, and until it is placed there is no
        // rectangle to draw in - so everything set beforehand would be lost without this.
        RenderAppearance();
    }

    private void RenderAppearanceOn(PdfDictionary annotation)
    {
        var rect = annotation.Elements.GetRectangle(PdfAnnotation.Keys.Rect);

        // A rectangle too small to draw in draws nothing, and XForm refuses to be made of one:
        // its floor is a point in each direction, so the test is against 1 rather than against 0.
        // A field reaches this while it is still being assembled, so it is a stage rather than a
        // fault. It also keeps the border below from being given a negative width.
        if (rect.Width < 1 || rect.Height < 1)
            return;

        // Nothing asked for. An appearance is what a reader shows in place of building one from
        // /MK, so writing an empty one here would blank a field decorated that way rather than
        // leave it alone - which is the difference between "draw nothing" and "draw it yourself".
        if (BackColor.IsEmpty && BorderColor.IsEmpty && Text.Length == 0)
        {
            annotation.Elements.Remove(PdfAnnotation.Keys.AP);
            return;
        }

        var form = new XForm(_document, rect.Size);
        var gfx = XGraphics.FromForm(form);

        if (!BackColor.IsEmpty)
            gfx.DrawRectangle(new XSolidBrush(BackColor), rect.ToXRect() - rect.Location);

        if (!BorderColor.IsEmpty)
        {
            // Inside the rectangle rather than centred on its edge, so that the outer half of the
            // stroke is not clipped by the annotation's own bounds.
            gfx.DrawRectangle(new XPen(BorderColor, 1),
                new XRect(0.5, 0.5, rect.Width - 1, rect.Height - 1));
        }

        // The bracket is written even with no text in it, so that a viewer editing the field
        // knows where its text goes.
        using (gfx.BeginVariableText())
        {
            var text = Text;
            if (text.Length > 0)
            {
                // Clipped inside the border, as a viewer clips the text it draws while editing.
                gfx.Save();
                gfx.IntersectClip(new XRect(1, 1, Math.Max(rect.Width - 2, 0), Math.Max(rect.Height - 2, 0)));
                DrawValue(gfx, text, new XSize(rect.Width, rect.Height));
                gfx.Restore();
            }
        }

        SetVariableTextAppearance(annotation, form);
    }

    /// <summary>
    /// Draws the value the way a viewer lays it out while the field is being edited, so that
    /// giving the field the focus and taking it away again does not move the text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One line is centred vertically, two points in from the side it is aligned to; several are
    /// wrapped and start two points from the top. A comb field puts one character in the middle
    /// of each of <see cref="MaxLength"/> equal cells, and a password field draws an asterisk for
    /// each character rather than the character. <c>/Q</c> says which side a line is aligned to.
    /// </para>
    /// <para>
    /// It used to draw every value as one line from the top left, whatever the field was: a
    /// multi-line value ran off the side, a comb field was ordinary text and a password was
    /// written in the clear into the drawing of it (issue #155).
    /// </para>
    /// </remarks>
    private void DrawValue(XGraphics gfx, string text, XSize size)
    {
        const double Margin = 2;

        var font = Font;
        var brush = new XSolidBrush(ForeColor);
        var alignment = Alignment;

        if (Password)
            text = new string('*', text.Length);

        var cells = MaxLength;
        if (Comb && cells > 0 && !MultiLine && !Password)
        {
            var cellWidth = size.Width / cells;
            for (var index = 0; index < text.Length && index < cells; index++)
            {
                gfx.DrawString(text[index].ToString(), font, brush,
                    new XRect(index * cellWidth, 0, cellWidth, size.Height), XStringFormats.Center);
            }
            return;
        }

        var inside = new XRect(Margin, Margin, Math.Max(size.Width - 2 * Margin, 0), Math.Max(size.Height - 2 * Margin, 0));
        if (MultiLine)
        {
            var formatter = new XTextFormatter(gfx)
            {
                Alignment = alignment switch
                {
                    1 => XParagraphAlignment.Center,
                    2 => XParagraphAlignment.Right,
                    _ => XParagraphAlignment.Left
                }
            };
            formatter.DrawString(text, font, brush, inside);
            return;
        }

        gfx.DrawString(text, font, brush, inside, alignment switch
        {
            1 => XStringFormats.Center,
            2 => XStringFormats.CenterRight,
            _ => XStringFormats.CenterLeft
        });
    }

    /// <summary>
    /// Whether the field divides itself into <see cref="MaxLength"/> cells, one character each.
    /// </summary>
    private bool Comb => (Flags & PdfAcroFieldFlags.Comb) != 0;

    /// <summary>
    /// The quadding, <c>/Q</c>: 0 to align left, 1 to centre and 2 to align right. Inheritable,
    /// and the form's when no field in the chain says.
    /// </summary>
    private int Alignment
    {
        get
        {
            var owner = InheritedFrom(this, PdfAcroField.Keys.Q);
            if (owner != null)
                return owner.Elements.GetInteger(PdfAcroField.Keys.Q);

            return Owner?.AcroForm?.Elements.GetInteger(PdfAcroForm.Keys.Q) ?? 0;
        }
    }

    internal override void PrepareForSave()
    {
        base.PrepareForSave();
        RenderAppearance();
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        /// <summary>
        /// (Optional; inheritable) The maximum length of the field’s text, in characters.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string MaxLen = "/MaxLen";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
