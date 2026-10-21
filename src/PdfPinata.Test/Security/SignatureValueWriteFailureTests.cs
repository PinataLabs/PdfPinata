using System;
using System.IO;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Security;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   A signature value is written with the writer's security handler taken away, and a write that
///   fails part way through still gives it back. Issue #325.
/// </summary>
/// <remarks>
///   The writer is internal and a failed save discards it, so nothing a caller does can see the
///   handler left off; it is reached here by reflection, the way the other probes in this suite
///   reach internal types. What it guards is everything the same writer writes afterwards, which
///   would go out in plain.
/// </remarks>
public class SignatureValueWriteFailureTests
{
    private const string Marker = "FAIL-HERE";

    [Test]
    public void AFailedWriteOfTheSignatureValuePutsTheSecurityHandlerBack()
    {
        var document = new PdfDocument();
        var handler = document.SecurityHandler;
        handler.Should().NotBeNull();

        var signature = new PdfDictionary(document);
        signature.Elements["/ByteRange"] = new PdfArray(document, new Pdf.PdfInteger(0), new Pdf.PdfInteger(0));
        signature.Elements["/Contents"] = new PdfString(Marker);

        var writerType = typeof(PdfDocument).Assembly.GetType("PdfPinata.Pdf.IO.PdfWriter", true)!;
        var writer = Activator.CreateInstance(writerType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [new FailOnMarkerStream(), handler], null)!;
        var securityHandler = writerType.GetProperty("SecurityHandler", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var writeElement = typeof(PdfDictionary).GetMethod("WriteDictionaryElement", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var write = () => writeElement.Invoke(signature, [writer, new PdfName("/Contents")]);

        write.Should().Throw<TargetInvocationException>().WithInnerException<IOException>();
        securityHandler.GetValue(writer).Should().BeSameAs(handler,
            "everything the writer writes after the signature value is to be encrypted again");
    }

    /// <summary>A stream that fails the write carrying the marker, as a full disk would.</summary>
    private sealed class FailOnMarkerStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Encoding.Latin1.GetString(buffer, offset, count).Contains(Marker, StringComparison.Ordinal))
                throw new IOException("The disk is full.");
            base.Write(buffer, offset, count);
        }
    }
}
