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
using System.Collections.Generic;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the base class for all choice field dictionaries.
/// </summary>
public abstract class PdfChoiceField : PdfAcroField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfChoiceField"/> class.
    /// </summary>
    protected PdfChoiceField(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfChoiceField"/> class of the named type.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <param name="fieldType">The value of <c>/FT</c>, which for every choice is <c>/Ch</c>.</param>
    private protected PdfChoiceField(PdfDocument document, string fieldType)
        : base(document, fieldType)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfChoiceField"/> class. Used for type
    /// transformation.
    /// </summary>
    protected PdfChoiceField(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// A <c>/Ch</c> is a combo box or a list box according to this one bit, so it belongs to the
    /// class rather than to the caller.
    /// </summary>
    private protected override PdfAcroFieldFlags KindMask => PdfAcroFieldFlags.Combo;

    /// <summary>
    /// The options the field offers, in the order a reader lists them - the <c>/Opt</c> array,
    /// which is what a choice field is for and what it has nothing to choose between without.
    /// </summary>
    /// <remarks>
    /// An option may be written either as one text string or as a pair of them, an export value
    /// and the text shown for it. Reading answers the export value, because that is what
    /// <c>/V</c> is matched against; writing writes the plain form, one string per option.
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
                text[idx] = ValueInOptArray(idx);

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
    /// Gets the index of the specified string in the /Opt array or -1, if no such string exists.
    /// </summary>
    protected int IndexInOptArray(string value)
    {
        return IndexInOptArray(value, null);
    }

    /// <summary>
    /// The index in <c>/Opt</c> of the first option exporting <paramref name="value"/> that is not
    /// already among <paramref name="taken"/>, or -1 when the array holds no such option. A null
    /// <paramref name="taken"/> takes nothing to be spoken for and so finds the first match.
    /// </summary>
    /// <remarks>
    /// Two options may export the same text, and a search that always starts at the beginning finds
    /// the first of them every time - so a <c>/V</c> naming that text twice would be read as one
    /// option chosen rather than two. Passing over what earlier entries already accounted for gives
    /// the second occurrence the second option.
    /// </remarks>
    int IndexInOptArray(string value, List<int> taken)
    {
        PdfArray opt = Elements.GetArray(Keys.Opt);

        if (opt != null)
        {
            int count = opt.Elements.Count;
            for (int idx = 0; idx < count; idx++)
            {
                if (taken != null && taken.Contains(idx))
                    continue;

                PdfItem item = opt.Elements[idx];
                if (item is PdfString)
                {
                    if (TextOfOption(item) == value)
                        return idx;
                }
                else if (item is PdfArray)
                {
                    // An option may be an [exportValue displayText] pair, and it is the export
                    // value that /V is meant to match.
                    PdfArray array = (PdfArray)item;
                    if (array.Elements.Count != 0)
                    {
                        if (TextOfOption(array.Elements[0]) == value)
                            return idx;
                    }
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets the value from the index in the /Opt array.
    /// </summary>
    protected string ValueInOptArray(int index)
    {
        PdfArray opt = Elements.GetArray(Keys.Opt);
        if (opt != null)
        {
            int count = opt.Elements.Count;
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException(nameof(index));

            PdfItem item = opt.Elements[index];
            if (item is PdfString)
                return TextOfOption(item);

            if (item is PdfArray)
            {
                PdfArray array = (PdfArray)item;
                if (array.Elements.Count != 0)
                    return TextOfOption(array.Elements[0]);
            }
        }
        return "";
    }

    /// <summary>
    /// The indices in <c>/Opt</c> of the options currently chosen, in ascending order. Empty when
    /// nothing is chosen, or when what is chosen is no longer among the options on offer.
    /// </summary>
    /// <remarks>
    /// Read from <c>/V</c> rather than from <c>/I</c>. The two are allowed to disagree, and the
    /// specification says <c>/V</c> is the one that wins; <c>/I</c> is there for a reader that
    /// would otherwise have to search <c>/Opt</c>, and to tell apart two options that display
    /// differently but export the same text. <c>/V</c> is a text string when one option is chosen
    /// and an array of them when several are, which is why this reads both.
    /// </remarks>
    protected int[] SelectedIndicesFromValue()
    {
        PdfItem value = Elements[Keys.V];
        if (value is Advanced.PdfReference reference)
            value = reference.Value;

        if (value == null || value is PdfNull)
            return Array.Empty<int>();

        // The export values /V names, in the order it names them.
        var chosenTexts = new List<string>();
        if (value is PdfArray chosen)
            foreach (PdfItem item in chosen.Elements)
                chosenTexts.Add(TextOfOption(item));
        else
            chosenTexts.Add(TextOfOption(value));

        int[] disambiguated = SelectedIndicesFromIndexEntry(chosenTexts);
        if (disambiguated != null)
            return disambiguated;

        // Each named export value takes the first option offering it that an earlier one has not
        // already taken, so that two options exporting the same text account for two entries in /V
        // rather than both collapsing onto the first. /I says which two where it is there and
        // usable; this is what is left when it is not, and the specification requires it to be
        // there in exactly this case.
        var indices = new List<int>();
        foreach (string text in chosenTexts)
        {
            int index = IndexInOptArray(text, indices);
            if (index != -1)
                indices.Add(index);
        }

        indices.Sort();
        return indices.ToArray();
    }

    /// <summary>
    /// The options <c>/I</c> names, when what it names is the same set of export values that
    /// <c>/V</c> does; null when there is no usable <c>/I</c>, or when it names something else.
    /// </summary>
    /// <remarks>
    /// Two options may display differently and export the same text, and the specification says
    /// <c>/I</c> shall be used to tell them apart - searching <c>/Opt</c> for the text in <c>/V</c>
    /// finds the first of them and cannot do better. So <c>/I</c> is believed where it agrees with
    /// <c>/V</c> about which values are chosen, and disbelieved where it does not, <c>/V</c> being
    /// the entry the specification gives precedence to.
    /// </remarks>
    int[] SelectedIndicesFromIndexEntry(List<string> chosenTexts)
    {
        PdfArray entry = Elements.GetArray(Keys.I);
        PdfArray opt = Elements.GetArray(Keys.Opt);
        if (entry == null || opt == null || entry.Elements.Count != chosenTexts.Count)
            return null;

        var indices = new List<int>();
        var unaccountedFor = new List<string>(chosenTexts);
        foreach (PdfItem item in entry.Elements)
        {
            PdfItem resolved = item is Advanced.PdfReference reference ? reference.Value : item;
            if (!(resolved is PdfInteger number))
                return null;

            int index = number.Value;
            if (index < 0 || index >= opt.Elements.Count || indices.Contains(index))
                return null;

            // Removing rather than searching, so that two options exporting the same text account
            // for two entries in /V rather than both matching the one.
            if (!unaccountedFor.Remove(ValueInOptArray(index)))
                return null;

            indices.Add(index);
        }

        indices.Sort();
        return indices.ToArray();
    }

    /// <summary>
    /// Writes <c>/I</c> as the array of chosen indices the specification calls for, sorted
    /// ascending, and removes the entry when <paramref name="indices"/> is empty rather than
    /// leaving one behind that says something the field no longer does.
    /// </summary>
    protected void WriteSelectedIndices(int[] indices)
    {
        if (indices == null || indices.Length == 0)
        {
            Elements.Remove(Keys.I);
            return;
        }

        // Sorted here rather than trusted from the caller, so that the entry is in the order the
        // specification gives for it however this is reached.
        PdfArray entry = new PdfArray(Owner);
        foreach (int index in Ordered(indices))
            entry.Elements.Add(new PdfInteger(index));
        Elements[Keys.I] = entry;
    }

    /// <summary>
    /// Sorts and removes repeats, so that a caller need not, and so that <c>/I</c> and <c>/V</c>
    /// are written in the one order the specification gives for <c>/I</c>.
    /// </summary>
    internal static int[] Ordered(int[] indices)
    {
        var ordered = new List<int>();
        foreach (int index in indices ?? Array.Empty<int>())
            if (!ordered.Contains(index))
                ordered.Add(index);
        ordered.Sort();
        return ordered.ToArray();
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Required; inheritable) An array of options to be presented to the user. Each element of
        /// the array is either a text string representing one of the available options or a two-element
        /// array consisting of a text string together with a default appearance string for constructing
        /// the item’s appearance dynamically at viewing time.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Opt = "/Opt";

        /// <summary>
        /// (Optional; inheritable) For scrollable list boxes, the top index (the index in the Opt array
        /// of the first option visible in the list).
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string TI = "/TI";

        /// <summary>
        /// (Sometimes required, otherwise optional; inheritable; PDF 1.4) For choice fields that allow
        /// multiple selection (MultiSelect flag set), an array of integers, sorted in ascending order,
        /// representing the zero-based indices in the Opt array of the currently selected option
        /// items. This entry is required when two or more elements in the Opt array have different
        /// names but the same export value, or when the value of the choice field is an array; in
        /// other cases, it is permitted but not required. If the items identified by this entry differ
        /// from those in the V entry of the field dictionary (see below), the V entry takes precedence.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string I = "/I";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;

        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
