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

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the signature field.
/// </summary>
public sealed class PdfSignatureField : PdfAcroField
{
    /// <summary>
    /// Initializes a new instance of PdfSignatureField.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <remarks>
    /// A place for a signature, and nothing more: this is a field type, not a signing
    /// implementation. <c>PdfPinata.Signing</c> is what signs a document.
    /// </remarks>
    public PdfSignatureField(PdfDocument document)
        : base(document, "/Sig")
    { }

    internal PdfSignatureField(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// The fields that become read-only once this field is signed - <c>/Lock</c> - or null.
    /// </summary>
    /// <remarks>
    /// Written as an indirect object, as ISO 32000-1 requires, so the field has to belong to a
    /// document and the lock to the same one. It is the signing application that honours it:
    /// <see cref="Signatures.PdfSigner"/> honours the lock it is asked for through
    /// <see cref="Signatures.PdfSignatureOptions.LockAction"/>, on the field it creates.
    /// </remarks>
    public PdfSignatureFieldLock Lock
    {
        get => Elements.GetDictionary(Keys.Lock) is { } dict ? PdfSignatureFieldLock.From(dict) : null;
        set => SetIndirect(Keys.Lock, value);
    }

    /// <summary>
    /// What the field's author asks of whoever signs it - <c>/SV</c> - or null.
    /// </summary>
    /// <remarks>
    /// Written as an indirect object, as ISO 32000-1 requires. Modelled and round-tripped only:
    /// <see cref="Signatures.PdfSigner"/> never signs a field it did not create, so it is never
    /// the application a seed value asks something of.
    /// </remarks>
    public PdfSignatureSeedValue SeedValue
    {
        get => Elements.GetDictionary(Keys.SV) is { } dict ? PdfSignatureSeedValue.From(dict) : null;
        set => SetIndirect(Keys.SV, value);
    }

    private void SetIndirect(string key, PdfDictionary value)
    {
        if (value == null)
        {
            Elements.Remove(key);
            return;
        }

        if (Owner == null || value.Owner != Owner)
            throw new ArgumentException("The dictionary and the field must belong to the same document.", nameof(value));

        if (value.Reference == null)
            Owner.Internals.AddObject(value);

        Elements.SetReference(key, value);
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be Sig for a signature dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public new const string Type = "/Type";

        /// <summary>
        /// (Required; inheritable) The name of the signature handler to be used for
        /// authenticating the field’s contents, such as Adobe.PPKLite, Entrust.PPKEF,
        /// CICI.SignIt, or VeriSign.PPKVS.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Filter = "/Filter";

        /// <summary>
        /// (Optional) The name of a specific submethod of the specified handler.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string SubFilter = "/SubFilter";

        /// <summary>
        /// (Required) An array of pairs of integers (starting byte offset, length in bytes)
        /// describing the exact byte range for the digest calculation. Multiple discontinuous
        /// byte ranges may be used to describe a digest that does not include the
        /// signature token itself.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string ByteRange = "/ByteRange";

        /// <summary>
        /// (Required) The encrypted signature token.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string Contents = "/Contents";

        /// <summary>
        /// (Optional) The name of the person or authority signing the document.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Name = "/Name";

        /// <summary>
        /// (Optional) The time of signing. Depending on the signature handler, this
        /// may be a normal unverified computer time or a time generated in a verifiable
        /// way from a secure time server.
        /// </summary>
        [KeyInfo(KeyType.Date | KeyType.Optional)]
        public const string M = "/M";

        /// <summary>
        /// (Optional) The CPU host name or physical location of the signing.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Location = "/Location";

        /// <summary>
        /// (Optional) The reason for the signing, such as (I agree…).
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Reason = "/Reason";

        /// <summary>
        /// (Optional; shall be an indirect reference; PDF 1.5) A signature field lock dictionary
        /// that specifies a set of form fields to be locked when this signature field is signed.
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string Lock = "/Lock";

        /// <summary>
        /// (Optional; shall be an indirect reference; PDF 1.5) A seed value dictionary containing
        /// information that constrains the properties of a signature that is applied to this field.
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string SV = "/SV";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
