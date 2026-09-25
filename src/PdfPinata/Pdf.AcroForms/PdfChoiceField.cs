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
using System.Globalization;
using System.Text.RegularExpressions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Annotations;

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
            var options = Elements.GetArray(Keys.Opt);
            if (options == null)
                return [];

            var count = options.Elements.Count;
            var text = new string[count];
            for (var idx = 0; idx < count; idx++)
                text[idx] = ValueInOptArray(idx);

            return text;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            var options = new PdfArray(Owner);
            foreach (var option in value)
                options.Elements.Add(new PdfString(option ?? ""));

            Elements[Keys.Opt] = options;
            RenderAppearance();
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
    private int IndexInOptArray(string value, List<int> taken)
    {
        var opt = Elements.GetArray(Keys.Opt);
        if (opt == null)
            return -1;

        var count = opt.Elements.Count;
        for (var idx = 0; idx < count; idx++)
        {
            if (taken != null && taken.Contains(idx))
                continue;

            if (ExportsValue(opt.Elements[idx], value))
                return idx;
        }
        return -1;
    }

    /// <summary>
    /// Whether an entry of <c>/Opt</c> exports <paramref name="value"/>.
    /// </summary>
    private static bool ExportsValue(PdfItem item, string value)
    {
        return item switch
        {
            PdfString => TextOfOption(item) == value,
            // An option may be an [exportValue displayText] pair, and it is the export
            // value that /V is meant to match.
            PdfArray array => array.Elements.Count != 0 && TextOfOption(array.Elements[0]) == value,
            _ => false
        };
    }

    /// <summary>
    /// Gets the value from the index in the /Opt array.
    /// </summary>
    protected string ValueInOptArray(int index)
    {
        var opt = Elements.GetArray(Keys.Opt);
        if (opt == null)
            return "";

        var count = opt.Elements.Count;
        if (index < 0 || index >= count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var item = opt.Elements[index];
        return item switch
        {
            PdfString => TextOfOption(item),
            PdfArray { Elements.Count: not 0 } array => TextOfOption(array.Elements[0]),
            _ => ""
        };
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
        var value = Elements[PdfAcroField.Keys.V];
        if (value is Advanced.PdfReference reference)
            value = reference.Value;

        if (value is null or PdfNull)
            return [];

        var chosenTexts = ChosenTexts(value);

        return SelectedIndicesFromIndexEntry(chosenTexts) ?? SelectedIndicesFromTexts(chosenTexts);
    }

    /// <summary>
    /// The export values <c>/V</c> names, in the order it names them.
    /// </summary>
    private static List<string> ChosenTexts(PdfItem value)
    {
        var chosenTexts = new List<string>();
        if (value is PdfArray chosen)
            foreach (var item in chosen.Elements)
                chosenTexts.Add(TextOfOption(item));
        else
            chosenTexts.Add(TextOfOption(value));
        return chosenTexts;
    }

    /// <summary>
    /// The options exporting the values <c>/V</c> names, found by searching <c>/Opt</c>.
    /// </summary>
    private int[] SelectedIndicesFromTexts(List<string> chosenTexts)
    {
        // Each named export value takes the first option offering it that an earlier one has not
        // already taken, so that two options exporting the same text account for two entries in /V
        // rather than both collapsing onto the first. /I says which two where it is there and
        // usable; this is what is left when it is not, and the specification requires it to be
        // there in exactly this case.
        var indices = new List<int>();
        foreach (var text in chosenTexts)
        {
            var index = IndexInOptArray(text, indices);
            if (index != -1)
                indices.Add(index);
        }

        indices.Sort();
        return [..indices];
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
    private int[] SelectedIndicesFromIndexEntry(List<string> chosenTexts)
    {
        var entry = Elements.GetArray(Keys.I);
        var opt = Elements.GetArray(Keys.Opt);
        if (entry == null || opt == null || entry.Elements.Count != chosenTexts.Count)
            return null;

        var indices = new List<int>();
        var unaccountedFor = new List<string>(chosenTexts);
        foreach (var item in entry.Elements)
        {
            var index = IndexEntry(item, opt.Elements.Count, indices);
            if (index < 0)
                return null;

            // Removing rather than searching, so that two options exporting the same text account
            // for two entries in /V rather than both matching the one.
            if (!unaccountedFor.Remove(ValueInOptArray(index)))
                return null;

            indices.Add(index);
        }

        indices.Sort();
        return [..indices];
    }

    /// <summary>
    /// The option an entry of <c>/I</c> names, or -1 when it is not an integer, names no option or
    /// names one an earlier entry already did.
    /// </summary>
    private static int IndexEntry(PdfItem item, int optionCount, List<int> indices)
    {
        var resolved = item is Advanced.PdfReference reference ? reference.Value : item;
        if (resolved is not PdfInteger number)
            return -1;

        var index = number.Value;
        return index < 0 || index >= optionCount || indices.Contains(index) ? -1 : index;
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
        var entry = new PdfArray(Owner);
        foreach (var index in Ordered(indices))
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
        foreach (var index in indices ?? [])
            if (!ordered.Contains(index))
                ordered.Add(index);
        ordered.Sort();
        return [..ordered];
    }

    // ----- Appearance ------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the font the options are drawn in. Unset, it is the resolver's default font at
    /// the size the field's <c>/DA</c> names, or 10 points when that names none or asks for
    /// auto-sizing.
    /// </summary>
    /// <remarks>
    /// The size is taken from <c>/DA</c> so that the drawing agrees with the one a reader builds
    /// from the same string. Setting this, or any of the colours below, redraws the field.
    /// </remarks>
    public XFont Font
    {
        get => _font ?? DefaultFont();
        set
        {
            _font = value;
            RenderAppearance();
        }
    }

    private XFont _font;

    /// <summary>
    /// Gets or sets the colour of the text. Unset, it is the colour the field's <c>/DA</c> names,
    /// or black.
    /// </summary>
    public XColor ForeColor
    {
        get => _foreColor.IsEmpty ? ColorFromDefaultAppearance() : _foreColor;
        set
        {
            _foreColor = value;
            RenderAppearance();
        }
    }

    private XColor _foreColor = XColor.Empty;

    /// <summary>
    /// Gets or sets the colour the box is filled with. <see cref="XColor.Empty"/>, the default,
    /// takes the background each widget's <c>/MK</c> names, and fills nothing where it names none.
    /// </summary>
    /// <remarks>
    /// <c>/MK</c> is what a reader builds an appearance from, and a widget that has an appearance
    /// is drawn from that instead - so a field decorated through <c>/MK</c> alone would lose its
    /// box the moment it drew itself, as a text field does, were <c>/MK</c> not read here too.
    /// </remarks>
    public XColor BackColor
    {
        get => _backColor;
        set
        {
            _backColor = value;
            RenderAppearance();
        }
    }

    private XColor _backColor = XColor.Empty;

    /// <summary>
    /// Gets or sets the colour of the one-point border drawn inside the box.
    /// <see cref="XColor.Empty"/>, the default, takes the border colour each widget's <c>/MK</c>
    /// names, and draws none where it names none.
    /// </summary>
    public XColor BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            RenderAppearance();
        }
    }

    private XColor _borderColor = XColor.Empty;

    /// <summary>
    /// The text shown for the option at <paramref name="index"/>: the second element of an
    /// <c>[export display]</c> pair, or the option itself when it is a single string. "" when the
    /// field has no such option.
    /// </summary>
    private protected string DisplayTextAt(int index)
    {
        var options = Elements.GetArray(Keys.Opt);
        if (options == null || index < 0 || index >= options.Elements.Count)
            return "";

        var item = options.Elements[index];
        if (item is PdfReference reference)
            item = reference.Value;

        return item switch
        {
            PdfArray { Elements.Count: >= 2 } pair => TextOfOption(pair.Elements[1]),
            PdfArray { Elements.Count: 1 } single => TextOfOption(single.Elements[0]),
            _ => TextOfOption(item)
        };
    }

    /// <summary>
    /// Whether the field has anything of its own to draw - a value to show, or options to list.
    /// </summary>
    private protected virtual bool HasContent => false;

    /// <summary>
    /// Draws the field's own content - its value, or its list - inside the border.
    /// </summary>
    /// <param name="gfx">The graphics of the widget's appearance, already clipped to the inside.</param>
    /// <param name="inside">The box inside the border, in the appearance's own space.</param>
    private protected virtual void DrawContent(XGraphics gfx, XRect inside)
    { }

    /// <summary>
    /// Draws the normal appearance of each of the field's widgets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A choice field wrote its value and left the drawing to the reader, through
    /// <c>/NeedAppearances</c>. That flag is a request, and Ghostscript, print pipelines and most
    /// previewers ignore it, so the value was set and invisible; PDF 2.0 deprecates it, and PDF/A
    /// forbids it. Issue #151.
    /// </para>
    /// <para>
    /// Called whenever something drawn from changes, and not on save: a field read from a file
    /// keeps the appearance it came with until the caller changes the field.
    /// </para>
    /// </remarks>
    private protected void RenderAppearance()
    {
        if (Owner == null)
            return;

        foreach (var widget in Widgets)
            RenderAppearanceOn(widget);
    }

    internal override void OnWidgetAdded() => RenderAppearance();

    internal override void OnDefaultAppearanceChanged() => RenderAppearance();

    private void RenderAppearanceOn(PdfDictionary widget)
    {
        var rect = widget.Elements.GetRectangle(PdfAnnotation.Keys.Rect);

        // XForm refuses a box under a point in either direction, and a widget can be that small
        // while it is being assembled.
        if (rect.Width < 1 || rect.Height < 1)
            return;

        var characteristics = widget.Elements.GetDictionary(PdfWidgetAnnotation.Keys.MK);
        var back = _backColor.IsEmpty ? ColorIn(characteristics, "/BG") : _backColor;
        var border = _borderColor.IsEmpty ? ColorIn(characteristics, "/BC") : _borderColor;

        // Nothing to draw. Writing an empty appearance would blank the field rather than leave a
        // reader to build it, so the one it had is taken away instead.
        if (back.IsEmpty && border.IsEmpty && !HasContent)
        {
            widget.Elements.Remove(PdfAnnotation.Keys.AP);
            return;
        }

        var form = new XForm(Owner, rect.Size);
        var gfx = XGraphics.FromForm(form);
        var box = new XRect(0, 0, rect.Width, rect.Height);

        if (!back.IsEmpty)
            gfx.DrawRectangle(new XSolidBrush(back), box);

        if (!border.IsEmpty)
            gfx.DrawRectangle(new XPen(border, 1), new XRect(0.5, 0.5, box.Width - 1, box.Height - 1));

        var inside = new XRect(1, 1, Math.Max(box.Width - 2, 0), Math.Max(box.Height - 2, 0));
        gfx.Save();
        gfx.IntersectClip(inside);
        DrawContent(gfx, inside);
        gfx.Restore();

        SetVariableTextAppearance(widget, form);
    }

    /// <summary>
    /// The colour an appearance-characteristics entry names - one component for grey, three for
    /// RGB and four for CMYK, as ISO 32000-1 Table 189 has them - or empty when it names none.
    /// </summary>
    private static XColor ColorIn(PdfDictionary characteristics, string key)
    {
        var components = characteristics?.Elements.GetArray(key);
        if (components == null)
            return XColor.Empty;

        double At(int index) => Math.Clamp(components.Elements.GetReal(index), 0, 1);

        return components.Elements.Count switch
        {
            1 => XColor.FromGrayScale(At(0)),
            3 => XColor.FromArgb((int)Math.Round(At(0) * 255), (int)Math.Round(At(1) * 255), (int)Math.Round(At(2) * 255)),
            4 => XColor.FromCmyk(At(0), At(1), At(2), At(3)),
            _ => XColor.Empty
        };
    }

    /// <summary>
    /// The field's default appearance string: its own <c>/DA</c>, an ancestor's, or the form's.
    /// </summary>
    private string EffectiveDefaultAppearance()
    {
        var owner = InheritedFrom(this, PdfAcroField.Keys.DA);
        if (owner != null)
            return owner.Elements.GetString(PdfAcroField.Keys.DA);

        return Owner?.AcroForm?.DefaultAppearance ?? "";
    }

    private XFont DefaultFont()
    {
        var size = 10.0;
        var match = FontSize.Match(EffectiveDefaultAppearance());
        if (match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var named)
            && named > 0)
        {
            size = named;
        }

        return new XFont(GlobalFontSettings.FontResolver.DefaultFontName, size);
    }

    private XColor ColorFromDefaultAppearance()
    {
        var appearance = EffectiveDefaultAppearance();

        double Number(Group group) => Math.Clamp(double.Parse(group.Value, CultureInfo.InvariantCulture), 0, 1);

        // The last colour operator wins, as it would in the content stream the string is for.
        Match last = null;
        foreach (Match match in ColorOperator.Matches(appearance))
            last = match;

        if (last == null)
            return XColors.Black;

        return last.Groups["op"].Value switch
        {
            "g" => XColor.FromGrayScale(Number(last.Groups["a"])),
            "rg" => XColor.FromArgb((int)Math.Round(Number(last.Groups["a"]) * 255),
                (int)Math.Round(Number(last.Groups["b"]) * 255), (int)Math.Round(Number(last.Groups["c"]) * 255)),
            "k" => XColor.FromCmyk(Number(last.Groups["a"]), Number(last.Groups["b"]),
                Number(last.Groups["c"]), Number(last.Groups["d"])),
            _ => XColors.Black
        };
    }

    private static readonly Regex FontSize = new(@"([0-9]*\.?[0-9]+)\s+Tf\b", RegexOptions.CultureInvariant);

    private static readonly Regex ColorOperator = new(
        @"(?<a>[0-9]*\.?[0-9]+)\s+(?:(?<b>[0-9]*\.?[0-9]+)\s+(?<c>[0-9]*\.?[0-9]+)\s+(?:(?<d>[0-9]*\.?[0-9]+)\s+)?)?(?<op>rg|g|k)\b",
        RegexOptions.CultureInvariant);

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
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;

        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
