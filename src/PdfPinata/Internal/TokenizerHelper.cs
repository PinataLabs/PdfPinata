#region Copyright

//
// Authors:
//   Microsoft
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
using System.Globalization;

namespace PdfPinata.Internal;

internal class TokenizerHelper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenizerHelper"/> class.
    /// </summary>
    public TokenizerHelper(string str, IFormatProvider formatProvider)
    {
        var numericListSeparator = GetNumericListSeparator(formatProvider);
        Initialize(str, '\'', numericListSeparator);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenizerHelper"/> class.
    /// </summary>
    public TokenizerHelper(string str, char quoteChar, char separator)
    {
        Initialize(str, quoteChar, separator);
    }

    private void Initialize(string str, char quoteChar, char separator)
    {
        _str = str;
        _strLen = str == null ? 0 : str.Length;
        _currentTokenIndex = -1;
        _quoteChar = quoteChar;
        _argSeparator = separator;

        // Skip any whitespace.
        while (_charIndex < _strLen)
        {
            if (!char.IsWhiteSpace(_str, _charIndex))
                return;
            _charIndex++;
        }
    }

    public string NextTokenRequired()
    {
        if (!NextToken(false))
            throw
                new InvalidOperationException(
                    "PrematureStringTermination"); //SR.Get(SRID.TokenizerHelperPrematureStringTermination, new object[0]));
        return GetCurrentToken();
    }

    public string NextTokenRequired(bool allowQuotedToken)
    {
        if (!NextToken(allowQuotedToken))
            throw
                new InvalidOperationException(
                    "PrematureStringTermination"); //SR.Get(SRID.TokenizerHelperPrematureStringTermination, new object[0]));
        return GetCurrentToken();
    }

    public string GetCurrentToken()
    {
        return _currentTokenIndex < 0 ? null : _str.Substring(_currentTokenIndex, _currentTokenLength);
    }

    public void LastTokenRequired()
    {
        if (_charIndex != _strLen)
            throw
                new InvalidOperationException(
                    "Extra data encountered"); //SR.Get(SRID.TokenizerHelperExtraDataEncountered, new object[0]));
    }

    /// <summary>
    /// Move to next token.
    /// </summary>
    public bool NextToken()
    {
        return NextToken(false);
    }

    /// <summary>
    /// Move to next token.
    /// </summary>
    public bool NextToken(bool allowQuotedToken)
    {
        return NextToken(allowQuotedToken, _argSeparator);
    }

    public bool NextToken(bool allowQuotedToken, char separator)
    {
        // Reset index.
        _currentTokenIndex = -1;
        _foundSeparator = false;

        // Already at the end of the string?
        if (_charIndex >= _strLen)
            return false;

        var currentChar = _str[_charIndex];

        // Setup the quoteCount .
        var quoteCount = 0;

        // If we are allowing a quoted token and this token begins with a quote,
        // set up the quote count and skip the initial quote
        if (allowQuotedToken &&
            currentChar == _quoteChar)
        {
            quoteCount++;
            _charIndex++;
        }

        var newTokenIndex = _charIndex;
        var newTokenLength = 0;

        // Loop until hit end of string or hit a separator or whitespace.
        while (_charIndex < _strLen)
        {
            currentChar = _str[_charIndex];

            // If you have a quoteCount and this is a quote  decrement the quoteCount.
            if (quoteCount > 0)
            {
                // If anything but a quoteChar we move on.
                if (currentChar == _quoteChar)
                {
                    quoteCount--;

                    // If at zero which it always should for now break out of the loop.
                    if (quoteCount == 0)
                    {
                        ++_charIndex;
                        break;
                    }
                }
            }
            else if ((char.IsWhiteSpace(currentChar)) || (currentChar == separator))
            {
                if (currentChar == separator)
                    _foundSeparator = true;
                break;
            }

            _charIndex++;
            newTokenLength++;
        }

        // If quoteCount isn't zero we hit the end of the string before the ending quote.
        if (quoteCount > 0)
            throw
                new InvalidOperationException(
                    "Missing end quote"); //SR.Get(SRID.TokenizerHelperMissingEndQuote, new object[0]));

        // Move at the start of the nextToken.
        ScanToNextToken(separator);

        // Update the _currentToken values.
        _currentTokenIndex = newTokenIndex;
        _currentTokenLength = newTokenLength;

        if (_currentTokenLength < 1)
            throw
                new InvalidOperationException("Empty token");

        return true;
    }

    private void ScanToNextToken(char separator)
    {
        // Do nothing if already at end of the string.
        if (_charIndex < _strLen)
        {
            var currentChar = _str[_charIndex];

            // Ensure that currentChar is a white space or separator.
            if (currentChar != separator && !char.IsWhiteSpace(currentChar))
                throw
                    new InvalidOperationException(
                        "ExtraDataEncountered");

            // Loop until a character that isn't the separator or white space.
            var argSepCount = 0;
            while (_charIndex < _strLen)
            {
                currentChar = _str[_charIndex];
                if (currentChar == separator)
                {
                    _foundSeparator = true;
                    argSepCount++;
                    _charIndex++;

                    if (argSepCount > 1)
                        throw
                            new InvalidOperationException("EmptyToken");
                }
                else if (char.IsWhiteSpace(currentChar))
                {
                    // Skip white space.
                    ++_charIndex;
                }
                else
                {
                    break;
                }
            }

            // If there was a separatorChar then we shouldn't be at the end of string or means there was a separator but there isn't an arg.
            if (argSepCount > 0 && _charIndex >= _strLen)
                throw
                    new InvalidOperationException("EmptyToken");
        }
    }

    public static char GetNumericListSeparator(IFormatProvider provider)
    {
        var numericSeparator = ',';
        var numberFormat = NumberFormatInfo.GetInstance(provider);
        if (numberFormat.NumberDecimalSeparator.Length > 0 &&
            numericSeparator == numberFormat.NumberDecimalSeparator[0])
            numericSeparator = ';';
        return numericSeparator;
    }

    public bool FoundSeparator => _foundSeparator;

    private bool _foundSeparator;

    private char _argSeparator;
    private int _charIndex;
    private int _currentTokenIndex;
    private int _currentTokenLength;
    private char _quoteChar;
    private string _str;
    private int _strLen;
}
