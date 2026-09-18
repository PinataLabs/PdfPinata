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

namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// What kind of member a <see cref="ValueDescriptor"/> describes, and so how it answers.
/// </summary>
/// <remarks>
/// This replaces the abstract-class-per-kind hierarchy the value model used to carry. The kinds
/// differ only in behaviour, never in state, and one of the two callers had to ask which subclass it
/// was holding by listing the types by name - which meant a new kind was wrong by default.
/// </remarks>
public enum ValueKind
{
    /// <summary>
    /// A member that carries its own null: Nullable&lt;T&gt; or a string. Being null is what "not
    /// set" means, so there is no wrapper struct to mutate through INullableValue and write back.
    /// </summary>
    Leaf,

    /// <summary>
    /// A struct that implements INullableValue and so tracks its own null: Unit, Color, LeftPosition
    /// and TopPosition. Reading, nulling and testing all go through the interface.
    /// </summary>
    NullableValue,

    /// <summary>
    /// A value type with no null of its own - a plain bool or enum. FormattedText's delegating
    /// properties are the only members of this kind. They can be read and written but not unset, so
    /// SetNull does nothing and IsNull is always false.
    /// </summary>
    PlainValue,

    /// <summary>
    /// A nested DocumentObject. Created on demand when read under <see cref="GV.ReadWrite"/>, if the
    /// member is a field.
    /// </summary>
    DocumentObject,

    /// <summary>
    /// A DocumentObjectCollection. Behaves as <see cref="DocumentObject"/> does; kept distinct
    /// because the DDL parser and the serializer care about the difference.
    /// </summary>
    Collection,
}
