#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using System.Collections.Generic;
using System.Diagnostics;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Deals with image file names, searches along the image path, checks if images exist etc.
/// </summary>
public class ImageHelper
{
  /// <summary>
  /// Gets the first existing image from the subfolders.
  /// </summary>
  public static string GetImageName(string root, string filename, string imagePath)
  {
    try
    {
      var subfolders = new List<string>(imagePath.Split(';', StringSplitOptions.RemoveEmptyEntries));
      subfolders.Add("");

      foreach (var subfolder in subfolders)
      {
        var fullname = System.IO.Path.Combine(System.IO.Path.Combine(root, subfolder), filename);
        var realFile = ExtractPageNumber(fullname, out _);

        if (System.IO.File.Exists(realFile))
          return fullname;
      }
    }
    catch (Exception ex) when (!Unrecoverable.Is(ex))
    {
      Debug.Assert(false, "Should never occur with properly formatted Wiki texts. " + ex);
    }
    return null;
  }

  /// <summary>
  /// Gets a value indicating whether the filename given in the referenceFilename exists in the subfolders.
  /// </summary>
  public static bool InSubfolder(string root, string filename, string imagePath, string referenceFilename)
  {
    var subfolders = new List<string>(imagePath.Split(';', StringSplitOptions.RemoveEmptyEntries));
    subfolders.Add("");

    foreach (var subfolder in subfolders)
    {
      var fullname = System.IO.Path.Combine(System.IO.Path.Combine(root, subfolder), filename);
      var realFile = ExtractPageNumber(fullname, out _);
      if (System.IO.File.Exists(realFile))
      {
        if (fullname == referenceFilename)
          return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Extracts the page number if the path has the form 'MyFile.pdf#123' and returns
  /// the actual path without the number sign and the following digits.
  /// </summary>
  public static string ExtractPageNumber(string path, out int pageNumber)
  {
    // Note: duplicated from class XPdfForm
    ArgumentNullException.ThrowIfNull(path);

    pageNumber = 0;
    var length = path.Length;
    if (length != 0)
    {
      length--;
      if (Char.IsDigit(path, length))
      {
        // Bound first: the old order asked whether path[-1] was a digit when every character
        // was one, and Char.IsDigit threw rather than the loop ending. Duplicated in XPdfForm.
        while (length >= 0 && Char.IsDigit(path, length))
          length--;
        if (length > 0 && path[length] == '#')
        {
          // must have at least one dot left of colon to distinguish from e.g. '#123'
          if (path.Contains('.'))
          {
            pageNumber = Int32.Parse(path[(length + 1)..]);
            path = path[..length];
          }
        }
      }
    }
    return path;
  }

  internal static string GetImageName(string filePath, object name, string imagePath)
  {
    throw new NotImplementedException();
  }
}
