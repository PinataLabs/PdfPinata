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

using System.Diagnostics;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents the page setup of a section.
/// </summary>
public partial class PageSetup : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the PageSetup class.
  /// </summary>
  public PageSetup()
  {
  }

  /// <summary>
  /// Initializes a new instance of the PageSetup class with the specified parent.
  /// </summary>
  internal PageSetup(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new PageSetup Clone()
  {
    return (PageSetup)DeepCopy();
  }

  /// <summary>
  /// Gets the page's size and height for the given PageFormat.
  /// </summary>
  /// <remarks>
  /// Each sheet is built from the unit that defines it: the ISO and DIN formats from whole
  /// millimetres, the North American and traditional formats from whole inches, at 72 points to
  /// the inch. Rounding either one into the other would move a page by a fraction of a millimetre
  /// and buy nothing.
  /// </remarks>
  public static void GetPageSize(PageFormat pageFormat, out Unit pageWidth, out Unit pageHeight)
  {
    switch (pageFormat)
    {
      // ISO 216 A series.
      case PageFormat.A0: Millimeter(841, 1189, out pageWidth, out pageHeight); return;
      case PageFormat.A1: Millimeter(594, 841, out pageWidth, out pageHeight); return;
      case PageFormat.A2: Millimeter(420, 594, out pageWidth, out pageHeight); return;
      case PageFormat.A3: Millimeter(297, 420, out pageWidth, out pageHeight); return;
      case PageFormat.A4: Millimeter(210, 297, out pageWidth, out pageHeight); return;
      case PageFormat.A5: Millimeter(148, 210, out pageWidth, out pageHeight); return;
      case PageFormat.A6: Millimeter(105, 148, out pageWidth, out pageHeight); return;
      case PageFormat.A7: Millimeter(74, 105, out pageWidth, out pageHeight); return;
      case PageFormat.A8: Millimeter(52, 74, out pageWidth, out pageHeight); return;
      case PageFormat.A9: Millimeter(37, 52, out pageWidth, out pageHeight); return;
      case PageFormat.A10: Millimeter(26, 37, out pageWidth, out pageHeight); return;

      // DIN 476 oversizes.
      case PageFormat.TwoA0: Millimeter(1189, 1682, out pageWidth, out pageHeight); return;
      case PageFormat.FourA0: Millimeter(1682, 2378, out pageWidth, out pageHeight); return;

      // ISO 216 B series.
      case PageFormat.B0: Millimeter(1000, 1414, out pageWidth, out pageHeight); return;
      case PageFormat.B1: Millimeter(707, 1000, out pageWidth, out pageHeight); return;
      case PageFormat.B2: Millimeter(500, 707, out pageWidth, out pageHeight); return;
      case PageFormat.B3: Millimeter(353, 500, out pageWidth, out pageHeight); return;
      case PageFormat.B4: Millimeter(250, 353, out pageWidth, out pageHeight); return;
      case PageFormat.B5: Millimeter(176, 250, out pageWidth, out pageHeight); return;
      case PageFormat.B6: Millimeter(125, 176, out pageWidth, out pageHeight); return;
      case PageFormat.B7: Millimeter(88, 125, out pageWidth, out pageHeight); return;
      case PageFormat.B8: Millimeter(62, 88, out pageWidth, out pageHeight); return;
      case PageFormat.B9: Millimeter(44, 62, out pageWidth, out pageHeight); return;
      case PageFormat.B10: Millimeter(31, 44, out pageWidth, out pageHeight); return;
      case PageFormat.JISB5: Millimeter(182, 257, out pageWidth, out pageHeight); return;

      // ISO 269 C series, the envelopes.
      case PageFormat.C0: Millimeter(917, 1297, out pageWidth, out pageHeight); return;
      case PageFormat.C1: Millimeter(648, 917, out pageWidth, out pageHeight); return;
      case PageFormat.C2: Millimeter(458, 648, out pageWidth, out pageHeight); return;
      case PageFormat.C3: Millimeter(324, 458, out pageWidth, out pageHeight); return;
      case PageFormat.C4: Millimeter(229, 324, out pageWidth, out pageHeight); return;
      case PageFormat.C5: Millimeter(162, 229, out pageWidth, out pageHeight); return;
      case PageFormat.C6: Millimeter(114, 162, out pageWidth, out pageHeight); return;
      case PageFormat.C7: Millimeter(81, 114, out pageWidth, out pageHeight); return;
      case PageFormat.C8: Millimeter(57, 81, out pageWidth, out pageHeight); return;
      case PageFormat.C9: Millimeter(40, 57, out pageWidth, out pageHeight); return;
      case PageFormat.C10: Millimeter(28, 40, out pageWidth, out pageHeight); return;

      // ISO 217 untrimmed stock.
      case PageFormat.RA0: Millimeter(860, 1220, out pageWidth, out pageHeight); return;
      case PageFormat.RA1: Millimeter(610, 860, out pageWidth, out pageHeight); return;
      case PageFormat.RA2: Millimeter(430, 610, out pageWidth, out pageHeight); return;
      case PageFormat.RA3: Millimeter(305, 430, out pageWidth, out pageHeight); return;
      case PageFormat.RA4: Millimeter(215, 305, out pageWidth, out pageHeight); return;
      case PageFormat.RA5: Millimeter(153, 215, out pageWidth, out pageHeight); return;
      case PageFormat.SRA0: Millimeter(900, 1280, out pageWidth, out pageHeight); return;
      case PageFormat.SRA1: Millimeter(640, 900, out pageWidth, out pageHeight); return;
      case PageFormat.SRA2: Millimeter(450, 640, out pageWidth, out pageHeight); return;
      case PageFormat.SRA3: Millimeter(320, 450, out pageWidth, out pageHeight); return;
      case PageFormat.SRA4: Millimeter(225, 320, out pageWidth, out pageHeight); return;

      // North American sizes.
      case PageFormat.Letter: Inch(8.5, 11, out pageWidth, out pageHeight); return;
      case PageFormat.Legal: Inch(8.5, 14, out pageWidth, out pageHeight); return;
      case PageFormat.Ledger: Inch(17, 11, out pageWidth, out pageHeight); return;
      case PageFormat.Tabloid:
      case PageFormat.P11x17: Inch(11, 17, out pageWidth, out pageHeight); return;
      case PageFormat.Executive: Inch(7.25, 10.5, out pageWidth, out pageHeight); return;
      case PageFormat.GovernmentLetter: Inch(8, 10.5, out pageWidth, out pageHeight); return;
      case PageFormat.Statement:
      case PageFormat.STMT: Inch(5.5, 8.5, out pageWidth, out pageHeight); return;
      case PageFormat.Folio: Inch(8.5, 13, out pageWidth, out pageHeight); return;
      case PageFormat.Size10x14: Inch(10, 14, out pageWidth, out pageHeight); return;

      // Traditional British sizes.
      case PageFormat.Quarto: Inch(8, 10, out pageWidth, out pageHeight); return;
      case PageFormat.Foolscap: Inch(8, 13, out pageWidth, out pageHeight); return;
      case PageFormat.Post: Inch(15.5, 19.25, out pageWidth, out pageHeight); return;
      case PageFormat.Crown: Inch(20, 15, out pageWidth, out pageHeight); return;
      case PageFormat.LargePost: Inch(16.5, 21, out pageWidth, out pageHeight); return;
      case PageFormat.Demy: Inch(17.5, 22, out pageWidth, out pageHeight); return;
      case PageFormat.Medium: Inch(18, 23, out pageWidth, out pageHeight); return;
      case PageFormat.Royal: Inch(20, 25, out pageWidth, out pageHeight); return;
      case PageFormat.Elephant: Inch(23, 28, out pageWidth, out pageHeight); return;
      case PageFormat.DoubleDemy: Inch(23.5, 35, out pageWidth, out pageHeight); return;
      case PageFormat.QuadDemy: Inch(35, 45, out pageWidth, out pageHeight); return;
    }

    // A value that names no format has no size. PageSetup.PageFormat refuses one, so this is
    // reachable only by calling here with an integer cast to the enumeration, and it answers what
    // it has always answered: zero by zero.
    pageWidth = 0;
    pageHeight = 0;
  }

  private static void Millimeter(int width, int height, out Unit pageWidth, out Unit pageHeight)
  {
    pageWidth = Unit.FromMillimeter(width);
    pageHeight = Unit.FromMillimeter(height);
  }

  /// <summary>
  /// Takes inches and returns points, at 72 to the inch. A Unit remembers the unit it was made
  /// from and writes it out as a suffix, so building these with Unit.FromInch would turn the 612
  /// that a serialized Letter page has always carried into 8.5in.
  /// </summary>
  private static void Inch(double width, double height, out Unit pageWidth, out Unit pageHeight)
  {
    pageWidth = Unit.FromPoint(width * 72);
    pageHeight = Unit.FromPoint(height * 72);
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets a value which defines whether the section starts on next, odd or even page.
  /// </summary>
  public BreakType SectionStart
  {
    get => sectionStart ?? default;
    set => sectionStart = EnumGuard.Checked(value);
  }
  [DV]
  internal BreakType? sectionStart;

  /// <summary>
  /// Gets or sets the page orientation of the section.
  /// </summary>
  public Orientation Orientation
  {
    get => orientation ?? default;
    set => orientation = EnumGuard.Checked(value);
  }
  [DV]
  internal Orientation? orientation;

  /// <summary>
  /// Gets or sets the page width.
  /// </summary>
  public Unit PageWidth
  {
    get => pageWidth;
    set => pageWidth = value;
  }
  [DV]
  internal Unit pageWidth = Unit.NullValue;

  /// <summary>
  /// Gets or sets the starting number for the first section page.
  /// </summary>
  public int StartingNumber
  {
    get => startingNumber ?? 0;
    set => startingNumber = value;
  }
  [DV]
  internal int? startingNumber;

  /// <summary>
  /// Gets or sets the page height.
  /// </summary>
  public Unit PageHeight
  {
    get => pageHeight;
    set => pageHeight = value;
  }
  [DV]
  internal Unit pageHeight = Unit.NullValue;

  /// <summary>
  /// Gets or sets the top margin of the pages in the section.
  /// </summary>
  public Unit TopMargin
  {
    get => topMargin;
    set => topMargin = value;
  }
  [DV]
  internal Unit topMargin = Unit.NullValue;

  /// <summary>
  /// Gets or sets the bottom margin of the pages in the section.
  /// </summary>
  public Unit BottomMargin
  {
    get => bottomMargin;
    set => bottomMargin = value;
  }
  [DV]
  internal Unit bottomMargin = Unit.NullValue;

  /// <summary>
  /// Gets or sets the left margin of the pages in the section.
  /// </summary>
  public Unit LeftMargin
  {
    get => leftMargin;
    set => leftMargin = value;
  }
  [DV]
  internal Unit leftMargin = Unit.NullValue;

  /// <summary>
  /// Gets or sets the right margin of the pages in the section.
  /// </summary>
  public Unit RightMargin
  {
    get => rightMargin;
    set => rightMargin = value;
  }
  [DV]
  internal Unit rightMargin = Unit.NullValue;

  /// <summary>
  /// Gets or sets a value which defines whether the odd and even pages
  /// of the section have different header and footer.
  /// </summary>
  public bool OddAndEvenPagesHeaderFooter
  {
    get => oddAndEvenPagesHeaderFooter ?? false;
    set => oddAndEvenPagesHeaderFooter = value;
  }
  [DV]
  internal bool? oddAndEvenPagesHeaderFooter;

  /// <summary>
  /// Gets or sets a value which define whether the section has a different
  /// first page header and footer.
  /// </summary>
  public bool DifferentFirstPageHeaderFooter
  {
    get => differentFirstPageHeaderFooter ?? false;
    set => differentFirstPageHeaderFooter = value;
  }
  [DV]
  internal bool? differentFirstPageHeaderFooter;

  /// <summary>
  /// Gets or sets the distance between the header and the page top
  /// of the pages in the section.
  /// </summary>
  public Unit HeaderDistance
  {
    get => headerDistance;
    set => headerDistance = value;
  }
  [DV]
  internal Unit headerDistance = Unit.NullValue;

  /// <summary>
  /// Gets or sets the distance between the footer and the page bottom
  /// of the pages in the section.
  /// </summary>
  public Unit FooterDistance
  {
    get => footerDistance;
    set => footerDistance = value;
  }
  [DV]
  internal Unit footerDistance = Unit.NullValue;

  /// <summary>
  /// Gets or sets a value which defines whether the odd and even pages
  /// of the section should change left and right margin.
  /// </summary>
  public bool MirrorMargins
  {
    get => mirrorMargins ?? false;
    set => mirrorMargins = value;
  }
  [DV]
  internal bool? mirrorMargins;

  /// <summary>
  /// Gets or sets a value which defines whether a page should break horizontally.
  /// Currently only tables are supported.
  /// </summary>
  public bool HorizontalPageBreak
  {
    get => horizontalPageBreak ?? false;
    set => horizontalPageBreak = value;
  }
  [DV]
  internal bool? horizontalPageBreak;

  /// <summary>
  /// Gets or sets the page format of the section.
  /// </summary>
  public PageFormat PageFormat
  {
    get => pageFormat ?? default;
    set => pageFormat = EnumGuard.Checked(value);
  }
  [DV]
  internal PageFormat? pageFormat;

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

  /// <summary>
  /// Gets the PageSetup of the previous section, or null, if the page setup belongs
  /// to the first section.
  /// </summary>
  public PageSetup PreviousPageSetup()
  {
    var section = Parent as Section;
    if (section != null)
    {
      section = section.PreviousSection();
      if (section != null)
        return section.PageSetup;
    }
    return null;
  }

  /// <summary>
  /// Gets a PageSetup object with default values for all properties.
  /// </summary>
  internal static PageSetup DefaultPageSetup
  {
    get
    {
      AssertDefaultPageSetupUnmodified();
      return defaultPageSetup;
    }
  }

  /// <summary>
  /// The page setup every section starts out from. Built by the type initializer rather than on
  /// first use: filling one in afterwards would let a second thread find the field already set
  /// and carry off a page setup that is still only half written.
  /// </summary>
  private static readonly PageSetup defaultPageSetup = CreateDefaultPageSetup();

  /// <summary>
  /// What <see cref="defaultPageSetup"/> was built as, kept to check nobody has since written to
  /// it. Built on first use rather than as a field initializer: Clone reads Meta, which the source
  /// generator emits as a static field of this same type, and a field initializer that ran before
  /// that field's own initializer would read it as null. Whichever thread first asks for it, the
  /// class has by then finished initializing - a field initializer running inside the same type
  /// initializer has no such guarantee about a field declared later.
  /// </summary>
  private static PageSetup defaultPageSetupClone;

  private static PageSetup CreateDefaultPageSetup()
  {
    var pageSetup = new PageSetup();
    pageSetup.PageFormat = PageFormat.A4;
    pageSetup.SectionStart = BreakType.BreakNextPage;
    pageSetup.Orientation = Orientation.Portrait;
    pageSetup.PageWidth = "21cm";
    pageSetup.PageHeight = "29.7cm";
    pageSetup.TopMargin = "2.5cm";
    pageSetup.BottomMargin = "2cm";
    pageSetup.LeftMargin = "2.5cm";
    pageSetup.RightMargin = "2.5cm";
    pageSetup.HeaderDistance = "1.25cm";
    pageSetup.FooterDistance = "1.25cm";
    pageSetup.OddAndEvenPagesHeaderFooter = false;
    pageSetup.DifferentFirstPageHeaderFooter = false;
    pageSetup.MirrorMargins = false;
    pageSetup.HorizontalPageBreak = false;
    return pageSetup;
  }

  /// <summary>
  /// Checks that nobody has written to the shared default, which Document.DefaultPageSetup hands
  /// straight out to whoever asks for it.
  /// </summary>
  private static void AssertDefaultPageSetupUnmodified()
  {
    defaultPageSetupClone ??= defaultPageSetup.Clone();
    Debug.Assert(defaultPageSetup.PageFormat == defaultPageSetupClone.PageFormat, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.SectionStart == defaultPageSetupClone.SectionStart, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.Orientation == defaultPageSetupClone.Orientation, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.PageWidth == defaultPageSetupClone.PageWidth, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.PageHeight == defaultPageSetupClone.PageHeight, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.TopMargin == defaultPageSetupClone.TopMargin, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.BottomMargin == defaultPageSetupClone.BottomMargin, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.LeftMargin == defaultPageSetupClone.LeftMargin, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.RightMargin == defaultPageSetupClone.RightMargin, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.HeaderDistance == defaultPageSetupClone.HeaderDistance, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.FooterDistance == defaultPageSetupClone.FooterDistance, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.OddAndEvenPagesHeaderFooter == defaultPageSetupClone.OddAndEvenPagesHeaderFooter, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.DifferentFirstPageHeaderFooter == defaultPageSetupClone.DifferentFirstPageHeaderFooter, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.MirrorMargins == defaultPageSetupClone.MirrorMargins, "DefaultPageSetup must not be modified");
    Debug.Assert(defaultPageSetup.HorizontalPageBreak == defaultPageSetupClone.HorizontalPageBreak, "DefaultPageSetup must not be modified");
  }

  #region Internal
  /// <summary>
  /// Converts PageSetup into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteComment((comment ?? ""));
    var pos = serializer.BeginContent("PageSetup");

    if (!pageHeight.IsNull)
      serializer.WriteSimpleAttribute("PageHeight", PageHeight);

    if (!pageWidth.IsNull)
      serializer.WriteSimpleAttribute("PageWidth", PageWidth);

    if (orientation != null)
      serializer.WriteSimpleAttribute("Orientation", Orientation);

    if (!leftMargin.IsNull)
      serializer.WriteSimpleAttribute("LeftMargin", LeftMargin);

    if (!rightMargin.IsNull)
      serializer.WriteSimpleAttribute("RightMargin", RightMargin);

    if (!topMargin.IsNull)
      serializer.WriteSimpleAttribute("TopMargin", TopMargin);

    if (!bottomMargin.IsNull)
      serializer.WriteSimpleAttribute("BottomMargin", BottomMargin);

    if (!footerDistance.IsNull)
      serializer.WriteSimpleAttribute("FooterDistance", FooterDistance);

    if (!headerDistance.IsNull)
      serializer.WriteSimpleAttribute("HeaderDistance", HeaderDistance);

    if (oddAndEvenPagesHeaderFooter != null)
      serializer.WriteSimpleAttribute("OddAndEvenPagesHeaderFooter", OddAndEvenPagesHeaderFooter);

    if (differentFirstPageHeaderFooter != null)
      serializer.WriteSimpleAttribute("DifferentFirstPageHeaderFooter", DifferentFirstPageHeaderFooter);

    if (sectionStart != null)
      serializer.WriteSimpleAttribute("SectionStart", SectionStart);

    if (pageFormat != null)
      serializer.WriteSimpleAttribute("PageFormat", PageFormat);

    if (mirrorMargins != null)
      serializer.WriteSimpleAttribute("MirrorMargins", MirrorMargins);

    if (horizontalPageBreak != null)
      serializer.WriteSimpleAttribute("HorizontalPageBreak", HorizontalPageBreak);

    if (startingNumber != null)
      serializer.WriteSimpleAttribute("StartingNumber", StartingNumber);

    serializer.EndContent(pos);
  }

  #endregion
}
