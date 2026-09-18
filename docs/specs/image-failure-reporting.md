# Spec — image failure reporting, issue #366

[empira/PDFsharp#366](https://github.com/empira/PDFsharp/issues/366) reports that an image which
fails to decode surfaces as a generic error with the original exception discarded, so a decode that
ran out of memory is indistinguishable in a log from a file in an unsupported format. What follows
is the design as built, on `fix/image-failure-diagnosis`.

| item | what | status |
|---|---|---|
| 1 | `XImage.FromStream` swallowing decode exceptions | does not apply to this fork |
| 2 | `ImageRenderer` swallows every exception, `OutOfMemoryException` included | done |
| 3 | The reason an image failed is unreachable from outside | done, `DocumentRenderer.ImageFailed` |
| 4 | `ImageFailure.InvalidType` is always overwritten by `NotRead` | done, turned up on the way |
| 5 | `ImageFailure.EmptySize` never reaches the format info | done, turned up on the way |

---

## Item 1 — the reported API is already right here

The issue is written against `PngImageImporter`, which PDFsharp 6.x wraps in
`catch (Exception) { return null; }` and whose null return `XImage.FromStream` turns into
`InvalidOperationException("Unsupported image format.")`.

PdfPinata has no importer of its own. `XImage.FromStream` goes through the
`ImageSource.ImageSourceImpl` seam to a backend, and neither backend swallows anything:

- `SkiaImageSource.Decode` throws `InvalidOperationException` naming the image and the
  `SKCodecResult` that failed, and its one `catch` disposes the bitmap and rethrows.
- `ImageSharpImageSource` catches only `MissingMemberException` and `TypeLoadException`, and only
  to translate an ImageSharp 3.x binding failure into an explanatory exception with the original as
  `InnerException`.

So the exact defect reported does not exist here. The anti-pattern it describes does, one layer up.

## Items 2 and 3 — MigraDoc swallows the reason instead

`PinataLayout.Rendering.ImageRenderer` catches every exception in two places — measuring the image
in `CalculateImageDimensions`, and drawing it in `Render` — and replaces the image with a grey
placeholder. That behaviour is wanted: one unreadable image should not cost a five hundred page
report. Two things about it were not.

**`OutOfMemoryException` was caught along with everything else.** Running out of memory says nothing
about the image and everything about the process rendering it. Swallowing it turns a memory problem
into a page of grey boxes and leaves the process to fall over somewhere else, with nothing pointing
back at the image that exhausted it. It now propagates, through an exception filter rather than a
rethrow so the stack is never touched:

```csharp
catch (Exception ex) when (!IsUnrecoverable(ex))
```

`IsUnrecoverable` names `OutOfMemoryException` alone. `InsufficientMemoryException` derives from it
and so is covered. `StackOverflowException` cannot be caught, and .NET Core does not deliver
`AccessViolationException` to a managed handler, so neither needs naming.

**The exception went to `Debug.WriteLine` and nowhere else,** which a release build compiles away
entirely. The exception is now kept on `ImageFormatInfo.FailureException` from the point it is
caught, and every placeholder drawn raises a new event:

```csharp
public event EventHandler<ImageFailedEventArgs> ImageFailed;   // on DocumentRenderer
```

carrying the `Image` from the document, the `ImageFailure` kind, and the `Exception` — the same
instance that was thrown, not a copy or a message.

`RenderFailureImage` is the single funnel: every path that draws a placeholder goes through it, so
there is exactly one event per placeholder and no path that draws one silently.

### Why an event and not a throw

Throwing would be the simpler change and the wrong one. MigraDoc's contract is that a document with
a bad image still renders, and callers depend on it. An event leaves that contract alone while
making the reason reachable, which is what the issue actually asks for.

### API surface added

- `PinataLayout.Rendering.ImageFailedEventArgs` — new, sealed, internal constructor.
- `PinataLayout.Rendering.DocumentRenderer.ImageFailed` — new event.
- `PinataLayout.Rendering.ImageFailure` — was `internal`, now `public`, because the event arguments
  carry it. Additive; nothing that compiled before stops compiling.

## Item 4 — `InvalidType` was always overwritten

`CalculateImageDimensions` caught `InvalidOperationException` from `XImage.FromImageSource`, set
`Failure = InvalidType`, and carried straight on into the measuring block that dereferences the
`xImage` it had just failed to create. The `NullReferenceException` that followed was caught by the
general handler, which set `Failure = NotRead` over the top of it. `InvalidType` was therefore
never reported and its placeholder text never drawn — every failure looked like `NotRead`.

The catch now sizes the placeholder and returns.

## Item 5 — `EmptySize` never reached the format info

The empty-size branch assigned the renderer's `failure` field, but `Format` had already copied that
field into `formatInfo.Failure` before `CalculateImageDimensions` ran, so the assignment went
nowhere. `Render` saw `None`, tried to draw the image, and the `EmptySize` placeholder was
unreachable. It now sets `formatInfo.Failure` directly, and the `failure` field — which nothing
else assigned — is gone. `Format` resets `Failure`/`FailureException` explicitly in its place.

## Placeholder sizing

The three failure paths sized their placeholder differently; they now share
`SetFallbackDimensions`, which takes whatever size the document asked for and falls back to 2.5cm
per side. It also refuses a non-positive size, which would otherwise draw a placeholder of nothing
and hide the failure it exists to show.

## Deliberately not done

- No logging abstraction. The library has none, and adding one for a single call site would be a
  larger decision than this issue justifies. The event lets a caller route the failure into
  whatever logging it already has.
- `XImage`, `ImageSource` and both backends are untouched. They already propagate.
- The `Debug.WriteLine` calls stay. They cost nothing in release and are still useful under a
  debugger for a caller that has attached no handler.
