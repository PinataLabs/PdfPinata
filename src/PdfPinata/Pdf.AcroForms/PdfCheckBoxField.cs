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
    /// Indicates whether the field is checked.
    /// </summary>
    public bool Checked
    {
        get
        {
            if (!HasKids) //R080317
            {
                var value = Elements.GetString(PdfAcroField.Keys.V);
                return value.Length != 0 && value != "/Off";
            }
            else //R080317
            {
                // The answer lives in the first child rather than in the field, whatever the
                // number of children: a field with one widget is as much a tick box as a field
                // with the twin widgets the setter below was written for.
                var child = ChildAt(0);
                if (child == null)
                    return false;

                var value = child.Elements.GetString(PdfAcroField.Keys.V);
                return
                    value.Length != 0 && value != "/Off" &&
                    // Some forms name their off state /Nein, German for "no", rather than /Off.
                    value != "/Nein";
            }
        }
        set
        {
            EnsureCanBeFilled();

            if (!HasKids)
                SetOwnState(value);
            else if (Widgets.Count == 1)
                SetSingleChildState(value);
            else if (Widgets.Count == 2)
                SetTwinChildStates(value);
        }
    }

    /// <summary>
    /// A field that is its own widget takes the state in its own <c>/V</c> and <c>/AS</c>.
    /// </summary>
    private void SetOwnState(bool value)
    {
        var name = value ? GetNonOffValue() : "/Off";
        Elements.SetName(PdfAcroField.Keys.V, name);
        Elements.SetName(PdfAnnotation.Keys.AS, name);
    }

    /// <summary>
    /// One widget of its own is the ordinary shape of a tick box whose annotation was not merged
    /// into the field, and it is a tick box rather than half of a pair: the state asked for is the
    /// state it takes. The names come from the child, because the child is what carries the
    /// appearances.
    /// </summary>
    private void SetSingleChildState(bool value)
    {
        var child = ChildAt(0);
        var name = value ? OnStateOf(child) : OffStateOf(child);
        if (child != null && name.Length != 0)
        {
            child.Elements.SetName(PdfAcroField.Keys.V, name);
            child.Elements.SetName(PdfAnnotation.Keys.AS, name);
            Elements.SetName(PdfAcroField.Keys.V, name);
        }
    }

    /// <summary>
    /// Here we have to handle fields that exist twice with the same name. Checked must be set for
    /// both fields, using /Off for one field and skipping /Off for the other, to have only one field
    /// with a check mark. Finding this took me two working days.
    /// </summary>
    /// <remarks>
    /// Ticked, the first child takes its on state and the second its off state; unticked, the
    /// second takes its on state and the first its off state. Either way the child taking the on
    /// state is written first. Each child's name is looked up on its own, so a child that offers no
    /// such state is left untouched - carrying one name over from the other child is how both
    /// were once ticked.
    /// </remarks>
    private void SetTwinChildStates(bool value)
    {
        SetTwinChildState(ReferencedChildAt(value ? 0 : 1), on: true);
        SetTwinChildState(ReferencedChildAt(value ? 1 : 0), on: false);
    }

    /// <summary>
    /// The widget at the given position, which the twin-widget path takes to exist.
    /// </summary>
    private PdfDictionary ReferencedChildAt(int index) => Widgets[index];

    /// <summary>
    /// Writes the child's on or off state into its <c>/V</c> and <c>/AS</c>, unless its appearances
    /// name no such state.
    /// </summary>
    private static void SetTwinChildState(PdfDictionary child, bool on)
    {
        var name = StateIn(child.Elements, wanted: !on);
        if (name.Length == 0)
            return;

        child.Elements.SetName(PdfAcroField.Keys.V, name);
        child.Elements.SetName(PdfAnnotation.Keys.AS, name);
    }

    /// <summary>
    /// Gets the widget at the given position, or null when there is no such widget.
    /// </summary>
    /// <remarks>
    /// The widgets, not the kids: <c>/Kids</c> can hold nested fields as well, and those carry
    /// states of their own rather than this field's.
    /// </remarks>
    private PdfDictionary ChildAt(int index)
    {
        var widgets = Widgets;
        return index >= 0 && index < widgets.Count ? widgets[index] : null;
    }

    /// <summary>
    /// The name of the first appearance state of the child that is not "/Off", or "" when it
    /// names none - which is how a child with no appearances at all is left as it was.
    /// </summary>
    private static string OnStateOf(PdfDictionary child) => StateOf(child, wanted: false);

    /// <summary>
    /// The name of the child's "/Off" appearance state, or "" when it has not got one.
    /// </summary>
    private static string OffStateOf(PdfDictionary child) => StateOf(child, wanted: true);

    private static string StateOf(PdfDictionary child, bool wanted) =>
        child == null ? "" : StateIn(child.Elements, wanted);

    /// <summary>
    /// The first normal appearance state that is "/Off" when <paramref name="wanted"/> is true, or
    /// that is not when it is false; "" when there is no such state or no appearances at all.
    /// </summary>
    private static string StateIn(PdfDictionary.DictionaryElements elements, bool wanted)
    {
        var appearances = elements["/AP"] as PdfDictionary;
        if (appearances?.Elements["/N"] is not PdfDictionary normal)
            return "";

        foreach (var name in normal.Elements.Keys)
        {
            if (name == "/Off" == wanted)
                return name;
        }
        return "";
    }

    /// <summary>
    /// Gets or sets the name of the dictionary that represents the Checked state.
    /// </summary>
    /// The default value is "/Yes".
    public string CheckedName { get; set; } = "/Yes";

    /// <summary>
    /// Gets or sets the name of the dictionary that represents the Unchecked state.
    /// The default value is "/Off".
    /// </summary>
    public string UncheckedName { get; set; } = "/Off";

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
