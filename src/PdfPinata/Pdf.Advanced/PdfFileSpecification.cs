namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represent a file stream embedded in the PDF document
/// </summary>
public class PdfFileSpecification : PdfDictionary
{
    /// <summary>Initializes a new file specification with an empty embedded-file dictionary.</summary>
    public PdfFileSpecification(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Filespec");
        Elements.SetObject(Keys.EF, new PdfDictionary());
    }

    /// <summary>Initializes a new file specification naming a file and the embedded bytes for it.</summary>
    public PdfFileSpecification(PdfDocument document, string fileName, PdfEmbeddedFile embeddedFile)
        : this(document)
    {
        FileName = fileName;
        EmbeddedFile = embeddedFile;
    }

    /// <summary>
    /// Takes over a dictionary read out of a document, so that a specification reached through the
    /// catalog or through an annotation is this type rather than the plain dictionary it was parsed
    /// as.
    /// </summary>
    internal PdfFileSpecification(PdfDictionary dictionary)
        : base(dictionary)
    { }

    /// <summary>
    /// The <c>/EF</c> dictionary naming the streams the file is embedded as, made on first use.
    /// <para>
    /// Read out of the elements rather than held in a field, because a specification also arrives by
    /// being read from a document — and one that did would otherwise have no dictionary here at all,
    /// and lose its embedded file the moment anybody asked for it.
    /// </para>
    /// </summary>
    private PdfDictionary EmbeddedFiles
    {
        get
        {
            var files = Elements.GetDictionary(Keys.EF);
            if (files == null)
            {
                files = new PdfDictionary();
                Elements.SetObject(Keys.EF, files);
            }
            return files;
        }
    }

    /// <summary>
    /// Gets or sets the name of the specified file. Setting it writes both <c>/F</c> and <c>/UF</c>;
    /// reading prefers <c>/UF</c>, which is the one that can carry a name outside ASCII.
    /// </summary>
    public string FileName
    {
        // /UF says what the name really is, so it is the one to believe. Documents written
        // before it was set, and those written by other libraries, have only /F.
        get
        {
            if (Elements.TryGetString(Keys.UF, out var unicodeName))
                return unicodeName;
            return Elements.GetString(Keys.F);
        }
        // A file specification string is not a text string: it stays one byte per character,
        // and it is /UF that holds the name for readers that want more than ASCII.
        set
        {
            Elements.SetString(Keys.F, value, PdfStringEncoding.RawEncoding);
            Elements.SetString(Keys.UF, value);
        }
    }

    /// <summary>
    /// Gets or sets what a viewer shows beside the file name in its attachments pane, written as
    /// <c>/Desc</c>. Setting it to null removes it.
    /// </summary>
    public string Description
    {
        get => Elements.GetString(Keys.Desc);
        set
        {
            if (value == null)
                Elements.Remove(Keys.Desc);
            else
                Elements.SetString(Keys.Desc, value);
        }
    }

    /// <summary>
    /// Gets or sets what this file has to do with the document, written as <c>/AFRelationship</c>.
    /// A specification that says nothing, or that names a relationship this enumeration does not
    /// have, reads as <see cref="PdfAFRelationship.Unspecified"/>.
    /// </summary>
    /// <remarks>
    /// Reading and writing are deliberately not symmetrical about an absent entry. Writing
    /// <see cref="PdfAFRelationship.Unspecified"/> puts <c>/Unspecified</c> in the file, because
    /// PDF/A-3 asks for the entry to be present and that is the value for having nothing to say;
    /// reading an absent entry answers the same thing, because there is nothing else it could mean.
    /// So a document read and written back keeps its meaning while gaining an entry a validator
    /// wants — which is the direction that matters.
    /// </remarks>
    public PdfAFRelationship Relationship
    {
        get => RelationshipOf(Elements.GetName(Keys.AFRelationship));
        set => Elements.SetName(Keys.AFRelationship, NameOf(value));
    }

    /// <summary>Gets or sets the embedded file this specification points at.</summary>
    /// <remarks>
    /// The <c>/EF</c> dictionary may name the stream under either <c>/F</c> or <c>/UF</c> — its keys
    /// mirror the ones the specification itself carries. This library writes <c>/F</c>, and reads both,
    /// because a producer that wrote only <c>/UF</c> has still embedded a file and answering null for
    /// it would leave that file invisible to everything downstream: to a caller asking what a document
    /// carries, and to the PDF/A check that refuses an embedded file under PDF/A-1.
    /// </remarks>
    public PdfEmbeddedFile EmbeddedFile
    {
        get
        {
            var files = Elements.GetDictionary(Keys.EF);
            if (files == null)
                return null;

            // Transformed rather than cast. Read back out of a document the stream is a plain
            // dictionary, and a cast would answer null for an attachment that is plainly there.
            return EmbeddedFileOf(files.Elements[Keys.F]) ?? EmbeddedFileOf(files.Elements[Keys.UF]);
        }
        set
        {
            if (value == null)
            {
                // Both, or clearing a specification read from a document that used the other key
                // would leave the stream still attached.
                var files = Elements.GetDictionary(Keys.EF);
                files?.Elements.Remove(Keys.F);
                files?.Elements.Remove(Keys.UF);
            }
            else
            {
                if (!value.IsIndirect)
                    Owner._irefTable.Add(value);

                EmbeddedFiles.Elements.SetReference(Keys.F, value);
            }
        }
    }

    /// <summary>
    /// The embedded file an entry stands for, whichever of the three shapes it arrived in: the
    /// stream already transformed to this type, an indirect reference to one, or the plain
    /// dictionary a reader parsed. Transforming re-points the reference at the new instance, so
    /// asking twice does not build two.
    /// </summary>
    private static PdfEmbeddedFile EmbeddedFileOf(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfEmbeddedFile embedded)
            return embedded;

        return item is PdfDictionary dictionary ? new PdfEmbeddedFile(dictionary) : null;
    }

    /// <summary>
    /// The relationship a <c>/AFRelationship</c> name stands for. Anything unrecognised is
    /// unspecified rather than an error: a document may legally carry a relationship from a later
    /// part of the standard than this enumeration covers, and refusing to read the rest of the
    /// attachment over it would help nobody.
    /// </summary>
    private static PdfAFRelationship RelationshipOf(string name)
    {
        switch (name)
        {
            case "/Source": return PdfAFRelationship.Source;
            case "/Data": return PdfAFRelationship.Data;
            case "/Alternative": return PdfAFRelationship.Alternative;
            case "/Supplement": return PdfAFRelationship.Supplement;
            default: return PdfAFRelationship.Unspecified;
        }
    }

    private static string NameOf(PdfAFRelationship relationship)
    {
        switch (relationship)
        {
            case PdfAFRelationship.Source: return "/Source";
            case PdfAFRelationship.Data: return "/Data";
            case PdfAFRelationship.Alternative: return "/Alternative";
            case PdfAFRelationship.Supplement: return "/Supplement";
            default: return "/Unspecified";
        }
    }

    /// <summary>
    /// Predefined keys of this embedded file.
    /// </summary>
    public class Keys : KeysBase
    {
        /// <summary>
        /// (Required if an EF or RF entry is present; recommended always)
        /// The type of PDF object that this dictionary describes; must be Filespec
        /// for a file specification dictionary (see implementation note 45 in Appendix H).
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "Filespec")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required if the DOS, Mac, and Unix entries are all absent; amended with the UF
        /// entry for PDF 1.7) A file specification string of the form described in Section
        /// 3.10.1, “File Specification Strings,” or (if the file system is URL) a uniform
        /// resource locator, as described in Section 3.10.4, “URL Specifications.”
        ///
        /// Note: It is recommended that the UF entry be used in addition to the F entry.
        /// The UF entry provides cross-platform and cross-language compatibility and the F
        /// entry provides backwards compatibility
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string F = "/F";

        /// <summary>
        /// (Optional, but recommended if the F entry exists; PDF 1.7) A Unicode text string
        /// that provides file specification of the form described in Section 3.10.1,
        /// “File Specification Strings.” This is a text string encoded using PDFDocEncoding
        /// or UTF-16BE with a leading byte-order marker, and so can name a file that the
        /// one byte per character F entry cannot spell.
        /// </summary>
        [KeyInfo("1.7", KeyType.TextString | KeyType.Optional)]
        public const string UF = "/UF";

        /// <summary>
        /// (Required if RF is present; PDF 1.3; amended to include the UF key in PDF 1.7)
        /// A dictionary containing a subset of the keys F, UF, DOS, Mac, and Unix,
        /// corresponding to the entries by those names in the file specification dictionary.
        /// The value of each such key is an embedded file stream (see Section 3.10.3,
        /// “Embedded File Streams”) containing the corresponding file. If this entry is
        /// present, the Type entry is required and the file specification dictionary must
        /// be indirectly referenced. (See implementation note 46in Appendix H.)
        ///
        /// Note: It is recommended that the F and UF entries be used in place of the DOS,
        /// Mac, or Unix entries.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string EF = "/EF";

        /// <summary>
        /// (Optional; PDF 1.6) Descriptive text associated with the file specification, shown
        /// beside the file name by a viewer listing what a document carries.
        /// </summary>
        [KeyInfo("1.6", KeyType.TextString | KeyType.Optional)]
        public const string Desc = "/Desc";

        /// <summary>
        /// (Required for an associated file; PDF 2.0, and by ISO 19005-3 for every attachment of a
        /// PDF/A-3 document) A name saying how the embedded file relates to the document it is
        /// attached to: Source, Data, Alternative, Supplement or Unspecified.
        /// </summary>
        [KeyInfo("1.7", KeyType.Name | KeyType.Optional)]
        public const string AFRelationship = "/AFRelationship";

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
