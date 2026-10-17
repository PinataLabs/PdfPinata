using System.Collections.Generic;
using System.Linq;

using PdfPinata.Drawing;
using PdfPinata.Fonts;


namespace PdfPinata.Utils;

/// <summary>
/// Picks the face of a family that answers a requested style, nearest first, and says what the
/// renderer must simulate to make up the difference.
/// </summary>
/// <remarks>
/// This is the whole of how <see cref="FontResolverBase"/> chooses a face once it has found the
/// family, kept apart from where the faces came from so that a resolver which does not discover
/// files - one holding its fonts in memory - matches by exactly the same rule rather than by a copy
/// of it. Internal: the public surface for choosing a face is <see cref="IFontResolver"/>.
/// </remarks>
internal static class FontFaceMatching
{
    /// <summary>
    /// Picks the closest face the family ships a file for, and asks the renderer to supply what
    /// is missing.
    /// </summary>
    /// <param name="faces">
    /// The family's faces, each under the style it reports, as the face name a resolver hands out.
    /// </param>
    /// <param name="isBold">Whether bold was asked for.</param>
    /// <param name="isItalic">Whether italic was asked for.</param>
    /// <param name="boldSimulation">How a simulated bold is to be drawn, should one be needed.</param>
    /// <remarks>
    /// Falling back to the regular face and saying nothing, as this used to, renders bold text
    /// as regular text without a word about it. PdfPinata can draw a bold or an italic that
    /// a family has no file for - stroking the glyphs, and skewing them - but only if it is
    /// told to, and this is where it is told.
    /// </remarks>
    public static FontResolverInfo Resolve(IReadOnlyDictionary<XFontStyle, string> faces,
        bool isBold, bool isItalic, XBoldSimulation boldSimulation)
    {
        foreach (var candidate in Candidates(isBold, isItalic))
        {
            if (faces.TryGetValue(candidate.Key, out var faceName))
                return new FontResolverInfo(faceName, candidate.Value, boldSimulation);
        }

        // The family is keyed by the four styles the candidates cover, so this is unreachable
        // for a family that has any file at all. Kept so that a family which somehow has one
        // under another key still resolves to something.
        return new FontResolverInfo(faces.First().Value);
    }


    /// <summary>
    /// The faces to try for a requested style, nearest first, each with what the renderer would
    /// have to simulate if that face were used.
    /// </summary>
    /// <remarks>
    /// Only the axis that is missing gets simulated. Asked for bold italic by a family shipping
    /// a bold, the bold file is used and only the slant is drawn on - a simulated italic over a
    /// real bold beats simulating both over the regular face.
    /// <para>
    /// Neither weight nor slant can be taken away, so a request for a plainer face than the
    /// family ships is answered with the nearest one and no simulation at all.
    /// </para>
    /// </remarks>
    private static IEnumerable<KeyValuePair<XFontStyle, XStyleSimulations>> Candidates(bool isBold, bool isItalic)
    {
        const XStyleSimulations none = XStyleSimulations.None;
        const XStyleSimulations bold = XStyleSimulations.BoldSimulation;
        const XStyleSimulations italic = XStyleSimulations.ItalicSimulation;
        const XStyleSimulations both = XStyleSimulations.BoldItalicSimulation;

        if (isBold && isItalic)
        {
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Bold, italic);
            yield return Candidate(XFontStyle.Italic, bold);
            yield return Candidate(XFontStyle.Regular, both);
        }
        else if (isBold)
        {
            yield return Candidate(XFontStyle.Bold, none);
            yield return Candidate(XFontStyle.Regular, bold);
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Italic, bold);
        }
        else if (isItalic)
        {
            yield return Candidate(XFontStyle.Italic, none);
            yield return Candidate(XFontStyle.Regular, italic);
            yield return Candidate(XFontStyle.BoldItalic, none);
            yield return Candidate(XFontStyle.Bold, italic);
        }
        else
        {
            yield return Candidate(XFontStyle.Regular, none);
            yield return Candidate(XFontStyle.Italic, none);
            yield return Candidate(XFontStyle.Bold, none);
            yield return Candidate(XFontStyle.BoldItalic, none);
        }
    }


    private static KeyValuePair<XFontStyle, XStyleSimulations> Candidate(XFontStyle style, XStyleSimulations simulations)
    {
        return new KeyValuePair<XFontStyle, XStyleSimulations>(style, simulations);
    }
}
