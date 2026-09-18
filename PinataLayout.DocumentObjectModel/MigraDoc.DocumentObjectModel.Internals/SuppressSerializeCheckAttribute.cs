using System;

namespace PinataLayout.DocumentObjectModel.Internals;

/// <summary>
/// Opts a type out of MDG007, the check that a [DV] member's name appears somewhere in the type's
/// own Serialize method(s).
/// </summary>
/// <remarks>
/// Not a <c>#pragma warning disable</c>: a source generator's own diagnostics are not filtered by
/// pragma or by <c>.editorconfig</c> the way an ordinary analyzer's are - <c>#pragma warning
/// disable MDG007</c> compiles cleanly but the warning still appears in a real build. This
/// attribute is read by the generator itself instead, at parse time, so the question is answered
/// before the diagnostic is ever created.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class SuppressSerializeCheckAttribute : Attribute
{
    /// <param name="reason">
    /// Why this type's Serialize is exempt - required, so the suppression explains itself to the
    /// next reader rather than reading as "trust me".
    /// </param>
    public SuppressSerializeCheckAttribute(string reason) => Reason = reason;

    public string Reason { get; }
}
