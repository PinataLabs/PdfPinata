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

using System;
using System.Diagnostics;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// An Unit consist of a numerical value and an UnitType like Centimeter, Millimeter or Inch.
/// Several conversion between different measures are supported.
/// </summary>
public struct Unit : IFormattable, INullableValue
{
    /// <summary>
    /// Initializes a new instance of the Unit class with type set to point.
    /// </summary>
    public Unit(double point)
    {
        value = (float)point;
        type = UnitType.Point;
        initialized = true;
    }

    /// <summary>
    /// Initializes a new instance of the Unit class.
    /// Throws System.ArgumentException if <code>type</code> is invalid.
    /// </summary>
    public Unit(double value, UnitType type)
    {
        if (!Enum.IsDefined(typeof(UnitType), type))
            //throw new InvalidEnumArgumentException("type", (int)type, type.GetType());
            throw new ArgumentException("type");
        this.value = (float)value;
        this.type = type;
        initialized = true;
    }

    /// <summary>
    /// Determines whether this instance is empty.
    /// </summary>
    public bool IsEmpty => IsNull;

    /// <summary>
    /// Gets the value of the unit.
    /// </summary>
    object INullableValue.GetValue()
    {
        return this;
    }

    /// <summary>
    /// Sets the unit to the given value.
    /// </summary>
    void INullableValue.SetValue(object value)
    {
        if (value == null)
            throw new ArgumentNullException("value");

        if (value is Unit)
            this = (Unit)value;
        else
            this = value.ToString();
    }

    /// <summary>
    /// Resets this instance,
    /// i.e. IsNull() will return true afterwards.
    /// </summary>
    void INullableValue.SetNull()
    {
        value = 0;
        type = UnitType.Point;
        initialized = false;
    }

    // Explicit interface implementations cannot contain access specifiers, i.e. they are accessible by a
    // cast operator only, e.g. ((IDomValue)obj).IsNull.
    // Therefore the second IsNull-Property is used as a handy shortcut.
    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    bool INullableValue.IsNull => IsNull;

    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    internal bool IsNull => !initialized;

    #region Properties
    /// <summary>
    /// Gets or sets the raw value of the object without any conversion.
    /// To determine the UnitType use property <code>Type</code>.
    /// </summary>
    public double Value
    {
        get => (IsNull ? 0 : value);
        set
        {
            this.value = (float)value;
            initialized = true;
        }
    }

    /// <summary>
    /// Gets the UnitType of the object.
    /// </summary>
    public UnitType Type => type;

    /// <summary>
    /// Gets or sets the value in point.
    /// </summary>
    public double Point
    {
        get
        {
            if (IsNull)
                return 0;

            switch (type)
            {
                case UnitType.Centimeter:
                    return value * 72 / 2.54;

                case UnitType.Inch:
                    return value * 72;

                case UnitType.Millimeter:
                    return value * 72 / 25.4;

                case UnitType.Pica:
                    return value * 12;

                case UnitType.Point:
                    return value;

                default:
                    Debug.Assert(false, "Missing unit type.");
                    return 0;
            }
        }
        set
        {
            this.value = (float)value;
            type = UnitType.Point;
            initialized = true;
        }
    }

    /// <summary>
    /// Gets or sets the value in centimeter.
    /// </summary>
    public double Centimeter
    {
        get
        {
            if (IsNull)
                return 0;

            switch (type)
            {
                case UnitType.Centimeter:
                    return value;

                case UnitType.Inch:
                    return value * 2.54;

                case UnitType.Millimeter:
                    return value / 10;

                case UnitType.Pica:
                    return value * 12 * 2.54 / 72;

                case UnitType.Point:
                    return value * 2.54 / 72;

                default:
                    Debug.Assert(false, "Missing unit type");
                    return 0;
            }
        }
        set
        {
            this.value = (float)value;
            type = UnitType.Centimeter;
            initialized = true;
        }
    }

    /// <summary>
    /// Gets or sets the value in inch.
    /// </summary>
    public double Inch
    {
        get
        {
            if (IsNull)
                return 0;

            switch (type)
            {
                case UnitType.Centimeter:
                    return value / 2.54;

                case UnitType.Inch:
                    return value;

                case UnitType.Millimeter:
                    return value / 25.4;

                case UnitType.Pica:
                    return value * 12 / 72;

                case UnitType.Point:
                    return value / 72;

                default:
                    Debug.Assert(false, "Missing unit type");
                    return 0;
            }
        }
        set
        {
            this.value = (float)value;
            type = UnitType.Inch;
            initialized = true;
        }
    }

    /// <summary>
    /// Gets or sets the value in millimeter.
    /// </summary>
    public double Millimeter
    {
        get
        {
            if (IsNull)
                return 0;

            switch (type)
            {
                case UnitType.Centimeter:
                    return value * 10;

                case UnitType.Inch:
                    return value * 25.4;

                case UnitType.Millimeter:
                    return value;

                case UnitType.Pica:
                    return value * 12 * 25.4 / 72;

                case UnitType.Point:
                    return value * 25.4 / 72;

                default:
                    Debug.Assert(false, "Missing unit type");
                    return 0;
            }
        }
        set
        {
            this.value = (float)value;
            type = UnitType.Millimeter;
            initialized = true;
        }
    }

    /// <summary>
    /// Gets or sets the value in pica.
    /// </summary>
    public double Pica
    {
        get
        {
            if (IsNull)
                return 0;

            switch (type)
            {
                case UnitType.Centimeter:
                    return value * 72 / 2.54 / 12;

                case UnitType.Inch:
                    return value * 72 / 12;

                case UnitType.Millimeter:
                    return value * 72 / 25.4 / 12;

                case UnitType.Pica:
                    return value;

                case UnitType.Point:
                    return value / 12;

                default:
                    Debug.Assert(false, "Missing unit type");
                    return 0;
            }
        }
        set
        {
            this.value = (float)value;
            type = UnitType.Pica;
            initialized = true;
        }
    }
    #endregion

    #region Methods
    /// <summary>
    /// Returns the object as string using the format information.
    /// Measure will be added to the end of the string.
    /// </summary>
    public string ToString(IFormatProvider formatProvider)
    {
        if (IsNull)
            return 0.ToString(formatProvider); // TODO: ?? can it be anything other than "0"??

        string valuestring;
        valuestring = value.ToString(formatProvider) + GetSuffix();
        return valuestring;
    }

    /// <summary>
    /// Returns the object as string using the format.
    /// Measure will be added to the end of the string.
    /// </summary>
    public string ToString(string format)
    {
        if (IsNull)
            return 0.ToString(format); // TODO: ?? can it be anything other than "0"??

        string valuestring;
        valuestring = value.ToString(format) + GetSuffix();
        return valuestring;
    }

    /// <summary>
    /// Returns the object as string using the specified format and format information.
    /// Measure will be added to the end of the string.
    /// </summary>
    string IFormattable.ToString(string format, IFormatProvider formatProvider)
    {
        if (IsNull)
            return 0.ToString(format, formatProvider);

        string valuestring;
        valuestring = value.ToString(format, formatProvider) + GetSuffix();
        return valuestring;
    }

    /// <summary>
    /// Returns the object as string. Measure will be added to the end of the string.
    /// </summary>
    public override string ToString()
    {
        if (IsNull)
            return 0.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string valuestring;
        valuestring = value.ToString(System.Globalization.CultureInfo.InvariantCulture) + GetSuffix();
        return valuestring;
    }

    /// <summary>
    /// Returns the type of the object as a string like 'pc', 'cm', or 'in'. Empty string is equal to 'pt'.
    /// </summary>
    string GetSuffix()
    {
        switch (type)
        {
            case UnitType.Centimeter:
                return "cm";

            case UnitType.Inch:
                return "in";

            case UnitType.Millimeter:
                return "mm";

            case UnitType.Pica:
                return "pc";

            case UnitType.Point:
                //Point is default, so leave this blank.
                return "";

            default:
                Debug.Assert(false, "Missing unit type");
                return "";
        }
    }

    /// <summary>
    /// Returns an Unit object. Sets type to centimeter.
    /// </summary>
    public static Unit FromCentimeter(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Centimeter;
        return unit;
    }

    /// <summary>
    /// Returns an Unit object. Sets type to millimeter.
    /// </summary>
    public static Unit FromMillimeter(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Millimeter;
        return unit;
    }

    /// <summary>
    /// Returns an Unit object. Sets type to point.
    /// </summary>
    public static Unit FromPoint(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Point;
        return unit;
    }

    /// <summary>
    /// Returns an Unit object. Sets type to inch.
    /// </summary>
    public static Unit FromInch(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Inch;
        return unit;
    }

    /// <summary>
    /// Returns an Unit object. Sets type to pica.
    /// </summary>
    public static Unit FromPica(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Pica;
        return unit;
    }
    #endregion

    /// <summary>
    /// Converts a string to an Unit object.
    /// If the string contains a suffix like 'cm' or 'in' the object will be converted
    /// to the appropriate type, otherwise point is assumed.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is null. Worth knowing why this is stated rather than left to fail:
    /// this conversion is what makes <c>someUnit == null</c> compile. null converts to string and
    /// string converts to Unit, so the comparison binds to operator ==(Unit, Unit) rather than
    /// lifting to Unit?, which means CS8073 cannot warn about it - the comparison is not constant.
    /// Without this guard the expression reached Trim() and threw NullReferenceException with
    /// nothing to say for itself. It is still the wrong thing to write; at least it now says so.
    /// </exception>
    public static implicit operator Unit(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value),
                "A null string cannot be converted to a Unit. If this came from writing "
                + "'unit == null', that comparison is always meaningless: Unit is a value type, and "
                + "the implicit string conversion is the only reason it compiles at all. Test "
                + "unit.IsEmpty instead.");

        Unit unit = Zero;
        value = value.Trim();

        // For Germans...
        value = value.Replace(',', '.');

        int count = value.Length;
        int valLen = 0;
        for (; valLen < count;)
        {
            char ch = value[valLen];
            if (ch == '.' || ch == '-' || ch == '+' || Char.IsNumber(ch))
                valLen++;
            else
                break;
        }

        unit.value = 1;
        try
        {
            unit.value = float.Parse(value.Substring(0, valLen).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(DomSR.InvalidUnitValue(value), ex);
        }

        string typeStr = value.Substring(valLen).Trim().ToLower();
        unit.type = UnitType.Point;
        switch (typeStr)
        {
            case "cm":
                unit.type = UnitType.Centimeter;
                break;

            case "in":
                unit.type = UnitType.Inch;
                break;

            case "mm":
                unit.type = UnitType.Millimeter;
                break;

            case "pc":
                unit.type = UnitType.Pica;
                break;

            case "":
            case "pt":
                unit.type = UnitType.Point;
                break;

            default:
                throw new ArgumentException(DomSR.InvalidUnitType(typeStr));
        }

        return unit;
    }

    /// <summary>
    /// Converts an int to an Unit object with type set to point.
    /// </summary>
    public static implicit operator Unit(int value)
    {
        Unit unit = Zero;
        unit.value = value;
        unit.type = UnitType.Point;
        return unit;
    }

    /// <summary>
    /// Converts a float to an Unit object with type set to point.
    /// </summary>
    public static implicit operator Unit(float value)
    {
        Unit unit = Zero;
        unit.value = value;
        unit.type = UnitType.Point;
        return unit;
    }

    /// <summary>
    /// Converts a double to an Unit object with type set to point.
    /// </summary>
    public static implicit operator Unit(double value)
    {
        Unit unit = Zero;
        unit.value = (float)value;
        unit.type = UnitType.Point;
        return unit;
    }

    /// <summary>
    /// Returns a double value as point.
    /// </summary>
    public static implicit operator double(Unit value)
    {
        return value.Point;
    }

    /// <summary>
    /// Returns a float value as point.
    /// </summary>
    public static implicit operator float(Unit value)
    {
        return (float)value.Point;
    }

    /// <summary>
    /// Memberwise comparison. To compare by value, 
    /// use code like Math.Abs(a.Point - b.Point) &lt; 1e-5.
    /// </summary>
    public static bool operator ==(Unit l, Unit r)
    {
        #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
        return (l.initialized == r.initialized && l.type == r.type && l.value == r.value);
        #pragma warning restore S1244
    }

    /// <summary>
    /// Memberwise comparison. To compare by value, 
    /// use code like Math.Abs(a.Point - b.Point) &lt; 1e-5.
    /// </summary>
    public static bool operator !=(Unit l, Unit r)
    {
        return !(l == r);
    }

    /// <summary>
    /// Calls base class Equals.
    /// </summary>
    public override bool Equals(Object obj)
    {
        return base.Equals(obj);
    }

    /// <summary>
    /// Calls base class GetHashCode.
    /// </summary>
    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    /// This member is intended to be used by XmlDomainObjectReader only.
    /// </summary>
    public static Unit Parse(string value)
    {
        Unit unit = Zero;
        unit = value;
        return unit;
    }

    /// <summary>
    /// Converts an existing object from one unit into another unit type.
    /// </summary>
    public void ConvertType(UnitType type)
    {
        if (this.type == type)
            return;

        if (!Enum.IsDefined(typeof(UnitType), type))
            throw new ArgumentException(DomSR.InvalidUnitType(type.ToString()));

        switch (type)
        {
            case UnitType.Centimeter:
                value = (float)Centimeter;
                this.type = UnitType.Centimeter;
                break;

            case UnitType.Inch:
                value = (float)Inch;
                this.type = UnitType.Inch;
                break;

            case UnitType.Millimeter:
                value = (float)Millimeter;
                this.type = UnitType.Millimeter;
                break;

            case UnitType.Pica:
                value = (float)Pica;
                this.type = UnitType.Pica;
                break;

            case UnitType.Point:
                value = (float)Point;
                this.type = UnitType.Point;
                break;

            default:
                //Remember missing unit type!!!
                Debug.Assert(false, "Missing unit type");
                break;
        }
    }

    /// <summary>
    /// Represents the uninitialized Unit object.
    /// </summary>
    public static readonly Unit Empty = new Unit();

    /// <summary>
    /// Represents an initialized Unit object with value 0 and unit type point.
    /// </summary>
    public static readonly Unit Zero = new Unit(0);

    /// <summary>
    /// Represents the uninitialized Unit object. Same as Unit.Empty.
    /// </summary>
    internal static readonly Unit NullValue = Empty;

    bool initialized;
    float value;
    UnitType type;
}
