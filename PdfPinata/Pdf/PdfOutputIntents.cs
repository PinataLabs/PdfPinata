using System;
using System.IO;
using System.Reflection;

namespace PdfPinata.Pdf;

/// <summary>
/// The colour profile this library can describe a document's colours with, for the output intent
/// every PDF/A document has to carry.
/// </summary>
/// <remarks>
/// <para>
/// One profile, and only one, because there is only one this library can honestly supply. A
/// document's colours mean whatever the device they were made for meant by them, and an archival
/// file has to say which device that was — so the profile that belongs in a document is a fact
/// about the document, which is why <see cref="PdfDocumentOptions.OutputIntentIccProfile"/> exists
/// and why anything a caller sets there wins.
/// </para>
/// <para>
/// The exception is <see cref="PdfColorMode.Rgb"/>, and it is a large exception because it is the
/// default. Colours written as RGB by a library that was never told otherwise are sRGB — that is
/// what every reader assumes of them and what they were almost certainly authored as — so sRGB is
/// not a guess there but a description. A document claiming PDF/A with RGB colours and no profile
/// of its own gets <see cref="SrgbProfile"/> and <see cref="SrgbIdentifier"/>. A CMYK document
/// still refuses: only the press knows what those numbers mean.
/// </para>
/// <para>
/// It is offered here as well as used automatically, for the caller who would rather say it than
/// have it inferred, and for the one who wants to see what is in it.
/// </para>
/// </remarks>
public static class PdfOutputIntents
{
    /// <summary>
    /// The name the profile is embedded in this assembly under.
    /// </summary>
    const string Resource = "PdfPinata.sRGB-v2-micro.icc";

    /// <summary>
    /// Where an ICC profile's own file signature sits, and what it says. Every profile carries
    /// <c>acsp</c> there and nothing else does.
    /// </summary>
    const int SignatureAt = 36;

    /// <summary>
    /// The profile, read and checked once. <see cref="Lazy{T}"/> rather than a field and a null
    /// check, because two threads asking at the same moment would otherwise both read the resource
    /// and both check it — the same answer twice over, which is waste rather than a defect, but
    /// stating "once" is shorter than explaining why doing it twice is harmless.
    /// </summary>
    static readonly Lazy<byte[]> Loaded = new Lazy<byte[]>(Read);

    /// <summary>
    /// The bytes of an sRGB profile, ready to be assigned to
    /// <see cref="PdfDocumentOptions.OutputIntentIccProfile"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 456 bytes, released to the public domain under CC0, so a document carrying it owes nobody
    /// anything. It is a real profile rather than a plausible one: the true sRGB primaries, a white
    /// point adapted to the D50 connection space every ICC profile is expressed in, and the
    /// transfer curve sampled at 42 points. <c>assets/icc/LICENSE.txt</c> in the repository records
    /// where it came from and what is in it.
    /// </para>
    /// <para>
    /// <b>ICC version 2 rather than 4, deliberately.</b> PDF/A-1 is defined against a PDF that
    /// predates ICC v4 and will not accept one, so a v2 profile is the only kind that serves every
    /// part of the standard.
    /// </para>
    /// <para>
    /// A fresh copy each time, because an array is mutable and a caller who edited a shared one
    /// would change what every later document says about its colours.
    /// </para>
    /// </remarks>
    public static byte[] SrgbProfile => (byte[])Loaded.Value.Clone();

    /// <summary>
    /// What to write as <c>/OutputConditionIdentifier</c> for <see cref="SrgbProfile"/>, which is
    /// the name of the condition rather than the name of the file.
    /// </summary>
    /// <remarks>
    /// The distinction matters and is easy to lose. The identifier says which viewing condition the
    /// numbers in the document are in; the embedded profile says what that condition is in terms a
    /// machine can act on. sRGB IEC61966-2.1 is the condition, and it stays the condition whichever
    /// of the several sRGB profiles in the world describes it.
    /// </remarks>
    public const string SrgbIdentifier = "sRGB IEC61966-2.1";

    /// <summary>
    /// The embedded bytes. Never handed out directly — see <see cref="SrgbProfile"/>.
    /// </summary>
    static byte[] Read()
    {
        using (var stream = typeof(PdfOutputIntents).GetTypeInfo().Assembly
                   .GetManifestResourceStream(Resource))
        {
            if (stream == null)
                throw new InvalidOperationException(
                    "The sRGB profile '" + Resource + "' is not embedded in this assembly, so no "
                    + "document can be given the output intent PDF/A requires of it. A build that "
                    + "strips embedded resources is the likely cause.");

            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                var bytes = buffer.ToArray();

                // Checked because the failure it catches is otherwise a mystery. A binary that has
                // been through line-ending normalisation is still a file and still embeds, and what
                // it produces is every PDF/A document at once failing validation on its output
                // intent — which reads as a defect in the writer rather than as a mangled asset.
                if (bytes.Length < SignatureAt + 4
                    || bytes[SignatureAt] != 'a' || bytes[SignatureAt + 1] != 'c'
                    || bytes[SignatureAt + 2] != 's' || bytes[SignatureAt + 3] != 'p')
                {
                    throw new InvalidOperationException(
                        "'" + Resource + "' is embedded but is not an ICC profile: the 'acsp' "
                        + "signature every profile carries at byte " + SignatureAt + " is not there. "
                        + "The likely cause is the file having been treated as text somewhere "
                        + "between the repository and here.");
                }

                return bytes;
            }
        }
    }
}
