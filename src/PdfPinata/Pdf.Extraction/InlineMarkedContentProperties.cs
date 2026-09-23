using PdfPinata.Pdf.Content;

namespace PdfPinata.Pdf.Extraction;

/// <summary>
/// Reads the two keys text extraction cares about out of an inline marked-content properties
/// dictionary — the raw <c>&lt;&lt;...&gt;&gt;</c> text <see cref="CLexer.ScanDictionary"/> hands
/// back as one opaque token, because there is no object-model parser for a dictionary that never
/// became an indirect object. A property list written by name instead is a real
/// <see cref="PdfDictionary"/> already, reached through the page's own resources, and does not come
/// through here.
/// </summary>
internal static class InlineMarkedContentProperties
{
    /// <summary>
    /// Reads <c>/ActualText</c> and <c>/MCID</c> out of the raw dictionary text. Anything else the
    /// dictionary declares, and anything malformed about it, is ignored rather than reported.
    /// </summary>
    internal static (string ActualText, int? Mcid) Read(string rawDictionary)
    {
        // Stripped rather than reparsed from '<' — the dictionary token always opens with exactly
        // two '<' and closes with the exactly two '>' that balanced them, so what is between is
        // always its content, hex strings, nested dictionaries and all. See CLexer.ScanDictionary.
        if (rawDictionary == null || rawDictionary.Length < 4
            || rawDictionary[0] != '<' || rawDictionary[1] != '<'
            || rawDictionary[^1] != '>' || rawDictionary[^2] != '>')
            return (null, null);

        var inner = rawDictionary.Substring(2, rawDictionary.Length - 4);

        // Content-stream strings are byte strings, one char per byte — the same convention the
        // content lexer itself reads with, so turning this text back into bytes for a lexer of its
        // own to re-scan is a direct cast rather than an encoding.
        var bytes = new byte[inner.Length];
        for (var index = 0; index < inner.Length; index++)
            bytes[index] = (byte)inner[index];

        var lexer = new CLexer(bytes);
        string actualText = null;
        int? mcid = null;
        string key = null;

        CSymbol symbol;
        while ((symbol = lexer.ScanNextToken()) != CSymbol.Eof)
        {
            switch (symbol)
            {
                case CSymbol.Name:
                    key = lexer.Token;
                    continue;

                case CSymbol.Integer:
                    if (key == "/MCID")
                        mcid = lexer.TokenToInteger;
                    break;

                // A literal or hex string with no byte order mark is read the same way the
                // extractor falls back to for a simple font with no /ToUnicode: Latin-1, right for
                // the range PDFDocEncoding and WinAnsi share and the only guess available here.
                case CSymbol.String:
                case CSymbol.HexString:
                    if (key == "/ActualText")
                        actualText = lexer.Token;
                    break;

                // Already decoded to real characters by the content lexer, which reads the byte
                // order mark itself and turns the bytes after it into UTF-16 code units.
                case CSymbol.UnicodeString:
                case CSymbol.UnicodeHexString:
                    if (key == "/ActualText")
                        actualText = lexer.Token;
                    break;
            }

            key = null;
        }

        return (actualText, mcid);
    }
}
