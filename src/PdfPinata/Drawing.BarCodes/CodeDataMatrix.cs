#region Copyright

//
// Authors:
//   David Stephensen
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

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Defines the DataMatrix 2D barcode. THIS IS AN EMPIRA INTERNAL IMPLEMENTATION. THE CODE IN
/// THE OPEN SOURCE VERSION IS A FAKE.
/// </summary>
public class CodeDataMatrix : MatrixCode
{
    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix()
        : this("", "", 26, 26, 0, XSize.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, int length)
        : this(code, "", length, length, 0, XSize.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, int length, XSize size)
        : this(code, "", length, length, 0, size)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, DataMatrixEncoding dmEncoding, int length, XSize size)
        : this(code, CreateEncoding(dmEncoding, code.Length), length, length, 0, size)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, int rows, int columns)
        : this(code, "", rows, columns, 0, XSize.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, int rows, int columns, XSize size)
        : this(code, "", rows, columns, 0, size)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, DataMatrixEncoding dmEncoding, int rows, int columns, XSize size)
        : this(code, CreateEncoding(dmEncoding, code.Length), rows, columns, 0, size)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, int rows, int columns, int quietZone)
        : this(code, "", rows, columns, quietZone, XSize.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of CodeDataMatrix.
    /// </summary>
    public CodeDataMatrix(string code, string encoding, int rows, int columns, int quietZone, XSize size)
        : base(code, encoding, rows, columns, size)
    {
        QuietZone = quietZone;
    }

    /// <summary>
    /// Sets the encoding of the DataMatrix.
    /// </summary>
    public void SetEncoding(DataMatrixEncoding dmEncoding)
    {
        Encoding = CreateEncoding(dmEncoding, Text.Length);
    }

    private static string CreateEncoding(DataMatrixEncoding dmEncoding, int length)
    {
        var tempencoding = dmEncoding switch
        {
            DataMatrixEncoding.Ascii => new string('a', length),
            DataMatrixEncoding.C40 => new string('c', length),
            DataMatrixEncoding.Text => new string('t', length),
            DataMatrixEncoding.X12 => new string('x', length),
            DataMatrixEncoding.EDIFACT => new string('e', length),
            DataMatrixEncoding.Base256 => new string('b', length),
            _ => ""
        };
        return tempencoding;
    }

    /// <summary>
    /// Gets or sets the size of the Matrix Quiet Zone.
    /// </summary>
    public int QuietZone { get; set; }

    /// <summary>
    /// Renders the matrix code.
    /// </summary>
    protected internal override void Render(XGraphics gfx, XBrush brush, XPoint position)
    {
        var state = gfx.Save();

        switch (Direction)
        {
            case CodeDirection.RightToLeft:
                gfx.RotateAtTransform(180, position);
                break;

            case CodeDirection.TopToBottom:
                gfx.RotateAtTransform(90, position);
                break;

            case CodeDirection.BottomToTop:
                gfx.RotateAtTransform(-90, position);
                break;
        }

        var modules = DataMatrixSymbol.Build(Text, Encoding, Rows, Columns);

        var size = Size.IsEmpty ? DefaultSize(modules) : Size;

        var pos = position + CalcDistance(Anchor, AnchorType.TopLeft, size);

        if (QuietZone > 0)
        {
            var sizeWithZone = new XSize(size.Width, size.Height);
            sizeWithZone.Width = sizeWithZone.Width / (Columns + 2 * QuietZone) * Columns;
            sizeWithZone.Height = sizeWithZone.Height / (Rows + 2 * QuietZone) * Rows;

            var posWithZone = new XPoint(pos.X, pos.Y);
            posWithZone.X += size.Width / (Columns + 2 * QuietZone) * QuietZone;
            posWithZone.Y += size.Height / (Rows + 2 * QuietZone) * QuietZone;

            gfx.DrawRectangle(XBrushes.White, pos.X, pos.Y, size.Width, size.Height);
            DrawModules(gfx, brush, modules, posWithZone, sizeWithZone);
        }
        else
            DrawModules(gfx, brush, modules, pos, size);

        gfx.Restore(state);
    }

    /// <summary>
    /// The extent to draw at when the caller never gave one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five of this class's eight constructors default <see cref="CodeBase.Size"/> to
    /// <see cref="XSize.Empty"/>, and an empty <see cref="XSize"/> is not a zero one - it is
    /// (-Infinity, -Infinity), which is how the type distinguishes "no size" from "no area". A
    /// code built by one of those constructors therefore used to draw every one of its modules at
    /// an infinite negative extent, which a viewer cannot parse and answers by drawing nothing.
    /// The barcode came out invisible, and the constructor taking nothing but text and a module
    /// count is the one the documentation and the upstream issue both use.
    /// </para>
    /// <para>
    /// Two points per module, quiet zone included, puts a 26 by 26 symbol at a little under 18mm
    /// square: comfortably larger than a reader needs and small enough to sit in the corner of a
    /// page. No better answer is available here - the code is told how many modules to draw and
    /// nothing whatever about the paper, so it cannot take a proportion of the page. A caller who
    /// cares passes a size, and one of the three constructors that take one is how.
    /// </para>
    /// <para>
    /// <see cref="BarCode.InitRendering"/> answers the same question the other way, refusing an
    /// empty size outright, and the difference is deliberate. A linear code's width depends on how
    /// many bars its text turns into, so there is no size to fall back on and nothing to do but
    /// ask. A matrix code is square and is measured in modules, both of which are known by the
    /// time it is drawn, so a default here means something.
    /// </para>
    /// </remarks>
    private XSize DefaultSize(bool[,] modules) => new(
        (modules.GetLength(1) + 2 * QuietZone) * _defaultModuleSize,
        (modules.GetLength(0) + 2 * QuietZone) * _defaultModuleSize);

    private const double _defaultModuleSize = 2;

    /// <summary>
    /// Draws the modules of the symbol, one filled square for each dark one. Drawn rather
    /// than drawn as a picture: a square is exact at any size, where a picture of one has a
    /// resolution of its own to disagree with the paper about, and needs an imaging backend
    /// that a caller wanting nothing but a barcode would otherwise not have to install.
    /// </summary>
    private static void DrawModules(XGraphics gfx, XBrush brush, bool[,] modules, XPoint position, XSize size)
    {
        var rows = modules.GetLength(0);
        var columns = modules.GetLength(1);
        var moduleWidth = size.Width / columns;
        var moduleHeight = size.Height / rows;

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (!modules[row, column])
                    continue;

                // Run of adjacent dark modules drawn as one rectangle, which keeps the
                // content stream short and leaves no seam between them.
                var run = 1;
                while (column + run < columns && modules[row, column + run])
                    run++;

                gfx.DrawRectangle(brush,
                    position.X + column * moduleWidth,
                    position.Y + row * moduleHeight,
                    run * moduleWidth,
                    moduleHeight);

                column += run - 1;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified string can be used as data in the DataMatrix.
    /// </summary>
    /// <param name="text">The code to be checked.</param>
    protected override void CheckCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
    }
}
