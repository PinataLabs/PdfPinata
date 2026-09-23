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

using System.Diagnostics;

namespace PdfPinata.Pdf.Content.Objects;

/// <summary>
/// Represents an operator a PDF content stream.
/// </summary>
[DebuggerDisplay("({Name}, operands={Operands.Count})")]
public class COperator : CObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="COperator"/> class.
    /// </summary>
    protected COperator()
    {
    }

    internal COperator(OpCode opcode)
    {
        _opcode = opcode;
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    public new COperator Clone()
    {
        return (COperator)Copy();
    }

    /// <summary>
    /// Implements the copy mechanism of this class.
    /// </summary>
    protected override CObject Copy()
    {
        var copy = (COperator)base.Copy();
        copy._seqence = _seqence?.Clone();
        return copy;
    }

    /// <summary>
    /// Gets or sets the name of the operator
    /// </summary>
    /// <value>The name.</value>
    public virtual string Name => _opcode.Name;

    /// <summary>
    /// Gets or sets the operands.
    /// </summary>
    /// <value>The operands.</value>
    public CSequence Operands => _seqence ?? (_seqence = []);

    private CSequence _seqence;

    /// <summary>
    /// Gets the operator description for this instance.
    /// </summary>
    public OpCode OpCode => _opcode;

    private readonly OpCode _opcode;


    /// <summary>
    /// Returns a string that represents the current operator.
    /// </summary>
    public override string ToString()
    {
        if (_opcode.OpCodeName == OpCodeName.Dictionary)
            return " ";

        return Name;
    }

    internal override void WriteObject(ContentWriter writer)
    {
        var count = _seqence?.Count ?? 0;
        for (var idx = 0; idx < count; idx++)
        {
            // ReSharper disable once PossibleNullReferenceException because the loop is not entered if _sequence is null
            _seqence[idx].WriteObject(writer);
        }

        writer.WriteLineRaw(ToString());
    }
}
