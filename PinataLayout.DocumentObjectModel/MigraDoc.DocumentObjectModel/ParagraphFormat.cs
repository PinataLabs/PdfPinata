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
using PdfPinata.Text;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A ParagraphFormat represents the formatting of a paragraph.
/// </summary>
public partial class ParagraphFormat : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the ParagraphFormat class that can be used as a template.
  /// </summary>
  public ParagraphFormat()
  {
  }

  /// <summary>
  /// Initializes a new instance of the ParagraphFormat class with the specified parent.
  /// </summary>
  internal ParagraphFormat(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new ParagraphFormat Clone()
  {
    return (ParagraphFormat)DeepCopy();
  }

  /// <summary>
  /// Adds a TabStop object to the collection.
  /// </summary>
  public TabStop AddTabStop(Unit position)
  {
    return TabStops.AddTabStop(position);
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its alignment and leader.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabAlignment alignment, TabLeader leader)
  {
    return TabStops.AddTabStop(position, alignment, leader);
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its leader.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabLeader leader)
  {
    return TabStops.AddTabStop(position, leader);
  }

  /// <summary>
  /// Adds a TabStop object to the collection and sets its alignment.
  /// </summary>
  public TabStop AddTabStop(Unit position, TabAlignment alignment)
  {
    return TabStops.AddTabStop(position, alignment);
  }

  /// <summary>
  /// Adds a TabStop object to the collection marked to remove the tab stop at
  /// the given position.
  /// </summary>
  public void RemoveTabStop(Unit position)
  {
    TabStops.RemoveTabStop(position);
  }

  /// <summary>
  /// Adds a TabStop object to the collection.
  /// </summary>
  public void Add(TabStop tabStop)
  {
    TabStops.AddTabStop(tabStop);
  }

  /// <summary>
  /// Clears all TapStop objects from the collection. Additionally 'TabStops = null'
  /// is written to the DDL stream when serialized.
  /// </summary>
  public void ClearAll()
  {
    TabStops.ClearAll();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the Alignment of the paragraph.
  /// </summary>
  public ParagraphAlignment Alignment
  {
    get => alignment ?? default;
    set { ThrowIfReadOnly(); alignment = EnumGuard.Checked(value); }
  }
  [DV]
  internal ParagraphAlignment? alignment;

  /// <summary>
  /// Gets the Borders object.
  /// </summary>
  public Borders Borders
  {
    get
    {
      if (borders == null)
        borders = new Borders(this);

      return borders;
    }
    set
    {
      ThrowIfReadOnly();
      SetParent(value);
      borders = value;
    }
  }
  [DV]
  internal Borders borders;

  /// <summary>
  /// Gets or sets the indent of the first line in the paragraph.
  /// </summary>
  public Unit FirstLineIndent
  {
    get => firstLineIndent;
    set { ThrowIfReadOnly(); firstLineIndent = value; }
  }
  [DV]
  internal Unit firstLineIndent = Unit.NullValue;

  /// <summary>
  /// Gets or sets the Font object.
  /// </summary>
  public Font Font
  {
    get
    {
      if (font == null)
        font = new Font(this);

      return font;
    }
    set
    {
      ThrowIfReadOnly();
      SetParent(value);
      font = value;
    }
  }
  [DV]
  internal Font font;

  /// <summary>
  /// Gets or sets a value indicating whether to keep all the paragraph's lines on the same page.
  /// </summary>
  public bool KeepTogether
  {
    get => keepTogether ?? false;
    set { ThrowIfReadOnly(); keepTogether = value; }
  }
  [DV]
  internal bool? keepTogether;

  /// <summary>
  /// Gets or sets a value indicating whether this and the next paragraph stay on the same page.
  /// </summary>
  public bool KeepWithNext
  {
    get => keepWithNext ?? false;
    set { ThrowIfReadOnly(); keepWithNext = value; }
  }
  [DV]
  internal bool? keepWithNext;

  /// <summary>
  /// Gets or sets the left indent of the paragraph.
  /// </summary>
  public Unit LeftIndent
  {
    get => leftIndent;
    set { ThrowIfReadOnly(); leftIndent = value; }
  }
  [DV]
  internal Unit leftIndent = Unit.NullValue;

  /// <summary>
  /// Gets or sets the space between lines on the paragraph.
  /// </summary>
  public Unit LineSpacing
  {
    get => lineSpacing;
    set { ThrowIfReadOnly(); lineSpacing = value; }
  }
  [DV]
  internal Unit lineSpacing = Unit.NullValue;

  /// <summary>
  /// Gets or sets the rule which is used to define the line spacing.
  /// </summary>
  public LineSpacingRule LineSpacingRule
  {
    get => lineSpacingRule ?? default;
    set { ThrowIfReadOnly(); lineSpacingRule = EnumGuard.Checked(value); }
  }
  [DV]
  internal LineSpacingRule? lineSpacingRule;

  /// <summary>
  /// Gets or sets the ListInfo object of the paragraph.
  /// </summary>
  public ListInfo ListInfo
  {
    get
    {
      if (listInfo == null)
        listInfo = new ListInfo(this);

      return listInfo;
    }
    set
    {
      ThrowIfReadOnly();
      SetParent(value);
      listInfo = value;
    }
  }
  [DV]
  internal ListInfo listInfo;

  /// <summary>
  /// Gets or sets the out line level of the paragraph.
  /// </summary>
  public OutlineLevel OutlineLevel
  {
    get => outlineLevel ?? default;
    set { ThrowIfReadOnly(); outlineLevel = EnumGuard.Checked(value); }
  }
  [DV]
  internal OutlineLevel? outlineLevel;

  /// <summary>
  /// Gets or sets a value indicating whether a page break is inserted before the paragraph.
  /// </summary>
  public bool PageBreakBefore
  {
    get => pageBreakBefore ?? false;
    set { ThrowIfReadOnly(); pageBreakBefore = value; }
  }
  [DV]
  internal bool? pageBreakBefore;

  /// <summary>
  /// Gets or sets the right indent of the paragraph.
  /// </summary>
  public Unit RightIndent
  {
    get => rightIndent;
    set { ThrowIfReadOnly(); rightIndent = value; }
  }
  [DV]
  internal Unit rightIndent = Unit.NullValue;

  /// <summary>
  /// Gets the shading object.
  /// </summary>
  public Shading Shading
  {
    get
    {
      if (shading == null)
        shading = new Shading(this);

      return shading;
    }
    set
    {
      ThrowIfReadOnly();
      SetParent(value);
      shading = value;
    }
  }
  [DV]
  internal Shading shading;

  /// <summary>
  /// Gets or sets the space that's inserted after the paragraph.
  /// </summary>
  public Unit SpaceAfter
  {
    get => spaceAfter;
    set { ThrowIfReadOnly(); spaceAfter = value; }
  }
  [DV]
  internal Unit spaceAfter = Unit.NullValue;

  /// <summary>
  /// Gets or sets the space that's inserted before the paragraph.
  /// </summary>
  public Unit SpaceBefore
  {
    get => spaceBefore;
    set { ThrowIfReadOnly(); spaceBefore = value; }
  }
  [DV]
  internal Unit spaceBefore = Unit.NullValue;

  /// <summary>
  /// Indicates whether the ParagraphFormat has a TabStops collection.
  /// </summary>
  public bool HasTabStops => tabStops != null;

  /// <summary>
  /// Get the TabStops collection.
  /// </summary>
  public TabStops TabStops
  {
    get
    {
      if (tabStops == null)
        tabStops = new TabStops(this);

      return tabStops;
    }
    set
    {
      ThrowIfReadOnly();
      SetParent(value);
      tabStops = value;
    }
  }
  [DV]
  internal TabStops tabStops;

  /// <summary>
  /// Gets or sets which way the paragraph runs. The default is
  /// <see cref="BidiParagraphDirection.Automatic"/>, which reads it off the text itself.
  /// </summary>
  /// <remarks>
  /// The Unicode Bidirectional Algorithm takes the direction from the first strong character it
  /// finds, which is right far more often than not and wrong in the cases that matter: a paragraph
  /// of Hebrew opening with a Latin brand name, a date, or a quotation mark. This says what the
  /// paragraph is rather than leaving it to be guessed - and it is one answer for the whole
  /// paragraph, where the guess is made afresh for every line of it.
  /// <para>
  /// A line whose words have to change places to be read is laid out in that order. A line
  /// containing a tab is not: where a tabbed line's columns should sit in a right-to-left paragraph
  /// is a question this does not answer, so such a line keeps the order it was written in.
  /// </para>
  /// </remarks>
  public BidiParagraphDirection TextDirection
  {
    get => textDirection ?? BidiParagraphDirection.Automatic;
    set { ThrowIfReadOnly(); textDirection = EnumGuard.Checked(value); }
  }
  [DV]
  internal BidiParagraphDirection? textDirection;

  /// <summary>
  /// Gets or sets a value indicating whether a line from the paragraph stays alone in a page.
  /// </summary>
  public bool WidowControl
  {
    get => widowControl ?? false;
    set { ThrowIfReadOnly(); widowControl = value; }
  }
  [DV]
  internal bool? widowControl;
  #endregion

  #region Internal
  /// <summary>
  /// Converts ParagraphFormat into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if (parent is Style)
      Serialize(serializer, "ParagraphFormat", null);
    else
      Serialize(serializer, "Format", null);
  }

  /// <summary>
  /// Converts ParagraphFormat into DDL.
  /// </summary>
  internal void Serialize(Serializer serializer, string name, ParagraphFormat refFormat)
  {
    int pos = serializer.BeginContent(name);

    if (!IsNull("Font") && Parent.GetType() != typeof(Style))
      Font.Serialize(serializer);

    // If a refFormat is specified, it is important to compare the fields and not the properties.
    // Only the fields holds the internal information whether a value is NULL. In contrast to the
    // Efw.Application framework the nullable values and all the meta stuff is kept internal to
    // give the user the illusion of simplicity.

    if (alignment != null && (refFormat == null || (alignment != refFormat.alignment)))
      serializer.WriteSimpleAttribute("Alignment", Alignment);

    if (!leftIndent.IsNull && (refFormat == null || (leftIndent != refFormat.leftIndent)))
      serializer.WriteSimpleAttribute("LeftIndent", LeftIndent);

    if (!firstLineIndent.IsNull && (refFormat == null || firstLineIndent != refFormat.firstLineIndent))
      serializer.WriteSimpleAttribute("FirstLineIndent", FirstLineIndent);

    if (!rightIndent.IsNull && (refFormat == null || rightIndent != refFormat.rightIndent))
      serializer.WriteSimpleAttribute("RightIndent", RightIndent);

    if (!spaceBefore.IsNull && (refFormat == null || spaceBefore != refFormat.spaceBefore))
      serializer.WriteSimpleAttribute("SpaceBefore", SpaceBefore);

    if (!spaceAfter.IsNull && (refFormat == null || spaceAfter != refFormat.spaceAfter))
      serializer.WriteSimpleAttribute("SpaceAfter", SpaceAfter);

    if (lineSpacingRule != null && (refFormat == null || lineSpacingRule != refFormat.lineSpacingRule))
      serializer.WriteSimpleAttribute("LineSpacingRule", LineSpacingRule);

    if (!lineSpacing.IsNull && (refFormat == null || lineSpacing != refFormat.lineSpacing))
      serializer.WriteSimpleAttribute("LineSpacing", LineSpacing);

    if (keepTogether != null && (refFormat == null || keepTogether != refFormat.keepTogether))
      serializer.WriteSimpleAttribute("KeepTogether", KeepTogether);

    if (keepWithNext != null && (refFormat == null || keepWithNext != refFormat.keepWithNext))
      serializer.WriteSimpleAttribute("KeepWithNext", KeepWithNext);

    if (textDirection != null && (refFormat == null || textDirection != refFormat.textDirection))
      serializer.WriteSimpleAttribute("TextDirection", TextDirection);

    if (widowControl != null && (refFormat == null || widowControl != refFormat.widowControl))
      serializer.WriteSimpleAttribute("WidowControl", WidowControl);

    if (pageBreakBefore != null && (refFormat == null || pageBreakBefore != refFormat.pageBreakBefore))
      serializer.WriteSimpleAttribute("PageBreakBefore", PageBreakBefore);

    if (outlineLevel != null && (refFormat == null || outlineLevel != refFormat.outlineLevel))
      serializer.WriteSimpleAttribute("OutlineLevel", OutlineLevel);

    if (!IsNull("ListInfo"))
      ListInfo.Serialize(serializer);

    if (!IsNull("TabStops"))
      tabStops.Serialize(serializer);

    if (!IsNull("Borders"))
    {
      if (refFormat != null)
        borders.Serialize(serializer, refFormat.Borders);
      else
        borders.Serialize(serializer, null);
    }

    if (!IsNull("Shading"))
      shading.Serialize(serializer);

    serializer.EndContent(pos);
  }

  #endregion
}
