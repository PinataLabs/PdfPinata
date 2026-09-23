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
using System.Collections.Generic;
using System.Diagnostics;
using PdfPinata.Pdf.Annotations;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Represents the base class for all button fields.
/// </summary>
public abstract class PdfButtonField : PdfAcroField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfButtonField"/> class.
    /// </summary>
    protected PdfButtonField(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfButtonField"/> class of the named type.
    /// </summary>
    /// <param name="document">The document the field belongs to.</param>
    /// <param name="fieldType">The value of <c>/FT</c>, which for every button is <c>/Btn</c>.</param>
    private protected PdfButtonField(PdfDocument document, string fieldType)
        : base(document, fieldType)
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfButtonField"/> class.
    /// </summary>
    protected PdfButtonField(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// A <c>/Btn</c> is a push button, a radio group or a check box according to these two bits,
    /// so they belong to the class rather than to the caller.
    /// </summary>
    private protected override PdfAcroFieldFlags KindMask
        => PdfAcroFieldFlags.Pushbutton | PdfAcroFieldFlags.Radio;

    /// <summary>
    /// Gets the name which represents the opposite of /Off.
    /// </summary>
    protected string GetNonOffValue()
    {
        // Try to get the information from the appearance dictionaray.
        // Just return the first key that is not /Off.
        // I'm not sure what is the right solution to get this value.
        if (Elements[PdfAnnotation.Keys.AP] is PdfDictionary ap && ap.Elements["/N"] is PdfDictionary n)
        {
            foreach (var name in n.Elements.Keys)
                if (name != "/Off")
                    return name;
        }
        // A field built by hand, or one whose appearances have been stripped, names no state at
        // all. /Yes is what the reference uses throughout for the on state of a check box, and
        // answering with it is better than handing a null to the caller's SetName.
        return "/Yes";
    }

    internal override void GetDescendantNames(ref List<string> names, string partialName)
    {
        var t = Elements.GetString(PdfAcroField.Keys.T);
        // HACK: ??? 
        if (t == "")
            t = "???";
        Debug.Assert(t != "");
        if (t.Length <= 0)
            return;

        if (!string.IsNullOrEmpty(partialName))
            names.Add(partialName + "." + t);
        else
            names.Add(t);
    }

    /// <summary>
    /// Predefined keys of this dictionary. 
    /// The description comes from PDF 1.4 Reference.
    /// </summary>
    public new class Keys : PdfAcroField.Keys
    {
        // Pushbuttons have no additional entries.
    }
}
