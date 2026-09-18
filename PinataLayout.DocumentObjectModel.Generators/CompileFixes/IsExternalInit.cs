// netstandard2.0 - which a Roslyn analyzer must target - predates the type the compiler emits
// init accessors and records against. Same polyfill the DOM and PdfPinata projects carry.

namespace System.Runtime.CompilerServices;

internal static class IsExternalInit
{
}
