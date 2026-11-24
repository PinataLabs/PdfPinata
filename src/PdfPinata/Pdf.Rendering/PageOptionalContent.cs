using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Which optional content (layers) a page is drawn with: a configuration of the document, and
/// groups switched on or off over it (ISO 32000-1 8.11).
/// </summary>
/// <remarks>
/// <para>
/// A default instance draws the document's default configuration, the <c>/D</c> of the catalog's
/// <c>/OCProperties</c>, as a viewer opens the file. That is what rendering does when no
/// instance is given.
/// </para>
/// <para>
/// A caller that builds a layer panel lists the groups from <c>/OCProperties /OCGs</c>, and
/// passes the ones the reader switched in <see cref="On"/> and <see cref="Off"/>.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PageOptionalContent
{
    /// <summary>
    /// The <c>/Name</c> of an entry of <c>/OCProperties /Configs</c> to draw with instead of
    /// <c>/D</c>, or null for <c>/D</c>. A name no configuration has is reported as
    /// <see cref="PageRenderProblem.InvalidValue"/> and <c>/D</c> is used. Where two have it, the
    /// first is used.
    /// </summary>
    public string Configuration { get; init; }

    /// <summary>
    /// A configuration of <see cref="OptionalContentLayers"/> to draw with, which wins over
    /// <see cref="Configuration"/>. It names the configuration itself, so one with no
    /// <c>/Name</c>, or with the name of another, is drawn as it is and not as <c>/D</c> or the
    /// first of that name. The default configuration of the model draws <c>/D</c>. One that is not
    /// of the document drawn is reported as <see cref="PageRenderProblem.InvalidValue"/> and
    /// <c>/D</c> is used.
    /// </summary>
    public OptionalContentConfiguration ChosenConfiguration { get; init; }

    /// <summary>
    /// Groups switched on whatever the configuration says. These are the group dictionaries of the
    /// document, as <c>/OCProperties /OCGs</c> holds them. A group does not count where its
    /// <c>/Intent</c> is not one of the configuration's, as for the configuration's own states.
    /// </summary>
    public IReadOnlyCollection<PdfDictionary> On { get; init; }

    /// <summary>
    /// Groups switched off whatever the configuration says. A group in both this and
    /// <see cref="On"/> is off.
    /// </summary>
    public IReadOnlyCollection<PdfDictionary> Off { get; init; }
}
