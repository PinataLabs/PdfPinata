using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Internal;

/// <summary>
///   The arithmetic helpers in <c>PdfPinata.Internal</c>: the floating-point comparisons the
///   geometry types are written in, the degrees-to-radians factor the arc drawing turns on, and
///   the stream reader that fills a buffer properly. None of them is reachable by name from
///   outside the library, so they are reached by
///   reflection, the way <c>AreaProbe</c> and <c>ParagraphIteratorProbe</c> already reach what
///   they need - this repository carries no <c>InternalsVisibleTo</c>, which is also why every
///   assembly gets its own copy of the netstandard2.1 polyfills.
///   <para>
///   They are worth reaching. <c>DoubleUtil</c> is what decides whether a matrix is invertible,
///   and a comparison that is wrong at the edges is wrong in a way no drawing test would localise.
///   </para>
/// </summary>
public class InternalHelperTests
{
    private static readonly Assembly Library = typeof(XPoint).Assembly;

    private static Type TypeNamed(string name) =>
        Library.GetType("PdfPinata.Internal." + name, throwOnError: true);

    private static object Call(string typeName, string method, params object[] arguments)
    {
        var type = TypeNamed(typeName);
        var candidates = type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == method && m.GetParameters().Length == arguments.Length)
            .ToArray();

        var chosen = candidates.Length == 1
            ? candidates[0]
            : candidates.Single(m => m.GetParameters()
                .Select((p, idx) => p.ParameterType.IsInstanceOfType(arguments[idx]))
                .All(matches => matches));

        try
        {
            return chosen.Invoke(null, arguments);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    private static bool Bool(string typeName, string method, params object[] arguments) =>
        (bool)Call(typeName, method, arguments);

    // ----- DoubleUtil ----------------------------------------------------------------------------

    [Fact]
    public void TwoNumbersAreCloseWhenNothingBetweenThemMatters()
    {
        // The tolerance scales with the numbers being compared rather than being a fixed epsilon,
        // so it means the same thing for page coordinates as for a unit vector.
        Bool("DoubleUtil", "AreClose", 1.0, 1.0).Should().BeTrue();
        Bool("DoubleUtil", "AreClose", 1.0, 1.0 + 1e-16).Should().BeTrue();
        Bool("DoubleUtil", "AreClose", 1.0, 1.5).Should().BeFalse();
        Bool("DoubleUtil", "AreClose", 1e6, 1e6 + 1.0).Should().BeFalse("a whole unit apart is apart");
        Bool("DoubleUtil", "AreClose", 1e-6, 1e-5).Should().BeFalse();
    }

    [Fact]
    public void TheOrderingComparisonsTreatCloseNumbersAsEqual()
    {
        Bool("DoubleUtil", "GreaterThanOrClose", 1.0, 1.0).Should().BeTrue();
        Bool("DoubleUtil", "GreaterThanOrClose", 0.5, 1.0).Should().BeFalse();
        Bool("DoubleUtil", "LessThanOrClose", 1.0, 1.0).Should().BeTrue();
        Bool("DoubleUtil", "LessThanOrClose", 1.5, 1.0).Should().BeFalse();
    }

    [Fact]
    public void ZeroIsRecognisedToWithinTheTolerance()
    {
        Bool("DoubleUtil", "IsZero", 0.0).Should().BeTrue();
        Bool("DoubleUtil", "IsZero", 1e-300).Should().BeTrue("smaller than the tolerance is zero");
        Bool("DoubleUtil", "IsZero", 0.1).Should().BeFalse();
    }

    [Fact]
    public void NotANumberIsRecognisedWithoutComparingItToItself()
    {
        // Written by reading the bit pattern rather than with the == that never holds for NaN,
        // which is the whole reason the helper exists.
        Bool("DoubleUtil", "IsNaN", double.NaN).Should().BeTrue();
        Bool("DoubleUtil", "IsNaN", 0.0).Should().BeFalse();
        Bool("DoubleUtil", "IsNaN", double.PositiveInfinity).Should().BeFalse();
        Bool("DoubleUtil", "IsNaN", double.NegativeInfinity).Should().BeFalse();
    }

    [Theory]
    [InlineData(1.9, 2)]
    [InlineData(1.4, 1)]
    [InlineData(-1.9, -2)]
    [InlineData(-1.4, -1)]
    [InlineData(0.0, 0)]
    public void ADoubleBecomesAnIntByRoundingAwayFromZero(double value, int expected)
    {
        // Not a cast, which would truncate towards zero and lose half a unit on the way to a
        // pixel coordinate.
        Call("DoubleUtil", "DoubleToInt", value).Should().Be(expected);
    }

    // ----- Calc ----------------------------------------------------------------------------------

    [Fact]
    public void DegreesConvertToRadiansByTheFactorEverythingElseUses()
    {
        // ReSharper disable PossibleNullReferenceException
        var deg2Rad = (double)TypeNamed("Calc").GetField("Deg2Rad",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
        // ReSharper restore PossibleNullReferenceException

        (180 * deg2Rad).Should().BeApproximately(Math.PI, 1e-12);
    }

    [Fact]
    public void TheDegreeToRadianFactorIsDeclaredInOnePlaceOnly()
    {
        // It used to be declared identically on both Internal.Calc and Const, so which one a
        // caller reached for was a coin toss and nothing would have caught the two drifting.
        // ReSharper disable PossibleNullReferenceException
        var elsewhere = Library.GetType("PdfPinata.Const")
            .GetField("Deg2Rad", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        // ReSharper restore PossibleNullReferenceException

        elsewhere.Should().BeNull("Internal.Calc is where this factor lives");
    }

    // ----- StreamHelper --------------------------------------------------------------------------

    [Fact]
    public void ABufferIsFilledEvenWhenTheStreamHandsOverItsBytesALittleAtATime()
    {
        // A single Read is allowed to return fewer bytes than asked for, and buffered, compressed
        // and crypto streams routinely do. Reading in a loop is the only way to fill a buffer.
        var stream = new DribblingStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, mostPerRead: 3);
        var buffer = new byte[8];

        var read = (int)Call("StreamHelper", "ReadUpTo", stream, buffer, 0, 8);

        read.Should().Be(8);
        buffer.Should().Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    }

    [Fact]
    public void AStreamThatEndsEarlyGivesBackWhatThereWasAndSaysHowMuch()
    {
        var stream = new DribblingStream(new byte[] { 1, 2, 3 }, mostPerRead: 2);
        var buffer = new byte[8];

        var read = (int)Call("StreamHelper", "ReadUpTo", stream, buffer, 0, 8);

        read.Should().Be(3);
        buffer.Should().Equal(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0 }, "the rest of the buffer is left alone");
    }

    [Fact]
    public void ABufferCanBeFilledFromPartWayAlong()
    {
        var stream = new DribblingStream(new byte[] { 9, 9 }, mostPerRead: 1);
        var buffer = new byte[4];

        var read = (int)Call("StreamHelper", "ReadUpTo", stream, buffer, 2, 2);

        read.Should().Be(2);
        buffer.Should().Equal(new byte[] { 0, 0, 9, 9 });
    }

    /// <summary>A stream that never hands over more than a few bytes at a time.</summary>
    private sealed class DribblingStream : Stream
    {
        private readonly byte[] _data;
        private readonly int _mostPerRead;
        private int _position;

        internal DribblingStream(byte[] data, int mostPerRead)
        {
            _data = data;
            _mostPerRead = mostPerRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var available = Math.Min(Math.Min(count, _mostPerRead), _data.Length - _position);
            Array.Copy(_data, _position, buffer, offset, available);
            _position += available;
            return available;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
