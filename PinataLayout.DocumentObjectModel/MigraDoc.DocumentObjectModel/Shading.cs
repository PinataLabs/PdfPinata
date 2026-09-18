#region MigraDoc - Creating Documents on the Fly
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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

using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Shading represents the background color of a document object.
/// </summary>
public sealed partial class Shading : DocumentObject
{
    /// <summary>
    /// Initializes a new instance of the Shading class.
    /// </summary>
    public Shading()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Shading class with the specified parent.
    /// </summary>
    internal Shading(DocumentObject parent) : base(parent) { }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Shading Clone()
    {
        return (Shading)DeepCopy();
    }

    /// <summary>
    /// Clears the Shading object. Additionally 'Shading = null'
    /// is written to the DDL stream when serialized.
    /// </summary>
    public void Clear()
    {
        isCleared = true;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets a value indicating whether the shading is visible.
    /// </summary>
    public bool Visible
    {
        get => visible ?? false;
        set => visible = value;
    }
    [DV]
    internal bool? visible;

    /// <summary>
    /// Gets or sets the shading color.
    /// </summary>
    public Color Color
    {
        get => color;
        set => color = value;
    }
    [DV]
    internal Color color = Color.Empty;

    /// <summary>
    /// Gets the information if the shading is marked as cleared. Additionally 'Shading = null'
    /// is written to the DDL stream when serialized.
    /// </summary>
    public bool IsCleared => isCleared;

    internal bool isCleared = false;
    #endregion

    #region Null handling
    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    /// <remarks>
    /// A cleared shading is not null, for the same reason a cleared Border is not - see
    /// Border.IsNull. isCleared carries no [DV] attribute, so the value descriptors Meta.IsNull
    /// consults cannot see it.
    /// </remarks>
    public override bool IsNull()
    {
        return !isCleared && base.IsNull();
    }

    /// <summary>
    /// Resets this instance, i.e. IsNull() will return true afterwards.
    /// </summary>
    public override void SetNull()
    {
        base.SetNull();
        isCleared = false;
    }
    #endregion

    #region Internal
    /// <summary>
    /// Converts Shading into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        if (isCleared)
            serializer.WriteLine("Shading = null");

        int pos = serializer.BeginContent("Shading");

        if (visible != null)
            serializer.WriteSimpleAttribute("Visible", Visible);

        if (!color.IsNull)
            serializer.WriteSimpleAttribute("Color", Color);

        serializer.EndContent(pos);
    }

    #endregion
}
