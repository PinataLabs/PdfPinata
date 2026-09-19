#region Copyright
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

namespace PinataLayout.DocumentObjectModel.Fields;

/// <summary>
/// PageRefField is used to reference the page number of a bookmark in the document.
/// </summary>
public partial class PageRefField : NumericFieldBase
{
  /// <summary>
  /// Initializes a new instance of the PageRefField class.
  /// </summary>    
  internal PageRefField()
  {
  }

  /// <summary>
  /// Initializes a new instance of the PageRefField class with the necessary bookmark name.
  /// </summary>
  public PageRefField(string name)
    : this()
  {
    this.Name = name;
  }

  /// <summary>
  /// Initializes a new instance of the PageRefField class with the specified parent.
  /// </summary>
  internal PageRefField(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new PageRefField Clone()
  {
    return (PageRefField)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the bookmark name whose page is to be shown.
  /// </summary>
  public string Name
  {
    get => this.name ?? "";
    set => this.name = value;
  }
  [DV]
  internal string name;
  #endregion

  #region Internal
  /// <summary>
  /// Converts PageRefField into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    var str = "\\field(PageRef)";
    str += "[Name = \"" + this.Name + "\"";

    if ((this.format ?? "") != "")
      str += " Format = \"" + this.Format + "\"";
    str += "]";

    serializer.Write(str);
  }

  #endregion
}
