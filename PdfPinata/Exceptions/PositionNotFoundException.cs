using PdfPinata.Pdf;

namespace PdfPinata.Exceptions;

/// <summary>
/// Thrown when a cross-reference entry points at a negative offset, so the object it names cannot
/// be found. A document that provokes it is malformed; whether the read carries on regardless is
/// decided by <see cref="PdfPinata.Pdf.IO.enums.PdfReadAccuracy"/>.
/// </summary>
public class PositionNotFoundException : System.Exception
{
    /// <summary>Initializes a new instance naming the object that could not be found.</summary>
    public PositionNotFoundException(PdfObjectID id) : base($"Object with ID {id} resolved with negative position.") { }
}
