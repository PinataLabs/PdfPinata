# Spec — the simple-type rule enforced, not just stated (T12)

`PdfItem` (`PdfPinata/Pdf/PdfItem.cs:40-43`) carried the rule as a bare comment — *"All simple
types (i.e. derived from PdfItem but not from PdfObject) must be immutable"* — and `Copy()`
(`:61-64`, still `MemberwiseClone()`) leaned on it without anything checking it. Twelve concrete
types derive from `PdfItem` without deriving from `PdfObject`. Eleven kept the rule for free, by
declaring only `readonly` fields; the twelfth, `PdfString`, did not, and said so above the field:
*"Not readonly: decrypting a string can reveal that it is UTF-16BE, see EncryptionValue"* — with
the setter that reassigned it commented, in turn, `// BUG: May lead to trouble with the value
semantics of PdfString`. This turned that comment into a test, and repaired the one type it was
apologising for.

## The reflection test

`SimpleTypeImmutabilityTests` (`PdfPinata.Test/Pdfs/SimpleTypeImmutabilityTests.cs`) landed
essentially as planned. `AllSimpleTypes()` (`:33-39`) enumerates
`typeof(PdfItem).Assembly.GetTypes()` and keeps every `t` where `PdfItem.IsAssignableFrom(t)`,
`!PdfObject.IsAssignableFrom(t)`, and `!t.IsAbstract` — the rule's own wording read back as a
predicate. Two `[Theory]` methods run over that set minus one exclusion:
`ASimpleTypeIsSealed` asserts `type.IsSealed`, and
`ASimpleTypeDeclaresNoFieldThatCanBeAssignedAfterConstruction` (`:72-90`) asserts every instance
field reached through `GetFields(BindingFlags.Instance | Public | NonPublic)` — not
`DeclaredOnly`, in case a future intermediate class adds one — is `IsInitOnly` or `IsLiteral`. The
field check is the whole enforcement; no separate sweep of public setters or method names exists,
because either would only ever agree with the field check.

`PdfReference` is excluded by a one-element `static readonly Type[] Excluded` (`:57`), not a
filter predicate, with the reasoning written as a doc comment above it: it is an indirection cell,
not a value, and `_objectID`, `_position`, `_value` and `_document` are all mutable on purpose —
`Position` rewritten by the writer as objects are laid out, `ObjectID` moved on renumbering,
`Document` assigned on filing into a cross-reference table, `Value` reassigned for the "dead
object" hack. Two more tests guard the exclusion list itself, both present in the plan and both
shipped: `TheOnlyExcludedTypeIsPdfReference` asserts the array is exactly `{ typeof(PdfReference) }`,
and `TheAssertionsWouldIndeedFailATypeThatBrokeTheRule` asserts `PdfReference`'s own fields are
non-empty when run through the same `IsInitOnly`/`IsLiteral` filter — proof the check is capable of
failing something, not merely passing everything it was pointed at. A fourth,
`TheSweepFindsTheTypesTheRuleWasWrittenAbout`, asserts the sweep's result actually contains the
twelve named types, so a sweep that silently found nothing would not read as green. All four match
the plan's Implementation and Testing Decisions almost line for line; nothing here diverged.

## `PdfString.EncryptionValue` becomes a factory, not a setter

This landed exactly as planned, down to the mechanism. `PdfString.cs:169-173`'s internal
constructor `PdfString(string value, PdfStringFlags flags)` — already present, needed no change —
now backs a static `internal static PdfString FromEncryptionValue(byte[] value, PdfStringFlags
flags)` (`PdfString.cs:219-236`), doing the identical byte-order-mark check and encoding switch the
old setter's body ran, but returning a new instance instead of assigning `_value` and `_flags` on
the one it was given. The getter it replaced is now a one-line expression body,
`internal byte[] EncryptionValue => _value == null ? new byte[0] : GetBytesFromEncoding();`
(`:206`). `_flags` (`:192`) and `_value` (`:201`) are both `readonly`; the comment explaining why
`_flags` was not is gone along with the field state it was explaining.

## The two call sites, and a subtlety the plan didn't anticipate

`PdfStandardSecurityHandler.EncryptString` (`:237-244`) changed from `void` to `PdfString`, exactly
as proposed: it reads `value.EncryptionValue`, runs it through `stringEncryptor.Encrypt`, and
returns `PdfString.FromEncryptionValue(bytes, value.Flags)` — except a zero-length string now short
-circuits by returning the same reference unencrypted (`:239-240`), a detail the plan's
Implementation Decisions did not call out but which follows from the same guard the old `void`
version had (`if (value.Length != 0)`).

`EncryptArray` (`:208-227`) landed exactly as the plan described: it already indexed by position,
so `array.Elements[idx] = EncryptString(value1);` is a one-line change with no reshaping of the
loop.

`EncryptDictionary` (`:153-203`) did not land the way the plan described. The plan's Implementation
Decisions said the write could go straight through the indexer inside the existing `foreach
(KeyValuePair<string, PdfItem> item in dict.Elements)`, arguing that overwriting an existing key
mid-enumeration is safe because ".NET's `Dictionary<TKey,TValue>` only invalidates an enumerator's
version on a structural change." That claim is only true of the net8.0 and net10.0 legs. This
fork also multi-targets `netstandard2.1` for Unity, whose Mono runtime *does* still bump a
dictionary's version on an overwrite, and the same loop would throw partway through decrypting a
document on that runtime. The shipped code collects instead: a `List<KeyValuePair<string,
PdfString>> decrypted` (`:167`) accumulates `(key, EncryptString(value1))` pairs while the
enumeration runs, and a second pass after the `foreach` ends (`:185-192`) writes each one through
`dict.Elements[replacement.Key] = replacement.Value`. The commit message calls this out directly:
"Overwriting an existing key mid-foreach is safe on the net8.0 and net10.0 legs … but netstandard2.1
exists for Unity's Mono runtime, which still bumps it." The plan's own multi-targeting section
(`CLAUDE.md`) says as much about `netstandard2.1` generally; the spec that reasoned about `Dictionary`
enumerator safety simply didn't reach for it. Both indexers still call `MarkOwnerAsChanged()`
exactly as the plan predicted, just from outside the loop rather than inside it.

## The dirty-flag consequence the plan predicted and that did not happen

This is where the implementation diverges from the plan most substantially. The plan's
Implementation and Testing Decisions spent real space arguing that going through the indexer would
mark a decrypted document's dictionaries and arrays dirty, that this was a real, user-visible
change for the one combination that could observe it — `PdfDocumentOpenMode.Append` plus a
password, followed by `SaveIncremental` with no edits of the caller's own — and that it should be
accepted rather than engineered around, quoting `PdfObject.IsDirty`'s own doc comment ("when in
doubt this says dirty") in its defence. A whole test was specified for it: open an encrypted
document, reopen for `Append`, call `SaveIncremental` with nothing else changed, and assert the
appended bytes are non-empty and contain the object that held the decrypted string.

It does not happen. `PdfDocument.CaptureOriginalBytes` (`PdfPinata/Pdf/PdfDocument.cs:448-479`)
clears `IsDirty` on every object once a document opened for `Append` has finished being read
(`:476-477`), under its own comment explaining why: "Reading a document mutates plenty of it … so
whatever is dirty at this point is dirty from being read rather than from being changed, and none
of it needs writing again." Decryption runs before that capture — `PdfReader.cs:568` calls
`document.SecurityHandler.EncryptDocument()`, and `PdfReader.cs:612` calls
`document.CaptureOriginalBytes(stream)` only afterward — so every `MarkOwnerAsChanged()` the new
indexer writes trigger is wiped by the same pass that always ran. `IsDirty` is read by
`SaveIncremental` and nothing else, so the predicted behaviour change is invisible from outside the
library after all; the fix really is, as User Story 6 asked for, indistinguishable from the outside.

`DecryptedStringReplacementTests.cs` records the finding rather than the plan's original
prediction. `DecryptingADocumentOnOpenLeavesNothingMarkedAsChanged` (`:127-144`) opens an encrypted
document with a Unicode title for `Append` and asserts `reopened.Info.IsDirty` is `false`.
`NothingAnEncryptedDocumentWasReadWithIsReportedAsChanged` (`:146-158`) asserts the same of every
object the reader touched, via `reopened.Internals.GetAllObjects().Should().OnlyContain(o =>
!o.IsDirty)`. Both docstrings say directly that they exist to pin the outcome discovered mid-fix
rather than leave it to be rediscovered by diffing an incremental save's byte count later. A third,
`ADecryptedDocumentStillSaysWhatItSaid` (`:160-171`), reopens the same document for `Modify` and
checks `Info.Title` and `Info.Author` round-trip unchanged, covering User Story 6 the direct way.
The file also carries a small leftover from the plan's original framing: a `static string
Appended(byte[] updated, int originalLength)` helper (`:191-193`), written for the `SaveIncremental`
byte-diffing test the plan specified, and never called — the test it was built for was replaced by
the two `IsDirty` assertions once the investigation found there was nothing to diff.

The rest of the plan's testing intentions landed as described: the reflection sweep round-trips
`FromEncryptionValue` by reflection rather than by building a whole encrypted document per case
(`PlainBytesBecomeThePlainStringTheySpell`, `ByteOrderMarkedBytesBecomeAUnicodeStringWithoutTheMark`,
`BytesAlreadyKnownToBeUnicodeAreReadAsUnicode`, `EverythingButTheEncodingSurvivesTheReplacement`,
`TheDecryptedStringIsANewObject`), and `PdfStringNoLongerOffersAWayToAssignItsValue` asserts
`typeof(PdfString).GetProperty("EncryptionValue", …).SetMethod` is `null` — the setter is gone, not
merely unused. The existing encryption integration tests in `PdfPinata.Test/Security/PdfSecurity.cs`
needed no change, exactly as the plan expected.

## `PdfStringObject`: an existing file, left alone and now explained

The diff touches `PdfPinata/Pdf/PdfStringObject.cs`, but it is not a new file — it existed
before this commit with its mutating `EncryptionValue` property already in place, and the whole of
the change is a seven-line `<remarks>` block added above that property (`:118-127`). The property
itself — getter and setter both, still assigning `_value` and `Encoding` in place
(`:128-153`) — is untouched. The new remarks say exactly what the plan's Implementation Decisions
argued: the simple-type rule only covers types derived from `PdfItem` but not from `PdfObject`,
`PdfStringObject` is a `PdfObject`, it has identity rather than value semantics, and it is reached
through its own indirect reference rather than held inside a dictionary or array entry — so there
is no entry for a caller to replace, and nothing for the new pattern to buy it. Its own doc comment
already said "This type is not used by PdfPinata" before this change and still does; the
asymmetry with `PdfString` is now a comment explaining a decision rather than a fact a future reader
would have to reconstruct.

## What was left alone

Everything the plan's Out of Scope section named stayed out of scope: `PdfReference`'s mutability
is unreworked, `PdfItem.Copy()` still uses `MemberwiseClone()` (now actually correct for all twelve
covered types), the AES/RC4 correctness in `PdfPinata.Test/Security/` is untouched, and
`IEncryptor.Encrypt` still runs the AES *decrypt* operation under that name (`AESEncryptor.cs:290`)
— unrenamed, as planned, because renaming it is a separate and unrelated fix. No source analyzer
was added; the reflection test is the only enforcement mechanism, running on every `dotnet test`
across both target frameworks as before.
