using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Internal;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// What the author of a signature field asks of whoever signs it - a signature field seed value
/// dictionary, ISO 32000-1 section 12.7.4.5 and Table 234. It is
/// <see cref="PdfSignatureField.SeedValue"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each entry is a suggestion unless the matching bit of <see cref="Flags"/> makes it a
/// requirement, and it is the signing application - a reader, when a person signs - that is asked
/// to honour it. This class models and round-trips the dictionary; <see cref="PdfSigner"/> always
/// creates a field of its own rather than signing one placed by somebody else, so nothing in this
/// library is ever the application a seed value constrains.
/// </para>
/// <para>
/// Names are written and read with their solidus, as <see cref="PdfDictionary.DictionaryElements.GetName"/>
/// hands them back: <c>/adbe.pkcs7.detached</c>, <c>/SHA256</c>.
/// </para>
/// </remarks>
public sealed class PdfSignatureSeedValue : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSignatureSeedValue"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfSignatureSeedValue(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/SV");
    }

    internal PdfSignatureSeedValue(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Which of the entries are requirements - <c>/Ff</c>.
    /// </summary>
    public PdfSeedValueFlags Flags
    {
        get => (PdfSeedValueFlags)Elements.GetInteger(Keys.Ff);
        set => SetOrRemove(Keys.Ff, (int)value);
    }

    /// <summary>
    /// The signature handler to sign with - <c>/Filter</c>, such as <c>/Adobe.PPKLite</c> - or null.
    /// </summary>
    public string Filter
    {
        get => NameOrNull(Keys.Filter);
        set => SetNameOrRemove(Keys.Filter, value);
    }

    /// <summary>
    /// The encodings to sign in, in order of preference - <c>/SubFilter</c>.
    /// </summary>
    public IReadOnlyList<string> SubFilters
    {
        get => Names(Keys.SubFilter);
        set => SetNames(Keys.SubFilter, value);
    }

    /// <summary>
    /// The digest algorithms to sign with - <c>/DigestMethod</c>: <c>/SHA1</c>, <c>/SHA256</c>,
    /// <c>/SHA384</c>, <c>/SHA512</c> or <c>/RIPEMD160</c>.
    /// </summary>
    public IReadOnlyList<string> DigestMethods
    {
        get => Names(Keys.DigestMethod);
        set => SetNames(Keys.DigestMethod, value);
    }

    /// <summary>
    /// The reasons a signer may give - <c>/Reasons</c>.
    /// </summary>
    public IReadOnlyList<string> Reasons
    {
        get => Strings(Keys.Reasons);
        set => SetStrings(Keys.Reasons, value);
    }

    /// <summary>
    /// The legal attestations a signer may make - <c>/LegalAttestation</c>.
    /// </summary>
    public IReadOnlyList<string> LegalAttestations
    {
        get => Strings(Keys.LegalAttestation);
        set => SetStrings(Keys.LegalAttestation, value);
    }

    /// <summary>
    /// The minimum version of the seed value handling a signer must support - <c>/V</c> - or null.
    /// </summary>
    public double? Version
    {
        get => Elements.ContainsKey(Keys.V) ? Elements.GetReal(Keys.V) : null;
        set
        {
            if (value == null)
                Elements.Remove(Keys.V);
            else
                Elements.SetReal(Keys.V, value.Value);
        }
    }

    /// <summary>
    /// Whether revocation information is to be embedded with the signature - <c>/AddRevInfo</c>.
    /// </summary>
    public bool AddRevocationInfo
    {
        get => Elements.GetBoolean(Keys.AddRevInfo);
        set
        {
            if (value)
                Elements.SetBoolean(Keys.AddRevInfo, true);
            else
                Elements.Remove(Keys.AddRevInfo);
        }
    }

    /// <summary>
    /// The kind of signature asked for - <c>/MDP</c>'s <c>/P</c>. Null leaves it open;
    /// <see cref="PdfCertificationLevel.NotCertified"/> asks for an approval signature and any other
    /// level for a certifying signature at that level.
    /// </summary>
    public PdfCertificationLevel? CertificationLevel
    {
        get
        {
            var mdp = Elements.GetDictionary(Keys.MDP);
            if (mdp == null || !mdp.Elements.ContainsKey("/P"))
                return null;

            var level = mdp.Elements.GetInteger("/P");
            return Enum.IsDefined(typeof(PdfCertificationLevel), level) ? (PdfCertificationLevel)level : null;
        }
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.MDP);
                return;
            }

            var mdp = new PdfDictionary(Owner);
            mdp.Elements.SetInteger("/P", (int)value.Value);
            Elements[Keys.MDP] = mdp;
        }
    }

    /// <summary>
    /// A time-stamping authority to time-stamp the signature with - <c>/TimeStamp</c>'s
    /// <c>/URL</c> - or null.
    /// </summary>
    public string TimeStampUrl
    {
        get => Elements.GetDictionary(Keys.TimeStamp) is { } timeStamp && timeStamp.Elements.ContainsKey("/URL")
            ? timeStamp.Elements.GetString("/URL")
            : null;
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.TimeStamp);
                return;
            }

            var timeStamp = new PdfDictionary(Owner);
            timeStamp.Elements.SetString("/URL", value);
            Elements[Keys.TimeStamp] = timeStamp;
        }
    }

    /// <summary>
    /// What is asked of the signing certificate - <c>/Cert</c> - or null. Direct, as the
    /// specification allows.
    /// </summary>
    public PdfCertificateSeedValue Certificate
    {
        get => Elements.GetDictionary(Keys.Cert) is { } cert
            ? cert as PdfCertificateSeedValue ?? new PdfCertificateSeedValue(cert)
            : null;
        set
        {
            if (value == null)
                Elements.Remove(Keys.Cert);
            else
                Elements[Keys.Cert] = value;
        }
    }

    internal static PdfSignatureSeedValue From(PdfDictionary dict) =>
        dict as PdfSignatureSeedValue ?? new PdfSignatureSeedValue(dict);

    // ----- shared with the certificate dictionary ---------------------------------------------------

    void SetOrRemove(string key, int value)
    {
        if (value == 0)
            Elements.Remove(key);
        else
            Elements.SetInteger(key, value);
    }

    string NameOrNull(string key)
    {
        var name = Elements.GetName(key);
        return name.Length == 0 ? null : name;
    }

    void SetNameOrRemove(string key, string value)
    {
        if (string.IsNullOrEmpty(value))
            Elements.Remove(key);
        else
            Elements.SetName(key, value);
    }

    IReadOnlyList<string> Names(string key) => SeedValues.Read(Elements.GetArray(key), (a, i) => a.Elements.GetName(i));

    void SetNames(string key, IReadOnlyList<string> value) =>
        SeedValues.Write(Elements, key, value, name =>
        {
            if (string.IsNullOrEmpty(name) || name == "/")
                throw new ArgumentException("A name in " + key + " cannot be empty.", nameof(value));

            return new PdfName(name[0] == '/' ? name : "/" + name);
        });

    IReadOnlyList<string> Strings(string key) => SeedValues.Read(Elements.GetArray(key), (a, i) => a.Elements.GetString(i));

    void SetStrings(string key, IReadOnlyList<string> value) =>
        SeedValues.Write(Elements, key, value, text =>
            new PdfString(text ?? throw new ArgumentException("A string in " + key + " cannot be null.", nameof(value))));

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>(Optional) Shall be SV.</summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "SV")]
        public const string Type = "/Type";

        /// <summary>(Optional) Flags saying which entries are required.</summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Ff = "/Ff";

        /// <summary>(Optional) The signature handler to be used.</summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Filter = "/Filter";

        /// <summary>(Optional) An array of names of acceptable encodings.</summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string SubFilter = "/SubFilter";

        /// <summary>(Optional; PDF 1.7) An array of names of acceptable digest algorithms.</summary>
        [KeyInfo("1.7", KeyType.Array | KeyType.Optional)]
        public const string DigestMethod = "/DigestMethod";

        /// <summary>(Optional) The minimum required capability of the signature field seed value handler.</summary>
        [KeyInfo(KeyType.Real | KeyType.Optional)]
        public const string V = "/V";

        /// <summary>(Optional) A certificate seed value dictionary.</summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Cert = "/Cert";

        /// <summary>(Optional) An array of text strings specifying possible reasons for signing.</summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Reasons = "/Reasons";

        /// <summary>(Optional; PDF 1.6) A dictionary whose P entry says which MDP permissions are allowed.</summary>
        [KeyInfo("1.6", KeyType.Dictionary | KeyType.Optional)]
        public const string MDP = "/MDP";

        /// <summary>(Optional; PDF 1.6) A time-stamp dictionary with the URL of a time-stamping authority.</summary>
        [KeyInfo("1.6", KeyType.Dictionary | KeyType.Optional)]
        public const string TimeStamp = "/TimeStamp";

        /// <summary>(Optional; PDF 1.6) An array of text strings specifying possible legal attestations.</summary>
        [KeyInfo("1.6", KeyType.Array | KeyType.Optional)]
        public const string LegalAttestation = "/LegalAttestation";

        /// <summary>(Optional; PDF 1.7) Whether revocation checking is to be performed and its result embedded.</summary>
        [KeyInfo("1.7", KeyType.Boolean | KeyType.Optional)]
        public const string AddRevInfo = "/AddRevInfo";

        // ReSharper restore InconsistentNaming

        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}

/// <summary>
/// What a signature field's author asks of the signing certificate - a certificate seed value
/// dictionary, ISO 32000-1 Table 235. It is <see cref="PdfSignatureSeedValue.Certificate"/>.
/// </summary>
/// <remarks>
/// Certificates are DER bytes, as the specification has them. An object identifier is given in its
/// dotted form, <c>2.16.840.1.101.3.2.1.3.7</c>, and written as the byte string of those
/// characters.
/// </remarks>
public sealed class PdfCertificateSeedValue : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCertificateSeedValue"/> class. It needs no
    /// document: it is held directly inside the seed value dictionary.
    /// </summary>
    public PdfCertificateSeedValue()
    {
        Elements.SetName(Keys.Type, "/SVCert");
    }

    internal PdfCertificateSeedValue(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Which of the entries are requirements - <c>/Ff</c>.
    /// </summary>
    public PdfCertificateSeedValueFlags Flags
    {
        get => (PdfCertificateSeedValueFlags)Elements.GetInteger(Keys.Ff);
        set
        {
            if (value == PdfCertificateSeedValueFlags.None)
                Elements.Remove(Keys.Ff);
            else
                Elements.SetInteger(Keys.Ff, (int)value);
        }
    }

    /// <summary>
    /// The certificates the signer must sign with one of, DER-encoded - <c>/Subject</c>.
    /// </summary>
    public IReadOnlyList<byte[]> Subjects
    {
        get => SeedValues.Read(Elements.GetArray(Keys.Subject), BytesAt);
        set => SeedValues.Write(Elements, Keys.Subject, value, ByteString);
    }

    /// <summary>
    /// The certificates the signing certificate must be issued by one of, DER-encoded - <c>/Issuer</c>.
    /// </summary>
    public IReadOnlyList<byte[]> Issuers
    {
        get => SeedValues.Read(Elements.GetArray(Keys.Issuer), BytesAt);
        set => SeedValues.Write(Elements, Keys.Issuer, value, ByteString);
    }

    /// <summary>
    /// The certificate policies the signing certificate must carry one of, as dotted object
    /// identifiers - <c>/OID</c>.
    /// </summary>
    public IReadOnlyList<string> PolicyOids
    {
        get => SeedValues.Read(Elements.GetArray(Keys.OID), (a, i) => a.Elements.GetString(i));
        set => SeedValues.Write(Elements, Keys.OID, value,
            oid => new PdfString(oid, PdfStringEncoding.RawEncoding));
    }

    /// <summary>
    /// The key usages the signing certificate must have - <c>/KeyUsage</c>, each a string of
    /// <c>0</c>, <c>1</c> and <c>X</c> as ISO 32000-1 describes.
    /// </summary>
    public IReadOnlyList<string> KeyUsages
    {
        get => SeedValues.Read(Elements.GetArray(Keys.KeyUsage), (a, i) => a.Elements.GetString(i));
        set => SeedValues.Write(Elements, Keys.KeyUsage, value, usage => new PdfString(usage));
    }

    /// <summary>
    /// Where a signer without a suitable certificate can get one - <c>/URL</c> - or null.
    /// </summary>
    public string Url
    {
        get => Elements.ContainsKey(Keys.URL) ? Elements.GetString(Keys.URL) : null;
        set
        {
            if (value == null)
                Elements.Remove(Keys.URL);
            else
                Elements.SetString(Keys.URL, value);
        }
    }

    static byte[] BytesAt(PdfArray array, int index) =>
        array.Elements[index] is PdfString text ? PdfEncoders.RawEncoding.GetBytes(text.Value) : Array.Empty<byte>();

    static PdfItem ByteString(byte[] bytes) =>
        new PdfString(PdfEncoders.RawEncoding.GetString(bytes, 0, bytes.Length), PdfStringEncoding.RawEncoding);

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>(Optional) Shall be SVCert.</summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "SVCert")]
        public const string Type = "/Type";

        /// <summary>(Optional) Flags saying which entries are required.</summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string Ff = "/Ff";

        /// <summary>(Optional) An array of byte strings containing DER-encoded X.509v3 certificates acceptable for signing.</summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Subject = "/Subject";

        /// <summary>(Optional; PDF 1.7) An array of dictionaries naming acceptable subject distinguished names.</summary>
        [KeyInfo("1.7", KeyType.Array | KeyType.Optional)]
        public const string SubjectDN = "/SubjectDN";

        /// <summary>(Optional; PDF 1.7) An array of ASCII strings specifying acceptable key usages.</summary>
        [KeyInfo("1.7", KeyType.Array | KeyType.Optional)]
        public const string KeyUsage = "/KeyUsage";

        /// <summary>(Optional) An array of byte strings containing DER-encoded X.509v3 certificates of acceptable issuers.</summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Issuer = "/Issuer";

        /// <summary>(Optional) An array of byte strings containing object identifiers of acceptable certificate policies.</summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string OID = "/OID";

        /// <summary>(Optional) A URL where a certificate can be obtained.</summary>
        [KeyInfo(KeyType.String | KeyType.Optional)]
        public const string URL = "/URL";

        /// <summary>(Optional) A name indicating the usage of URL: Browser or ASSP.</summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string URLType = "/URLType";

        // ReSharper restore InconsistentNaming

        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}

/// <summary>
/// The arrays of names, strings and byte strings both seed value dictionaries are mostly made of.
/// </summary>
static class SeedValues
{
    public static IReadOnlyList<T> Read<T>(PdfArray array, Func<PdfArray, int, T> item)
    {
        if (array == null)
            return Array.Empty<T>();

        var values = new T[array.Elements.Count];
        for (var index = 0; index < values.Length; index++)
            values[index] = item(array, index);
        return values;
    }

    /// <summary>
    /// Writes the values as a direct array, or removes the entry for null or none.
    /// </summary>
    public static void Write<T>(PdfDictionary.DictionaryElements elements, string key,
        IReadOnlyList<T> values, Func<T, PdfItem> item)
    {
        if (values == null || values.Count == 0)
        {
            elements.Remove(key);
            return;
        }

        var array = new PdfArray();
        foreach (var value in values)
            array.Elements.Add(item(value));
        elements[key] = array;
    }
}
