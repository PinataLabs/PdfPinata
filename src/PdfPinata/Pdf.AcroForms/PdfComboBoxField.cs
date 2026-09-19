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

using System;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the combo box field.
/// </summary>
public sealed class PdfComboBoxField : PdfChoiceField
{
    /// <summary>
    /// Initializes a new instance of PdfComboBoxField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// The <c>Combo</c> flag is set here rather than left to the caller, because it is what tells
    /// a combo box from a list box: a <c>/Ch</c> without it is a list box, and reading the
    /// document back would make one.
    /// </remarks>
    public PdfComboBoxField(PdfDocument document)
        : base(document, "/Ch")
    {
        Flags = PdfAcroFieldFlags.Combo;
    }

    internal PdfComboBoxField(PdfDictionary dict)
        : base(dict)
    { }

    private protected override PdfAcroFieldFlags KindFlags => PdfAcroFieldFlags.Combo;

    /// <summary>
    /// Gets or sets the index of the selected item.
    /// </summary>
    public int SelectedIndex
    {
        get
        {
            var value = Elements.GetString(PdfAcroField.Keys.V);
            return IndexInOptArray(value);
        }
        set
        {
            Owner?.EnsureCanModify("filling in a form field", PdfChangeKind.FormFieldValues);

            // Minus one means nothing chosen. There is no option at that index to name in /V,
            // so the field keeps what it had rather than being emptied.
            if (value != -1)
            {
                var key = ValueInOptArray(value);
                Elements.SetString(PdfAcroField.Keys.V, key);
                // /I is an array of the indices selected - one of them here, a combo box offering
                // a single choice.
                //
                // The entry is kept rather than dropped, though the specification says it should
                // not be used by a field that does not allow multiple selection, and the list box
                // accordingly does not write one. It is here because a viewer was once found that
                // would not follow /V without it, and that is a recommendation rather than a
                // requirement, whereas the array shape is required.
                WriteSelectedIndices(new[] { value });
            }
        }
    }

    /// <summary>
    /// Gets or sets the value of the field. A value the field does not already offer is added to
    /// its <c>/Opt</c> array, which is what an editable combo box does with text typed into it.
    /// </summary>
    public override PdfItem Value
    {
        get => Elements[PdfAcroField.Keys.V];
        set
        {
            if (ReadOnly)
                throw new InvalidOperationException("The field is read only.");

            Owner?.EnsureCanModify("filling in a form field", PdfChangeKind.FormFieldValues);

            if (!(value is PdfString || value is PdfName))
                throw new NotImplementedException("Values other than string cannot be set.");

            // A choice field's value is a text string, and so is every entry of its /Opt array. A
            // caller passing a name is taken to mean the text the name stands for, as the radio
            // group does when it compares its own /V against /Opt: the slash that makes a name a
            // name is not part of the value. Storing the name itself would write /V and the option
            // as names, which no reader is obliged to make sense of, and IndexInOptArray does not
            // look at names, so /I would never be pointed at the option either.
            var text = value as PdfString ?? new PdfString(TextOfName((PdfName)value));

            Elements[PdfAcroField.Keys.V] = text;
            SyncSelectedIndex();
            if (SelectedIndex != -1)
                return;

            // The value names no option the field offers, so record it as one. /Opt is optional
            // and a field that never had options has no array to append to yet.
            var options = Elements.GetArray(PdfChoiceField.Keys.Opt);
            if (options == null)
            {
                options = new PdfArray(Owner);
                Elements[PdfChoiceField.Keys.Opt] = options;
            }
            options.Elements.Add(text);
            SyncSelectedIndex();
        }
    }

    /// <summary>
    /// Points <c>/I</c> at the option <c>/V</c> names, and rewrites <c>/V</c> with that option's
    /// own text so the two agree. Leaves both alone when <c>/V</c> names no option on offer,
    /// because there is no index to point at.
    /// </summary>
    void SyncSelectedIndex()
    {
        var index = SelectedIndex;
        if (index != -1)
            SelectedIndex = index;
    }

    /// <summary>
    /// The text a name stands for, without the solidus that makes it a name.
    /// </summary>
    static string TextOfName(PdfName name)
    {
        var value = name.Value ?? "";
        return value.Length != 0 && value[0] == '/' ? value[1..] : value;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        // Combo boxes have no additional entries.

        internal static DictionaryMeta Meta
        {
            get
            {
                if (_meta == null)
                    _meta = CreateMeta(typeof(Keys));
                return _meta;
            }
        }
        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
