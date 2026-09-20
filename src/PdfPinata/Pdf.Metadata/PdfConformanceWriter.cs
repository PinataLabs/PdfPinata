using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Security;

namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// Writes what a document needs in order to claim an archival profile — the XMP packet, the output
/// intent — and refuses to write it at all when it breaks a rule of the profile it claims.
/// </summary>
/// <remarks>
/// <para>
/// The refusal is the point. A library that stamps <c>pdfaid:part 3</c> on a file and leaves the
/// caller to find out from a validator, or from their customer, that it does not conform has made
/// things worse rather than better. So every rule checked here throws — at <c>Save</c> always, and
/// through <see cref="CheckClaimable"/> at <see cref="PdfDocument.ClaimConformance"/> as well for
/// what a claim can be held to immediately — naming the rule and what to do about it.
/// </para>
/// <para>
/// Every PDF/A rule this library enforces is meant to be reachable from here, including the two
/// that live where the thing they govern is written rather than in this file: <see cref="RequiresCidSet"/>
/// is what <see cref="PdfCIDFont"/> asks before writing a subset's <c>/CIDSet</c>, and
/// <see cref="PdfVersionRequirements"/> is the one place a floor the profile claimed implies gets
/// raised, alongside the two other features that raise it for reasons of their own.
/// </para>
/// </remarks>
internal static class PdfConformanceWriter
{
    /// <summary>
    /// Called while the document is preparing to be saved, after the information dictionary has
    /// been settled — the XMP packet is built from it and the two must agree.
    /// </summary>
    public static void PrepareForSave(PdfDocument document)
    {
        var options = document.Options;
        var claimsConformance = options.Conformance != PdfAConformance.None;
        var claimsAccessibility = options.UAConformance != PdfUAConformance.None;
        var requiresTagging = RequiresTagging(options.Conformance);

        if (!claimsConformance && !claimsAccessibility && !options.WriteXmpMetadata)
            return;

        if (claimsConformance)
        {
            Enforce(document);
            AttachOutputIntent(document);
        }

        // An A-level archival claim is held to the same tagging rules a PDF/UA claim is, without
        // itself writing a PDF/UA identifier — the two are separate standards, and claiming one
        // does not claim the other. What is shared is the checking, not the metadata.
        if (claimsAccessibility || requiresTagging)
            EnforceAccessibility(document);

        AttachMetadata(document);
    }

    /// <summary>
    /// Whether the claimed archival profile is an <c>A</c> level, which additionally requires the
    /// document to be tagged.
    /// </summary>
    internal static bool RequiresTagging(PdfAConformance conformance) => conformance is
        PdfAConformance.PdfA1A or PdfAConformance.PdfA2A or PdfAConformance.PdfA3A;

    /// <summary>Whether the claimed profile is part 1 of ISO 19005, whichever level.</summary>
    static bool IsPart1(PdfAConformance conformance) =>
        conformance is PdfAConformance.PdfA1B or PdfAConformance.PdfA1A;

    /// <summary>Whether the claimed profile is part 3 of ISO 19005, whichever level.</summary>
    static bool IsPart3(PdfAConformance conformance) =>
        conformance is PdfAConformance.PdfA3B or PdfAConformance.PdfA3A;

    /// <summary>
    /// Settles the two things a PDF/UA claim implies rather than asks for, and then holds the
    /// document to the rest.
    /// </summary>
    static void EnforceAccessibility(PdfDocument document)
    {
        // Set rather than demanded. Both are mechanical consequences of the claim — nobody asks for
        // PDF/UA and wants a reader to announce the file name — and refusing to save over something
        // there is only one right answer to teaches nothing. The title itself is refused rather than
        // invented, because only the caller knows it.
        document.ViewerPreferences.DisplayDocTitle = true;

        Structure.PdfUaValidator.Validate(document);
    }

    /// <summary>
    /// The rules of a claimed profile that are properties of the document as it stands, and so can
    /// be settled the moment the claim is made rather than waiting for <c>Save</c> to discover them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from <see cref="PdfDocument.ClaimConformance"/>, so that a caller who claims a profile
    /// before the document is ready is told where the mistake was made, and from <see cref="Enforce"/>,
    /// so that a document changed after the claim — encrypted afterwards, say — is still held to it.
    /// Both calls run exactly these checks; nothing here depends on which one is asking.
    /// </para>
    /// <para>
    /// What is not here is exactly what cannot be settled by looking at the document now: whether it
    /// carries an attachment, which PDF/A-3 alone may hold. A caller may legitimately attach a file
    /// after claiming the profile — <c>FacturXInvoice.AttachTo</c> does exactly that — so that rule
    /// stays in <see cref="Enforce"/> alone, checked against the document as it is at <c>Save</c>.
    /// </para>
    /// <para>
    /// Nor is <see cref="CheckVersionAgainstProfile"/> here, even though it too is settled by looking
    /// at the document now: adding an attachment already raises the version floor, so checking it
    /// ahead of the attachment rule would have a document refused for its version rather than for the
    /// attachment PDF/A-1 cannot carry at all — a less specific message for the same mistake. Called
    /// separately, and later, so <see cref="Enforce"/> can put the attachment rule first exactly as
    /// it did before either was extracted.
    /// </para>
    /// </remarks>
    internal static void CheckClaimable(PdfDocument document, PdfAConformance conformance)
    {
        if (conformance == PdfAConformance.None)
            return;

        // Settleable immediately, unlike the rest of what tagging implies: a document with no
        // structure tree cannot become tagged by being saved, so there is nothing to gain by
        // waiting for Save to say so.
        if (RequiresTagging(conformance) && !document.IsTagged)
            throw new InvalidOperationException(
                conformance + " additionally requires the document to be tagged, and this one is "
                + "not — a document with no structure tree cannot become tagged by being saved. "
                + "Build one through PdfDocument.Structure before claiming " + conformance + ", or "
                + "claim the B level instead, which does not require tagging.");

        if (document.SecuritySettings.DocumentSecurityLevel != PdfDocumentSecurityLevel.None)
            throw new InvalidOperationException(
                "A PDF/A document may not be encrypted. Either drop the conformance claim or clear "
                + "SecuritySettings.DocumentSecurityLevel.");

        if (string.IsNullOrEmpty(document.Info.Title))
            throw new InvalidOperationException(
                "A PDF/A document has to have a title, in the document information dictionary and "
                + "in its XMP metadata alike. Set Info.Title.");

        var options = document.Options;

        // Refused only where nothing true could be supplied. An RGB document with no profile of its
        // own is given sRGB, because colours written as RGB by a library that was never told
        // otherwise are sRGB — see PdfOutputIntents. The other two modes are a different question
        // and get a different answer.
        if (!HasProfile(options) && options.ColorMode == PdfColorMode.Cmyk)
            throw new InvalidOperationException(
                "A PDF/A document has to embed an ICC profile saying what its colours mean, and CMYK "
                + "numbers mean nothing at all without one — the same four numbers are a different "
                + "colour on every press. This library supplies sRGB for an RGB document, where the "
                + "numbers describe themselves, and cannot do the equivalent here. Set "
                + "Options.OutputIntentIccProfile to the profile your work was made for.");

        if (!HasProfile(options) && options.ColorMode == PdfColorMode.Undefined)
            throw new InvalidOperationException(
                "A PDF/A document has to embed an ICC profile saying what its colours mean, and "
                + "ColorMode is Undefined — which writes every colour as the XColor it came from, so "
                + "this document may hold RGB and CMYK together and no one profile describes it. "
                + "Either set Options.ColorMode to Rgb, which is given " + nameof(PdfOutputIntents)
                + "." + nameof(PdfOutputIntents.SrgbProfile) + " when nothing else is set, or set "
                + "Options.OutputIntentIccProfile yourself.");
    }

    /// <summary>
    /// The two rules that follow from PDF/A-1 being defined against an older PDF version than the
    /// later parts are. Kept apart from <see cref="CheckClaimable"/> — see its remarks — so that
    /// <see cref="Enforce"/> can check them after the attachment rule rather than before it.
    /// </summary>
    internal static void CheckVersionAgainstProfile(PdfDocument document, PdfAConformance conformance)
    {
        if (!IsPart1(conformance))
            return;

        // PDF/A-1 is defined against PDF 1.4 and the later parts against PDF 1.7. Raising a low
        // version is not enough on its own: a document that has already asked for something newer
        // keeps it, and would carry a PDF/A-1 claim over a header PDF/A-1 does not allow.
        if (document._version > 14)
            throw new InvalidOperationException(
                "PDF/A-1 is defined against PDF 1.4, and this document is written as PDF "
                + (document._version / 10) + "." + (document._version % 10) + ". Either claim "
                + "PDF/A-2 or later, or stop asking for the feature that raised the version.");

        // Checked directly against the setting that would raise it, rather than against the version
        // number: Options.CrossReferenceFormat is known the moment it is set, but the version it
        // implies is not raised until the document is written, which is too late for this to see.
        if (document.Options.CrossReferenceFormat == PdfCrossReferenceFormat.Stream)
            throw new InvalidOperationException(
                "PDF/A-1 is defined against PDF 1.4 and a cross-reference stream is a PDF 1.5 "
                + "construction, so the two cannot both be asked for. Either claim PDF/A-2 or later, "
                + "or leave Options.CrossReferenceFormat as Classic.");
    }

    /// <summary>
    /// The rules of the claimed profile that can be settled by looking at the document.
    /// </summary>
    static void Enforce(PdfDocument document)
    {
        var options = document.Options;

        CheckClaimable(document, options.Conformance);

        var attachments = EmbeddedFiles(document);

        if (!IsPart3(options.Conformance) && attachments.Count != 0)
            throw new InvalidOperationException(
                options.Conformance + " may not carry an embedded file unless that file is itself "
                + "PDF/A — and nothing here can establish that, so the claim is refused rather than "
                + "made on trust. This document carries " + attachments.Count + ", the first of them "
                + "'" + attachments[0].FileName + "'. PDF/A-3 is the profile with no such "
                + "restriction, which is why hybrid e-invoices such as ZUGFeRD and Factur-X are "
                + "built on it.");

        if (IsPart3(options.Conformance))
            EnforceAssociation(document, attachments);

        CheckVersionAgainstProfile(document, options.Conformance);
        CheckResourceRules(document, options.Conformance);

        // Raise rather than set, so a document that has already asked for more keeps it. See
        // PdfVersionRequirements for the other two features that raise this floor.
        var floor = IsPart1(options.Conformance) ? 14 : 17;
        PdfVersionRequirements.Require(document, floor);
    }

    /// <summary>
    /// The four rules a page's content and resources can break, none of which a claim-time check
    /// can settle — what a page will contain is not knowable until it is drawn. Transparency and a
    /// JPEG 2000 image are refused for part 1 alone and permitted by the later parts; an
    /// interpolated image is refused under every archival profile; and a device colour space the
    /// output intent does not describe is refused wherever an output intent is required, which is
    /// every archival profile.
    /// </summary>
    /// <remarks>
    /// Walking a page's resources is <see cref="PdfResourcePruner"/>'s job as well as this one — see
    /// <see cref="PdfPageResourceUsage"/> — so the two cannot disagree about what a page uses. A page
    /// whose content could not be fully understood is left unchecked here exactly as it is left
    /// unpruned there, rather than refused on a guess: what is checked is checked properly, and a
    /// document this library itself writes never reaches that path.
    /// </remarks>
    static void CheckResourceRules(PdfDocument document, PdfAConformance conformance)
    {
        if (conformance == PdfAConformance.None)
            return;

        var families = new HashSet<int>();

        for (var index = 0; index < document.PageCount; index++)
        {
            var usage = PdfPageResourceUsage.Walk(document.Pages[index]);
            if (!usage.Understood)
                continue;

            if (IsPart1(conformance))
            {
                if (PdfResourceConformanceRules.UsesTransparency(usage))
                    throw new InvalidOperationException(
                        "PDF/A-1 forbids transparency, and page " + (index + 1) + " paints with it "
                        + "— found by walking every resource the page reaches, not only what its "
                        + "content names outright. Claim PDF/A-2 or later, which permits it, or "
                        + "remove the transparency.");

                if (PdfResourceConformanceRules.UsesJpxImage(usage))
                    throw new InvalidOperationException(
                        "PDF/A-1 forbids a JPEG 2000 image, and page " + (index + 1) + " carries "
                        + "one. Claim PDF/A-2 or later, which permits it, or re-encode the image.");
            }

            if (PdfResourceConformanceRules.UsesInterpolatedImage(usage))
                throw new InvalidOperationException(
                    conformance + " forbids an image set to interpolate, and page " + (index + 1)
                    + " carries one. Clear XImage.Interpolate before saving.");

            PdfResourceConformanceRules.CollectDeviceColorFamilies(usage, families);
        }

        if (families.Count == 0)
            return;

        var options = document.Options;
        var componentsOfIntent = ComponentsOf(
            HasProfile(options) ? options.OutputIntentIccProfile : PdfOutputIntents.SrgbProfile,
            options.ColorMode);

        // A device colour of one component, DeviceGray, never conflicts with an output intent of
        // any size: grey is unambiguous in any working space, which is what makes it the one
        // family this loop leaves alone.
        var mismatched = new List<int>();
        foreach (var family in families)
        {
            if (family != 1 && family != componentsOfIntent)
                mismatched.Add(family);
        }

        if (mismatched.Count != 0)
        {
            mismatched.Sort();
            throw new InvalidOperationException(
                conformance + " requires the output intent to describe every device colour the "
                + "document paints, and its profile describes a " + componentsOfIntent
                + "-component space while a page paints with a " + string.Join(" and ",
                    mismatched.ConvertAll(count => count + "-component"))
                + " device colour. Set Options.ColorMode and Options.OutputIntentIccProfile to "
                + "agree with what the pages actually paint, or stop mixing colour spaces the "
                + "intent cannot describe together.");
        }
    }

    /// <summary>
    /// Whether a Type 2 CIDFont's subset needs a <c>/CIDSet</c> under the profile claimed — PDF/A-1
    /// clause 6.3.5 alone; PDF/A-2 dropped the requirement as redundant.
    /// </summary>
    /// <remarks>
    /// Asked of this module rather than read directly off <see cref="PdfDocumentOptions.Conformance"/>
    /// so that every rule PDF/A implies is reachable from one place, including the ones a font
    /// dictionary has to act on well before <see cref="PrepareForSave"/> runs.
    /// </remarks>
    internal static bool RequiresCidSet(PdfAConformance conformance) => IsPart1(conformance);

    /// <summary>
    /// The rules PDF/A-3 adds by permitting attachments at all: each has to be attached <em>to</em>
    /// something rather than merely present, each has to say what it is to the document, and each has
    /// to say what kind of file it is.
    /// </summary>
    /// <remarks>
    /// All three are refused rather than repaired, and the difference from the other rules here is
    /// worth stating. A loose file could be associated with the document, a missing relationship could
    /// be written as <c>/Unspecified</c>, and a missing media type as <c>application/octet-stream</c> —
    /// and every one of those would be this code deciding what an attachment means, which is the one
    /// thing it does not know. <see cref="PdfAttachments"/> settles all three at the point the caller
    /// does know, which is why a document built through it never reaches these throws.
    /// </remarks>
    static void EnforceAssociation(PdfDocument document, List<PdfFileSpecification> attachments)
    {
        var associated = document.Catalog.Elements.GetArray(PdfCatalog.Keys.AF);

        foreach (var attachment in attachments)
        {
            if (!IsListedIn(associated, attachment))
                throw new InvalidOperationException(
                    "PDF/A-3 requires every embedded file to be associated with the document or with "
                    + "part of it, and '" + attachment.FileName + "' is associated with nothing — it "
                    + "is in the file but not of it, which a validator reads as a broken document "
                    + "rather than as a document with an attachment. Attach it through "
                    + "Attachments.Add, or hand the specification you already have to "
                    + "Attachments.Associate.");

            if (attachment.Elements[PdfFileSpecification.Keys.AFRelationship] == null)
                throw new InvalidOperationException(
                    "PDF/A-3 requires every attachment to say what it has to do with the document, "
                    + "and '" + attachment.FileName + "' says nothing. Set its Relationship — "
                    + nameof(PdfAFRelationship) + "." + nameof(PdfAFRelationship.Data) + " is what a "
                    + "hybrid e-invoice wants, and " + nameof(PdfAFRelationship) + "."
                    + nameof(PdfAFRelationship.Unspecified) + " is the honest answer when there is "
                    + "nothing more precise to say.");

            if (string.IsNullOrEmpty(attachment.EmbeddedFile.MimeType))
                throw new InvalidOperationException(
                    "PDF/A-3 requires every attachment to say what kind of file it is, and '"
                    + attachment.FileName + "' does not. Set the embedded file's MimeType — "
                    + "'application/octet-stream' is what the standard names for a file whose type is "
                    + "not known, which Attachments.Add writes when it is given none.");
        }
    }

    /// <summary>
    /// How many components the embedded profile's colour space has, which is what <c>/N</c> states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read out of the profile rather than inferred from <see cref="PdfDocumentOptions.ColorMode"/>,
    /// because the mode does not decide it. <see cref="PdfColorMode.Undefined"/> writes each colour
    /// as the <c>XColor</c> gave it and says nothing about which profile was supplied, and a caller
    /// in any mode may hand over a profile for a space other than the one they are drawing in — both
    /// of which used to write <c>/N 3</c> over four-component data, an output intent a validator
    /// rejects in a document whose whole point is that it does not get rejected. An ICC profile
    /// names its data colour space at byte 16, so there is no need to guess.
    /// </para>
    /// <para>
    /// An unreadable header falls back to what the colour mode implies rather than throwing.
    /// Nothing in this library parses a profile — the tests hand the writer legible stand-ins like
    /// <c>NOT-AN-ICC-PROFILE</c> precisely because it does not — and refusing to save over a header
    /// this cannot read would turn a writer into a validator it has never claimed to be.
    /// </para>
    /// <para>
    /// What this answers for the output intent, <see cref="CheckResourceRules"/> compares against
    /// what a page-resource walk finds the document actually paints with — a document in
    /// <see cref="PdfColorMode.Undefined"/> may hold RGB and CMYK together, and the output intent
    /// this method describes has to be one of them or the other.
    /// </para>
    /// </remarks>
    static int ComponentsOf(byte[] profile, PdfColorMode mode)
    {
        const int SpaceAt = 16;

        if (profile != null && profile.Length >= SpaceAt + 4)
        {
            var space = new string(new[]
            {
                (char)profile[SpaceAt], (char)profile[SpaceAt + 1],
                (char)profile[SpaceAt + 2], (char)profile[SpaceAt + 3]
            });

            switch (space)
            {
                case "GRAY": return 1;
                case "CMYK": return 4;

                // The three-component spaces a PDF output intent can plausibly carry. Lab, XYZ,
                // Luv and CMY are not device spaces and will not appear here from this library, but
                // a caller's profile is a caller's profile and answering 3 for them is right.
                case "RGB ":
                case "Lab ":
                case "XYZ ":
                case "Luv ":
                case "CMY ": return 3;
            }

            // ICC.1:2010 Table 19 also names an nCLR family for multi-channel devices: '2CLR'
            // through '9CLR' and then 'ACLR' through 'FCLR', the leading character spelling the
            // channel count in hex from 2 to 15. Falling back to the colour mode's 3-or-4 guess for
            // one of these is exactly the wrong /N this method exists to stop writing.
            if (space[1] == 'C' && space[2] == 'L' && space[3] == 'R')
            {
                var digit = space[0];
                if (digit is >= '2' and <= '9')
                    return digit - '0';
                if (digit is >= 'A' and <= 'F')
                    return digit - 'A' + 10;
            }
        }

        return mode == PdfColorMode.Cmyk ? 4 : 3;
    }

    /// <summary>Whether the caller supplied a profile of their own.</summary>
    static bool HasProfile(PdfDocumentOptions options) =>
        options.OutputIntentIccProfile != null && options.OutputIntentIccProfile.Length != 0;

    /// <summary>
    /// Whether the output condition identifier is still the placeholder nobody chose, which is what
    /// makes it safe to replace with the condition the built-in profile describes.
    /// </summary>
    static bool IsDefaultIdentifier(string identifier) =>
        string.IsNullOrEmpty(identifier)
        || identifier == PdfDocumentOptions.DefaultOutputIntentIdentifier;

    static bool IsListedIn(PdfArray associated, PdfFileSpecification attachment)
    {
        if (associated == null)
            return false;

        for (var idx = 0; idx < associated.Elements.Count; idx++)
        {
            if (ReferenceEquals(PdfAttachments.Resolve(associated.Elements[idx]), attachment))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Every file the document carries the bytes of, wherever it is listed: the catalog's
    /// association array, the <c>/EmbeddedFiles</c> name tree, and the file attachment annotations
    /// on its pages.
    /// </summary>
    /// <remarks>
    /// All three, because the rule is about the file being in the document and not about how it got
    /// there — and all three are walked by <see cref="PdfAttachments.Reachable"/>, which is the one
    /// place that knows where a document's files can be found, rather than by a second copy of that
    /// walk kept here. Looking only at the name tree — which is what this did — meant a document
    /// could carry an attachment on an annotation, claim PDF/A-1, and be told nothing: the one path
    /// a caller had for attaching a file before <see cref="PdfAttachments"/> existed was the one
    /// path the check could not see. What is left here is the one question that was never about
    /// reachability: a specification with no <c>/EF</c> is not counted, because it names a file
    /// somewhere else, and PDF/A objects to carrying bytes rather than to mentioning a filename.
    /// </remarks>
    static List<PdfFileSpecification> EmbeddedFiles(PdfDocument document)
    {
        var found = new List<PdfFileSpecification>();

        foreach (var specification in document.Attachments.Reachable(includeAnnotations: true))
        {
            if (specification.EmbeddedFile != null)
                found.Add(specification);
        }

        return found;
    }

    /// <summary>
    /// Builds the XMP packet from the information dictionary and hangs it off the catalog.
    /// </summary>
    static void AttachMetadata(PdfDocument document)
    {
        var metadata = XmpMetadata.FromDocument(document);

        document.InvokeMetadataContributors(metadata);

        // Set after the callback rather than before it. The conformance claim is what a validator
        // reads to decide which rules to hold the file to, and Options.Conformance is the one place
        // that decides it — a callback that could clear it, or set one the document was never
        // checked against, would be a way of writing a claim nothing stands behind. The same goes
        // for the accessibility claim, which XMP is the only place to make.
        metadata.Conformance = document.Options.Conformance;
        metadata.UAConformance = document.Options.UAConformance;

        var stream = new PdfDictionary(document);
        stream.Elements.SetName("/Type", "/Metadata");
        stream.Elements.SetName("/Subtype", "/XML");

        // Left uncompressed on purpose. The packet is meant to be findable by a tool that scans the
        // bytes without parsing the PDF around it, which is the whole reason it carries those
        // xpacket markers, and a compressed packet is invisible to one.
        stream.CreateStream(metadata.Build());

        document._irefTable.Add(stream);
        document.Catalog.Elements[PdfCatalog.Keys.Metadata] = stream.Reference;
    }

    /// <summary>
    /// Embeds the ICC profile and points the catalog's output intent at it.
    /// </summary>
    static void AttachOutputIntent(PdfDocument document)
    {
        var options = document.Options;

        // Enforce has already refused the modes for which there is no answer, so an unset profile
        // here means an RGB document that never said which RGB. Written rather than assigned back
        // onto Options: what a caller set is what a caller reads afterwards, and a save that
        // rewrites the document's own settings is a surprise nobody asked for.
        var supplied = HasProfile(options);
        var bytes = supplied ? options.OutputIntentIccProfile : PdfOutputIntents.SrgbProfile;

        // The identifier goes with the profile. A caller who named a condition but supplied no
        // profile keeps their name — they said something specific and this is not the place to
        // argue — but the default, which names nothing, gives way to the condition now known.
        var identifier = supplied || !IsDefaultIdentifier(options.OutputIntentIdentifier)
            ? options.OutputIntentIdentifier ?? "Custom"
            : PdfOutputIntents.SrgbIdentifier;

        var profile = new PdfDictionary(document);
        profile.Elements.SetInteger("/N", ComponentsOf(bytes, options.ColorMode));
        profile.CreateStream(bytes);
        document._irefTable.Add(profile);

        var intent = new PdfDictionary(document);
        intent.Elements.SetName(Keys.Type, "/OutputIntent");

        // /GTS_PDFA1 for every part of PDF/A, not only the first. The subtype names the family
        // rather than the part — a quirk of the standard that reads like a mistake and is not.
        intent.Elements.SetName(Keys.S, "/GTS_PDFA1");
        intent.Elements.SetString(Keys.OutputConditionIdentifier, identifier);
        intent.Elements[Keys.DestOutputProfile] = profile.Reference;
        document._irefTable.Add(intent);

        var intents = new PdfArray(document);
        intents.Elements.Add(intent.Reference);
        document.Catalog.Elements[PdfCatalog.Keys.OutputIntents] = intents;
    }

    /// <summary>
    /// The entries of an output intent dictionary.
    /// </summary>
    static class Keys
    {
        public const string Type = "/Type";
        public const string S = "/S";
        public const string OutputConditionIdentifier = "/OutputConditionIdentifier";
        public const string DestOutputProfile = "/DestOutputProfile";
    }
}
