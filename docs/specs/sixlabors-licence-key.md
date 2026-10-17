# The Six Labors licence key

The key that lets the repository build `PdfPinata.ImageSharp4` in Release. PinataLabs/PdfPinata#306,
part of #305.

| item | what | status |
|---|---|---|
| 1 | An open-source community key, committed | done |
| 2 | A Release build with the key succeeds, and fails without it | done |
| 3 | A missing, unreadable or expired key fails with a message that names it and says how to renew | done |
| 4 | Nothing depends on a CI secret, so a pull request from a fork builds | done |

## The key

| | |
|---|---|
| Id | `ctm_01m53x4b44x8t38gypme9wv3gf` |
| Kind | Community (open source) |
| Expires | 2028-01-15 03:05 UTC |
| Covers | the assembly name `PdfPinata.ImageSharp4`, and no other |
| From | licensing.sixlabors.com, applied for by the maintainer for this open-source project |
| Copies | `src/PdfPinata.ImageSharp4/sixlabors.lic` |

Six Labors allows a community key to be committed to a public repository. That is why it is a file
in the tree and not a CI secret: a secret is not given to a pull request from a fork, so a fork's
Release build would fail.

**Keep the Copies row current.** Every project that has a `PackageReference` to SixLabors.ImageSharp
4.x or SixLabors.Fonts 3.x needs its own copy, and the renewal steps below rely on this list.

## Why one project, and why a copy per project

ImageSharp 4.x and Fonts 3.x (and ImageSharp 3.2+, Fonts 2.2+) ship an MSBuild target,
`SixLabors_ValidateLicense`, that runs before `CoreCompile`. It looks for a key in
`$(SixLaborsLicenseKey)`, then in `$(SixLaborsLicenseFile)`, then in any `sixlabors.lic` under the
project's own folder. Without a valid key, a Debug build warns and every other build fails. Nothing is
checked at run time, and the target is in the package's `build/` folder, which does not flow to
anything that references the project the package is installed in. So a consumer of the PdfPinata
package needs no key, and neither does a project in this repository that reaches ImageSharp through a
`ProjectReference` to `PdfPinata.ImageSharp4`.

**The key is scoped to one assembly name.** It was checked on 2026-10-17 with ImageSharp 4.1.3 and
Fonts 3.2.0: the same key passes a Release build whose `AssemblyName` is `PdfPinata.ImageSharp4` and
fails one whose `AssemblyName` is `PdfPinata.ImageSharp4.Tests` or anything else, with Six Labors'
bare "license validation failed". So a test project cannot reuse the key. It does not need to: a
test project that takes ImageSharp and Fonts only through its `ProjectReference` to
`PdfPinata.ImageSharp4` compiles against both, and is never checked. Give it no `PackageReference` to
either.

The key is a file per project rather than one at the root because Six Labors' search starts at the
project's folder. A root copy would need `SixLaborsLicenseFile` set for each project, and the copy
is simpler.

## What the build says

`Directory.Build.targets` adds two targets that run just before Six Labors' own, in exactly the
projects it runs in. Six Labors says only "No Six Labors license found" or "license validation
failed". These say which key, why, and what to do. Each uses Six Labors' severity: an error, or a
warning in a configuration whose name starts with `Debug`.

| code | when | |
|---|---|---|
| PPSL001 | the project has no `sixlabors.lic` | says the key covers `PdfPinata.ImageSharp4` alone, and what to do otherwise |
| PPSL002 | the key has no `ExpiryDateUtc` the build can read | |
| PPSL003 | the key has expired | names the Id and the date, and how to renew |
| PPSL004 | the key expires within 60 days | always a warning; `PdfPinataSixLaborsKeyWarningDays` changes the window |

The expiry check reads `ExpiryDateUtc` out of the key and compares it with the clock, with property
functions and nothing else, so it runs the same on Linux CI. It does not check the key's signature.
Six Labors' own target does that straight after.

The key is never packed or copied to the build output. `Directory.Build.targets` sets `Pack="false"`
and `CopyToOutputDirectory="Never"` on it, whether it is a `None` or a `Content` item. That holds even
when a project marks it `Pack="true"`.

## Renewing

1. Apply for a new open-source community key at https://licensing.sixlabors.com, for the assembly
   name `PdfPinata.ImageSharp4`. Ask for any other assembly name that needs one.
2. Replace every copy in the table above with the new key, as one line.
3. Update the table: the Id, the expiry date and, if they changed, the copies.
4. Build `src/PdfPinata.ImageSharp4` in Release. A build that passes without PPSL003 or PPSL004
   means the new key is in place.

PPSL004 starts to warn 60 days before the key expires, to leave time for this.
