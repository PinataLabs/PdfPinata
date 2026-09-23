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

namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// Describes one member of a <see cref="DocumentObject"/> that is reachable by name through
/// GetValue, SetValue, IsNull and SetNull.
/// </summary>
/// <remarks>
/// <para>
/// Sealed, and built from delegates rather than from FieldInfo and PropertyInfo. The accessors are
/// emitted by the source generator in PinataLayout.DocumentObjectModel.Generators, which reads the
/// [DV] attributes at compile time. Nothing here reflects.
/// </para>
/// <para>
/// The behaviour of every method below is the behaviour of the five descriptor classes it replaced,
/// with one deliberate exception, marked at <see cref="SetNull"/>.
/// </para>
/// </remarks>
public sealed class ValueDescriptor
{
  private readonly Func<DocumentObject, object> getter;
  private readonly Action<DocumentObject, object> setter;
  private readonly Func<DocumentObject> factory;
  private readonly object valueWhenNull;
  private readonly bool isField;

  internal ValueDescriptor(
    string valueName,
    Type valueType,
    Type memberType,
    ValueKind kind,
    bool isRefOnly,
    Func<DocumentObject, object> getter,
    Action<DocumentObject, object> setter,
    Func<DocumentObject> factory = null,
    object valueWhenNull = null,
    bool isField = false)
  {
    ValueName = valueName;
    ValueType = valueType;
    MemberType = memberType;
    Kind = kind;
    IsRefOnly = isRefOnly;
    this.getter = getter;
    this.setter = setter;
    this.factory = factory;
    this.valueWhenNull = valueWhenNull;
    this.isField = isField;
  }

  /// <summary>
  /// Name of the value, as written in DDL and passed to GetValue and SetValue.
  /// </summary>
  public string ValueName { get; }

  /// <summary>
  /// Type of the described value, e.g. typeof(int) for an int?.
  /// </summary>
  public Type ValueType { get; }

  /// <summary>
  /// Type of the described field or property, e.g. typeof(int?) for an int?.
  /// </summary>
  public Type MemberType { get; }

  /// <summary>
  /// What kind of member this is, and so how the methods below answer.
  /// </summary>
  public ValueKind Kind { get; }

  /// <summary>
  /// Whether the member is excluded from recursive operations. Only DocumentObject.parent is,
  /// which is what stops IsNull and SetNull walking up the document tree forever.
  /// </summary>
  public bool IsRefOnly { get; }

  /// <summary>
  /// Whether this describes a simple value rather than a DocumentObject that the rest of a dotted
  /// name is reached through.
  /// </summary>
  /// <remarks>
  /// Meta used to ask this by listing the descriptor classes that answered yes, so a new descriptor
  /// was wrong by default - NullableMemberDescriptor fell through to the DocumentObject branch and
  /// threw InvalidCastException on every string until the third name was added by hand.
  /// </remarks>
  public bool IsSimpleValue =>
    Kind == ValueKind.Leaf || Kind == ValueKind.NullableValue || Kind == ValueKind.PlainValue;

  /// <summary>
  /// Whether <see cref="SetValue"/> can be called at all. A DocumentObject or Collection member is
  /// only settable when it is a field - a property of that kind, such as Style.Font delegating to
  /// Style.ParagraphFormat.Font, has nowhere of its own to write a value into.
  /// </summary>
  public bool IsSettable => setter != null;

  /// <summary>
  /// Creates an instance of the described type using its parameterless constructor.
  /// </summary>
  public object CreateValue()
  {
    if (factory == null)
      throw new InvalidOperationException($"'{ValueName}' is not a type that can be created.");
    return factory();
  }

  /// <summary>
  /// Reads the member.
  /// </summary>
  public object GetValue(DocumentObject dom, GV flags)
  {
    if (!Enum.IsDefined(flags))
      throw new ArgumentException($@"'{flags}' is not a defined value of {nameof(GV)}.", nameof(flags));

    switch (Kind)
    {
      case ValueKind.Leaf:
      {
        var value = getter(dom);
        if (value == null)
          return flags == GV.GetNull ? null : valueWhenNull;
        return value;
      }

      case ValueKind.NullableValue:
      {
        var value = getter(dom);
        if (value is INullableValue nullable && nullable.IsNull && flags == GV.GetNull)
          return null;
        return value;
      }

      case ValueKind.PlainValue:
        return getter(dom);

      default:
      {
        var value = getter(dom) as DocumentObject;
        // Only a field is created on demand. A property has nowhere to put the new object.
        if (isField && value == null && flags == GV.ReadWrite)
        {
          value = factory();
          value.parent = dom;
          setter(dom, value);
          return value;
        }
        if (value != null && value.IsNull() && flags == GV.GetNull)
          return null;
        return value;
      }
    }
  }

  /// <summary>
  /// Writes the member.
  /// </summary>
  public void SetValue(DocumentObject dom, object value)
  {
    if (setter == null)
      throw new InvalidOperationException("This value cannot be set.");
    setter(dom, value);
  }

  /// <summary>
  /// Resets the member, so that <see cref="IsNull"/> is true afterwards.
  /// </summary>
  public void SetNull(DocumentObject dom)
  {
    switch (Kind)
    {
      case ValueKind.Leaf:
        setter(dom, null);
        break;

      case ValueKind.NullableValue:
      {
        // The struct is boxed by the getter, mutated through the interface, and written back.
        var value = getter(dom);
        ((INullableValue)value).SetNull();
        setter(dom, value);
        break;
      }

      case ValueKind.PlainValue:
        // A bool or a plain enum has no null to write. The descriptor class this replaced cast to
        // INullableValue here without checking, so new FormattedText().SetNull() threw
        // InvalidCastException on the first of its five bool and enum properties. This is the one
        // deliberate behaviour change in the move to a generated model: it does nothing instead.
        break;

      default:
      {
        var value = getter(dom) as DocumentObject;
        if (value != null)
          value.SetNull();
        break;
      }
    }
  }

  /// <summary>
  /// Determines whether the member is null (not set).
  /// </summary>
  public bool IsNull(DocumentObject dom)
  {
    switch (Kind)
    {
      case ValueKind.Leaf:
        return getter(dom) == null;

      case ValueKind.NullableValue:
        return getter(dom) is INullableValue nullable && nullable.IsNull;

      case ValueKind.PlainValue:
        return false;

      default:
      {
        // Fields and properties answer alike. The class this replaced computed val.IsNull() for a
        // property, discarded it and returned true regardless, so Style.Font - the only [DV]
        // property in the DOM whose type is a DocumentObject - reported itself null whatever it
        // held. That was carried forward unchanged through the move to a generated model so the
        // parity harness gated a replacement rather than a behaviour change; this is the deliberate
        // change afterwards.
        var value = getter(dom) as DocumentObject;
        return value == null || value.IsNull();
      }
    }
  }
}
