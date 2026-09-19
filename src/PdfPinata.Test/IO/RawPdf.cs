using System.Collections.Generic;
using System.Text;

namespace PdfPinata.Test.IO;

/// <summary>
///   Assembles documents written by hand, for the tests that need a document shaped in a way
///   PdfPinata does not write itself.
/// </summary>
internal static class RawPdf
{
    /// <summary>
    ///   Wraps the objects given in a header, a cross reference table and a trailer. The first
    ///   object is the catalog, and objects are numbered from one in the order they are given.
    ///   <paramref name="extraTrailerEntries" /> is copied into the trailer dictionary verbatim,
    ///   straight after the root, so it has to begin with a delimiter of its own: "/Info 5 0 R"
    ///   rather than "Info 5 0 R".
    /// </summary>
    internal static byte[] Build(IReadOnlyList<string> objects, string extraTrailerEntries = "")
    {
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var startOfCrossReferenceTable = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Count + 1).Append('\n');
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<</Size ").Append(objects.Count + 1).Append("/Root 1 0 R")
            .Append(extraTrailerEntries).Append(">>\n");
        pdf.Append("startxref\n").Append(startOfCrossReferenceTable).Append("\n%%EOF\n");

        // The document is plain ASCII, so a byte is a character and the offsets above hold.
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    /// <summary>
    ///   A stream object, its length worked out from the data.
    /// </summary>
    internal static string Stream(string entries, string data)
    {
        return "<<" + entries + "/Length " + data.Length + ">>stream\n" + data + "\nendstream";
    }
}
