using System;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represent a file stream embedded in the PDF document
/// </summary>
public class PdfEmbeddedFile : PdfDictionary
{
    /// <summary>Initializes a new embedded file with no content yet.</summary>
    public PdfEmbeddedFile(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/EmbeddedFile");

        // Made here as well as on demand, so that a file created and never filled in writes the
        // same empty parameter dictionary it always did.
        Elements.SetObject(Keys.Params, new PdfDictionary());
    }

    /// <summary>Initializes a new embedded file holding the given bytes.</summary>
    public PdfEmbeddedFile(PdfDocument document, byte[] bytes, string checksum = null)
        : this(document)
    {
        CreateStreamAndSetProperties(bytes, checksum);
    }

    /// <summary>
    /// Takes over a dictionary read out of a document, so that an embedded file reached through a
    /// file specification is this type rather than the plain dictionary it was parsed as.
    /// </summary>
    internal PdfEmbeddedFile(PdfDictionary dictionary)
        : base(dictionary)
    { }

    /// <summary>
    /// The parameter dictionary the size, the checksum and the dates live in, made on first use.
    /// <para>
    /// Read out of <c>/Params</c> rather than held in a field, because an embedded file also arrives
    /// by being read from a document — and one that did would otherwise have no dictionary here at
    /// all, and answer every question about itself by throwing.
    /// </para>
    /// </summary>
    private PdfDictionary Parameters
    {
        get
        {
            var parameters = Elements.GetDictionary(Keys.Params);
            if (parameters == null)
            {
                parameters = new PdfDictionary();
                Elements.SetObject(Keys.Params, parameters);
            }
            return parameters;
        }
    }

    /// <summary>
    /// Stores the bytes as this file's stream and records its size, and its MD5 checksum when one is
    /// given. Passing no checksum removes any recorded already.
    /// </summary>
    public void CreateStreamAndSetProperties(byte[] bytes, string checksum = null)
    {
        CreateStream(bytes);

        var parameters = Parameters;
        parameters.Elements.SetInteger(Keys.Size, bytes.Length);

        if (string.IsNullOrEmpty(checksum))
            parameters.Elements.Remove(Keys.CheckSum);
        else
            // The checksum is the bytes of an MD5 digest rather than text, so it is named raw
            // and the bytes above ASCII in it are written as they are.
            parameters.Elements.SetString(Keys.CheckSum, checksum, PdfStringEncoding.RawEncoding);
    }

    /// <summary>Gets or sets the media type of the embedded file, written as the <c>/Subtype</c> name.</summary>
    public string MimeType
    {
        get => Elements.GetName(Keys.Subtype);
        set => Elements.SetName(Keys.Subtype, value);
    }

    /// <summary>
    /// Gets or sets when the embedded file was last modified, written as <c>/ModDate</c> in the
    /// parameter dictionary. Reading a file that records none answers
    /// <see cref="DateTime.MinValue"/>.
    /// </summary>
    /// <remarks>
    /// PDF/A-3 requires this of an attachment, which is why it is a property rather than a comment
    /// about the parameter dictionary: an archive has to be able to say how old the thing it is
    /// keeping is, and the file system it came from is not there to be asked.
    /// </remarks>
    public DateTime ModificationDate
    {
        get => Parameters.Elements.GetDateTime(Keys.ModDate, DateTime.MinValue);
        set => Parameters.Elements.SetDateTime(Keys.ModDate, value);
    }

    /// <summary>
    /// Gets or sets when the embedded file was created, written as <c>/CreationDate</c> in the
    /// parameter dictionary. Reading a file that records none answers
    /// <see cref="DateTime.MinValue"/>.
    /// </summary>
    public DateTime CreationDate
    {
        get => Parameters.Elements.GetDateTime(Keys.CreationDate, DateTime.MinValue);
        set => Parameters.Elements.SetDateTime(Keys.CreationDate, value);
    }

    /// <summary>
    /// Predefined keys of this embedded file.
    /// </summary>
    public class Keys : PdfDictionary.PdfStream.Keys
    {
        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be EmbeddedFile for an embedded file stream.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "EmbeddedFile")]
        public const string Type = "/Type";

        /// <summary>
        /// (Optional) The subtype of the embedded file. The value of this entry must be a
        /// first-class name, as defined in Appendix E. Names without a registered prefix
        /// must conform to the MIME media type names defined in Internet RFC 2046,
        /// Multipurpose Internet Mail Extensions (MIME), Part Two: Media Types(see the
        /// Bibliography), with the provision that characters not allowed in names must
        /// use the 2-character hexadecimal code format described in Section 3.2.4,
        /// “Name Objects.”
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Subtype = "/Subtype";

        /// <summary>
        /// (Optional) An embedded file parameter dictionary containing additional,
        /// file-specific information (see Table 3.43).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Params = "/Params";

        /// <summary>
        /// (Optional) The size of the embedded file, in bytes.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Size = "/Size";

        /// <summary>
        /// (Optional) The date and time when the embedded file was created.
        /// </summary>
        [KeyInfo(KeyType.Date | KeyType.Optional)]
        public const string CreationDate = "/CreationDate";

        /// <summary>
        /// (Optional) The date and time when the embedded file was last modified.
        /// </summary>
        [KeyInfo(KeyType.Date | KeyType.Optional)]
        public const string ModDate = "/ModDate";

        /// <summary>
        /// (Optional) A subdictionary containing additional information specific to Mac OS files (see Table 3.44).
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Mac = "/Mac";

        /// <summary>
        /// (Optional) A 16-byte string that is the checksum of the bytes of the uncompressed
        /// embedded file. The checksum is calculated by applying the standard MD5 message-digest
        /// algorithm (described in Internet RFC 1321, The MD5 Message-Digest Algorithm; see the
        /// Bibliography) to the bytes of the embedded file stream.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string CheckSum = "/CheckSum";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta
        {
            get
            {
                if (meta == null)
                    meta = CreateMeta(typeof(Keys));
                return meta;
            }
        }
        private static DictionaryMeta meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
