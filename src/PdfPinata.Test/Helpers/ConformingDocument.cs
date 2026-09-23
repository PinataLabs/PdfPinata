using System;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   A document with everything a PDF/A claim will be held to at save time, so that a test about
///   one feature fails for that feature rather than for a missing title or profile.
/// </summary>
/// <remarks>
///   <see cref="IO.XmpMetadataTests"/> and <see cref="Pdfs.EInvoiceTests"/> each grew this
///   arrangement independently before it lived here — the clearest sign an interface was missing a
///   parameter, not that either author was doing anything unusual.
/// </remarks>
internal static class ConformingDocument
{
    internal const string Title = "Invoice 2026-0042";

    /// <summary>
    ///   Not a real ICC profile. Nothing here parses one — the writer embeds the bytes it is given —
    ///   so a recognisable stand-in makes the assertions legible and says plainly that these tests
    ///   are not colour-management tests.
    /// </summary>
    internal static readonly byte[] SomeProfile = "NOT-AN-ICC-PROFILE"u8.ToArray();

    /// <summary>A document with a title and an output-intent profile, claiming no conformance yet.</summary>
    internal static PdfDocument Prepared()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Info.Title = Title;
        document.Options.OutputIntentIccProfile = SomeProfile;
        document.Options.OutputIntentIdentifier = "sRGB IEC61966-2.1";
        return document;
    }

    /// <summary>Everything a document needs before it may claim the given profile.</summary>
    internal static Action<PdfDocument> Conforming(PdfAConformance conformance) => document =>
    {
        document.Options.Conformance = conformance;
        document.Options.OutputIntentIccProfile = SomeProfile;
        document.Options.OutputIntentIdentifier = "sRGB IEC61966-2.1";
    };
}
