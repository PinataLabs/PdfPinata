using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using TUnit.Core;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="ReaderDiagnostics.FinishesWithin"/>, which the tests of a reader that never comes
///   back are built on. It answers whether the task finished and nothing else: how the task
///   finished stays with the task, for a caller that awaits it to find out.
/// </summary>
public class ReaderDiagnosticsTests
{
    [Test]
    public async Task ATaskThatHasFinishedFinishesWithin()
    {
        (await ReaderDiagnostics.FinishesWithin(Task.CompletedTask, TimeSpan.FromSeconds(5)))
            .Should().BeTrue();
    }

    [Test]
    public async Task ATaskThatNeverFinishesDoesNot()
    {
        var never = new TaskCompletionSource().Task;

        (await ReaderDiagnostics.FinishesWithin(never, TimeSpan.FromMilliseconds(50)))
            .Should().BeFalse();
    }

    [Test]
    public async Task ATaskThatFailedHasFinishedAndKeepsItsFailure()
    {
        var failed = Task.FromException(new InvalidOperationException("the reader's own"));

        (await ReaderDiagnostics.FinishesWithin(failed, TimeSpan.FromSeconds(5)))
            .Should().BeTrue();
    }

    /// <summary>
    ///   A task that failed with a timeout of its own has still finished. Telling that apart from the
    ///   wait running out by asking afterwards whether the task had completed was a race as well:
    ///   one finishing just after the deadline turned the wait's timeout into an exception.
    /// </summary>
    [Test]
    public async Task ATaskThatTimedOutOnItsOwnAccountHasFinished()
    {
        var timedOut = Task.FromException(new TimeoutException("the reader's own"));

        (await ReaderDiagnostics.FinishesWithin(timedOut, TimeSpan.FromSeconds(5)))
            .Should().BeTrue();
    }
}
