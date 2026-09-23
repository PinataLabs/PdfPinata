using System;

namespace PinataLayout.DocumentObjectModel.Fields;

/// <summary>
/// Says what a field reads as, given the facts a laid-out document produces.
/// </summary>
/// <remarks>
/// This used to be three private methods on <c>ParagraphRenderer</c>, which is why asking what a
/// <see cref="SectionField"/> formatted as cost a rendered page. It is a pure function of a field
/// and a <see cref="FieldEvaluationContext"/>: no graphics, no page, and nothing about how the
/// answer is drawn.
/// <para>
/// It produces the real value or says it cannot yet, and never a placeholder. What to show in the
/// meantime - an "XX" width estimate, or a complaint that a bookmark was never defined - is a
/// decision about a rendering pipeline's phase rather than a fact about the field, and stays with
/// the renderer.
/// </para>
/// </remarks>
public static class FieldEvaluator
{
    /// <summary>
    /// Whether this object is a field with a value, and so one <see cref="Evaluate"/> can be asked
    /// about. <see cref="BookmarkField"/> is not one: it marks a place rather than reading as
    /// anything.
    /// </summary>
    public static bool IsField(DocumentObject documentObject)
    {
        return documentObject is NumericFieldBase
               || documentObject is DateField
               || documentObject is InfoField;
    }

    /// <summary>
    /// The text the field reads as, or null when the answer is not knowable yet: a bookmark that
    /// has not been placed, or a page count for a document or section that has not finished being
    /// laid out.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The object is not a field with a value - <see cref="IsField"/> says which are - or it is an
    /// <see cref="InfoField"/> belonging to no document, so there is no information to read.
    /// </exception>
    public static string Evaluate(DocumentObject field, FieldEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(context);

        switch (field)
        {
            case NumericFieldBase numericField:
            {
                var number = NumberFor(numericField, context);
                return number == null ? null : NumberFormatter.Format(number.Value, numericField.Format);
            }
            case DateField dateField:
                return context.PrintDate.ToString(dateField.Format);
            case InfoField infoField:
                return DocumentInformation(infoField);
            default:
                throw new ArgumentException(
                    $@"'{field.GetType().Name}' is not a field with a value. Ask IsField before Evaluate.",
                    nameof(field));
        }
    }

    /// <summary>
    /// The number the field stands for, or null when it is not knowable yet. A page number and a
    /// section number always are - the page a field is being evaluated on is the page it is on -
    /// so only the two counts and an unplaced bookmark can answer null.
    /// </summary>
    private static int? NumberFor(NumericFieldBase field, FieldEvaluationContext context)
    {
        if (field is PageRefField pageRefField)
        {
            var page = context.ResolveBookmarkPage?.Invoke(pageRefField.Name);
            return page > 0 ? page : null;
        }

        if (field is PageField)
            return context.DisplayPageNumber;

        if (field is SectionField)
            return context.SectionNumber;

        if (field is NumPagesField)
            return context.NumberOfPages;

        if (field is SectionPagesField)
            return context.PagesInSection;

        throw new ArgumentException(
            $@"'{field.GetType().Name}' is a numeric field this evaluator does not know a number for.",
            nameof(field));
    }

    /// <summary>
    /// What the document records under the name the field asks for. The name is matched against
    /// <see cref="InfoFieldType"/> the way <c>DocumentInfo</c> stores it, case ignored; a name that
    /// matches nothing reads as the empty string, which is what an unnamed field does too.
    /// </summary>
    private static string DocumentInformation(InfoField field)
    {
        var document = field.Document;
        if (document == null)
            throw new ArgumentException(
                "An InfoField that belongs to no document has no information to read. Add it to a "
                + "paragraph before asking what it reads as.",
                nameof(field));

        foreach (var name in Enum.GetNames<InfoFieldType>())
        {
            if (string.Compare(field.Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                return document.Info.GetValue(name)?.ToString() ?? "";
        }

        return "";
    }
}
