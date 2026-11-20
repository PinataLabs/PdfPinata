using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   An integer too large for a long used to end <c>PdfReader.Open</c> with an
///   <see cref="System.OverflowException"/> from <c>Lexer.ScanNumber</c>. It is read as a real now,
///   as pdf.js and MuPDF read it, and an object or generation number that overflows is still not
///   a number a reference can be made of. The content lexer's twin tests are in
///   <c>CLexerTests</c>.
/// </summary>
public class LexerHugeNumberTests
{
    private const string ThirtyNineDigits = "123456789012345678901234567890123456789";

    private static readonly string FourHundredDigits = new('7', 400);

    private static Lexer CreateLexer(string text) => new(new MemoryStream(Encoding.ASCII.GetBytes(text)));

    [Test, Timeout(5000)]
    [Arguments(ThirtyNineDigits)]
    [Arguments("-" + ThirtyNineDigits)]
    [Arguments("+" + ThirtyNineDigits)]
    [Arguments("9223372036854775808")]   // one past Int64.MaxValue
    [Arguments("-9223372036854775809")]  // one before Int64.MinValue
    [Arguments(ThirtyNineDigits + ".5")]
    [Arguments(ThirtyNineDigits + ".")]
    [Arguments("0." + ThirtyNineDigits)]
    public async Task ScanNumber_readsAnIntegerOrRealBeyondInt64AsARealAndDoesNotThrow(string text)
    {
        await Interruptibly.Run(() =>
        {
            var lexer = CreateLexer(text + " ");

            lexer.ScanNextToken().Should().Be(Symbol.Real);

            lexer.Token.Should().Be(text);
            lexer.TokenToReal.Should().Be(double.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
        });
    }

    [Test, Timeout(5000)]
    public async Task ScanNumber_readsFourHundredDigitsAsARealWithoutThrowing()
    {
        await Interruptibly.Run(() =>
        {
            foreach (var text in new[] { FourHundredDigits, "-" + FourHundredDigits, FourHundredDigits + "." + FourHundredDigits })
            {
                var lexer = CreateLexer(text + " ");

                lexer.ScanNextToken().Should().Be(Symbol.Real);

                lexer.Token.Should().Be(text);
            }
        });
    }

    [Test, Timeout(5000)]
    [Arguments("2147483647", Symbol.Integer)]
    [Arguments("-2147483648", Symbol.Integer)]
    [Arguments("2147483648", Symbol.Long)]
    [Arguments("9223372036854775807", Symbol.Long)]
    [Arguments("-9223372036854775808", Symbol.Long)]
    [Arguments("12.5", Symbol.Real)]
    public async Task ScanNumber_stillSeparatesIntegersLongsAndRealsInRange(string text, Symbol expected)
    {
        await Interruptibly.Run(() => CreateLexer(text + " ").ScanNextToken().Should().Be(expected));
    }

    [Test, Timeout(5000)]
    public async Task AnObjectOrGenerationNumberBeyondInt64ComesBackAsARealNotAnInteger()
    {
        // A real is neither an object number nor a generation number, so the parser,
        // which asks for an Integer in each place, treats them as malformed.
        var pdf = "%PDF-1.4\n" + ThirtyNineDigits + " 0 obj\n<< >>\nendobj\n1 " + ThirtyNineDigits + " obj\n<< >>\nendobj\n";

        await Interruptibly.Run(() =>
        {
            var lexer = CreateLexer(pdf);
            lexer.ScanNextToken().Should().Be(Symbol.Real);
            lexer.ScanNextToken().Should().Be(Symbol.Integer);
            lexer.ScanNextToken().Should().Be(Symbol.Obj);
            lexer.ScanNextToken().Should().Be(Symbol.BeginDictionary);
            lexer.ScanNextToken().Should().Be(Symbol.EndDictionary);
            lexer.ScanNextToken().Should().Be(Symbol.EndObj);
            lexer.ScanNextToken().Should().Be(Symbol.Integer);
            lexer.ScanNextToken().Should().Be(Symbol.Real);
            lexer.ScanNextToken().Should().Be(Symbol.Obj);
        });
    }

    [Test, Timeout(10000)]
    public async Task Open_readsA39DigitNumberInAnAnnotationRectAsAReal()
    {
        const string placeholder = "111111111111111111111111111111111111111";
        var document = new PdfDocument();
        document.AddPage().Elements["/PdfPinataHuge"] = new PdfLiteral("[0 0 " + placeholder + " 10]");
        using var saved = new MemoryStream();
        document.Save(saved, false);
        var text = Encoding.Latin1.GetString(saved.ToArray()).Replace(placeholder, ThirtyNineDigits);
        text.Should().Contain(ThirtyNineDigits);

        await Interruptibly.Run(() =>
        {
            using var ms = new MemoryStream(Encoding.Latin1.GetBytes(text));
            var reread = PdfPinata.Pdf.IO.PdfReader.Open(ms, PdfDocumentOpenMode.Import);

            var rect = (PdfArray)reread.Pages[0].Elements["/PdfPinataHuge"]!;
            rect.Elements.Count.Should().Be(4);
            rect.Elements[2].Should().BeOfType<PdfReal>();
            ((PdfReal)rect.Elements[2]).Value.Should().BeApproximately(1.2345678901234568e38, 1e24);
        });
    }
}
