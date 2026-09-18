using AwesomeAssertions;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <c>BindToRenderer</c> throws when a document is already bound to a different renderer, and its
///   own message names <c>Clone</c> as the way around that - render the same document on a second
///   renderer by rendering a copy instead. <c>renderer</c> carries no <c>[DV]</c>, so
///   <c>DocumentObject.DeepCopy</c>'s descriptor walk never reaches it and a clone would otherwise
///   still carry the original's binding by the same MemberwiseClone that copies every other plain
///   field. <c>Document.DeepCopy</c> clears it explicitly for that reason.
/// </summary>
public class DocumentCloneRendererBindingTests
{
    [Fact]
    public void ACloneOfABoundDocumentStartsUnbound()
    {
        var document = new Document();
        document.BindToRenderer(new object());

        var clone = document.Clone();

        clone.IsBoundToRenderer.Should().BeFalse();
    }

    [Fact]
    public void ACloneOfABoundDocumentCanBeBoundToADifferentRenderer()
    {
        var document = new Document();
        document.BindToRenderer(new object());

        var clone = document.Clone();
        var act = () => clone.BindToRenderer(new object());

        act.Should().NotThrow();
    }

    [Fact]
    public void CloningDoesNotUnbindTheOriginal()
    {
        var document = new Document();
        var renderer = new object();
        document.BindToRenderer(renderer);

        document.Clone();

        document.IsBoundToRenderer.Should().BeTrue();
    }
}
