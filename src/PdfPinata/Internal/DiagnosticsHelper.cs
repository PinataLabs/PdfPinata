#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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
using System.Diagnostics;

namespace PdfPinata.Internal;

/// <summary>
/// A bunch of internal helper functions.
/// </summary>
internal static class DiagnosticsHelper
{
    public static void HandleNotImplemented(string message)
    {
        var text = "Not implemented: " + message;
        switch (Diagnostics.NotImplementedBehaviour)
        {
            case NotImplementedBehaviour.DoNothing:
                break;

            case NotImplementedBehaviour.Log:
                Logger.Log(text);
                break;

            case NotImplementedBehaviour.Throw:
                ThrowNotImplementedException(text);
                break;

            default:
                #pragma warning disable CA2208 // What is out of range is the Diagnostics.NotImplementedBehaviour setting, not a parameter, so there is none to name.
                throw new ArgumentOutOfRangeException();
                #pragma warning restore CA2208
        }
    }

    /// <summary>
    /// Indirectly throws NotImplementedException.
    /// Required because PDFsharp Release builds tread warnings as errors and
    /// throwing NotImplementedException may lead to unreachable code which
    /// crashes the build.
    /// </summary>
    public static void ThrowNotImplementedException(string message)
    {
        throw new NotImplementedException(message);
    }
}

internal static class Logger
{
    public static void Log(string format, params object[] args)
    {
        // Both arguments used to be thrown away and the literal string "Log..." written instead,
        // so NotImplementedBehaviour.Log reported that something unimplemented had been reached
        // and never which thing.
        Debug.WriteLine(args.Length == 0 ? format : string.Format(format, args));
    }
}
