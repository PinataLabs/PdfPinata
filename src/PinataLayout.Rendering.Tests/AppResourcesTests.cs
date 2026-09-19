using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   The renderer's strongly typed resource class looks its strings up by a manifest name the SDK
///   builds from the folder the resx sits in, so moving that folder without changing the name
///   compiles cleanly and then throws MissingManifestResourceException on the first message read.
///   The DOM's resources are guarded the same way, by <c>ErrorMessageResourceTests</c>.
/// </summary>
public class AppResourcesTests
{
    [Fact]
    public void EveryMessageCanBeRead()
    {
        // One lookup name serves all of them, so one of these failing means none of them work.
        var read = () => Messages().Select(message => message.Value).ToList();

        read.Should().NotThrow();
    }

    [Fact]
    public void NoMessageIsEmpty()
    {
        Messages().Should().NotBeEmpty();
        Messages().Should().AllSatisfy(message => message.Value.Should().NotBeNullOrWhiteSpace());
    }

    /// <summary>
    ///   Every message the resource class holds, read the way the library reads them. The class
    ///   is internal and this repository carries no <c>InternalsVisibleTo</c>, so it is reached by
    ///   name rather than by type.
    /// </summary>
    static IReadOnlyList<KeyValuePair<string, string>> Messages()
    {
        var resources = typeof(PdfDocumentRenderer).Assembly.GetType(
            "PinataLayout.Rendering.Resources.AppResources", true);

        // ReSharper disable once PossibleNullReferenceException
        return resources
            .GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => new KeyValuePair<string, string>(property.Name, Read(property)))
            .ToList();
    }

    static string Read(PropertyInfo property)
    {
        try
        {
            return (string)property.GetValue(null);
        }
        catch (TargetInvocationException exception)
        {
            // Reflection wraps what the property threw; the test wants to see that instead.
            throw exception.InnerException ?? exception;
        }
    }
}
