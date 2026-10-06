using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Marks a test, or a class of them, that moves the clock.
/// </summary>
/// <remarks>
/// <see cref="PdfPinata.GlobalTimeSettings.Clock"/> is one static for the whole application
/// domain, and testing it means setting it and putting it back. Any test that creates a document
/// while it is set would be stamped with the fixed time and could not tell why - and that is any
/// test at all, so a <see cref="NotInParallelAttribute"/> with no key runs each of these alone.
/// </remarks>
public sealed class ClockSensitiveAttribute : NotInParallelAttribute;
