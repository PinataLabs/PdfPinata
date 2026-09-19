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
        Ecc200Block size = SizeOf(rows, columns);

        byte[] data = DataMatrixEncoder.Encode(text ?? "", encoding, size.Bytes);
        byte[] codewords = AddErrorCorrection(data, size);

        return Assemble(codewords, size);
    }

    /// <summary>
    /// The smallest symbol the text fits in, as rows by columns.
    /// </summary>
    internal static void SmallestSizeFor(string text, string encoding, out int rows, out int columns)
    {
        int needed = DataMatrixEncoder.CountCodewords(text ?? "", encoding);

        foreach (Ecc200Block candidate in Ecc200Sizes.All)
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

    static Ecc200Block SizeOf(int rows, int columns)
    {
        foreach (Ecc200Block candidate in Ecc200Sizes.All)
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
    static byte[] AddErrorCorrection(byte[] data, Ecc200Block size)
    {
        int blocks = (data.Length + size.DataBlock - 1) / size.DataBlock;
        byte[] codewords = new byte[data.Length + blocks * size.RSBlock];

        Array.Copy(data, codewords, data.Length);

        for (int block = 0; block < blocks; block++)
        {
            // The codewords of a block are every blocks'th one, taken from the start.
            int length = 0;
            for (int at = block; at < data.Length; at += blocks)
                length++;

            byte[] blockData = new byte[length];
            int written = 0;
            for (int at = block; at < data.Length; at += blocks)
                blockData[written++] = data[at];

            byte[] correction = DataMatrixReedSolomon.Compute(blockData, size.RSBlock);

            for (int at = 0; at < size.RSBlock; at++)
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
    static bool[,] Assemble(byte[] codewords, Ecc200Block size)
    {
        int regionsDown = size.Height / size.CellHeight;
        int regionsAcross = size.Width / size.CellWidth;

        // The data regions with their finder patterns taken off, laid side by side.
        int height = size.Height - 2 * regionsDown;
        int width = size.Width - 2 * regionsAcross;

        Placement placement = new Placement(height, width);
        placement.Run();

        bool[,] modules = new bool[size.Height, size.Width];

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                bool dark = placement.IsDark(row, column, codewords);

                // Back into the region it belongs to, past the finder pattern around it.
                int regionRow = row / (size.CellHeight - 2);
                int regionColumn = column / (size.CellWidth - 2);
                int y = regionRow * size.CellHeight + 1 + row % (size.CellHeight - 2);
                int x = regionColumn * size.CellWidth + 1 + column % (size.CellWidth - 2);

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
    static void DrawFinderPatterns(bool[,] modules, Ecc200Block size, int regionsDown, int regionsAcross)
    {
        for (int regionRow = 0; regionRow < regionsDown; regionRow++)
        {
            for (int regionColumn = 0; regionColumn < regionsAcross; regionColumn++)
            {
                int top = regionRow * size.CellHeight;
                int left = regionColumn * size.CellWidth;
                int bottom = top + size.CellHeight - 1;
                int right = left + size.CellWidth - 1;

                for (int y = top; y <= bottom; y++)
                {
                    modules[y, left] = true;                        // solid, down the left
                    modules[y, right] = (y - top) % 2 == 1;         // alternating, up the right
                }

                for (int x = left; x <= right; x++)
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
    sealed class Placement
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

        readonly int _height;
        readonly int _width;
        readonly int[] _codeword;
        readonly int[] _bit;
        readonly bool[] _forcedDark;
        readonly bool[] _filled;

        internal bool IsDark(int row, int column, byte[] codewords)
        {
            int at = row * _width + column;
            if (_forcedDark[at])
                return true;

            if (!_filled[at])
                return false;

            int index = _codeword[at];
            if (index >= codewords.Length)
                return false;

            // Bit one is the most significant of the codeword.
            return (codewords[index] & (1 << (8 - _bit[at]))) != 0;
        }

        internal void Run()
        {
            int codeword = 0;
            int row = 4;
            int column = 0;

            do
            {
                if (row == _height && column == 0)
                    Corner1(codeword++);
                if (row == _height - 2 && column == 0 && _width % 4 != 0)
                    Corner2(codeword++);
                if (row == _height - 2 && column == 0 && _width % 8 == 4)
                    Corner3(codeword++);
                if (row == _height + 4 && column == 2 && _width % 8 == 0)
                    Corner4(codeword++);

                // Up and to the right.
                do
                {
                    if (row < _height && column >= 0 && !_filled[row * _width + column])
                        Shape(row, column, codeword++);

                    row -= 2;
                    column += 2;
                }
                while (row >= 0 && column < _width);

                row += 1;
                column += 3;

                // Down and to the left.
                do
                {
                    if (row >= 0 && column < _width && !_filled[row * _width + column])
                        Shape(row, column, codeword++);

                    row += 2;
                    column -= 2;
                }
                while (row < _height && column >= 0);

                row += 3;
                column += 1;
            }
            while (row < _height || column < _width);

            // The two modules of the bottom right corner go unused by some symbol sizes, and
            // carry a fixed pattern rather than nothing.
            if (!_filled[_height * _width - 1])
            {
                _forcedDark[_height * _width - 1] = true;
                _forcedDark[_height * _width - _width - 2] = true;
                _filled[_height * _width - 1] = true;
                _filled[_height * _width - _width - 2] = true;
            }
        }

        /// <summary>Places one bit, wrapping it round the symbol where it falls outside.</summary>
        void Place(int row, int column, int codeword, int bit)
        {
            if (row < 0)
            {
                row += _height;
                column += 4 - ((_height + 4) % 8);
            }

            if (column < 0)
            {
                column += _width;
                row += 4 - ((_width + 4) % 8);
            }

            int at = row * _width + column;
            _codeword[at] = codeword;
            _bit[at] = bit;
            _filled[at] = true;
        }

        /// <summary>The eight modules of a codeword, in the shape they are usually written in.</summary>
        void Shape(int row, int column, int codeword)
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

        void Corner1(int codeword)
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

        void Corner2(int codeword)
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

        void Corner3(int codeword)
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

        void Corner4(int codeword)
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
