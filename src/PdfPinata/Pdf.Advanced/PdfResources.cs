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

using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a PDF resource object.
/// </summary>
public sealed class PdfResources : PdfDictionary
{
    // Resource management works roughly like this:
    // When the user creates an XFont and uses it in the XGraphics of a PdfPage, then at the first time
    // a PdfFont is created and cached in the document global font table. If the user creates a new
    // XFont object for an existing PdfFont, the PdfFont object is reused. When the PdfFont is added
    // to the resources of a PdfPage for the first time, it is added to the page local PdfResourceMap for
    // fonts and automatically associated with a local resource name.

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfResources"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    public PdfResources(PdfDocument document)
        : base(document)
    {
        Elements[Keys.ProcSet] = new PdfLiteral("[/PDF/Text/ImageB/ImageC/ImageI]");
    }

    internal PdfResources(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// Adds the specified font to this resource dictionary and returns its local resource name.
    /// </summary>
    public string AddFont(PdfFont font)
    {
        if (!_resources.TryGetValue(font, out var name))
        {
            name = NextFontName;
            _resources[font] = name;
            if (font.Reference == null)
                Owner._irefTable.Add(font);
            Fonts.Elements[name] = font.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified image to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddImage(PdfImage image)
    {
        string name;
        if (!_resources.TryGetValue(image, out name))
        {
            name = NextImageName;
            _resources[image] = name;
            if (image.Reference == null)
                Owner._irefTable.Add(image);
            XObjects.Elements[name] = image.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified form object to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddForm(PdfFormXObject form)
    {
        if (!_resources.TryGetValue(form, out var name))
        {
            name = NextFormName;
            _resources[form] = name;
            if (form.Reference == null)
                Owner._irefTable.Add(form);
            XObjects.Elements[name] = form.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified graphics state to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddExtGState(PdfExtGState extGState)
    {
        if (!_resources.TryGetValue(extGState, out var name))
        {
            name = NextExtGStateName;
            _resources[extGState] = name;
            if (extGState.Reference == null)
                Owner._irefTable.Add(extGState);
            ExtGStates.Elements[name] = extGState.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified pattern to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddPattern(PdfShadingPattern pattern)
    {
        string name;
        if (!_resources.TryGetValue(pattern, out name))
        {
            name = NextPatternName;
            _resources[pattern] = name;
            if (pattern.Reference == null)
                Owner._irefTable.Add(pattern);
            Patterns.Elements[name] = pattern.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified pattern to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddPattern(PdfTilingPattern pattern)
    {
        if (!_resources.TryGetValue(pattern, out var name))
        {
            name = NextPatternName;
            _resources[pattern] = name;
            if (pattern.Reference == null)
                Owner._irefTable.Add(pattern);
            Patterns.Elements[name] = pattern.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified colour space - an array such as a Separation - to this resource
    /// dictionary and returns its local resource name.
    /// </summary>
    internal string AddColorSpace(PdfArray colorSpace)
    {
        if (!_resources.TryGetValue(colorSpace, out var name))
        {
            name = NextColorSpaceName;
            _resources[colorSpace] = name;
            if (colorSpace.Reference == null)
                Owner._irefTable.Add(colorSpace);
            ColorSpaces.Elements[name] = colorSpace.Reference;
        }
        return name;
    }

    /// <summary>
    /// Adds the specified shading to this resource dictionary
    /// and returns its local resource name.
    /// </summary>
    public string AddShading(PdfShading shading)
    {
        if (!_resources.TryGetValue(shading, out var name))
        {
            name = NextShadingName;
            _resources[shading] = name;
            if (shading.Reference == null)
                Owner._irefTable.Add(shading);
            Shadings.Elements[name] = shading.Reference;
        }
        return name;
    }

    /// <summary>
    /// Gets the fonts map.
    /// </summary>
    internal PdfResourceMap Fonts => _fonts ??= (PdfResourceMap)Elements.GetValue(Keys.Font, VCF.Create);

    PdfResourceMap _fonts;

    /// <summary>
    /// Gets the external objects map.
    /// </summary>
    internal PdfResourceMap XObjects => _xObjects ??= (PdfResourceMap)Elements.GetValue(Keys.XObject, VCF.Create);

    PdfResourceMap _xObjects;

    internal PdfResourceMap ExtGStates => _extGStates ??= (PdfResourceMap)Elements.GetValue(Keys.ExtGState, VCF.Create);

    PdfResourceMap _extGStates;

    internal PdfResourceMap ColorSpaces => _colorSpaces ??= (PdfResourceMap)Elements.GetValue(Keys.ColorSpace, VCF.Create);

    PdfResourceMap _colorSpaces;

    internal PdfResourceMap Patterns => _patterns ??= (PdfResourceMap) Elements.GetValue(Keys.Pattern, VCF.Create);

    PdfResourceMap _patterns;

    internal PdfResourceMap Shadings => _shadings ??= (PdfResourceMap) Elements.GetValue(Keys.Shading, VCF.Create);

    PdfResourceMap _shadings;

    internal PdfResourceMap Properties => _properties ??= (PdfResourceMap) Elements.GetValue(Keys.Properties, VCF.Create);

    PdfResourceMap _properties;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextFontName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/F{_fontNumber++}")) { }
            return name;
        }
    }
    int _fontNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextImageName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/I{_imageNumber++}")) { }
            return name;
        }
    }
    int _imageNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextFormName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/Fm{_formNumber++}")) { }
            return name;
        }
    }
    int _formNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextExtGStateName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/GS{_extGStateNumber++}")) { }
            return name;
        }
    }
    int _extGStateNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextPatternName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/Pa{_patternNumber++}")) ;
            return name;
        }
    }
    int _patternNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextShadingName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/Sh{_shadingNumber++}")) ;
            return name;
        }
    }
    int _shadingNumber;

    /// <summary>
    /// Gets a new local name for this resource.
    /// </summary>
    string NextColorSpaceName
    {
        get
        {
            string name;
            while (ExistsResourceNames(name = $"/CS{_colorSpaceNumber++}")) { }
            return name;
        }
    }
    int _colorSpaceNumber;

    /// <summary>
    /// Check whether a resource name is already used in the context of this resource dictionary.
    /// PDF4NET uses GUIDs as resource names, but I think this weapon is to heavy.
    /// </summary>
    internal bool ExistsResourceNames(string name)
    {
        // Collect every name already in this dictionary, whether the page was imported or built
        // here: a name that is taken must not be handed out again, and skipping the collection
        // for a page thought not to be imported could only ever make a clash possible.
        if (_importedResourceNames == null)
        {
            _importedResourceNames = new Dictionary<string, object>();

            if (Elements[Keys.Font] != null)
                Fonts.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.XObject] != null)
                XObjects.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.ExtGState] != null)
                ExtGStates.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.ColorSpace] != null)
                ColorSpaces.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.Pattern] != null)
                Patterns.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.Shading] != null)
                Shadings.CollectResourceNames(_importedResourceNames);

            if (Elements[Keys.Properties] != null)
                Properties.CollectResourceNames(_importedResourceNames);
        }
        return _importedResourceNames.ContainsKey(name);
        // This is superfluous because PDFsharp resource names cannot be double.
        // importedResourceNames.Add(name, null);
    }

    /// <summary>
    /// All the names of imported resources.
    /// </summary>
    Dictionary<string, object> _importedResourceNames;

    /// <summary>
    /// Maps all PDFsharp resources to their local resource names.
    /// </summary>
    readonly Dictionary<PdfObject, string> _resources = new();

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        /// <summary>
        /// (Optional) A dictionary that maps resource names to graphics state
        /// parameter dictionaries.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string ExtGState = "/ExtGState";

        /// <summary>
        /// (Optional) A dictionary that maps each resource name to either the name of a
        /// device-dependent color space or an array describing a color space.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string ColorSpace = "/ColorSpace";

        /// <summary>
        /// (Optional) A dictionary that maps each resource name to either the name of a
        /// device-dependent color space or an array describing a color space.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string Pattern = "/Pattern";

        /// <summary>
        /// (Optional; PDF 1.3) A dictionary that maps resource names to shading dictionaries.
        /// </summary>
        [KeyInfo("1.3", KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string Shading = "/Shading";

        /// <summary>
        /// (Optional) A dictionary that maps resource names to external objects.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string XObject = "/XObject";

        /// <summary>
        /// (Optional) A dictionary that maps resource names to font dictionaries.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string Font = "/Font";

        /// <summary>
        /// (Optional) An array of predefined procedure set names.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string ProcSet = "/ProcSet";

        /// <summary>
        /// (Optional; PDF 1.2) A dictionary that maps resource names to property list
        /// dictionaries for marked content.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResourceMap))]
        public const string Properties = "/Properties";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
