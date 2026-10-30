using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PdfPinata.Test.Helpers;
using PinataLayout.DocumentObjectModel.IO;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   What the reader has to say about a piece of MDDDL. The parser reports far more than it
///   throws - every attribute assignment is wrapped in <c>catch (Exception)</c> - so a test that
///   wants to know what was wrong with a document has to read the error list and the exception
///   together, and cannot tell in advance which of the two a given fault will arrive in.
/// </summary>
internal static class ReaderDiagnostics
{
    /// <summary>
    ///   Every complaint reading the given DDL produced, whether the reader wrote it to the error
    ///   list or gave up and threw it.
    /// </summary>
    /// <remarks>
    ///   This catches everything, so it is the wrong helper for a test asserting that a
    ///   particular exception is <em>not</em> thrown - such a test must call the reader itself.
    ///   <c>DdlParserException</c> is internal to the assembly under test, which is why the catch
    ///   is written against its base.
    /// </remarks>
    public static IReadOnlyList<string> ComplaintsAbout(string ddl)
    {
        var errors = new DdlReaderErrors();
        try
        {
            DdlReader.ObjectFromString(ddl, errors);
        }
        catch (Exception fatal)
        {
            return [..Reported(errors).Append(fatal.Message)];
        }
        return Reported(errors);
    }

    /// <summary>The messages the reader wrote to the error list, in the order it wrote them.</summary>
    public static IReadOnlyList<string> Reported(DdlReaderErrors errors) =>
        [..errors.Cast<DdlReaderError>().Select(error => error.ErrorMessage)];

    /// <summary>The errors proper, leaving out anything reported only as a warning or a note.</summary>
    public static IReadOnlyList<DdlReaderError> ErrorsIn(DdlReaderErrors errors) =>
        [..errors.Cast<DdlReaderError>()
            .Where(error => error.ErrorLevel == DdlErrorLevel.Error)];

    /// <summary>
    ///   Reads the DDL on a thread of its own, through <see cref="Interruptibly"/>. The task
    ///   finishes when the reader does - with the exception it threw, or with null when it
    ///   returned - and never finishes when the reader does not come back, which for some
    ///   documents it does not.
    /// </summary>
    /// <remarks>
    ///   A fault completes the task rather than failing it, so there is never an exception left
    ///   for nobody to observe when a caller stops waiting.
    /// </remarks>
    public static Task<Exception> ReadingOnItsOwnThread(string ddl) =>
        Interruptibly.Run<Exception>(() =>
        {
            try
            {
                DdlReader.ObjectFromString(ddl, new DdlReaderErrors());
                return null;
            }
            catch (Exception fault)
            {
                return fault;
            }
        });

    /// <summary>
    ///   What reading the DDL threw, or null if it returned - or if it never came back at all,
    ///   which is a separate defect and not what a caller of this is asking about.
    /// </summary>
    public static async Task<Exception> FaultReading(string ddl, TimeSpan patience)
    {
        var reading = ReadingOnItsOwnThread(ddl);

        return await FinishesWithin(reading, patience)
            ? await reading
            : null;
    }

    /// <summary>
    ///   Whether the task finishes within the time given. The wait is given up on, not the task,
    ///   and nothing is left running behind it - a timer raced against the task with
    ///   <c>Task.WhenAny</c> runs on to its end when the task wins.
    /// </summary>
    public static async Task<bool> FinishesWithin(Task task, TimeSpan patience)
    {
        try
        {
            await task.WaitAsync(patience);
            return true;
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            return false;
        }
    }
}
