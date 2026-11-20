using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Which of a document's optional content is switched off in its default configuration: the
/// <c>/D</c> of the catalog's <c>/OCProperties</c> (ISO 32000-1 8.11).
/// </summary>
/// <remarks>
/// <para>
/// This answers a question and hides nothing. Page rendering does not apply optional content
/// yet, and asks only so that it can say when it has drawn what a viewer would not.
/// </para>
/// <para>
/// One instance serves one render, and remembers its answer for each membership: a page may
/// put thousands of runs of content in the same one, and an expression is read to a budget of
/// operands each time it is read.
/// </para>
/// </remarks>
internal sealed class OptionalContent
{
    /// <summary>
    /// How deep a visibility expression is read. One nested deeper is taken to be visible,
    /// because a stack overflow cannot be caught.
    /// </summary>
    private const int MaximumExpressionDepth = 32;

    /// <summary>
    /// How many operands of one visibility expression are looked at. An expression may name the
    /// same array twice, or itself, so the depth alone lets a few bytes ask for two to the
    /// thirty-second operands. Past this many the expression is taken to be visible.
    /// </summary>
    private const int MaximumExpressionOperands = 1024;

    private int _operandsLeft;

    // Set while reading an expression when part of it cannot be read, which makes the whole of
    // it visible.
    private bool _unreadable;

    // The groups /D lists against its base state: those in /OFF, or where every group starts
    // off, those in /ON.
    private readonly HashSet<object> _exceptions = new(ReferenceIdentity.Instance);
    private readonly bool _offUnlessListed;

    // The intents of /D, or null where it names /All.
    private readonly HashSet<string> _intents;

    private readonly Dictionary<object, bool> _hides = new(ReferenceIdentity.Instance);

    private OptionalContent(PdfDictionary configuration)
    {
        _offUnlessListed = configuration?.Elements.GetNameOrEmpty("/BaseState") == "/OFF";
        var listed = configuration?.Elements.GetArray(_offUnlessListed ? "/ON" : "/OFF");
        for (var index = 0; listed != null && index < listed.Elements.Count; index++)
        {
            if (listed.Elements.GetDictionary(index) is { } group)
                _exceptions.Add(ReferenceIdentity.KeyOf(group));
        }

        _intents = Intents(configuration?.Elements["/Intent"]);
        if (_intents.Contains("/All"))
            _intents = null;
    }

    /// <summary>The optional content of a document, which may state none.</summary>
    internal static OptionalContent Of(PdfDocument document) =>
        new(document?.Catalog.Elements.GetDictionary(PdfCatalog.Keys.OCProperties)?.Elements.GetDictionary("/D"));

    /// <summary>
    /// Whether content is hidden that belongs to a group, or to a membership dictionary that
    /// makes one answer of several groups.
    /// </summary>
    /// <param name="membership">What an <c>/OC</c> names: an optional content group or an OCMD.</param>
    internal bool Hides(PdfDictionary membership)
    {
        var key = ReferenceIdentity.KeyOf(membership);
        if (!_hides.TryGetValue(key, out var hides))
            _hides[key] = hides = Evaluate(membership);

        return hides;
    }

    private bool Evaluate(PdfDictionary membership)
    {
        if (membership.Elements.GetNameOrEmpty("/Type") != "/OCMD")
            return !IsOn(membership);

        // A visibility expression, where there is one, is used and the policy is not.
        if (membership.Elements.GetArray("/VE") is { } expression)
        {
            _operandsLeft = MaximumExpressionOperands;
            _unreadable = false;
            var visible = IsVisible(expression, 0);
            return !_unreadable && !visible;
        }

        var groups = new List<PdfDictionary>();
        if (membership.Elements.GetArray("/OCGs") is { } several)
        {
            for (var index = 0; index < several.Elements.Count; index++)
            {
                if (several.Elements.GetDictionary(index) is { } group)
                    groups.Add(group);
            }
        }
        else if (membership.Elements.GetDictionary("/OCGs") is { } single)
        {
            groups.Add(single);
        }

        // A membership dictionary that names no group hides nothing.
        if (groups.Count == 0)
            return false;

        return membership.Elements.GetNameOrEmpty("/P") switch
        {
            "/AllOn" => !groups.TrueForAll(IsOn),
            "/AnyOff" => groups.TrueForAll(IsOn),
            "/AllOff" => groups.Exists(IsOn),
            _ => !groups.Exists(IsOn)
        };
    }

    /// <summary>
    /// Whether a group is on. One with no intent the configuration names has no effect on
    /// visibility (ISO 32000-1 8.11.2.3), and so is on.
    /// </summary>
    private bool IsOn(PdfDictionary group) =>
        !Counts(group) || _exceptions.Contains(ReferenceIdentity.KeyOf(group)) == _offUnlessListed;

    private bool Counts(PdfDictionary group)
    {
        if (_intents == null)
            return true;

        foreach (var intent in Intents(group.Elements["/Intent"]))
        {
            if (_intents.Contains(intent))
                return true;
        }

        return false;
    }

    /// <summary>
    /// An <c>/Intent</c>: one name or an array of names, and <c>/View</c> where there is none
    /// (ISO 32000-1 Tables 98 and 101).
    /// </summary>
    private static HashSet<string> Intents(PdfItem intent)
    {
        var intents = new HashSet<string>();
        switch (PdfReference.Dereference(intent))
        {
            case PdfArray names:
                foreach (var name in names.Elements)
                {
                    if (PdfItemValues.TryGetName(name, out var value))
                        intents.Add(value);
                }

                break;

            case var single when PdfItemValues.TryGetName(single, out var value):
                intents.Add(value);
                break;

            default:
                intents.Add("/View");
                break;
        }

        return intents;
    }

    /// <summary>
    /// A visibility expression: <c>/And</c> or <c>/Or</c> over one or more operands, or
    /// <c>/Not</c> over one, each a group or a further expression. Where any part of it is not
    /// that, or lies past the depth or the operands read, the whole expression is visible.
    /// </summary>
    private bool IsVisible(PdfArray expression, int depth)
    {
        if (depth >= MaximumExpressionDepth || expression.Elements.Count < 2
                                            || !PdfItemValues.TryGetName(expression.Elements[0], out var operation))
        {
            return Unreadable();
        }

        if (operation == "/Not")
            return expression.Elements.Count == 2 ? !IsVisible(expression.Elements[1], depth) : Unreadable();

        if (operation != "/And" && operation != "/Or")
            return Unreadable();

        // Every operand is read, because one that cannot be read anywhere makes the expression
        // visible whatever the others answer.
        var all = operation == "/And";
        var result = all;
        for (var index = 1; index < expression.Elements.Count; index++)
        {
            var visible = IsVisible(expression.Elements[index], depth);
            if (_unreadable)
                return true;
            if (visible != all)
                result = !all;
        }

        return result;
    }

    private bool IsVisible(PdfItem operand, int depth)
    {
        if (_operandsLeft <= 0)
            return Unreadable();

        _operandsLeft--;
        return PdfReference.Dereference(operand) switch
        {
            PdfArray nested => IsVisible(nested, depth + 1),
            PdfDictionary group => IsOn(group),
            _ => Unreadable()
        };
    }

    private bool Unreadable()
    {
        _unreadable = true;
        return true;
    }
}
