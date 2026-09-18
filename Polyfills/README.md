# Polyfills

Members the .NET analyzers ask for that `netstandard2.1` does not have, written as C# 14 static
extension members on the types that own them on .NET 8 and later. `Directory.Build.props` compiles
every file here into the `netstandard2.1` leg of each project, and into nothing else, so on `net8.0`
and `net10.0` a call such as `ArgumentNullException.ThrowIfNull(value)` binds to the runtime's own
method and on `netstandard2.1` to the one here. Shared source can then be written once, the modern
way, rather than behind `#if`.

Each is `internal`, so every assembly gets its own copy and none of them is visible to a consumer.
Add a member here when a fix needs one; keep its behaviour — the exception type and the parameter
name — the same as the runtime's, so the three legs cannot disagree about what they throw.
