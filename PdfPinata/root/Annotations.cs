using System;

// ReSharper disable once CheckNamespace
namespace JetBrains.Annotations;

/// <summary>
/// Marks a method whose return value carries the result of the call, so that ignoring it is
/// always a mistake. ReSharper and Rider recognise this attribute by its full name and warn on
/// a call whose result is discarded.
/// <para>
/// It is declared here rather than taken from the JetBrains.Annotations package so that
/// PdfPinata gains no dependency. It is deliberately <c>internal</c>: a public type in the
/// JetBrains.Annotations namespace would collide with the real package in any consuming project
/// that references both. That means the warning fires while building PdfPinata itself, but
/// not in code that merely consumes the compiled library - enforcing it downstream needs a
/// Roslyn analyzer shipped with the package.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Delegate)]
internal sealed class MustUseReturnValueAttribute : Attribute
{
    public MustUseReturnValueAttribute()
    {
    }

    public MustUseReturnValueAttribute(string justification)
    {
        Justification = justification;
    }

    public string Justification { get; private set; }
}
