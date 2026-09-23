#region Copyright

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

using System;
using System.Diagnostics;
using System.Globalization;
using PdfPinata.Internal;

namespace PdfPinata.Drawing;

/// <summary>
/// Represents a value and its unit of measure. The structure converts implicitly from and to
/// double with a value measured in point.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public struct XUnit : IFormattable, IEquatable<XUnit>
{
    internal const double InchFactor = 72;
    internal const double MillimeterFactor = 72 / 25.4;
    internal const double CentimeterFactor = 72 / 2.54;
    internal const double PresentationFactor = 72 / 96.0;

    /// <summary>
    /// Initializes a new instance of the XUnit class with type set to point.
    /// </summary>
    public XUnit(double point)
    {
        _value = point;
        _type = XGraphicsUnit.Point;
    }

    /// <summary>
    /// Initializes a new instance of the XUnit class.
    /// </summary>
    public XUnit(double value, XGraphicsUnit type)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentException(@"The unit type is not a member of XGraphicsUnit.", nameof(type));
        _value = value;
        _type = type;
    }

    /// <summary>
    /// Gets the raw value of the object without any conversion.
    /// To determine the XGraphicsUnit use property <code>Type</code>.
    /// To get the value in point use the implicit conversion to double.
    /// </summary>
    public double Value => _value;

    /// <summary>
    /// Gets the unit of measure.
    /// </summary>
    public XGraphicsUnit Type => _type;

    /// <summary>
    /// Gets or sets the value in point.
    /// </summary>
    public double Point
    {
        get
        {
            return _type switch
            {
                XGraphicsUnit.Point => _value,
                XGraphicsUnit.Inch => _value * 72,
                XGraphicsUnit.Millimeter => _value * 72 / 25.4,
                XGraphicsUnit.Centimeter => _value * 72 / 2.54,
                XGraphicsUnit.Presentation => _value * 72 / 96,
                _ => throw new InvalidCastException()
            };
        }
        set
        {
            _value = value;
            _type = XGraphicsUnit.Point;
        }
    }

    /// <summary>
    /// Gets or sets the value in inch.
    /// </summary>
    public double Inch
    {
        get
        {
            return _type switch
            {
                XGraphicsUnit.Point => _value / 72,
                XGraphicsUnit.Inch => _value,
                XGraphicsUnit.Millimeter => _value / 25.4,
                XGraphicsUnit.Centimeter => _value / 2.54,
                XGraphicsUnit.Presentation => _value / 96,
                _ => throw new InvalidCastException()
            };
        }
        set
        {
            _value = value;
            _type = XGraphicsUnit.Inch;
        }
    }

    /// <summary>
    /// Gets or sets the value in millimeter.
    /// </summary>
    public double Millimeter
    {
        get
        {
            return _type switch
            {
                XGraphicsUnit.Point => _value * 25.4 / 72,
                XGraphicsUnit.Inch => _value * 25.4,
                XGraphicsUnit.Millimeter => _value,
                XGraphicsUnit.Centimeter => _value * 10,
                XGraphicsUnit.Presentation => _value * 25.4 / 96,
                _ => throw new InvalidCastException()
            };
        }
        set
        {
            _value = value;
            _type = XGraphicsUnit.Millimeter;
        }
    }

    /// <summary>
    /// Gets or sets the value in centimeter.
    /// </summary>
    public double Centimeter
    {
        get
        {
            return _type switch
            {
                XGraphicsUnit.Point => _value * 2.54 / 72,
                XGraphicsUnit.Inch => _value * 2.54,
                XGraphicsUnit.Millimeter => _value / 10,
                XGraphicsUnit.Centimeter => _value,
                XGraphicsUnit.Presentation => _value * 2.54 / 96,
                _ => throw new InvalidCastException()
            };
        }
        set
        {
            _value = value;
            _type = XGraphicsUnit.Centimeter;
        }
    }

    /// <summary>
    /// Gets or sets the value in presentation units (1/96 inch).
    /// </summary>
    public double Presentation
    {
        get
        {
            return _type switch
            {
                XGraphicsUnit.Point => _value * 96 / 72,
                XGraphicsUnit.Inch => _value * 96,
                XGraphicsUnit.Millimeter => _value * 96 / 25.4,
                XGraphicsUnit.Centimeter => _value * 96 / 2.54,
                XGraphicsUnit.Presentation => _value,
                _ => throw new InvalidCastException()
            };
        }
        set
        {
            _value = value;
            // Presentation, not Point. Every other setter names its own measure; this one was a
            // copy of the Point setter, so a length assigned in presentation units was stored as
            // that many points - four thirds of the length the caller asked for, and read back as
            // such by every one of the five getters.
            _type = XGraphicsUnit.Presentation;
        }
    }

    /// <summary>
    /// Returns the object as string using the format information.
    /// The unit of measure is appended to the end of the string.
    /// </summary>
    public string ToString(IFormatProvider formatProvider)
    {
        return _value.ToString(formatProvider) + GetSuffix();
    }

    /// <summary>
    /// Returns the object as string using the specified format and format information.
    /// The unit of measure is appended to the end of the string.
    /// </summary>
    string IFormattable.ToString(string format, IFormatProvider formatProvider)
    {
        return _value.ToString(format, formatProvider) + GetSuffix();
    }

    /// <summary>
    /// Returns the object as string. The unit of measure is appended to the end of the string.
    /// </summary>
    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture) + GetSuffix();
    }

    /// <summary>
    /// Returns the unit of measure of the object as a string like 'pt', 'cm', or 'in'.
    /// </summary>
    private string GetSuffix()
    {
        return _type switch
        {
            XGraphicsUnit.Point => "pt",
            XGraphicsUnit.Inch => "in",
            XGraphicsUnit.Millimeter => "mm",
            XGraphicsUnit.Centimeter => "cm",
            XGraphicsUnit.Presentation => "pu",
            _ => throw new InvalidCastException()
        };
    }

    /// <summary>
    /// Returns an XUnit object. Sets type to point.
    /// </summary>
    public static XUnit FromPoint(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Point;
        return unit;
    }

    /// <summary>
    /// Returns an XUnit object. Sets type to inch.
    /// </summary>
    public static XUnit FromInch(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Inch;
        return unit;
    }

    /// <summary>
    /// Returns an XUnit object. Sets type to millimeters.
    /// </summary>
    public static XUnit FromMillimeter(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Millimeter;
        return unit;
    }

    /// <summary>
    /// Returns an XUnit object. Sets type to centimeters.
    /// </summary>
    public static XUnit FromCentimeter(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Centimeter;
        return unit;
    }

    /// <summary>
    /// Returns an XUnit object. Sets type to Presentation.
    /// </summary>
    public static XUnit FromPresentation(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Presentation;
        return unit;
    }

    /// <summary>
    /// Converts a string to an XUnit object.
    /// If the string contains a suffix like 'cm' or 'in' the object will be converted
    /// to the appropriate type, otherwise point is assumed.
    /// </summary>
    public static implicit operator XUnit(string value)
    {
        XUnit unit;
        value = value.Trim();

        value = value.Replace(',', '.');

        var count = value.Length;
        var valLen = 0;
        for (; valLen < count;)
        {
            var ch = value[valLen];
            if (ch == '.' || ch == '-' || ch == '+' || char.IsNumber(ch))
                valLen++;
            else
                break;
        }

        try
        {
            unit._value = double.Parse(value[..valLen].Trim(), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            unit._value = 1;
            var message = $"String '{value}' is not a valid value for structure 'XUnit'.";
#pragma warning disable S3877 // Public API: this conversion is the parser, and a string that names no unit has nothing to convert to.
            throw new ArgumentException(message, ex);
#pragma warning restore S3877
        }

        var typeStr = value[valLen..].Trim().ToLower();
        unit._type = XGraphicsUnit.Point;
        unit._type = typeStr switch
        {
            "cm" => XGraphicsUnit.Centimeter,
            "in" => XGraphicsUnit.Inch,
            "mm" => XGraphicsUnit.Millimeter,
            "" or "pt" => XGraphicsUnit.Point,
            "pu" => // presentation units
                XGraphicsUnit.Presentation,
            _ => throw new ArgumentException("Unknown unit type: '" + typeStr + "'")
        };

        return unit;
    }

    /// <summary>
    /// Converts an int to an XUnit object with type set to point.
    /// </summary>
    public static implicit operator XUnit(int value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Point;
        return unit;
    }

    /// <summary>
    /// Converts a double to an XUnit object with type set to point.
    /// </summary>
    public static implicit operator XUnit(double value)
    {
        XUnit unit;
        unit._value = value;
        unit._type = XGraphicsUnit.Point;
        return unit;
    }

    /// <summary>
    /// Returns a double value as point.
    /// </summary>
    public static implicit operator double(XUnit value)
    {
        return value.Point;
    }

    /// <summary>
    /// Memberwise comparison. To compare by value,
    /// use code like Math.Abs(a.Pt - b.Pt) &lt; 1e-5.
    /// </summary>
    public static bool operator ==(XUnit value1, XUnit value2)
    {
        // ReSharper disable CompareOfFloatsByEqualityOperator
#pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
        return value1._type == value2._type && value1._value == value2._value;
#pragma warning restore S1244
        // ReSharper restore CompareOfFloatsByEqualityOperator
    }

    /// <summary>
    /// Memberwise comparison. To compare by value,
    /// use code like Math.Abs(a.Pt - b.Pt) &lt; 1e-5.
    /// </summary>
    public static bool operator !=(XUnit value1, XUnit value2)
    {
        return !(value1 == value2);
    }

    /// <summary>
    /// Calls base class Equals.
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is XUnit unit)
            return this == unit;
        return false;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    public override int GetHashCode()
    {
        // ReSharper disable NonReadonlyFieldInGetHashCode
        return _value.GetHashCode() ^ _type.GetHashCode();
        // ReSharper restore NonReadonlyFieldInGetHashCode
    }

    /// <summary>
    /// This member is intended to be used by XmlDomainObjectReader only.
    /// </summary>
    public static XUnit Parse(string value)
    {
        XUnit unit = value;
        return unit;
    }

    /// <summary>
    /// Converts an existing object from one unit into another unit type.
    /// </summary>
    public void ConvertType(XGraphicsUnit type)
    {
        if (_type == type)
            return;

        switch (type)
        {
            case XGraphicsUnit.Point:
                _value = Point;
                _type = XGraphicsUnit.Point;
                break;

            case XGraphicsUnit.Inch:
                _value = Inch;
                _type = XGraphicsUnit.Inch;
                break;

            case XGraphicsUnit.Centimeter:
                _value = Centimeter;
                _type = XGraphicsUnit.Centimeter;
                break;

            case XGraphicsUnit.Millimeter:
                _value = Millimeter;
                _type = XGraphicsUnit.Millimeter;
                break;

            case XGraphicsUnit.Presentation:
                _value = Presentation;
                _type = XGraphicsUnit.Presentation;
                break;

            default:
                throw new ArgumentException("Unknown unit type: '" + type + "'");
        }
    }

    /// <summary>
    /// Represents a unit with all values zero.
    /// </summary>
    public static readonly XUnit Zero = new();

    private double _value;
    private XGraphicsUnit _type;

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    /// <value>The debugger display.</value>
    // ReSharper disable UnusedMember.Local
    private string DebuggerDisplay
        // ReSharper restore UnusedMember.Local
    {
        get
        {
            const string format = Config.SignificantFigures10;
            return string.Format(CultureInfo.InvariantCulture, "unit=({0:" + format + "} {1})", _value, GetSuffix());
        }
    }

    /// <summary>
    /// Indicates whether this instance and the one given are the same measure of the same unit.
    /// It decides that exactly as <c>operator ==</c> does, so that the two cannot give different
    /// answers. A memberwise comparison would: <see cref="double.Equals(double)" /> holds two NaNs
    /// to be equal, where <c>==</c> holds nothing equal to a NaN at all.
    /// </summary>
    /// <remarks>
    /// Worth knowing what implementing <see cref="IEquatable{T}" /> does to an argument that is not
    /// an <see cref="XUnit" />. An int, a double and a float all convert to one implicitly, and
    /// <see cref="XUnit" /> is a better conversion target than <see cref="object" />, so
    /// <c>unit.Equals(72)</c> binds here and is converted before it is compared - which is what
    /// <c>unit == 72</c> has always done. It used to bind to <see cref="Equals(object)" /> instead,
    /// box the argument, ask whether an object was an <see cref="XUnit" /> and answer false, so the
    /// operator and <c>Equals</c> disagreed about the same pair of values. An argument declared as
    /// <see cref="object" /> still answers false, because at that point there is no conversion left
    /// to make. A string would bind here too, and must not - see <see cref="Equals(string)" />.
    /// </remarks>
    public bool Equals(XUnit other) => this == other;

    /// <summary>
    /// Always false: a string is not an <see cref="XUnit" />, and this is the overload that says so.
    /// </summary>
    /// <remarks>
    /// It is here to keep <c>unit.Equals(someString)</c> from binding to
    /// <see cref="Equals(XUnit)" /> through the implicit conversion from <see cref="string" />,
    /// which would parse the string before comparing it - and throw <see cref="ArgumentException" />
    /// for one that names no unit, where an <c>Equals</c> is required to answer rather than to fail.
    /// Delete this and <c>unit.Equals("an inch")</c> stops answering false and starts throwing.
    /// Note that <c>unit == "an inch"</c> does throw, and always has; that is the operator's
    /// business and it is not made better by <c>Equals</c> joining in.
    /// </remarks>
    #pragma warning disable S3400 // Deliberate: the constant is the answer, and the remarks say why the overload exists at all.
    public bool Equals(string other) => false;
    #pragma warning restore S3400
}
