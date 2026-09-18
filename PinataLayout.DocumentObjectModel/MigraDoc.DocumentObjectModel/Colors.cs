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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Represents 141 predefined colors.
/// </summary>
public class Colors
{
    Colors() { }

    /// <summary>The predefined colour <c>AliceBlue</c>, ARGB <c>#FFF0F8FF</c>.</summary>
    public static readonly Color AliceBlue = new Color(0xFFF0F8FF);
    /// <summary>The predefined colour <c>AntiqueWhite</c>, ARGB <c>#FFFAEBD7</c>.</summary>
    public static readonly Color AntiqueWhite = new Color(0xFFFAEBD7);
    /// <summary>The predefined colour <c>Aqua</c>, ARGB <c>#FF00FFFF</c>.</summary>
    public static readonly Color Aqua = new Color(0xFF00FFFF);
    /// <summary>The predefined colour <c>Aquamarine</c>, ARGB <c>#FF7FFFD4</c>.</summary>
    public static readonly Color Aquamarine = new Color(0xFF7FFFD4);
    /// <summary>The predefined colour <c>Azure</c>, ARGB <c>#FFF0FFFF</c>.</summary>
    public static readonly Color Azure = new Color(0xFFF0FFFF);
    /// <summary>The predefined colour <c>Beige</c>, ARGB <c>#FFF5F5DC</c>.</summary>
    public static readonly Color Beige = new Color(0xFFF5F5DC);
    /// <summary>The predefined colour <c>Bisque</c>, ARGB <c>#FFFFE4C4</c>.</summary>
    public static readonly Color Bisque = new Color(0xFFFFE4C4);
    /// <summary>The predefined colour <c>Black</c>, ARGB <c>#FF000000</c>.</summary>
    public static readonly Color Black = new Color(0xFF000000);
    /// <summary>The predefined colour <c>BlanchedAlmond</c>, ARGB <c>#FFFFEBCD</c>.</summary>
    public static readonly Color BlanchedAlmond = new Color(0xFFFFEBCD);
    /// <summary>The predefined colour <c>Blue</c>, ARGB <c>#FF0000FF</c>.</summary>
    public static readonly Color Blue = new Color(0xFF0000FF);
    /// <summary>The predefined colour <c>BlueViolet</c>, ARGB <c>#FF8A2BE2</c>.</summary>
    public static readonly Color BlueViolet = new Color(0xFF8A2BE2);
    /// <summary>The predefined colour <c>Brown</c>, ARGB <c>#FFA52A2A</c>.</summary>
    public static readonly Color Brown = new Color(0xFFA52A2A);
    /// <summary>The predefined colour <c>BurlyWood</c>, ARGB <c>#FFDEB887</c>.</summary>
    public static readonly Color BurlyWood = new Color(0xFFDEB887);
    /// <summary>The predefined colour <c>CadetBlue</c>, ARGB <c>#FF5F9EA0</c>.</summary>
    public static readonly Color CadetBlue = new Color(0xFF5F9EA0);
    /// <summary>The predefined colour <c>Chartreuse</c>, ARGB <c>#FF7FFF00</c>.</summary>
    public static readonly Color Chartreuse = new Color(0xFF7FFF00);
    /// <summary>The predefined colour <c>Chocolate</c>, ARGB <c>#FFD2691E</c>.</summary>
    public static readonly Color Chocolate = new Color(0xFFD2691E);
    /// <summary>The predefined colour <c>Coral</c>, ARGB <c>#FFFF7F50</c>.</summary>
    public static readonly Color Coral = new Color(0xFFFF7F50);
    /// <summary>The predefined colour <c>CornflowerBlue</c>, ARGB <c>#FF6495ED</c>.</summary>
    public static readonly Color CornflowerBlue = new Color(0xFF6495ED);
    /// <summary>The predefined colour <c>Cornsilk</c>, ARGB <c>#FFFFF8DC</c>.</summary>
    public static readonly Color Cornsilk = new Color(0xFFFFF8DC);
    /// <summary>The predefined colour <c>Crimson</c>, ARGB <c>#FFDC143C</c>.</summary>
    public static readonly Color Crimson = new Color(0xFFDC143C);
    /// <summary>The predefined colour <c>Cyan</c>, ARGB <c>#FF00FFFF</c>.</summary>
    public static readonly Color Cyan = new Color(0xFF00FFFF);
    /// <summary>The predefined colour <c>DarkBlue</c>, ARGB <c>#FF00008B</c>.</summary>
    public static readonly Color DarkBlue = new Color(0xFF00008B);
    /// <summary>The predefined colour <c>DarkCyan</c>, ARGB <c>#FF008B8B</c>.</summary>
    public static readonly Color DarkCyan = new Color(0xFF008B8B);
    /// <summary>The predefined colour <c>DarkGoldenrod</c>, ARGB <c>#FFB8860B</c>.</summary>
    public static readonly Color DarkGoldenrod = new Color(0xFFB8860B);
    /// <summary>The predefined colour <c>DarkGray</c>, ARGB <c>#FFA9A9A9</c>.</summary>
    public static readonly Color DarkGray = new Color(0xFFA9A9A9);
    /// <summary>The predefined colour <c>DarkGreen</c>, ARGB <c>#FF006400</c>.</summary>
    public static readonly Color DarkGreen = new Color(0xFF006400);
    /// <summary>The predefined colour <c>DarkKhaki</c>, ARGB <c>#FFBDB76B</c>.</summary>
    public static readonly Color DarkKhaki = new Color(0xFFBDB76B);
    /// <summary>The predefined colour <c>DarkMagenta</c>, ARGB <c>#FF8B008B</c>.</summary>
    public static readonly Color DarkMagenta = new Color(0xFF8B008B);
    /// <summary>The predefined colour <c>DarkOliveGreen</c>, ARGB <c>#FF556B2F</c>.</summary>
    public static readonly Color DarkOliveGreen = new Color(0xFF556B2F);
    /// <summary>The predefined colour <c>DarkOrange</c>, ARGB <c>#FFFF8C00</c>.</summary>
    public static readonly Color DarkOrange = new Color(0xFFFF8C00);
    /// <summary>The predefined colour <c>DarkOrchid</c>, ARGB <c>#FF9932CC</c>.</summary>
    public static readonly Color DarkOrchid = new Color(0xFF9932CC);
    /// <summary>The predefined colour <c>DarkRed</c>, ARGB <c>#FF8B0000</c>.</summary>
    public static readonly Color DarkRed = new Color(0xFF8B0000);
    /// <summary>The predefined colour <c>DarkSalmon</c>, ARGB <c>#FFE9967A</c>.</summary>
    public static readonly Color DarkSalmon = new Color(0xFFE9967A);
    /// <summary>The predefined colour <c>DarkSeaGreen</c>, ARGB <c>#FF8FBC8B</c>.</summary>
    public static readonly Color DarkSeaGreen = new Color(0xFF8FBC8B);
    /// <summary>The predefined colour <c>DarkSlateBlue</c>, ARGB <c>#FF483D8B</c>.</summary>
    public static readonly Color DarkSlateBlue = new Color(0xFF483D8B);
    /// <summary>The predefined colour <c>DarkSlateGray</c>, ARGB <c>#FF2F4F4F</c>.</summary>
    public static readonly Color DarkSlateGray = new Color(0xFF2F4F4F);
    /// <summary>The predefined colour <c>DarkTurquoise</c>, ARGB <c>#FF00CED1</c>.</summary>
    public static readonly Color DarkTurquoise = new Color(0xFF00CED1);
    /// <summary>The predefined colour <c>DarkViolet</c>, ARGB <c>#FF9400D3</c>.</summary>
    public static readonly Color DarkViolet = new Color(0xFF9400D3);
    /// <summary>The predefined colour <c>DeepPink</c>, ARGB <c>#FFFF1493</c>.</summary>
    public static readonly Color DeepPink = new Color(0xFFFF1493);
    /// <summary>The predefined colour <c>DeepSkyBlue</c>, ARGB <c>#FF00BFFF</c>.</summary>
    public static readonly Color DeepSkyBlue = new Color(0xFF00BFFF);
    /// <summary>The predefined colour <c>DimGray</c>, ARGB <c>#FF696969</c>.</summary>
    public static readonly Color DimGray = new Color(0xFF696969);
    /// <summary>The predefined colour <c>DodgerBlue</c>, ARGB <c>#FF1E90FF</c>.</summary>
    public static readonly Color DodgerBlue = new Color(0xFF1E90FF);
    /// <summary>The predefined colour <c>Firebrick</c>, ARGB <c>#FFB22222</c>.</summary>
    public static readonly Color Firebrick = new Color(0xFFB22222);
    /// <summary>The predefined colour <c>FloralWhite</c>, ARGB <c>#FFFFFAF0</c>.</summary>
    public static readonly Color FloralWhite = new Color(0xFFFFFAF0);
    /// <summary>The predefined colour <c>ForestGreen</c>, ARGB <c>#FF228B22</c>.</summary>
    public static readonly Color ForestGreen = new Color(0xFF228B22);
    /// <summary>The predefined colour <c>Fuchsia</c>, ARGB <c>#FFFF00FF</c>.</summary>
    public static readonly Color Fuchsia = new Color(0xFFFF00FF);
    /// <summary>The predefined colour <c>Gainsboro</c>, ARGB <c>#FFDCDCDC</c>.</summary>
    public static readonly Color Gainsboro = new Color(0xFFDCDCDC);
    /// <summary>The predefined colour <c>GhostWhite</c>, ARGB <c>#FFF8F8FF</c>.</summary>
    public static readonly Color GhostWhite = new Color(0xFFF8F8FF);
    /// <summary>The predefined colour <c>Gold</c>, ARGB <c>#FFFFD700</c>.</summary>
    public static readonly Color Gold = new Color(0xFFFFD700);
    /// <summary>The predefined colour <c>Goldenrod</c>, ARGB <c>#FFDAA520</c>.</summary>
    public static readonly Color Goldenrod = new Color(0xFFDAA520);
    /// <summary>The predefined colour <c>Gray</c>, ARGB <c>#FF808080</c>.</summary>
    public static readonly Color Gray = new Color(0xFF808080);
    /// <summary>The predefined colour <c>Green</c>, ARGB <c>#FF008000</c>.</summary>
    public static readonly Color Green = new Color(0xFF008000);
    /// <summary>The predefined colour <c>GreenYellow</c>, ARGB <c>#FFADFF2F</c>.</summary>
    public static readonly Color GreenYellow = new Color(0xFFADFF2F);
    /// <summary>The predefined colour <c>Honeydew</c>, ARGB <c>#FFF0FFF0</c>.</summary>
    public static readonly Color Honeydew = new Color(0xFFF0FFF0);
    /// <summary>The predefined colour <c>HotPink</c>, ARGB <c>#FFFF69B4</c>.</summary>
    public static readonly Color HotPink = new Color(0xFFFF69B4);
    /// <summary>The predefined colour <c>IndianRed</c>, ARGB <c>#FFCD5C5C</c>.</summary>
    public static readonly Color IndianRed = new Color(0xFFCD5C5C);
    /// <summary>The predefined colour <c>Indigo</c>, ARGB <c>#FF4B0082</c>.</summary>
    public static readonly Color Indigo = new Color(0xFF4B0082);
    /// <summary>The predefined colour <c>Ivory</c>, ARGB <c>#FFFFFFF0</c>.</summary>
    public static readonly Color Ivory = new Color(0xFFFFFFF0);
    /// <summary>The predefined colour <c>Khaki</c>, ARGB <c>#FFF0E68C</c>.</summary>
    public static readonly Color Khaki = new Color(0xFFF0E68C);
    /// <summary>The predefined colour <c>Lavender</c>, ARGB <c>#FFE6E6FA</c>.</summary>
    public static readonly Color Lavender = new Color(0xFFE6E6FA);
    /// <summary>The predefined colour <c>LavenderBlush</c>, ARGB <c>#FFFFF0F5</c>.</summary>
    public static readonly Color LavenderBlush = new Color(0xFFFFF0F5);
    /// <summary>The predefined colour <c>LawnGreen</c>, ARGB <c>#FF7CFC00</c>.</summary>
    public static readonly Color LawnGreen = new Color(0xFF7CFC00);
    /// <summary>The predefined colour <c>LemonChiffon</c>, ARGB <c>#FFFFFACD</c>.</summary>
    public static readonly Color LemonChiffon = new Color(0xFFFFFACD);
    /// <summary>The predefined colour <c>LightBlue</c>, ARGB <c>#FFADD8E6</c>.</summary>
    public static readonly Color LightBlue = new Color(0xFFADD8E6);
    /// <summary>The predefined colour <c>LightCoral</c>, ARGB <c>#FFF08080</c>.</summary>
    public static readonly Color LightCoral = new Color(0xFFF08080);
    /// <summary>The predefined colour <c>LightCyan</c>, ARGB <c>#FFE0FFFF</c>.</summary>
    public static readonly Color LightCyan = new Color(0xFFE0FFFF);
    /// <summary>The predefined colour <c>LightGoldenrodYellow</c>, ARGB <c>#FFFAFAD2</c>.</summary>
    public static readonly Color LightGoldenrodYellow = new Color(0xFFFAFAD2);
    /// <summary>The predefined colour <c>LightGray</c>, ARGB <c>#FFD3D3D3</c>.</summary>
    public static readonly Color LightGray = new Color(0xFFD3D3D3);
    /// <summary>The predefined colour <c>LightGreen</c>, ARGB <c>#FF90EE90</c>.</summary>
    public static readonly Color LightGreen = new Color(0xFF90EE90);
    /// <summary>The predefined colour <c>LightPink</c>, ARGB <c>#FFFFB6C1</c>.</summary>
    public static readonly Color LightPink = new Color(0xFFFFB6C1);
    /// <summary>The predefined colour <c>LightSalmon</c>, ARGB <c>#FFFFA07A</c>.</summary>
    public static readonly Color LightSalmon = new Color(0xFFFFA07A);
    /// <summary>The predefined colour <c>LightSeaGreen</c>, ARGB <c>#FF20B2AA</c>.</summary>
    public static readonly Color LightSeaGreen = new Color(0xFF20B2AA);
    /// <summary>The predefined colour <c>LightSkyBlue</c>, ARGB <c>#FF87CEFA</c>.</summary>
    public static readonly Color LightSkyBlue = new Color(0xFF87CEFA);
    /// <summary>The predefined colour <c>LightSlateGray</c>, ARGB <c>#FF778899</c>.</summary>
    public static readonly Color LightSlateGray = new Color(0xFF778899);
    /// <summary>The predefined colour <c>LightSteelBlue</c>, ARGB <c>#FFB0C4DE</c>.</summary>
    public static readonly Color LightSteelBlue = new Color(0xFFB0C4DE);
    /// <summary>The predefined colour <c>LightYellow</c>, ARGB <c>#FFFFFFE0</c>.</summary>
    public static readonly Color LightYellow = new Color(0xFFFFFFE0);
    /// <summary>The predefined colour <c>Lime</c>, ARGB <c>#FF00FF00</c>.</summary>
    public static readonly Color Lime = new Color(0xFF00FF00);
    /// <summary>The predefined colour <c>LimeGreen</c>, ARGB <c>#FF32CD32</c>.</summary>
    public static readonly Color LimeGreen = new Color(0xFF32CD32);
    /// <summary>The predefined colour <c>Linen</c>, ARGB <c>#FFFAF0E6</c>.</summary>
    public static readonly Color Linen = new Color(0xFFFAF0E6);
    /// <summary>The predefined colour <c>Magenta</c>, ARGB <c>#FFFF00FF</c>.</summary>
    public static readonly Color Magenta = new Color(0xFFFF00FF);
    /// <summary>The predefined colour <c>Maroon</c>, ARGB <c>#FF800000</c>.</summary>
    public static readonly Color Maroon = new Color(0xFF800000);
    /// <summary>The predefined colour <c>MediumAquamarine</c>, ARGB <c>#FF66CDAA</c>.</summary>
    public static readonly Color MediumAquamarine = new Color(0xFF66CDAA);
    /// <summary>The predefined colour <c>MediumBlue</c>, ARGB <c>#FF0000CD</c>.</summary>
    public static readonly Color MediumBlue = new Color(0xFF0000CD);
    /// <summary>The predefined colour <c>MediumOrchid</c>, ARGB <c>#FFBA55D3</c>.</summary>
    public static readonly Color MediumOrchid = new Color(0xFFBA55D3);
    /// <summary>The predefined colour <c>MediumPurple</c>, ARGB <c>#FF9370DB</c>.</summary>
    public static readonly Color MediumPurple = new Color(0xFF9370DB);
    /// <summary>The predefined colour <c>MediumSeaGreen</c>, ARGB <c>#FF3CB371</c>.</summary>
    public static readonly Color MediumSeaGreen = new Color(0xFF3CB371);
    /// <summary>The predefined colour <c>MediumSlateBlue</c>, ARGB <c>#FF7B68EE</c>.</summary>
    public static readonly Color MediumSlateBlue = new Color(0xFF7B68EE);
    /// <summary>The predefined colour <c>MediumSpringGreen</c>, ARGB <c>#FF00FA9A</c>.</summary>
    public static readonly Color MediumSpringGreen = new Color(0xFF00FA9A);
    /// <summary>The predefined colour <c>MediumTurquoise</c>, ARGB <c>#FF48D1CC</c>.</summary>
    public static readonly Color MediumTurquoise = new Color(0xFF48D1CC);
    /// <summary>The predefined colour <c>MediumVioletRed</c>, ARGB <c>#FFC71585</c>.</summary>
    public static readonly Color MediumVioletRed = new Color(0xFFC71585);
    /// <summary>The predefined colour <c>MidnightBlue</c>, ARGB <c>#FF191970</c>.</summary>
    public static readonly Color MidnightBlue = new Color(0xFF191970);
    /// <summary>The predefined colour <c>MintCream</c>, ARGB <c>#FFF5FFFA</c>.</summary>
    public static readonly Color MintCream = new Color(0xFFF5FFFA);
    /// <summary>The predefined colour <c>MistyRose</c>, ARGB <c>#FFFFE4E1</c>.</summary>
    public static readonly Color MistyRose = new Color(0xFFFFE4E1);
    /// <summary>The predefined colour <c>Moccasin</c>, ARGB <c>#FFFFE4B5</c>.</summary>
    public static readonly Color Moccasin = new Color(0xFFFFE4B5);
    /// <summary>The predefined colour <c>NavajoWhite</c>, ARGB <c>#FFFFDEAD</c>.</summary>
    public static readonly Color NavajoWhite = new Color(0xFFFFDEAD);
    /// <summary>The predefined colour <c>Navy</c>, ARGB <c>#FF000080</c>.</summary>
    public static readonly Color Navy = new Color(0xFF000080);
    /// <summary>The predefined colour <c>OldLace</c>, ARGB <c>#FFFDF5E6</c>.</summary>
    public static readonly Color OldLace = new Color(0xFFFDF5E6);
    /// <summary>The predefined colour <c>Olive</c>, ARGB <c>#FF808000</c>.</summary>
    public static readonly Color Olive = new Color(0xFF808000);
    /// <summary>The predefined colour <c>OliveDrab</c>, ARGB <c>#FF6B8E23</c>.</summary>
    public static readonly Color OliveDrab = new Color(0xFF6B8E23);
    /// <summary>The predefined colour <c>Orange</c>, ARGB <c>#FFFFA500</c>.</summary>
    public static readonly Color Orange = new Color(0xFFFFA500);
    /// <summary>The predefined colour <c>OrangeRed</c>, ARGB <c>#FFFF4500</c>.</summary>
    public static readonly Color OrangeRed = new Color(0xFFFF4500);
    /// <summary>The predefined colour <c>Orchid</c>, ARGB <c>#FFDA70D6</c>.</summary>
    public static readonly Color Orchid = new Color(0xFFDA70D6);
    /// <summary>The predefined colour <c>PaleGoldenrod</c>, ARGB <c>#FFEEE8AA</c>.</summary>
    public static readonly Color PaleGoldenrod = new Color(0xFFEEE8AA);
    /// <summary>The predefined colour <c>PaleGreen</c>, ARGB <c>#FF98FB98</c>.</summary>
    public static readonly Color PaleGreen = new Color(0xFF98FB98);
    /// <summary>The predefined colour <c>PaleTurquoise</c>, ARGB <c>#FFAFEEEE</c>.</summary>
    public static readonly Color PaleTurquoise = new Color(0xFFAFEEEE);
    /// <summary>The predefined colour <c>PaleVioletRed</c>, ARGB <c>#FFDB7093</c>.</summary>
    public static readonly Color PaleVioletRed = new Color(0xFFDB7093);
    /// <summary>The predefined colour <c>PapayaWhip</c>, ARGB <c>#FFFFEFD5</c>.</summary>
    public static readonly Color PapayaWhip = new Color(0xFFFFEFD5);
    /// <summary>The predefined colour <c>PeachPuff</c>, ARGB <c>#FFFFDAB9</c>.</summary>
    public static readonly Color PeachPuff = new Color(0xFFFFDAB9);
    /// <summary>The predefined colour <c>Peru</c>, ARGB <c>#FFCD853F</c>.</summary>
    public static readonly Color Peru = new Color(0xFFCD853F);
    /// <summary>The predefined colour <c>Pink</c>, ARGB <c>#FFFFC0CB</c>.</summary>
    public static readonly Color Pink = new Color(0xFFFFC0CB);
    /// <summary>The predefined colour <c>Plum</c>, ARGB <c>#FFDDA0DD</c>.</summary>
    public static readonly Color Plum = new Color(0xFFDDA0DD);
    /// <summary>The predefined colour <c>PowderBlue</c>, ARGB <c>#FFB0E0E6</c>.</summary>
    public static readonly Color PowderBlue = new Color(0xFFB0E0E6);
    /// <summary>The predefined colour <c>Purple</c>, ARGB <c>#FF800080</c>.</summary>
    public static readonly Color Purple = new Color(0xFF800080);
    /// <summary>The predefined colour <c>Red</c>, ARGB <c>#FFFF0000</c>.</summary>
    public static readonly Color Red = new Color(0xFFFF0000);
    /// <summary>The predefined colour <c>RosyBrown</c>, ARGB <c>#FFBC8F8F</c>.</summary>
    public static readonly Color RosyBrown = new Color(0xFFBC8F8F);
    /// <summary>The predefined colour <c>RoyalBlue</c>, ARGB <c>#FF4169E1</c>.</summary>
    public static readonly Color RoyalBlue = new Color(0xFF4169E1);
    /// <summary>The predefined colour <c>SaddleBrown</c>, ARGB <c>#FF8B4513</c>.</summary>
    public static readonly Color SaddleBrown = new Color(0xFF8B4513);
    /// <summary>The predefined colour <c>Salmon</c>, ARGB <c>#FFFA8072</c>.</summary>
    public static readonly Color Salmon = new Color(0xFFFA8072);
    /// <summary>The predefined colour <c>SandyBrown</c>, ARGB <c>#FFF4A460</c>.</summary>
    public static readonly Color SandyBrown = new Color(0xFFF4A460);
    /// <summary>The predefined colour <c>SeaGreen</c>, ARGB <c>#FF2E8B57</c>.</summary>
    public static readonly Color SeaGreen = new Color(0xFF2E8B57);
    /// <summary>The predefined colour <c>SeaShell</c>, ARGB <c>#FFFFF5EE</c>.</summary>
    public static readonly Color SeaShell = new Color(0xFFFFF5EE);
    /// <summary>The predefined colour <c>Sienna</c>, ARGB <c>#FFA0522D</c>.</summary>
    public static readonly Color Sienna = new Color(0xFFA0522D);
    /// <summary>The predefined colour <c>Silver</c>, ARGB <c>#FFC0C0C0</c>.</summary>
    public static readonly Color Silver = new Color(0xFFC0C0C0);
    /// <summary>The predefined colour <c>SkyBlue</c>, ARGB <c>#FF87CEEB</c>.</summary>
    public static readonly Color SkyBlue = new Color(0xFF87CEEB);
    /// <summary>The predefined colour <c>SlateBlue</c>, ARGB <c>#FF6A5ACD</c>.</summary>
    public static readonly Color SlateBlue = new Color(0xFF6A5ACD);
    /// <summary>The predefined colour <c>SlateGray</c>, ARGB <c>#FF708090</c>.</summary>
    public static readonly Color SlateGray = new Color(0xFF708090);
    /// <summary>The predefined colour <c>Snow</c>, ARGB <c>#FFFFFAFA</c>.</summary>
    public static readonly Color Snow = new Color(0xFFFFFAFA);
    /// <summary>The predefined colour <c>SpringGreen</c>, ARGB <c>#FF00FF7F</c>.</summary>
    public static readonly Color SpringGreen = new Color(0xFF00FF7F);
    /// <summary>The predefined colour <c>SteelBlue</c>, ARGB <c>#FF4682B4</c>.</summary>
    public static readonly Color SteelBlue = new Color(0xFF4682B4);
    /// <summary>The predefined colour <c>Tan</c>, ARGB <c>#FFD2B48C</c>.</summary>
    public static readonly Color Tan = new Color(0xFFD2B48C);
    /// <summary>The predefined colour <c>Teal</c>, ARGB <c>#FF008080</c>.</summary>
    public static readonly Color Teal = new Color(0xFF008080);
    /// <summary>The predefined colour <c>Thistle</c>, ARGB <c>#FFD8BFD8</c>.</summary>
    public static readonly Color Thistle = new Color(0xFFD8BFD8);
    /// <summary>The predefined colour <c>Tomato</c>, ARGB <c>#FFFF6347</c>.</summary>
    public static readonly Color Tomato = new Color(0xFFFF6347);
    /// <summary>The predefined colour <c>Transparent</c>, ARGB <c>#00FFFFFF</c>.</summary>
    public static readonly Color Transparent = new Color(0x00FFFFFF);
    /// <summary>The predefined colour <c>Turquoise</c>, ARGB <c>#FF40E0D0</c>.</summary>
    public static readonly Color Turquoise = new Color(0xFF40E0D0);
    /// <summary>The predefined colour <c>Violet</c>, ARGB <c>#FFEE82EE</c>.</summary>
    public static readonly Color Violet = new Color(0xFFEE82EE);
    /// <summary>The predefined colour <c>Wheat</c>, ARGB <c>#FFF5DEB3</c>.</summary>
    public static readonly Color Wheat = new Color(0xFFF5DEB3);
    /// <summary>The predefined colour <c>White</c>, ARGB <c>#FFFFFFFF</c>.</summary>
    public static readonly Color White = new Color(0xFFFFFFFF);
    /// <summary>The predefined colour <c>WhiteSmoke</c>, ARGB <c>#FFF5F5F5</c>.</summary>
    public static readonly Color WhiteSmoke = new Color(0xFFF5F5F5);
    /// <summary>The predefined colour <c>Yellow</c>, ARGB <c>#FFFFFF00</c>.</summary>
    public static readonly Color Yellow = new Color(0xFFFFFF00);
    /// <summary>The predefined colour <c>YellowGreen</c>, ARGB <c>#FF9ACD32</c>.</summary>
    public static readonly Color YellowGreen = new Color(0xFF9ACD32);
}
