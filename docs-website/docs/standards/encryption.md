---
title: Encryption and permissions
description: Protect a PDF with a user and an owner password, set its permission flags, open protected documents, and remove protection.
demos: [Protect]
---

A PDF can be encrypted so that it opens only with a password, and it can carry permission flags that
ask a reader not to print, copy or change it. PdfPinata writes RC4-encrypted documents and reads
documents encrypted with RC4 or AES. Everything on this page is in the core `PdfPinata` package, in
namespaces `PdfPinata.Pdf.Security` and `PdfPinata.Pdf.IO`.

Use encryption to keep a document from casual readers, or to state how its author wants it used. Do
not rely on it to protect secrets: see [Things to know](#things-to-know).

## Two passwords

A protected PDF has two passwords, and they do different jobs:

- The **user password** opens the document. A reader asks for it before it shows anything.
- The **owner password** lifts the restrictions. A reader asks for it before it lets someone change
  the permissions.

You can set either or both. A document with only an owner password opens for anyone, and the
permissions still apply. Most "protected" PDFs you meet are like this: they open without a password
and refuse to print.

## Protect a document

Set the passwords and permissions on `document.SecuritySettings`, then save. The settings are read
when the document is saved, so you can set them at any point before that:

```csharp demo=Protect snippet=encrypt
```

Setting either password turns encryption on. If `DocumentSecurityLevel` is still `None`, it becomes
`Encrypted128Bit`. If you set a security level but no password, the save fails.

`PdfDocumentSecurityLevel` has three values:

| Value | Meaning |
|---|---|
| `None` | Not encrypted. The default. |
| `Encrypted40Bit` | 40-bit RC4. Use it only for a reader that cannot open anything newer. |
| `Encrypted128Bit` | 128-bit RC4. |

PdfPinata does not write AES encryption.

## Permission flags

Each flag is a `bool` on `PdfSecuritySettings`. All eight are allowed by default.

| Property | Allows the reader to |
|---|---|
| `PermitPrint` | print the document |
| `PermitFullQualityPrint` | print at full resolution rather than as a low-quality draft |
| `PermitExtractContent` | copy text and graphics |
| `PermitAccessibilityExtractContent` | extract text for a screen reader or other assistive tool |
| `PermitModifyDocument` | change the content |
| `PermitAssembleDocument` | insert, rotate or delete pages |
| `PermitAnnotations` | add or change comments and markup |
| `PermitFormsFill` | fill in form fields |

Leave `PermitAccessibilityExtractContent` on unless you have a reason. Turning it off stops screen
readers from reading the document.

## Open a protected document

Pass the password to `PdfReader.Open`. A wrong password throws `PdfReaderException`. A password
given for a document that is not encrypted is ignored.

Which password you give decides what you can do:

- The **owner password** opens the document in any mode, including `PdfDocumentOpenMode.Modify`.
- The **user password** opens it in `ReadOnly`, `Import` and `Append` mode. Opening it with
  `PdfDocumentOpenMode.Modify` throws `PdfReaderException`, which says the owner password is required.

```csharp demo=Protect snippet=open-modes
```

`PdfReader.Open(path, password)` without a mode opens for `Modify`, so with a user password you must
also pass another mode.

`SecuritySettings.HasOwnerPermissions` tells you which password opened the document: `true` for the
owner password or an unencrypted document, `false` for the user password. PdfPinata enforces the
open mode; the permission flags are yours to honour. If your program shows or processes documents
for other people, check the flags and `HasOwnerPermissions` before you print or copy.

### Ask for the password only when it is needed

If you do not know in advance whether a file is encrypted, pass a `PdfPasswordProvider` callback.
PdfPinata calls it only when the document needs a password, which is where an application shows a
password dialog. Set `args.Password` to try a password, or set `args.Abort` to `true` to give up, in
which case `Open` returns `null`:

```csharp demo=Protect snippet=password-provider
```

If the password the callback gives is wrong, or is only the user password when you asked for
`Modify`, PdfPinata calls it again.

## Remove protection

If you have the owner password, you can save an unprotected copy:

```csharp
PdfDocument document = PdfReader.Open("protected.pdf", "owner-password", PdfDocumentOpenMode.Modify);
document.Save("unprotected.pdf");
```

A document read from an encrypted file is saved without encryption unless you set a password again.
The old passwords are not reused. To keep the file protected after changing it, set
`UserPassword`, `OwnerPassword` and the permissions again before you save.

PdfPinata does not help to open a document without its password.

## Things to know

- **A permission is a request, not a lock.** The flags travel inside the file, and a well-behaved
  reader honours them. A program that has the user password can ignore them. Only the encryption
  itself stops someone reading the content.
- **RC4 is a broken cipher.** A determined attacker can recover the content of an RC4-encrypted PDF.
  Treat PdfPinata's encryption as a statement to honest readers, not as protection for sensitive
  data. If you need strong protection, encrypt the file with another tool after PdfPinata writes it.
- **AES is read, not written.** Documents encrypted by other tools with AES-128 or AES-256 open
  normally when you give the password.
- **PDF/A forbids encryption.** A document that claims a [PDF/A](./pdf-a.md) level and has a password
  will not save.
- **Saving rewrites the whole file.** If you open a signed document and save it again, with or
  without new passwords, its signatures break. See [Digital signatures](./digital-signatures.md).
- **The demo's passwords are public.** The Protect demo's PDF opens with the user password `open-me`.
  Its owner password is `owner-only`. Both are printed on its first page.

## See it in action

[The Protect demo](../demos.mdx#protect) writes an encrypted document with a mixed set of
permissions. It then reads a protected document back with each password and shows which open modes
each allows.

<details>
<summary>The full Protect demo</summary>

```csharp demo=Protect
```

</details>
