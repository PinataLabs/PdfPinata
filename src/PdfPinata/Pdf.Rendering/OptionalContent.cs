using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Which of a document's optional content is switched off in its default configuration: the
/// <c>/D</c> of the catalog's <c>/OCProperties</c> (ISO 32000-1 8.11).
/// </summary>
/// <remarks>
/// This answers a question and hides nothing. Page rendering does not apply optional content
/// yet, and asks only so that it can say when it has drawn what a viewer would not.
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
    /// thirty-second operands. Past this many the rest are taken to be visible.
    /// </summary>
    private const int MaximumExpressionOperands = 1024;

    private int _operandsLeft;

    // The groups /D lists against its base state: those in /OFF, or where every group starts
    // off, those in /ON.
    private readonly HashSet<object> _exceptions = new(ReferenceIdentity.Instance);
    private readonly bool _offUnlessListed;

    private OptionalContent(PdfDictionary configuration)
    {
        _offUnlessListed = configuration?.Elements.GetNameOrEmpty("/BaseState") == "/OFF";
        var listed = configuration?.Elements.GetArray(_offUnlessListed ? "/ON" : "/OFF");
        for (var index = 0; listed != null && index < listed.Elements.Count; index++)
        {
            if (listed.Elements.GetDictionary(index) is { } group)
                _exceptions.Add(ReferenceIdentity.KeyOf(group));
        }
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
        if (membership.Elements.GetNameOrEmpty("/Type") != "/OCMD")
            return !IsOn(membership);

        // A visibility expression, where there is one, is used and the policy is not.
        if (membership.Elements.GetArray("/VE") is { } expression)
        {
            _operandsLeft = MaximumExpressionOperands;
            return !IsVisible(expression, 0);
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

    private bool IsOn(PdfDictionary group) => _exceptions.Contains(ReferenceIdentity.KeyOf(group)) == _offUnlessListed;

    /// <summary>
    /// A visibility expression: <c>/And</c>, <c>/Or</c> or <c>/Not</c>, then groups and further
    /// expressions. One that is not that is taken to be visible.
    /// </summary>
    private bool IsVisible(PdfArray expression, int depth)
    {
        if (depth >= MaximumExpressionDepth || expression.Elements.Count < 2
                                            || !PdfItemValues.TryGetName(expression.Elements[0], out var operation))
        {
            return true;
        }

        if (operation == "/Not")
            return !IsVisible(expression.Elements[1], depth);

        if (operation != "/And" && operation != "/Or")
            return true;

        // And is true until an operand is not, and Or is false until one is.
        var all = operation == "/And";
        for (var index = 1; index < expression.Elements.Count; index++)
        {
            if (IsVisible(expression.Elements[index], depth) != all)
                return !all;
        }

        return all;
    }

    private bool IsVisible(PdfItem operand, int depth)
    {
        if (_operandsLeft <= 0)
            return true;

        _operandsLeft--;
        return PdfReference.Dereference(operand) switch
        {
            PdfArray nested => IsVisible(nested, depth + 1),
            PdfDictionary group => IsOn(group),
            _ => true
        };
    }
}
