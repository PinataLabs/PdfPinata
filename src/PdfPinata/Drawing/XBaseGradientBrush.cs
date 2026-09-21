#region Copyright
//
// Authors:
//   Ben Askren
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



// ReSharper disable RedundantNameQualifier because it is required for hybrid build
namespace PdfPinata.Drawing;

/// <summary>The state two-colour gradient brushes have in common: the colour at each end.</summary>
public class XBaseGradientBrush : XBrush
{
    /// <summary>Initializes a new gradient brush running between the two given colours.</summary>
    protected XBaseGradientBrush(XColor color1, XColor color2)
    {
        Color1 = color1;
        Color2 = color2;

    }

    /// <summary>
    /// Gets or sets whether the gradient goes on past its start in its first colour.
    /// </summary>
    /// <remarks>
    /// The start is the first point of an <see cref="XLinearGradientBrush"/>, or the first circle
    /// of an <see cref="XRadialGradientBrush"/>. Left false, nothing is painted before it, which
    /// for a radial gradient whose first radius is not zero is a hole in the middle. The name is
    /// the one PDFsharp gives it; it is written as the first half of the shading's
    /// <c>/Extend</c>.
    /// </remarks>
    public bool ExtendLeft { get; set; }

    /// <summary>
    /// Gets or sets whether the gradient goes on past its end in its second colour.
    /// </summary>
    /// <remarks>
    /// The end is the second point of an <see cref="XLinearGradientBrush"/>, or the second circle
    /// of an <see cref="XRadialGradientBrush"/>. Left false, nothing is painted beyond it, so a
    /// radial gradient filling a rectangle leaves the corners outside its outer circle unpainted;
    /// set it to give them the second colour.
    /// </remarks>
    public bool ExtendRight { get; set; }

    /// <summary>
    /// Gets or sets an XMatrix that defines a local geometric transform for this gradient. It is
    /// applied to the gradient's points and radii before the transform of the graphics the
    /// gradient is drawn with.
    /// </summary>
    public XMatrix Transform
    {
        get => Matrix;
        set => Matrix = value;
    }

    /// <summary>
    /// Translates the brush with the specified offset.
    /// </summary>
    public void TranslateTransform(double dx, double dy)
    {
        Matrix.TranslatePrepend(dx, dy);
    }

    /// <summary>
    /// Translates the brush with the specified offset.
    /// </summary>
    public void TranslateTransform(double dx, double dy, XMatrixOrder order)
    {
        Matrix.Translate(dx, dy, order);
    }

    /// <summary>
    /// Scales the brush with the specified scalars.
    /// </summary>
    public void ScaleTransform(double sx, double sy)
    {
        Matrix.ScalePrepend(sx, sy);
    }

    /// <summary>
    /// Scales the brush with the specified scalars.
    /// </summary>
    public void ScaleTransform(double sx, double sy, XMatrixOrder order)
    {
        Matrix.Scale(sx, sy, order);
    }

    /// <summary>
    /// Rotates the brush with the specified angle.
    /// </summary>
    public void RotateTransform(double angle)
    {
        Matrix.RotatePrepend(angle);
    }

    /// <summary>
    /// Rotates the brush with the specified angle.
    /// </summary>
    public void RotateTransform(double angle, XMatrixOrder order)
    {
        Matrix.Rotate(angle, order);
    }

    /// <summary>
    /// Multiply the brush transformation matrix with the specified matrix.
    /// </summary>
    public void MultiplyTransform(XMatrix matrix)
    {
        Matrix.Prepend(matrix);
    }

    /// <summary>
    /// Multiply the brush transformation matrix with the specified matrix.
    /// </summary>
    public void MultiplyTransform(XMatrix matrix, XMatrixOrder order)
    {
        Matrix.Multiply(matrix, order);
    }

    /// <summary>
    /// Resets the brush transformation matrix with identity matrix.
    /// </summary>
    public void ResetTransform()
    {
        Matrix = new XMatrix();
    }

    //public void SetBlendTriangularShape(double focus);
    //public void SetBlendTriangularShape(double focus, double scale);
    //public void SetSigmaBellShape(double focus);
    //public void SetSigmaBellShape(double focus, double scale);



    //public Blend Blend { get; set; }
    //public bool GammaCorrection { get; set; }
    //public ColorBlend InterpolationColors { get; set; }
    //public XColor[] LinearColors { get; set; }
    //public RectangleF Rectangle { get; }
    //public WrapMode WrapMode { get; set; }
    //private bool interpolationColorsWasSet;

    internal XColor Color1, Color2;
    internal XMatrix Matrix;

}
