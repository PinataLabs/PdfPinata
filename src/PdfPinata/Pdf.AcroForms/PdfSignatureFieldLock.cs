using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// The fields that become read-only once a signature field is signed - a signature field lock
/// dictionary, ISO 32000-1 section 12.7.4.5 and Table 233. It is <see cref="PdfSignatureField.Lock"/>
/// on a signature field; <see cref="Signatures.PdfSignatureOptions.LockAction"/> asks
/// <see cref="Signatures.PdfSigner"/> for one on the field it signs.
/// </summary>
/// <remarks>
/// Fields are named by their fully qualified names - the partial names of a field and its
/// ancestors joined by periods, which is what <see cref="PdfAcroForm"/>'s <c>Fields[name]</c> looks
/// a field up by.
/// </remarks>
public sealed class PdfSignatureFieldLock : PdfDictionary
{
    /// <summary>
    /// Initializes a lock over every field in the document.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfSignatureFieldLock(PdfDocument document)
        : this(document, PdfFieldLockAction.All)
    { }

    /// <summary>
    /// Initializes a lock with the given action over the named fields.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="action">Which fields the lock covers.</param>
    /// <param name="fields">
    /// The fully qualified names <see cref="PdfFieldLockAction.Include"/> and
    /// <see cref="PdfFieldLockAction.Exclude"/> refer to. Ignored for <see cref="PdfFieldLockAction.All"/>.
    /// </param>
    public PdfSignatureFieldLock(PdfDocument document, PdfFieldLockAction action, params string[] fields)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/SigFieldLock");
        Action = action;
        if (action != PdfFieldLockAction.All)
            Fields = fields ?? [];
    }

    internal PdfSignatureFieldLock(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Which fields the lock covers - <c>/Action</c>.
    /// </summary>
    public PdfFieldLockAction Action
    {
        get => Elements.GetName(Keys.Action) switch
        {
            "/Include" => PdfFieldLockAction.Include,
            "/Exclude" => PdfFieldLockAction.Exclude,
            _ => PdfFieldLockAction.All,
        };
        set
        {
            if (!Enum.IsDefined(typeof(PdfFieldLockAction), value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Not a lock action.");

            Elements.SetName(Keys.Action, "/" + value);
        }
    }

    /// <summary>
    /// The fully qualified names of the fields <see cref="Action"/> includes or excludes -
    /// <c>/Fields</c>, which ISO 32000-1 requires unless the action is <see cref="PdfFieldLockAction.All"/>.
    /// Setting null removes it.
    /// </summary>
    public IReadOnlyList<string> Fields
    {
        get
        {
            var array = Elements.GetArray(Keys.Fields);
            if (array == null)
                return [];

            var names = new string[array.Elements.Count];
            for (var index = 0; index < names.Length; index++)
                names[index] = array.Elements.GetString(index);
            return names;
        }
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.Fields);
                return;
            }

            var names = new PdfArray();
            foreach (var name in value)
                names.Elements.Add(new PdfString(name));
            Elements[Keys.Fields] = names;
        }
    }

    /// <summary>
    /// Whether the lock covers the field with the given fully qualified name.
    /// </summary>
    /// <remarks>
    /// A name in <see cref="Fields"/> names a field and everything under it, so listing
    /// <c>address</c> covers <c>address.street</c> - what a reader does too, and the only reading
    /// under which naming a parent field means anything.
    /// </remarks>
    public bool Covers(string fullyQualifiedName) => Action switch
    {
        PdfFieldLockAction.Include => Fields.Any(listed => Names(listed, fullyQualifiedName)),
        PdfFieldLockAction.Exclude => !Fields.Any(listed => Names(listed, fullyQualifiedName)),
        _ => true,
    };

    private static bool Names(string listed, string field) =>
        !string.IsNullOrEmpty(listed) && field != null
        && (field == listed
            || (field.Length > listed.Length && field[listed.Length] == '.'
                && field.StartsWith(listed, StringComparison.Ordinal)));

    /// <summary>
    /// The lock a dictionary is, typed - the same object when it already is one.
    /// </summary>
    internal static PdfSignatureFieldLock From(PdfDictionary dict) =>
        dict as PdfSignatureFieldLock ?? new PdfSignatureFieldLock(dict);

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present, shall be
        /// SigFieldLock.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "SigFieldLock")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) A name which, in conjunction with Fields, indicates the set of fields that
        /// should be locked: All, Include or Exclude.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Action = "/Action";

        /// <summary>
        /// (Required if the value of Action is Include or Exclude) An array of text strings
        /// containing field names.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Fields = "/Fields";

        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
