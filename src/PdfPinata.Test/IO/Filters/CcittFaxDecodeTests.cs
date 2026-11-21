using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
/// CCITTFaxDecode (ISO 32000-1 7.4.6), checked against data this library did not write: each
/// <c>.bin</c> under <c>Assets/Ccitt</c> is Ghostscript's CCITTFaxEncode of a bitmap stored beside
/// it, and <c>group4-scan.pdf</c> is the PDF ImageMagick writes for a scan. <c>Generate.ps1</c>
/// makes them all again.
/// </summary>
public class CcittFaxDecodeTests
{
    internal static readonly Pbm Text = Pbm.Read("text.pbm");

    [Test]
    [Arguments("g4", -1, false, false, false)]
    [Arguments("g4-noeob", -1, false, false, false)]
    [Arguments("g4-align", -1, false, true, false)]
    [Arguments("g4-blackis1", -1, false, false, true)]
    [Arguments("g3-1d", 0, false, false, false)]
    [Arguments("g3-1d-noeob", 0, false, false, false)]
    [Arguments("g3-1d-eol", 0, true, false, false)]
    [Arguments("g3-1d-eol-align", 0, true, true, false)]
    [Arguments("g3-1d-align", 0, false, true, false)]
    [Arguments("g3-2d", 4, false, false, false)]
    [Arguments("g3-2d-eol", 4, true, false, false)]
    [Arguments("g3-2d-eol-align", 4, true, true, false)]
    [Arguments("g3-2d-blackis1", 2, true, false, true)]
    public void EachCodingDecodesToTheBitmapItWasMadeFrom(string name, int k, bool endOfLine, bool byteAlign, bool blackIs1)
    {
        var parameters = Parameters(("/K", k), ("/Columns", Text.Width), ("/Rows", Text.Height),
            ("/EndOfLine", endOfLine), ("/EncodedByteAlign", byteAlign), ("/BlackIs1", blackIs1));

        Decode(Asset(name + ".bin"), parameters).Should().Equal(Text.Bits(blackIs1));
    }

    [Test]
    [Arguments("g4", -1, false)]
    [Arguments("g4-noeob", -1, false)]
    [Arguments("g3-1d", 0, false)]
    [Arguments("g3-1d-noeob", 0, false)]
    [Arguments("g3-2d-eol-align", 4, true)]
    public void WithoutRowsTheRowsAreThoseTheDataHolds(string name, int k, bool byteAlign)
    {
        // With or without the end-of-block code: the data ends where the last row does, give or
        // take the padding to a byte.
        var parameters = Parameters(("/K", k), ("/Columns", Text.Width), ("/EndOfLine", k == 4),
            ("/EncodedByteAlign", byteAlign));

        Decode(Asset(name + ".bin"), parameters).Should().Equal(Text.Bits(blackIs1: false));
    }

    [Test]
    [Arguments("g4", -1)]
    [Arguments("g3-1d", 0)]
    public void TheEndOfBlockEndsTheDataWhateverFollowsIt(string name, int k)
    {
        // EOFB in Group 4 and RTC in Group 3: without /Rows, the bytes after them are not rows.
        var data = Asset(name + ".bin").Concat(Enumerable.Repeat((byte)0xA5, 40)).ToArray();

        Decode(data, Parameters(("/K", k), ("/Columns", Text.Width))).Should().Equal(Text.Bits(blackIs1: false));
    }

    [Test]
    [Arguments("wide-g4", -1)]
    [Arguments("wide-g3-1d", 0)]
    public void RunsLongerThan1728AreCodedWithTheExtendedMakeUpCodes(string name, int k)
    {
        // The rows encode.ps paints: white; black; black for 2000 columns; black from 100 to 2650.
        const int columns = 2700;
        const int rowLength = (columns + 7) / 8;
        var expected = new byte[rowLength * 4];
        expected.AsSpan().Fill(0xFF);
        Black(1, 0, 2700);
        Black(2, 0, 2000);
        Black(3, 100, 2650);

        Decode(Asset(name + ".bin"), Parameters(("/K", k), ("/Columns", columns), ("/Rows", 4))).Should().Equal(expected);

        void Black(int row, int from, int to)
        {
            for (var x = from; x < to; x++)
                expected[row * rowLength + x / 8] &= (byte)~(0x80 >> (x % 8));
        }
    }

    [Test]
    public void TheDefaultsAreOneDimensionalRowsOf1728Columns()
    {
        // A white row in one dimension: 1728 as a make-up code and 0 as a terminating one.
        var row = Bits("010011011" + "00110101");

        Filtering.CcittFaxDecode.Decode(row, (FilterParms)null).Should().Equal(Enumerable.Repeat((byte)0xFF, 216));
    }

    [Test]
    public void AGroup4ScanImageMagickWroteDecodesToTheScan()
    {
        var scan = Pbm.Read("scan.pbm");
        using var document = PdfPinata.Pdf.IO.PdfReader.Open(Path.Combine(AppContext.BaseDirectory, "Assets", "Ccitt", "group4-scan.pdf"),
            PdfDocumentOpenMode.Import);
        var image = document.Internals.GetAllObjects().OfType<PdfDictionary>()
            .Single(dictionary => dictionary.Elements.GetName("/Subtype") == "/Image" && dictionary.Elements.GetInteger("/Width") == scan.Width);

        image.Stream.TryUnfilter().Should().BeTrue();

        image.Stream.Value.Should().Equal(scan.Bits(blackIs1: false));
        image.Elements.ContainsKey("/Filter").Should().BeFalse();
    }

    [Test]
    [Arguments("g4", -1)]
    [Arguments("g3-1d", 0)]
    [Arguments("g3-2d", 4)]
    public void DataCutShortDecodesAsFarAsItGoesAndTheRestIsWhite(string name, int k)
    {
        var data = Asset(name + ".bin");
        var decoded = Decode(data.AsSpan(0, data.Length / 2).ToArray(),
            Parameters(("/K", k), ("/Columns", Text.Width), ("/Rows", Text.Height)));

        decoded.Should().HaveCount(Text.Bits(false).Length, "every row of /Rows is there");
        var rows = Rows(decoded);
        var expected = Rows(Text.Bits(false));
        var good = Enumerable.Range(0, Text.Height).TakeWhile(row => rows[row].SequenceEqual(expected[row])).Count();
        good.Should().BeGreaterThan(Text.Height / 4, "the rows before the cut are decoded");
        good.Should().BeLessThan(Text.Height);
        rows.Skip(good + 1).Should().AllSatisfy(row => row.Should().OnlyContain(value => value == 0xFF));
    }

    [Test]
    public void DataCutShortWithoutRowsIsTheWholeRowsItHolds()
    {
        var data = Asset("g4.bin");
        var decoded = Decode(data.AsSpan(0, data.Length / 2).ToArray(), Parameters(("/K", -1), ("/Columns", Text.Width)));

        (decoded.Length % Text.RowLength).Should().Be(0);
        decoded.Length.Should().BeLessThan(Text.Bits(false).Length);
        decoded.AsSpan(0, Text.RowLength * 10).ToArray().Should().Equal(Text.Bits(false).AsSpan(0, Text.RowLength * 10).ToArray());
    }

    [Test]
    [Arguments(0, false)]
    [Arguments(5, true)]
    public void ADamagedRowIsSteppedOverToTheNextEndOfLineOnlyAsOftenAsDamagedRowsBeforeErrorSays(int damagedRowsBeforeError, bool resumes)
    {
        // Two bytes of zeros in the middle of a row read as an end-of-line code where no row ends.
        var data = Asset("g3-1d-eol.bin");
        data[data.Length / 2] = 0;
        data[data.Length / 2 + 1] = 0;
        var decoded = Rows(Decode(data, Parameters(("/K", 0), ("/EndOfLine", true), ("/Columns", Text.Width),
            ("/Rows", Text.Height), ("/DamagedRowsBeforeError", damagedRowsBeforeError))));

        var expected = Rows(Text.Bits(false));
        decoded.Take(10).Should().BeEquivalentTo(expected.Take(10), options => options.WithStrictOrdering());
        var last = decoded[Text.Height - 1];
        if (resumes)
            last.Should().Equal(expected[Text.Height - 1]);
        else
            last.Should().OnlyContain(value => value == 0xFF);
    }

    [Test, Timeout(60_000)]
    public async Task NoDataIsAnExceptionOrAHang()
    {
        // Whatever the bytes, each row costs at least one bit, so the work ends with the data.
        var lengths = await Interruptibly.Run(() =>
        {
            var random = new Random(385);
            var decodedLengths = new System.Collections.Generic.List<int>();
            foreach (var k in new[] { -1, 0, 3 })
            {
                foreach (var endOfLine in new[] { false, true })
                {
                    foreach (var byteAlign in new[] { false, true })
                    {
                        for (var trial = 0; trial < 1000; trial++)
                        {
                            var data = new byte[random.Next(0, 400)];
                            random.NextBytes(data);
                            var rows = trial % 2 == 0 ? 0 : random.Next(1, 100);
                            var decoded = Decode(data, Parameters(("/K", k), ("/Columns", random.Next(1, 3000)), ("/Rows", rows),
                                ("/EndOfLine", endOfLine), ("/EncodedByteAlign", byteAlign), ("/DamagedRowsBeforeError", 3)));
                            decodedLengths.Add(decoded.Length);
                        }
                    }
                }
            }

            // Valid data with bits flipped, which gets further into a row than random bytes do.
            foreach (var (name, k, endOfLine, byteAlign) in new[]
                     {
                         ("g4", -1, false, false), ("g4-align", -1, false, true), ("g3-1d-eol", 0, true, false),
                         ("g3-2d", 4, false, false), ("g3-2d-eol-align", 4, true, true),
                     })
            {
                var valid = Asset(name + ".bin");
                for (var trial = 0; trial < 1000; trial++)
                {
                    var data = (byte[])valid.Clone();
                    for (var flip = random.Next(1, 4); flip > 0; flip--)
                        data[random.Next(data.Length)] ^= (byte)(1 << random.Next(8));
                    var decoded = Decode(data, Parameters(("/K", k), ("/Columns", Text.Width), ("/Rows", trial % 2 * Text.Height),
                        ("/EndOfLine", endOfLine), ("/EncodedByteAlign", byteAlign), ("/DamagedRowsBeforeError", 3)));
                    decodedLengths.Add(decoded.Length);
                }
            }

            return decodedLengths;
        });

        lengths.Should().HaveCount(17000);
    }

    [Test]
    public void ASizeThatWouldDecodeToMoreThan256MiBIsRefusedBeforeAnythingIsDecoded()
    {
        var act = () => Decode([0xFF], Parameters(("/K", -1), ("/Columns", 1 << 20), ("/Rows", 1 << 20)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than*");
    }

    [Test]
    [Arguments("/Columns", 0)]
    [Arguments("/Rows", -1)]
    public void AParameterOutOfRangeIsRefused(string key, int value)
    {
        var act = () => Decode([0xFF], Parameters((key, value)));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{key}*");
    }

    [Test]
    public void FacsimileDataIsNotEncoded()
    {
        var act = () => Filtering.CcittFaxDecode.Encode([0xFF]);

        act.Should().Throw<NotSupportedException>();
    }

    private static byte[] Decode(byte[] data, PdfDictionary parameters) => Filtering.CcittFaxDecode.Decode(data, parameters);

    private static PdfDictionary Parameters(params (string Key, object Value)[] entries)
    {
        var parameters = new PdfDictionary();
        foreach (var (key, value) in entries)
        {
            parameters.Elements[key] = value switch
            {
                int integer => new global::PdfPinata.Pdf.PdfInteger(integer),
                bool boolean => new PdfBoolean(boolean),
                _ => throw new ArgumentException("No PDF item for " + value),
            };
        }

        return parameters;
    }

    private static byte[][] Rows(byte[] bits) => bits.Chunk(Text.RowLength).ToArray();

    private static byte[] Bits(string digits)
    {
        digits = digits.PadRight((digits.Length + 7) / 8 * 8, '0');
        return digits.Chunk(8).Select(octet => Convert.ToByte(new string(octet), 2)).ToArray();
    }

    internal static byte[] Asset(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Ccitt", name));

    /// <summary>A binary PBM: black is 1, and each row is padded to a byte with zeros, which are white.</summary>
    internal sealed class Pbm
    {
        private readonly byte[] _blackIs1;

        private Pbm(int width, int height, byte[] blackIs1)
        {
            Width = width;
            Height = height;
            _blackIs1 = blackIs1;
        }

        internal int Width { get; }

        internal int Height { get; }

        internal int RowLength => (Width + 7) / 8;

        internal bool IsBlack(int x, int y) => (_blackIs1[y * RowLength + x / 8] & (0x80 >> (x % 8))) != 0;

        internal byte[] Bits(bool blackIs1) => blackIs1 ? [.. _blackIs1] : _blackIs1.Select(value => (byte)~value).ToArray();

        internal static Pbm Read(string name)
        {
            var file = Asset(name);
            var at = 0;
            var header = new string[3];
            for (var field = 0; field < 3; field++)
            {
                while (char.IsWhiteSpace((char)file[at]) || file[at] == '#')
                {
                    if (file[at] == '#')
                        while (file[at] != '\n')
                            at++;
                    at++;
                }

                var start = at;
                while (!char.IsWhiteSpace((char)file[at]))
                    at++;
                header[field] = Encoding.ASCII.GetString(file, start, at - start);
            }

            header[0].Should().Be("P4");
            var width = int.Parse(header[1]);
            var height = int.Parse(header[2]);
            return new Pbm(width, height, file.AsSpan(at + 1, (width + 7) / 8 * height).ToArray());
        }
    }
}
