using System;

namespace PinataLayout.DocumentObjectModel.Fields;

/// <summary>
/// The facts about a laid-out document that a field's value can depend on: which page and section
/// it is being asked on, how many pages there turned out to be, when the document is being printed,
/// and where a bookmark ended up.
/// </summary>
/// <remarks>
/// It carries what pagination produces and nothing else. A field that reads the document itself -
/// <see cref="InfoField"/> is the only one - walks to it through <see cref="DocumentObject.Document"/>
/// rather than being handed it here, which keeps this to a page's worth of numbers instead of the
/// whole object model.
/// </remarks>
public sealed class FieldEvaluationContext
{
    /// <summary>
    /// The page number as the document shows it, which is not the physical page number when a
    /// section restarts the numbering.
    /// </summary>
    public int DisplayPageNumber { get; set; }

    /// <summary>
    /// The one-based number of the section the field sits in.
    /// </summary>
    public int SectionNumber { get; set; }

    /// <summary>
    /// How many pages the document has, or null while it is still being laid out and the answer is
    /// genuinely not known yet.
    /// </summary>
    public int? NumberOfPages { get; set; }

    /// <summary>
    /// How many pages this field's section has, or null while that section is still being laid out.
    /// </summary>
    public int? PagesInSection { get; set; }

    /// <summary>
    /// The date a <see cref="DateField"/> reads as.
    /// </summary>
    public DateTime PrintDate { get; set; }

    /// <summary>
    /// Answers the page number shown for a named bookmark, or null when no bookmark of that name
    /// has been placed. A <see cref="PageRefField"/> needs one lookup rather than the whole table,
    /// so this is a question rather than a dictionary.
    /// </summary>
    public Func<string, int?> ResolveBookmarkPage { get; set; }
}
