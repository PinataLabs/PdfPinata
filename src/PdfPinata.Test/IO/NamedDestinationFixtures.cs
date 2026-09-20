using System.Collections.Generic;

namespace PdfPinata.Test.IO;

/// <summary>
///   Documents whose links name where they go rather than saying it outright, for the tests
///   about importing a page carrying one. The catalog holds what the names stand for, and it is
///   not imported along with a page, so a name has to be resolved while the document it came
///   from is still at hand.
/// </summary>
internal static class NamedDestinationFixtures
{
    /// <summary>The length of the stream of each of the two images, in bytes.</summary>
    internal const int ImageLength = 20000;

    /// <summary>The name every fixture below holds its destination under.</summary>
    internal const string Name = "target";

    /// <summary>The destination every fixture holds, going to the second page.</summary>
    internal const string Destination = "[4 0 R/XYZ 11 22 0]";

    /// <summary>
    ///   Two pages, each drawing an image of its own so that one which came along uninvited
    ///   shows in the weight. The first carries the annotations given, of which there can be up
    ///   to four; every destination the catalog holds goes to the second page, object 4.
    /// </summary>
    /// <param name="catalogEntries">What the catalog says beyond its page tree.</param>
    /// <param name="annotations">The annotations of the first page.</param>
    /// <param name="held">The objects the catalog entries refer to, numbered from 13.</param>
    internal static byte[] Document(string catalogEntries, string[] annotations, params string[] held)
    {
        var references = new List<string>();
        for (var number = 0; number < annotations.Length; number++)
            references.Add(FirstAnnotation + number + " 0 R");

        var objects = new List<string>
        {
            "<</Type/Catalog/Pages 2 0 R" + catalogEntries + ">>",
            "<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>",
            Page("/Resources<</XObject<</Im0 5 0 R>>>>/Contents 7 0 R" +
                 "/Annots[" + string.Join(" ", references) + "]"),
            Page("/Resources<</XObject<</Im1 6 0 R>>>>/Contents 8 0 R"),
            Image(),
            Image(),
            Content("Im0"),
            Content("Im1")
        };

        // The annotations take the four numbers up to the ones the catalog holds, whatever the
        // page ended up carrying, so that the fixtures can refer to those by number.
        objects.AddRange(annotations);
        for (var number = annotations.Length; number < HeldObjects - FirstAnnotation; number++)
            objects.Add("null");
        objects.AddRange(held);

        return RawPdf.Build(objects);
    }

    /// <summary>
    ///   A catalog holding the destination in a name tree, the way PDF 1.2 onwards writes it.
    ///   The name is a string.
    /// </summary>
    internal static byte[] InNameTree(params string[] annotations)
    {
        return Document("/Names<</Dests " + HeldObjects + " 0 R>>", annotations,
            "<</Names[(" + Name + ") " + Destination + "]>>");
    }

    /// <summary>
    ///   The same, with the destination held in a dictionary of its own under /D rather than
    ///   written into the tree.
    /// </summary>
    internal static byte[] InNameTreeUnderD(params string[] annotations)
    {
        return Document("/Names<</Dests " + HeldObjects + " 0 R>>", annotations,
            "<</Names[(" + Name + ") " + (HeldObjects + 1) + " 0 R]>>",
            "<</D " + Destination + ">>");
    }

    /// <summary>
    ///   The same, with the tree split over a root and two leaves that say which names lie below
    ///   them. The destination sits in the second leaf, so a search that stops at the first, or
    ///   that reads the bounds the wrong way round, does not find it.
    /// </summary>
    internal static byte[] InNameTreeWithKids(params string[] annotations)
    {
        return Document("/Names<</Dests " + HeldObjects + " 0 R>>", annotations,
            "<</Kids[" + (HeldObjects + 1) + " 0 R " + (HeldObjects + 2) + " 0 R]>>",
            "<</Limits[(aaa) (aab)]/Names[(aaa) [4 0 R/Fit] (aab) [4 0 R/Fit]]>>",
            "<</Limits[(" + Name + ") (" + Name + ")]/Names[(" + Name + ") " + Destination + "]>>");
    }

    /// <summary>
    ///   A catalog holding the destination in the /Dests dictionary, the way PDF 1.1 wrote it.
    ///   The name is a name rather than a string.
    /// </summary>
    internal static byte[] InDestsDictionary(params string[] annotations)
    {
        return Document("/Dests " + HeldObjects + " 0 R", annotations,
            "<</" + Name + " " + Destination + ">>");
    }

    /// <summary>A catalog holding nothing, for a name that stands for nothing.</summary>
    internal static byte[] WithNothingHeld(params string[] annotations)
    {
        return Document("", annotations);
    }

    /// <summary>A link that names where it goes, by a string.</summary>
    internal static string LinkToName()
    {
        return Link("/Dest(" + Name + ")");
    }

    /// <summary>A link that names where it goes, by a name.</summary>
    internal static string LinkToNameObject()
    {
        return Link("/Dest/" + Name);
    }

    /// <summary>A link performing the action given.</summary>
    internal static string LinkWithAction(string action)
    {
        return Link("/A<<" + action + ">>");
    }

    internal static string Link(string entries)
    {
        return "<</Type/Annot/Subtype/Link/Rect[0 0 10 10]/Border[0 0 0]" + entries + ">>";
    }

    /// <summary>The number of the first object that holds an annotation.</summary>
    private const int FirstAnnotation = 9;

    /// <summary>The number of the first object the catalog entries refer to.</summary>
    private const int HeldObjects = 13;

    private static string Page(string entries)
    {
        return "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]" + entries + ">>";
    }

    private static string Image()
    {
        return RawPdf.Stream("/Type/XObject/Subtype/Image/Width 100/Height 100" +
                             "/ColorSpace/DeviceGray/BitsPerComponent 8",
            new string('A', ImageLength));
    }

    private static string Content(string name)
    {
        return RawPdf.Stream("", "q 100 0 0 100 10 10 cm /" + name + " Do Q");
    }
}
