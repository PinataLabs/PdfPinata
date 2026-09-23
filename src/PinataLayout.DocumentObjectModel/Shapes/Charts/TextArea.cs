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
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Visitors;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// An area object in the chart which contain text or legend.
/// </summary>
public partial class TextArea : ChartObject, IVisitable
{
  /// <summary>
  /// Initializes a new instance of the TextArea class.
  /// </summary>
  internal TextArea()
  {
  }

  /// <summary>
  /// Initializes a new instance of the TextArea class with the specified parent.
  /// </summary>
  internal TextArea(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new TextArea Clone()
  {
    return (TextArea)DeepCopy();
  }

  /// <summary>
  /// Adds a new paragraph to the text area.
  /// </summary>
  public Paragraph AddParagraph()
  {
    return Elements.AddParagraph();
  }

  /// <summary>
  /// Adds a new paragraph with the specified text to the text area.
  /// </summary>
  public Paragraph AddParagraph(string paragraphText)
  {
    return Elements.AddParagraph(paragraphText);
  }

  /// <summary>
  /// Adds a new table to the text area.
  /// </summary>
  public Table AddTable()
  {
    return Elements.AddTable();
  }

  /// <summary>
  /// Adds a new Image to the text area.
  /// </summary>
  public Image AddImage(IImageSource imageSource)
  {
    return Elements.AddImage(imageSource);
  }

  /// <summary>
  /// Adds a new legend to the text area.
  /// </summary>
  public Legend AddLegend()
  {
    return Elements.AddLegend();
  }

  /// <summary>
  /// Adds a new paragraph to the text area.
  /// </summary>
  public void Add(Paragraph paragraph)
  {
    Elements.Add(paragraph);
  }

  /// <summary>
  /// Adds a new table to the text area.
  /// </summary>
  public void Add(Table table)
  {
    Elements.Add(table);
  }

  /// <summary>
  /// Adds a new image to the text area.
  /// </summary>
  public void Add(Image image)
  {
    Elements.Add(image);
  }

  /// <summary>
  /// Adds a new legend to the text area.
  /// </summary>
  public void Add(Legend legend)
  {
    Elements.Add(legend);
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the height of the area.
  /// </summary>
  public Unit Height
  {
    get => height;
    set => height = value;
  }
  [DV]
  internal Unit height = Unit.NullValue;

  /// <summary>
  /// Gets or sets the width of the area.
  /// </summary>
  public Unit Width
  {
    get => width;
    set => width = value;
  }
  [DV]
  internal Unit width = Unit.NullValue;

  /// <summary>
  /// Gets or sets the default style name of the area.
  /// </summary>
  public string Style
  {
    get => style ?? "";
    set => style = value;
  }
  [DV]
  internal string style;

  /// <summary>
  /// Gets or sets the default paragraph format of the area.
  /// </summary>
  public ParagraphFormat Format
  {
    get
    {
      if (format == null)
        format = new ParagraphFormat(this);

      return format;
    }
    set
    {
      SetParent(value);
      format = value;
    }
  }
  [DV]
  internal ParagraphFormat format;

  /// <summary>
  /// Gets the line format of the area's border.
  /// </summary>
  public LineFormat LineFormat
  {
    get
    {
      if (lineFormat == null)
        lineFormat = new LineFormat(this);

      return lineFormat;
    }
    set
    {
      SetParent(value);
      lineFormat = value;
    }
  }
  [DV]
  internal LineFormat lineFormat;

  /// <summary>
  /// Gets the background filling of the area.
  /// </summary>
  public FillFormat FillFormat
  {
    get
    {
      if (fillFormat == null)
        fillFormat = new FillFormat(this);

      return fillFormat;
    }
    set
    {
      SetParent(value);
      fillFormat = value;
    }
  }
  [DV]
  internal FillFormat fillFormat;

  /// <summary>
  /// Gets or sets the left padding of the area.
  /// </summary>
  public Unit LeftPadding
  {
    get => leftPadding;
    set => leftPadding = value;
  }
  [DV]
  internal Unit leftPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the right padding of the area.
  /// </summary>
  public Unit RightPadding
  {
    get => rightPadding;
    set => rightPadding = value;
  }
  [DV]
  internal Unit rightPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the top padding of the area.
  /// </summary>
  public Unit TopPadding
  {
    get => topPadding;
    set => topPadding = value;
  }
  [DV]
  internal Unit topPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the bottom padding of the area.
  /// </summary>
  public Unit BottomPadding
  {
    get => bottomPadding;
    set => bottomPadding = value;
  }
  [DV]
  internal Unit bottomPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the Vertical alignment of the area.
  /// </summary>
  public VerticalAlignment VerticalAlignment
  {
    get => verticalAlignment ?? default;
    set => verticalAlignment = EnumGuard.Checked(value);
  }
  [DV]
  internal VerticalAlignment? verticalAlignment;

  /// <summary>
  /// Gets the document objects that creates the text area.
  /// </summary>
  public DocumentElements Elements
  {
    get
    {
      if (elements == null)
        elements = new DocumentElements(this);

      return elements;
    }
    set
    {
      SetParent(value);
      elements = value;
    }
  }
  [DV]
  internal DocumentElements elements;
  #endregion

  #region Internal
  /// <summary>
  /// Converts TextArea into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    var chartObject = parent as Chart;

    // ReSharper disable once PossibleNullReferenceException
    serializer.WriteLine("\\" + chartObject.CheckTextArea(this));
    var pos = serializer.BeginAttributes();

    if (style != null)
      serializer.WriteSimpleAttribute("Style", Style);
    if (!IsNull("Format"))
      format.Serialize(serializer, "Format", null);

    if (!topPadding.IsNull)
      serializer.WriteSimpleAttribute("TopPadding", TopPadding);
    if (!leftPadding.IsNull)
      serializer.WriteSimpleAttribute("LeftPadding", LeftPadding);
    if (!rightPadding.IsNull)
      serializer.WriteSimpleAttribute("RightPadding", RightPadding);
    if (!bottomPadding.IsNull)
      serializer.WriteSimpleAttribute("BottomPadding", BottomPadding);

    if (!width.IsNull)
      serializer.WriteSimpleAttribute("Width", Width);
    if (!height.IsNull)
      serializer.WriteSimpleAttribute("Height", Height);

    if (verticalAlignment != null)
      serializer.WriteSimpleAttribute("VerticalAlignment", VerticalAlignment);

    if (!IsNull("LineFormat"))
      lineFormat.Serialize(serializer);
    if (!IsNull("FillFormat"))
      fillFormat.Serialize(serializer);

    serializer.EndAttributes(pos);

    serializer.BeginContent();
    elements?.Serialize(serializer);
    serializer.EndContent();
  }

  #endregion

  void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
  {
    visitor.VisitTextArea(this);
    if (elements != null && visitChildren)
      // ReSharper disable once ConditionIsAlwaysTrueOrFalse
      ((IVisitable)elements).AcceptVisitor(visitor, visitChildren);
  }
}
