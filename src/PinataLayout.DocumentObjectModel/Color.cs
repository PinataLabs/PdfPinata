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
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// The Color class represents an ARGB color value.
/// </summary>
[DebuggerDisplay("(A={A}, R={R}, G={G}, B={B} C={C}, M={M}, Y={Y}, K={K})")]
public struct Color : INullableValue, IEquatable<Color>
{
    /// <summary>
    /// Initializes a new instance of the Color class.
    /// </summary>
    public Color(uint argb)
    {
        isCmyk = false;
        this.argb = argb;
        a = c = m = y = k = 0f; // Compiler enforces this line of code
        InitCmykFromRgb();
    }

    /// <summary>
    /// Initializes a new instance of the Color class.
    /// </summary>
    public Color(byte r, byte g, byte b)
    {
        isCmyk = false;
        argb = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | b;
        a = c = m = y = k = 0f; // Compiler enforces this line of code
        InitCmykFromRgb();
    }

    /// <summary>
    /// Initializes a new instance of the Color class.
    /// </summary>
    public Color(byte a, byte r, byte g, byte b)
    {
        isCmyk = false;
        argb = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
        this.a = c = m = y = k = 0f; // Compiler enforces this line of code
        InitCmykFromRgb();
    }

    /// <summary>
    /// Initializes a new instance of the Color class with a CMYK color.
    /// All values must be in a range between 0 to 100 percent.
    /// </summary>
    public Color(double alpha, double cyan, double magenta, double yellow, double black)
    {
        isCmyk = true;
        a = (float)(alpha > 100 ? 100 : alpha < 0 ? 0 : alpha);
        c = (float)(cyan > 100 ? 100 : cyan < 0 ? 0 : cyan);
        m = (float)(magenta > 100 ? 100 : magenta < 0 ? 0 : magenta);
        y = (float)(yellow > 100 ? 100 : yellow < 0 ? 0 : yellow);
        k = (float)(black > 100 ? 100 : black < 0 ? 0 : black);
        argb = 0; // Compiler enforces this line of code
        InitRgbFromCmyk();
    }

    /// <summary>
    /// Initializes a new instance of the Color class with a CMYK color.
    /// All values must be in a range between 0 to 100 percent.
    /// </summary>
    public Color(double cyan, double magenta, double yellow, double black)
        : this(100, cyan, magenta, yellow, black)
    { }

    void InitCmykFromRgb()
    {
        // Similar formula as in PDFsharp
        isCmyk = false;
        var cyan = 255 - (int)R;
        var magenta = 255 - (int)G;
        var yellow = 255 - (int)B;
        var key = Math.Min(cyan, Math.Min(magenta, yellow));
        if (key == 255)
            this.c = this.m = this.y = 0;
        else
        {
            var black = 255f - key;
            this.c = 100f * (cyan - key) / black;
            this.m = 100f * (magenta - key) / black;
            this.y = 100f * (yellow - key) / black;
        }
        this.k = 100f * key / 255f;
        a = A / 2.55f;
    }

    void InitRgbFromCmyk()
    {
        // Similar formula as in PDFsharp
        isCmyk = true;
        var black = k * 2.55f + 0.5f;
        var factor = (255f - black) / 100f;
        var alpha = (byte)(this.a * 2.55 + 0.5);
        var r = (byte)(255 - Math.Min(255f, c * factor + black));
        var g = (byte)(255 - Math.Min(255f, m * factor + black));
        var b = (byte)(255 - Math.Min(255f, y * factor + black));
        argb = ((uint)alpha << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    /// <summary>
    /// Gets a value indicating whether this instance is a CMYK color.
    /// </summary>
    public bool IsCmyk => isCmyk;

    /// <summary>
    /// Determines whether this color is empty.
    /// </summary>
    public bool IsEmpty => this == Empty;

    /// <summary>
    /// Returns the value.
    /// </summary>
    object INullableValue.GetValue()
    {
        return this;
    }

    /// <summary>
    /// Sets the given value.
    /// </summary>
    void INullableValue.SetValue(object value)
    {
        if (value is uint u)
            argb = u;
        else
            this = Parse(value.ToString());
    }

    /// <summary>
    /// Resets this instance, i.e. IsNull() will return true afterward.
    /// </summary>
    void INullableValue.SetNull()
    {
        this = Empty;
    }

    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    bool INullableValue.IsNull => this == Empty;

    /// <summary>
    /// Determines whether this instance is null (not set).
    /// </summary>
    internal bool IsNull => this == Empty;

    /// <summary>
    /// Gets or sets the ARGB value.
    /// </summary>
    public uint Argb
    {
        get => argb;
        set
        {
            if (isCmyk)
                throw new InvalidOperationException("Cannot change a CMYK color.");
            argb = value;
            InitCmykFromRgb();
        }
    }

    /// <summary>
    /// Gets or sets the RGB value.
    /// </summary>
    public uint RGB
    {
        get => argb;
        set
        {
            if (isCmyk)
                throw new InvalidOperationException("Cannot change a CMYK color.");
            argb = value;
            InitCmykFromRgb();
        }
    }

    /// <summary>
    /// Calls base class Equals.
    /// </summary>
    public override bool Equals(Object obj)
    {
        if (obj is Color)
        {
            var color = (Color)obj;
            if (isCmyk ^ color.isCmyk)
                return false;
            #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
            if (isCmyk)
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return a == color.a && c == color.c && m == color.m && y == color.y && k == color.k;
                #pragma warning restore S1244
            else
                return argb == color.argb;
        }
        return false;
    }

    /// <summary>
    /// Compares this color with another color.
    /// </summary>
    public bool Equals(Color other)
    {
        return this == other;
    }

    /// <summary>
    /// Gets the ARGB value that this Color instance represents.
    /// </summary>
    public override int GetHashCode()
    {
        return (int)argb ^ a.GetHashCode() ^ c.GetHashCode() ^ m.GetHashCode() ^ y.GetHashCode() ^ k.GetHashCode();
    }

    /// <summary>
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    /// Compares two color objects. True if both argb values are equal, false otherwise.
    /// </summary>
    public static bool operator ==(Color color1, Color color2)
    {
        if (color1.isCmyk ^ color2.isCmyk)
            return false;
        #pragma warning disable S1244 // Exact on purpose: equality has to be transitive and agree with GetHashCode.
        if (color1.isCmyk)
            return color1.a == color2.a && color1.c == color2.c && color1.m == color2.m && color1.y == color2.y && color1.k == color2.k;
            #pragma warning restore S1244
        else
            return color1.argb == color2.argb;
    }

    /// <summary>
    /// Compares two color objects. True if both argb values are not equal, false otherwise.
    /// </summary>
    public static bool operator !=(Color color1, Color color2)
    {
        return !(color1 == color2);
    }

    /// <summary>
    /// Parses the string and returns a color object.
    /// Throws ArgumentException if color is invalid.
    /// </summary>
    /// <param name="color">integer, hex or color name.</param>
    public static Color Parse(string color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (color == "")
            throw new ArgumentException("A colour must not be empty.", nameof(color));

        try
        {
            uint clr = 0;
            // Must use Enum.Parse because Enum.IsDefined is case sensitive
            try
            {
                clr = (uint)Enum.Parse<ColorName>(color, true);
                return new Color(clr);
            }
            catch
            {
                //ignore exception cause it's not a ColorName.
            }

            var numberStyle = NumberStyles.Integer;
            var number = color.ToLower();
            if (number.StartsWith("0x"))
            {
                numberStyle = NumberStyles.HexNumber;
                number = color[2..];
            }
            clr = uint.Parse(number, numberStyle);
            return new Color(clr);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException(DomSR.InvalidColorString(color), ex);
        }
    }

    /// <summary>
    /// Gets the alpha (transparency) part of the RGB Color.
    /// The values is in the range between 0 to 255.
    /// </summary>
    public uint A => (argb & 0xFF000000) >> 24;

    /// <summary>
    /// Gets the red part of the Color.
    /// The values is in the range between 0 to 255.
    /// </summary>
    public uint R => (argb & 0xFF0000) >> 16;

    /// <summary>
    /// Gets the green part of the Color.
    /// The values is in the range between 0 to 255.
    /// </summary>
    public uint G => (argb & 0x00FF00) >> 8;

    /// <summary>
    /// Gets the blue part of the Color.
    /// The values is in the range between 0 to 255.
    /// </summary>
    public uint B => argb & 0x0000FF;

    /// <summary>
    /// Gets the alpha (transparency) part of the CMYK Color.
    /// The values is in the range between 0 (transparent) to 100 (opaque) percent.
    /// </summary>
    public double Alpha => a;

    /// <summary>
    /// Gets the cyan part of the Color.
    /// The values is in the range between 0 to 100 percent.
    /// </summary>
    public double C => c;

    /// <summary>
    /// Gets the magenta part of the Color.
    /// The values is in the range between 0 to 100 percent.
    /// </summary>
    public double M => m;

    /// <summary>
    /// Gets the yellow part of the Color.
    /// The values is in the range between 0 to 100 percent.
    /// </summary>
    public double Y => y;

    /// <summary>
    /// Gets the key (black) part of the Color.
    /// The values is in the range between 0 to 100 percent.
    /// </summary>
    public double K => k;

    /// <summary>
    /// Gets a non transparent color brightened in terms of transparency if any is given(A &lt; 255),
    /// otherwise this instance itself.
    /// </summary>
    public Color GetMixedTransparencyColor()
    {
        var alpha = (int)A;
        if (alpha == 0xFF)
            return this;

        var red = (int)R;
        var green = (int)G;
        var blue = (int)B;

        var whiteFactor = 1 - alpha / 255.0;

        red = (int)(red + (255 - red) * whiteFactor);
        green = (int)(green + (255 - green) * whiteFactor);
        blue = (int)(blue + (255 - blue) * whiteFactor);
        return new Color((uint)(0xFF << 24 | (red << 16) | (green << 8) | blue));
    }

    /// <summary>
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    /// Writes the Color object in its hexadecimal value.
    /// </summary>
    public override string ToString()
    {
        if (isCmyk)
        {
            string s;
            #pragma warning disable S1244 // Exact on purpose: only the exact value takes the special case, and the general path is right for anything near it.
            if (Alpha == 100.0)
                s = String.Format(CultureInfo.InvariantCulture, "CMYK({0:0.##},{1:0.##},{2:0.##},{3:0.##})", C, M, Y, K);
            #pragma warning restore S1244
            else
                s = String.Format(CultureInfo.InvariantCulture, "CMYK({0:0.##},{1:0.##},{2:0.##},{3:0.##},{4:0.##})", Alpha, C, M, Y, K);
            return s;
        }
        else
        {
            if (StdColors.TryGetValue(argb, out var name))
                return name;
            else
            {
                if ((argb & 0xFF000000) == 0xFF000000)
                    return "RGB(" +
                           ((argb & 0xFF0000) >> 16).ToString(CultureInfo.InvariantCulture) + "," +
                           ((argb & 0x00FF00) >> 8).ToString(CultureInfo.InvariantCulture) + "," +
                           (argb & 0x0000FF).ToString(CultureInfo.InvariantCulture) + ")";
                else
                    return "0x" + argb.ToString("X");
            }
        }
    }

    /// <summary>
    /// The name of each standard colour, by its ARGB value.
    /// </summary>
    /// <remarks>
    /// Built by a static initializer rather than on first use. The CLR runs one of those once and
    /// finishes it before any thread reads the field, which is what the previous lazy version got
    /// wrong: it assigned the table to the static while the table was still empty and only then
    /// filled it, so a second thread could read one that was half built, or race the
    /// ContainsKey/Add pair and hit "Item has already been added". Calling ToString on a standard
    /// colour from several threads at once returned the name, "RGB(r,g,b)", an empty string or an
    /// ArgumentException, depending on the timing.
    /// </remarks>
    static readonly Dictionary<uint, string> StdColors = BuildStdColors();

    static Dictionary<uint, string> BuildStdColors()
    {
        var names = Enum.GetNames<ColorName>();
        var values = Enum.GetValues<ColorName>();
        var colors = new Dictionary<uint, string>(names.Length);
        for (var index = 0; index < names.Length; index++)
        {
            // Some colors are double named, and the first name of a pair wins:
            // Aqua == Cyan, Fuchsia == Magenta.
            var value = (uint)values[index];
            if (!colors.ContainsKey(value))
                colors.Add(value, names[index]);
        }
        return colors;
    }

    /// <summary>
    /// Creates an RGB color from an existing color with a new alpha (transparency) value.
    /// </summary>
    public static Color FromRgbColor(byte a, Color color)
    {
        return new Color(a, (byte)color.R, (byte)color.G, (byte)color.B);
    }

    /// <summary>
    /// Creates a Color structure from the specified CMYK values.
    /// All values must be in a range between 0 to 100 percent.
    /// </summary>
    public static Color FromCmyk(double cyan, double magenta, double yellow, double black)
    {
        return new Color(cyan, magenta, yellow, black);
    }

    /// <summary>
    /// Creates a Color structure from the specified CMYK values.
    /// All values must be in a range between 0 to 100 percent.
    /// </summary>
    public static Color FromCmyk(double alpha, double cyan, double magenta, double yellow, double black)
    {
        return new Color(alpha, cyan, magenta, yellow, black);
    }

    /// <summary>
    /// Creates a CMYK color from an existing color with a new alpha (transparency) value.
    /// </summary>
    public static Color FromCmykColor(double alpha, Color color)
    {
        return new Color(alpha, color.C, color.M, color.Y, color.K);
    }

    uint argb; // ARGB
    bool isCmyk;
    private float a; // \
    private float c; // |
    private float m; // |--- alpha + CMYK
    private float y; // |
    private float k; // /

    /// <summary>
    /// Represents a null color.
    /// </summary>
    public static readonly Color Empty = new Color(0);
}
