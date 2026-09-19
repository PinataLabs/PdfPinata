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

using System;
using System.Resources;
using System.Reflection;
using PdfPinata.Drawing;
using PdfPinata.Internal;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;


namespace PdfPinata;

/// <summary>
/// The Pdf-Sharp-String-Resources.
/// </summary>
// ReSharper disable once InconsistentNaming
static class PSSR
{
    // How to use:
    // Create a function or property for each message text, depending on how many parameters are
    // part of the message. For the beginning, type plain English text in the function or property.
    // The use of functions is safe when a parameter must be changed. The compiler tells you all
    // places in your code that must be modified.
    // For localization, create an enum value for each function or property with the same name. Then
    // create localized message files with the enum values as messages identifiers. In the properties
    // and functions all text is replaced by Format or GetString functions with the corresponding enum value
    // as first parameter. The use of enums ensures that typing errors in message resource names are
    // simply impossible.

    #region Helper functions
    /// <summary>
    /// Loads the message from the resource associated with the enum type and formats it
    /// using 'String.Format'. Because this function is intended to be used during error
    /// handling it never raises an exception.
    /// </summary>
    /// <param name="id">The type of the parameter identifies the resource
    /// and the name of the enum identifies the message in the resource.</param>
    /// <param name="args">Parameters passed through 'String.Format'.</param>
    /// <returns>The formatted message.</returns>
    public static string Format(PSMsgID id, params object[] args)
    {
        string message;
        try
        {
            message = GetString(id);
            message = message != null ? Format(message, args) : "INTERNAL ERROR: Message not found in resources.";
            return message;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            message = $"UNEXPECTED ERROR while formatting message with ID {id}: {ex}";
        }
        return message;
    }

    public static string Format(string format, params object[] args)
    {
        ArgumentNullException.ThrowIfNull(format);

        string message;
        try
        {
            message = String.Format(format, args);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            message = $"UNEXPECTED ERROR while formatting message '{format}': {ex}";
        }
        return message;
    }

    /// <summary>
    /// Gets the localized message identified by the specified DomMsgID.
    /// </summary>
    public static string GetString(PSMsgID id)
    {
        return ResMngr.GetString(id.ToString());
    }

    #endregion

    #region General messages

    public static string IndexOutOfRange => "The index is out of range.";

    public static string ListEnumCurrentOutOfRange => "Enumeration out of range.";

    public static string PageIndexOutOfRange => "The index of a page is out of range.";

    public static string OutlineIndexOutOfRange => "The index of an outline is out of range.";

    public static string SetValueMustNotBeNull => "The set value property must not be null.";

    public static string InvalidValue(int val, string name, int min, int max)
    {
        return Format("{0} is not a valid value for {1}. {1} should be greater than or equal to {2} and less than or equal to {3}.",
            val, name, min, max);
    }

    public static string ObsoleteFunktionCalled => "The function is obsolete and must not be called.";

    public static string OwningDocumentRequired => "The PDF object must belong to a PdfDocument, but property Document is null.";

    public static string FileNotFound(string path)
    {
        return Format("The file '{0}' does not exist.", path);
    }

    public static string FontDataReadOnly => "Font data is read-only.";

    public static string ErrorReadingFontData => "Error while parsing an OpenType font.";

    #endregion

    #region XGraphics specific messages

    // ----- XGraphics ----------------------------------------------------------------------------

    public static string PointArrayEmpty => "The PointF array must not be empty.";

    public static string PointArrayAtLeast(int count)
    {
        return Format("The point array must contain {0} or more points.", count);
    }

    public static string NeedPenOrBrush => "XPen or XBrush or both must not be null.";

    public static string CannotChangeImmutableObject(string typename)
    {
        return $"You cannot change this immutable {typename} object.";
    }

    public static string FontAlreadyAdded(string fontname)
    {
        return $"Fontface with the name '{fontname}' already added to font collection.";
    }

    public static string NotImplementedForFontsRetrievedWithFontResolver(string name)
    {
        return $"Not implemented for font '{name}', because it was retrieved with font resolver.";
    }

    #endregion

    #region PDF specific messages

    // ----- PDF ----------------------------------------------------------------------------------

    public static string InvalidPdf => "The file is not a valid PDF document.";

    public static string InvalidVersionNumber => "Invalid version number. Valid values are 12 to 17 and 20.";

    public static string CannotHandleXRefStreams => "Cannot handle cross-reference streams. The current implementation of PDFsharp cannot handle this PDF feature introduced with Acrobat 6.";

    public static string PasswordRequired => "A password is required to open the PDF document.";

    public static string InvalidPassword => "The specified password is invalid.";

    public static string OwnerPasswordRequired => "To modify the document the owner password is required";

    public static string UserOrOwnerPasswordRequired => GetString(PSMsgID.UserOrOwnerPasswordRequired);

    //get { return "At least a user or an owner password is required to encrypt the document."; }

    /// <summary>
    /// Refuses an operation that would change a document that was not opened for changing, naming
    /// both the mode it was opened with and the modes the operation needs.
    /// </summary>
    /// <param name="operation">
    /// What the caller was trying to do, as a gerund phrase - "adding a page", "saving the
    /// document". It is read as the middle of a sentence.
    /// </param>
    /// <param name="openMode">The mode the document was actually opened with.</param>
    /// <remarks>
    /// This used to be a constant string reading "The document cannot be modified.", which named
    /// neither mode and so left the caller to find both in the source. Naming them is the whole
    /// point of the refusal: the mistake is nearly always at the call to
    /// <see cref="PdfPinata.Pdf.IO.PdfReader"/>.Open, not at the operation that reports it.
    /// </remarks>
    public static string CannotModify(string operation, PdfDocumentOpenMode openMode)
    {
        return
            $"This document was opened with PdfDocumentOpenMode.{openMode} and {operation} needs a document opened " +
            "with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.";
    }

    /// <summary>
    /// Refuses an operation that a certifying signature's <c>/DocMDP</c> level does not permit, naming
    /// the level rather than the open mode — the other reason <see cref="CannotModify"/> refuses, and
    /// the point of having two messages rather than one is so a caller can tell which of them applies.
    /// </summary>
    public static string CertificationForbids(string operation, PdfCertificationLevel level)
    {
        return $"This document was certified with PdfCertificationLevel.{level} and {operation} is not permitted by " +
               "that certification.";
    }

    public static string NameMustStartWithSlash =>
        //get { return GetString(PSMsgID.NameMustStartWithSlash); }
        "A PDF name must start with a slash (/).";

    public static string ImportPageNumberOutOfRange(int pageNumber, int maxPage, string path)
    {
        return String.Format("The page cannot be imported from document '{2}', because the page number is out of range. " +
                             "The specified page number is {0}, but it must be in the range from 1 to {1}.", pageNumber, maxPage, path);
    }

    public static string MultiplePageInsert => "The page cannot be added to this document because the document already owned this page.";

    /// <summary>
    /// Explains that a page is already placed in this document, and names the operation the
    /// caller most likely wanted instead.
    /// </summary>
    /// <param name="currentIndex">The index the page already sits at.</param>
    /// <param name="requestedIndex">The index the caller asked to place it at.</param>
    public static string PageAlreadyPlaced(int currentIndex, int requestedIndex)
    {
        return String.Format(
            "This page is already at index {0} of this document, so it cannot be placed again. " +
            "To move it, use document.MovePage({0}, {1}). " +
            "To place a second, independent page showing the same content, use document.DuplicatePage({0}, {1}). " +
            "If this page came from AddPage() or InsertPage(), it was already placed by that call - " +
            "draw on the page those methods returned instead of placing it a second time. " +
            "To build a page before placing it, use 'new PdfPage(document)', which creates a drawable " +
            "page without adding it to the page tree.",
            currentIndex, requestedIndex);
    }

    /// <summary>
    /// Explains that a page belongs to another document and must be imported rather than placed.
    /// </summary>
    public static string PageBelongsToAnotherDocument => "This page belongs to another document. Use document.ImportPage(index, page) to " +
                                                         "copy it into this document; the copy it returns is a different object from the " +
                                                         "page passed in. PlacePage only accepts pages this document already owns.";

    /// <summary>
    /// Explains that a page belongs to this document already and so cannot be imported.
    /// </summary>
    public static string PageBelongsToThisDocument => "This page already belongs to this document, so there is nothing to import. " +
                                                      "Use document.PlacePage to position a page this document owns but has not placed, " +
                                                      "or document.DuplicatePage to add a second page showing the same content.";

    public static string UnexpectedTokenInPdfFile => "Unexpected token in PDF file. The PDF file may be corrupt. If it is not, please send us the file for service.";

    public static string InappropriateColorSpace(PdfColorMode colorMode, XColorSpace colorSpace)
    {
        string mode;
        switch (colorMode)
        {
            case PdfColorMode.Rgb:
                mode = "RGB";
                break;

            case PdfColorMode.Cmyk:
                mode = "CMYK";
                break;

            default:
                mode = "(undefined)";
                break;
        }

        string space;
        switch (colorSpace)
        {
            case XColorSpace.Rgb:
                space = "RGB";
                break;

            case XColorSpace.Cmyk:
                space = "CMYK";
                break;

            case XColorSpace.GrayScale:
                space = "grayscale";
                break;

            default:
                space = "(undefined)";
                break;
        }
        return $"The document requires color mode {mode}, but a color is defined using {space}. " +
               "Use only colors that match the color mode of the PDF document";
    }

    public static string CannotGetGlyphTypeface(string fontName)
    {
        return Format("Cannot get a matching glyph typeface for font '{0}'.", fontName);
    }


    // ----- PdfParser ----------------------------------------------------------------------------

    public static string UnexpectedToken(string token)
    {
        return Format(PSMsgID.UnexpectedToken, token);
        //return Format("Token '{0}' was not expected.", token);
    }

    public static string UnknownEncryption => GetString(PSMsgID.UnknownEncryption);

    //get { return "The PDF document is protected with an encryption not supported by PdfPinata."; }

    #endregion

    #region Resource manager

    /// <summary>
    /// Gets the resource manager for this module.
    /// </summary>
    public static ResourceManager ResMngr
    {
        get
        {
            if (_resmngr == null)
            {
                try
                {
                    Lock.EnterFontFactory();
                    if (_resmngr == null)
                    {
                        _resmngr = new ResourceManager("PdfPinata.Resources.Messages",
                            Assembly.GetExecutingAssembly());
                    }
                }
                finally { Lock.ExitFontFactory(); }
            }
            return _resmngr;
        }
    }
    static ResourceManager _resmngr;

    #endregion
}
