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

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the list box field.
/// </summary>
public sealed class PdfListBoxField : PdfChoiceField
{
    /// <summary>
    /// Initializes a new instance of PdfListBoxField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// A list box is a choice field without the <c>Combo</c> flag, so it needs no flag of its
    /// own: the two kinds of choice are told apart by what a list box does not say.
    /// </remarks>
    public PdfListBoxField(PdfDocument document)
        : base(document, "/Ch")
    { }

    internal PdfListBoxField(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Gets whether the list allows more than one of its options to be chosen at once, which is
    /// the <c>MultiSelect</c> flag. A combo box never does; a list box may.
    /// </summary>
    public bool AllowsMultipleSelection => (Flags & PdfAcroFieldFlags.MultiSelect) != 0;

    /// <summary>
    /// Gets the index of the selected item, answering -1 when nothing is selected, and sets which
    /// one option is selected. Where several are selected this reads the first of them;
    /// <see cref="SelectedIndices"/> gives them all.
    /// </summary>
    /// <remarks>
    /// -1 is an answer, not something to set: it is what the getter says when nothing is chosen,
    /// while the setter takes an index the list has an option for and refuses anything else. To
    /// select nothing, set <see cref="SelectedIndices"/> to an empty array.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The index names no option the list offers.
    /// </exception>
    public int SelectedIndex
    {
        get
        {
            var indices = SelectedIndicesFromValue();
            return indices.Length == 0 ? -1 : indices[0];
        }
        set => SelectedIndices = new[] { value };
    }

    /// <summary>
    /// Gets or sets the indices of every selected option, in ascending order. Empty when nothing
    /// is selected, and setting it to an empty array selects nothing.
    /// </summary>
    /// <remarks>
    /// A list box is the only field that can offer more than one choice at a time, and it could
    /// not be told to take one: it had an index and no more, so a document with several options
    /// selected could not be written, and one read back threw rather than answering, because
    /// <c>/V</c> is an array in that case and was being read as a string.
    /// <para>
    /// Selecting several options needs the <c>MultiSelect</c> flag, without which a viewer is told
    /// at most one may be chosen, and a document saying both would contradict itself.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// More than one index is given for a list that does not allow multiple selection.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An index names no option the list offers.
    /// </exception>
    public int[] SelectedIndices
    {
        get => SelectedIndicesFromValue();
        set
        {
            EnsureCanBeFilled();

            var indices = Ordered(value);

            if (indices.Length > 1 && !AllowsMultipleSelection)
                throw new InvalidOperationException(
                    "The list allows one option to be selected at a time. Set the MultiSelect flag "
                    + "on the field before selecting " + indices.Length + " of them.");

            // A list with no /Opt at all offers nothing, so no index names an option. Checked
            // here because ValueInOptArray answers "" for that case rather than refusing, which
            // would otherwise write an empty /V and an /I pointing into an array that is not
            // there, and the getter would then report nothing selected.
            if (indices.Length != 0 && Elements.GetArray(PdfChoiceField.Keys.Opt) == null)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "The list has no options, so there is no option at any index to select.");

            // Mapped before anything is written, so an index the list has no option for leaves the
            // field as it was rather than half changed.
            var texts = new string[indices.Length];
            for (var idx = 0; idx < indices.Length; idx++)
                texts[idx] = ValueInOptArray(indices[idx]);

            if (indices.Length == 0)
            {
                Elements.Remove(PdfAcroField.Keys.V);
            }
            else if (indices.Length == 1)
            {
                Elements.SetString(PdfAcroField.Keys.V, texts[0]);
            }
            else
            {
                var values = new PdfArray(Owner);
                foreach (var text in texts)
                    values.Elements.Add(new PdfString(text));
                Elements[PdfAcroField.Keys.V] = values;
            }

            // /I is for a list that allows several choices, so a single-choice list carries /V
            // alone and any /I left over from before is taken away rather than left to disagree
            // with it. The exception is the other case the specification requires /I for: two
            // options exporting the same text, where /V names the text and cannot say which of
            // them was meant, so searching /Opt for it finds the first and loses the choice.
            var tellsApartWhatTheValueCannot =
                indices.Length == 1 && IndexInOptArray(texts[0]) != indices[0];

            WriteSelectedIndices(
                AllowsMultipleSelection || tellsApartWhatTheValueCannot ? indices : Array.Empty<int>());
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        // List boxes have no additional entries.

        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
