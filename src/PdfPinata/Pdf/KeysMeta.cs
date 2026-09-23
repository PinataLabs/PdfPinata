#region Copyright

//
// Authors:
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
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf;

/// <summary>
/// Holds information about the value of a key in a dictionary. This information is used to create
/// and interpret this value.
/// </summary>
internal sealed class KeyDescriptor
{
    /// <summary>
    /// Initializes a new instance of KeyDescriptor from the specified attribute during a KeysMeta
    /// initializes itself using reflection.
    /// </summary>
    public KeyDescriptor(KeyInfoAttribute attribute)
    {
        Version = attribute.Version;
        KeyType = attribute.KeyType;
        FixedValue = attribute.FixedValue;
        ObjectType = attribute.ObjectType;

        if (Version == "")
            Version = "1.0";
    }

    /// <summary>
    /// Gets or sets the PDF version starting with the availability of the described key.
    /// </summary>
    public string Version { get; set; }

    public KeyType KeyType { get; set; }

    public string KeyValue { get; set; }

    public string FixedValue { get; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    public Type ObjectType { get; set; }

    public bool CanBeIndirect => (KeyType & KeyType.MustNotBeIndirect) == 0;

    /// <summary>
    /// Returns the type of the object to be created as value for the described key.
    /// </summary>
    [return:
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                   DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    public Type GetValueType()
    {
        // If we have no ObjectType specified, use the KeyType enumeration.
        return ObjectType ?? TypeFromKeyType();
    }

    /// <summary>
    /// The type the <see cref="KeyType"/> names, for a key given no <see cref="ObjectType"/>.
    /// </summary>
    [return:
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                   DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    private Type TypeFromKeyType()
    {
        return (KeyType & KeyType.TypeMask) switch
        {
            KeyType.Name => typeof(PdfName),
            KeyType.String => typeof(PdfString),
            KeyType.Boolean => typeof(PdfBoolean),
            KeyType.Integer => typeof(PdfInteger),
            KeyType.Real => typeof(PdfReal),
            KeyType.Date => typeof(PdfDate),
            KeyType.Rectangle => typeof(PdfRectangle),
            KeyType.Array => typeof(PdfArray),
            KeyType.Dictionary => typeof(PdfDictionary),
            KeyType.Stream => typeof(PdfDictionary),
            KeyType.NumberTree => typeof(PdfNumberTreeNode),

            // The following types are not yet used
            KeyType.NameOrArray => throw new NotImplementedException("KeyType.NameOrArray"),
            KeyType.ArrayOrDictionary => throw new NotImplementedException("KeyType.ArrayOrDictionary"),
            KeyType.StreamOrArray => throw new NotImplementedException("KeyType.StreamOrArray"),
            KeyType.ArrayOrNameOrString => null, // HACK: Make PdfOutline work

            _ => InvalidKeyType()
        };
    }

    [return:
        DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors |
                                   DynamicallyAccessedMemberTypes.NonPublicConstructors)]
    private Type InvalidKeyType()
    {
        Debug.Assert(false, "Invalid KeyType: " + KeyType);
        return null;
    }
}

/// <summary>
/// Contains meta information about all keys of a PDF dictionary.
/// </summary>
internal class DictionaryMeta
{
    public DictionaryMeta([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type type)
    {
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var field in fields)
        {
            var attributes = field.GetCustomAttributes<KeyInfoAttribute>(false);
            foreach (var attribute in attributes)
            {
                var descriptor = new KeyDescriptor(attribute) { KeyValue = (string)field.GetValue(null) };
                // ReSharper disable once AssignNullToNotNullAttribute
                _keyDescriptors[descriptor.KeyValue] = descriptor;
            }
        }
    }

    /// <summary>
    /// Gets the KeyDescriptor of the specified key, or null if no such descriptor exits.
    /// </summary>
    public KeyDescriptor this[string key]
    {
        get
        {
            _keyDescriptors.TryGetValue(key, out var keyDescriptor);
            return keyDescriptor;
        }
    }

    private readonly Dictionary<string, KeyDescriptor> _keyDescriptors = new();
}
