#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using PdfPinata.Internal;
using PdfPinata.Pdf;

namespace PdfPinata.Fonts;

/// <summary>
/// Provides functionality to specify information about the handling of fonts in the current application domain.
/// </summary>
public static class GlobalFontSettings
{
    /// <summary>
    /// The name of the default font.
    /// </summary>
    public const string DefaultFontName = "PlatformDefault";

    /// <summary>
    /// Gets or sets the global font resolver for the current application domain.
    /// This static property should be set only once and before any font operation was executed by PdfPinata.
    /// If this is not easily to obtain, e.g. because your code is running on a web server, you must provide the
    /// same instance of your font resolver in every subsequent setting of this property.
    /// In a web application set the font resolver in Global.asax.
    /// A resolver must be set before the first font operation. Install PdfPinata.Skia and use
    /// <c>new SkiaFontResolver()</c>, install PdfPinata.ImageSharp and use <c>new ImageSharpFontResolver()</c>,
    /// or derive your own from <see cref="T:PdfPinata.Utils.FontResolverBase"/>.
    /// </summary>
    public static IFontResolver FontResolver
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                if (_fontResolver == null)
                    throw new InvalidOperationException(
                        "No IFontResolver has been configured. Set GlobalFontSettings.FontResolver before "
                        + "performing any font operation, e.g. 'GlobalFontSettings.FontResolver = new SkiaFontResolver();' "
                        + "from the PdfPinata.Skia package.");
                return _fontResolver;
            }
            finally { Lock.ExitFontFactory(); }
        }
        set
        {
            // Cannot remove font resolver.
            ArgumentNullException.ThrowIfNull(value);

            try
            {
                Lock.EnterFontFactory();
                // Ignore multiple setting e.g. in a web application.
                if (ReferenceEquals(_fontResolver, value))
                    return;

                if (FontFactory.HasFontSources)
                    throw new InvalidOperationException("Must not change font resolver after is was once used.");

                _fontResolver = value;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }
    static IFontResolver _fontResolver;

    /// <summary>
    /// Whether <see cref="FontResolver"/> has been set, so a caller can find out without catching
    /// the <see cref="InvalidOperationException"/> reading it unset throws.
    /// </summary>
    public static bool IsFontResolverSet
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _fontResolver != null;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }

    /// <summary>
    /// States that <see cref="FontResolver"/> refuses a second write once a font has been resolved
    /// through it - see <see cref="SeamLifecycle.SetOnce"/>.
    /// </summary>
    public static SeamLifecycle FontResolverLifecycle => SeamLifecycle.SetOnce;

    /// <summary>
    /// Gets or sets the provider that turns text into glyph outlines for the current application domain.
    /// Only <see cref="T:PdfPinata.Drawing.XGraphicsPath"/>.AddString needs one; drawing and measuring
    /// text do not. Install PdfPinata.Skia and use <c>new SkiaGlyphOutlineProvider()</c>, or install
    /// PdfPinata.ImageSharp and use <c>new ImageSharpGlyphOutlineProvider()</c>.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="FontResolver"/> rather than part of it: <see cref="IFontResolver"/> is
    /// implemented by every consumer who has written a resolver of their own, and a new member on it
    /// would break all of them. A provider reads its font bytes through the registered resolver, so the
    /// two cannot disagree about which face a family means.
    /// </remarks>
    public static IGlyphOutlineProvider GlyphOutlineProvider
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                if (_glyphOutlineProvider == null)
                    throw new InvalidOperationException(
                        "No IGlyphOutlineProvider has been configured. Set GlobalFontSettings.GlyphOutlineProvider "
                        + "before adding text to a path, e.g. 'GlobalFontSettings.GlyphOutlineProvider = "
                        + "new SkiaGlyphOutlineProvider();' from the PdfPinata.Skia package, or "
                        + "'new ImageSharpGlyphOutlineProvider();' from PdfPinata.ImageSharp.");
                return _glyphOutlineProvider;
            }
            finally { Lock.ExitFontFactory(); }
        }
        set
        {
            try
            {
                Lock.EnterFontFactory();
                // Null clears it, unlike FontResolver, which nothing can work without. Outlines are
                // wanted by one method, so a consumer may reasonably want to take the provider away
                // again - and a seam that can be cleared is a seam whose unset behaviour can be tested.
                _glyphOutlineProvider = value;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }
    static IGlyphOutlineProvider _glyphOutlineProvider;

    /// <summary>
    /// Whether <see cref="GlyphOutlineProvider"/> has been set, so a caller can find out without
    /// catching the <see cref="InvalidOperationException"/> reading it unset throws.
    /// </summary>
    public static bool IsGlyphOutlineProviderSet
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _glyphOutlineProvider != null;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }

    /// <summary>
    /// States that <see cref="GlyphOutlineProvider"/> may be set, replaced or cleared at any time -
    /// see <see cref="SeamLifecycle.SetAnytime"/>.
    /// </summary>
    public static SeamLifecycle GlyphOutlineProviderLifecycle => SeamLifecycle.SetAnytime;

    /// <summary>
    /// Gets or sets the shaper that turns characters into the glyphs a font really draws for them,
    /// for the current application domain. Null means none is registered, and then every path
    /// behaves as it always has: one character, one glyph, no reordering.
    /// </summary>
    /// <remarks>
    /// The fourth seam, and the only one whose unset state is not an error - see
    /// <see cref="ITextShaper"/> for why. Reading it therefore answers null rather than throwing,
    /// and setting it to null puts the unshaped behaviour back. It may be set, replaced or cleared
    /// at any time; unlike <see cref="FontResolver"/>, nothing is cached against it.
    /// <para>
    /// It is not part of <see cref="IFontResolver"/> for the same reason
    /// <see cref="GlyphOutlineProvider"/> is not: that interface is implemented by every consumer
    /// who has written a resolver of their own, and a new member on it would break all of them. A
    /// shaper reads its font bytes from the font it is handed, which came through the registered
    /// resolver, so the two cannot disagree about which face a family means.
    /// </para>
    /// </remarks>
    public static ITextShaper TextShaper
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _textShaper;
            }
            finally { Lock.ExitFontFactory(); }
        }
        set
        {
            try
            {
                Lock.EnterFontFactory();
                _textShaper = value;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }
    static ITextShaper _textShaper;

    /// <summary>
    /// Whether <see cref="TextShaper"/> has been set. Unlike <see cref="FontResolver"/> and
    /// <see cref="GlyphOutlineProvider"/>, reading this seam unset does not throw - null is a
    /// working default - so this answers the same thing <c>TextShaper != null</c> would, stated
    /// here for the same shape as the other seams.
    /// </summary>
    public static bool IsTextShaperSet
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _textShaper != null;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }

    /// <summary>
    /// States that <see cref="TextShaper"/> may be set, replaced or cleared at any time - see
    /// <see cref="SeamLifecycle.SetAnytime"/>.
    /// </summary>
    public static SeamLifecycle TextShaperLifecycle => SeamLifecycle.SetAnytime;

    /// <summary>
    /// Gets or sets what to try when the chosen face has no glyph for a character, for the current
    /// application domain. Null means nothing is tried, and then a character the face cannot draw
    /// is <c>.notdef</c>, exactly as it has always been.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fifth seam, and the second whose unset state is not an error. Reading it answers the
    /// registered fallback if there is one, and otherwise the registered
    /// <see cref="FontResolver"/> when that resolver implements <see cref="IFontFallback"/> as
    /// well - a resolver that already knows what is installed is the natural place to answer from,
    /// and a consumer who has written one should not have to register it twice.
    /// </para>
    /// <para>
    /// It may be set, replaced or cleared at any time. Changing it drops what the library had
    /// worked out about which face draws which character, so a document part-way through being
    /// drawn will use the new answer from the next string onwards - which is why it is better set
    /// once, before anything is drawn.
    /// </para>
    /// </remarks>
    public static IFontFallback FontFallback
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _fontFallback ?? _fontResolver as IFontFallback;
            }
            finally { Lock.ExitFontFactory(); }
        }
        set
        {
            try
            {
                Lock.EnterFontFactory();
                _fontFallback = value;
                FontFallbackResolution.Forget();
            }
            finally { Lock.ExitFontFactory(); }
        }
    }
    static IFontFallback _fontFallback;

    /// <summary>
    /// Whether <see cref="FontFallback"/> answers anything other than null - either because a
    /// fallback was registered directly, or because the registered <see cref="FontResolver"/>
    /// implements <see cref="IFontFallback"/> itself. Mirrors what the getter falls back to, rather
    /// than only the field this seam sets.
    /// </summary>
    public static bool IsFontFallbackSet
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _fontFallback != null || _fontResolver is IFontFallback;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }

    /// <summary>
    /// States that <see cref="FontFallback"/> may be set, replaced or cleared at any time - see
    /// <see cref="SeamLifecycle.SetAnytime"/>.
    /// </summary>
    public static SeamLifecycle FontFallbackLifecycle => SeamLifecycle.SetAnytime;

    /// <summary>
    /// Gets or sets the default font encoding used for XFont objects where encoding is not explicitly specified.
    /// If it is not set, the default value is PdfFontEncoding.Unicode.
    /// If you are sure your document contains only Windows-1252 characters (see https://en.wikipedia.org/wiki/Windows-1252) 
    /// set default encoding to PdfFontEncodingj.Windows1252.
    /// Must be set only once per app domain.
    /// </summary>
    public static PdfFontEncoding DefaultFontEncoding
    {
        get
        {
            if (!_fontEncodingInitialized)
                DefaultFontEncoding = PdfFontEncoding.Unicode;
            return _fontEncoding;
        }
        set
        {
            try
            {
                Lock.EnterFontFactory();
                if (_fontEncodingInitialized)
                {
                    // Ignore multiple setting e.g. in a web application.
                    if (_fontEncoding == value)
                        return;
                    throw new InvalidOperationException("Must not change DefaultFontEncoding after is was set once.");
                }

                _fontEncoding = value;
                _fontEncodingInitialized = true;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }
    static PdfFontEncoding _fontEncoding;
    static bool _fontEncodingInitialized;

    /// <summary>
    /// Whether <see cref="DefaultFontEncoding"/> has been explicitly set. Reading it unset does not
    /// throw - it answers <see cref="PdfFontEncoding.Unicode"/> - so this is the only way to tell
    /// that default apart from one a caller chose.
    /// </summary>
    public static bool IsDefaultFontEncodingSet
    {
        get
        {
            try
            {
                Lock.EnterFontFactory();
                return _fontEncodingInitialized;
            }
            finally { Lock.ExitFontFactory(); }
        }
    }

    /// <summary>
    /// States that <see cref="DefaultFontEncoding"/> refuses a second write to a different value
    /// once it has been set once - see <see cref="SeamLifecycle.SetOnce"/>.
    /// </summary>
    public static SeamLifecycle DefaultFontEncodingLifecycle => SeamLifecycle.SetOnce;
}
