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
using PdfPinata.Pdf.Signatures;

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
                string value = Elements.GetString(Keys.V);
                return value.Length != 0 && value != "/Off";
            }
            else //R080317
            {
                // The answer lives in the first child rather than in the field, whatever the
                // number of children: a field with one widget is as much a tick box as a field
                // with the twin widgets the setter below was written for.
                PdfDictionary child = ChildAt(0);
                if (child == null)
                    return false;

                string value = child.Elements.GetString(Keys.V);
                return
                    value.Length != 0 && value != "/Off" &&
                    value != "/Nein"; //R081114 (3Std.!!) auch auf Nein prüfen; //TODO woher kommt der Wert?
            }
        }
        set
        {
            Owner?.EnsureCanModify("filling in a form field", PdfChangeKind.FormFieldValues);

            if (!HasKids)
            {
                string name = value ? GetNonOffValue() : "/Off";
                Elements.SetName(Keys.V, name);
                Elements.SetName(PdfAnnotation.Keys.AS, name);
            }
            else if (Fields.Elements.Items.Length == 1)
            {
                // One widget of its own is the ordinary shape of a tick box whose annotation was
                // not merged into the field, and it is a tick box rather than half of a pair: the
                // state asked for is the state it takes. The names come from the child, because
                // the child is what carries the appearances.
                PdfDictionary child = ChildAt(0);
                string name = value ? OnStateOf(child) : OffStateOf(child);
                if (child != null && name.Length != 0)
                {
                    child.Elements.SetName(Keys.V, name);
                    child.Elements.SetName(PdfAnnotation.Keys.AS, name);
                    Elements.SetName(Keys.V, name);
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
                        //Element 0 behandeln -> auf checked setzen
                        string name1 = "";
                        PdfDictionary o =
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            PdfDictionary n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (string name in n.Elements.Keys)
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
                                Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }

                        //Element 1 behandeln -> auf unchecked setzen
                        // Cleared first: name1 still holds the on state found for element 0, and
                        // if element 1 offers no /Off state the search below leaves it untouched -
                        // so without this the second element was set to the first one's on state
                        // and both were ticked.
                        name1 = "";
                        o = ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            PdfDictionary n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (string name in n.Elements.Keys)
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
                                Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }
                    }
                    else
                    {
                        //Element 0 behandeln -> auf unchecked setzen
                        string name1 = "";
                        PdfDictionary o =
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            PdfDictionary n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (string name in n.Elements.Keys)
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
                                Keys.V, name1);
                            ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[1])).Value)).Elements.SetName(
                                PdfAnnotation.Keys.AS, name1);
                        }

                        //Element 1 behandeln -> auf checked setzen
                        // Cleared first, for the same reason as the branch above.
                        name1 = "";
                        o = ((PdfDictionary)(((PdfReference)(Fields.Elements.Items[0])).Value)).Elements["/AP"] as
                            PdfDictionary;
                        if (o != null)
                        {
                            PdfDictionary n = o.Elements["/N"] as PdfDictionary;
                            if (n != null)
                            {
                                foreach (string name in n.Elements.Keys)
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
                                Keys.V, name1);
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
    PdfDictionary ChildAt(int index)
    {
        PdfItem[] kids = Fields.Elements.Items;
        if (index < 0 || index >= kids.Length)
            return null;

        PdfItem kid = kids[index];
        if (kid is PdfReference reference)
            kid = reference.Value;
        return kid as PdfDictionary;
    }

    /// <summary>
    /// The name of the first appearance state of the child that is not "/Off", or "" when it
    /// names none - which is how a child with no appearances at all is left as it was.
    /// </summary>
    static string OnStateOf(PdfDictionary child) => StateOf(child, wanted: false);

    /// <summary>
    /// The name of the child's "/Off" appearance state, or "" when it has not got one.
    /// </summary>
    static string OffStateOf(PdfDictionary child) => StateOf(child, wanted: true);

    static string StateOf(PdfDictionary child, bool wanted)
    {
        PdfDictionary appearances = child?.Elements["/AP"] as PdfDictionary;
        PdfDictionary normal = appearances?.Elements["/N"] as PdfDictionary;
        if (normal == null)
            return "";

        foreach (string name in normal.Elements.Keys)
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

    string _checkedName = "/Yes";

    /// <summary>
    /// Gets or sets the name of the dictionary that represents the Unchecked state.
    /// The default value is "/Off".
    /// </summary>
    public string UncheckedName
    {
        get => _uncheckedName;
        set => _uncheckedName = value;
    }

    string _uncheckedName = "/Off";

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
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
