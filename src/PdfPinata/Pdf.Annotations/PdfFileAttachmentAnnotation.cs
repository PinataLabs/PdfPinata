using PdfPinata.Pdf.Advanced;
using System;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represent a file that is attached to the PDF
/// </summary>
public class PdfFileAttachmentAnnotation : PdfAnnotation
{
    /// <summary>
    /// Name of icons used in displaying the annotation.
    /// </summary>
    public enum IconType
    {
        /// <summary>A graph.</summary>
        Graph,
        /// <summary>A push pin. The viewer default.</summary>
        PushPin,
        /// <summary>A paperclip.</summary>
        Paperclip,
        /// <summary>A tag.</summary>
        Tag
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileAttachmentAnnotation"/> class.
    /// </summary>
    public PdfFileAttachmentAnnotation()
    {
        Elements.SetName(Keys.Subtype, "/FileAttachment");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileAttachmentAnnotation"/> class.
    /// </summary>
    public PdfFileAttachmentAnnotation(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Subtype, "/FileAttachment");
        Flags = PdfAnnotationFlags.Locked;
    }

    /// <summary>
    /// The icon a viewer draws for the attachment. Unlike the icon of a text annotation or a
    /// rubber stamp there is no "none": an attachment with no <c>/Name</c> is a push pin, which
    /// is the default ISO 32000-1 Table 184 gives the entry.
    /// </summary>
    public IconType Icon
    {
        get => IconFromName(Elements.GetName(Keys.Name), IconType.PushPin);
        set
        {
            // Removing the key rather than writing a name for a value the enumeration does not
            // have. A cast from an out-of-range integer would otherwise put something like /42
            // into the file, and a reader handed a name it does not know draws nothing at all.
            if (Enum.IsDefined(value))
                Elements.SetName(Keys.Name, value.ToString());
            else
                Elements.Remove(Keys.Name);
        }
    }

    /// <summary>
    /// Gets or sets the file specification naming the attached file and holding its embedded bytes.
    /// </summary>
    public PdfFileSpecification File
    {
        get
        {
            var reference = Elements.GetReference(Keys.FS);

            return reference?.Value as PdfFileSpecification;
        }
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.FS);
            }
            else
            {
                if (!value.IsIndirect)
                    Owner._irefTable.Add(value);

                Elements.SetReference(Keys.FS, value);
            }
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal new class Keys : PdfAnnotation.Keys
    {
        /// <summary>
        /// (Required) The file associated with this annotation.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string FS = "/FS";

        /// <summary>
        /// (Optional) The name of an icon to be used in displaying the annotation.
        /// Viewer applications should provide predefined icon appearances for at least
        /// the following standard names:
        ///
        /// Graph
        /// PushPin
        /// Paperclip
        /// Tag
        ///
        /// Additional names may be supported as well. Default value: PushPin.
        /// Note: The annotation dictionary’s AP entry, if present, takes precedence over
        /// the Name entry; see Table 8.15 on page 606 and Section 8.4.4, “Appearance Streams.”
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional)]
        public const string Name = "/Name";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta
        {
            get
            {
                if (meta == null)
                    meta = CreateMeta(typeof(Keys));
                return meta;
            }
        }
        static DictionaryMeta meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
