using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A key declared <c>[KeyInfo("1.2", …)]</c> says which PDF version it arrived in, and the
///   attribute has to keep what it was told. The constructor that also takes the value's class
///   dropped the version on the floor, so every key declared that way claimed PDF 1.0.
///   <para>
///   The attribute is internal and this repository carries no <c>InternalsVisibleTo</c>, so it is
///   read the way anything outside the assembly would read it: by reflection, comparing the version
///   written in the source (the constructor argument) with the one the instance reports.
///   </para>
/// </summary>
public class KeyInfoVersionTests
{
    [Fact]
    public void EveryKeyDeclaredWithAVersionReportsThatVersion()
    {
        var assembly = typeof(PdfDocument).Assembly;
        var attributeType = assembly.GetType("PdfPinata.Pdf.KeyInfoAttribute", throwOnError: true)!;
        var versionProperty = attributeType.GetProperty("Version")!;

        var fields = assembly.GetTypes()
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .ToList();

        var declared = 0;
        var wrong = fields
            .SelectMany(field => field.GetCustomAttributesData()
                .Where(data => data.AttributeType == attributeType)
                .Where(data => data.ConstructorArguments.Count > 0 && data.ConstructorArguments[0].ArgumentType == typeof(string))
                .Select(data => (field, written: (string)data.ConstructorArguments[0].Value!)))
            .Select(entry =>
            {
                declared++;
                var instance = entry.field.GetCustomAttribute(attributeType)!;
                return (entry.field, entry.written, reported: (string)versionProperty.GetValue(instance));
            })
            .Where(entry => entry.reported != entry.written)
            .Select(entry => $"{entry.field.DeclaringType!.FullName}.{entry.field.Name}: written {entry.written}, reports {entry.reported}")
            .ToList();

        declared.Should().BePositive("the library declares versioned keys, or this test reads nothing");
        wrong.Should().BeEmpty();
    }
}
