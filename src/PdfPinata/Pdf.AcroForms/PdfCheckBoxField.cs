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
            {
                var name = value ? GetNonOffValue() : "/Off";
                Elements.SetName(PdfAcroField.Keys.V, name);
                Elements.SetName(PdfAnnotation.Keys.AS, name);
            }
            else if (Fields.Elements.Items.Length == 1)
            {
                // One widget of its own is the ordinary shape of a tick box whose annotation was
                // not merged into the field, and it is a tick box rather than half of a pair: the
                // state asked for is the state it takes. The names come from the child, because
                // the child is what carries the appearances.
                var child = ChildAt(0);
                var name = value ? OnStateOf(child) : OffStateOf(child);
                if (child != null && name.Length != 0)
                {
                    child.Elements.SetName(PdfAcroField.Keys.V, name);
                    child.Elements.SetName(PdfAnnotation.Keys.AS, name);
                    Elements.SetName(PdfAcroField.Keys.V, name);
                }
            }
            else
            {
                // Here we have to handle fields that exist twice with the same name.
                // Checked must be set for both fields, using /Off for one field and skipping /Off for the other,
                // to have only one field with a check mark.
                // Finding this took me two working days.
                if (Fields.Elements.Items.Length == 2)
                {
                    if (value)
                    {
                        // Element 0: set it to its on state.
                        var name1 = "";
                        var o =
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            var n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (var name in n.Elements.Keys)
                                {
                                    if (name != "/Off")
                                    {
                                        name1 = name;
                                        break;
                                    }
                                }
                            }
                        }

                        if (name1.Length != 0)
                        {
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements.SetName(
                                PdfAcroField.Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }

                        // Element 1: set it to /Off.
                        // Cleared first: name1 still holds the on state found for element 0, and
                        // if element 1 offers no /Off state the search below leaves it untouched -
                        // so without this the second element was set to the first one's on state
                        // and both were ticked.
                        name1 = "";
                        o = ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            var n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (var name in n.Elements.Keys)
                                {
                                    if (name == "/Off")
                                    {
                                        name1 = name;
                                        break;
                                    }
                                }
                            }
                        }

                        if (name1.Length != 0)
                        {
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAcroField.Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }
                    }
                    else
                    {
                        // Element 1: set it to its on state.
                        var name1 = "";
                        var o =
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            var n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (var name in n.Elements.Keys)
                                {
                                    if (name != "/Off")
                                    {
                                        name1 = name;
                                        break;
                                    }
                                }
                            }
                        }

                        if (name1.Length != 0)
                        {
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAcroField.Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }

                        // Element 0: set it to /Off.
                        // Cleared first, for the same reason as the branch above.
                        name1 = "";
                        o = ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            var n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (var name in n.Elements.Keys)
                                {
                                    if (name == "/Off")
                                    {
                                        name1 = name;
                                        break;
                                    }
                                }
                            }
                        }

                        if (name1.Length != 0)
                        {
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements.SetName(
                                PdfAcroField.Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the child field at the given position, or null when there is no such child or it is
    /// not a dictionary.
    /// </summary>
    private PdfDictionary ChildAt(int index)
    {
        var kids = Fields.Elements.Items;
        if (index < 0 || index >= kids.Length)
            return null;

        var kid = kids[index];
        if (kid is PdfReference reference)
            kid = reference.Value;
        return kid as PdfDictionary;
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

    private static string StateOf(PdfDictionary child, bool wanted)
    {
        var appearances = child?.Elements["/AP"] as PdfDictionary;
        var normal = appearances?.Elements["/N"] as PdfDictionary;
        if (normal == null)
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
    public string CheckedName
    {
        get => _checkedName;
        set => _checkedName = value;
    }

    private string _checkedName = "/Yes";

    /// <summary>
    /// Gets or sets the name of the dictionary that represents the Unchecked state.
    /// The default value is "/Off".
    /// </summary>
    public string UncheckedName
    {
        get => _uncheckedName;
        set => _uncheckedName = value;
    }

    private string _uncheckedName = "/Off";

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
