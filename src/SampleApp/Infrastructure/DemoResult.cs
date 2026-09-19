using System;

namespace SampleApp.Infrastructure;

/// <summary>
///   What a demo produced. A demo that threw has no result; the runner reports the exception
///   instead and carries on to the next one.
/// </summary>
/// <param name="Name">The demo that produced it.</param>
/// <param name="OutputPath">The absolute path of the PDF written.</param>
/// <param name="PageCount">Pages in the saved document.</param>
/// <param name="Bytes">Size of the file on disk.</param>
/// <param name="Elapsed">How long building and saving it took.</param>
/// <param name="OpenPassword">
///   What the file needs before it will open, or null. Reported beside the path, so that somebody
///   running the one demo that encrypts its output is told the password rather than having to read
///   the source to find it.
/// </param>
public sealed record DemoResult(
    string Name,
    string OutputPath,
    int PageCount,
    long Bytes,
    TimeSpan Elapsed,
    string? OpenPassword = null);
