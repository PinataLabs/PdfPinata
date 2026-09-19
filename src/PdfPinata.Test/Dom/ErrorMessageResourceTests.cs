using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   The strongly typed resource class named the resources of the assembly it was generated in
///   before this fork renamed that assembly, so it asked for
///   "PinataLayout.DocumentObjectModel...AppResources" while the assembly embeds
///   "PinataLayout.DocumentObjectModel...AppResources". Every message it holds threw
///   MissingManifestResourceException instead, so the error you were given was never the error
///   meant for you: asking for an unreadable colour reported a missing resource rather than the
///   colour it could not read.
/// </summary>
public class ErrorMessageResourceTests
{
    [Fact]
    public void EveryMessageCanBeRead()
    {
        // The guard that matters: one lookup name serves all of them, so one of these failing
        // means none of them work.
        var read = () => Messages().Select(message => message.Value).ToList();

        read.Should().NotThrow();
    }

    [Fact]
    public void NoMessageIsEmpty()
    {
        Messages().Should().NotBeEmpty();
        Messages().Should().AllSatisfy(message => message.Value.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void AColourThatCannotBeReadIsNamedInTheComplaint()
    {
        var parse = () => Color.Parse("not a colour");

        parse.Should().Throw<ArgumentException>()
            .WithMessage("*not a colour*");
    }

    [Fact]
    public void AUnitThatCannotBeReadIsNamedInTheComplaint()
    {
        var parse = () => Unit.Parse("not a unit");

        parse.Should().Throw<ArgumentException>()
            .WithMessage("*not a unit*");
    }

    [Fact]
    public void AValueNameThatDoesNotExistIsNamedInTheComplaint()
    {
        var paragraph = new Document().AddSection().AddParagraph();

        var read = () => paragraph.GetValue("NoSuchValue", GV.ReadOnly);

        read.Should().Throw<ArgumentException>()
            .WithMessage("*NoSuchValue*");
    }

    [Fact]
    public void ABaseStyleThatDoesNotExistIsNamedInTheComplaint()
    {
        var document = new Document();

        var define = () => document.Styles.AddStyle("Mine", "NoSuchBaseStyle");

        define.Should().Throw<ArgumentException>()
            .WithMessage("*NoSuchBaseStyle*");
    }

    /// <summary>
    ///   A second lookup sat on top of the first and had the same shape of fault. Every message
    ///   raised by an identifier rather than by a dedicated helper - which is every parser error
    ///   and every validation error in the DOM - went through <c>DomSR.GetString</c>, and that
    ///   asked <c>typeof(AppResources).GetProperties()</c> with no binding flags. The default
    ///   flags look for public instance properties; the generated ones are internal statics, so
    ///   the query matched nothing, <c>GetString</c> answered null for all fifty-eight of them,
    ///   and <c>FormatMessage</c> fell through to its "message not found" placeholder.
    ///   <para>
    ///   The messages were in the .resx the whole time. Reading a malformed .mdddl file reported
    ///   <c>&lt;&lt;&lt;error: message not found&gt;&gt;&gt;</c> rather than which symbol was
    ///   wrong, so the tests above passed - they read the properties directly - while nothing a
    ///   caller was actually shown worked.
    ///   </para>
    /// </summary>
    [Fact]
    public void EveryMessageIsReachableByTheIdentifierThatNamesIt()
    {
        // The guard that matters, again: one lookup serves all of them, so this failing for one
        // means it fails for every error the DOM can raise. Success is the exception because it
        // is not an error - it carries no message, and nothing raises it.
        foreach (var id in Enum.GetValues(MsgIdType))
        {
            if (id.ToString() == "Success")
                continue;

            GetString(id).Should().NotBeNullOrWhiteSpace(
                "{0} is an error the DOM can raise", id);
        }
    }

    [Fact]
    public void AMessageIsFormattedWithWhatWentWrongRatherThanAPlaceholder()
    {
        FormatMessage(MsgId("UnexpectedSymbol"), "\\pagebreak")
            .Should().Be("Unexpected symbol '\\pagebreak'.");
    }

    // ReSharper disable PossibleNullReferenceException
    static string GetString(object id) => (string)DomSRType
        .GetMethod("GetString", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new[] { id });
    // ReSharper restore PossibleNullReferenceException

    // ReSharper disable PossibleNullReferenceException
    static string FormatMessage(object id, params object[] args) => (string)DomSRType
        .GetMethod("FormatMessage", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new[] { id, args });
    // ReSharper restore PossibleNullReferenceException

    static object MsgId(string name) => Enum.Parse(MsgIdType, name);

    /// <summary>
    ///   Both are internal to the document object model, and this repository carries no
    ///   <c>InternalsVisibleTo</c>, so they are reached by name the way AppResources above is.
    /// </summary>
    static readonly Type DomSRType = typeof(Document).Assembly.GetType(
        "PinataLayout.DocumentObjectModel.Resources.DomSR", true);

    static readonly Type MsgIdType = typeof(Document).Assembly.GetType(
        "PinataLayout.DocumentObjectModel.DomMsgID", true);

    /// <summary>
    ///   Every message the resource class holds, read the way the library reads them. The class
    ///   is internal to the document object model, so it is reached by name rather than by type.
    /// </summary>
    static IReadOnlyList<KeyValuePair<string, string>> Messages()
    {
        var resources = typeof(Document).Assembly.GetType(
            "PinataLayout.DocumentObjectModel.Resources.AppResources", true);

        // ReSharper disable PossibleNullReferenceException
        return resources
            .GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => new KeyValuePair<string, string>(property.Name, Read(property)))
            .ToList();
        // ReSharper restore PossibleNullReferenceException
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
