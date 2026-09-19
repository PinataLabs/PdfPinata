using System.Collections.Generic;
using PdfPinata.Test.IO;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   Documents whose outline entries were written by something other than this library, for the
///   tests about reading them back. The shapes come from
///   https://github.com/empira/PDFsharp/issues/8, where a document written by LaTeX could not be
///   read at all.
/// </summary>
internal static class ImportedOutlineFixtures
{
    /// <summary>The name every fixture below holds its destination under.</summary>
    internal const string Name = "sec1.section";

    /// <summary>The destination every fixture holds, going to the second page.</summary>
    internal const string Destination = "[4 0 R/XYZ 11 22 0]";

    /// <summary>
    ///   Two pages and one outline entry, saying where it goes in the way given. The catalog
    ///   holds what it says beyond its page tree, and the objects it refers to are numbered from
    ///   nine.
    /// </summary>
    internal static byte[] Document(string entry, string catalogEntries = "", params string[] held)
    {
        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R/Outlines 7 0 R" + catalogEntries + ">>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Contents 5 0 R"),
            Page("/Contents 6 0 R"),
            RawPdf.Stream("", "0 0 1 RG 10 10 100 100 re S"),
            RawPdf.Stream("", "1 0 0 RG 10 10 100 100 re S"),
            "<</Type/Outlines/First 8 0 R/Last 8 0 R/Count 1>>",
            "<</Title(Section 1)/Parent 7 0 R" + entry + ">>"
        };
        objects.AddRange(held);

        return RawPdf.Build(objects);
    }

    /// <summary>
    ///   An entry going to a destination the catalog holds by name, named the way LaTeX names
    ///   it: through a GoTo action, with the name written as a string, and with the name tree
    ///   split over a root and two leaves so that the search has to walk it.
    /// </summary>
    internal static byte[] NamedInGoToAction()
    {
        return WithNameTree("/A<</S/GoTo/D(" + Name + ")>>");
    }

    /// <summary>The same destination, named in the /Dest entry rather than through an action.</summary>
    internal static byte[] NamedInDestEntry()
    {
        return WithNameTree("/Dest(" + Name + ")");
    }

    /// <summary>
    ///   The same destination, named as a name rather than as a string and held in the /Dests
    ///   dictionary of the catalog, which is where PDF 1.1 put named destinations.
    /// </summary>
    internal static byte[] NamedInDestsDictionary()
    {
        return Document("/Dest/" + Name, "/Dests 9 0 R", "<</" + Name + " " + Destination + ">>");
    }

    /// <summary>An entry naming a destination the catalog holds nothing under.</summary>
    internal static byte[] NamedNowhere()
    {
        return Document("/A<</S/GoTo/D(" + Name + ")>>");
    }

    /// <summary>An entry that opens a web page rather than going anywhere in the document.</summary>
    internal static byte[] WithUriAction()
    {
        return Document("/A<</S/URI/URI(https://example.com)>>");
    }

    /// <summary>An entry going to a page of another document, which this one cannot show.</summary>
    internal static byte[] WithRemoteGoToAction()
    {
        return Document("/A<</S/GoToR/F(other.pdf)/D[0/Fit]>>");
    }

    /// <summary>An entry whose destination is written out rather than named.</summary>
    internal static byte[] WithDestinationArray(string destination = Destination)
    {
        return Document("/Dest" + destination);
    }

    /// <summary>An entry whose destination is written out and held in an object of its own.</summary>
    internal static byte[] WithIndirectDestinationArray()
    {
        return Document("/A<</S/GoTo/D 9 0 R>>", "", Destination);
    }

    /// <summary>An entry whose action is held in an object of its own rather than written out.</summary>
    internal static byte[] WithIndirectGoToAction()
    {
        return Document("/A 9 0 R", "", "<</S/GoTo/D" + Destination + ">>");
    }

    /// <summary>
    ///   An entry whose action leads on to another once it has gone where it goes, which is what
    ///   /Next is for. The entry does two things, and only one of them is a destination.
    /// </summary>
    internal static byte[] WithChainedGoToAction()
    {
        return Document("/A<</S/GoTo/D" + Destination + "/Next<</S/URI/URI(https://example.com)>> >>");
    }

    /// <summary>
    ///   An entry whose destination goes to a page by number rather than by reference, which is
    ///   how a destination reaching into another document is written.
    /// </summary>
    internal static byte[] WithPageNumberDestination(int number)
    {
        return Document("/Dest[" + number + "/Fit]");
    }

    private static byte[] WithNameTree(string entry)
    {
        // The tree says which names lie below each leaf, and the destination sits in the second
        // one, so a search that stops at the first does not find it.
        return Document(entry, "/Names<</Dests 9 0 R>>",
            "<</Kids[10 0 R 11 0 R]>>",
            "<</Limits[(Doc-Start) (page.1)]/Names[(Doc-Start) [3 0 R/Fit] (page.1) [3 0 R/Fit]]>>",
            "<</Limits[(" + Name + ") (" + Name + ")]/Names[(" + Name + ") " + Destination + "]>>");
    }

    private static string Page(string entries)
    {
        return "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + ">>";
    }
}
