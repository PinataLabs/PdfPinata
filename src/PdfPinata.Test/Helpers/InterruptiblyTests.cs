using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Work handed somewhere a test's Timeout can interrupt it.
/// </summary>
/// <remarks>
///   Small enough to look not worth testing, and the one thing that matters about it is invisible
///   at the call sites: <em>which</em> thread the work lands on. A version that quietly went back
///   to the pool would pass every test in the five classes that use this and bring the timeout
///   flake back with it, so the thread is asserted on here.
/// </remarks>
public class InterruptiblyTests
{
    [Fact]
    public async Task WorkAnswersWithWhatItReturned()
    {
        (await Interruptibly.Run(() => 6 * 7)).Should().Be(42);
    }

    [Fact]
    public async Task WorkThatThrowsHandsTheExceptionBack()
    {
        var run = async () => await Interruptibly.Run<int>(() => throw new InvalidOperationException("no"));

        // The exception itself, not a wrapper around it: these tests assert on what a reader
        // throws, and AwesomeAssertions' ThrowAsync would see an AggregateException instead.
        (await run.Should().ThrowAsync<InvalidOperationException>()).WithMessage("no");
    }

    [Fact]
    public async Task WorkRunsOffTheThreadPool()
    {
        // The whole point of the class. A pool thread is one the runtime may not start for as long
        // as it likes, and a Timeout counts that wait against the work.
        var pooled = await Interruptibly.Run(() => Thread.CurrentThread.IsThreadPoolThread);

        pooled.Should().BeFalse();
    }

    [Fact]
    public async Task WorkRunsWhereItCannotHoldTheProcessOpen()
    {
        // Work that never ends outlives the test that gave up on it, so the thread it is on has to
        // be one the runtime will abandon at exit.
        var background = await Interruptibly.Run(() => Thread.CurrentThread.IsBackground);

        background.Should().BeTrue();
    }

    [Fact]
    public async Task WorkRunsSomewhereOtherThanTheTest()
    {
        var testThread = Environment.CurrentManagedThreadId;

        var workThread = await Interruptibly.Run(() => Environment.CurrentManagedThreadId);

        // xUnit honours a Timeout only against what is not on the test's own thread.
        workThread.Should().NotBe(testThread);
    }

    [Fact]
    public async Task WorkWithNothingToReturnStillRuns()
    {
        var ran = false;

        await Interruptibly.Run(() => { ran = true; });

        ran.Should().BeTrue();
    }

    [Fact]
    public void NoWorkAtAllIsRefused()
    {
        // Typed as Action rather than left to inference: these refuse before there is a task to
        // await, and a lambda inferred as returning one would be asserted on as if there were.
        Action withNothingToReturn = () => Interruptibly.Run((Action)null);
        Action withSomethingToReturn = () => Interruptibly.Run((Func<int>)null);

        withNothingToReturn.Should().Throw<ArgumentNullException>();
        withSomethingToReturn.Should().Throw<ArgumentNullException>();
    }
}
