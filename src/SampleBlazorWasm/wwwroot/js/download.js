// Saves a stream the .NET side holds as a file: the documented Blazor pattern, an object URL on a
// Blob and a click on an <a download>. A browser - and a Playwright test - sees an ordinary download.
window.pdfPinata = {
    download: async function (fileName, streamReference) {
        const buffer = await streamReference.arrayBuffer();
        const url = URL.createObjectURL(new Blob([buffer], { type: "application/pdf" }));
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName ?? "";
        anchor.click();
        anchor.remove();
        URL.revokeObjectURL(url);
    }
};
