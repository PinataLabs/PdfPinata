
using System;
using System.Collections.Generic;
using System.IO;


namespace PdfPinata.Utils;

/// <summary>
/// Which files on a machine PdfPinata will try to read a font out of.
/// </summary>
internal static class FontFileTypes
{
    /// <summary>
    /// TrueType and OpenType, single fonts and collections of them.
    /// </summary>
    /// <remarks>
    /// Not Type 1 (.pfb, .pfa) or the bitmap formats: the parser reads an sfnt and nothing
    /// else, so surfacing one of those would only move the failure from discovery to use.
    /// </remarks>
    private static readonly string[] Extensions = { ".ttf", ".otf", ".ttc", ".otc" };


    public static bool IsFontFile(string path)
    {
        string extension = Path.GetExtension(path);

        foreach (string candidate in Extensions)
        {
            if (string.Equals(extension, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }


    /// <summary>
    /// A font directory is walked on the first font operation of the process, so an
    /// unreadable subdirectory in it must not be the thing that fails that operation.
    /// </summary>
    private static readonly EnumerationOptions WalkOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };


    /// <summary>
    /// Every font file under the given directory. One walk rather than one per extension,
    /// since a font directory is walked on the first font operation of the process.
    /// </summary>
    public static IEnumerable<string> In(string directory)
    {
        foreach (string path in Directory.EnumerateFiles(directory, "*", WalkOptions))
        {
            if (IsFontFile(path))
                yield return path;
        }
    }
}
