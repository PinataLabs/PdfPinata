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

using System.Collections.Generic;
using System.Diagnostics;
using PdfPinata.Pdf.Advanced;
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
    /// Gets the name of the on state: the first state other than <c>/Off</c> that the widgets'
    /// normal appearances name, or <c>/Yes</c> - the name ISO 32000-1 uses throughout - when none
    /// of them names one.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="PdfAcroField.Widgets"/>, which for a field merged with its only widget
    /// is that widget. This used to read the field's own <c>/AP</c> alone, which a field whose
    /// widgets are separate from it - the shape <see cref="PdfAcroField.AddWidget"/> always makes -
    /// does not have, so it answered <c>/Yes</c> whatever the widgets named.
    /// </remarks>
    protected string GetNonOffValue()
    {
        foreach (var widget in Widgets)
        {
            var states = StatesOf(widget);
            if (states == null)
                continue;

            foreach (var state in states)
            {
                if (IsOnState(state))
                    return state;
            }
        }

        // A field built by hand, or one whose appearances have been stripped, names no state at
        // all, and answering /Yes is better than handing a null to the caller's SetName.
        return "/Yes";
    }

    /// <summary>
    /// The off state, whatever the on state is called.
    /// </summary>
    private protected const string Off = "/Off";

    /// <summary>
    /// Whether a state name is an on state. Some forms name their off state <c>/Nein</c>, German
    /// for "no", rather than <c>/Off</c>, and that is read as off too.
    /// </summary>
    private protected static bool IsOnState(string name) => name.Length != 0 && name != Off && name != "/Nein";

    /// <summary>
    /// The names of a widget's normal appearance states, or null when it has no normal
    /// appearances to choose between.
    /// </summary>
    private protected static ICollection<string> StatesOf(PdfDictionary widget)
    {
        var appearances = PdfReference.Dereference(widget.Elements[PdfAnnotation.Keys.AP]);

        var normal = PdfReference.Dereference((appearances as PdfDictionary)?.Elements["/N"]);

        return normal is PdfDictionary states ? states.Elements.Keys : null;
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
