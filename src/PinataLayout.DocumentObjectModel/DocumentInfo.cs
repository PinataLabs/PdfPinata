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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Contains information about document content, author etc.
/// </summary>
public partial class DocumentInfo : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the DocumentInfo class.
  /// </summary>
  public DocumentInfo()
  {
  }

  /// <summary>
  /// Initializes a new instance of the DocumentInfo class with the specified parent.
  /// </summary>
  internal DocumentInfo(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new DocumentInfo Clone()
  {
    return (DocumentInfo)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the document title.
  /// </summary>
  public string Title
  {
    get => title ?? "";
    set => title = value;
  }
  [DV]
  internal string title;

  /// <summary>
  /// Gets or sets the document author.
  /// </summary>
  public string Author
  {
    get => author ?? "";
    set => author = value;
  }
  [DV]
  internal string author;

  /// <summary>
  /// Gets or sets keywords related to the document.
  /// </summary>
  public string Keywords
  {
    get => keywords ?? "";
    set => keywords = value;
  }
  [DV]
  internal string keywords;

  /// <summary>
  /// Gets or sets the subject of the document.
  /// </summary>
  public string Subject
  {
    get => subject ?? "";
    set => subject = value;
  }
  [DV]
  internal string subject;

  /// <summary>
  /// Gets or sets a comment associated with this object.
  /// </summary>
  public string Comment
  {
    get => comment ?? "";
    set => comment = value;
  }
  [DV]
  internal string comment;
  #endregion

  #region Internal
  /// <summary>
  /// Converts DocumentInfo into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteComment((comment ?? ""));
    int pos = serializer.BeginContent("Info");

    // Whether a value was set decides whether it is written, as everywhere else in the DOM. These
    // four used to ask whether the string was empty instead, which wrote an assigned "" the same
    // way it wrote one that had never been assigned - not at all - and lost the difference over a
    // round trip.
    if (title != null)
      serializer.WriteSimpleAttribute("Title", Title);

    if (subject != null)
      serializer.WriteSimpleAttribute("Subject", Subject);

    if (author != null)
      serializer.WriteSimpleAttribute("Author", Author);

    if (keywords != null)
      serializer.WriteSimpleAttribute("Keywords", Keywords);

    serializer.EndContent(pos);
  }

  #endregion
}
