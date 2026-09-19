using System;

namespace PdfPinata.Internal;

/// <summary>
/// Tells an exception that says something about the data being read from one that says something
/// about the process reading it.
/// </summary>
static class Unrecoverable
{
    /// <summary>
    ///   Whether an exception is one there is no carrying on from. A catch that means "this input
    ///   is malformed" — one that swallows the failure, substitutes a fallback, or rewraps it as a
    ///   font or parser error — is wrong about an <see cref="OutOfMemoryException"/>, which says
    ///   nothing about the input and everything about the process reading it. Disguising one as a
    ///   parse error loses the diagnosis and leaves the process to fail later somewhere unrelated,
    ///   with nothing pointing back here.
    ///   <para>
    ///   Used as an exception filter — <c>catch (Exception ex) when (!Unrecoverable.Is(ex))</c> —
    ///   so that an unrecoverable exception is never caught in the first place, and the stack it
    ///   carries is the stack of where it was thrown rather than of where it was rethrown.
    ///   </para>
    /// </summary>
    public static bool Is(Exception ex)
        // InsufficientMemoryException derives from OutOfMemoryException, so it is covered too.
        => ex is OutOfMemoryException;
}
