using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Shapes;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   Every DocumentObject caches the reflection metadata for its type in a static. That cache used
///   to be filled on first use without synchronization, so two threads arriving together each
///   reflected over the whole type and one of the two results was thrown away.
///
///   That was wasteful rather than wrong - Meta's constructor finishes before the instance is
///   assigned, so neither thread ever saw a half-built one - and these tests pin the invariant that
///   makes it moot: there is one Meta per type, and everyone gets the same one.
/// </summary>
public class MetaCacheTests
{
    private const int Threads = 64;

    [Fact]
    public void EveryInstanceOfATypeSharesOneMeta()
    {
        var first = Meta.GetMeta(new Document());
        var second = Meta.GetMeta(new Document());

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void EveryThreadIsHandedTheSameMeta()
    {
        var metas = new ConcurrentBag<Meta>();

        Parallel.For(0, Threads, _ => metas.Add(Meta.GetMeta(new Document())));

        metas.Should().HaveCount(Threads);
        metas.Distinct().Should().ContainSingle();
    }

    [Fact]
    public void DifferentTypesGetDifferentMetas()
    {
        var document = Meta.GetMeta(new Document());
        var image = Meta.GetMeta(new Image());

        image.Should().NotBeSameAs(document);
    }

    [Fact]
    public void BuildingManyDifferentMetasAtOnceIsStable()
    {
        var results = new ConcurrentBag<string>();

        Parallel.For(0, Threads, index =>
        {
            // A spread of types so the first touch of several caches overlaps.
            var meta = (index % 4) switch
            {
                0 => Meta.GetMeta(new Document()),
                1 => Meta.GetMeta(new Image()),
                2 => Meta.GetMeta(new Document().AddSection().AddParagraph("x").Format.Font),
                _ => Meta.GetMeta(new Document().AddSection().PageSetup)
            };
            results.Add($"{index % 4}:{meta.ValueDescriptors.Count}");
        });

        // Each of the four types must report the same descriptor count every time it is asked.
        results.Select(r => r.Split(':')[0]).Distinct().Should().HaveCount(4);
        results.Distinct().Should().HaveCount(4, "a type's descriptor count must not depend on who built its Meta");
    }
}
