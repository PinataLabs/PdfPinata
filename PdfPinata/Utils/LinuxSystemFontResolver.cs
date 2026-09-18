using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using System.Text.RegularExpressions;

using PdfPinata.Internal;


namespace PdfPinata.Utils;

/// <summary>
/// Finds the font files installed on a Linux machine, by asking fontconfig through
/// <c>libfontconfig.so.1</c> and falling back to walking the usual font directories when that
/// library is absent or refuses to load.
/// </summary>
/// <remarks>
/// The fontconfig bindings below are public only because the interop types have to be reachable
/// from the extern declarations that use them. They are not intended as API and may change.
/// </remarks>
public static class LinuxSystemFontResolver
{
    const string libfontconfig = "libfontconfig.so.1";


    [DllImport(libfontconfig)] private static extern IntPtr FcInitLoadConfigAndFonts();

    static readonly Lazy<IntPtr> fcConfig = new(FcInitLoadConfigAndFonts);


    /// <summary>Creates an empty fontconfig pattern. Binds to <c>FcPatternCreate</c>.</summary>
    [DllImport(libfontconfig)] public static extern FcPatternHandle FcPatternCreate();
    /// <summary>Reads a string property out of a pattern. Binds to <c>FcPatternGetString</c>.</summary>
    [DllImport(libfontconfig)] public static extern int FcPatternGetString(IntPtr p, [MarshalAs(UnmanagedType.LPStr)] string obj, int n, ref IntPtr s);
    /// <summary>Releases a pattern. Binds to <c>FcPatternDestroy</c>.</summary>
    [DllImport(libfontconfig)] public static extern void FcPatternDestroy(IntPtr pattern);

    /// <summary>A handle to a fontconfig pattern, released when disposed.</summary>
    public class FcPatternHandle : SafeHandle
    {
        FcPatternHandle() : base(IntPtr.Zero, true) { }

        /// <summary>Gets whether this handle holds nothing to release.</summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>Releases the unmanaged handle. Called by <see cref="System.Runtime.InteropServices.SafeHandle"/>.</summary>
        protected override bool ReleaseHandle()
        {
            FcPatternDestroy(handle);
            return true;
        }
    }


    /// <summary>Creates an empty fontconfig object set. Binds to <c>FcObjectSetCreate</c>.</summary>
    [DllImport(libfontconfig)] public static extern FcObjectSetHandle FcObjectSetCreate();
    /// <summary>Adds a property name to an object set. Binds to <c>FcObjectSetAdd</c>.</summary>
    [DllImport(libfontconfig)] public static extern int FcObjectSetAdd(FcObjectSetHandle os, [MarshalAs(UnmanagedType.LPStr)] string obj);
    /// <summary>Releases an object set. Binds to <c>FcObjectSetDestroy</c>.</summary>
    [DllImport(libfontconfig)] public static extern void FcObjectSetDestroy(IntPtr os);

    /// <summary>A handle to a fontconfig object set, released when disposed.</summary>
    public class FcObjectSetHandle : SafeHandle
    {
        FcObjectSetHandle() : base(IntPtr.Zero, true) { }

        /// <summary>Gets whether this handle holds nothing to release.</summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>Releases the unmanaged handle. Called by <see cref="System.Runtime.InteropServices.SafeHandle"/>.</summary>
        protected override bool ReleaseHandle()
        {
            FcObjectSetDestroy(handle);
            return true;
        }

        /// <summary>Creates an object set naming the properties to be read back for each font.</summary>
        public static FcObjectSetHandle Create(params string[] objs)
        {
            var os = FcObjectSetCreate();
            foreach (var obj in objs)
                FcObjectSetAdd(os, obj);
            FcObjectSetAdd(os, "");
            return os;
        }
    }


    /// <summary>Lists the fonts matching a pattern. Binds to <c>FcFontList</c>.</summary>
    [DllImport(libfontconfig)] public static extern FcFontSetHandle FcFontList(IntPtr config, FcPatternHandle pattern, FcObjectSetHandle os);
    /// <summary>Releases a font set. Binds to <c>FcFontSetDestroy</c>.</summary>
    [DllImport(libfontconfig)] public static extern void FcFontSetDestroy(IntPtr fs);

    /// <summary>The layout of fontconfig's <c>FcFontSet</c>, as marshalled back from a font set handle.</summary>
    public struct FcFontSet
    {
        /// <summary>The number of fonts in the set.</summary>
        public int nfont;
        /// <summary>The number of font slots allocated in the set.</summary>
        public int sfont;
        /// <summary>A pointer to the array of pattern pointers, one per font.</summary>
        public IntPtr fonts;
    }

    /// <summary>A handle to a fontconfig font set, released when disposed.</summary>
    public class FcFontSetHandle : SafeHandle
    {
        FcFontSetHandle() : base(IntPtr.Zero, true) { }

        /// <summary>Gets whether this handle holds nothing to release.</summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>Releases the unmanaged handle. Called by <see cref="System.Runtime.InteropServices.SafeHandle"/>.</summary>
        protected override bool ReleaseHandle()
        {
            FcFontSetDestroy(handle);
            return true;
        }

        /// <summary>Marshals the font set this handle points at into managed form.</summary>
        public FcFontSet Read()
        {
            return Marshal.PtrToStructure<FcFontSet>(handle);
        }
    }


    static string GetString(IntPtr handle, string obj)
    {
        var ptr = IntPtr.Zero;
        var result = FcPatternGetString(handle, obj, 0, ref ptr);
        if (result == 0)
            return Marshal.PtrToStringAnsi(ptr);
        else
            return null;
    }


    static IEnumerable<string> ResolveFontConfig()
    {
        var config = fcConfig.Value;
        using (var pattern = FcPatternCreate())
        using (var os = FcObjectSetHandle.Create("family", "style", "file"))
        using (var fs = FcFontList(config, pattern, os))
        {
            var fset = fs.Read();
            for (int index = 0; index < fset.nfont; index++)
            {
                var font = Marshal.ReadIntPtr(fset.fonts, index * Marshal.SizeOf<IntPtr>());
                var family = GetString(font, "family");
                var style = GetString(font, "style");
                var file = GetString(font, "file");

                if (family is null || style is null || file is null)
                    continue;

                yield return file;
            }
        }
    }


    /// <summary>
    /// Reports a font discovery problem that is otherwise ignored. Written to the trace listeners
    /// rather than to the console, so that a host that wants to hear about a font directory it
    /// could not read can listen for it and one that does not is not made to read it.
    /// </summary>
    private static void LogError(string message)
    {
        Trace.WriteLine(message);
    }


    /// <summary>
    /// Returns the paths of the font files on this machine. Asks fontconfig first and falls back to
    /// walking the standard font directories if that fails, so a machine without the library still
    /// finds its fonts.
    /// </summary>
    public static string[] Resolve()
    {
        try
        {
            return ResolveFontConfig().Where(FontFileTypes.IsFontFile).ToArray();
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            LogError(ex.ToString());
            return ResolveFallback().Where(FontFileTypes.IsFontFile).ToArray();
        }
    }


    static IEnumerable<string> ResolveFallback()
    {
        var fontList = new List<string>();

        void AddFontsToFontList(string path)
        {
            if (!Directory.Exists(path))
                return;

            foreach (string subDir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories))
                fontList.AddRange(Directory.EnumerateFiles(subDir, "*", SearchOption.AllDirectories));
        }

        var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in SearchPaths())
        {
            if (hs.Contains(path))
                continue;
            hs.Add(path);
            AddFontsToFontList(path);
        }

        return fontList.ToArray();
    }

    static IEnumerable<string> SearchPaths()
    {
        var dirs = new List<string>();
        try
        {
            Regex confRegex = new Regex("<dir>(?<dir>.*)</dir>", RegexOptions.Compiled);
            using (var reader = new StreamReader(File.OpenRead("/etc/fonts/fonts.conf")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = confRegex.Match(line);
                    if (!match.Success)
                        continue;

                    string path = match.Groups["dir"].Value.Trim();
                    if (path.StartsWith("~"))
                    {
                        path = Environment.GetEnvironmentVariable("HOME") + path.Substring(1);
                    }

                    dirs.Add(path);
                }
            }
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            LogError(ex.Message);
            LogError(ex.StackTrace);
        }

        dirs.Add("/usr/share/fonts");
        dirs.Add("/usr/local/share/fonts");
        dirs.Add(Environment.GetEnvironmentVariable("HOME") + "/.fonts");
        return dirs;
    }
}
