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

using System;
using System.Collections;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// A Borders collection represents the eight border objects used for paragraphs, tables etc.
/// </summary>
public partial class Borders : DocumentObject, IEnumerable
{
    /// <summary>
    /// Initializes a new instance of the Borders class.
    /// </summary>
    public Borders()
    {
    }

    /// <summary>
    /// Initializes a new instance of the Borders class with the specified parent.
    /// </summary>
    internal Borders(DocumentObject parent) : base(parent) { }

    /// <summary>
    /// Determines whether a particular border exists.
    /// </summary>
    public bool HasBorder(BorderType type)
    {
        if (!Enum.IsDefined(type))
            //throw new InvalidEnumArgumentException("type");
            throw new ArgumentException($"'{type}' is not a defined value of {nameof(BorderType)}.", nameof(type));

        return !(IsNull(type.ToString()));
    }

    #region Methods
    /// <summary>
    /// Creates a deep copy of this object.
    /// </summary>
    public new Borders Clone()
    {
        return (Borders)DeepCopy();
    }

    /// <summary>
    /// Gets an enumerator for the borders object.
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator()
    {
        var ht = new Hashtable();
        ht.Add("Top", top);
        ht.Add("Left", left);
        ht.Add("Bottom", bottom);
        ht.Add("Right", right);
        ht.Add("DiagonalUp", diagonalUp);
        ht.Add("DiagonalDown", diagonalDown);

        return new BorderEnumerator(ht);
    }

    /// <summary>
    /// Clears all Border objects from the collection. Additionally 'Borders = null'
    /// is written to the DDL stream when serialized.
    /// </summary>
    public void ClearAll()
    {
        clearAll = true;
    }
    #endregion

    #region Properties
    /// <summary>
    /// Gets or sets the top border.
    /// </summary>
    public Border Top
    {
        get
        {
            if (top == null)
                top = new Border(this);

            return top;
        }
        set
        {
            SetParent(value);
            top = value;
        }
    }
    [DV]
    internal Border top;

    /// <summary>
    /// Gets or sets the left border.
    /// </summary>
    public Border Left
    {
        get
        {
            if (left == null)
                left = new Border(this);

            return left;
        }
        set
        {
            SetParent(value);
            left = value;
        }
    }
    [DV]
    internal Border left;

    /// <summary>
    /// Gets or sets the bottom border.
    /// </summary>
    public Border Bottom
    {
        get
        {
            if (bottom == null)
                bottom = new Border(this);

            return bottom;
        }
        set
        {
            SetParent(value);
            bottom = value;
        }
    }
    [DV]
    internal Border bottom;

    /// <summary>
    /// Gets or sets the right border.
    /// </summary>
    public Border Right
    {
        get
        {
            if (right == null)
                right = new Border(this);

            return right;
        }
        set
        {
            SetParent(value);
            right = value;
        }
    }
    [DV]
    internal Border right;

    /// <summary>
    /// Gets or sets the diagonalup border.
    /// </summary>
    public Border DiagonalUp
    {
        get
        {
            if (diagonalUp == null)
                diagonalUp = new Border(this);

            return diagonalUp;
        }
        set
        {
            SetParent(value);
            diagonalUp = value;
        }
    }
    [DV]
    internal Border diagonalUp;

    /// <summary>
    /// Gets or sets the diagonaldown border.
    /// </summary>
    public Border DiagonalDown
    {
        get
        {
            if (diagonalDown == null)
                diagonalDown = new Border(this);

            return diagonalDown;
        }
        set
        {
            SetParent(value);
            diagonalDown = value;
        }
    }
    [DV]
    internal Border diagonalDown;

    /// <summary>
    /// Gets or sets a value indicating whether the borders are visible.
    /// </summary>
    public bool Visible
    {
        get => visible ?? false;
        set => visible = value;
    }
    [DV]
    internal bool? visible;

    /// <summary>
    /// Gets or sets the line style of the borders.
    /// </summary>
    public BorderStyle Style
    {
        get => style ?? default;
        set => style = EnumGuard.Checked(value);
    }
    [DV]
    internal BorderStyle? style;

    /// <summary>
    /// Gets or sets the standard width of the borders.
    /// </summary>
    public Unit Width
    {
        get => width;
        set => width = value;
    }
    [DV]
    internal Unit width = Unit.NullValue;

    /// <summary>
    /// Gets or sets the color of the borders.
    /// </summary>
    public Color Color
    {
        get => color;
        set => color = value;
    }
    [DV]
    internal Color color = Color.Empty;

    /// <summary>
    /// Gets or sets the distance between text and the top border.
    /// </summary>
    public Unit DistanceFromTop
    {
        get => distanceFromTop;
        set => distanceFromTop = value;
    }
    [DV]
    internal Unit distanceFromTop = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between text and the bottom border.
    /// </summary>
    public Unit DistanceFromBottom
    {
        get => distanceFromBottom;
        set => distanceFromBottom = value;
    }
    [DV]
    internal Unit distanceFromBottom = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between text and the left border.
    /// </summary>
    public Unit DistanceFromLeft
    {
        get => distanceFromLeft;
        set => distanceFromLeft = value;
    }
    [DV]
    internal Unit distanceFromLeft = Unit.NullValue;

    /// <summary>
    /// Gets or sets the distance between text and the right border.
    /// </summary>
    public Unit DistanceFromRight
    {
        get => distanceFromRight;
        set => distanceFromRight = value;
    }
    [DV]
    internal Unit distanceFromRight = Unit.NullValue;

    /// <summary>
    /// Sets the distance to all four borders to the specified value.
    /// </summary>
    public Unit Distance
    {
        set
        {
            DistanceFromTop = value;
            DistanceFromBottom = value;
            DistanceFromLeft = value;
            distanceFromRight = value;
        }
    }

    /// <summary>
    /// Gets the information if the collection is marked as cleared. Additionally 'Borders = null'
    /// is written to the DDL stream when serialized.
    /// </summary>
    public bool BordersCleared
    {
        get => clearAll;
        set => clearAll = value;
    }
    /// <summary>Backing field for <see cref="BordersCleared"/>.</summary>
    protected bool clearAll;
    #endregion

    #region Null handling
    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    /// <remarks>
    /// Cleared borders are not null, for the same reason a cleared Border is not - see
    /// Border.IsNull. clearAll carries no [DV] attribute, so the value descriptors Meta.IsNull
    /// consults cannot see it.
    /// </remarks>
    public override bool IsNull()
    {
        return !clearAll && base.IsNull();
    }

    /// <summary>
    /// Resets this instance, i.e. IsNull() will return true afterwards.
    /// </summary>
    public override void SetNull()
    {
        base.SetNull();
        clearAll = false;
    }
    #endregion

    #region Internal
    /// <summary>
    /// Converts Borders into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        Serialize(serializer, null);
    }

    /// <summary>
    /// Converts Borders into DDL.
    /// </summary>
    internal void Serialize(Serializer serializer, Borders refBorders)
    {
        if (clearAll)
            serializer.WriteLine("Borders = null");

        var pos = serializer.BeginContent("Borders");

        if (visible != null && (refBorders == null || refBorders.visible == null || (Visible != refBorders.Visible)))
            serializer.WriteSimpleAttribute("Visible", Visible);

        if (style != null && (refBorders == null || (Style != refBorders.Style)))
            serializer.WriteSimpleAttribute("Style", Style);

        #pragma warning disable S1244 // Exact on purpose: a value is written unless it is exactly the one it inherits.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!width.IsNull && (refBorders == null || (width.Value != refBorders.width.Value)))
            serializer.WriteSimpleAttribute("Width", Width);
        #pragma warning restore S1244

        if (!color.IsNull && (refBorders == null || ((Color.Argb != refBorders.Color.Argb))))
            serializer.WriteSimpleAttribute("Color", Color);

        #pragma warning disable S1244 // Exact on purpose: a value is written unless it is exactly the one it inherits.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!distanceFromTop.IsNull && (refBorders == null || (DistanceFromTop.Point != refBorders.DistanceFromTop.Point)))
            serializer.WriteSimpleAttribute("DistanceFromTop", DistanceFromTop);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!distanceFromBottom.IsNull && (refBorders == null || (DistanceFromBottom.Point != refBorders.DistanceFromBottom.Point)))
            serializer.WriteSimpleAttribute("DistanceFromBottom", DistanceFromBottom);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!distanceFromLeft.IsNull && (refBorders == null || (DistanceFromLeft.Point != refBorders.DistanceFromLeft.Point)))
            serializer.WriteSimpleAttribute("DistanceFromLeft", DistanceFromLeft);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!distanceFromRight.IsNull && (refBorders == null || (DistanceFromRight.Point != refBorders.DistanceFromRight.Point)))
            serializer.WriteSimpleAttribute("DistanceFromRight", DistanceFromRight);
        #pragma warning restore S1244

        if (!IsNull("Top"))
            top.Serialize(serializer, "Top", null);

        if (!IsNull("Left"))
            left.Serialize(serializer, "Left", null);

        if (!IsNull("Bottom"))
            bottom.Serialize(serializer, "Bottom", null);

        if (!IsNull("Right"))
            right.Serialize(serializer, "Right", null);

        if (!IsNull("DiagonalDown"))
            diagonalDown.Serialize(serializer, "DiagonalDown", null);

        if (!IsNull("DiagonalUp"))
            diagonalUp.Serialize(serializer, "DiagonalUp", null);

        serializer.EndContent(pos);
    }

    /// <summary>
    /// Gets a name of a border.
    /// </summary>
    internal string GetMyName(Border border)
    {
        if (border == top)
            return "Top";
        else if (border == bottom)
            return "Bottom";
        else if (border == left)
            return "Left";
        else if (border == right)
            return "Right";
        else if (border == diagonalUp)
            return "DiagonalUp";
        else if (border == diagonalDown)
            return "DiagonalDown";
        return null;
    }

    /// <summary>
    /// Returns an enumerator that can iterate through the Borders.
    /// </summary>
    public class BorderEnumerator : IEnumerator
    {
        int index;
        Hashtable ht;

        /// <summary>
        /// Creates a new BorderEnumerator.
        /// </summary>
        public BorderEnumerator(Hashtable ht)
        {
            this.ht = ht;
            index = -1;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the border collection.
        /// </summary>
        public void Reset()
        {
            index = -1;
        }

        /// <summary>
        /// Gets the current element in the border collection.
        /// </summary>
        public Border Current
        {
            get
            {
                // ReSharper disable once GenericEnumeratorNotDisposed
                IEnumerator enumerator = ht.GetEnumerator();
                enumerator.Reset();
                for (var idx = 0; idx < index + 1; idx++)
                    enumerator.MoveNext();
                // ReSharper disable once PossibleNullReferenceException
                return ((DictionaryEntry)enumerator.Current).Value as Border;
            }
        }

        /// <summary>
        /// Gets the current element in the border collection.
        /// </summary>
        object IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next element of the border collection.
        /// </summary>
        public bool MoveNext()
        {
            index++;
            return (index < ht.Count);
        }
    }

    #endregion
}
