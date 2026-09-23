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

using System.Collections;
using System.Collections.Generic;

namespace PinataLayout.DocumentObjectModel.Visitors;

/// <summary>
/// Flattens a document for PDF rendering.
/// </summary>
public class PdfFlattenVisitor : VisitorBase
{
  /// <summary>
  /// Initializes a new instance of the PdfFlattenVisitor class.
  /// </summary>
  public PdfFlattenVisitor()
  {
  }

  internal override void VisitDocumentElements(DocumentElements elements)
  {
    var splitParaList = new SortedList();

    for (var idx = 0; idx < elements.Count; ++idx)
    {
      if (elements[idx] is not Paragraph paragraph)
        continue;

      var paragraphs = paragraph.SplitOnParaBreak();
      if (paragraphs != null)
        splitParaList.Add(idx, paragraphs);
    }

    var insertedObjects = 0;
    for (var idx = 0; idx < splitParaList.Count; ++idx)
    {
      var insertPosition = (int)splitParaList.GetKey(idx);
      var paragraphs = (Paragraph[])splitParaList.GetByIndex(idx);
      // ReSharper disable once PossibleNullReferenceException
      foreach (var paragraph in paragraphs)
      {
        elements.InsertObject(insertPosition + insertedObjects, paragraph);
        ++insertedObjects;
      }
      elements.RemoveObjectAt(insertPosition + insertedObjects);
      --insertedObjects;
    }
  }

  internal override void VisitDocumentObjectCollection(DocumentObjectCollection elements)
  {
    // Found before any is split, so each position is moved on by what the texts before it grew by.
    var insertedObjects = 0;
    foreach (var idx in TextIndices(elements))
      insertedObjects += SplitIntoWords(elements, idx + insertedObjects);
  }

  /// <summary>
  /// The positions of the texts among a paragraph's elements; none for any other collection.
  /// </summary>
  private static List<int> TextIndices(DocumentObjectCollection elements)
  {
    var indices = new List<int>();
    if (elements is not ParagraphElements)
      return indices;

    for (var idx = 0; idx < elements.Count; ++idx)
    {
      if (elements[idx] is Text)
        indices.Add(idx);
    }
    return indices;
  }

  /// <summary>
  /// Replaces the text at <paramref name="position"/> with one text per word: every whitespace
  /// character becomes a space of its own, a word ends after a hyphen or a zero-width space, and a
  /// soft hyphen stands alone. Answers how many elements the collection grew by.
  /// </summary>
  private static int SplitIntoWords(DocumentObjectCollection elements, int position)
  {
    var text = (Text)elements[position];
    var inserted = 0;
    var currentString = "";

    foreach (var ch in text.Content)
    {
      switch (ch)
      {
        case ' ':
        case '\r':
        case '\n':
        case '\t':
          InsertWordSoFar();
          Insert(" ");
          break;

        case Chars.ZeroWidthSpace:
        case '-': //minus
          Insert(currentString + ch);
          currentString = "";
          break;

        case Chars.SoftHyphen: //soft hyphen
          InsertWordSoFar();
          Insert(new string(Chars.SoftHyphen, 1));
          break;

        default:
          currentString += ch;
          break;
      }
    }
    InsertWordSoFar();

    elements.RemoveObjectAt(position + inserted);
    return inserted - 1;

    void Insert(string content)
    {
      elements.InsertObject(position + inserted, new Text(content));
      ++inserted;
    }

    void InsertWordSoFar()
    {
      if (currentString == "")
        return;

      Insert(currentString);
      currentString = "";
    }
  }

  internal override void VisitFormattedText(FormattedText formattedText)
  {
    var document = formattedText.Document;
    ParagraphFormat format = null;

    var style = document.styles[formattedText.style ?? ""];
    if (style != null)
      format = style.paragraphFormat;
    else if ((formattedText.style ?? "") != "")
      format = document.styles["InvalidStyleName"].paragraphFormat;

    if (format != null)
    {
      if (formattedText.font == null)
        formattedText.Font = format.font.Clone();
      else if (format.font != null)
        FlattenFont(formattedText.font, format.font);
    }

    var parentFont = GetParentFont(formattedText);

    if (formattedText.font == null)
      formattedText.Font = parentFont.Clone();
    else if (parentFont != null)
      FlattenFont(formattedText.font, parentFont);
  }

  internal override void VisitHyperlink(Hyperlink hyperlink)
  {
    var styleFont = hyperlink.Document.Styles["Hyperlink"].Font;
    if (hyperlink.font == null)
      hyperlink.Font = styleFont.Clone();
    else
      FlattenFont(hyperlink.font, styleFont);

    FlattenFont(hyperlink.font, GetParentFont(hyperlink));
  }

  #pragma warning disable CA1822 // Protected on an unsealed public visitor: making it static would change the public API.
  /// <summary>Returns the font the given object inherits from whatever holds it.</summary>
  protected Font GetParentFont(DocumentObject obj)
  {
    var parentElements = DocumentRelations.GetParent(obj);
    var parentObject = DocumentRelations.GetParent(parentElements);
    Font parentFont;
    if (parentObject is Paragraph paragraph)
    {
      var format = paragraph.Format;
      parentFont = format.font;
    }
    else //Hyperlink or FormattedText
    {
      parentFont = parentObject.GetValue("Font") as Font;
    }
    return parentFont;
  }
  #pragma warning restore CA1822
}
