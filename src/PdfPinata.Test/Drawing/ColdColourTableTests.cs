using System;
using System.Reflection;
using System.Runtime.Loader;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The table of 141 named colours is static state, and the question these tests ask is what the
///   library does with it before anything has asked for a colour by name. That is impossible to
///   answer from an ordinary test: by the time one runs, two thousand others have been through the
///   table and it is long since built. So the assembly is loaded a second time, into a load context
///   of its own, where its statics are untouched and its type initializers have not yet run.
/// </summary>
/// <remarks>
///   <para>
///   Everything here goes through reflection because the second copy's types are not the same
///   types as the ones this assembly is compiled against - an <c>XColor</c> from the fresh context
///   cannot be assigned to an <see cref="XColor"/> - and the context is collectible so that the
///   copy goes away with the test.
///   </para>
/// </remarks>
public class ColdColourTableTests
{
    /// <summary>
    ///   Runs <paramref name="work"/> against a copy of PdfPinata whose static state is
    ///   untouched, and hands back whatever it returns.
    /// </summary>
    static object OnAColdCopyOfTheLibrary(Func<Assembly, object> work)
    {
        var context = new AssemblyLoadContext("cold PdfPinata", isCollectible: true);
        try
        {
            return work(context.LoadFromAssemblyPath(typeof(XColor).Assembly.Location));
        }
        finally
        {
            context.Unload();
        }
    }

    static object InvokeThrough(Func<Assembly, object> work)
    {
        try
        {
            return OnAColdCopyOfTheLibrary(work);
        }
        catch (TargetInvocationException ex)
        {
            // Reflection wraps whatever the library threw; the test wants the real one.
            throw ex.InnerException ?? ex;
        }
    }

    [Fact]
    public void AskingWhetherAColourIsKnownWorksBeforeAnyColourHasBeenNamed()
    {
        // The table is built by the lookup that turns a known colour into an ARGB, and by nothing
        // else. The two lookups that search it did not build it and did not check whether it was
        // there, so this was a NullReferenceException out of a property getter.
        var known = InvokeThrough(assembly =>
        {
            var colorType = assembly.GetType("PdfPinata.Drawing.XColor", throwOnError: true);
            // ReSharper disable once PossibleNullReferenceException
            var fromArgb = colorType.GetMethod("FromArgb", new[] { typeof(int), typeof(int), typeof(int) });
            // ReSharper disable once PossibleNullReferenceException
            var color = fromArgb.Invoke(null, new object[] { 1, 2, 3 });
            // ReSharper disable once PossibleNullReferenceException
            return colorType.GetProperty("IsKnownColor").GetValue(color);
        });

        known.Should().Be(false, "1, 2, 3 is not one of the 141");
    }

    [Fact]
    public void LookingUpAKnownColourByItsArgbWorksBeforeAnyColourHasBeenNamed()
    {
        var name = InvokeThrough(assembly =>
        {
            var managerType = assembly.GetType("PdfPinata.Drawing.XColorResourceManager", throwOnError: true);
            // ReSharper disable once PossibleNullReferenceException
            var getKnownColor = managerType.GetMethod("GetKnownColor", new[] { typeof(uint) });
            // ReSharper disable once PossibleNullReferenceException
            return getKnownColor.Invoke(null, new object[] { 0xFFFF0000u }).ToString();
        });

        name.Should().Be("Red");
    }

    [Fact]
    public void TheTableIsBuiltOnceAndCannotBeReplacedAfterwards()
    {
        // Which is what makes the two tests above hold rather than happening to hold: a field the
        // type initializer fills can never be read before it is filled, and nothing can empty it
        // again. The PinataLayout colour table was bitten by the other arrangement - built on first
        // use, assigned while still half built - and ColorThreadSafetyTests records what that
        // cost.
        var tableType = typeof(XColor).Assembly
            .GetType("PdfPinata.Drawing.XKnownColorTable", throwOnError: true);
        // ReSharper disable once PossibleNullReferenceException
        var table = tableType.GetField("ColorTable", BindingFlags.Static | BindingFlags.NonPublic);

        table.Should().NotBeNull();
        // ReSharper disable once PossibleNullReferenceException
        table.IsInitOnly.Should().BeTrue("only the type initializer may fill it");
        ((uint[])table.GetValue(null)).Should().HaveCount(141);
    }
}
