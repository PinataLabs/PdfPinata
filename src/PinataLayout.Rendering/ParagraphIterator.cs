#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using PinataLayout.DocumentObjectModel;
using System.Collections;

namespace PinataLayout.Rendering;

/// <summary>
/// Iterates sequentially through the elements of a paragraph.
/// </summary>
internal class ParagraphIterator
{
  /// <summary>
  /// Initializes a paragraph iterator pointing on the given paragraph elements object.
  /// Paragraph iterators received from this paragraph iterator relate to this root node.
  /// </summary>
  /// <param name="rootNode">The root node for the paragraph iterator.</param>
  internal ParagraphIterator(ParagraphElements rootNode)
  {
    _rootNode = rootNode;
    _current = rootNode;
    _positionIndices = new ArrayList();
  }

  /// <summary>
  /// Initializes a paragraph iterator given the root node, its position in the object tree and the current object
  /// </summary>
  /// <param name="rootNode">The node the position indices relate to.</param>
  /// <param name="current">The element the iterator shall point to.</param>
  /// <param name="indices">The position of the paragraph iterator in terms of element indices.</param>
  private ParagraphIterator(ParagraphElements rootNode, DocumentObject current, ArrayList indices)
  {
    _rootNode = rootNode;
    _positionIndices = indices;
    _current = current;
  }

  /// <summary>
  /// Determines whether this iterator is the first leaf of the root node.
  /// </summary>
  internal bool IsFirstLeaf
  {
    get
    {
      if (_current is DocumentElements)
        return false;

      var prevIter = GetPreviousLeaf();
      return prevIter == null;
    }
  }

  /// <summary>
  /// Determines whether this iterator is the last leaf of the document object tree.
  /// </summary>
  internal bool IsLastLeaf
  {
    get
    {
      if (_current is DocumentElements)
        return false;

      var nextIter = GetNextLeaf();
      return nextIter == null;
    }
  }

  /// <summary>
  /// Gets the document object this instance ponits to.
  /// </summary>
  internal DocumentObject Current => _current;

  /// <summary>
  /// Gets the last leaf of the document object tree.
  /// </summary>
  /// <returns>The paragraph iterator pointing to the last leaf in the document object tree.</returns>
  internal ParagraphIterator GetLastLeaf()
  {
    return _rootNode.Count == 0 ? null : SeekLastLeaf();
  }


  /// <summary>
  /// Gets the first leaf of the element tree.
  /// </summary>
  /// <returns>The paragraph iterator pointing to the first leaf in the element tree.</returns>
  internal ParagraphIterator GetFirstLeaf()
  {
    return _rootNode.Count == 0 ? null : SeekFirstLeaf();
  }

  /// <summary>
  /// Returns the next iterator in the tree pointing to a leaf.
  /// </summary>
  /// <remarks>This function is intended to receive the renderable objects of a paragraph.
  /// Thus, empty ParagraphElement objects (which are collections) don't count as leafs.</remarks>
  internal ParagraphIterator GetNextLeaf()
  {
    //Move up to appropriate parent element
    var parIterator = GetParentIterator();
    if (parIterator == null)
      return null;

    var elementIndex = LastIndex;
    var parEls = (ParagraphElements)parIterator._current;
    while (elementIndex == parEls.Count - 1)
    {
      elementIndex = parIterator.LastIndex;
      parIterator = parIterator.GetParentIterator();
      if (parIterator == null)
        break;

      parEls = (ParagraphElements)parIterator._current;
    }
    if (parIterator == null)
      return null;
    var newIndex = elementIndex + 1;
    if (newIndex >= parEls.Count)
      return null;

    var indices = (ArrayList)parIterator._positionIndices.Clone();
    indices.Add(newIndex);
    var obj = GetNodeObject(parEls[newIndex]);
    var iterator = new ParagraphIterator(_rootNode, obj, indices);
    return iterator.SeekFirstLeaf();
  }

  /// <summary>
  /// Gets the object a paragraph iterator shall point to.
  /// Only ParagraphElements and renderable objects are allowed.
  /// </summary>
  /// <param name="obj">The object to select the node object for.</param>
  /// <returns>The object a paragraph iterator shall point to.</returns>
  private static DocumentObject GetNodeObject(DocumentObject obj)
  {
    if (obj is FormattedText text)
      return text.Elements;
    if (obj is Hyperlink hyperlink)
      return hyperlink.Elements;
    return obj;
  }

  /// <summary>
  /// Returns the previous iterator to a leaf in the document object tree pointing.
  /// </summary>
  /// <returns>The previous leaf, null if none exists.</returns>
  internal ParagraphIterator GetPreviousLeaf()
  {
    //Move up to appropriate parent element
    var parIterator = GetParentIterator();
    if (parIterator == null)
      return null;

    var elementIndex = LastIndex;
    var parEls = (ParagraphElements)parIterator._current;
    while (elementIndex == 0)
    {
      elementIndex = parIterator.LastIndex;
      parIterator = parIterator.GetParentIterator();
      if (parIterator == null)
        break;

      parEls = (ParagraphElements)parIterator._current;
    }
    if (parIterator == null)
      return null;

    var newIndex = elementIndex - 1;
    if (newIndex < 0)
      return null;

    var indices = (ArrayList)parIterator._positionIndices.Clone();
    indices.Add(newIndex);

    var obj = GetNodeObject(parEls[newIndex]);
    var iterator = new ParagraphIterator(_rootNode, obj, indices);
    return iterator.SeekLastLeaf();
  }

  private ParagraphIterator SeekLastLeaf()
  {
    var obj = Current;
    if (obj is not ParagraphElements)
      return this;

    var indices = (ArrayList)_positionIndices.Clone();

    while (obj is ParagraphElements parEls)
    {
      if (parEls.Count == 0)
        return new ParagraphIterator(_rootNode, parEls, indices);

      var idx = parEls.Count - 1;
      indices.Add(idx);
      obj = GetNodeObject(parEls[idx]);
    }
    return new ParagraphIterator(_rootNode, obj, indices);
  }

  /// <summary>
  /// Gets the leftmost leaf within the hierarchy.
  /// </summary>
  /// <returns>The searched leaf.</returns>
  private ParagraphIterator SeekFirstLeaf()
  {
    var obj = Current;
    if (obj is not ParagraphElements)
      return this;
    var indices = (ArrayList)_positionIndices.Clone();

    while (obj is ParagraphElements parEls)
    {
      if (parEls.Count == 0)
        return new ParagraphIterator(_rootNode, parEls, indices);

      indices.Add(0);
      obj = GetNodeObject(parEls[0]);
    }
    return new ParagraphIterator(_rootNode, obj, indices);
  }

  private ParagraphIterator GetParentIterator()
  {
    if (_positionIndices.Count == 0)
      return null;

    var indices = (ArrayList)_positionIndices.Clone();
    indices.RemoveAt(indices.Count - 1);
    var parent = DocumentRelations.GetParentOfType(_current, typeof(ParagraphElements));
    return new ParagraphIterator(_rootNode, parent, indices);
  }

  private int LastIndex
  {
    get
    {
      if (_positionIndices.Count == 0)
        return -1;
      return (int)_positionIndices[^1]!;
    }
  }

  private readonly ParagraphElements _rootNode;
  private readonly ArrayList _positionIndices;
  private readonly DocumentObject _current;
}
