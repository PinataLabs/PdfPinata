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
using System.Diagnostics;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the base class for all interactive field dictionaries.
/// </summary>
public abstract class PdfAcroField : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of PdfAcroField.
    /// </summary>
    internal PdfAcroField(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Initializes a new instance of <see cref="PdfAcroField"/> of the named type.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <param name="fieldType">
    /// The value of <c>/FT</c> - <c>/Btn</c>, <c>/Tx</c>, <c>/Ch</c> or <c>/Sig</c>.
    /// </param>
    /// <remarks>
    /// The type has to be written by the constructor rather than left to the caller, because it
    /// is what tells a reader - and <c>PdfAcroFieldCollection</c> reading the document back - what
    /// kind of field this is. A field carrying none is a <c>PdfGenericField</c> to everybody.
    /// </remarks>
    private protected PdfAcroField(PdfDocument document, string fieldType)
        : base(document)
    {
        Elements.SetName(Keys.FT, fieldType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAcroField"/> class. Used for type transformation.
    /// </summary>
    protected PdfAcroField(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Gets or sets the partial name of this field - <c>/T</c> - which is what
    /// <see cref="PdfAcroFieldCollection.this[string]"/> looks a field up by, and what the dotted
    /// path of a nested field is assembled from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A root field with no name cannot be found, filled or submitted, so a form authored without
    /// one is a form that silently does nothing. An empty name is not refused, though, because a
    /// field that is one widget of a parent legitimately has none.
    /// </para>
    /// <para>
    /// A period <em>is</em> refused. ISO 32000-1 section 12.7.3.2 says a partial name shall not
    /// contain one, because a period is what joins two partial names into the path a field is
    /// known by - so <c>Name = "name.full"</c> reads like the obvious thing to write and produces
    /// a field that <c>Fields["name.full"]</c> cannot find, having split the name at the period
    /// and gone looking for a field called <c>name</c> with a child called <c>full</c>. Nest the
    /// fields instead, which is what the path means.
    /// </para>
    /// </remarks>
    public string Name
    {
        get
        {
            var name = Elements.GetString(Keys.T);
            return name;
        }
        set
        {
            if (value != null && value.Contains('.'))
            {
                throw new ArgumentException(
                    "A field's partial name cannot contain a period: '" + value + "'. A period "
                    + "joins the partial names of nested fields into the path the innermost one "
                    + "is known by, so a name with one in it cannot be looked up.", nameof(value));
            }

            Elements.SetString(Keys.T, value);
        }
    }

    /// <summary>
    /// Gets or sets the alternate field name - <c>/TU</c> - which a reader shows in place of
    /// <see cref="Name"/> wherever the field has to be identified to a person, and which every
    /// reader in practice shows as the field's tooltip.
    /// </summary>
    public string ToolTip
    {
        get => Elements.GetString(Keys.TU);
        set => Elements.SetString(Keys.TU, value);
    }

    /// <summary>
    /// Gets or sets this field's own default appearance string - <c>/DA</c>, which overrides the
    /// one <see cref="PdfAcroForm.DefaultAppearance"/> sets for the whole form.
    /// </summary>
    /// <remarks>
    /// Worth setting per field even when the form has one, for the reason
    /// <see cref="PdfAcroForm.DefaultAppearance"/> gives: a size of zero means auto-size, and what
    /// a reader makes of that differs from reader to reader.
    /// </remarks>
    public string DefaultAppearance
    {
        get => Elements.GetString(Keys.DA);
        set => Elements.SetString(Keys.DA, value);
    }

    /// <summary>
    /// Gets or sets the field flags of this instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writing them is what a form being authored needs, and what nothing outside this assembly
    /// could do: the flags could be read through this property and set only through
    /// <c>Elements.SetInteger("/Ff", …)</c>, which loses the enumeration.
    /// </para>
    /// <para>
    /// The bits that say what <em>kind</em> of field this is are not the caller's to assign, and
    /// are put back on the way through - see <see cref="KindMask"/>. Assigning them away would
    /// otherwise change the field's type behind the caller's back:
    /// <c>new PdfComboBoxField(document) { Flags = PdfAcroFieldFlags.Required }</c> reads like a
    /// combo box that has to be filled in, and without this would write a <c>/Ch</c> with no
    /// <c>Combo</c> bit - which is a list box, and is what reopening the file would give back.
    /// </para>
    /// <para>
    /// <c>/Ff</c> is inheritable (ISO 32000-1 Table 220), so reading answers the field's own entry
    /// when it has one and otherwise the nearest ancestor's, found up <c>/Parent</c>. A form read
    /// from a file routinely sets the flags once on a parent and leaves its children without, and
    /// reading the field's own entry alone answered zero for every one of them. Writing always
    /// writes this field's own entry, which from then on replaces whatever it inherited.
    /// </para>
    /// </remarks>
    public PdfAcroFieldFlags Flags
    {
        get => InheritedFlags(this);
        set => Elements.SetInteger(Keys.Ff, (int)((value & ~KindMask) | KindFlags));
    }

    /// <summary>
    /// The bits of <c>/Ff</c> that say what kind of field this is rather than how it behaves, and
    /// which <see cref="Flags"/> therefore keeps rather than letting a caller assign over. Zero
    /// for a field whose kind <c>/FT</c> settles on its own, which is every field but a button
    /// and a choice.
    /// </summary>
    private protected virtual PdfAcroFieldFlags KindMask => 0;

    /// <summary>
    /// What those bits are for this kind of field. Zero is an answer rather than an absence: a
    /// check box is the <c>/Btn</c> that says neither <c>Pushbutton</c> nor <c>Radio</c>, and a
    /// list box the <c>/Ch</c> that does not say <c>Combo</c>.
    /// </summary>
    private protected virtual PdfAcroFieldFlags KindFlags => 0;

    internal PdfAcroFieldFlags SetFlags
    {
        // Read the way Flags reads, so that setting one bit on a field that inherits the rest
        // writes the rest too - its own /Ff replaces the inherited one rather than adding to it.
        get => InheritedFlags(this);
        set => Elements.SetInteger(Keys.Ff, (int)value);
    }

    /// <summary>
    /// The field flags a field dictionary has in effect: its own <c>/Ff</c>, or the nearest
    /// ancestor's when it has none.
    /// </summary>
    private static PdfAcroFieldFlags InheritedFlags(PdfDictionary field)
        => (PdfAcroFieldFlags)(InheritedFrom(field, Keys.Ff)?.Elements.GetInteger(Keys.Ff) ?? 0);

    /// <summary>
    /// The nearest of a field dictionary and its ancestors, walking up <c>/Parent</c>, that
    /// carries an entry for an inheritable key; or null when none does.
    /// </summary>
    /// <remarks>
    /// A <c>/Parent</c> chain that comes back on itself is malformed but possible in a file read
    /// from disk, and is treated as ending where it first repeats rather than walked for ever.
    /// </remarks>
    private static PdfDictionary InheritedFrom(PdfDictionary field, string key)
    {
        HashSet<PdfDictionary> visited = null;
        for (var dict = field; dict != null; dict = dict.Elements.GetDictionary(Keys.Parent))
        {
            if (dict.Elements.ContainsKey(key))
                return dict;

            visited ??= [];
            if (!visited.Add(dict))
                return null;
        }
        return null;
    }

    /// <summary>
    /// Puts this field on a page, as the widget annotation a reader draws and a person clicks.
    /// </summary>
    /// <param name="page">The page the field appears on.</param>
    /// <param name="rectangle">
    /// Where on the page, in default user space - the space measured up from the bottom left, not
    /// the top-left world space <c>XGraphics</c> draws in. <c>gfx.Transformer.WorldToDefaultPage</c>
    /// converts.
    /// </param>
    /// <returns>
    /// The widget, so that a caller can give it the appearance streams a check box or a radio
    /// button needs one of per state.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The field has not been added to a form yet, so there is nothing for the widget to point at.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Always a separate annotation under <c>/Kids</c>, never the field dictionary doing double
    /// duty. ISO 32000-1 section 12.7.3.1 allows the two to be merged whenever a field has exactly
    /// one widget, and this deliberately does not: a caller who may add a second widget later would
    /// otherwise have to know that the first one changes shape when they do.
    /// </para>
    /// <para>
    /// The widget is marked as printing. A form field that is not is one that vanishes when the
    /// page is put on paper, which is almost never what an author means and is invisible until
    /// somebody prints.
    /// </para>
    /// </remarks>
    public PdfWidgetAnnotation AddWidget(PdfPage page, PdfRectangle rectangle)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (Reference == null)
        {
            throw new InvalidOperationException(
                "The field does not belong to a form yet. Add it - form.Fields.Add(field) - "
                + "before putting it on a page, or the widget has no parent to point at.");
        }

        var widget = new PdfWidgetAnnotation(Owner);
        page.Annotations.Add(widget);

        widget.Rectangle = rectangle;
        widget.Flags = PdfAnnotationFlags.Print;

        // /P, the page the widget is drawn on. Named through this class's own Keys because
        // PdfAnnotation.Keys leaves the entry out - it is listed there as a comment and nothing
        // more.
        widget.Elements.SetReference(Keys.P, page);
        widget.Elements.SetReference(Keys.Parent, this);

        Fields.Elements.Add(widget.Reference);

        OnWidgetAdded();
        return widget;
    }

    /// <summary>
    /// Called once a widget has been added, and so once the field has somewhere to be drawn.
    /// </summary>
    /// <remarks>
    /// A field that draws its own appearance cannot draw it until there is a rectangle to draw
    /// in, and a caller describes a field before placing it as often as the other way round. This
    /// is what makes the order not matter.
    /// </remarks>
    internal virtual void OnWidgetAdded()
    { }

    /// <summary>
    /// Gets or sets the value of the field.
    /// </summary>
    public virtual PdfItem Value
    {
        get => Elements[Keys.V];
        set
        {
            EnsureCanBeFilled();

            if (value is PdfString || value is PdfName)
                Elements[Keys.V] = value;
            else
                throw new NotImplementedException("Values other than string cannot be set.");
        }
    }

    /// <summary>
    /// Throws unless the field may be given a value: it must not be read only, and the document
    /// must be open for filling in its form.
    /// </summary>
    /// <remarks>
    /// Every public setter that writes <c>/V</c> asks this, and asks nothing else. <see cref="Value"/>
    /// used to be the only one that looked at <see cref="ReadOnly"/>, so a read-only text field
    /// refused a value and took the same value as <c>Text</c>, and likewise a check box's
    /// <c>Checked</c> and each choice and radio field's <c>SelectedIndex</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The field is read only, or the document was not opened in a mode that lets it be changed.
    /// </exception>
    internal void EnsureCanBeFilled()
    {
        if (ReadOnly)
            throw new InvalidOperationException("The field is read only.");

        Owner?.EnsureCanModify("filling in a form field", PdfChangeKind.FormFieldValues);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the field is read only.
    /// </summary>
    public bool ReadOnly
    {
        get => (Flags & PdfAcroFieldFlags.ReadOnly) != 0;
        set
        {
            if (value)
                SetFlags |= PdfAcroFieldFlags.ReadOnly;
            else
                SetFlags &= ~PdfAcroFieldFlags.ReadOnly;
        }
    }

    /// <summary>
    /// Gets the field with the specified name.
    /// </summary>
    public PdfAcroField this[string name] => GetValue(name);

    /// <summary>
    /// Gets a child field by name.
    /// </summary>
    protected virtual PdfAcroField GetValue(string name)
    {
        if (String.IsNullOrEmpty(name))
            return this;
        return HasKids ? Fields.GetValue(name) : null;
    }

    /// <summary>
    /// Indicates whether the field has child fields.
    /// </summary>
    /// <remarks>
    /// The reference is followed. <c>/Kids</c> may be an indirect array as well as a direct one -
    /// and is one for every field this library builds, because <see cref="Fields"/> asks for it
    /// with <c>VCF.CreateIndirect</c> - where this used to answer false for anything that was not
    /// a <c>PdfArray</c> outright. A field whose children it could not see was treated as a
    /// terminal field, so a check box with a widget of its own toggled the field's own appearance
    /// state and left its widget showing whatever it showed before.
    /// </remarks>
    public bool HasKids
    {
        get
        {
            var item = Elements[Keys.Kids];
            if (item is PdfReference reference)
                item = reference.Value;

            return item is PdfArray array && array.Elements.Count > 0;
        }
    }

    /// <summary>
    /// Gets the names of all descendants of this field.
    /// </summary>
    public string[] GetDescendantNames()
    {
        var names = new List<string>();
        if (HasKids)
        {
            var fields = Fields;
            fields.GetDescendantNames(ref names, null);
        }
        var temp = new List<string>();
        foreach (var name in names)
            temp.Add(name);
        return temp.ToArray();
    }

    /// <summary>
    /// Gets the names of all appearance dictionaries of this AcroField.
    /// </summary>
    public string[] GetAppearanceNames()
    {
        var names = new Dictionary<string, object>();
        var dict = Elements["/AP"] as PdfDictionary;
        if (dict != null)
        {
            AppDict(dict, names);

            if (HasKids)
            {
                var kids = Fields.Elements.Items;
                foreach (var pdfItem in kids)
                {
                    if (pdfItem is PdfReference)
                    {
                        var xxx = ((PdfReference)pdfItem).Value as PdfDictionary;
                        if (xxx != null)
                            AppDict(xxx, names);
                    }
                }
            }
        }
        var array = new string[names.Count];
        names.Keys.CopyTo(array, 0);
        return array;
    }

    private static void AppDict(PdfDictionary dict, Dictionary<string, object> names)
    {
        PdfDictionary sub;
        if ((sub = dict.Elements["/D"] as PdfDictionary) != null)
            AppDict2(sub, names);
        if ((sub = dict.Elements["/N"] as PdfDictionary) != null)
            AppDict2(sub, names);
    }

    private static void AppDict2(PdfDictionary dict, Dictionary<string, object> names)
    {
        foreach (var key in dict.Elements.Keys)
        {
            if (!names.ContainsKey(key))
                names.Add(key, null);
        }
    }

    /// <summary>
    /// Gets the text an option array entry holds, as the value of /V would spell it.
    /// </summary>
    /// <remarks>
    /// PdfString.ToString writes the string as it appears in the file, delimiters and all, so
    /// "Sussex" comes back as "(Sussex)". Option arrays are compared against /V, which
    /// PdfDictionary.DictionaryElements.GetString reads without them, so the two only ever agree
    /// on the value itself.
    /// </remarks>
    internal static string TextOfOption(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        return item switch
        {
            PdfString str => str.Value,
            PdfStringObject strObject => strObject.Value,
            PdfName name => name.Value,
            PdfNameObject nameObject => nameObject.Value,
            _ => item?.ToString()
        };
    }

    /// <remarks>
    /// <para>
    /// <c>/Kids</c> holds two different things - the fields nested under this one, and the widget
    /// annotations this field is drawn as - and only the first sort has a name. This used to
    /// assume every kid was a field, so a widget reached here with no <c>/T</c>, tripped a
    /// <c>Debug.Assert</c> and contributed nothing; and a field whose only kids were its widgets
    /// took the "has children" branch and so never reported its own name at all.
    /// </para>
    /// <para>
    /// A field is terminal when nothing underneath it contributed a name, which is the same thing
    /// said without having to ask what each kid is.
    /// </para>
    /// </remarks>
    internal virtual void GetDescendantNames(ref List<string> names, string partialName)
    {
        var t = Elements.GetString(Keys.T);
        if (t.Length == 0)
            return;

        var path = String.IsNullOrEmpty(partialName) ? t : partialName + "." + t;

        var before = names.Count;
        if (HasKids)
            Fields.GetDescendantNames(ref names, path);

        if (names.Count == before)
            names.Add(path);
    }

    /// <summary>
    /// Gets the collection of fields within this field.
    /// </summary>
    public PdfAcroFieldCollection Fields
    {
        get
        {
            if (_fields == null)
            {
                object o = Elements.GetValue(Keys.Kids, VCF.CreateIndirect);
                _fields = (PdfAcroFieldCollection)o;

                // Whose /Kids this is. The same class serves as a form's /Fields, where there is
                // nobody to be under, so the collection cannot work it out for itself - and a
                // field added to /Kids needs the /Parent back-reference that a root field must
                // not have.
                _fields.SetParentField(this);
            }
            return _fields;
        }
    }
    private PdfAcroFieldCollection _fields;

    /// <summary>
    /// Holds a collection of interactive fields.
    /// </summary>
    public sealed class PdfAcroFieldCollection : PdfArray
    {
        // Made by reflection, by the dictionary that holds the array, so neither is public.
        internal PdfAcroFieldCollection(PdfArray array)
            : base(array)
        { }

        internal PdfAcroFieldCollection(PdfDocument document)
            : base(document)
        { }

        /// <summary>
        /// The field this collection is the <c>/Kids</c> of, or null when it is a form's
        /// <c>/Fields</c> and the fields in it are therefore root fields.
        /// </summary>
        private PdfAcroField _parent;

        internal void SetParentField(PdfAcroField parent)
        {
            _parent = parent;
        }

        /// <summary>
        /// Adds a field to this collection, making it an indirect object of the document if it is
        /// not one already.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <exception cref="InvalidOperationException">
        /// The field belongs to another document.
        /// </exception>
        /// <remarks>
        /// This collection is both an interactive form's <c>/Fields</c> and a field's
        /// <c>/Kids</c>, and the same method serves for both - a root field goes in the one, a
        /// field nested under another goes in the other. A <em>widget</em> nested under a field is
        /// not a field and does not come this way;
        /// <see cref="PdfAcroField.AddWidget"/> puts one there.
        /// </remarks>
        public void Add(PdfAcroField field)
        {
            ArgumentNullException.ThrowIfNull(field);

            if (field.Owner != null && field.Owner != Owner)
                throw new InvalidOperationException("The field belongs to another document.");

            // A field's entry in /Fields is required to be an indirect reference, so the field has
            // to be in the reference table before there is anything to write.
            if (field.Reference == null)
                Owner.Internals.AddObject(field);

            Elements.Add(field.Reference);

            // ISO 32000-1 Table 220: /Parent is required of a field that is the child of another
            // and absent otherwise. Every lookup here walks down from /Fields, but the inheritable
            // entries - /FT and /Ff - are read up it, a reader working out what a field is called
            // walks up, and a validator checks that the two directions agree.
            if (_parent != null)
                field.Elements.SetReference(Keys.Parent, _parent);
            else
                field.Elements.Remove(Keys.Parent);
        }

        /// <summary>
        /// Gets the names of all fields in the collection.
        /// </summary>
        public string[] Names
        {
            get
            {
                var count = Elements.Count;
                var names = new string[count];
                for (var idx = 0; idx < count; idx++)
                    names[idx] = ((PdfDictionary)((PdfReference)Elements[idx]).Value).Elements.GetString(Keys.T);
                return names;
            }
        }

        /// <summary>
        /// Gets an array of all descendant names.
        /// </summary>
        public string[] DescendantNames
        {
            get
            {
                var names = new List<string>();
                GetDescendantNames(ref names, null);
                return names.ToArray();
            }
        }

        internal void GetDescendantNames(ref List<string> names, string partialName)
        {
            var count = Elements.Count;
            for (var idx = 0; idx < count; idx++)
            {
                var field = this[idx];
                if (field != null)
                    field.GetDescendantNames(ref names, partialName);
            }
        }

        /// <summary>
        /// Gets a field from the collection. For your convenience an instance of a derived class like
        /// PdfTextField or PdfCheckBox is returned if PDFsharp can guess the actual type of the dictionary.
        /// If the actual type cannot be guessed by PDFsharp the function returns an instance
        /// of PdfGenericField.
        /// </summary>
        public PdfAcroField this[int index]
        {
            get
            {
                var item = Elements[index];
                Debug.Assert(item is PdfReference);
                var dict = ((PdfReference)item).Value as PdfDictionary;
                Debug.Assert(dict != null);
                var field = dict as PdfAcroField;
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (field == null && dict != null)
                {
                    // Do type transformation
                    field = CreateAcroField(dict);
                }
                return field;
            }
        }

        /// <summary>
        /// Gets the field with the specified name.
        /// </summary>
        public PdfAcroField this[string name] => GetValue(name);

        internal PdfAcroField GetValue(string name)
        {
            if (String.IsNullOrEmpty(name))
                return null;

            var dot = name.IndexOf('.');
            var prefix = dot == -1 ? name : name[..dot];
            var suffix = dot == -1 ? "" : name[(dot + 1)..];

            var count = Elements.Count;
            for (var idx = 0; idx < count; idx++)
            {
                var field = this[idx];
                if (field.Name == prefix)
                    return field.GetValue(suffix);
            }
            return null;
        }

        /// <summary>
        /// Create a derived type like PdfTextField or PdfCheckBox if possible.
        /// If the actual cannot be guessed by PDFsharp the function returns an instance
        /// of PdfGenericField.
        /// </summary>
        private static PdfAcroField CreateAcroField(PdfDictionary dict)
        {
            // Both entries are inheritable, and a terminal field read from a file often carries
            // neither itself: a group of radio buttons, say, whose parent says /Btn and Radio once
            // for all of them.
            var ft = InheritedFrom(dict, Keys.FT)?.Elements.GetName(Keys.FT) ?? "";
            var flags = InheritedFlags(dict);
            switch (ft)
            {
                case "/Btn":
                    if ((flags & PdfAcroFieldFlags.Pushbutton) != 0)
                        return new PdfPushButtonField(dict);

                    if ((flags & PdfAcroFieldFlags.Radio) != 0)
                        return new PdfRadioButtonField(dict);

                    return new PdfCheckBoxField(dict);

                case "/Tx":
                    return new PdfTextField(dict);

                case "/Ch":
                    if ((flags & PdfAcroFieldFlags.Combo) != 0)
                        return new PdfComboBoxField(dict);
                    else
                        return new PdfListBoxField(dict);

                case "/Sig":
                    return new PdfSignatureField(dict);

                default:
                    return new PdfGenericField(dict);
            }
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Required for terminal fields; inheritable) The type of field that this dictionary
        /// describes:
        ///   Btn           Button
        ///   Tx            Text
        ///   Ch            Choice
        ///   Sig (PDF 1.3) Signature
        /// Note: This entry may be present in a nonterminal field (one whose descendants
        /// are themselves fields) in order to provide an inheritable FT value. However, a
        /// nonterminal field does not logically have a type of its own; it is merely a container
        /// for inheritable attributes that are intended for descendant terminal fields of
        /// any type.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string FT = "/FT";

        /// <summary>
        /// (Required if this field is the child of another in the field hierarchy; absent otherwise)
        /// The field that is the immediate parent of this one (the field, if any, whose Kids array
        /// includes this field). A field can have at most one parent; that is, it can be included
        /// in the Kids array of at most one other field.
        /// </summary>
        [KeyInfo(KeyType.Dictionary)]
        public const string Parent = "/Parent";

        /// <summary>
        /// (Optional) An array of indirect references to the immediate children of this field.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional, typeof(PdfAcroFieldCollection))]
        public const string Kids = "/Kids";

        /// <summary>
        /// (Optional) The partial field name.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string T = "/T";

        /// <summary>
        /// (Optional; PDF 1.3) An alternate field name, to be used in place of the actual
        /// field name wherever the field must be identified in the user interface (such as
        /// in error or status messages referring to the field). This text is also useful
        /// when extracting the document’s contents in support of accessibility to disabled
        /// users or for other purposes.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string TU = "/TU";

        /// <summary>
        /// (Optional; PDF 1.3) The mapping name to be used when exporting interactive form field
        /// data from the document.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string TM = "/TM";

        /// <summary>
        /// (Optional; inheritable) A set of flags specifying various characteristics of the field.
        /// Default value: 0.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Ff = "/Ff";

        /// <summary>
        /// (Optional; inheritable) The field’s value, whose format varies depending on
        /// the field type; see the descriptions of individual field types for further information.
        /// </summary>
        [KeyInfo(KeyType.Various | KeyType.Optional)]
        public const string V = "/V";

        /// <summary>
        /// (Optional; inheritable) The default value to which the field reverts when a
        /// reset-form action is executed. The format of this value is the same as that of V.
        /// </summary>
        [KeyInfo(KeyType.Various | KeyType.Optional)]
        public const string DV = "/DV";

        /// <summary>
        /// (Optional; PDF 1.2) An additional-actions dictionary defining the field’s behavior
        /// in response to various trigger events. This entry has exactly the same meaning as
        /// the AA entry in an annotation dictionary.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string AA = "/AA";

        // ----- Additional entries to all fields containing variable text --------------------------

        /// <summary>
        /// (Required; inheritable) A resource dictionary containing default resources
        /// (such as fonts, patterns, or color spaces) to be used by the appearance stream.
        /// At a minimum, this dictionary must contain a Font entry specifying the resource
        /// name and font dictionary of the default font for displaying the field’s text.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string DR = "/DR";

        /// <summary>
        /// (Required; inheritable) The default appearance string, containing a sequence of
        /// valid page-content graphics or text state operators defining such properties as
        /// the field’s text size and color.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string DA = "/DA";

        /// <summary>
        /// (Optional; inheritable) A code specifying the form of quadding (justification)
        /// to be used in displaying the text:
        ///   0 Left-justified
        ///   1 Centered
        ///   2 Right-justified
        /// Default value: 0 (left-justified).
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Q = "/Q";

        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be Sig for a signature dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Type = "/Type";

        /// <summary>
        ///
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Subtype = "/Subtype";


        /// <summary>
        ///
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Required)]
        public const string Rect = "/Rect";


        /// <summary>
        /// (Optional; PDF 1.3) An indirect reference to the page this widget is drawn on. Note that
        /// the KeyInfo below describes it as a required rectangle, which is wrong and was copied
        /// from <see cref="Rect"/>; correcting it is a change to the key metadata, not to this doc.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Required)]
        public const string P = "/P";

        // ReSharper restore InconsistentNaming
    }
}
