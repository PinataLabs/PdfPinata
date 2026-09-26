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

using System.Collections.Generic;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the check box field.
/// </summary>
public sealed class PdfCheckBoxField : PdfButtonField
{
    /// <summary>
    /// Initializes a new instance of PdfCheckBoxField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// A check box is a button field with neither the <c>Radio</c> nor the <c>Pushbutton</c> flag
    /// set, so it needs no flag of its own: the three kinds of button are told apart by what a
    /// check box does not say.
    /// </remarks>
    public PdfCheckBoxField(PdfDocument document)
        : base(document, "/Btn")
    {
        _document = document;
    }

    internal PdfCheckBoxField(PdfDictionary dict)
        : base(dict)
    {
    }

    /// <summary>
    /// Gets or sets whether the box is ticked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The state is the field's value, <c>/V</c>: the name of an on state, or <c>/Off</c> (ISO
    /// 32000-1 section 12.7.4.2.3). Each widget only shows it - its <c>/AS</c> is the value when
    /// its appearances offer that state and <c>/Off</c> when they do not - so a box drawn in two
    /// places is ticked in both, and a pair whose widgets name different on states is ticked in
    /// the first alone, which is what a reader does with the same file.
    /// </para>
    /// <para>
    /// <c>/V</c> is inheritable, and read from the nearest ancestor that has one. A file that says
    /// no value anywhere - some software writes only the appearance states - is read by its
    /// widgets: ticked when one of them shows an on state.
    /// </para>
    /// <para>
    /// This used to read the first widget's own <c>/V</c> whenever the field had widgets under
    /// <c>/Kids</c>, and a field with exactly two was ticked by turning the first on and the
    /// second off and unticked the other way round, with no value written on the field at all.
    /// A reader goes by the field's value, so what one showed and what this read disagreed.
    /// </para>
    /// </remarks>
    public bool Checked
    {
        get
        {
            var owner = InheritedFrom(this, PdfAcroField.Keys.V);
            if (owner != null)
                return IsOnState(owner.Elements.GetName(PdfAcroField.Keys.V));

            foreach (var widget in Widgets)
            {
                if (IsOnState(widget.Elements.GetName(PdfAnnotation.Keys.AS)))
                    return true;
            }
            return false;
        }
        set
        {
            EnsureCanBeFilled();

            var state = value ? CheckedName : Off;
            Elements.SetName(PdfAcroField.Keys.V, state);

            foreach (var widget in Widgets)
            {
                // A widget that is a view of this field shares its entries, and /V is the field's.
                // Any other widget has no business carrying one: nothing reads it but the scheme
                // this replaces, which put the state there.
                if (!ReferenceEquals(widget.Elements, Elements))
                    widget.Elements.Remove(PdfAcroField.Keys.V);

                var states = StatesOf(widget);
                if (states == null)
                    continue;

                widget.Elements.SetName(PdfAnnotation.Keys.AS, states.Contains(state) ? state : Off);
            }
        }
    }

    /// <summary>
    /// Gets the name of the on state: the first state other than <c>/Off</c> that the widgets'
    /// normal appearances name, or <c>/Yes</c> - the name ISO 32000-1 uses throughout - when none
    /// of them names one.
    /// </summary>
    /// <remarks>
    /// Read from the appearances rather than kept, because the appearances are what a reader
    /// shows: a value naming a state they do not have shows every widget off. It used to be a
    /// property that could be set and that nothing read, beside an <c>UncheckedName</c> that
    /// nothing read either; the off state is always <c>/Off</c>.
    /// </remarks>
    public string CheckedName
    {
        get
        {
            foreach (var widget in Widgets)
            {
                var states = StatesOf(widget);
                if (states == null)
                    continue;

                foreach (var state in states)
                {
                    if (IsOnState(state))
                        return state;
                }
            }
            return "/Yes";
        }
    }

    private const string Off = "/Off";

    /// <summary>
    /// Whether a state name is an on state. Some forms name their off state <c>/Nein</c>, German
    /// for "no", rather than <c>/Off</c>, and that is read as off too.
    /// </summary>
    private static bool IsOnState(string name) => name.Length != 0 && name != Off && name != "/Nein";

    /// <summary>
    /// The names of a widget's normal appearance states, or null when it has no normal
    /// appearances to choose between.
    /// </summary>
    private static ICollection<string> StatesOf(PdfDictionary widget)
    {
        var appearances = PdfReference.Dereference(widget.Elements[PdfAnnotation.Keys.AP]);

        var normal = PdfReference.Dereference((appearances as PdfDictionary)?.Elements["/N"]);

        return normal is PdfDictionary states ? states.Elements.Keys : null;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfButtonField.Keys
    {
        /// <summary>
        /// (Optional; inheritable; PDF 1.4) A text string to be used in place of the V entry for the
        /// value of the field.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Opt = "/Opt";

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
