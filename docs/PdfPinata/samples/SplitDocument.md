# Split Document

> **Runnable version:** the `Assemble` demo.
> `dotnet run --project SampleApp -- run -e Assemble`
>
> The demos are built on every commit and their page counts are asserted by
> `DemoSmokeTests`, so one that stops working fails the build. The code on this page is
> prose and has no such protection. See
> [Before any of this runs](index.md#before-any-of-this-runs) - this fork needs a backend
> registered before it will draw anything.

This sample shows how to convert a PDF document with n pages into n documents with one page each.


## Code

This is the whole source code needed to create the PDF file:

```cs
// Get a fresh copy of the sample PDF file
const string filename = "Portable Document Format.pdf";
File.Copy(Path.Combine("../../../../../PDFs/", filename),
Path.Combine(Directory.GetCurrentDirectory(), filename), true);
 
// Open the file
PdfDocument inputDocument = PdfReader.Open(filename, PdfDocumentOpenMode.Import);
 
string name = Path.GetFileNameWithoutExtension(filename);
for (int idx = 0; idx < inputDocument.PageCount; idx++)
{
    // Create new document
    PdfDocument outputDocument = new PdfDocument();
    outputDocument.Version = inputDocument.Version;
    outputDocument.Info.Title =
    String.Format("Page {0} of {1}", idx + 1, inputDocument.Info.Title);
    outputDocument.Info.Creator = inputDocument.Info.Creator;
    
    // Add the page and save it
    outputDocument.AddPage(inputDocument.Pages[idx]);
    outputDocument.Save(String.Format("{0} - Page {1}_tempfile.pdf", name, idx + 1));
}
```
