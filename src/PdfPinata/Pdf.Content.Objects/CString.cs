#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Text;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Content.Objects;

/// <summary>
/// Represents a string value in a PDF content stream.
/// </summary>
[DebuggerDisplay("({Value})")]
public class CString : CObject
{
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new CString Clone()
    {
        return (CString)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var obj = base.Copy();
        return obj;
    }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string Value
    {
        get => _value;
        set => _value = value;
    }

    string _value;

    /// <summary>
    /// Gets or sets the type of the content string.
    /// </summary>
    public CStringType CStringType
    {
        get => _cStringType;
        set => _cStringType = value;
    }

    CStringType _cStringType;

    /// <summary>
    /// Returns a string that represents the current value.
    /// </summary>
    public override string ToString()
    {
        var s = new StringBuilder();
        switch (CStringType)
        {
            case CStringType.String:
                s.Append('(');
                var length = _value.Length;
                for (var ich = 0; ich < length; ich++)
                {
                    var ch = _value[ich];
                    switch (ch)
                    {
                        case Chars.LF:
                            s.Append("\\n");
                            break;

                        case Chars.CR:
                            s.Append("\\r");
                            break;

                        case Chars.HT:
                            s.Append("\\t");
                            break;

                        case Chars.BS:
                            s.Append("\\b");
                            break;

                        case Chars.FF:
                            s.Append("\\f");
                            break;

                        case Chars.ParenLeft:
                            s.Append("\\(");
                            break;

                        case Chars.ParenRight:
                            s.Append("\\)");
                            break;

                        case Chars.BackSlash:
                            s.Append("\\\\");
                            break;

                        default:
                            s.Append(ch);
                            break;
                    }
                }

                s.Append(')');
                break;


            case CStringType.HexString:
                throw new NotImplementedException();
            //break;

            case CStringType.UnicodeString:
                throw new NotImplementedException();
            //break;

            case CStringType.UnicodeHexString:
                throw new NotImplementedException();
            //break;

            case CStringType.Dictionary:
                s.Append(_value);
                break;

            default:
                #pragma warning disable S3877 // An undefined CStringType has no textual form, as the unfinished Unicode cases above already say by throwing.
                throw new ArgumentOutOfRangeException();
                #pragma warning restore S3877
        }

        return s.ToString();
    }

    internal override void WriteObject(ContentWriter writer)
    {
        writer.WriteRaw(ToString());
    }
}
