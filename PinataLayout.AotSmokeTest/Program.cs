using System;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
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
/// </remarks>
public static class Program
{
    static int failures;

    public static int Main()
    {
        Console.WriteLine("PinataLayout AOT smoke test");

        // Neither PdfPinata nor PinataLayout ships a font or imaging backend.
        PdfPinata.Fonts.GlobalFontSettings.FontResolver = new PdfPinata.Utils.SkiaFontResolver();
        DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl = new SkiaImageSource();

        Document document = BuildDocument();

        CheckValueModel(document);
        CheckDdlRoundTrip(document);
        CheckRendersToPdf(document);

        if (failures > 0)
        {
            Console.WriteLine($"FAILED: {failures} check(s)");
            return 1;
        }

        Console.WriteLine("All checks passed.");
        return 0;
    }

    static Document BuildDocument()
    {
        var document = new Document();
        document.Info.Title = "AOT smoke test";

        Style style = document.Styles.AddStyle("Highlight", "Normal");
        style.Font.Bold = true;
        style.Font.Color = Colors.DarkBlue;

        Section section = document.AddSection();
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);

        Paragraph paragraph = section.AddParagraph("Rendered from a natively compiled binary.");
        paragraph.Format.Alignment = ParagraphAlignment.Justify;
        paragraph.Format.SpaceAfter = Unit.FromPoint(6);
        paragraph.Format.Borders.Top.Style = BorderStyle.DashDot;
        paragraph.Format.Borders.Top.Width = Unit.FromPoint(1);
        paragraph.Format.Borders.Top.Color = Colors.Firebrick;

        section.AddParagraph("Styled.").Style = "Highlight";

        Table table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.AddColumn(Unit.FromCentimeter(4));
        table.AddColumn(Unit.FromCentimeter(4));
        Row row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        row.Cells[0].AddParagraph("Left");
        row.Cells[1].AddParagraph("Right");

        return document;
    }

    /// <summary>
    /// The generated value model, reached the way the DDL parser reaches it: by name.
    /// </summary>
    static void CheckValueModel(Document document)
    {
        Section section = document.LastSection;
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
        ValueDescriptor parent = Meta.GetMeta(paragraph)["parent"];
        Check("parent is RefOnly", parent is { IsRefOnly: true });

        // The descriptor table itself survived native compilation.
        Check("descriptor table is populated", Meta.GetMeta(paragraph.Format).ValueDescriptors.Count > 10);
    }

    /// <summary>
    /// DDL is the value model's real client: it resolves every name through Meta.
    /// </summary>
    static void CheckDdlRoundTrip(Document document)
    {
        string ddl = DdlWriter.WriteToString(document);
        Check("DDL is written", ddl.Length > 200 && ddl.Contains("\\document"));
        Check("assigned enum is written", ddl.Contains("DashDot"));
        Check("unassigned member is not written", !ddl.Contains("OutlineLevel = BodyText"));

        Document reread = DdlReader.DocumentFromString(ddl);
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

    static void CheckRendersToPdf(Document document)
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
    }

    static void Check(string what, bool ok)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok)
            failures++;
    }
}
