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
            throw new ArgumentException($@"'{type}' is not a defined value of {nameof(BorderType)}.", nameof(type));

        return !IsNull(type.ToString());
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
        var ht = new Hashtable
        {
            { "Top", top },
            { "Left", left },
            { "Bottom", bottom },
            { "Right", right },
            { "DiagonalUp", diagonalUp },
            { "DiagonalDown", diagonalDown }
        };

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
            top ??= new Border(this);

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
            left ??= new Border(this);

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
            bottom ??= new Border(this);

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
            right ??= new Border(this);

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
            diagonalUp ??= new Border(this);

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
            diagonalDown ??= new Border(this);

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
    /// <remarks>
    /// A default, not a value applied to every border: it is used for each border whose own
    /// <see cref="Border.Visible"/> is not set, and a border that sets one keeps it.
    /// </remarks>
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
    /// <remarks>
    /// A default, not a value applied to every border: it is used for each border whose own
    /// <see cref="Border.Style"/> is not set, and a border that sets one keeps it.
    /// </remarks>
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
    /// <remarks>
    /// A default, not a value applied to every border: it is used for each border whose own
    /// <see cref="Border.Width"/> is not set, and a border that sets one keeps it.
    /// </remarks>
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
    /// <remarks>
    /// A default, not a value applied to every border: it is used for each border whose own
    /// <see cref="Border.Color"/> is not set, and a border that sets one keeps it.
    /// </remarks>
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

        WriteIfDifferent(serializer, "Visible", visible, refBorders?.visible);
        // Compared with the style the reference answers, which is the default where it sets none.
        WriteIfDifferent(serializer, "Style", style, refBorders?.Style);

        if (!width.IsNull && WidthDiffersFrom(refBorders))
            serializer.WriteSimpleAttribute("Width", Width);

        if (!color.IsNull && (refBorders == null || Color.Argb != refBorders.Color.Argb))
            serializer.WriteSimpleAttribute("Color", Color);

        WriteDistanceIfDifferent(serializer, "DistanceFromTop", distanceFromTop, refBorders?.distanceFromTop);
        WriteDistanceIfDifferent(serializer, "DistanceFromBottom", distanceFromBottom, refBorders?.distanceFromBottom);
        WriteDistanceIfDifferent(serializer, "DistanceFromLeft", distanceFromLeft, refBorders?.distanceFromLeft);
        WriteDistanceIfDifferent(serializer, "DistanceFromRight", distanceFromRight, refBorders?.distanceFromRight);

        SerializeBorder(serializer, "Top", top);
        SerializeBorder(serializer, "Left", left);
        SerializeBorder(serializer, "Bottom", bottom);
        SerializeBorder(serializer, "Right", right);
        SerializeBorder(serializer, "DiagonalDown", diagonalDown);
        SerializeBorder(serializer, "DiagonalUp", diagonalUp);

        serializer.EndContent(pos);
    }

    /// <summary>
    /// Writes a value that is set, unless the borders it is compared with hold the same value.
    /// There being no such borders, or their leaving the value unset, is never a match.
    /// </summary>
    private static void WriteIfDifferent<T>(Serializer serializer, string valueName, T? value, T? refValue)
        where T : struct
    {
        if (value != null && !Nullable.Equals(value, refValue))
            serializer.WriteSimpleAttribute(valueName, value.Value);
    }

    /// <summary>
    /// Whether the width is not the one <paramref name="refBorders"/> has, compared by number alone
    /// rather than as a length.
    /// </summary>
    private bool WidthDiffersFrom(Borders refBorders)
    {
        #pragma warning disable S1244 // Exact on purpose: a value is written unless it is exactly the one it inherits.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return refBorders == null || width.Value != refBorders.width.Value;
        #pragma warning restore S1244
    }

    /// <summary>
    /// Writes a distance that is set, unless there is a reference distance and it is the same
    /// number of points.
    /// </summary>
    private static void WriteDistanceIfDifferent(Serializer serializer, string valueName, Unit distance, Unit? refDistance)
    {
        #pragma warning disable S1244 // Exact on purpose: a value is written unless it is exactly the one it inherits.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!distance.IsNull && (refDistance == null || distance.Point != refDistance.Value.Point))
            serializer.WriteSimpleAttribute(valueName, distance);
        #pragma warning restore S1244
    }

    /// <summary>
    /// Writes the border held under <paramref name="borderName"/> unless it is null.
    /// </summary>
    private void SerializeBorder(Serializer serializer, string borderName, Border border)
    {
        if (!IsNull(borderName))
            border.Serialize(serializer, borderName, null);
    }

    /// <summary>
    /// Gets a name of a border.
    /// </summary>
    internal string GetMyName(Border border)
    {
        if (border == top)
            return "Top";
        if (border == bottom)
            return "Bottom";
        if (border == left)
            return "Left";
        if (border == right)
            return "Right";
        if (border == diagonalUp)
            return "DiagonalUp";
        if (border == diagonalDown)
            return "DiagonalDown";
        return null;
    }

    /// <summary>
    /// Returns an enumerator that can iterate through the Borders.
    /// </summary>
    public class BorderEnumerator : IEnumerator
    {
        private int index;
        private readonly Hashtable ht;

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
            return index < ht.Count;
        }
    }

    #endregion
}
