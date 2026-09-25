using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   An interactive form - text boxes, check boxes, radio buttons, a combo box, a list box and a
///   push button - built through the typed AcroForm API.
/// </summary>
/// <remarks>
///   <para>
///     This demo used to be the odd one out. Every other demo called a method the library offers;
///     this one wrote dictionaries by hand, because <see cref="PdfAcroForm"/> and every
///     <see cref="PdfAcroField"/> under it had an <c>internal</c> constructor, the field collection
///     had no <c>Add</c>, and <see cref="PdfWidgetAnnotation"/> was internal too. The typed API
///     could fill in a form somebody else wrote. It could not make one.
///   </para>
///   <para>
///     It can now, and this is what that looks like: no <c>PdfDictionary</c>, no content-stream
///     operators, and every appearance stream drawn with <see cref="XGraphics"/> like the rest of
///     the library. Page two sets out what changed and what is still the caller's problem.
///   </para>
/// </remarks>
internal sealed class FormsDemo : PdfDemo
{
    public FormsDemo() : base() { }

    public override string Name => "Forms";

    public override string Summary => "An interactive AcroForm: text, choice and button fields.";

    public override IReadOnlyList<string> Shows => [
        "Text fields - single line, required, multiline, and a password",
        "A check box and a radio group, each with the appearance streams a viewer toggles between",
        "A combo box and a list box, both from an /Opt array, one of them editable",
        "A push button carrying a URI action",
        "That every field here is made through the typed API rather than assembled by hand"
    ];

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        const string Sans = "Liberation Sans";

        var document = new PdfDocument();
        document.Info.Title = "Interactive form";

        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont(Sans, 18, XFontStyle.Bold);
        var labelFont = new XFont(Sans, 9, XFontStyle.Bold);
        var noteFont = new XFont(Sans, 7.5);

        // ---- The form ----------------------------------------------------------------
        //
        // docs:begin create-form
        // GetOrCreateAcroForm makes the form, makes it indirect and puts it in the catalogue.
        // PdfDocument.AcroForm only reads, and answers null until this has been called.
        var form = document.GetOrCreateAcroForm();

        // Every field below draws its own appearance, so the form reads the same in a viewer that
        // builds nothing - Ghostscript, a print pipeline, a thumbnailer. /NeedAppearances asks a
        // viewer that can to build them again anyway, which is what reflows the multi-line notes:
        // the library draws a value as one line. Leave it out of a PDF/A document, which forbids it.
        form.NeedAppearances = true;

        // A real size, not the "/Helv 0 Tf" most examples show. Zero means auto-size, and what a
        // viewer makes of that on a multiline box is its own business: Ghostscript scales the
        // first line to the height of the whole box, which fills the page with one word.
        form.DefaultAppearance = "/Helv 9 Tf 0 g";

        // The two standard-14 faces a form needs, named as /DA refers to them. Neither is
        // embedded and neither has to be: these are the faces every viewer already has.
        form.AddStandardFont("/Helv", "/Helvetica");
        form.AddStandardFont("/ZaDb", "/ZapfDingbats");
        // docs:end create-form

        gfx.DrawString("Interactive form", titleFont, XBrushes.Black, new XPoint(56, 68));
        gfx.DrawLine(new XPen(XColors.SteelBlue, 1.5), 56, 78, 539, 78);
        gfx.DrawString("Open this in a reader that supports forms - the fields below are fillable.",
            noteFont, XBrushes.DimGray, new XPoint(56, 92));

        const double FieldX = 210;
        const double FieldW = 300;

        // One cursor down the page, rather than a measured coordinate per row. The note under
        // each field is what forces it: the notes are wider than the label column, so anything
        // that placed them by hand would sooner or later run one of them under a field box.
        double cursor = 116;

        XRect Row(string label, double height)
        {
            gfx.DrawString(label, labelFont, XBrushes.Black, new XPoint(56, cursor + 12));
            return new XRect(FieldX, cursor, FieldW, height);
        }

        void EndRow(XRect box, string note)
        {
            gfx.DrawString(note, noteFont, XBrushes.DimGray,
                new XPoint(FieldX, box.Bottom + 12));
            cursor = box.Bottom + 28;
        }

        // ---- Putting a field on the page ---------------------------------------------
        //
        // docs:begin place
        // A field says what it is and what it holds; a widget says where on a page it is drawn.
        // AddWidget makes one and links the two - always a separate annotation under /Kids, so a
        // field that gains a second widget later does not change shape.
        //
        // The drawing above is in world space, measured down from the top left. A widget is
        // placed in default page space, measured up from the bottom left.
        PdfWidgetAnnotation Place(PdfAcroField field, XRect box)
        {
            return field.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box)));
        }
        // docs:end place

        // docs:begin style-text
        // A text field draws its own appearance out of these, from the value in /V - so the box a
        // reader shows is the library's drawing rather than something built from /MK, and naming
        // the colours here is what stops a field losing its box the moment it is given a value.
        // The size in /DA is the field's own rather than the form's, for the same reason the form
        // names one at all.
        void StyleText(PdfTextField field)
        {
            field.BackColor = XColor.FromArgb(245, 245, 245);
            field.BorderColor = XColor.FromArgb(115, 115, 115);
            field.DefaultAppearance = "/Helv 9 Tf 0 g";
        }
        // docs:end style-text

        // A combo box and a list box draw their own appearance too, in the size and colour their
        // /DA names, so they take the same box as the text fields.
        void StyleChoice(PdfChoiceField field)
        {
            field.BackColor = XColor.FromArgb(245, 245, 245);
            field.BorderColor = XColor.FromArgb(115, 115, 115);
            field.DefaultAppearance = "/Helv 9 Tf 0 g";
        }

        // An appearance stream is a form XObject, and XGraphics draws onto one exactly as it
        // draws onto a page - which is what SetAppearance takes. This demo's first draft drew
        // its radio rings with an "arc" operator, which is PostScript: a PDF path knows only
        // m, l, c, v, y, re and h, and a viewer handed an operator it does not know draws
        // nothing and reports nothing. Drawing through XGraphics puts that mistake out of reach.
        XForm Appearance(XRect box, Action<XGraphics> draw)
        {
            var appearance = new XForm(document, new XSize(box.Width, box.Height));
            using var into = XGraphics.FromForm(appearance);
            draw(into);
            return appearance;
        }

        // ---- Text fields -------------------------------------------------------------
        // docs:begin text-fields
        var fullNameBox = Row("Full name", 20);
        var fullName = new PdfTextField(document)
        {
            Name = "fullName",
            ToolTip = "Your name as it appears on your passport"
        };
        form.Fields.Add(fullName);
        StyleText(fullName);
        Place(fullName, fullNameBox);
        fullName.Text = "Ada Lovelace";
        EndRow(fullNameBox, "A value in /V, a tooltip in /TU, and a box the library draws itself.");

        var emailBox = Row("Email", 20);
        var email = new PdfTextField(document)
        {
            Name = "email",
            ToolTip = "Required - we will not use it for anything",
            Flags = PdfAcroFieldFlags.Required
        };
        form.Fields.Add(email);
        StyleText(email);
        Place(email, emailBox);
        EndRow(emailBox, "Required, so a reader marks it when the form is submitted empty.");
        // docs:end text-fields

        var secretBox = Row("Passphrase", 20);
        var secret = new PdfTextField(document)
        {
            Name = "secret",
            ToolTip = "Typed back as bullets",
            Password = true
        };
        form.Fields.Add(secret);
        StyleText(secret);
        Place(secret, secretBox);
        // The flag masks what is typed and nothing more. ISO 32000-1 Table 228 adds only a note
        // that a reader "should never store the value", which is advice to the reader rather than
        // a guarantee to the author - so a form field is not somewhere to keep a secret.
        EndRow(secretBox, "Password: echoed as bullets. Advisory only - not secret storage.");

        var postcodeBox = Row("Postcode", 20);
        var postcode = new PdfTextField(document)
        {
            Name = "postcode",
            ToolTip = "Six cells, one character each",
            MaxLength = 6,
            Flags = PdfAcroFieldFlags.Comb
        };
        form.Fields.Add(postcode);
        StyleText(postcode);
        Place(postcode, postcodeBox);
        // Comb is bit 25, and was the one field flag PdfAcroFieldFlags did not have. It divides
        // the box into as many equal cells as /MaxLen allows characters, which is how a form
        // draws the boxes for a postcode or a card number.
        EndRow(postcodeBox, "Comb + MaxLength: one character per cell, evenly spaced.");

        var notesBox = Row("Notes", 56);
        var notes = new PdfTextField(document)
        {
            Name = "notes",
            ToolTip = "Anything else we should know",
            MultiLine = true
        };
        form.Fields.Add(notes);
        StyleText(notes);
        Place(notes, notesBox);
        notes.Text = "Multiline: this box wraps and scrolls.";
        EndRow(notesBox, "Multiline, so a reader wraps the value rather than scrolling it sideways.");

        // ---- A check box -------------------------------------------------------------
        //
        // Both states are drawn here rather than left to /NeedAppearances. What a check box
        // shows IS its value, so the two streams are the field rather than a rendering of it.
        // docs:begin check-box
        var tickBox = Row("Subscribe", 16);
        tickBox = new XRect(tickBox.X, tickBox.Y, 16, 16);

        var subscribe = new PdfCheckBoxField(document)
        {
            Name = "subscribe",
            ToolTip = "Send me the newsletter"
        };
        form.Fields.Add(subscribe);
        var tick = Place(subscribe, tickBox);

        var boxOutline = new XPen(XColors.Gray, 1);
        var inside = new XRect(0.5, 0.5, 15, 15);

        tick.SetAppearance("/Yes", Appearance(tickBox, into =>
        {
            into.DrawRectangle(boxOutline, XBrushes.White, inside);
            into.DrawLines(new XPen(XColors.Black, 2),
                [new XPoint(3.5, 8), new XPoint(6.5, 11.5), new XPoint(12.5, 4.5)]);
        }));
        tick.SetAppearance("/Off", Appearance(tickBox, into =>
            into.DrawRectangle(boxOutline, XBrushes.White, inside)));

        // /AS names which of the two streams is showing; /V is the field's value. Checked keeps
        // them in step, reading the names out of the appearances the widget was just given.
        subscribe.Checked = true;
        EndRow(tickBox, "/AP /N holds one stream per state; /AS names the one on show.");
        // docs:end check-box

        // ---- A radio group -----------------------------------------------------------
        //
        // One field, three widgets. The field holds the name and the value; each widget's "on"
        // state is named after the choice it stands for, and that name is what /V is compared
        // against - so the two have to agree exactly.
        var deliveryRow = Row("Delivery", 14);

        // docs:begin radio-field
        var delivery = new PdfRadioButtonField(document)
        {
            Name = "delivery",
            ToolTip = "How soon do you want it",
            Flags = PdfAcroFieldFlags.Radio | PdfAcroFieldFlags.NoToggleToOff
        };
        form.Fields.Add(delivery);

        string[] choices = ["Standard", "Express", "Collect"];
        delivery.Options = choices;
        // docs:end radio-field

        const int Chosen = 0;

        // docs:begin radio-widgets
        for (var index = 0; index < choices.Length; index++)
        {
            var dot = new XRect(FieldX + index * 100, deliveryRow.Y, 14, 14);
            var button = Place(delivery, dot);

            var ring = new XRect(0.5, 0.5, 13, 13);
            var pip = new XRect(3.5, 3.5, 7, 7);

            button.SetAppearance("/" + choices[index], Appearance(dot, into =>
            {
                into.DrawEllipse(boxOutline, XBrushes.White, ring);
                into.DrawEllipse(XBrushes.Black, pip);
            }));
            button.SetAppearance("/Off", Appearance(dot, into =>
                into.DrawEllipse(boxOutline, XBrushes.White, ring)));

            // Both states are in the file; /AS picks the one on show. SetAppearance points it at
            // whichever it has just written, so exactly one button has to be told otherwise -
            // and a radio group where two are on is the mistake this prevents.
            button.AppearanceState = index == Chosen ? "/" + choices[index] : "/Off";

            gfx.DrawString(choices[index], noteFont, XBrushes.Black,
                new XPoint(FieldX + index * 100 + 19, deliveryRow.Y + 11));
        }

        // Which one the field holds. SelectedIndex looks the choice up in /Opt and writes /V as
        // the name the chosen widget's "on" state is called by.
        delivery.SelectedIndex = Chosen;
        // docs:end radio-widgets
        EndRow(deliveryRow,
            "One field, three widgets under /Kids. The field holds the name and the value.");

        // ---- Choice fields -----------------------------------------------------------
        var countryBox = Row("Country", 20);
        // docs:begin combo-box
        var country = new PdfComboBoxField(document)
        {
            Name = "country",
            ToolTip = "Pick one, or type your own",
            Flags = PdfAcroFieldFlags.Combo | PdfAcroFieldFlags.Edit | PdfAcroFieldFlags.Sort,
            Options = [
                "Australia", "Canada", "Ireland", "New Zealand", "United Kingdom"
            ]
        };
        form.Fields.Add(country);
        StyleChoice(country);
        Place(country, countryBox);
        country.SelectedIndex = 4;
        // docs:end combo-box
        EndRow(countryBox,
            "Combo + Edit, so the list can also be typed into. Sort orders it for display.");

        var interestsBox = Row("Interests", 56);
        // docs:begin list-box
        var interests = new PdfListBoxField(document)
        {
            Name = "interests",
            ToolTip = "Choose as many as you like",
            Flags = PdfAcroFieldFlags.MultiSelect,
            Options = [
                "Typography", "Colour management", "Page imposition", "Tagged PDF"
            ]
        };
        form.Fields.Add(interests);
        StyleChoice(interests);
        Place(interests, interestsBox);
        interests.SelectedIndices = [0, 3];
        // docs:end list-box
        EndRow(interestsBox,
            "A list box is a choice field without the Combo flag. /I carries the selected rows.");

        // ---- A push button -----------------------------------------------------------
        //
        // A push button has no value at all - it exists for its action. This one opens a URL,
        // which lives on the widget rather than on the field, because an action is something a
        // person does to an annotation.
        var buttonBox = Row("Then", 24);
        buttonBox = new XRect(buttonBox.X, buttonBox.Y, 140, 24);

        // docs:begin push-button
        var help = new PdfPushButtonField(document)
        {
            Name = "help",
            ToolTip = "Opens the PdfPinata repository"
        };
        form.Fields.Add(help);
        var face = Place(help, buttonBox);

        face.Elements["/A"] = new PdfLiteral(
            "<</S/URI/URI(https://github.com/PinataLabs/PdfPinata)>>");

        var caption = new PdfDictionary(document);
        caption.Elements.SetString("/CA", "Read the manual");
        face.Elements["/MK"] = caption;

        // Unlike the text fields, a push button gets no help from /NeedAppearances, so its face
        // is drawn here - with the same XGraphics calls that drew the page.
        var buttonFont = new XFont(Sans, 10);
        face.SetAppearance(Appearance(buttonBox, into =>
        {
            into.DrawRectangle(new XPen(XColor.FromArgb(89, 115, 153), 1),
                new XSolidBrush(XColor.FromArgb(217, 227, 240)), new XRect(0.5, 0.5, 139, 23));
            into.DrawString("Read the manual", buttonFont,
                new XSolidBrush(XColor.FromArgb(26, 51, 89)),
                new XRect(0, 0, 140, 24), XStringFormats.Center);
        }));
        // docs:end push-button
        EndRow(buttonBox,
            "A push button carries an action instead of a value: /A here is a URI action.");

        // ---- Page two: what the typed API can and cannot do ---------------------------
        var notesPage = document.AddPage();
        var notesGfx = XGraphics.FromPdfPage(notesPage);

        notesGfx.DrawString("What the typed AcroForm API does", titleFont, XBrushes.Black,
            new XPoint(56, 68));
        notesGfx.DrawLine(new XPen(XColors.SteelBlue, 1.5), 56, 78, 539, 78);

        var body = new XFont(Sans, 9.5);
        var mono = new XFont("Source Code Pro", 8.5);

        string[] paragraphs =
        [
            "Every field on page one is a PdfTextField, PdfCheckBoxField, PdfRadioButtonField,",
            "PdfComboBoxField, PdfListBoxField or PdfPushButtonField, made with new, named, given",
            "flags, added to the form and put on the page. None of it is assembled by hand.",
            "",
            "It used to be. Every constructor under PdfPinata.Pdf.AcroForms was internal,",
            "PdfAcroFieldCollection had no Add, PdfWidgetAnnotation was internal and there was no",
            "way to make a form at all - so the only route was to write the dictionaries of",
            "ISO 32000-1 section 12.7 yourself and hang them off the catalogue's /AcroForm."
        ];

        double lineY = 104;
        foreach (var paragraph in paragraphs)
        {
            notesGfx.DrawString(paragraph, body, XBrushes.Black, new XPoint(56, lineY));
            lineY += 14;
        }

        (string Capability, string State)[] table =
        [
            ("Make a form", "PdfDocument.GetOrCreateAcroForm()"),
            ("Create a field of any type", "new PdfTextField(document), and so on"),
            ("Add a field to a form or a field", "PdfAcroFieldCollection.Add"),
            ("Put a field on a page", "PdfAcroField.AddWidget"),
            ("Name it, describe it, flag it", "Name, ToolTip, Flags"),
            ("Give a widget an appearance", "PdfAnnotation.SetAppearance"),
            ("Offer choices", "PdfChoiceField.Options"),
            ("Read and fill a form somebody wrote", "AcroForm.Fields[name].Value"),
            ("Make every field read-only", "PdfDocument.MakeAcroFormsReadOnly()"),
            ("Sign a document", "PdfPinata.Signing - see the Signing demo"),
            ("Flatten a form into page content", "not offered")
        ];

        lineY += 16;
        notesGfx.DrawString("One capability to a line, and where it lives", labelFont,
            XBrushes.Black, new XPoint(56, lineY));
        lineY += 8;
        notesGfx.DrawLine(XPens.LightGray, 56, lineY, 539, lineY);
        lineY += 16;

        foreach (var row in table)
        {
            notesGfx.DrawString(row.Capability, body, XBrushes.Black, new XPoint(56, lineY));
            notesGfx.DrawString(row.State, mono, XBrushes.DimGray, new XPoint(250, lineY));
            lineY += 16;
        }

        lineY += 14;
        string[] closing =
        [
            "Two entries above are still written by name, because nothing wraps them: the push",
            "button's /MK, which carries its caption, and its /A action. Everything else on page",
            "one goes through a property or a method.",
            "",
            "One rule to know. A partial field name may not contain a period, because a period is",
            "what joins nested names into the path a field is found by - so Name = \"name.full\" is",
            "refused at the call rather than left to produce a field nobody can look up."
        ];

        foreach (var line in closing)
        {
            notesGfx.DrawString(line, body, XBrushes.Black, new XPoint(56, lineY));
            lineY += 14;
        }
        #endregion

        return document;
    }
}
