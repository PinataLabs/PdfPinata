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

using System.Collections.Generic;
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
    if (PageSizes.ByFormat.TryGetValue(pageFormat, out var sheet))
    {
      pageWidth = sheet.Width;
      pageHeight = sheet.Height;
      return;
    }

    // A value that names no format has no size. PageSetup.PageFormat refuses one, so this is
    // reachable only by calling here with an integer cast to the enumeration, and it answers what
    // it has always answered: zero by zero.
    pageWidth = 0;
    pageHeight = 0;
  }

  /// <summary>
  /// The size of every named format. A class of its own so that the table is built by its own type
  /// initializer, on first use, whatever order PageSetup's own static fields are initialized in.
  /// </summary>
  private static class PageSizes
  {
    internal static readonly Dictionary<PageFormat, Sheet> ByFormat = new()
    {
      // ISO 216 A series.
      [PageFormat.A0] = Sheet.Millimeter(841, 1189),
      [PageFormat.A1] = Sheet.Millimeter(594, 841),
      [PageFormat.A2] = Sheet.Millimeter(420, 594),
      [PageFormat.A3] = Sheet.Millimeter(297, 420),
      [PageFormat.A4] = Sheet.Millimeter(210, 297),
      [PageFormat.A5] = Sheet.Millimeter(148, 210),
      [PageFormat.A6] = Sheet.Millimeter(105, 148),
      [PageFormat.A7] = Sheet.Millimeter(74, 105),
      [PageFormat.A8] = Sheet.Millimeter(52, 74),
      [PageFormat.A9] = Sheet.Millimeter(37, 52),
      [PageFormat.A10] = Sheet.Millimeter(26, 37),

      // DIN 476 oversizes.
      [PageFormat.TwoA0] = Sheet.Millimeter(1189, 1682),
      [PageFormat.FourA0] = Sheet.Millimeter(1682, 2378),

      // ISO 216 B series.
      [PageFormat.B0] = Sheet.Millimeter(1000, 1414),
      [PageFormat.B1] = Sheet.Millimeter(707, 1000),
      [PageFormat.B2] = Sheet.Millimeter(500, 707),
      [PageFormat.B3] = Sheet.Millimeter(353, 500),
      [PageFormat.B4] = Sheet.Millimeter(250, 353),
      [PageFormat.B5] = Sheet.Millimeter(176, 250),
      [PageFormat.B6] = Sheet.Millimeter(125, 176),
      [PageFormat.B7] = Sheet.Millimeter(88, 125),
      [PageFormat.B8] = Sheet.Millimeter(62, 88),
      [PageFormat.B9] = Sheet.Millimeter(44, 62),
      [PageFormat.B10] = Sheet.Millimeter(31, 44),
      [PageFormat.JISB5] = Sheet.Millimeter(182, 257),

      // ISO 269 C series, the envelopes.
      [PageFormat.C0] = Sheet.Millimeter(917, 1297),
      [PageFormat.C1] = Sheet.Millimeter(648, 917),
      [PageFormat.C2] = Sheet.Millimeter(458, 648),
      [PageFormat.C3] = Sheet.Millimeter(324, 458),
      [PageFormat.C4] = Sheet.Millimeter(229, 324),
      [PageFormat.C5] = Sheet.Millimeter(162, 229),
      [PageFormat.C6] = Sheet.Millimeter(114, 162),
      [PageFormat.C7] = Sheet.Millimeter(81, 114),
      [PageFormat.C8] = Sheet.Millimeter(57, 81),
      [PageFormat.C9] = Sheet.Millimeter(40, 57),
      [PageFormat.C10] = Sheet.Millimeter(28, 40),

      // ISO 217 untrimmed stock.
      [PageFormat.RA0] = Sheet.Millimeter(860, 1220),
      [PageFormat.RA1] = Sheet.Millimeter(610, 860),
      [PageFormat.RA2] = Sheet.Millimeter(430, 610),
      [PageFormat.RA3] = Sheet.Millimeter(305, 430),
      [PageFormat.RA4] = Sheet.Millimeter(215, 305),
      [PageFormat.RA5] = Sheet.Millimeter(153, 215),
      [PageFormat.SRA0] = Sheet.Millimeter(900, 1280),
      [PageFormat.SRA1] = Sheet.Millimeter(640, 900),
      [PageFormat.SRA2] = Sheet.Millimeter(450, 640),
      [PageFormat.SRA3] = Sheet.Millimeter(320, 450),
      [PageFormat.SRA4] = Sheet.Millimeter(225, 320),

      // North American sizes.
      [PageFormat.Letter] = Sheet.Inch(8.5, 11),
      [PageFormat.Legal] = Sheet.Inch(8.5, 14),
      [PageFormat.Ledger] = Sheet.Inch(17, 11),
      [PageFormat.Tabloid] = Sheet.Inch(11, 17),
      [PageFormat.P11x17] = Sheet.Inch(11, 17),
      [PageFormat.Executive] = Sheet.Inch(7.25, 10.5),
      [PageFormat.GovernmentLetter] = Sheet.Inch(8, 10.5),
      [PageFormat.Statement] = Sheet.Inch(5.5, 8.5),
      [PageFormat.STMT] = Sheet.Inch(5.5, 8.5),
      [PageFormat.Folio] = Sheet.Inch(8.5, 13),
      [PageFormat.Size10x14] = Sheet.Inch(10, 14),

      // Traditional British sizes.
      [PageFormat.Quarto] = Sheet.Inch(8, 10),
      [PageFormat.Foolscap] = Sheet.Inch(8, 13),
      [PageFormat.Post] = Sheet.Inch(15.5, 19.25),
      [PageFormat.Crown] = Sheet.Inch(20, 15),
      [PageFormat.LargePost] = Sheet.Inch(16.5, 21),
      [PageFormat.Demy] = Sheet.Inch(17.5, 22),
      [PageFormat.Medium] = Sheet.Inch(18, 23),
      [PageFormat.Royal] = Sheet.Inch(20, 25),
      [PageFormat.Elephant] = Sheet.Inch(23, 28),
      [PageFormat.DoubleDemy] = Sheet.Inch(23.5, 35),
      [PageFormat.QuadDemy] = Sheet.Inch(35, 45),
    };
  }

  /// <summary>
  /// A sheet's two sides in the unit that defines it.
  /// </summary>
  private readonly struct Sheet
  {
    private readonly double width;
    private readonly double height;
    private readonly bool inInches;

    private Sheet(double width, double height, bool inInches)
    {
      this.width = width;
      this.height = height;
      this.inInches = inInches;
    }

    public static Sheet Millimeter(double width, double height) => new(width, height, false);

    public static Sheet Inch(double width, double height) => new(width, height, true);

    public Unit Width => ToUnit(width);

    public Unit Height => ToUnit(height);

    /// <summary>
    /// Takes inches and returns points, at 72 to the inch. A Unit remembers the unit it was made
    /// from and writes it out as a suffix, so building these with Unit.FromInch would turn the 612
    /// that a serialized Letter page has always carried into 8.5in.
    /// </summary>
    private Unit ToUnit(double length) => inInches ? Unit.FromPoint(length * 72) : Unit.FromMillimeter(length);
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
    if (section == null)
      return null;

    section = section.PreviousSection();
    if (section != null)
      return section.PageSetup;
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
    var pageSetup = new PageSetup
    {
      PageFormat = PageFormat.A4,
      SectionStart = BreakType.BreakNextPage,
      Orientation = Orientation.Portrait,
      PageWidth = "21cm",
      PageHeight = "29.7cm",
      TopMargin = "2.5cm",
      BottomMargin = "2cm",
      LeftMargin = "2.5cm",
      RightMargin = "2.5cm",
      HeaderDistance = "1.25cm",
      FooterDistance = "1.25cm",
      OddAndEvenPagesHeaderFooter = false,
      DifferentFirstPageHeaderFooter = false,
      MirrorMargins = false,
      HorizontalPageBreak = false
    };
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
    serializer.WriteComment(comment ?? "");
    var pos = serializer.BeginContent("PageSetup");

    WriteIfSet(serializer, "PageHeight", pageHeight);
    WriteIfSet(serializer, "PageWidth", pageWidth);
    WriteIfSet(serializer, "Orientation", orientation);
    WriteIfSet(serializer, "LeftMargin", leftMargin);
    WriteIfSet(serializer, "RightMargin", rightMargin);
    WriteIfSet(serializer, "TopMargin", topMargin);
    WriteIfSet(serializer, "BottomMargin", bottomMargin);
    WriteIfSet(serializer, "FooterDistance", footerDistance);
    WriteIfSet(serializer, "HeaderDistance", headerDistance);
    WriteIfSet(serializer, "OddAndEvenPagesHeaderFooter", oddAndEvenPagesHeaderFooter);
    WriteIfSet(serializer, "DifferentFirstPageHeaderFooter", differentFirstPageHeaderFooter);
    WriteIfSet(serializer, "SectionStart", sectionStart);
    WriteIfSet(serializer, "PageFormat", pageFormat);
    WriteIfSet(serializer, "MirrorMargins", mirrorMargins);
    WriteIfSet(serializer, "HorizontalPageBreak", horizontalPageBreak);
    WriteIfSet(serializer, "StartingNumber", startingNumber);

    serializer.EndContent(pos);
  }

  /// <summary>
  /// Writes a length unless it was left unset.
  /// </summary>
  private static void WriteIfSet(Serializer serializer, string valueName, Unit value)
  {
    if (!value.IsNull)
      serializer.WriteSimpleAttribute(valueName, value);
  }

  /// <summary>
  /// Writes a value unless it was left unset.
  /// </summary>
  private static void WriteIfSet<T>(Serializer serializer, string valueName, T? value) where T : struct
  {
    if (value != null)
      serializer.WriteSimpleAttribute(valueName, value.Value);
  }

  #endregion
}
