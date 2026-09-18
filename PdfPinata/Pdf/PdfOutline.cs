#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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

// Review: Under construction - StL/14-10-05

using System;
using System.Diagnostics;
using System.Globalization;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Actions;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf;

/// <summary>
/// Represents an outline item in the outlines tree. An 'outline' is also known as a 'bookmark'.
/// </summary>
public sealed class PdfOutline : PdfDictionary  // Reference: 8.2.2 Document Outline / Page 584
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    public PdfOutline()
    {
        // Create _outlines on demand.
        //_outlines = new PdfOutlineCollection(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    /// <param name="document">The document.</param>
    internal PdfOutline(PdfDocument document)
        : base(document)
    {
        // Create _outlines on demand.
        //_outlines = new PdfOutlineCollection(this);
    }

    /// <summary>
    /// Initializes a new instance from an existing dictionary. Used for object type transformation.
    /// </summary>
    public PdfOutline(PdfDictionary dict)
        : base(dict)
    {
        Initialize();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    /// <param name="style">The font style used to draw the outline text.</param>
    /// <param name="textColor">The color used to draw the outline text.</param>
    public PdfOutline(string title, PdfPage destinationPage, bool opened, PdfOutlineStyle style, XColor textColor)
    {
        Title = title;
        DestinationPage = destinationPage;
        Opened = opened;
        Style = style;
        TextColor = textColor;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    /// <param name="style">The font style used to draw the outline text.</param>
    public PdfOutline(string title, PdfPage destinationPage, bool opened, PdfOutlineStyle style)
    {
        Title = title;
        DestinationPage = destinationPage;
        Opened = opened;
        Style = style;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    /// <param name="opened">Specifies whether the node is displayed expanded (opened) or collapsed.</param>
    public PdfOutline(string title, PdfPage destinationPage, bool opened)
    {
        Title = title;
        DestinationPage = destinationPage;
        Opened = opened;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfOutline"/> class.
    /// </summary>
    /// <param name="title">The outline text.</param>
    /// <param name="destinationPage">The destination page.</param>
    public PdfOutline(string title, PdfPage destinationPage)
    {
        Title = title;
        DestinationPage = destinationPage;
    }

    /// <summary>
    /// How many rows this entry would add to a reader's panel if it were expanded: one for each
    /// child, plus whatever the children that are themselves open contribute below them. A closed
    /// child contributes itself and hides its own descendants, which is the whole difference
    /// between this and a count of the subtree.
    /// </summary>
    /// <remarks>
    /// Filled in by <see cref="MeasureVisibleDescendants"/> at the start of a save, and meaningless
    /// outside one. It is not maintained as the tree is built: the bookkeeping this replaced tried
    /// that, from <see cref="PdfOutlineCollection.Add(PdfOutline)"/>, and so recorded the state an
    /// entry was constructed with, missed every later assignment to <see cref="Opened"/>, and was
    /// never undone by a removal.
    /// </remarks>
    int _visibleDescendants;

    /// <summary>
    /// Measures this entry and everything under it, child first, storing each node's count as it
    /// comes back up. One pass over the tree measures all of it.
    /// </summary>
    /// <remarks>
    /// Post-order for a reason. Deriving the count on demand instead - reading a property that
    /// walked the subtree - meant every node re-walked everything below it as the writer came to
    /// it, which is O(n^2) on a deep tree that is open all the way down: a chain of n entries
    /// measured suffixes of length n-1, n-2 ... 1. A document with a chapter per page and a heading
    /// per section is exactly that shape.
    /// </remarks>
    int MeasureVisibleDescendants()
    {
        int count = 0;
        if (_outlines != null)
        {
            foreach (PdfOutline child in _outlines)
            {
                // Every child is measured, open or not, because it has to carry its own count
                // when it is written. Only an open one contributes what is under it to this one.
                int below = child.MeasureVisibleDescendants();
                count += 1 + (child.Opened ? below : 0);
            }
        }

        _visibleDescendants = count;
        return count;
    }

    /// <summary>
    /// Gets the parent of this outline item. The root item has no parent and returns null.
    /// </summary>
    public PdfOutline Parent
    {
        get => _parent;
        internal set => _parent = value;
    }
    PdfOutline _parent;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title
    {
        get => Elements.GetString(Keys.Title);
        set
        {
            PdfString s = new PdfString(value, PdfStringEncoding.Unicode);
            Elements.SetValue(Keys.Title, s);
        }
    }

    /// <summary>
    /// Gets or sets the destination page.
    /// </summary>
    public PdfPage DestinationPage
    {
        get => _destinationPage;
        set
        {
            _destinationPage = value;
            // Being given a destination page makes the entry one this library describes, whatever
            // the document it came from said.
            _keepDestinationAsFound = false;
        }
    }
    PdfPage _destinationPage;

    /// <summary>
    /// Gets or sets the left postion of the page positioned at the left side of the window.
    /// Applies only if PageDestinationType is Xyz, FitV, FitR, or FitBV.
    /// </summary>
    public double Left
    {
        get => _left;
        set => _left = value;
    }
    double _left = double.NaN;

    /// <summary>
    /// Gets or sets the top postion of the page positioned at the top side of the window.
    /// Applies only if PageDestinationType is Xyz, FitH, FitR, ob FitBH.
    /// </summary>
    public double Top
    {
        get => _top;
        set => _top = value;
    }
    double _top = double.NaN;

    /// <summary>
    /// Gets or sets the right postion of the page positioned at the right side of the window.
    /// Applies only if PageDestinationType is FitR.
    /// </summary>
    public double Right
    {
        get => _right;
        set => _right = value;
    }
    double _right = double.NaN;

    /// <summary>
    /// Gets or sets the bottom postion of the page positioned at the bottom side of the window.
    /// Applies only if PageDestinationType is FitR.
    /// </summary>
    public double Bottom
    {
        get => _bottom;
        set => _bottom = value;
    }
    double _bottom = double.NaN;

    /// <summary>
    /// Gets or sets the zoom faction of the page.
    /// Applies only if PageDestinationType is Xyz.
    /// </summary>
    public double Zoom
    {
        get => _zoom;
        set => _zoom = value;
    }
    double _zoom = double.NaN; // PDF teats 0 and null equally.

    /// <summary>
    /// Gets or sets whether the outline item is opened (or expanded).
    /// </summary>
    public bool Opened
    {
        get => _opened;
        set => _opened = value;
    }
    bool _opened;

    /// <summary>
    /// Gets or sets the style of the outline text.
    /// </summary>
    public PdfOutlineStyle Style
    {
        get => (PdfOutlineStyle)Elements.GetInteger(Keys.F);
        set => Elements.SetInteger(Keys.F, (int)value);
    }

    /// <summary>
    /// Gets or sets the type of the page destination.
    /// </summary>
    public PdfPageDestinationType PageDestinationType
    {
        get => _pageDestinationType;
        set
        {
            _pageDestinationType = value;
            _keepDestinationAsFound = false;
        }
    }
    PdfPageDestinationType _pageDestinationType = PdfPageDestinationType.Xyz;

    /// <summary>
    /// Whether where this entry goes is something this library cannot describe - a destination of
    /// a type it does not know, or an action that leads on to another - and so must be written
    /// back out as it was found rather than from the properties above.
    /// </summary>
    bool _keepDestinationAsFound;

    /// <summary>
    /// Gets or sets the color of the text.
    /// </summary>
    /// <value>The color of the text.</value>
    public XColor TextColor
    {
        get => _textColor;
        set => _textColor = value;
    }
    XColor _textColor;

    /// <summary>
    /// Gets a value indicating whether this outline object has child items.
    /// </summary>
    public bool HasChildren => _outlines != null && _outlines.Count > 0;

    /// <summary>
    /// Gets the outline collection of this node.
    /// </summary>
    public PdfOutlineCollection Outlines => _outlines ?? (_outlines = new PdfOutlineCollection(Owner, this));

    PdfOutlineCollection _outlines;

    /// <summary>
    /// Initializes this instance from an exisiting PDF document.
    /// </summary>
    void Initialize()
    {
        string title;
        if (Elements.TryGetString(Keys.Title, out title))
            Title = title;

        PdfReference parentRef = Elements.GetReference(Keys.Parent);
        if (parentRef != null)
        {
            PdfOutline parent = parentRef.Value as PdfOutline;
            if (parent != null)
                Parent = parent;
        }

        // /Count is how an entry records whether it is expanded: positive when it is, negative
        // when it is not, absent when it has no descendants to expand. Reading it back is what
        // lets a document be opened, edited and saved without every branch in it closing.
        _opened = Elements.GetInteger(Keys.Count) > 0;

        PdfArray colors = Elements.GetArray(Keys.C);
        if (colors != null && colors.Elements.Count == 3)
        {
            double r = colors.Elements.GetReal(0);
            double g = colors.Elements.GetReal(1);
            double b = colors.Elements.GetReal(2);
            TextColor = XColor.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        // Style directly works on dictionary element.

        // An outline entry says where it goes either outright, in /Dest, or by performing an
        // action. A document holding both is malformed, and /Dest is what this reads, because
        // it is the entry the specification says the other one replaces.
        PdfItem dest = Elements.GetValue(Keys.Dest);
        PdfItem a = Elements.GetValue(Keys.A);

        if (dest != null)
        {
            PdfArray destination = ResolveDestination(dest);
            if (destination != null)
                SplitDestinationPage(destination);
        }
        else if (a != null)
        {
            InitializeFromAction(a);
        }
        else
        {
            // Neither destination page nor GoTo action.
        }

        InitializeChildren();
    }

    /// <summary>
    /// Takes the destination of this outline from the action it performs, for the entries that
    /// go somewhere by performing one.
    /// </summary>
    void InitializeFromAction(PdfItem a)
    {
        // Only a GoTo action leads somewhere inside this document. Every other kind - a URI, a
        // file to launch, a page of another document - is left exactly as it stands: it has no
        // destination page to hand out, and an outline entry that opens a web page is a
        // perfectly ordinary thing for a document to hold.
        PdfDictionary action = a as PdfDictionary;
        if (action == null || action.Elements.GetName(PdfAction.Keys.S) != "/GoTo")
            return;

        PdfArray destination = ResolveDestination(action.Elements[PdfGoToAction.Keys.D]);
        if (destination == null)
            return;

        SplitDestinationPage(destination);

        // An action can lead on to another once it has done what it does, and going somewhere is
        // then only the first thing the entry asks for. Such an entry keeps the action it was
        // found with, since a /Dest says where it goes and nothing about what follows. Note the
        // order: reading the destination above sets the properties, and setting them says the
        // entry is one this library describes.
        if (action.Elements.ContainsKey(PdfAction.Keys.Next))
        {
            _keepDestinationAsFound = true;
            return;
        }

        // Replace Action with /Dest entry.
        Elements.Remove(Keys.A);
        Elements.Add(Keys.Dest, destination);
    }

    /// <summary>
    /// The destination an entry stands for, or null when it names nothing this document holds.
    /// </summary>
    /// <remarks>
    /// A destination is written either as an array or as the name of one the catalog holds, and
    /// the documents LaTeX writes name theirs. A name the document has no destination under is
    /// not an error worth refusing to read the outline over: the entry simply goes nowhere,
    /// which is what a reader shows.
    /// </remarks>
    PdfArray ResolveDestination(PdfItem dest)
    {
        if (dest is PdfReference iref)
            dest = iref.Value;

        return dest as PdfArray ?? PdfNamedDestinations.Lookup(Owner, dest);
    }

    void SplitDestinationPage(PdfArray destination)  // Reference: 8.2 Destination syntax / Page 582
    {
        // ReSharper disable HeuristicUnreachableCode
#pragma warning disable 162

        // The destination page may not yet transformed to PdfPage.
        PdfDictionary destPage = DestinationPageOf(destination);
        if (destPage == null)
            return;

        PdfPage page = destPage as PdfPage;
        if (page == null)
            page = new PdfPage(destPage);

        DestinationPage = page;
        PdfName type = destination.Elements.Count > 1 ? destination.Elements[1] as PdfName : null;
        // A destination whose type is one this library does not know leaves the page it goes to
        // and nothing more, which is still more than refusing to read the outline at all. What it
        // does say is kept, though: the entry is written back out as it was found rather than as
        // the /XYZ with no position the properties below would otherwise amount to.
        if (type != null && TryParseDestinationType(type.Value.Substring(1), out PdfPageDestinationType destinationType))
        {
            PageDestinationType = destinationType;
            switch (PageDestinationType)
            {
                // [page /XYZ left top zoom]
                case PdfPageDestinationType.Xyz:
                    Left = RealAt(destination, 2);
                    Top = RealAt(destination, 3);
                    Zoom = RealAt(destination, 4);
                    break;

                // [page /Fit]
                case PdfPageDestinationType.Fit:
                    // /Fit has no parameters.
                    break;

                // [page /FitH top]
                case PdfPageDestinationType.FitH:
                    Top = RealAt(destination, 2);
                    break;

                // [page /FitV left]
                case PdfPageDestinationType.FitV:
                    Left = RealAt(destination, 2);
                    break;

                // [page /FitR left bottom right top]
                case PdfPageDestinationType.FitR:
                    Left = RealAt(destination, 2);
                    Bottom = RealAt(destination, 3);
                    Right = RealAt(destination, 4);
                    Top = RealAt(destination, 5);
                    break;

                // [page /FitB]
                case PdfPageDestinationType.FitB:
                    // /Fit has no parameters.
                    break;

                // [page /FitBH top]
                case PdfPageDestinationType.FitBH:
                    Top = RealAt(destination, 2);
                    break;

                // [page /FitBV left]
                case PdfPageDestinationType.FitBV:
                    Left = RealAt(destination, 2);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(destination), destinationType,
                        "The destination type is not one this library knows.");
            }
        }
        else if (type != null)
        {
            _keepDestinationAsFound = true;
        }

#pragma warning restore 162
        // ReSharper restore HeuristicUnreachableCode
    }

    /// <summary>
    /// The destination type a name stands for, for the eight names there is one for.
    /// </summary>
    /// <remarks>
    /// Enum.TryParse reads a name that is a number as the value the number stands for, which is
    /// not what a destination type is. "/999" parsed to a type there is none of and went on into
    /// the ArgumentOutOfRangeException the switch above ends with, and "/3" parsed to a type whose
    /// name the destination never gave, so a document saying it went one way was written back
    /// saying it went another.
    /// </remarks>
    static bool TryParseDestinationType(string name, out PdfPageDestinationType type)
    {
        type = default;
        return name.Length > 0 && !char.IsDigit(name[0]) && name[0] != '-' && name[0] != '+'
               && Enum.TryParse(name, true, out type)
               && Enum.IsDefined(type);
    }

    /// <summary>
    /// The page a destination goes to, or null when the destination does not name one this
    /// document holds. A destination written into the document itself refers to its page, and
    /// one that came from elsewhere - a remote destination - gives the number of a page instead.
    /// </summary>
    static PdfDictionary DestinationPageOf(PdfArray destination)
    {
        if (destination.Elements.Count == 0)
            return null;

        PdfItem first = destination.Elements[0];
        if (first is PdfInteger number)
        {
            PdfDocument owner = destination.Owner;
            if (owner == null || number.Value < 0 || number.Value >= owner.PageCount)
                return null;

            return owner.Pages[number.Value];
        }

        if (first is PdfReference iref)
            return iref.Value as PdfDictionary;

        return null;
    }

    /// <summary>
    /// The number a destination holds at the position given, or NaN when it holds none there.
    /// A destination that stops short of the parameters its type takes leaves them unset, which
    /// is written back out as the null the specification gives for a parameter left to the
    /// reader.
    /// </summary>
    static double RealAt(PdfArray destination, int index)
    {
        return index < destination.Elements.Count ? destination.Elements.GetReal(index) : double.NaN;
    }

    void InitializeChildren()
    {
        PdfReference firstRef = Elements.GetReference(Keys.First);
        PdfReference lastRef = Elements.GetReference(Keys.Last);
        PdfReference current = firstRef;
        while (current != null)
        {
            // Create item and add it to outline items dictionary.
            PdfOutline item = new PdfOutline((PdfDictionary)current.Value);
            Outlines.Add(item);

            current = item.Elements.GetReference(Keys.Next);
        }
    }

    /// <summary>
    /// Creates key/values pairs according to the object structure.
    /// </summary>
    internal override void PrepareForSave()
    {
        bool hasKids = HasChildren;

        // The root is the only entry point - PdfCatalog.PrepareForSave calls it, and it walks
        // down from here - so this is where the tree gets measured, once, before anything below
        // reads a count.
        if (_parent == null)
            MeasureVisibleDescendants();

        // Is something to do at all?
        if (_parent != null || hasKids)
        {
            if (_parent == null)
            {
                // Case: This is the outline dictionary (the root).
                // Reference: TABLE 8.3  Entries in the outline dictionary / Page 585
                Debug.Assert(_outlines != null && _outlines.Count > 0 && _outlines[0] != null);
                Elements[Keys.First] = _outlines[0].Reference;
                Elements[Keys.Last] = _outlines[_outlines.Count - 1].Reference;

                // Table 152: the outline dictionary's /Count is the number of rows a reader shows
                // with nothing expanded by hand - every top-level entry, plus what the open ones
                // bring with them. Always non-negative; the root is not something that closes.
                Elements[Keys.Count] = new PdfInteger(_visibleDescendants);
            }
            else
            {
                // Case: This is an outline item dictionary.
                // Reference: TABLE 8.4  Entries in the outline item dictionary / Page 585
                Elements[Keys.Parent] = _parent.Reference;

                int count = _parent._outlines.Count;
                int index = _parent._outlines.IndexOf(this);
                Debug.Assert(index != -1);

                // Has destination? Where an entry goes that this library cannot describe keeps
                // what the document was read with, which still says it; describing it from the
                // properties above would turn it into something else.
                if (DestinationPage != null && !_keepDestinationAsFound)
                {
                    //Elements[Keys.Dest] = new PdfArray(Owner, DestinationPage.Reference, new PdfLiteral("/XYZ null null 0"));
                    Elements[Keys.Dest] = CreateDestArray();
                    // An entry given a destination goes there rather than wherever its action led,
                    // and the specification has one of the two entries, not both.
                    Elements.Remove(Keys.A);
                }

                // Not the first element?
                if (index > 0)
                    Elements[Keys.Prev] = _parent._outlines[index - 1].Reference;

                // Not the last element?
                if (index < count - 1)
                    Elements[Keys.Next] = _parent._outlines[index + 1].Reference;

                if (hasKids)
                {
                    Elements[Keys.First] = _outlines[0].Reference;
                    Elements[Keys.Last] = _outlines[_outlines.Count - 1].Reference;
                }

                // Table 153: an entry with descendants carries how many would become visible if it
                // were expanded, signed by whether it already is. An entry with none carries no
                // /Count at all - and must not keep one it was read in with, since its children
                // may since have been removed.
                //
                // This is what Opened is written as, and until it was written a reader had nothing
                // to expand a branch from: every tree arrived collapsed however it was built, and
                // the flag read back exactly as it had been set.
                if (hasKids)
                    Elements[Keys.Count] = new PdfInteger(_opened ? _visibleDescendants : -_visibleDescendants);
                else
                    Elements.Remove(Keys.Count);

                if (_textColor != XColor.Empty && Owner.HasVersion("1.4"))
                    Elements[Keys.C] = new PdfLiteral("[{0}]", PdfEncoders.ToString(_textColor, PdfColorMode.Rgb));

                // if (Style != PdfOutlineStyle.Regular && Document.HasVersion("1.4"))
                //  //pdf.AppendFormat("/F {0}\n", (int)_style);
                //  Elements[Keys.F] = new PdfInteger((int)_style);
            }

            // Prepare child elements.
            if (hasKids)
            {
                foreach (PdfOutline outline in _outlines)
                    outline.PrepareForSave();
            }
        }
    }

    PdfArray CreateDestArray()
    {
        PdfArray dest = null;
        switch (PageDestinationType)
        {
            // [page /XYZ left top zoom]
            case PdfPageDestinationType.Xyz:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/XYZ {0} {1} {2}", Fd(Left), Fd(Top), Fd(Zoom))));
                break;

            // [page /Fit]
            case PdfPageDestinationType.Fit:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral("/Fit"));
                break;

            // [page /FitH top]
            case PdfPageDestinationType.FitH:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/FitH {0}", Fd(Top))));
                break;

            // [page /FitV left]
            case PdfPageDestinationType.FitV:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/FitV {0}", Fd(Left))));
                break;

            // [page /FitR left bottom right top]
            case PdfPageDestinationType.FitR:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/FitR {0} {1} {2} {3}", Fd(Left), Fd(Bottom), Fd(Right), Fd(Top))));
                break;

            // [page /FitB]
            case PdfPageDestinationType.FitB:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral("/FitB"));
                break;

            // [page /FitBH top]
            case PdfPageDestinationType.FitBH:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/FitBH {0}", Fd(Top))));
                break;

            // [page /FitBV left]
            case PdfPageDestinationType.FitBV:
                dest = new PdfArray(Owner,
                    DestinationPage.Reference, new PdfLiteral(String.Format("/FitBV {0}", Fd(Left))));
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
        return dest;
    }

    /// <summary>
    /// Format double.
    /// </summary>
    static string Fd(double value)
    {
        return Double.IsNaN(value) ? "null" : value.ToString("#.##", CultureInfo.InvariantCulture);
    }

    internal override void WriteObject(PdfWriter writer)
    {
        // TODO: Proof that there is nothing to do here.
        bool hasKids = HasChildren;
        if (_parent != null || hasKids)
        {
            ////// Everything done in PrepareForSave
            ////if (_parent == null)
            ////{
            ////    // This is the outline dictionary (the root)
            ////}
            ////else
            ////{
            ////    // This is an outline item dictionary
            ////}
            base.WriteObject(writer);
        }
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed class Keys : KeysBase
    {
        // ReSharper disable InconsistentNaming

        /// <summary>
        /// (Optional) The type of PDF object that this dictionary describes; if present,
        /// must be Outlines for an outline dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Optional, FixedValue = "Outlines")]
        public const string Type = "/Type";

        // Outline and outline item are combined
        ///// <summary>
        ///// (Required if there are any open or closed outline entries; must be an indirect reference)
        ///// An outline item dictionary representing the first top-level item in the outline.
        ///// </summary>
        //[KeyInfo(KeyType.Dictionary)]
        //public const string First = "/First";
        //
        ///// <summary>
        ///// (Required if there are any open or closed outline entries; must be an indirect reference)
        ///// An outline item dictionary representing the last top-level item in the outline.
        ///// </summary>
        //[KeyInfo(KeyType.Dictionary)]
        //public const string Last = "/Last";
        //
        ///// <summary>
        ///// (Required if the document has any open outline entries) The total number of open items at all
        ///// levels of the outline. This entry should be omitted if there are no open outline items.
        ///// </summary>
        //[KeyInfo(KeyType.Integer)]
        //public const string Count = "/Count";

        /// <summary>
        /// (Required) The text to be displayed on the screen for this item.
        /// </summary>
        [KeyInfo(KeyType.String | KeyType.Required)]
        public const string Title = "/Title";

        /// <summary>
        /// (Required; must be an indirect reference) The parent of this item in the outline hierarchy.
        /// The parent of a top-level item is the outline dictionary itself.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Parent = "/Parent";

        /// <summary>
        /// (Required for all but the first item at each level; must be an indirect reference)
        /// The previous item at this outline level.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Prev = "/Prev";

        /// <summary>
        /// (Required for all but the last item at each level; must be an indirect reference)
        /// The next item at this outline level.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Next = "/Next";

        /// <summary>
        /// (Required if the item has any descendants; must be an indirect reference)
        ///  The first of this item’s immediate children in the outline hierarchy.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string First = "/First";

        /// <summary>
        /// (Required if the item has any descendants; must be an indirect reference)
        /// The last of this item’s immediate children in the outline hierarchy.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string Last = "/Last";

        /// <summary>
        /// (Required if the item has any descendants) If the item is open, the total number of its 
        /// open descendants at all lower levels of the outline hierarchy. If the item is closed, a 
        /// negative integer whose absolute value specifies how many descendants would appear if the 
        /// item were reopened.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Required)]
        public const string Count = "/Count";

        /// <summary>
        /// (Optional; not permitted if an A entry is present) The destination to be displayed when this 
        /// item is activated.
        /// </summary>
        [KeyInfo(KeyType.ArrayOrNameOrString | KeyType.Optional)]
        public const string Dest = "/Dest";

        /// <summary>
        /// (Optional; not permitted if a Dest entry is present) The action to be performed when
        /// this item is activated.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string A = "/A";

        /// <summary>
        /// (Optional; PDF 1.3; must be an indirect reference) The structure element to which the item 
        /// refers.
        /// Note: The ability to associate an outline item with a structure element (such as the beginning 
        /// of a chapter) is a PDF 1.3 feature. For backward compatibility with earlier PDF versions, such
        /// an item should also specify a destination (Dest) corresponding to an area of a page where the
        /// contents of the designated structure element are displayed.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string SE = "/SE";

        /// <summary>
        /// (Optional; PDF 1.4) An array of three numbers in the range 0.0 to 1.0, representing the 
        /// components in the DeviceRGB color space of the color to be used for the outline entry’s text.
        /// Default value: [0.0 0.0 0.0].
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string C = "/C";

        /// <summary>
        /// (Optional; PDF 1.4) A set of flags specifying style characteristics for displaying the outline
        /// item’s text. Default value: 0.
        /// </summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string F = "/F";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;

        // ReSharper restore InconsistentNaming
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
