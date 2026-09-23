#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

namespace PdfPinata.Pdf.Security;

/// <summary>
/// Encapsulates access to the security settings of a PDF document.
/// </summary>
public sealed class PdfSecuritySettings
{
    internal PdfSecuritySettings(PdfDocument document)
    {
        _document = document;
    }
    private readonly PdfDocument _document;

    /// <summary>
    /// Indicates whether the granted access to the document is 'owner permission'. Returns true if the document
    /// is unprotected or was opened with the owner password. Returns false if the document was opened with the
    /// user password.
    /// </summary>
    public bool HasOwnerPermissions => _hasOwnerPermissions;

    internal bool _hasOwnerPermissions = true;

    /// <summary>
    /// Gets or sets the document security level. If you set the security level to anything but PdfDocumentSecurityLevel.None
    /// you must also set a user and/or an owner password. Otherwise saving the document will fail.
    /// </summary>
    public PdfDocumentSecurityLevel DocumentSecurityLevel { get; set; }

    /// <summary>
    /// Sets the user password of the document. Setting a password automatically sets the
    /// PdfDocumentSecurityLevel to PdfDocumentSecurityLevel.Encrypted128Bit if its current
    /// value is PdfDocumentSecurityLevel.None.
    /// </summary>
    public string UserPassword
    {
        set => SecurityHandler.UserPassword = value;
    }

    /// <summary>
    /// Sets the owner password of the document. Setting a password automatically sets the
    /// PdfDocumentSecurityLevel to PdfDocumentSecurityLevel.Encrypted128Bit if its current
    /// value is PdfDocumentSecurityLevel.None.
    /// </summary>
    public string OwnerPassword
    {
        set => SecurityHandler.OwnerPassword = value;
    }

    /// <summary>
    /// Determines whether the document can be saved.
    /// </summary>
    internal PdfSaveCheck CanSave()
    {
        if (DocumentSecurityLevel != PdfDocumentSecurityLevel.None
            && string.IsNullOrEmpty(SecurityHandler._userPassword)
            && string.IsNullOrEmpty(SecurityHandler._ownerPassword))
        {
            return PdfSaveCheck.Refused(PSSR.UserOrOwnerPasswordRequired);
        }

        return PdfSaveCheck.Allowed;
    }

    // The Permit* properties are the user access permission bits of the /P entry, ISO 32000-1
    // Table 22. Bits 9 to 12 are honoured only by revision 3 or later of the standard security
    // handler, which is what Encrypted128Bit writes; Encrypted40Bit writes revision 2, and there
    // those four bits are always set, whatever their properties say.
    #region Permissions

    /// <summary>
    /// Permits printing the document (bit 3). With 128-bit encryption the quality it may be printed
    /// at also depends on <see cref="PermitFullQualityPrint"/>.
    /// </summary>
    public bool PermitPrint
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitPrint) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitPrint;
            else
                permission &= ~PdfUserAccessPermission.PermitPrint;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits modifying the contents of the document by operations other than those controlled
    /// by <see cref="PermitAnnotations"/>, <see cref="PermitFormsFill"/> and
    /// <see cref="PermitAssembleDocument"/> (bit 4).
    /// </summary>
    public bool PermitModifyDocument
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitModifyDocument) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitModifyDocument;
            else
                permission &= ~PdfUserAccessPermission.PermitModifyDocument;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits copying or otherwise extracting text and graphics from the document (bit 5). With
    /// 40-bit encryption this includes extraction for accessibility; with 128-bit encryption that
    /// is <see cref="PermitAccessibilityExtractContent"/> instead.
    /// </summary>
    public bool PermitExtractContent
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitExtractContent) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitExtractContent;
            else
                permission &= ~PdfUserAccessPermission.PermitExtractContent;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits adding or modifying text annotations and filling in interactive form fields, and,
    /// together with <see cref="PermitModifyDocument"/>, creating or modifying form fields,
    /// signature fields included (bit 6).
    /// </summary>
    public bool PermitAnnotations
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitAnnotations) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitAnnotations;
            else
                permission &= ~PdfUserAccessPermission.PermitAnnotations;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits filling in existing interactive form fields, signature fields included, even when
    /// <see cref="PermitAnnotations"/> is not set (bit 9; 128-bit encryption only).
    /// </summary>
    public bool PermitFormsFill
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitFormsFill) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitFormsFill;
            else
                permission &= ~PdfUserAccessPermission.PermitFormsFill;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits extracting text and graphics in support of accessibility to users with disabilities
    /// or for other purposes (bit 10; 128-bit encryption only).
    /// </summary>
    public bool PermitAccessibilityExtractContent
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitAccessibilityExtractContent) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitAccessibilityExtractContent;
            else
                permission &= ~PdfUserAccessPermission.PermitAccessibilityExtractContent;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits assembling the document - inserting, rotating or deleting pages and creating
    /// bookmarks or thumbnail images - even when <see cref="PermitModifyDocument"/> is not set
    /// (bit 11; 128-bit encryption only).
    /// </summary>
    public bool PermitAssembleDocument
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitAssembleDocument) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitAssembleDocument;
            else
                permission &= ~PdfUserAccessPermission.PermitAssembleDocument;
            SecurityHandler.Permission = permission;
        }
    }

    /// <summary>
    /// Permits printing the document faithfully from its digital representation (bit 12; 128-bit
    /// encryption only). When it is not set but <see cref="PermitPrint"/> is, printing is limited
    /// to a low-level, possibly degraded representation.
    /// </summary>
    public bool PermitFullQualityPrint
    {
        get => (SecurityHandler.Permission & PdfUserAccessPermission.PermitFullQualityPrint) != 0;
        set
        {
            var permission = SecurityHandler.Permission;
            if (value)
                permission |= PdfUserAccessPermission.PermitFullQualityPrint;
            else
                permission &= ~PdfUserAccessPermission.PermitFullQualityPrint;
            SecurityHandler.Permission = permission;
        }
    }
    #endregion

    /// <summary>
    /// PdfStandardSecurityHandler is the only implemented handler.
    /// </summary>
    internal PdfStandardSecurityHandler SecurityHandler => _document._trailer.SecurityHandler;
}
