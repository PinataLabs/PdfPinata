using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Signing a document, and then asking the two questions a signature actually answers: does it
///   verify over the bytes it covers, and are those bytes the whole file.
/// </summary>
/// <remarks>
///   The one demo whose output cannot be produced by saving the document it builds - see
///   <see cref="Save"/>. Everything the base class does to write a PDF is exactly what destroys a
///   signature.
/// </remarks>
internal sealed class SigningDemo : PdfDemo
{
    public SigningDemo() : base() { }

    public override string Name => "Signing";

    public override string Summary => "A signed PDF, the hole its signature lives in, and what verifying it proves.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "PdfSigner.Sign - the placeholder, the byte range, and the patch that fills the hole",
        "Pkcs7Signer from PdfPinata.Signing, with PAdES and PKCS#7 as separate formats",
        "PdfSignatureOptions - a visible appearance drawn by the caller, and a reason and location",
        "That the revision is appended rather than rewritten, because rewriting invalidates",
        "PdfSignatures.InDocument, which reads a signature without believing any of it",
        "PdfSignatureVerifier - IsIntact and CoversWholeDocument, and why both are needed"
    };

    public override int PageCount => 3;

    /// <summary>
    ///   The signing certificate: generated here, self-signed, and trusted by nothing.
    /// </summary>
    /// <remarks>
    ///   Made rather than carried, for the reason the test suite makes its own: a checked-in
    ///   certificate expires, and then the sample fails one morning for a reason nobody changed.
    ///   Nothing about this demo depends on the certificate being believed - a reader will show a
    ///   yellow warning over this file, correctly, because the signature is intact and the identity
    ///   behind it is nobody's.
    ///   <para>
    ///     Held for one run and no longer. It is made during <c>Build</c>, so that the pages can
    ///     print what a real signature said, and used again in <c>Save</c> - and then let go, because
    ///     it carries a private key and a native handle to go with it. A demo registry holds one
    ///     instance of every demo for the life of the process, which is why this is cleared rather
    ///     than merely disposed: a second run makes a fresh certificate instead of signing with a
    ///     disposed one.
    ///   </para>
    /// </remarks>
    private X509Certificate2? _certificate;

    private X509Certificate2 Certificate() => _certificate ??= SelfSigned();

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var heading = new XFont(BundledFontResolver.SansFamily, 16, XFontStyle.Bold);
        var label = new XFont(BundledFontResolver.SansFamily, 9.5, XFontStyle.Bold);
        var body = new XFont(BundledFontResolver.SansFamily, 9);
        var mono = new XFont(BundledFontResolver.MonoFamily, 8);

        var document = new PdfDocument();
        document.Info.Title = "Signing";
        document.Info.Author = "PdfPinata sample app";

        // ----- page one: the document that gets signed ---------------------------------------------

        var first = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(first))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("Statement of agreement", heading, XBrushes.Black, 50, 70);

            prose.DrawString(
                "This page stands in for whatever it is that needed signing. Everything below the "
                + "rule is about the signature rather than the agreement, and the signature itself "
                + "is in the box at the foot of this page - drawn by this demo, because a signature "
                + "appearance is decoration the caller supplies and not something the library "
                + "invents.",
                body, XBrushes.Black, new XRect(50, 92, 495, 60));

            gfx.DrawLine(new XPen(XColors.Gainsboro, 0.8), 50, 165, 545, 165);

            prose.DrawString(
                "A PDF signature is a chicken-and-egg problem the format solves by cheating. The "
                + "signature covers the file and the signature is in the file, so the bytes to be "
                + "hashed cannot be known until the signature is written, and the signature cannot "
                + "be computed until they are.",
                body, XBrushes.Black, new XRect(50, 185, 495, 48));

            prose.DrawString(
                "The way out is to write the file with a hole of a known size where the signature "
                + "will go, and a /ByteRange saying \"everything except that hole\" - then compute "
                + "the signature over what was written and patch it into the hole without moving a "
                + "single byte. Every field involved is written at a fixed width, and that is what "
                + "makes the second pass a patch rather than a re-layout.",
                body, XBrushes.Black, new XRect(50, 243, 495, 62));

            gfx.DrawString("What is signed, and what is not", label, XBrushes.Black, 50, 325);

            prose.DrawString(
                "The two spans either side of the hole, which between them are the whole file. A "
                + "signature is entitled to cover only part of one, and a signature covering only "
                + "part of a file is precisely how a document gets altered without the alteration "
                + "showing - so \"does it verify\" and \"does it cover everything\" are two "
                + "questions and both have to be answered yes.",
                body, XBrushes.Black, new XRect(50, 340, 495, 62));

            gfx.DrawString("The revision is appended, never rewritten", label, XBrushes.Black, 50, 420);

            prose.DrawString(
                "Not an optimisation. A signature covers a byte range of the file, so rewriting the "
                + "file invalidates it - and rewriting is exactly what Save does. Signing therefore "
                + "goes through SaveIncremental, and a document already signed keeps every earlier "
                + "signature intact. This is also why this demo overrides how it is written out: the "
                + "base class saves the document it was handed, and doing that here would throw the "
                + "signature away.",
                body, XBrushes.Black, new XRect(50, 435, 495, 76));

            // The rectangle the appearance is drawn into is declared in the options, in the same
            // coordinates XGraphics uses. What goes inside it is drawn by the callback below and is
            // decoration: drawing the word "signed" does not sign anything.
            gfx.DrawRectangle(new XPen(XColors.Gainsboro, 0.8), SignatureBox);
            gfx.DrawString("The signature appearance is drawn into this box",
                new XFont(BundledFontResolver.SansFamily, 7.5), XBrushes.Gray,
                SignatureBox.X, SignatureBox.Y - 6);
        }

        // ----- pages two and three: what the signing actually produced -----------------------------

        // Signed here as a rehearsal, so that the pages below can report real numbers. A document
        // cannot describe its own signature - the description would change the bytes the signature
        // covers - so this signs a copy of page one alone and reports what came back. The file this
        // demo writes is signed the same way, by the same signer, with the same options.
        var rehearsed = Rehearse();

        var second = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(second))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What the signature says", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "Read back with PdfSignatures.InDocument, which walks the interactive form's field "
                + "tree and reports what it finds. Nothing here has been checked - the name, the "
                + "reason and the time are text the producer wrote, and are evidence of nothing on "
                + "their own.",
                body, XBrushes.Black, new XRect(50, 80, 495, 44));

            double y = 140;
            foreach (var fact in rehearsed.Said)
            {
                gfx.DrawString(fact.Field, label, XBrushes.Black, 50, y);
                gfx.DrawString(fact.Value, mono, XBrushes.Black, 205, y);
                y += 16;
            }

            gfx.DrawString("PAdES or PKCS#7", label, XBrushes.Black, 50, y + 18);

            prose.DrawString(
                "The /SubFilter is what says how to read /Contents, and it belongs to the signer "
                + "rather than to the writer. PAdES - /ETSI.CAdES.detached - hashes the signing "
                + "certificate into the signed attributes, which closes a real hole: without it the "
                + "certificate is merely carried alongside the signature, and an attacker who can "
                + "find a second certificate whose key verifies the same signature can swap it in "
                + "and change who the document appears to have been signed by.",
                body, XBrushes.Black, new XRect(50, y + 32, 495, 76));

            gfx.DrawString("The time is not evidence", label, XBrushes.Black, 50, y + 120);

            prose.DrawString(
                "It is the producer's own clock, and a reader will say so. Making the time of "
                + "signing provable needs a timestamp token from a time-stamping authority - PAdES "
                + "B-T - and that is not implemented. PAdES also asks that the CMS signing-time "
                + "attribute be left out when /M carries the claim, because two claimed times that "
                + "can disagree help nobody, so Pkcs7Signer leaves it out for PAdES and puts it in "
                + "for PKCS#7.",
                body, XBrushes.Black, new XRect(50, y + 134, 495, 76));

            gfx.DrawString("Certifying, rather than approving", label, XBrushes.Black, 50, y + 222);

            prose.DrawString(
                "PdfSignatureOptions.Certification turns the signature into a /DocMDP one, which "
                + "declares what a later revision is still allowed to do - nothing, form filling, or "
                + "form filling and annotation. An ordinary signature says \"I signed this\"; a "
                + "certifying one says \"and here is what may still happen to it\".",
                body, XBrushes.Black, new XRect(50, y + 236, 495, 58));
        }

        var third = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(third))
        {
            var prose = new XTextFormatter(gfx);

            gfx.DrawString("What verifying it proves", heading, XBrushes.Black, 50, 60);

            prose.DrawString(
                "PdfSignatureVerifier answers two questions and refuses to conflate them. IsIntact "
                + "says the signature verifies over the bytes it covers. CoversWholeDocument says "
                + "those bytes are the whole file but for the signature itself. A signature over the "
                + "first page of a five page document is perfectly intact, and reporting only that "
                + "would report the document as sound when a reader would not.",
                body, XBrushes.Black, new XRect(50, 80, 495, 62));

            double y = 160;
            foreach (var fact in rehearsed.Verified)
            {
                gfx.DrawString(fact.Field, label, XBrushes.Black, 50, y);
                gfx.DrawString(fact.Value, mono, XBrushes.Black, 205, y);
                y += 16;
            }

            gfx.DrawString("Integrity checking, not validation", label, XBrushes.Firebrick, 50, y + 20);

            prose.DrawString(
                "No certificate chain is built, no trust store is consulted, no revocation is "
                + "checked and no timestamp is evaluated - all of which a reader showing a green "
                + "tick has done. A signature reported valid here may have been made with a "
                + "certificate nobody should trust, and the one on this file was: it is self-signed "
                + "by a key this demo generated a moment ago and threw away.",
                body, XBrushes.Black, new XRect(50, y + 34, 495, 62));

            gfx.DrawString("It is still the check that catches things", label, XBrushes.Black, 50, y + 108);

            prose.DrawString(
                "A document edited after signing, a signature covering only part of the file, a "
                + "producer that got its byte range wrong - those are what actually goes wrong, and "
                + "all three are questions about bytes rather than about who is believed.",
                body, XBrushes.Black, new XRect(50, y + 122, 495, 48));

            gfx.DrawString("Where the split is", label, XBrushes.Black, 50, y + 182);

            prose.DrawString(
                "The core package holds all the PDF machinery - the placeholder, the byte range, the "
                + "patching - and no cryptography at all, behind the IPdfSigner seam. "
                + "PdfPinata.Signing is the package that carries a dependency the core refuses, "
                + "and it is the one shipped package that does not target netstandard2.1. A signer "
                + "of your own - a smart card, an HSM, a remote signing service - implements "
                + "IPdfSigner and needs neither.",
                body, XBrushes.Black, new XRect(50, y + 196, 495, 76));
        }
        #endregion

        return document;
    }

    /// <summary>
    ///   Signs the built document into the file, rather than saving it there.
    /// </summary>
    /// <remarks>
    ///   <c>Save</c> writes a file afresh from the object model, which renumbers the objects and
    ///   drops the bytes the document was read from - so a document saved after being signed carries
    ///   a signature over a file that no longer exists. Signing needs the opposite: the original
    ///   bytes kept exactly, with a revision appended. So the document is saved to memory, opened
    ///   again for appending, and signed on the way out.
    /// </remarks>
    protected override void Save(PdfDocument document, string path)
    {
        using var unsigned = new MemoryStream();
        document.Save(unsigned, false);
        unsigned.Position = 0;

        try
        {
            using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
            PdfSigner.Sign(unsigned, output, Signer(), OptionsFor(drawAppearance: true));
        }
        finally
        {
            // The last use of the certificate, and so where the native key handle behind it is let
            // go of. SelfSigned disposes the ephemeral certificate for exactly this reason; the one
            // it hands back carries an imported key and has earned the same treatment.
            _certificate?.Dispose();
            _certificate = null;
        }
    }

    /// <summary>Where the visible signature goes, in the coordinates XGraphics draws in.</summary>
    private static XRect SignatureBox => new(50, 640, 230, 70);

    /// <summary>
    ///   Signs a small document with the same signer and options, and reads the result back, so that
    ///   the pages above can print numbers rather than descriptions of numbers.
    /// </summary>
    private Rehearsal Rehearse()
    {
        using var probe = new PdfDocument();
        probe.Info.Title = "A rehearsal";
        _ = probe.AddPage();

        byte[] signed;
        using (var unsigned = new MemoryStream())
        {
            probe.Save(unsigned, false);
            unsigned.Position = 0;

            using var output = new MemoryStream();

            // No appearance here. The probe has nothing drawn on it to put one beside, and an
            // invisible signature is a real field covering the whole document all the same - which
            // is what most machine-applied signatures are.
            PdfSigner.Sign(unsigned, output, Signer(), OptionsFor(drawAppearance: false));
            signed = output.ToArray();
        }

        var checkedSignature = PdfSignatureVerifier.Verify(signed)[0];
        var said = checkedSignature.Signature;

        // The byte range is four numbers: start, length, start, length. The gap between the end of
        // the first span and the start of the second is the hole the signature sits in.
        var holeStart = said.ByteRange[0] + said.ByteRange[1];
        var holeEnd = said.ByteRange[2];

        return new Rehearsal(
            new[]
            {
                ("Field name", said.FieldName),
                ("/SubFilter", said.SubFilter),
                ("Signer name", said.SignerName ?? "(not recorded)"),
                ("Reason", said.Reason ?? "(not recorded)"),
                ("Location", said.Location ?? "(not recorded)"),
                ("Claimed time", said.SigningTime?.ToString("u", CultureInfo.InvariantCulture) ?? "(none)"),
                ("Certification level", said.CertificationLevel == 0
                    ? "0 - an approval signature, not a certifying one"
                    : said.CertificationLevel.ToString(CultureInfo.InvariantCulture)),
                ("/ByteRange", string.Join(" ", said.ByteRange)),
                ("The hole", Format(holeStart) + " to " + Format(holeEnd)
                    + " - " + Format(holeEnd - holeStart) + " bytes reserved"),
                ("Signature written", Format(said.Contents.Length) + " bytes, padded with zeros"),
                ("File signed", Format(signed.Length) + " bytes")
            },
            new[]
            {
                ("IsIntact", checkedSignature.IsIntact ? "true" : "false"),
                ("CoversWholeDocument", checkedSignature.CoversWholeDocument ? "true" : "false"),
                ("IsValid", checkedSignature.IsValid ? "true - both of the above" : "false"),
                ("Problem", checkedSignature.Problem ?? "(none)"),
                ("Certificate subject", checkedSignature.SignerCertificate?.Subject ?? "(none embedded)"),
                ("Certificate expires", checkedSignature.SignerCertificate?.NotAfter
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-"),
                ("Digest", "SHA-256; SHA-1 and MD5 are refused by the signer")
            });
    }

    private Pkcs7Signer Signer() => new(Certificate());

    private static PdfSignatureOptions OptionsFor(bool drawAppearance)
    {
        var options = new PdfSignatureOptions
        {
            FieldName = "Signature1",
            SignerName = "PdfPinata sample app",
            Reason = "To demonstrate what a signed PDF contains",
            Location = "The sample app's output directory",
            ContactInfo = "https://github.com/PinataLabs/PdfPinata"
        };

        if (!drawAppearance)
            return options;

        options.PageIndex = 0;
        options.Rectangle = SignatureBox;

        // Called with a surface the size of the rectangle, whose own origin is its top left corner.
        // What is drawn here is decoration and is not what a reader validates.
        options.DrawAppearance = (gfx, area) =>
        {
            var name = new XFont(BundledFontResolver.SansFamily, 10, XFontStyle.Bold);
            var detail = new XFont(BundledFontResolver.SansFamily, 7);

            gfx.DrawRectangle(XBrushes.WhiteSmoke, area);
            gfx.DrawString("Signed by the sample app", name, XBrushes.Black, 8, 18);
            gfx.DrawString("Self-signed. Trusted by nothing.", detail, XBrushes.Firebrick, 8, 32);
            gfx.DrawString("The tick a reader shows is about the certificate,", detail, XBrushes.DimGray, 8, 45);
            gfx.DrawString("not about the drawing in this box.", detail, XBrushes.DimGray, 8, 55);
        };

        return options;
    }

    private static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A self-signed certificate with a usable private key.</summary>
    private static X509Certificate2 SelfSigned()
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=PdfPinata Sample App, O=Not a real signer",
            key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        // Disposed once it has been exported. It is the one carrying the ephemeral key the round
        // trip below exists to get rid of, and holding a native key handle open for the life of the
        // process to no purpose is how a demo teaches a habit worth not having.
        using var ephemeral = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        // Round-tripped through PKCS#12 on purpose. A certificate straight out of CreateSelfSigned
        // carries an ephemeral key on Windows, and CMS signing goes looking for that key in a store
        // it was never put in - "Keyset does not exist", from a certificate that plainly has one.
        #pragma warning disable S2068 // Not a credential: it protects a throwaway self-signed certificate for the length of this method.
        const string password = "pdfpinata";
        #pragma warning restore S2068
        return new X509Certificate2(
            ephemeral.Export(X509ContentType.Pfx, password), password,
            X509KeyStorageFlags.Exportable);
    }

    /// <summary>What the rehearsal found, ready to be printed as two tables.</summary>
    private sealed class Rehearsal
    {
        public Rehearsal((string Field, string Value)[] said, (string Field, string Value)[] verified)
        {
            Said = said;
            Verified = verified;
        }

        public (string Field, string Value)[] Said { get; }

        public (string Field, string Value)[] Verified { get; }
    }
}
