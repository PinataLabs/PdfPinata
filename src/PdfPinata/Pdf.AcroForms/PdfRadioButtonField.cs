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
/// Represents the radio button field.
/// </summary>
public sealed class PdfRadioButtonField : PdfButtonField
{
    /// <summary>
    /// Initializes a new instance of PdfRadioButtonField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// The <c>Radio</c> flag is set here rather than left to the caller, because it is not a
    /// property of the field so much as the answer to "what kind of button is this": a
    /// <c>/Btn</c> without it is a check box, and reading the document back would make one.
    /// </remarks>
    public PdfRadioButtonField(PdfDocument document)
        : base(document, "/Btn")
    {
        _document = document;
        Flags = PdfAcroFieldFlags.Radio;
    }

    internal PdfRadioButtonField(PdfDictionary dict)
        : base(dict)
    { }

    private protected override PdfAcroFieldFlags KindFlags => PdfAcroFieldFlags.Radio;

    /// <summary>
    /// The export value of each button in the group, in the order of the widgets under
    /// <c>/Kids</c> - the <c>/Opt</c> array, which is what <see cref="SelectedIndex"/> turns an
    /// index into and what the field's value is one of.
    /// </summary>
    /// <remarks>
    /// Each entry has to match the name of the corresponding widget's "on" appearance state, or
    /// the value the field holds names a state no widget can show.
    /// </remarks>
    public string[] Options
    {
        get
        {
            PdfArray options = Elements.GetArray(Keys.Opt);
            if (options == null)
                return Array.Empty<string>();

            int count = options.Elements.Count;
            string[] text = new string[count];
            for (int idx = 0; idx < count; idx++)
                text[idx] = TextOfOption(options.Elements[idx]);

            return text;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            PdfArray options = new PdfArray(Owner);
            foreach (string option in value)
                options.Elements.Add(new PdfString(option ?? ""));

            Elements[Keys.Opt] = options;
        }
    }

    /// <summary>
    /// Gets or sets the index of the selected radio button in a radio button group.
    /// </summary>
    public int SelectedIndex
    {
        get
        {
            string value = Elements.GetString(Keys.V);
            // /V is a name, while /Opt holds the export values as text strings. The slash that
            // makes the name a name is not part of the value it stands for.
            if (value.Length != 0 && value[0] == '/')
                value = value.Substring(1);
            return IndexInOptStrings(value);
        }
        set
        {
            Owner?.EnsureCanModify("filling in a form field", PdfChangeKind.FormFieldValues);

            PdfArray opt = Elements[Keys.Opt] as PdfArray;

            if (opt == null)
                opt = Elements[Keys.Kids] as PdfArray;

            if (opt != null)
            {
                int count = opt.Elements.Count;
                if (value < 0 || value >= count)
                    throw new ArgumentOutOfRangeException(nameof(value));
                Elements.SetName(Keys.V, TextOfOption(opt.Elements[value]));
            }
        }
    }

    int IndexInOptStrings(string value)
    {
        PdfArray opt = Elements[Keys.Opt] as PdfArray;
        if (opt != null)
        {
            int count = opt.Elements.Count;
            for (int idx = 0; idx < count; idx++)
            {
                PdfItem item = opt.Elements[idx];
                if (item is PdfString)
                {
                    if (TextOfOption(item) == value)
                        return idx;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Predefined keys of this dictionary. 
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfButtonField.Keys
    {
        /// <summary>
        /// (Optional; inheritable; PDF 1.4) An array of text strings to be used in
        /// place of the V entries for the values of the widget annotations representing
        /// the individual radio buttons. Each element in the array represents
        /// the export value of the corresponding widget annotation in the
        /// Kids array of the radio button field.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Opt = "/Opt";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
