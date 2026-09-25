#region Copyright
//
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

using PdfPinata.Pdf.Annotations;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the push button field.
/// </summary>
public sealed class PdfPushButtonField : PdfButtonField
{
    /// <summary>
    /// Initializes a new instance of PdfPushButtonField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// The <c>Pushbutton</c> flag is set here for the same reason the radio group sets
    /// <c>Radio</c>: it is what tells the three kinds of <c>/Btn</c> apart, and a push button
    /// without it is read back as a check box.
    /// </remarks>
    public PdfPushButtonField(PdfDocument document)
        : base(document, "/Btn")
    {
        _document = document;
        Flags = PdfAcroFieldFlags.Pushbutton;
    }

    internal PdfPushButtonField(PdfDictionary dict)
        : base(dict)
    { }

    private protected override PdfAcroFieldFlags KindFlags => PdfAcroFieldFlags.Pushbutton;

    /// <summary>
    /// Gets or sets the text on the face of the button - each widget's <c>/MK /CA</c>, its normal
    /// caption. Reading answers what was set here, or else the first widget's caption; "" when
    /// there is none.
    /// </summary>
    /// <remarks>
    /// A button's face is drawn by the caller, through
    /// <see cref="Annotations.PdfAnnotation.SetAppearance(string, Drawing.XForm)"/>; the caption
    /// is what a viewer that builds its own button shows, and it is in <c>/MK</c> alongside
    /// <see cref="PdfAcroField.BackColor"/> and <see cref="PdfAcroField.BorderColor"/>. Setting it
    /// to null or "" removes it.
    /// </remarks>
    public string Caption
    {
        get
        {
            if (_caption != null)
                return _caption;

            foreach (var widget in Widgets)
            {
                if (widget.Elements.GetDictionary(PdfWidgetAnnotation.Keys.MK) is { } characteristics)
                    return characteristics.Elements.GetString("/CA");
            }
            return "";
        }
        set
        {
            _caption = value ?? "";
            foreach (var widget in Widgets)
                SetCaption(widget, _caption);
        }
    }

    private string _caption;

    private protected override void WriteAppearanceCharacteristics(PdfDictionary widget)
    {
        base.WriteAppearanceCharacteristics(widget);
        if (!string.IsNullOrEmpty(_caption))
            SetCaption(widget, _caption);
    }

    private static void SetCaption(PdfDictionary widget, string caption)
    {
        if (caption.Length != 0)
        {
            Characteristics(widget).Elements.SetString("/CA", caption);
            return;
        }

        if (widget.Elements.GetDictionary(PdfWidgetAnnotation.Keys.MK) is not { } characteristics)
            return;

        characteristics.Elements.Remove("/CA");
        if (characteristics.Elements.Count == 0)
            widget.Elements.Remove(PdfWidgetAnnotation.Keys.MK);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
