using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfPinata.Pdf.Security;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Replaces images with identical content by a single shared XObject, so that a document which
/// drew the same picture on many pages carries one copy of it.
/// <para>
/// Merging a document out of many files is what makes this worth doing: each file brought its own
/// copy of a logo or a photograph, and nothing in the merge notices that the bytes are the same.
/// Images are matched by the MD5 of their stream, so only byte-identical ones are merged - two
/// pictures that merely look alike are left alone.
/// </para>
/// <para>
/// This needs to see every page's images at once rather than one page at a time, which is why it
/// takes the whole set of pages where <see cref="PdfResourcePruner"/> beside it takes one page.
/// </para>
/// </summary>
internal sealed class PdfImageConsolidator
{
    /// <summary>
    /// Points every reference to an image at one XObject per distinct set of image bytes.
    /// </summary>
    /// <param name="pages">The pages whose resource dictionaries are to be merged.</param>
    internal static void Consolidate(IEnumerable<PdfPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var images = ImageInfo.FindAll(pages);

        var mapHashcodeToMd5 = new Dictionary<int, string>();
        var mapMd5ToPdfItem = new Dictionary<string, PdfItem>();

        // Calculate MD5 for each image XObject and build lookups for all images.
        foreach (var img in images)
        {
            mapHashcodeToMd5[img.XObject.GetHashCode()] = img.XObjectMD5;
            mapMd5ToPdfItem[img.XObjectMD5] = img.Item.Value;
        }

        // Set the PdfItem for each image to the one chosen for the MD5.
        foreach (var img in images)
        {
            var md5 = mapHashcodeToMd5[img.XObject.GetHashCode()];
            img.XObjects.Elements[img.Item.Key] = mapMd5ToPdfItem[md5];
        }
    }

    /// <summary>
    /// One image XObject as it is named by one page: the dictionary that names it, the entry
    /// naming it, the XObject itself, and the hash of its bytes.
    /// </summary>
    sealed class ImageInfo
    {
        public PdfDictionary XObjects { get; }
        public KeyValuePair<string, PdfItem> Item { get; }
        public PdfDictionary XObject { get; }
        public string XObjectMD5 { get; }

        static readonly MD5Managed Hasher = new();

        ImageInfo(PdfDictionary xObjects, KeyValuePair<string, PdfItem> item, PdfDictionary xObject)
        {
            XObjects = xObjects;
            Item = item;
            XObject = xObject;
            XObjectMD5 = ComputeMD5(xObject.Stream.Value);
        }

        /// <summary>
        /// Get info for each image named by the resources of any of the pages.
        /// </summary>
        internal static List<ImageInfo> FindAll(IEnumerable<PdfPage> pages) =>
            pages
                .Select(page => page.Elements.GetDictionary("/Resources"))
                .Select(resources => resources?.Elements?.GetDictionary("/XObject"))
                .Where(xObjects => xObjects?.Elements != null)
                .SelectMany(xObjects =>
                    from item in xObjects.Elements
                    let xObject = (item.Value as PdfReference)?.Value as PdfDictionary
                    where xObject?.Elements?.GetString("/Subtype") == "/Image"
                    select new ImageInfo(xObjects, item, xObject)
                )
                .ToList();

        /// <summary>
        /// Compute and return the MD5 hash of the input data.
        /// </summary>
        static string ComputeMD5(byte[] input)
        {
            byte[] hashBytes;
            lock (Hasher)
            {
                hashBytes = Hasher.ComputeHash(input);
                Hasher.Initialize();
            }

            var sb = new StringBuilder();
            foreach (var x in hashBytes)
            {
                sb.Append(x.ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
