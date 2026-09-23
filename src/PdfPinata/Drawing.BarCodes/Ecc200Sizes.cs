namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// One of the symbol sizes an ecc200 DataMatrix may take.
/// </summary>
internal struct Ecc200Block
{
    /// <summary>The height of the whole symbol in modules, finder patterns included.</summary>
    public int Height;

    /// <summary>The width of the whole symbol in modules, finder patterns included.</summary>
    public int Width;

    /// <summary>The height of one data region, its own finder pattern included.</summary>
    public int CellHeight;

    /// <summary>The width of one data region, its own finder pattern included.</summary>
    public int CellWidth;

    /// <summary>The number of data codewords the symbol holds.</summary>
    public int Bytes;

    /// <summary>The number of data codewords in one interleaved block.</summary>
    public int DataBlock;

    /// <summary>The number of error correction codewords in one interleaved block.</summary>
    // ReSharper disable once InconsistentNaming
    public int RSBlock;

    public Ecc200Block(int h, int w, int ch, int cw, int bytes, int datablock, int rsblock)
    {
        Height = h;
        Width = w;
        CellHeight = ch;
        CellWidth = cw;
        Bytes = bytes;
        DataBlock = datablock;
        RSBlock = rsblock;
    }
}

/// <summary>
/// The symbol sizes of ecc200, smallest first: the twenty-four square ones and the six
/// rectangular ones.
/// </summary>
internal static class Ecc200Sizes
{
    internal static readonly Ecc200Block[] All =
    [
        new( 10,  10, 10, 10,    3,   3,  5),
        new( 12,  12, 12, 12,    5,   5,  7),
        new(  8,  18,  8, 18,    5,   5,  7),
        new( 14,  14, 14, 14,    8,   8, 10),
        new(  8,  32,  8, 16,   10,  10, 11),
        new( 16,  16, 16, 16,   12,  12, 12),
        new( 12,  26, 12, 26,   16,  16, 14),
        new( 18,  18, 18, 18,   18,  18, 14),
        new( 20,  20, 20, 20,   22,  22, 18),
        new( 12,  36, 12, 18,   22,  22, 18),
        new( 22,  22, 22, 22,   30,  30, 20),
        new( 16,  36, 16, 18,   32,  32, 24),
        new( 24,  24, 24, 24,   36,  36, 24),
        new( 26,  26, 26, 26,   44,  44, 28),
        new( 16,  48, 16, 24,   49,  49, 28),
        new( 32,  32, 16, 16,   62,  62, 36),
        new( 36,  36, 18, 18,   86,  86, 42),
        new( 40,  40, 20, 20,  114, 114, 48),
        new( 44,  44, 22, 22,  144, 144, 56),
        new( 48,  48, 24, 24,  174, 174, 68),
        new( 52,  52, 26, 26,  204, 102, 42),
        new( 64,  64, 16, 16,  280, 140, 56),
        new( 72,  72, 18, 18,  368,  92, 36),
        new( 80,  80, 20, 20,  456, 114, 48),
        new( 88,  88, 22, 22,  576, 144, 56),
        new( 96,  96, 24, 24,  696, 174, 68),
        new(104, 104, 26, 26,  816, 136, 56),
        new(120, 120, 20, 20, 1050, 175, 68),
        new(132, 132, 22, 22, 1304, 163, 62),
        new(144, 144, 24, 24, 1558, 156, 62),
        new(  0,   0,  0,  0,    0,    0, 0)     // terminate
    ];
}
