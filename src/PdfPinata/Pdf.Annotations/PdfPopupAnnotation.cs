namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// The window a reader opens to show a markup annotation's text - ISO 32000-1 section 12.5.6.14.
/// </summary>
/// <remarks>
/// <para>
/// A pop-up has no appearance of its own and none is drawn here: it is the reader's window, and a
/// reader draws it in its own style. What it carries is where that window goes, whether it starts
/// open, and which annotation it belongs to.
/// </para>
/// <para>
/// It is an annotation on the page like any other, so it is added to the page's annotations first
/// and then given to <see cref="PdfMarkupAnnotation.Popup"/>, which links the two both ways:
/// </para>
/// <code>
/// var popup = new PdfPopupAnnotation { Rectangle = where, Open = true };
/// page.Annotations.Add(note);
/// page.Annotations.Add(popup);
/// note.Popup = popup;
/// </code>
/// </remarks>
public sealed class PdfPopupAnnotation : PdfAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPopupAnnotation"/> class.
    /// </summary>
    public PdfPopupAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPopupAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfPopupAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Wraps an annotation dictionary read from a document, keeping every entry it has and
    /// writing none of the defaults a new one is given.
    /// </summary>
    internal PdfPopupAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    private void Initialize()
    {
        Elements.SetName(PdfAnnotation.Keys.Subtype, "/Popup");
    }

    /// <summary>
    /// The pop-up a dictionary is, typed - the same object when it already is one.
    /// </summary>
    internal static PdfPopupAnnotation From(PdfDictionary dict) =>
        dict as PdfPopupAnnotation ?? new PdfPopupAnnotation(dict);

    /// <summary>
    /// The annotation whose text this shows - <c>/Parent</c>. Set by
    /// <see cref="PdfMarkupAnnotation.Popup"/> rather than here, so the two ends cannot disagree.
    /// </summary>
    public PdfAnnotation ParentAnnotation =>
        Elements.GetDictionary(Keys.Parent) is { } parent ? FromDictionary(parent) : null;

    /// <summary>
    /// Whether the window is open when the page is first shown - <c>/Open</c>. False by default.
    /// </summary>
    public bool Open
    {
        get => Elements.GetBoolean(Keys.Open);
        set
        {
            Elements.SetBoolean(Keys.Open, value);
            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        /// <summary>
        /// (Optional; shall be an indirect reference) The parent annotation with which this
        /// pop-up annotation shall be associated.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Parent = "/Parent";

        /// <summary>
        /// (Optional) A flag specifying whether the pop-up annotation shall initially be displayed
        /// open. Default value: false.
        /// </summary>
        [KeyInfo(KeyType.Boolean | KeyType.Optional)]
        public const string Open = "/Open";

        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
