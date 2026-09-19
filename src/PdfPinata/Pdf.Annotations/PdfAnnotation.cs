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
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// Represents the base class of all annotations.
/// </summary>
public abstract class PdfAnnotation : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotation"/> class.
    /// </summary>
    protected PdfAnnotation()
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotation"/> class.
    /// </summary>
    protected PdfAnnotation(PdfDocument document)
        : base(document)
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotation"/> class.
    /// </summary>
    internal PdfAnnotation(PdfDictionary dict)
        : base(dict)
    { }

    void Initialize()
    {
        Elements.SetName(Keys.Type, "/Annot");
        Elements.SetString(Keys.NM, Guid.NewGuid().ToString("D"));
        Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
    }

    /// <summary>
    /// Gets or sets the annotation flags of this instance.
    /// </summary>
    public PdfAnnotationFlags Flags
    {
        get => (PdfAnnotationFlags)Elements.GetInteger(Keys.F);
        set
        {
            Elements.SetInteger(Keys.F, (int)value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Gets or sets the PdfAnnotations object that this annotation belongs to.
    /// </summary>
    public PdfAnnotations Parent
    {
        get => _parent;
        set => _parent = value;
    }
    PdfAnnotations _parent;

    /// <summary>
    /// Called once the annotation has been added to a page, and so has an owning document.
    /// </summary>
    /// <remarks>
    /// An annotation constructed on its own has no Owner — PdfAnnotations.Add is what sets it.
    /// Anything an annotation has to put in the document rather than in its own dictionary,
    /// an appearance stream above all, therefore cannot be made until this is called.
    /// </remarks>
    internal virtual void OnAddedToPage()
    { }

    /// <summary>
    /// Called when a property that an appearance stream is drawn from has changed.
    /// </summary>
    /// <remarks>
    /// Only the properties an appearance depends on report here — its geometry, its colour and
    /// its opacity — and not the ones that merely describe it, such as the title or the contents.
    /// </remarks>
    internal virtual void OnAppearanceInvalidated()
    { }

    /// <summary>
    /// Gets or sets the annotation rectangle, defining the location of the annotation
    /// on the page in default user space units.
    /// </summary>
    public PdfRectangle Rectangle
    {
        get => Elements.GetRectangle(Keys.Rect, true);
        set
        {
            Elements.SetRectangle(Keys.Rect, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
            OnAppearanceInvalidated();
        }
    }

    /// <summary>
    /// Gets or sets the text label to be displayed in the title bar of the annotation’s
    /// pop-up window when open and active. By convention, this entry identifies
    /// the user who added the annotation.
    /// </summary>
    public string Title
    {
        get => Elements.GetString(Keys.T, true);
        set
        {
            Elements.SetString(Keys.T, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Gets or sets text representing a short description of the subject being
    /// addressed by the annotation.
    /// </summary>
    public string Subject
    {
        get => Elements.GetString(Keys.Subj, true);
        set
        {
            Elements.SetString(Keys.Subj, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Gets or sets the date and time when the annotation was created.
    /// </summary>
    public DateTime CreationDate
    {
        get => Elements.GetDateTime(Keys.CreationDate, DateTime.MinValue);
        set
        {
            Elements.SetDateTime(Keys.CreationDate, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Gets or sets the text to be displayed for the annotation or, if this type of
    /// annotation does not display text, an alternate description of the annotation’s
    /// contents in human-readable form.
    /// </summary>
    /// <remarks>
    /// Virtual for the one subtype that draws it. For every other annotation the contents are a
    /// description of what is drawn, so changing them redraws nothing; for a <c>/FreeText</c> they
    /// <em>are</em> what is drawn, and <see cref="PdfFreeTextAnnotation"/> overrides this to say so.
    /// </remarks>
    public virtual string Contents
    {
        get => Elements.GetString(Keys.Contents, true);
        set
        {
            Elements.SetString(Keys.Contents, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
        }
    }

    /// <summary>
    /// Gets or sets the color representing the components of the annotation. If the color
    /// has an alpha value other than 1, it is ignored. Use property Opacity to get or set the
    /// opacity of an annotation.
    /// </summary>
    public XColor Color
    {
        get
        {
            PdfItem item = Elements[Keys.C];
            PdfArray array = item as PdfArray;
            if (array != null)  // TODO: check for iref?
            {
                if (array.Elements.Count == 3)
                {
                    // TODO: an array.GetColor() function may be useful here
                    // Rounded rather than truncated. A component is written as a fraction of 255
                    // to the seven decimal places PdfWriter gives a real, so 127 goes out as
                    // 0.4980392 and comes back as 126.999996 - and truncating that loses a value
                    // the file all but said.
                    return XColor.FromArgb(
                        (int)Math.Round(array.Elements.GetReal(0) * 255),
                        (int)Math.Round(array.Elements.GetReal(1) * 255),
                        (int)Math.Round(array.Elements.GetReal(2) * 255));
                }
            }
            return XColors.Black;
        }
        set
        {
            // TODO: an array.SetColor(clr) function may be useful here
            PdfArray array = new PdfArray(Owner, new PdfReal[] { new(value.R / 255.0), new(value.G / 255.0), new(value.B / 255.0) });
            Elements[Keys.C] = array;
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
            OnAppearanceInvalidated();
        }
    }

    /// <summary>
    /// Gets or sets the constant opacity value to be used in painting the annotation.
    /// This value applies to all visible elements of the annotation in its closed state
    /// (including its background and border) but not to the popup window that appears when
    /// the annotation is opened.
    /// </summary>
    public double Opacity
    {
        get
        {
            if (!Elements.ContainsKey(Keys.CA))
                return 1;
            return Elements.GetReal(Keys.CA, true);
        }
        set
        {
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Opacity must be a value in the range from 0 to 1.");
            Elements.SetReal(Keys.CA, value);
            Elements.SetDateTime(Keys.M, GlobalTimeSettings.Now);
            OnAppearanceInvalidated();
        }
    }

    /// <summary>
    /// Gives this annotation the appearance a reader draws for it, from a form drawn with
    /// <see cref="XGraphics.FromForm"/>.
    /// </summary>
    /// <param name="form">
    /// The drawing. Its coordinates are the annotation's own space, so a form the size of
    /// <see cref="Rectangle"/> covers it exactly. Drawing on it is finished by this call and it
    /// cannot be drawn on afterwards.
    /// </param>
    /// <remarks>
    /// <para>
    /// Most annotation subtypes are drawn by the reader from their own entries, and need none of
    /// this. Four of them - <c>/Square</c>, <c>/Circle</c>, <c>/Line</c> and <c>/FreeText</c> -
    /// are drawn from <c>/AP</c> and from nothing else, so an annotation of one of those subtypes
    /// without an appearance is a rectangle a reader paints nothing into.
    /// </para>
    /// <para>
    /// The whole of what stood in the way of writing one was reach rather than capability:
    /// <see cref="XForm"/> has public constructors and <see cref="XGraphics.FromForm"/> draws onto
    /// it, but the form XObject underneath is internal, so the drawing could be made and not
    /// handed to anything.
    /// </para>
    /// </remarks>
    public void SetAppearance(XForm form)
    {
        PdfDictionary appearance = new PdfDictionary(RequireOwner());
        appearance.Elements["/N"] = FinishedForm(form).Reference;
        Elements[Keys.AP] = appearance;

        // A single appearance is not one of a set, so any state left naming one of a set would
        // now name something that is not there.
        Elements.Remove(Keys.AS);
    }

    /// <summary>
    /// Gives this annotation one of several appearances, each named, and shows the one named here.
    /// </summary>
    /// <param name="state">
    /// The name of the state this drawing is the appearance of - the value a check box or a radio
    /// button holds when it is showing it, such as <c>/Yes</c> or <c>/Off</c>. A leading solidus
    /// is added if it is left off.
    /// </param>
    /// <param name="form">The drawing, as for <see cref="SetAppearance(XForm)"/>.</param>
    /// <remarks>
    /// Calling this more than once adds to the set rather than replacing it, because that is what
    /// a set of states is for: a check box needs both its states in the file at once, and which of
    /// them is showing is <c>/AS</c>.
    /// </remarks>
    public void SetAppearance(string state, XForm form)
    {
        if (string.IsNullOrEmpty(state))
            throw new ArgumentException("An appearance state must be named.", nameof(state));

        string name = state[0] == '/' ? state : "/" + state;
        PdfDocument owner = RequireOwner();

        // The states already written are kept: /AP /N holds all of them, and /AS picks one.
        PdfDictionary appearance = Elements.GetDictionary(Keys.AP);
        if (appearance == null)
        {
            appearance = new PdfDictionary(owner);
            Elements[Keys.AP] = appearance;
        }

        PdfDictionary states = appearance.Elements.GetDictionary("/N");
        if (states == null || states.Elements.ContainsKey("/BBox"))
        {
            // Either nothing yet, or a single appearance set by the overload above - which is a
            // form XObject rather than a dictionary of states, and cannot be added to.
            states = new PdfDictionary(owner);
            appearance.Elements["/N"] = states;
        }

        states.Elements[name] = FinishedForm(form).Reference;
        Elements.SetName(Keys.AS, name);
    }

    /// <summary>
    /// Which of several named appearances is showing - <c>/AS</c>, with its solidus. Null when the
    /// annotation has a single appearance rather than a set of them, which is what an absent entry
    /// means.
    /// </summary>
    /// <remarks>
    /// <see cref="SetAppearance(string, PdfPinata.Drawing.XForm)"/> points this at whichever
    /// state it has just written, because an appearance nobody is showing is invisible and that is
    /// almost never what the caller meant by adding one. Setting the last state added is therefore
    /// the only thing needed for a check box; a radio button, where several widgets share a value
    /// and only one of them may be on, is what this exists for.
    /// </remarks>
    public string AppearanceState
    {
        get
        {
            string state = Elements.GetName(Keys.AS);
            return state.Length == 0 ? null : state;
        }
        set
        {
            if (value == null)
            {
                Elements.Remove(Keys.AS);
                return;
            }

            // Refused the same way SetAppearance(string, XForm) refuses it, and for the same
            // reason: /AS names a state, and the empty name names none. Checked here rather than
            // left to SetName, which would happily write a solidus and nothing after it.
            if (value.Length == 0)
                throw new ArgumentException("An appearance state must be named.", nameof(value));

            // SetName adds the solidus itself.
            Elements.SetName(Keys.AS, value);
        }
    }

    /// <summary>
    /// Finishes the form and hands back the object a reference can be taken to, having checked it
    /// belongs to the same document as this annotation.
    /// </summary>
    PdfFormXObject FinishedForm(XForm form)
    {
        ArgumentNullException.ThrowIfNull(form);

        if (form.Owner != Owner)
        {
            throw new ArgumentException(
                "The form was made for another document. An appearance and the annotation that "
                + "wears it must belong to the same one.", nameof(form));
        }

        // Closes the content stream and sets its length. Drawing on the form after this throws,
        // which is why it is documented on the two methods above rather than left to be found.
        form.DrawingFinished();

        // The getter adds the form to the reference table if it is not there, so the reference
        // read on the way back is always one that will be written.
        return form.PdfForm;
    }

    PdfDocument RequireOwner()
    {
        if (Owner == null)
        {
            throw new InvalidOperationException(
                "The annotation does not belong to a document yet. Add it to a page - "
                + "page.Annotations.Add(annotation) - before giving it an appearance.");
        }

        return Owner;
    }

    /// <summary>
    /// Checks a subtype is named at all and gives it the solidus a PDF name carries, for the
    /// annotations that are told their subtype rather than knowing it.
    /// </summary>
    /// <remarks>
    /// A dictionary with no <c>/Subtype</c> is not something a reader can act on: it ignores the
    /// annotation, and the mistake surfaces far away as "the annotation does nothing". Refusing it
    /// here turns that into an exception at the call that made it.
    /// </remarks>
    private protected static string SubtypeName(string subtype)
    {
        if (string.IsNullOrWhiteSpace(subtype))
            throw new ArgumentException("An annotation must name its subtype.", nameof(subtype));

        return subtype[0] == '/' ? subtype : "/" + subtype;
    }

    /// <summary>
    /// Turns the <c>/Name</c> of an annotation that names its icon into the member of
    /// <typeparamref name="T"/> that stands for it, or <paramref name="fallback"/> when the entry
    /// is absent or names something this enumeration does not have.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shared by the three subtypes that carry an icon name rather than copied into each of them.
    /// It was copied into each of them, and the copies drifted: two stripped the solidus and
    /// checked the member existed, and the third did neither, so
    /// <c>PdfFileAttachmentAnnotation.Icon</c> threw on every read it was ever given.
    /// </para>
    /// <para>
    /// The solidus is the trap. <c>Elements.GetName</c> hands back <c>PdfName.Value</c>, which
    /// carries it, so the first character of the returned string is never part of the member name -
    /// and <c>GetName</c> answers a missing key with <see cref="string.Empty"/> rather than
    /// <c>null</c>, so a null check for "no icon" never fires.
    /// </para>
    /// <para>
    /// <c>Enum.IsDefined</c> rather than <c>Enum.TryParse</c>: given a string of digits
    /// <c>TryParse</c> succeeds and hands back that number as the enumeration value, so a document
    /// naming its icon <c>/3</c> would read back as whichever member happens to be 3.
    /// </para>
    /// </remarks>
    private protected static T IconFromName<T>(string name, T fallback) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(name))
            return fallback;

        string member = name[0] == '/' ? name.Substring(1) : name;

        return Enum.IsDefined(typeof(T), member)
            ? Enum.Parse<T>(member, false)
            : fallback;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be Annot for an annotation dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "Annot")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) The type of annotation that this dictionary describes.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Subtype = "/Subtype";

        /// <summary>
        /// (Required) The annotation rectangle, defining the location of the annotation
        /// on the page in default user space units.
        /// </summary>
        [KeyInfo(KeyType.Rectangle | KeyType.Required)]
        public const string Rect = "/Rect";

        /// <summary>
        /// (Optional) Text to be displayed for the annotation or, if this type of annotation
        /// does not display text, an alternate description of the annotation’s contents
        /// in human-readable form. In either case, this text is useful when
        /// extracting the document’s contents in support of accessibility to users with
        /// disabilities or for other purposes.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Contents = "/Contents";

        // P

        /// <summary>
        /// (Optional; PDF 1.4) The annotation name, a text string uniquely identifying it
        /// among all the annotations on its page.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string NM = "/NM";

        /// <summary>
        /// (Optional; PDF 1.1) The date and time when the annotation was most recently
        /// modified. The preferred format is a date string, but viewer applications should be 
        /// prepared to accept and display a string in any format.
        /// </summary>
        [KeyInfo(KeyType.Date | KeyType.Optional)]
        public const string M = "/M";

        /// <summary>
        /// (Optional; PDF 1.1) A set of flags specifying various characteristics of the annotation.
        /// Default value: 0.
        /// </summary>
        [KeyInfo("1.1", KeyType.Integer | KeyType.Optional)]
        public const string F = "/F";

        /// <summary>
        /// (Optional; PDF 1.2) A border style dictionary specifying the characteristics of
        /// the annotation’s border.
        /// </summary>
        [KeyInfo("1.2", KeyType.Dictionary | KeyType.Optional)]
        public const string BS = "/BS";

        /// <summary>
        /// (Optional; PDF 1.2) An appearance dictionary specifying how the annotation
        /// is presented visually on the page. Individual annotation handlers may ignore
        /// this entry and provide their own appearances.
        /// </summary>
        [KeyInfo("1.2", KeyType.Dictionary | KeyType.Optional)]
        public const string AP = "/AP";

        /// <summary>
        /// (Required if the appearance dictionary AP contains one or more subdictionaries; PDF 1.2)
        /// The annotation’s appearance state, which selects the applicable appearance stream from 
        /// an appearance subdictionary.
        /// </summary>
        [KeyInfo("1.2", KeyType.Dictionary | KeyType.Optional)]
        public const string AS = "/AS";

        /// <summary>
        /// (Optional) An array specifying the characteristics of the annotation’s border.
        /// The border is specified as a rounded rectangle.
        /// In PDF 1.0, the array consists of three numbers defining the horizontal corner 
        /// radius, vertical corner radius, and border width, all in default user space units.
        /// If the corner radii are 0, the border has square (not rounded) corners; if the border 
        /// width is 0, no border is drawn.
        /// In PDF 1.1, the array may have a fourth element, an optional dash array defining a 
        /// pattern of dashes and gaps to be used in drawing the border. The dash array is 
        /// specified in the same format as in the line dash pattern parameter of the graphics state.
        /// For example, a Border value of [0 0 1 [3 2]] specifies a border 1 unit wide, with
        /// square corners, drawn with 3-unit dashes alternating with 2-unit gaps. Note that no
        /// dash phase is specified; the phase is assumed to be 0.
        /// Note: In PDF 1.2 or later, this entry may be ignored in favor of the BS entry.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Border = "/Border";

        /// <summary>
        /// (Optional; PDF 1.1) An array of three numbers in the range 0.0 to 1.0, representing
        /// the components of a color in the DeviceRGB color space. This color is used for the
        /// following purposes:
        /// • The background of the annotation’s icon when closed
        /// • The title bar of the annotation’s pop-up window
        /// • The border of a link annotation
        /// </summary>
        [KeyInfo("1.1", KeyType.Array | KeyType.Optional)]
        public const string C = "/C";

        /// <summary>
        /// (Optional; PDF 1.1) An action to be performed when the annotation is activated.
        /// Note: This entry is not permitted in link annotations if a Dest entry is present.
        /// Also note that the A entry in movie annotations has a different meaning.
        /// </summary>
        [KeyInfo("1.1", KeyType.Dictionary | KeyType.Optional)]
        public const string A = "/A";

        // AA
        // StructParent
        // OC

        // ----- Excerpt of entries specific to markup annotations ----------------------------------

        /// <summary>
        /// (Optional; PDF 1.1) The text label to be displayed in the title bar of the annotation’s
        /// pop-up window when open and active. By convention, this entry identifies
        /// the user who added the annotation.
        /// </summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string T = "/T";

        /// <summary>
        /// (Optional; PDF 1.3) An indirect reference to a pop-up annotation for entering or
        /// editing the text associated with this annotation.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Popup = "/Popup";

        /// <summary>
        /// (Optional; PDF 1.4) The constant opacity value to be used in painting the annotation.
        /// This value applies to all visible elements of the annotation in its closed state
        /// (including its background and border) but not to the popup window that appears when
        /// the annotation is opened.
        /// The specified value is not used if the annotation has an appearance stream; in that
        /// case, the appearance stream must specify any transparency. (However, if the viewer
        /// regenerates the annotation’s appearance stream, it may incorporate the CA value
        /// into the stream’s content.)
        /// The implicit blend mode is Normal.
        /// Default value: 1.0.
        /// </summary>
        [KeyInfo(KeyType.Real | KeyType.Optional)]
        public const string CA = "/CA";

        //RC

        /// <summary>
        /// (Optional; PDF 1.5) The date and time when the annotation was created.
        /// </summary>
        [KeyInfo("1.5", KeyType.Date | KeyType.Optional)]
        public const string CreationDate = "/CreationDate";

        //IRT

        /// <summary>
        /// (Optional; PDF 1.5) Text representing a short description of the subject being
        /// addressed by the annotation.
        /// </summary>
        [KeyInfo("1.5", KeyType.TextString | KeyType.Optional)]
        public const string Subj = "/Subj";

        //RT
        //IT
        // ReSharper restore InconsistentNaming
    }
}
