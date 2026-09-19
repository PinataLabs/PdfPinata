using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Runs a piece of work somewhere a test's <c>Timeout</c> can actually interrupt it.
/// </summary>
/// <remarks>
/// <para>
/// A lexer or parser handed malformed input can loop forever rather than fail, and a test that
/// calls one straight ends the whole test host instead of going red. The tests that scan such input
/// carry <c>[Fact(Timeout = …)]</c> to turn that back into a failure - and xUnit honours it only on
/// an <c>async</c> test, and only for what is not running on the test's own thread. So the work has
/// to be handed somewhere else, and every one of those tests does hand it somewhere else.
/// </para>
/// <para>
/// <b>The thread pool is the wrong somewhere.</b> xUnit starts the clock when it calls the test
/// method, so a timeout covers however long the work waited to begin as well as how long it ran.
/// Under <c>Task.Run</c> that is a queue: xUnit runs tests in parallel, coverage instrumentation
/// slows each of them, Ghostscript rasterizes inside this same process, and a CI runner has two
/// cores to spread it over. A work item can sit unstarted past the timeout, and the test then fails
/// saying it timed out having scanned nothing at all. That is what happened to
/// <c>CLexerTests.ScanHexadecimalString_readsTheBytesTheDigitsSpell</c> on a build whose only change
/// was a line in a workflow file - reported as timing out after 5000ms with a duration of 1ms, and
/// green on the re-run.
/// </para>
/// <para>
/// A thread of its own starts when it is told to, whatever the pool is doing, so the timeout goes
/// back to measuring the scan. That is the whole point of the class: not to move work off the test
/// thread - <c>Task.Run</c> did that much - but to stop the timeout from measuring how busy the
/// machine is.
/// </para>
/// <para>
/// A thread each is wasteful by the standards of ordinary code and irrelevant by the standards of
/// these tests: there are a few dozen of them, each scanning a few dozen bytes.
/// </para>
/// </remarks>
public static class Interruptibly
{
    /// <summary>
    /// Runs <paramref name="work"/> on a thread of its own and answers with what it returned.
    /// </summary>
    public static Task<T> Run<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Asynchronously, so that whatever awaits this does not carry on inline on the thread
        // below - which is the one thread here that may still be inside a scan that never ends.
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(work());
            }
            catch (Exception exception)
            {
                // Handed on rather than thrown here, where nothing is waiting to catch it: an
                // exception leaving a thread's entry point takes the process with it, which is the
                // failure mode these tests exist to avoid rather than one to add.
                completion.SetException(exception);
            }
        })
        {
            // A scan that never ends is exactly what the timeouts are written to catch, and giving
            // up on it does not stop it - the thread is still in the loop when the test has failed
            // and moved on. A background thread does not hold the process open, so the run still
            // finishes and still reports.
            IsBackground = true,
            Name = "interruptible test work"
        };

        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on a thread of its own.
    /// </summary>
    public static Task Run(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        return Run<object>(() =>
        {
            work();
            return null;
        });
    }
}
