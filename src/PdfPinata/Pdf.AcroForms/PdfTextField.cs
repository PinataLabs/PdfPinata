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

using PdfPinata.Drawing;
using PdfPinata.Fonts;
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
    /// Gets or sets the font used to draw the text of the field.
    /// </summary>
    /// <remarks>
    /// This and the three colours below redraw the field, as <see cref="Text"/> does. They used
    /// not to: the appearance was drawn from them when the value changed and at no other time,
    /// so setting a colour on a field whose value was already in place did nothing at all, and
    /// setting one on a field that never gets a value did nothing ever.
    /// </remarks>
    public XFont Font
    {
        get;
        set
        {
            field = value;
            RenderAppearance();
        }
    } = new(GlobalFontSettings.FontResolver.DefaultFontName, 10);

    /// <summary>
    /// Gets or sets the foreground color of the field.
    /// </summary>
    public XColor ForeColor
    {
        get;
        set
        {
            field = value;
            RenderAppearance();
        }
    } = XColors.Black;

    /// <summary>
    /// Gets or sets the background color of the field.
    /// </summary>
    public XColor BackColor
    {
        get => _backColor;
        set
        {
            _backColor = value;
            RenderAppearance();
        }
    }

    private XColor _backColor = XColor.Empty;

    /// <summary>
    /// Gets or sets the colour of the one-point border drawn around the field.
    /// <see cref="XColor.Empty"/>, which is the default, draws none.
    /// </summary>
    /// <remarks>
    /// This field draws its own appearance, and an appearance is what a reader shows in place of
    /// building one from <c>/MK</c> - so a text field decorated only through <c>/MK</c> loses its
    /// box the moment it is given a value. Naming the border here is what lets the drawing the
    /// library makes look like the field the author described.
    /// </remarks>
    public XColor BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            RenderAppearance();
        }
    }

    private XColor _borderColor = XColor.Empty;

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

        var kids = Elements.GetArray(PdfAcroField.Keys.Kids);
        if (kids == null)
            return;

        foreach (var kid in kids.Elements.Items)
        {
            var item = kid is PdfReference reference ? reference.Value : kid;
            if (item is PdfDictionary widget && widget.Elements.ContainsKey(PdfAnnotation.Keys.Rect))
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
        if (_backColor == XColor.Empty && _borderColor == XColor.Empty && Text.Length == 0)
        {
            annotation.Elements.Remove(PdfAnnotation.Keys.AP);
            return;
        }

        var form = new XForm(_document, rect.Size);
        var gfx = XGraphics.FromForm(form);

        if (_backColor != XColor.Empty)
            gfx.DrawRectangle(new XSolidBrush(BackColor), rect.ToXRect() - rect.Location);

        if (_borderColor != XColor.Empty)
        {
            // Inside the rectangle rather than centred on its edge, so that the outer half of the
            // stroke is not clipped by the annotation's own bounds.
            gfx.DrawRectangle(new XPen(_borderColor, 1),
                new XRect(0.5, 0.5, rect.Width - 1, rect.Height - 1));
        }

        var text = Text;
        if (text.Length > 0)
            gfx.DrawString(Text, Font, new XSolidBrush(ForeColor),
                rect.ToXRect() - rect.Location + new XPoint(2, 0), XStringFormats.TopLeft);

        form.DrawingFinished();
        form.PdfForm.Elements.Add("/FormType", new PdfLiteral("1"));

        // Get existing or create new appearance dictionary.
        var ap = annotation.Elements[PdfAnnotation.Keys.AP] as PdfDictionary;
        if (ap == null)
        {
            ap = new PdfDictionary(_document);
            annotation.Elements[PdfAnnotation.Keys.AP] = ap;
        }

        // Set XRef to normal state
        ap.Elements["/N"] = form.PdfForm.Reference;

        var xobj = form.PdfForm;
        var s = xobj.Stream.ToString();
        // Thank you Adobe: Without putting the content in 'EMC brackets'
        // the text is not rendered by PDF Reader 9 or higher.
        s = "/Tx BMC\n" + s + "\nEMC";
        xobj.Stream.Value = new RawEncoding().GetBytes(s);
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
