namespace PdfPinata.EInvoice;

/// <summary>
/// How much of an invoice the attached XML actually says, written into the metadata as
/// <c>fx:ConformanceLevel</c>.
/// </summary>
/// <remarks>
/// <para>
/// A profile is a promise about the XML, not about the PDF. A receiver reads it to decide whether
/// the document can be booked without a human looking at it: <see cref="Minimum"/> and
/// <see cref="BasicWithoutLines"/> carry no line items and are legally an accounting aid rather than
/// an invoice in France, while <see cref="En16931"/> is the European semantic standard in full and
/// is what a mandate means when it says "e-invoice".
/// </para>
/// <para>
/// This type is here for the spelling. The values a validator expects are exact strings with a space
/// in two of them — <c>BASIC WL</c> and <c>EN 16931</c> — and a document that writes
/// <c>EN16931</c> or <c>BASICWL</c> passes every check that looks at the PDF and is rejected by the
/// system that reads the invoice. Nothing here validates the XML against the profile it names:
/// saying which profile the XML meets is the caller's claim, and only a schema validator can settle
/// it.
/// </para>
/// </remarks>
public enum EInvoiceProfile
{
    /// <summary>
    /// The smallest profile: who, when, and the totals. No line items, and not a legal invoice in
    /// France — where it was defined as an accounting aid — though it is a valid Factur-X document.
    /// </summary>
    Minimum,

    /// <summary>
    /// Basic without lines: the whole invoice header, the tax breakdown and the totals, and still no
    /// line items. Written <c>BASIC WL</c>, with the space.
    /// </summary>
    BasicWithoutLines,

    /// <summary>
    /// Basic: a subset of <see cref="En16931"/> with line items, enough for the simple invoices that
    /// are most of them.
    /// </summary>
    Basic,

    /// <summary>
    /// The European semantic standard EN 16931 in full — the profile the public mandates are written
    /// against, and the default here. Written <c>EN 16931</c>, with the space.
    /// </summary>
    En16931,

    /// <summary>
    /// EN 16931 plus the extensions a sector or a country adds to it. A receiver that understands
    /// only <see cref="En16931"/> can still read the part of it that is EN 16931.
    /// </summary>
    Extended,

    /// <summary>
    /// The German XRechnung profile, which is EN 16931 with the national business rules applied.
    /// Carried in a Factur-X container as CII, which is what makes it a value here rather than a
    /// format of its own.
    /// </summary>
    XRechnung,
}
