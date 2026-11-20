#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.IO;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the FlateDecode filter by wrapping SharpZipLib.
/// </summary>
public class FlateDecode : Filter
{
    // Reference: 3.3.3  LZWDecode and FlateDecode Filters / Page 71

    /// <summary>
    /// Encodes the specified data.
    /// </summary>
    public override byte[] Encode(byte[] data)
    {
        return Encode(data, PdfFlateEncodeMode.Default);
    }

    /// <summary>
    /// Encodes the specified data.
    /// </summary>
    #pragma warning disable CA1822 // Public API: making it static would break every caller that reaches it through an instance.
    public byte[] Encode(byte[] data, PdfFlateEncodeMode mode)
    {
        var ms = new MemoryStream();

        // DeflateStream/GZipStream does not work immediately and I have not the leisure to work it out.
        // So I keep on using SharpZipLib even with .NET 2.0.

        var level = mode switch
        {
            PdfFlateEncodeMode.BestCompression => Deflater.BEST_COMPRESSION,
            PdfFlateEncodeMode.BestSpeed => Deflater.BEST_SPEED,
            _ => Deflater.DEFAULT_COMPRESSION
        };
        var zip = new DeflaterOutputStream(ms, new Deflater(level, false));
        zip.Write(data, 0, data.Length);
        zip.Finish();
        return ms.ToArray();
    }
    #pragma warning restore CA1822

    /// <summary>
    /// Decodes the specified data.
    /// </summary>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        if (data.Length == 0) return data;

        var msInput = new MemoryStream(data);
        var msOutput = new MemoryStream();

        var limit = parms?.Limit;
        var cap = limit?.MaximumLength ?? long.MaxValue;
        var iis = new InflaterInputStream(msInput, new Inflater(false));
        int cbRead;
        var abResult = new byte[32768];
        var inputWasCut = limit is { Truncated: true };
        try
        {
            do
            {
                var want = (int)Math.Min(abResult.Length, cap - msOutput.Length);
                if (want == 0)
                {
                    // At the cap: one more byte read says whether the stream really ended here.
                    if (limit != null && iis.Read(abResult, 0, 1) > 0)
                        limit.Truncated = true;
                    break;
                }

                cbRead = iis.Read(abResult, 0, want);
                if (cbRead > 0)
                    msOutput.Write(abResult, 0, cbRead);
            }
            while (cbRead > 0);
        }
        catch (SharpZipBaseException) when (inputWasCut)
        {
            // An earlier filter of the chain was stopped at the cap, so this one was given the start
            // of its stream and ran out. What it inflated is the start of the content, and the limit
            // already says the content was cut. Without a limit this throws, as it always did.
        }

        iis.Close();
        msOutput.Flush();
        if (limit != null)
            limit.Produced += msOutput.Length;
        if (msOutput.Length < 0)
            return null;

        // No parameters at all is not an error: it is what DecodeToString passes, and it says
        // the same thing as parameters with no predictor in them.
        return parms?.DecodeParms != null
            ? StreamDecoder.Decode(msOutput.ToArray(), parms.DecodeParms, limit)
            : msOutput.ToArray();
    }
}
