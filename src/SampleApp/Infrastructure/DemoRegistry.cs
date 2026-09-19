using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SampleApp.Demos;

namespace SampleApp.Infrastructure;

/// <summary>
///   Every demo, in the order they are meant to be read.
/// </summary>
/// <remarks>
///   Listed rather than discovered by reflection, for three reasons. The order is the curriculum -
///   hello world first, one feature at a time after it, the combined layouts last - and reflection
///   returns whatever order the metadata happens to be in. The smoke test reads this list as its
///   data source, and a theory whose cases come from assembly scanning is a theory that silently
///   covers nothing the day scanning stops finding them. And adding a demo is then one line in a
///   diff a reviewer sees, rather than an attribute nobody notices is missing.
/// </remarks>
public static class DemoRegistry
{
    public static IReadOnlyList<PdfDemo> All { get; } = new PdfDemo[]
    {
        new HelloWorldDemo(),
        new FontsDemo(),
        new UnicodeDemo(),
        new InternationalDemo(),
        new OrientationDemo(),
        new ImagesDemo(),
        new ImageFailuresDemo(),
        new TextDemo(),
        new VectorsDemo(),
        new BarcodesDemo(),
        new LayoutDemo(),
        new TablesDemo(),
        new PageResizeDemo(),
        new BleedDemo(),
        new AssembleDemo(),
        new ImpositionDemo(),
        new ProtectDemo(),
        new SigningDemo(),
        new NavigationDemo(),
        new CompressDemo(),
        new InspectDemo(),
        new ArchiveDemo(),
        new FacturXDemo(),
        new ExtractDemo(),
        new ReviseDemo(),
        new FormsDemo(),
        new AnnotationsDemo(),
        new OutlineDemo(),
        new InvoiceDemo(),
        new StructureDemo(),
        new AccessibilityDemo(),
        new FootnotesDemo(),
        new DdlDemo(),
        new ChartsDemo(),
        new NewspaperDemo(),
        new MagazineDemo(),
        new SideWrapDemo(),
    };

    public static IReadOnlyList<string> Names { get; } = All.Select(demo => demo.Name).ToArray();

    /// <summary>Finds a demo by name, ignoring case, so <c>-e fonts</c> finds <c>Fonts</c>.</summary>
    public static bool TryGet(string name, [NotNullWhen(true)] out PdfDemo? demo)
    {
        foreach (PdfDemo candidate in All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                demo = candidate;
                return true;
            }
        }

        demo = null;
        return false;
    }
}
