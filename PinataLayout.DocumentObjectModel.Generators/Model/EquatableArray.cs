using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PinataLayout.DocumentObjectModel.Generators.Model;

/// <summary>
/// An <see cref="ImmutableArray{T}"/> that compares by value.
/// </summary>
/// <remarks>
/// ImmutableArray's Equals is reference equality on the backing array, so a model record containing
/// one is never equal to a structurally identical rebuild of itself. Putting it in an incremental
/// generator pipeline defeats caching silently: every keystroke re-runs every downstream stage and
/// re-emits identical source. This wrapper is the standard fix.
/// </remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    readonly T[]? array;

    public EquatableArray(IEnumerable<T> items) => array = items.ToArray();

    public int Count => array?.Length ?? 0;

    public T this[int index] => array![index];

    public bool Equals(EquatableArray<T> other)
    {
        if (array is null || other.array is null)
            return array is null && other.array is null;
        if (array.Length != other.array.Length)
            return false;
        for (int i = 0; i < array.Length; i++)
        {
            if (!array[i].Equals(other.array[i]))
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (array is null)
            return 0;
        int hash = 17;
        foreach (T item in array)
            hash = hash * 31 + item.GetHashCode();
        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(array ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
