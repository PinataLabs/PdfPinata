using System.Collections.Generic;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Compares keys by which object they are. <c>ReferenceEqualityComparer</c> does the same and is
/// not in netstandard2.1.
/// </summary>
/// <remarks>
/// A document holds one instance for an object of the file, so the instance is the object's
/// identity, and its reference is where it has one.
/// </remarks>
internal sealed class ReferenceIdentity : IEqualityComparer<object>
{
    internal static readonly ReferenceIdentity Instance = new();

    /// <summary>What stands for a dictionary as a key: its reference, or itself where it is written in place.</summary>
    internal static object KeyOf(PdfDictionary dictionary) => (object)dictionary.Reference ?? dictionary;

    bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
