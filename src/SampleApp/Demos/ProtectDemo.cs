using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Passwords, permissions, and reading a protected document back.
/// </summary>
/// <remarks>
///   The only demo whose own output is encrypted. Anything less would be a page about encryption
///   rather than an encrypted page, and the difference is the whole point of the demo.
/// </remarks>
internal sealed class ProtectDemo : PdfDemo
{
    #pragma warning disable S2068 // Not a credential: the demo prints both passwords on its own page so that its output can be opened.
    /// <summary>What a reader is asked for. Printed on the page as well as declared here.</summary>
    private const string ReaderPassword = "open-me";

    /// <summary>What lifts the restrictions. Never give this one to a reader.</summary>
    private const string OwnerPassword = "owner-only";
    #pragma warning restore S2068

    public ProtectDemo() : base() { }

    public override string Name => "Protect";

    public override string Summary => "An encrypted document: two passwords, eight permissions, and the way back in.";

    public override string OpenPassword => ReaderPassword;

    public override IReadOnlyList<string> Shows => [
        $"A genuinely encrypted PDF - it opens with the password '{ReaderPassword}'",
        "The two passwords and what each is for: one opens the file, the other lifts its limits",
        "All eight permission flags, set to a deliberately mixed set a reader's dialog will show",
        "PdfDocumentSecurityLevel - 40-bit and 128-bit RC4 are what this library writes",
        "Reading a protected document back, and which open modes each password permits",
        "PdfPasswordProvider, for when the password is not known before the file is opened",
        "HasOwnerPermissions, which says which of the two passwords a document was opened with"
    ];

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var document = new PdfDocument();
        document.Info.Title = "Protect";

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var label = new XFont("Liberation Sans", 9, XFontStyle.Bold);
        var body = new XFont("Liberation Sans", 9);
        var mono = new XFont("Source Code Pro", 8.5);

        // ----- page 1: what was asked for -----

        var page1 = document.AddPage();
        var gfx1 = XGraphics.FromPdfPage(page1);
        var prose1 = new XTextFormatter(gfx1);

        gfx1.DrawString("This document is encrypted", heading, XBrushes.Black, new XPoint(50, 60));

        prose1.DrawString(
            "Two passwords, and they are not interchangeable. The user password is what a reader "
            + "asks for before it will open the file at all. The owner password lifts every "
            + "restriction below - it is the one a viewer wants before it will let somebody change "
            + "the permissions - and a document that has one but no user password opens for anybody "
            + "and is still restricted.",
            body, XBrushes.Black, new XRect(50, 80, 495, 70));

        gfx1.DrawString("User password", label, XBrushes.Black, new XPoint(50, 165));
        gfx1.DrawString(ReaderPassword, mono, XBrushes.Firebrick, new XPoint(180, 165));
        gfx1.DrawString("Owner password", label, XBrushes.Black, new XPoint(50, 182));
        gfx1.DrawString(OwnerPassword, mono, XBrushes.Firebrick, new XPoint(180, 182));

        // The eight flags, deliberately mixed rather than all on or all off, so that a reader's
        // security dialog has something to disagree about.
        (string Name, bool Allowed, string What)[] permissions =
        [
            ("PermitPrint", true, "Print the document at all"),
            ("PermitFullQualityPrint", false, "Print it at full resolution rather than a draft"),
            ("PermitExtractContent", false, "Copy text and graphics out of it"),
            ("PermitAccessibilityExtractContent", true, "Extract for a screen reader, which is not the same permission"),
            ("PermitModifyDocument", false, "Change the content"),
            ("PermitAssembleDocument", true, "Insert, rotate or delete pages without changing them"),
            ("PermitAnnotations", true, "Add notes and markup"),
            ("PermitFormsFill", true, "Fill in form fields")
        ];

        gfx1.DrawString("Permissions", label, XBrushes.Black, new XPoint(50, 215));

        double y = 235;
        foreach (var permission in permissions)
        {
            gfx1.DrawString(permission.Allowed ? "allowed" : "refused", body,
                permission.Allowed ? XBrushes.SeaGreen : XBrushes.Firebrick, new XPoint(50, y));
            gfx1.DrawString(permission.Name, mono, XBrushes.Black, new XPoint(105, y));
            gfx1.DrawString(permission.What, body, XBrushes.DimGray, new XPoint(300, y));
            y += 16;
        }

        prose1.DrawString(
            "A permission is a request, not a lock. The flags travel in the encrypted document and "
            + "a conformant reader honours them; nothing stops a program that has the password from "
            + "ignoring them entirely. Encryption is what keeps the content from being read at all, "
            + "and that part is arithmetic rather than good manners.",
            body, XBrushes.Black, new XRect(50, y + 14, 495, 60));

        gfx1.DrawString("What this library writes", label, XBrushes.Black, new XPoint(50, y + 90));
        prose1.DrawString(
            "PdfDocumentSecurityLevel offers None, Encrypted40Bit and Encrypted128Bit, and 128-bit "
            + "RC4 is what this document uses. AES is implemented for reading - EncryptorFactory "
            + "answers an /AESV2 or /AESV3 crypt filter with an AES decryptor - so a document "
            + "somebody else encrypted with it opens here. Nothing writes AES yet.",
            body, XBrushes.Black, new XRect(50, y + 100, 495, 60));

        // ----- page 2: reading one back -----

        // A separate document, built with the same settings, saved to memory and read back. It is
        // the only way to show the reading half of the API on the same page as the writing half:
        // the document being built cannot be reopened until it has been saved, and by then the
        // demo has handed it over.
        string report;
        using (var buffer = new MemoryStream())
        {
            // docs:begin encrypt
            var sample = new PdfDocument();
            _ = sample.AddPage();
            sample.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            sample.SecuritySettings.UserPassword = ReaderPassword;
            sample.SecuritySettings.OwnerPassword = OwnerPassword;
            sample.SecuritySettings.PermitExtractContent = false;
            sample.SecuritySettings.PermitModifyDocument = false;
            sample.Save(buffer, false);
            // docs:end encrypt

            // docs:begin open-modes
            // The user password opens the file to be read. It does not open it to be changed:
            // PdfDocumentOpenMode.Modify with it is refused, by name, which is the library
            // enforcing the distinction between the two passwords rather than merely recording it.
            string refusal;
            try
            {
                buffer.Position = 0;
                using var _ = PdfReader.Open(buffer, ReaderPassword, PdfDocumentOpenMode.Modify);
                refusal = "Modify with the user password was allowed";
            }
            catch (PdfReaderException exception)
            {
                refusal = exception.Message;
            }

            buffer.Position = 0;
            using var asReader = PdfReader.Open(buffer, ReaderPassword, PdfDocumentOpenMode.ReadOnly);

            buffer.Position = 0;
            using var asOwner = PdfReader.Open(buffer, OwnerPassword, PdfDocumentOpenMode.Modify);
            // docs:end open-modes

            // HasOwnerPermissions is how a program finds out which password it was let in with,
            // and therefore whether it is entitled to change anything.
            report =
                $"User password, ReadOnly:  HasOwnerPermissions = {asReader.SecuritySettings.HasOwnerPermissions}\n"
                + $"Owner password, Modify:   HasOwnerPermissions = {asOwner.SecuritySettings.HasOwnerPermissions}\n"
                + $"User password, Modify:    refused - \"{refusal}\"\n"
                + $"PermitExtractContent read back as {asOwner.SecuritySettings.PermitExtractContent}\n"
                + $"PermitModifyDocument read back as {asOwner.SecuritySettings.PermitModifyDocument}\n"
                + $"PermitPrint read back as {asOwner.SecuritySettings.PermitPrint}";
        }

        var page2 = document.AddPage();
        var gfx2 = XGraphics.FromPdfPage(page2);
        var prose2 = new XTextFormatter(gfx2);

        gfx2.DrawString("Reading a protected document", heading, XBrushes.Black, new XPoint(50, 60));

        prose2.DrawString(
            "There are two ways in. PdfReader.Open takes the password directly when the caller "
            + "already has it, and takes a PdfPasswordProvider when it does not - the provider is "
            + "called only if the document turns out to need one, which is how a viewer knows to "
            + "put a dialog up. A wrong password throws PdfReaderException rather than returning "
            + "an empty document.",
            body, XBrushes.Black, new XRect(50, 80, 495, 70));

        gfx2.DrawString("What came back", label, XBrushes.Black, new XPoint(50, 165));

        double line = 185;
        foreach (var row in report.Split('\n'))
        {
            gfx2.DrawString(row, mono, XBrushes.Black, new XPoint(50, line));
            line += 15;
        }

        gfx2.DrawString("The password provider", label, XBrushes.Black, new XPoint(50, line + 20));

        // Demonstrated rather than described: the provider is asked for a password only because
        // the document has one, and args.Abort is how a caller says the user gave up.
        var timesAsked = 0;
        using (var buffer = new MemoryStream())
        {
            var sample = new PdfDocument();
            _ = sample.AddPage();
            sample.SecuritySettings.UserPassword = ReaderPassword;
            sample.Save(buffer, false);

            // docs:begin password-provider
            buffer.Position = 0;
            using var reopened = PdfReader.Open(buffer, PdfDocumentOpenMode.Modify,
                args =>
                {
                    timesAsked++;
                    args.Password = ReaderPassword;
                });
            // docs:end password-provider

            gfx2.DrawString(
                $"The provider was called {timesAsked} time(s), and the document opened with "
                + $"{reopened.PageCount} page(s).",
                body, XBrushes.Black, new XPoint(50, line + 40));
        }

        prose2.DrawString(
            "Setting a user password is what makes a document encrypted; DocumentSecurityLevel "
            + "follows from it. Setting an owner password alone leaves the file readable by anyone "
            + "and still restricted, which is the arrangement most 'protected' PDFs in the world "
            + "actually use - and the reason so many of them can be opened without a password and "
            + "still refuse to print.",
            body, XBrushes.Black, new XRect(50, line + 70, 495, 70));

        // Set last, on the document that is about to be handed back and saved. The settings are
        // read at save time, so where in the build they are set makes no difference - but keeping
        // them next to each other is what makes them readable.
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.UserPassword = ReaderPassword;
        document.SecuritySettings.OwnerPassword = OwnerPassword;

        foreach (var permission in permissions)
        {
            switch (permission.Name)
            {
                case "PermitPrint":
                    document.SecuritySettings.PermitPrint = permission.Allowed;
                    break;
                case "PermitFullQualityPrint":
                    document.SecuritySettings.PermitFullQualityPrint = permission.Allowed;
                    break;
                case "PermitExtractContent":
                    document.SecuritySettings.PermitExtractContent = permission.Allowed;
                    break;
                case "PermitAccessibilityExtractContent":
                    document.SecuritySettings.PermitAccessibilityExtractContent = permission.Allowed;
                    break;
                case "PermitModifyDocument":
                    document.SecuritySettings.PermitModifyDocument = permission.Allowed;
                    break;
                case "PermitAssembleDocument":
                    document.SecuritySettings.PermitAssembleDocument = permission.Allowed;
                    break;
                case "PermitAnnotations":
                    document.SecuritySettings.PermitAnnotations = permission.Allowed;
                    break;
                case "PermitFormsFill":
                    document.SecuritySettings.PermitFormsFill = permission.Allowed;
                    break;
                default:
                    throw new InvalidOperationException($"No setter for {permission.Name}.");
            }
        }
        #endregion

        return document;
    }
}
