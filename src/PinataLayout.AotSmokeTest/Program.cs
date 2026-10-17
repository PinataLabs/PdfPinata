using System;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Skia;

namespace PinataLayout.AotSmokeTest;

/// <summary>
/// Publishes native and exercises the DOM's name-addressable value model end to end.
/// </summary>
/// <remarks>
/// <para>
/// The trim analyzer proves there is no statically visible reflection left in the value model. It
/// cannot prove there is none at all, and it says nothing about whether what it produced runs. This
/// does: if the generated model ever regresses to something that needs metadata at run time, the
/// checks below fail in the native binary even though the managed build was clean.
/// </para>
/// <para>
/// Written against the paths that used to reflect - GetValue and SetValue by dotted name, IsNull,
/// SetNull, CreateValue, and the DDL reader and writer that drive all of them - rather than against
/// the typed API, which never reflected and would pass either way.
/// </para>
/// <para>
/// It is also the one place a document is rendered with InMemoryFontResolver as its only source of
/// fonts: the global resolver can be set once a process, and the test suites' is set already. The
/// resolver for installed fonts, which it rendered through before, is still run, called directly.
/// </para>
/// </remarks>
public static class Program
{
    private static int failures;

    public static int Main()
    {
        Console.WriteLine("PinataLayout AOT smoke test");

        // Neither PdfPinata nor PinataLayout ships a font or imaging backend. The fonts are the
        // app's own, served from memory, so that the document renders from nothing the machine has
        // installed - the only way it can render in a browser.
        PdfPinata.Fonts.GlobalFontSettings.FontResolver = EmbeddedFonts();
        DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl = new SkiaImageSource();

        var document = BuildDocument();

        Check("default font is the family added", document.Styles.Normal.Font.Name == "Liberation Sans");

        CheckValueModel(document);
        CheckDdlRoundTrip(document);
        CheckRendersToPdf(document);
        CheckInstalledFonts();

        if (failures > 0)
        {
            Console.WriteLine($"FAILED: {failures} check(s)");
            return 1;
        }

        Console.WriteLine("All checks passed.");
        return 0;
    }

    private static PdfPinata.Utils.InMemoryFontResolver EmbeddedFonts()
    {
        var fonts = new PdfPinata.Utils.InMemoryFontResolver();
        var assembly = typeof(Program).Assembly;

        foreach (var face in new[] { "Regular", "Bold", "Italic", "BoldItalic" })
        {
            using var stream = assembly.GetManifestResourceStream($"Fonts.LiberationSans-{face}.ttf")
                               ?? throw new InvalidOperationException($"The {face} face is not embedded.");
            fonts.AddFont(stream);
        }

        return fonts;
    }

    private static Document BuildDocument()
    {
        var document = new Document { Info = { Title = "AOT smoke test" } };

        var style = document.Styles.AddStyle("Highlight", "Normal");
        style.Font.Bold = true;
        style.Font.Color = Colors.DarkBlue;

        var section = document.AddSection();
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);

        var paragraph = section.AddParagraph("Rendered from a natively compiled binary.");
        paragraph.Format.Alignment = ParagraphAlignment.Justify;
        paragraph.Format.SpaceAfter = Unit.FromPoint(6);
        paragraph.Format.Borders.Top.Style = BorderStyle.DashDot;
        paragraph.Format.Borders.Top.Width = Unit.FromPoint(1);
        paragraph.Format.Borders.Top.Color = Colors.Firebrick;

        section.AddParagraph("Styled.").Style = "Highlight";

        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.AddColumn(Unit.FromCentimeter(4));
        table.AddColumn(Unit.FromCentimeter(4));
        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        row.Cells[0].AddParagraph("Left");
        row.Cells[1].AddParagraph("Right");

        return document;
    }

    /// <summary>
    /// The generated value model, reached the way the DDL parser reaches it: by name.
    /// </summary>
    private static void CheckValueModel(Document document)
    {
        var section = document.LastSection;
        var paragraph = (Paragraph)section.Elements[0];

        // Leaf, through a dotted path.
        Check("dotted GetValue", Equals(paragraph.GetValue("Format.Alignment"), ParagraphAlignment.Justify));

        // The enum that used to be an NEnum, and its unset-reads-as-zero rule.
        Check("unset enum is null", paragraph.Format.IsNull("OutlineLevel"));
        Check("unset enum reads as zero", Equals(paragraph.GetValue("Format.OutlineLevel"), OutlineLevel.BodyText));
        Check("unset enum under GetNull", paragraph.Format.GetValue("OutlineLevel", GV.GetNull) is null);

        // NullableValue: a struct that tracks its own null.
        Check("Unit round-trips by name", Equals(paragraph.GetValue("Format.SpaceAfter"), Unit.FromPoint(6)));

        // SetValue by name, then read it back.
        paragraph.SetValue("Format.Alignment", ParagraphAlignment.Center);
        Check("SetValue by name", paragraph.Format.Alignment == ParagraphAlignment.Center);

        // SetNull by name.
        paragraph.Format.SetNull("Alignment");
        Check("SetNull by name", paragraph.Format.IsNull("Alignment"));
        paragraph.Format.Alignment = ParagraphAlignment.Justify;

        // Case-insensitive lookup, which DDL depends on.
        Check("case-insensitive lookup", paragraph.Format.HasValue("ALIGNMENT") && paragraph.Format.HasValue("alignment"));

        // A DocumentObject member created on demand, which is what GV.ReadWrite is for.
        var borders = paragraph.Format.GetValue("Borders", GV.ReadWrite) as Borders;
        Check("auto-created DocumentObject", borders is not null);

        // CreateValue, the last caller of the old Activator path.
        Check("CreateValue", document.CreateValue("Info") is DocumentInfo);

        // RefOnly: parent must never be walked, or IsNull recurses up the tree forever.
        var parent = Meta.GetMeta(paragraph)["parent"];
        Check("parent is RefOnly", parent is { IsRefOnly: true });

        // The descriptor table itself survived native compilation.
        Check("descriptor table is populated", Meta.GetMeta(paragraph.Format).ValueDescriptors.Count > 10);
    }

    /// <summary>
    /// DDL is the value model's real client: it resolves every name through Meta.
    /// </summary>
    private static void CheckDdlRoundTrip(Document document)
    {
        var ddl = DdlWriter.WriteToString(document);
        Check("DDL is written", ddl.Length > 200 && ddl.Contains("\\document"));
        Check("assigned enum is written", ddl.Contains("DashDot"));
        Check("unassigned member is not written", !ddl.Contains("OutlineLevel = BodyText"));

        var reread = DdlReader.DocumentFromString(ddl);
        var paragraph = (Paragraph)reread.LastSection.Elements[0];

        Check("enum survives DDL", paragraph.Format.Borders.Top.Style == BorderStyle.DashDot);
        Check("unit survives DDL", paragraph.Format.Borders.Top.Width == Unit.FromPoint(1));
        Check("colour survives DDL", paragraph.Format.Borders.Top.Color == Colors.Firebrick);
        Check("alignment survives DDL", paragraph.Format.Alignment == ParagraphAlignment.Justify);
        Check("info survives DDL", reread.Info.Title == "AOT smoke test");
        Check("style survives DDL", reread.Styles["Highlight"] is not null);

        // Writing what was read must produce the same DDL, or something is lost in one direction.
        Check("DDL is stable", DdlWriter.WriteToString(reread) == ddl);
    }

    private static void CheckRendersToPdf(Document document)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var buffer = new MemoryStream();
        renderer.PdfDocument.Save(buffer, false);

        Check("PDF has pages", renderer.PdfDocument.PageCount >= 1);
        Check("PDF is non-trivial", buffer.Length > 1000);

        buffer.Position = 0;
        Span<byte> header = stackalloc byte[5];
        buffer.ReadExactly(header);
        Check("PDF header", header is [0x25, 0x50, 0x44, 0x46, 0x2D]);   // %PDF-

        var pdf = System.Text.Encoding.Latin1.GetString(buffer.ToArray());
        Check("fonts embedded from memory", pdf.Contains("+Liberation#20Sans"));

        // The styled paragraph's bold is the bold face added, not the regular face drawn heavier.
        // Neither a font's name nor its subset tag says which face its program was cut from - the
        // tag is random, and a simulated bold can be a font of its own with ",Bold" in its name - so
        // this reads the programs themselves. A subset keeps the face's 'head' table, and macStyle
        // there says whether the face is bold.
        buffer.Position = 0;
        using var reread = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);
        var bold = 0;
        var regular = 0;
        foreach (var item in reread.Internals.GetAllObjects())
        {
            // A /FontFile2 stream is the one stream that carries /Length1.
            if (item is not PdfDictionary { Stream: not null } program || !program.Elements.ContainsKey("/Length1"))
                continue;

            if (IsBoldFace(program.Stream.UnfilteredValue))
                bold++;
            else
                regular++;
        }

        Check("regular face embedded", regular > 0);
        Check("bold face embedded from the bold font, not simulated", bold > 0);
    }

    /// <summary>
    /// The resolver for the fonts installed on the machine, which this binary rendered through before
    /// it took its fonts from memory. It cannot be registered as well - the global resolver is set
    /// once a process - so it is called directly: its scan of the platform's font folders, the
    /// metadata it reads from every file it finds, and the bytes it serves for the face it chooses.
    /// </summary>
    private static void CheckInstalledFonts()
    {
        try
        {
            var installed = new PdfPinata.Utils.SkiaFontResolver();
            var info = installed.ResolveTypeface(installed.DefaultFontName, false, false);
            Check("installed fonts resolve", info is not null);
            if (info is null)
                return;

            var family = PdfPinata.Utils.OpenTypeFontMetadata.Read(installed.GetFont(info.FaceName)).FamilyName;
            Check($"installed font served ({family})", family.Length > 0);
        }
        catch (Exception ex)
        {
            Check($"installed fonts resolve ({ex.GetType().Name}: {ex.Message})", false);
        }
    }

    /// <summary>
    /// Whether a TrueType font program says it is a bold face: bit 0 of macStyle in its 'head' table.
    /// </summary>
    private static bool IsBoldFace(byte[] font)
    {
        static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

        var tableCount = U16(font, 4);
        for (var table = 0; table < tableCount; table++)
        {
            var record = 12 + table * 16;
            if (font[record] != 'h' || font[record + 1] != 'e' || font[record + 2] != 'a' || font[record + 3] != 'd')
                continue;

            var head = (font[record + 8] << 24) | (font[record + 9] << 16) | (font[record + 10] << 8) | font[record + 11];
            return (U16(font, head + 44) & 1) != 0;
        }

        return false;
    }

    private static void Check(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok)
            failures++;
    }
}
