using System;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// What the markup annotations share - ISO 32000-1 section 12.5.6.2 and Table 170: the ones a
/// reader lists as a comment, with an author, a pop-up note and a place in a thread of replies.
/// </summary>
/// <remarks>
/// <para>
/// The title (<c>/T</c>), subject (<c>/Subj</c>), creation date and opacity (<c>/CA</c>) are markup
/// entries too, and they stay on <see cref="PdfAnnotation"/> where they have always been: moving a
/// public property down a class would break every caller reaching it through an annotation that is
/// not a markup one. What is here is what nothing had before - the pop-up, the reply thread, the
/// rich text and the intent.
/// </para>
/// <para>
/// <see cref="PdfLinkAnnotation"/>, <see cref="PdfWidgetAnnotation"/> and
/// <see cref="PdfPopupAnnotation"/> are the annotations here that are not markup, and do not
/// derive from this.
/// </para>
/// </remarks>
public abstract class PdfMarkupAnnotation : PdfAnnotation
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMarkupAnnotation"/> class.
    /// </summary>
    protected PdfMarkupAnnotation()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMarkupAnnotation"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    protected PdfMarkupAnnotation(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Wraps an annotation dictionary read from a document.
    /// </summary>
    private protected PdfMarkupAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// The pop-up note a reader opens for this annotation - <c>/Popup</c> - or null when it has
    /// none, and a reader positions one of its own.
    /// </summary>
    /// <remarks>
    /// Setting one also points the pop-up's <c>/Parent</c> back at this annotation, which is how a
    /// reader knows whose text it shows. Both have to be on a page first, since the entries are
    /// indirect references and only an annotation on a page has one.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// This annotation or the pop-up is not on a page yet, or they belong to different documents.
    /// </exception>
    public PdfPopupAnnotation Popup
    {
        get => Elements.GetDictionary(Keys.Popup) is { } popup
            ? PdfPopupAnnotation.From(popup)
            : null;
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.Popup);
                Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
                return;
            }

            RequireReference(this, "annotation");
            RequireReference(value, "pop-up");
            if (value.Owner != Owner)
                throw new InvalidOperationException("A pop-up and the annotation it belongs to must be in the same document.");

            Elements.SetReference(Keys.Popup, value);
            value.Elements.SetReference(PdfPopupAnnotation.Keys.Parent, this);
            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// The annotation this one replies to - <c>/IRT</c> - or null when it starts a thread.
    /// </summary>
    /// <remarks>
    /// Read back as the class the other annotation's subtype names, as the page's own collection
    /// hands it out. It must be on a page to be named, for the same reason as <see cref="Popup"/>.
    /// </remarks>
    public PdfAnnotation InReplyTo
    {
        get => Elements.GetDictionary(Keys.IRT) is { } replied ? FromDictionary(replied) : null;
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.IRT);
            }
            else
            {
                RequireReference(value, "annotation replied to");
                Elements.SetReference(Keys.IRT, value);
            }

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// How this annotation relates to the one it replies to - <c>/RT</c>. Meaningful only with
    /// <see cref="InReplyTo"/>.
    /// </summary>
    public PdfReplyType ReplyType
    {
        get => Elements.GetName(Keys.RT) == "/Group" ? PdfReplyType.Group : PdfReplyType.Reply;
        set
        {
            // R is the default, so it is written by leaving the entry out.
            if (value == PdfReplyType.Group)
                Elements.SetName(Keys.RT, "/Group");
            else
                Elements.Remove(Keys.RT);

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// The text of the pop-up as rich text - <c>/RC</c>, an XHTML fragment. Null when there is
    /// none, which leaves a reader showing <see cref="PdfAnnotation.Contents"/> as it is.
    /// </summary>
    /// <remarks>
    /// Written as it is given; nothing checks it is well formed. A reader showing rich text shows
    /// this in place of the contents, so the two should say the same thing.
    /// </remarks>
    public string RichText
    {
        get => Elements.ContainsKey(Keys.RC) ? Elements.GetString(Keys.RC) : null;
        set
        {
            if (value == null)
                Elements.Remove(Keys.RC);
            else
                Elements.SetString(Keys.RC, value);

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// What the annotation is for, beyond its subtype - <c>/IT</c>, such as <c>/PolygonCloud</c>
    /// or <c>/FreeTextCallout</c> - with its solidus, or null when it says nothing.
    /// </summary>
    public string Intent
    {
        get
        {
            var intent = Elements.GetName(Keys.IT);
            return intent.Length == 0 ? null : intent;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                Elements.Remove(Keys.IT);
            else
                Elements.SetName(Keys.IT, value);

            Elements.SetDateTime(PdfAnnotation.Keys.M, GlobalTimeSettings.Now);
        }
    }

    private static void RequireReference(PdfAnnotation annotation, string what)
    {
        if (annotation.Reference == null)
        {
            throw new InvalidOperationException(
                "The " + what + " is not on a page yet. Add it - page.Annotations.Add(annotation) - "
                + "before linking the two, because the link is an indirect reference.");
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public new class Keys : PdfAnnotation.Keys
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Required if an RT entry is present, otherwise optional; PDF 1.5) A reference to the
        /// annotation that this annotation is "in reply to".
        /// </summary>
        [KeyInfo("1.5", KeyType.Dictionary | KeyType.Optional)]
        public const string IRT = "/IRT";

        /// <summary>
        /// (Optional; PDF 1.6) A name specifying the relationship between this annotation and the
        /// one specified by IRT: R (a reply) or Group. Default value: R.
        /// </summary>
        [KeyInfo("1.6", KeyType.Name | KeyType.Optional)]
        public const string RT = "/RT";

        /// <summary>
        /// (Optional; PDF 1.5) A rich text string to be displayed in the pop-up window when the
        /// annotation is opened.
        /// </summary>
        [KeyInfo("1.5", KeyType.TextString | KeyType.Optional)]
        public const string RC = "/RC";

        /// <summary>
        /// (Optional; PDF 1.6) A name describing the intent of the markup annotation.
        /// </summary>
        [KeyInfo("1.6", KeyType.Name | KeyType.Optional)]
        public const string IT = "/IT";

        // ReSharper restore InconsistentNaming

        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
