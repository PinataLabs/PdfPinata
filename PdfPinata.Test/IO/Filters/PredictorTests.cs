using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO.Filters;

/// <summary>
///   A deflated or LZW stream may have been put through a <em>predictor</em> first: each byte
///   replaced by its difference from a neighbour, so that smooth data turns into runs of small
///   numbers and compresses better. Undoing that is the last step of decoding, and it is the step
///   cross-reference streams depend on - a PDF 1.5 file's object table is a PNG-predicted stream,
///   so a reader that gets this wrong cannot find any object in the file.
///   <para>
///   The predictors are driven from the stream's <c>/DecodeParms</c> dictionary, which is what
///   these tests build, and the arithmetic is checked by predicting data and unpredicting it back.
///   </para>
/// </summary>
public class PredictorTests
{
    static PdfDictionary Parms(int predictor, int colors, int bitsPerComponent, int columns)
    {
        var parms = new PdfDictionary(new PdfDocument());
        if (predictor != 0) parms.Elements.SetInteger("/Predictor", predictor);
        if (colors != 0) parms.Elements.SetInteger("/Colors", colors);
        if (bitsPerComponent != 0) parms.Elements.SetInteger("/BitsPerComponent", bitsPerComponent);
        if (columns != 0) parms.Elements.SetInteger("/Columns", columns);
        return parms;
    }

    /// <summary>
    ///   Applies one PNG filter type to every row of <paramref name="rows"/>, producing the shape a
    ///   predicted stream has: each row preceded by the byte that says how it was filtered.
    /// </summary>
    static byte[] Predict(byte filterType, int bpp, params byte[][] rows) =>
        Predict(_ => filterType, bpp, rows);

    /// <summary>
    ///   The same, with the filter type chosen per row - which is what a real encoder does, and
    ///   the reason each row carries its own filter byte.
    /// </summary>
    static byte[] Predict(Func<int, byte> filterTypeOfRow, int bpp, params byte[][] rows)
    {
        var stride = rows[0].Length;
        var output = new byte[rows.Length * (stride + 1)];
        var previous = new byte[stride];
        var pos = 0;

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var filterType = filterTypeOfRow(rowIndex);
            output[pos++] = filterType;
            for (var idx = 0; idx < stride; idx++)
            {
                var left = idx < bpp ? 0 : row[idx - bpp];
                var above = previous[idx];
                var aboveLeft = idx < bpp ? 0 : previous[idx - bpp];
                var predicted = filterType switch
                {
                    0 => 0,
                    1 => left,
                    2 => above,
                    3 => (left + above) / 2,
                    4 => Paeth(left, above, aboveLeft),
                    _ => throw new ArgumentOutOfRangeException(nameof(filterType)),
                };
                output[pos++] = (byte)(row[idx] - predicted);
            }
            previous = row;
        }
        return output;
    }

    static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;
        return pb <= pc ? b : c;
    }

    static readonly byte[][] Rows =
    {
        new byte[] { 10, 20, 30, 40, 50, 60 },
        new byte[] { 12, 24, 36, 48, 60, 72 },
        new byte[] { 200, 190, 180, 170, 160, 150 },
        new byte[] { 0, 255, 0, 255, 0, 255 },
    };

    static byte[] Flat => Rows.SelectMany(row => row).ToArray();

    /// <summary>
    ///   Runs data that has been predicted back through the decoder the way a stream's filter
    ///   would, and hands back what came out.
    /// </summary>
    static byte[] Unpredict(byte[] predicted, PdfDictionary parms)
    {
        // Through the flate filter, because that is the only way in from outside the library:
        // StreamDecoder is internal, and the predictor is applied to a filter's output.
        return Filtering.FlateDecode.Decode(
            Filtering.FlateDecode.Encode(predicted), new FilterParms(parms));
    }

    // ----- the PNG predictors --------------------------------------------------------------------

    [Theory]
    [InlineData(0)]   // None
    [InlineData(1)]   // Sub
    [InlineData(2)]   // Up
    [InlineData(3)]   // Average
    [InlineData(4)]   // Paeth
    public void EveryPngFilterTypeUndoesToTheDataItWasAppliedTo(byte filterType)
    {
        // One byte per component and one component per column, so a pixel is a byte and the
        // left-hand neighbour is the byte before.
        var predicted = Predict(filterType, 1, Rows);

        Unpredict(predicted, Parms(12, 1, 8, 6)).Should().Equal(Flat);
    }

    [Fact]
    public void EveryPngFilterTypeMayBeUsedOnADifferentRowOfTheSameStream()
    {
        // Which is the point of the per-row filter byte: an encoder picks whichever predicts that
        // row best, so one stream normally holds several. All five predictor numbers from 10 to 15
        // mean the same thing here - the encoder chose per row, so the number only says PNG.
        var predicted = Predict(row => (byte)(row % 5), 1, Rows);

        Unpredict(predicted, Parms(15, 1, 8, Rows[0].Length)).Should().Equal(Flat);
    }

    [Fact]
    public void ColoursWidenThePixelSoTheLeftNeighbourIsAWholePixelBack()
    {
        // Three colours at eight bits each is three bytes to a pixel, so Sub subtracts the byte
        // three back rather than the byte before - the same channel of the previous pixel.
        var rows = new[]
        {
            new byte[] { 10, 20, 30, 11, 21, 31, 12, 22, 32 },
            new byte[] { 40, 50, 60, 41, 51, 61, 42, 52, 62 },
        };
        var predicted = Predict(1, 3, rows);

        Unpredict(predicted, Parms(12, 3, 8, 3))
            .Should().Equal(rows.SelectMany(row => row).ToArray());
    }

    [Fact]
    public void APredictedStreamIsAsWideAsItsParametersSay()
    {
        // stride = ceiling(bpc x colours x columns / 8). Four columns of one bit is half a byte,
        // which rounds up to one, so each row is one byte and a filter byte.
        var predicted = new byte[] { 0, 0xA0, 0, 0x50 };

        Unpredict(predicted, Parms(12, 1, 1, 4)).Should().Equal(new byte[] { 0xA0, 0x50 });
    }

    // ----- the predictors that are not PNG -------------------------------------------------------

    [Fact]
    public void APredictorOfOneMeansTheDataWasNotPredictedAtAll()
    {
        var data = new byte[] { 1, 2, 3, 4 };

        Unpredict(data, Parms(1, 0, 0, 0)).Should().Equal(data);
    }

    [Fact]
    public void NoParametersAtAllMeansTheDataWasNotPredictedEither()
    {
        var data = new byte[] { 1, 2, 3, 4 };

        Filtering.FlateDecode.Decode(Filtering.FlateDecode.Encode(data), new FilterParms(null))
            .Should().Equal(data);
    }

    [Fact]
    public void ParametersThatSayNothingFallBackToTheDefaultsTheReferenceGives()
    {
        // Predictor 1, one colour, eight bits, one column - and predictor 1 is no prediction, so
        // an empty parameter dictionary leaves the data alone.
        var data = new byte[] { 1, 2, 3, 4 };

        Unpredict(data, Parms(0, 0, 0, 0)).Should().Equal(data);
    }

    [Fact]
    public void TheTiffPredictorIsNotImplementedAndSaysSo()
    {
        var act = () => Unpredict(new byte[] { 1, 2, 3, 4 }, Parms(2, 1, 8, 4));

        act.Should().Throw<NotImplementedException>().WithMessage("*TIFF*");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(16)]
    [InlineData(100)]
    public void APredictorThatIsNeitherOneNorTiffNorPngIsRefused(int predictor)
    {
        var act = () => Unpredict(new byte[] { 1, 2, 3, 4 }, Parms(predictor, 1, 8, 4));

        act.Should().Throw<PdfReaderException>().WithMessage("*predictor*");
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(32)]
    public void AComponentSizeThatIsNotAPowerOfTwoBitsIsRefused(int bitsPerComponent)
    {
        // One, two, four, eight and sixteen are the only sizes the reference allows.
        var act = () => Unpredict(new byte[] { 1, 2, 3, 4 }, Parms(12, 1, bitsPerComponent, 4));

        act.Should().Throw<PdfReaderException>().WithMessage("*bits per component*");
    }

    [Fact]
    public void ARowFilteredBySomethingThatIsNotAPngFilterIsRefused()
    {
        // The filter byte at the head of each row must be 0 to 4. Anything else is a stream that
        // was not PNG-predicted, whatever its parameters claimed.
        var predicted = new byte[] { 5, 1, 2, 3, 4 };

        var act = () => Unpredict(predicted, Parms(12, 1, 8, 4));

        act.Should().Throw<PdfReaderException>().WithMessage("*Png-Predictor*");
    }
}
