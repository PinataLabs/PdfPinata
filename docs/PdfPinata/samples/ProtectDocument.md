# Protect Document

> **Runnable version:** the `Protect` demo.
> `dotnet run --project SampleApp -- run -e Protect`
>
> The demos are built on every commit and their page counts are asserted by
> `DemoSmokeTests`, so one that stops working fails the build. The code on this page is
> prose and has no such protection. See
> [Before any of this runs](index.md#before-any-of-this-runs) - this fork needs a backend
> registered before it will draw anything.

This sample shows how to protect a document with a password.


## Code

This is the whole source code needed to create the PDF file:

```cs
// Get a fresh copy of the sample PDF file
const string filenameSource = "HelloWorld.pdf";
const string filenameDest = "HelloWorld_tempfile.pdf";
File.Copy(Path.Combine("../../../../../PDFs/", filenameSource),
Path.Combine(Directory.GetCurrentDirectory(), filenameDest), true);
 
// Open an existing document. Providing an unrequired password is ignored.
PdfDocument document = PdfReader.Open(filenameDest, "some text");
 
PdfSecuritySettings securitySettings = document.SecuritySettings;
 
// Setting one of the passwords automatically sets the security level to
// PdfDocumentSecurityLevel.Encrypted128Bit.
securitySettings.UserPassword  = "user";
securitySettings.OwnerPassword = "owner";
 
// Don't use 40 bit encryption unless needed for compatibility
//securitySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted40Bit;
 
// Restrict some rights.
securitySettings.PermitAccessibilityExtractContent = false;
securitySettings.PermitAnnotations = false;
securitySettings.PermitAssembleDocument = false;
securitySettings.PermitExtractContent = false;
securitySettings.PermitFormsFill = true;
securitySettings.PermitFullQualityPrint = false;
securitySettings.PermitModifyDocument = true;
securitySettings.PermitPrint = false;
 
// Save the document...
document.Save(filenameDest);
```

## What this fork can and cannot write

`PdfDocumentSecurityLevel` offers 40-bit and 128-bit RC4, and setting either password selects
128-bit as the comment above says. **AES is read-only here:** `PdfReader` will open an
AES-encrypted document given the password, and nothing can write one. RC4 is long since broken as a
cipher, so treat the passwords above as a statement of intent to a well-behaved reader rather than
as protection against anybody who does not want to behave.

The eight `Permit…` flags are likewise advisory. They are recorded in the document and honoured by
readers that choose to; nothing enforces them.

Setting the owner password and then reopening the document with it is what makes
`SecuritySettings.HasOwnerPermissions` true. Reopening with the *user* password gives a document
that reads but will not save — `PdfReader.Open` in `Modify` mode refuses it, and the `Protect` demo
shows that refusal rather than hiding it.
