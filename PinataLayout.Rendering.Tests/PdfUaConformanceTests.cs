using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Tagging a document and conforming to PDF/UA are not the same thing, and the gap is mostly rules
///   that are cheap to check and easy to break. These are the ones the writer holds a document to.
/// </summary>
/// <remarks>
///   The refusal is the point. Stamping <c>pdfuaid:part 1</c> on a file and leaving the caller to
///   hear from a validator — or from their customer — that it does not conform makes things worse
///   rather than better, and an accessibility claim is read by a procurement officer.
///   <para>
///   A successful save is still not a validator's verdict. What is checked is listed on
///   <c>PdfUaValidator</c>, and it is not the whole standard: veraPDF has the last word, and it is
///   not in CI yet.
///   </para>
/// </remarks>
public class PdfUaConformanceTests
{
    [Fact]
    public void AClaimingDocumentSaysSoInItsMetadata()
    {
        var saved = Save(Claiming());

        // XMP is the only place a PDF/UA claim can be made — unlike PDF/A there is no dictionary
        // entry for it, so a document with a perfect tree and no identifier claims nothing at all.
        var metadata = MetadataOf(saved);
        metadata.Should().Contain("http://www.aiim.org/pdfua/ns/id/");
        metadata.Should().Contain("<pdfuaid:part>1</pdfuaid:part>");

        // PDF/UA-1 has parts and no conformance levels, where PDF/A has both. Writing one to match
        // the pdfaid pair is a common mistake and a validator objects to it.
        metadata.Should().NotContain("pdfuaid:conformance");
    }

    [Fact]
    public void AClaimingDocumentAsksThatItsTitleBeShownRatherThanItsFileName()
    {
        var saved = Save(Claiming());

        // Set rather than demanded: nobody claims PDF/UA and wants a reader announcing the file
        // name. Without it the title is in the file and nothing ever reaches it.
        saved.ViewerPreferences.DisplayDocTitle.Should().BeTrue();
    }

    [Fact]
    public void ADocumentWithNoTitleIsRefused()
    {
        var renderer = Claiming();
        renderer.PdfDocument.Info.Title = "";

        Saving(renderer).Should().Throw<InvalidOperationException>()
            .WithMessage("*title*", "the caller is the only one who knows what it is");
    }

    [Fact]
    public void ADocumentWithNoLanguageIsRefused()
    {
        var renderer = Claiming(language: null);

        Saving(renderer).Should().Throw<InvalidOperationException>()
            .WithMessage("*language*", "a reader that does not know it cannot choose a voice");
    }

    [Fact]
    public void ADocumentThatWasNotTaggedIsRefused()
    {
        var renderer = Claiming(tagged: false);

        Saving(renderer).Should().Throw<InvalidOperationException>()
            .WithMessage("*tagged*", "a claim over an untagged file is the claim worth refusing");
    }

    [Fact]
    public void AnUndescribedFigureIsRefused()
    {
        var renderer = Claiming();

        // Reaching past the renderer, because MigraDoc will not produce one: an image with no
        // alternative text is drawn as an artifact rather than as a figure with nothing to say. This
        // is the check standing behind a document tagged by hand, or by a later version of this.
        var structure = renderer.PdfDocument.Structure;
        var figure = structure.CreateElement(PdfPinata.Pdf.Structure.PdfTag.Figure);
        figure.Should().NotBeNull();

        Saving(renderer).Should().Throw<InvalidOperationException>()
            .WithMessage("*alternate text*");
    }

    [Fact]
    public void TwoElementsUnderOneIdentifierAreRefused()
    {
        // An identifier is what something else points at, so it has to name one element - and the
        // loser of a collision is reported by nothing, it is simply unreachable. A caller naming
        // elements of their own alongside the generated note1, note2 collides easily, which is why
        // this is worth a rule rather than being left to the save.
        //
        // Asked of the validator rather than of a save, and deliberately: saving refuses it too, but
        // from the structure builder, which runs first. What this pins is that a caller can find out
        // by asking - which is the whole point of the validator being public.
        var renderer = Claiming();
        var structure = renderer.PdfDocument.Structure;

        var first = structure.CreateElement(PdfPinata.Pdf.Structure.PdfTag.Note);
        var second = structure.CreateElement(PdfPinata.Pdf.Structure.PdfTag.Note);
        first.Id = "note1";
        second.Id = "note1";

        // Set by the conformance writer during a save, so it has to be set by hand to reach the rule
        // this test is about rather than the one before it.
        renderer.PdfDocument.ViewerPreferences.DisplayDocTitle = true;

        var validating = () => PdfPinata.Pdf.Structure.PdfUaValidator.Validate(renderer.PdfDocument);

        validating.Should().Throw<InvalidOperationException>()
            .WithMessage("*share the identifier*note1*");
    }

    [Fact]
    public void ADocumentThatMeetsTheRulesIsWritten()
    {
        var saved = Save(Claiming());

        saved.Internals.Catalog.Elements.GetDictionary("/MarkInfo")
            .Elements.GetBoolean("/Marked").Should().BeTrue();
        saved.Internals.Catalog.Elements.GetString("/Lang").Should().Be("en-GB");
        saved.Pages[0].Elements.GetName("/Tabs").Should().Be("/S");
    }

    [Fact]
    public void ClaimingPdfUa2WritesPartTwoRatherThanPartOne()
    {
        var renderer = Tagged();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA2;

        var metadata = MetadataOf(Save(renderer));

        metadata.Should().Contain("<pdfuaid:part>2</pdfuaid:part>");
        metadata.Should().NotContain("<pdfuaid:part>1</pdfuaid:part>");
    }

    [Theory]
    [InlineData(PdfAConformance.PdfA1A, "1")]
    [InlineData(PdfAConformance.PdfA2A, "2")]
    [InlineData(PdfAConformance.PdfA3A, "3")]
    public void EachArchivalALevelCanBeClaimedSavedAndReopened(PdfAConformance conformance, string part)
    {
        var renderer = Tagged();
        renderer.PdfDocument.Options.Conformance = conformance;

        var saved = Save(renderer);
        var metadata = MetadataOf(saved);

        // Both halves of the conjunction: the archival identifier at the A level, and the tagging
        // rules PdfUaValidator already holds a PDF/UA claim to — checked here by the fact that
        // Save did not refuse, rather than restated as a second assertion of the same thing.
        metadata.Should().Contain("<pdfaid:part>" + part + "</pdfaid:part>");
        metadata.Should().Contain("<pdfaid:conformance>A</pdfaid:conformance>");
        saved.ViewerPreferences.DisplayDocTitle.Should().BeTrue();

        // An A-level claim is not itself a PDF/UA claim — the two are separate standards, and this
        // document asked only for the first of them.
        metadata.Should().NotContain("pdfuaid");
    }

    [Fact]
    public void AnALevelClaimOnAnUntaggedDocumentIsRefusedAtTheClaim()
    {
        var renderer = Tagged(tagged: false);

        var claiming = () => renderer.PdfDocument.ClaimConformance(PdfAConformance.PdfA2A);

        claiming.Should().Throw<InvalidOperationException>()
            .WithMessage("*tagged*", "a document with no structure tree cannot become tagged by saving");
        renderer.PdfDocument.Options.Conformance.Should().Be(PdfAConformance.None,
            "a refused claim must not half-set what it refused to make");
    }

    // ── PDF/UA-2's own rules, found by veraPDF against the conformance corpus ─────────────────────

    [Fact]
    public void ClaimingPdfUa2WritesTheRevisionYear()
    {
        // ISO 14289-2 clause 5: pdfuaid:rev has to be "2024", and PDF/UA-1 carries no such property
        // at all — it has never been revised.
        var renderer = Tagged();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA2;

        var metadata = MetadataOf(Save(renderer));

        metadata.Should().Contain("<pdfuaid:rev>2024</pdfuaid:rev>");
    }

    [Fact]
    public void ClaimingPdfUa1WritesNoRevisionYear()
    {
        var metadata = MetadataOf(Save(Claiming()));

        metadata.Should().NotContain("pdfuaid:rev");
    }

    [Fact]
    public void ClaimingPdfUa2PutsTheDocumentRootInThePdf20Namespace()
    {
        // ISO 14289-2 clause 8.2.5.2: the structure tree root's single child has to be a /Document
        // explicitly in the PDF 2.0 namespace, which nothing before PDF/UA-2 ever asked for.
        var renderer = Tagged();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA2;

        var saved = Save(renderer);

        var root = saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");
        var kids = root.Elements.GetArray("/K");
        kids.Elements.Count.Should().Be(1);

        var document = (PdfDictionary)((PdfReference)kids.Elements[0]).Value;
        document.Elements.GetName("/S").Should().Be("/Document");

        var ns = (PdfDictionary)((PdfReference)document.Elements["/NS"]).Value;
        ns.Elements.GetString("/NS").Should().Be("http://iso.org/pdf2/ssn");
    }

    [Fact]
    public void TheNamespaceTheDocumentRootNamesIsListedInTheStructureTreeRoot()
    {
        // ISO 32000-2 14.7.4.1: an element's /NS points into the root's /Namespaces rather than
        // declaring anything itself, so a namespace named on an element and nowhere else is one a
        // reader has no dictionary for. The same object both times, not two saying the same thing.
        var renderer = Tagged();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA2;

        var saved = Save(renderer);

        var root = saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");
        var namespaces = root.Elements.GetArray("/Namespaces");
        namespaces.Elements.Count.Should().Be(1);

        var listed = (PdfReference)namespaces.Elements[0];
        ((PdfDictionary)listed.Value).Elements.GetString("/NS").Should().Be("http://iso.org/pdf2/ssn");

        var document = (PdfDictionary)((PdfReference)root.Elements.GetArray("/K").Elements[0]).Value;
        document.Elements["/NS"].Should().BeSameAs(listed);
    }

    [Fact]
    public void ClaimingPdfUa1WritesNoNamespaceList()
    {
        // /Namespaces is PDF 2.0's, and a PDF/UA-1 document names no namespace to list.
        var saved = Save(Claiming());

        var root = saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");

        root.Elements.ContainsKey("/Namespaces").Should().BeFalse();
    }

    [Fact]
    public void ClaimingPdfUa1LeavesTheDocumentRootWithNoExplicitNamespace()
    {
        // The rule above is PDF/UA-2's alone — nothing about PDF/UA-1 or a plain tagged document
        // asks for an explicit namespace, and this stays unchanged by everything PDF/UA-2 needs.
        var saved = Save(Claiming());

        var root = saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");
        var kids = root.Elements.GetArray("/K");
        var document = (PdfDictionary)((PdfReference)kids.Elements[0]).Value;

        document.Elements.ContainsKey("/NS").Should().BeFalse();
    }

    [Fact]
    public void ClaimingPdfUa2RetagsAFootnoteAsFENote()
    {
        // ISO 14289-2 clause 8.2.5.14: /Note is PDF 1.7's type, removed from PDF 2.0 in favour of
        // /FENote — found by veraPDF, which refuses a PDF/UA-2 document that still carries /Note.
        var renderer = TaggedWithAFootnoteAndAList();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA2;

        var saved = Save(renderer);

        StructureTypesOf(saved).Should().Contain("/FENote").And.NotContain("/Note");
    }

    [Fact]
    public void ClaimingPdfUa1LeavesTheFootnoteTaggedAsNote()
    {
        var renderer = TaggedWithAFootnoteAndAList();
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;

        var saved = Save(renderer);

        StructureTypesOf(saved).Should().Contain("/Note").And.NotContain("/FENote");
    }

    [Fact]
    public void ABulletedListCarriesADiscListNumbering()
    {
        // ISO 14289-2 clause 8.2.5.25 requires this of any list carrying a /Lbl, at any value but
        // None — not only under PDF/UA-2: MigraDoc writes it for every list, since it costs nothing
        // for the profiles that do not ask for it and is what the correct one already looked like.
        var renderer = TaggedWithAFootnoteAndAList();

        var saved = Save(renderer);

        var list = FindByType(StructTreeRootKidsOf(saved), "/L");
        list.Should().NotBeNull();

        // Embedded directly rather than as an indirect reference — a small attribute dictionary
        // named by only one element does not need an identity of its own.
        var attributes = (PdfDictionary)list.Elements["/A"];
        attributes.Elements.GetName("/O").Should().Be("/List");
        attributes.Elements.GetName("/ListNumbering").Should().Be("/Disc");
    }

    /// <summary>
    ///   A rendered document claiming PDF/UA-1, ready to be saved.
    /// </summary>
    static PdfDocumentRenderer Claiming(bool tagged = true, string language = "en-GB")
    {
        var renderer = Tagged(tagged, language);
        renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;
        return renderer;
    }

    /// <summary>
    ///   A rendered document carrying a footnote and a bulleted list — the two corners of the
    ///   tagger PDF/UA-2's own rules touch that the plain <see cref="Tagged"/> shape does not reach.
    /// </summary>
    static PdfDocumentRenderer TaggedWithAFootnoteAndAList()
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Statement of account", "Heading1");

        var cited = section.AddParagraph("The rate applied to this account changed in April.");
        cited.AddFootnote("Set by the schedule in force on the date of issue.");

        var bulleted = section.AddParagraph("Payable by bank transfer", "List");
        bulleted.Format.ListInfo = new ListInfo { ListType = ListType.BulletList1 };

        var renderer = new PdfDocumentRenderer(true)
        {
            Document = document,
            TagContent = true,
            Language = "en-GB",
        };

        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = "Statement of account";
        return renderer;
    }

    /// <summary>
    ///   The structure tree root's <c>/K</c>, read from the catalog rather than through
    ///   <c>PdfDocument.Structure</c> — which, on a document just reopened by <see cref="PdfReader"/>,
    ///   would build a fresh empty tree instead of reading the one the file already carries.
    /// </summary>
    static PdfItem StructTreeRootKidsOf(PdfDocument saved) =>
        saved.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot").Elements["/K"];

    /// <summary>Every <c>/S</c> value reachable from the structure tree root.</summary>
    static System.Collections.Generic.List<string> StructureTypesOf(PdfDocument saved)
    {
        var found = new System.Collections.Generic.List<string>();
        Collect(StructTreeRootKidsOf(saved));
        return found;

        void Collect(PdfItem item)
        {
            if (item is PdfReference reference)
                item = reference.Value;

            switch (item)
            {
                case PdfArray array:
                    foreach (var element in array.Elements)
                        Collect(element);
                    break;

                case PdfDictionary dictionary:
                    found.Add(dictionary.Elements.GetName("/S"));
                    Collect(dictionary.Elements["/K"]);
                    break;
            }
        }
    }

    /// <summary>The first structure element of the given type reachable from <paramref name="item"/>.</summary>
    static PdfDictionary FindByType(PdfItem item, string type)
    {
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfArray array)
        {
            foreach (var element in array.Elements)
            {
                var found = FindByType(element, type);
                if (found != null)
                    return found;
            }
            return null;
        }

        if (item is not PdfDictionary dictionary)
            return null;

        if (dictionary.Elements.GetName("/S") == type)
            return dictionary;

        return FindByType(dictionary.Elements["/K"], type);
    }

    /// <summary>
    ///   A rendered document meeting every rule <see cref="PdfPinata.Pdf.Structure.PdfUaValidator"/>
    ///   checks, claiming nothing yet — what an accessibility claim and an A-level archival claim
    ///   alike are held to.
    /// </summary>
    static PdfDocumentRenderer Tagged(bool tagged = true, string language = "en-GB")
    {
        var document = new Document();
        var section = document.AddSection();
        section.AddParagraph("Statement of account", "Heading1");
        section.AddParagraph("Amounts are in pounds sterling.");

        var renderer = new PdfDocumentRenderer(true)
        {
            Document = document,
            TagContent = tagged,
            Language = language,
        };

        renderer.RenderDocument();
        renderer.PdfDocument.Info.Title = "Statement of account";
        return renderer;
    }

    static PdfDocument Save(PdfDocumentRenderer renderer)
    {
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;
        return PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    static Action Saving(PdfDocumentRenderer renderer) => () =>
    {
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
    };

    static string MetadataOf(PdfDocument saved)
    {
        var metadata = saved.Internals.Catalog.Elements.GetDictionary("/Metadata");
        return Encoding.UTF8.GetString(metadata.Stream.UnfilteredValue);
    }
}
