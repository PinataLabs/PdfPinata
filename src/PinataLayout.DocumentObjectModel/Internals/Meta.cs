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
using System.Collections.Generic;
using PinataLayout.DocumentObjectModel.Resources;

namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// The name-addressable value model of one DocumentObject type.
/// </summary>
/// <remarks>
/// Built at compile time. Each DOM class receives a generated static table from the source
/// generator in PinataLayout.DocumentObjectModel.Generators, which reads the [DV] attributes.
/// This class used to reflect over the type in its constructor, behind an unsynchronised static in
/// each of the 67 DOM classes; both are gone.
/// </remarks>
public sealed class Meta
{
  readonly ValueDescriptor[] descriptors;
  readonly Dictionary<string, ValueDescriptor> byName;

  /// <summary>
  /// Initializes a Meta from its generated descriptor table.
  /// </summary>
  internal Meta(params ValueDescriptor[] descriptors)
  {
    this.descriptors = descriptors;

    // Ordinal rather than InvariantCulture, which is what the Hashtable this replaced used. Member
    // names are ASCII identifiers, so the two agree, and ordinal is both faster and immune to the
    // culture the caller happens to be running under.
    byName = new Dictionary<string, ValueDescriptor>(descriptors.Length, StringComparer.OrdinalIgnoreCase);
    foreach (var descriptor in descriptors)
      byName.Add(descriptor.ValueName, descriptor);
  }

  /// <summary>
  /// Gets the metaobject of the specified document object.
  /// </summary>
  /// <param name="documentObject">The document object the meta is returned for.</param>
  public static Meta GetMeta(DocumentObject documentObject)
  {
    return documentObject.Meta;
  }

  /// <summary>
  /// Gets the object specified by name from dom.
  /// </summary>
  public object GetValue(DocumentObject dom, string name, GV flags)
  {
    var dot = name.IndexOf('.');
    if (dot == 0)
      throw new ArgumentException(string.Format(AppResources.InvalidValueName, name));
    string trail = null;
    if (dot > 0)
    {
      trail = name[(dot + 1)..];
      name = name[..dot];
    }
    var vd = this[name];
    if (vd == null)
      throw new ArgumentException(string.Format(AppResources.InvalidValueName, name));

    var value = vd.GetValue(dom, flags);
    if (value == null && flags == GV.GetNull)
      return null;

    if (trail != null)
    {
      if (value == null || trail == "")
        throw new ArgumentException(string.Format(AppResources.InvalidValueName, name));
      var doc = value as DocumentObject;
      if (doc == null)
        throw new ArgumentException(string.Format(AppResources.InvalidValueName, name));
      value = doc.GetValue(trail, flags);
    }
    return value;
  }

  /// <summary>
  /// Sets the member of dom specified by name to val.
  /// If a member with the specified name does not exist an ArgumentException will be thrown.
  /// </summary>
  public void SetValue(DocumentObject dom, string name, object val)
  {
    var dot = name.IndexOf('.');
    if (dot == 0)
      throw new ArgumentException(DomSR.InvalidValueName(name));
    string trail = null;
    if (dot > 0)
    {
      trail = name[(dot + 1)..];
      name = name[..dot];
    }
    var vd = this[name];
    if (vd == null)
      throw new ArgumentException(DomSR.InvalidValueName(name));

    if (trail != null)
    {
      var doc = dom.GetValue(name) as DocumentObject;
      // ReSharper disable once PossibleNullReferenceException
      doc.SetValue(trail, val);
    }
    else
      vd.SetValue(dom, val);
  }

  /// <summary>
  /// Determines whether this meta contains a value with the specified name.
  /// </summary>
  public bool HasValue(string name)
  {
    return this[name] != null;
  }

  /// <summary>
  /// Sets the member of dom specified by name to null.
  /// If a member with the specified name does not exist an ArgumentException will be thrown.
  /// </summary>
  public void SetNull(DocumentObject dom, string name)
  {
    var vd = this[name];
    if (vd == null)
      throw new ArgumentException(DomSR.InvalidValueName(name));

    vd.SetNull(dom);
  }

  /// <summary>
  /// Determines whether the member of dom specified by name is null.
  /// If a member with the specified name does not exist an ArgumentException will be thrown.
  /// </summary>
  public bool IsNull(DocumentObject dom, string name)
  {
    var dot = name.IndexOf('.');
    if (dot == 0)
      throw new ArgumentException(DomSR.InvalidValueName(name));
    string trail = null;
    if (dot > 0)
    {
      trail = name[(dot + 1)..];
      name = name[..dot];
    }
    var vd = this[name];
    if (vd == null)
      throw new ArgumentException(DomSR.InvalidValueName(name));

    // A simple value answers for itself. Anything else is a DocumentObject that the rest of the
    // name is reached through. This used to be a list of descriptor class names, which meant a new
    // descriptor was wrong by default.
    if (vd.IsSimpleValue)
    {
      if (trail != null)
        throw new ArgumentException(DomSR.InvalidValueName(name));
      return vd.IsNull(dom);
    }
    var docObj = (DocumentObject)vd.GetValue(dom, GV.ReadOnly);
    if (docObj == null)
      return true;
    return trail != null ? docObj.IsNull(trail) : docObj.IsNull();
  }

  /// <summary>
  /// Sets all members of the specified dom to null.
  /// </summary>
  public void SetNull(DocumentObject dom)
  {
    foreach (var vd in descriptors)
    {
      if (!vd.IsRefOnly)
        vd.SetNull(dom);
    }
  }

  /// <summary>
  /// Determines whether all members of the specified dom are null. If dom contains no members
  /// IsNull returns true.
  /// </summary>
  public bool IsNull(DocumentObject dom)
  {
    foreach (var vd in descriptors)
    {
      if (vd.IsRefOnly)
        continue;
      if (!vd.IsNull(dom))
        return false;
    }
    return true;
  }

  /// <summary>
  /// Gets the ValueDescriptor of the member specified by name, or null if there is none.
  /// Lookup is case-insensitive.
  /// </summary>
  public ValueDescriptor this[string name] =>
    name != null && byName.TryGetValue(name, out var vd) ? vd : null;

  /// <summary>
  /// Gets the descriptors of this type, in declaration order, base class first.
  /// </summary>
  public IReadOnlyList<ValueDescriptor> ValueDescriptors => descriptors;
}
