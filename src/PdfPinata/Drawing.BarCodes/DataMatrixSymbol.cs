using System;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Builds the modules of an ecc200 DataMatrix symbol: the dark and light squares a reader
/// sees, laid out as ISO/IEC 16022 lays them out.
/// <para>
/// The work is in four steps. The text becomes data codewords; those are split into blocks
/// and each block gains its error correction codewords; the codewords are interleaved and
/// their bits are walked into the symbol along the diagonal path the standard describes; and
/// the result is broken into data regions, each of which is given its finder pattern.
/// </para>
/// </summary>
internal static class DataMatrixSymbol
{
    /// <summary>
    /// The modules of the symbol, indexed by row then column, true where the module is dark.
    /// Row zero is the top of the symbol.
    /// </summary>
    internal static bool[,] Build(string text, string encoding, int rows, int columns)
    {
        var size = SizeOf(rows, columns);

        var data = DataMatrixEncoder.Encode(text ?? "", encoding, size.Bytes);
        var codewords = AddErrorCorrection(data, size);

        return Assemble(codewords, size);
    }

    /// <summary>
    /// The smallest symbol the text fits in, as rows by columns.
    /// </summary>
    internal static void SmallestSizeFor(string text, string encoding, out int rows, out int columns)
    {
        var needed = DataMatrixEncoder.CountCodewords(text ?? "", encoding);

        foreach (var candidate in Ecc200Sizes.All)
        {
            if (candidate.Height == 0)
                break;

            // Square symbols only, the shape a caller gets when it does not ask for one.
            if (candidate.Height != candidate.Width || candidate.Bytes < needed)
                continue;

            rows = candidate.Height;
            columns = candidate.Width;
            return;
        }

        throw new InvalidOperationException(BcgSR.DataMatrixTooBig);
    }

    private static Ecc200Block SizeOf(int rows, int columns)
    {
        foreach (var candidate in Ecc200Sizes.All)
        {
            if (candidate.Height == rows && candidate.Width == columns)
                return candidate;
        }

        throw new InvalidOperationException(BcgSR.DataMatrixInvalid(columns, rows));
    }

    #region Error correction

    /// <summary>
    /// Appends the error correction to the data. Both are interleaved across the blocks the
    /// symbol is divided into, so that damage to one part of it is spread thinly over all of
    /// them rather than falling wholly on one.
    /// </summary>
    private static byte[] AddErrorCorrection(byte[] data, Ecc200Block size)
    {
        var blocks = (data.Length + size.DataBlock - 1) / size.DataBlock;
        var codewords = new byte[data.Length + blocks * size.RSBlock];

        Array.Copy(data, codewords, data.Length);

        for (var block = 0; block < blocks; block++)
        {
            // The codewords of a block are every blocks'th one, taken from the start.
            var length = 0;
            for (var at = block; at < data.Length; at += blocks)
                length++;

            var blockData = new byte[length];
            var written = 0;
            for (var at = block; at < data.Length; at += blocks)
                blockData[written++] = data[at];

            var correction = DataMatrixReedSolomon.Compute(blockData, size.RSBlock);

            for (var at = 0; at < size.RSBlock; at++)
                codewords[data.Length + at * blocks + block] = correction[at];
        }

        return codewords;
    }

    #endregion

    #region Placement

    /// <summary>
    /// Walks the bits of the codewords into the symbol and gives each data region its finder
    /// pattern.
    /// </summary>
    private static bool[,] Assemble(byte[] codewords, Ecc200Block size)
    {
        var regionsDown = size.Height / size.CellHeight;
        var regionsAcross = size.Width / size.CellWidth;

        // The data regions with their finder patterns taken off, laid side by side.
        var height = size.Height - 2 * regionsDown;
        var width = size.Width - 2 * regionsAcross;

        var placement = new Placement(height, width);
        placement.Run();

        var modules = new bool[size.Height, size.Width];

        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var dark = placement.IsDark(row, column, codewords);

                // Back into the region it belongs to, past the finder pattern around it.
                var regionRow = row / (size.CellHeight - 2);
                var regionColumn = column / (size.CellWidth - 2);
                var y = regionRow * size.CellHeight + 1 + row % (size.CellHeight - 2);
                var x = regionColumn * size.CellWidth + 1 + column % (size.CellWidth - 2);

                modules[y, x] = dark;
            }
        }

        DrawFinderPatterns(modules, size, regionsDown, regionsAcross);
        return modules;
    }

    /// <summary>
    /// Draws the border of each data region: two solid sides that tell a reader where the
    /// region is and which way round, and two of alternating modules that tell it how wide
    /// a module is.
    /// </summary>
    private static void DrawFinderPatterns(bool[,] modules, Ecc200Block size, int regionsDown, int regionsAcross)
    {
        for (var regionRow = 0; regionRow < regionsDown; regionRow++)
        {
            for (var regionColumn = 0; regionColumn < regionsAcross; regionColumn++)
            {
                var top = regionRow * size.CellHeight;
                var left = regionColumn * size.CellWidth;
                var bottom = top + size.CellHeight - 1;
                var right = left + size.CellWidth - 1;

                for (var y = top; y <= bottom; y++)
                {
                    modules[y, left] = true;                        // solid, down the left
                    modules[y, right] = (y - top) % 2 == 1;         // alternating, up the right
                }

                for (var x = left; x <= right; x++)
                {
                    modules[bottom, x] = true;                      // solid, along the bottom
                    modules[top, x] = (x - left) % 2 == 0;          // alternating, along the top
                }
            }
        }
    }

    /// <summary>
    /// Works out which bit of which codeword each module of the data regions carries. The
    /// path is the one ISO/IEC 16022 lays down: a shape of eight modules stepped diagonally
    /// up and to the right, then down and to the left, wrapping at the edges, with four
    /// corner cases where the shape will not fit.
    /// </summary>
    private sealed class Placement
    {
        internal Placement(int height, int width)
        {
            _height = height;
            _width = width;
            _codeword = new int[height * width];
            _bit = new int[height * width];
            _forcedDark = new bool[height * width];
            _filled = new bool[height * width];
        }

        private readonly int _height;
        private readonly int _width;
        private readonly int[] _codeword;
        private readonly int[] _bit;
        private readonly bool[] _forcedDark;
        private readonly bool[] _filled;

        internal bool IsDark(int row, int column, byte[] codewords)
        {
            var at = row * _width + column;
            if (_forcedDark[at])
                return true;

            if (!_filled[at])
                return false;

            var index = _codeword[at];
            if (index >= codewords.Length)
                return false;

            // Bit one is the most significant of the codeword.
            return (codewords[index] & (1 << (8 - _bit[at]))) != 0;
        }

        internal void Run()
        {
            var codeword = 0;
            var row = 4;
            var column = 0;

            do
            {
                PlaceCorners(row, column, ref codeword);

                SweepUpAndRight(ref row, ref column, ref codeword);
                row += 1;
                column += 3;

                SweepDownAndLeft(ref row, ref column, ref codeword);
                row += 3;
                column += 1;
            }
            while (row < _height || column < _width);

            FillUnusedCorner();
        }

        /// <summary>
        /// Places the codeword whose shape will not fit at a corner, where the path is about to
        /// start a sweep from one. At most one of the four applies at any one position.
        /// </summary>
        private void PlaceCorners(int row, int column, ref int codeword)
        {
            if (row == _height && column == 0)
                Corner1(codeword++);
            if (row == _height - 2 && column == 0 && _width % 4 != 0)
                Corner2(codeword++);
            if (row == _height - 2 && column == 0 && _width % 8 == 4)
                Corner3(codeword++);
            if (row == _height + 4 && column == 2 && _width % 8 == 0)
                Corner4(codeword++);
        }

        /// <summary>Steps diagonally up and to the right, placing a codeword wherever one fits.</summary>
        private void SweepUpAndRight(ref int row, ref int column, ref int codeword)
        {
            do
            {
                if (row < _height && column >= 0 && !_filled[row * _width + column])
                    Shape(row, column, codeword++);

                row -= 2;
                column += 2;
            }
            while (row >= 0 && column < _width);
        }

        /// <summary>Steps diagonally down and to the left, placing a codeword wherever one fits.</summary>
        private void SweepDownAndLeft(ref int row, ref int column, ref int codeword)
        {
            do
            {
                if (row >= 0 && column < _width && !_filled[row * _width + column])
                    Shape(row, column, codeword++);

                row += 2;
                column -= 2;
            }
            while (row < _height && column >= 0);
        }

        /// <summary>
        /// The two modules of the bottom right corner go unused by some symbol sizes, and carry a
        /// fixed pattern rather than nothing.
        /// </summary>
        private void FillUnusedCorner()
        {
            if (_filled[_height * _width - 1])
                return;

            _forcedDark[_height * _width - 1] = true;
            _forcedDark[_height * _width - _width - 2] = true;
            _filled[_height * _width - 1] = true;
            _filled[_height * _width - _width - 2] = true;
        }

        /// <summary>Places one bit, wrapping it round the symbol where it falls outside.</summary>
        private void Place(int row, int column, int codeword, int bit)
        {
            if (row < 0)
            {
                row += _height;
                column += 4 - (_height + 4) % 8;
            }

            if (column < 0)
            {
                column += _width;
                row += 4 - (_width + 4) % 8;
            }

            var at = row * _width + column;
            _codeword[at] = codeword;
            _bit[at] = bit;
            _filled[at] = true;
        }

        /// <summary>The eight modules of a codeword, in the shape they are usually written in.</summary>
        private void Shape(int row, int column, int codeword)
        {
            Place(row - 2, column - 2, codeword, 1);
            Place(row - 2, column - 1, codeword, 2);
            Place(row - 1, column - 2, codeword, 3);
            Place(row - 1, column - 1, codeword, 4);
            Place(row - 1, column, codeword, 5);
            Place(row, column - 2, codeword, 6);
            Place(row, column - 1, codeword, 7);
            Place(row, column, codeword, 8);
        }

        private void Corner1(int codeword)
        {
            Place(_height - 1, 0, codeword, 1);
            Place(_height - 1, 1, codeword, 2);
            Place(_height - 1, 2, codeword, 3);
            Place(0, _width - 2, codeword, 4);
            Place(0, _width - 1, codeword, 5);
            Place(1, _width - 1, codeword, 6);
            Place(2, _width - 1, codeword, 7);
            Place(3, _width - 1, codeword, 8);
        }

        private void Corner2(int codeword)
        {
            Place(_height - 3, 0, codeword, 1);
            Place(_height - 2, 0, codeword, 2);
            Place(_height - 1, 0, codeword, 3);
            Place(0, _width - 4, codeword, 4);
            Place(0, _width - 3, codeword, 5);
            Place(0, _width - 2, codeword, 6);
            Place(0, _width - 1, codeword, 7);
            Place(1, _width - 1, codeword, 8);
        }

        private void Corner3(int codeword)
        {
            Place(_height - 3, 0, codeword, 1);
            Place(_height - 2, 0, codeword, 2);
            Place(_height - 1, 0, codeword, 3);
            Place(0, _width - 2, codeword, 4);
            Place(0, _width - 1, codeword, 5);
            Place(1, _width - 1, codeword, 6);
            Place(2, _width - 1, codeword, 7);
            Place(3, _width - 1, codeword, 8);
        }

        private void Corner4(int codeword)
        {
            Place(_height - 1, 0, codeword, 1);
            Place(_height - 1, _width - 1, codeword, 2);
            Place(0, _width - 3, codeword, 3);
            Place(0, _width - 2, codeword, 4);
            Place(0, _width - 1, codeword, 5);
            Place(1, _width - 3, codeword, 6);
            Place(1, _width - 2, codeword, 7);
            Place(1, _width - 1, codeword, 8);
        }
    }

    #endregion
}
