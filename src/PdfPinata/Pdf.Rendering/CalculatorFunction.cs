using System;
using System.Collections.Generic;
using System.Globalization;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Type 4: a program in the PostScript calculator language (ISO 32000-1 7.10.5), read once into
/// instructions and run on a stack of fixed depth.
/// </summary>
/// <remarks>
/// The language has no loop and no procedure call, and <c>if</c> and <c>ifelse</c> become jumps
/// forwards, so one evaluation runs each instruction at most once. A program that runs off either
/// end of its stack, hands an operator the wrong kind of operand, or asks for what has no value
/// (a division by zero, the root or the logarithm of a number out of its range) answers zeros.
/// </remarks>
internal sealed class CalculatorFunction : PdfFunction
{
    /// <summary>The most bytes of a program that are read.</summary>
    internal const int MaximumLength = 1 << 16;

    /// <summary>How deep braces may nest. Each is read by a call inside the one before.</summary>
    internal const int MaximumNesting = 32;

    /// <summary>The depth of the operand stack, which is what the standard asks of a reader.</summary>
    internal const int StackDepth = 100;

    private readonly Instruction[] _program;
    private readonly int _outputs;

    private CalculatorFunction(double[] domain, double[] range, Instruction[] program) : base(domain, range)
    {
        _program = program;
        _outputs = range.Length / 2;
    }

    internal override int OutputCount => _outputs;

    // Each instruction runs at most once.
    internal override long Cost => _program.Length + 1;

    internal static PdfFunction Read(PdfDictionary dictionary, double[] domain, double[] range, ShadingWork work,
        DiagnosticSink sink, string subject)
    {
        if (range == null || range.Length == 0)
            return Invalid(sink, subject, "A calculator function has a /Range.");

        // One byte past the most that is read, to know a program longer than that from one as long.
        var source = work.Decode(dictionary, MaximumLength + 1);
        if (source == null)
            return Undecodable(work, sink, subject, "A calculator function's program cannot be decoded.");

        if (source.Length > MaximumLength)
        {
            sink.Report(PageRenderProblem.LimitReached, subject,
                "A calculator function's program is too long. What it colours is not drawn.");
            return null;
        }

        var parser = new Parser(source);
        var program = parser.ReadProgram();
        if (program == null)
        {
            if (parser.TooDeep)
            {
                sink.Report(PageRenderProblem.LimitReached, subject,
                    "A calculator function's braces are nested too deep. What it colours is not drawn.");
                return null;
            }

            return Invalid(sink, subject, "A calculator function's program is not one this reader knows.");
        }

        return new CalculatorFunction(domain, range, program);
    }

    protected override void Compute(ReadOnlySpan<double> input, Span<double> output)
    {
        Span<double> values = stackalloc double[StackDepth];
        Span<bool> truths = stackalloc bool[StackDepth];
        var stack = new Stack(values, truths);
        foreach (var value in input)
            stack.Push(value);

        for (var at = 0; at < _program.Length && !stack.Failed; at++)
        {
            var instruction = _program[at];
            switch (instruction.Op)
            {
                case Op.Push: stack.Push(instruction.Value); break;
                case Op.Jump: at += instruction.Skip; break;
                case Op.JumpIfFalse:
                    if (!stack.PopTruth())
                        at += instruction.Skip;
                    break;
                default: Run(instruction.Op, ref stack); break;
            }
        }

        if (stack.Failed || stack.Count < output.Length)
        {
            output.Clear();
            return;
        }

        for (var index = output.Length - 1; index >= 0; index--)
            output[index] = stack.Pop();
        if (stack.Failed)
            output.Clear();
    }

    private static void Run(Op op, ref Stack stack)
    {
        switch (op)
        {
            case Op.Abs: stack.Push(Math.Abs(stack.Pop())); break;
            case Op.Neg: stack.Push(-stack.Pop()); break;
            case Op.Ceiling: stack.Push(Math.Ceiling(stack.Pop())); break;
            case Op.Floor: stack.Push(Math.Floor(stack.Pop())); break;
            case Op.Round: stack.Push(Math.Floor(stack.Pop() + 0.5)); break;
            case Op.Truncate:
            case Op.Cvi: stack.Push(Math.Truncate(stack.Pop())); break;
            case Op.Cvr: stack.Push(stack.Pop()); break;
            case Op.Sqrt: PushOrFail(ref stack, Math.Sqrt(stack.Pop())); break;
            case Op.Sin: stack.Push(Math.Sin(stack.Pop() * Math.PI / 180)); break;
            case Op.Cos: stack.Push(Math.Cos(stack.Pop() * Math.PI / 180)); break;
            case Op.Ln: PushOrFail(ref stack, Math.Log(stack.Pop())); break;
            case Op.Log: PushOrFail(ref stack, Math.Log10(stack.Pop())); break;

            case Op.Add:
            case Op.Sub:
            case Op.Mul:
            case Op.Div:
            case Op.Idiv:
            case Op.Mod:
            case Op.Exp:
            case Op.Atan:
            case Op.Bitshift:
                Arithmetic(op, ref stack);
                break;

            case Op.Eq:
            case Op.Ne:
            case Op.Gt:
            case Op.Ge:
            case Op.Lt:
            case Op.Le:
                Compare(op, ref stack);
                break;

            case Op.And:
            case Op.Or:
            case Op.Xor:
                Combine(op, ref stack);
                break;

            case Op.Not:
                if (stack.TopIsTruth)
                    stack.PushTruth(!stack.PopTruth());
                else
                    stack.Push(~Whole(stack.Pop()));
                break;

            case Op.True: stack.PushTruth(true); break;
            case Op.False: stack.PushTruth(false); break;

            case Op.Pop: stack.Drop(); break;
            case Op.Exch: stack.Roll(2, 1); break;
            case Op.Dup: stack.Copy(1); break;
            case Op.Copy: stack.Copy(Count(stack.Pop())); break;
            case Op.Index: stack.Index(Count(stack.Pop())); break;
            case Op.Roll:
                var by = stack.Pop();
                stack.Roll(Count(stack.Pop()), by);
                break;
        }
    }

    private static void Arithmetic(Op op, ref Stack stack)
    {
        var right = stack.Pop();
        var left = stack.Pop();
        switch (op)
        {
            case Op.Add: stack.Push(left + right); break;
            case Op.Sub: stack.Push(left - right); break;
            case Op.Mul: stack.Push(left * right); break;
            case Op.Div: PushOrFail(ref stack, left / right); break;
            case Op.Exp: stack.Push(Math.Pow(left, right)); break;
            case Op.Idiv:
            case Op.Mod:
                long divisor = Whole(right), dividend = Whole(left);
                if (divisor == 0)
                    stack.Fail();
                else
                    stack.Push(op == Op.Idiv ? dividend / divisor : dividend % divisor);
                break;

            case Op.Atan:
                // The angle of (denominator, numerator), in degrees from 0 up to 360.
                var angle = Math.Atan2(left, right) * 180 / Math.PI;
                stack.Push(angle < 0 ? angle + 360 : angle);
                break;

            default:
                long shift = Whole(right), bits = Whole(left);
                stack.Push(shift is <= -32 or >= 32 ? 0 : shift >= 0 ? (int)(bits << (int)shift) : (int)bits >> (int)-shift);
                break;
        }
    }

    // A result that is no number is an error in PostScript, as undefinedresult or rangecheck.
    private static void PushOrFail(ref Stack stack, double value)
    {
        if (double.IsFinite(value))
            stack.Push(value);
        else
            stack.Fail();
    }

    private static void Compare(Op op, ref Stack stack)
    {
        // Two truth values are compared as 1 and 0, which is equal exactly where they are the same.
        // A truth value is never equal to a number, as in PostScript.
        var rightIsTruth = stack.TopIsTruth;
        var right = stack.PopEither();
        var leftIsTruth = stack.TopIsTruth;
        var left = stack.PopEither();
        if (rightIsTruth != leftIsTruth && op is Op.Eq or Op.Ne)
        {
            stack.PushTruth(op == Op.Ne);
            return;
        }

#pragma warning disable S1244 // Exact on purpose: eq and ne are PostScript's, which compare numbers as they are.
        stack.PushTruth(op switch
        {
            Op.Eq => left == right,
            Op.Ne => left != right,
            Op.Gt => left > right,
            Op.Ge => left >= right,
            Op.Lt => left < right,
            _ => left <= right
        });
#pragma warning restore S1244
    }

    private static void Combine(Op op, ref Stack stack)
    {
        if (stack.TopIsTruth)
        {
            var right = stack.PopTruth();
            var left = stack.PopTruth();
            stack.PushTruth(op switch { Op.And => left & right, Op.Or => left | right, _ => left ^ right });
        }
        else
        {
            var right = Whole(stack.Pop());
            var left = Whole(stack.Pop());
            stack.Push(op switch { Op.And => left & right, Op.Or => left | right, _ => left ^ right });
        }
    }

    // A number as the 32-bit integer the language's integer operators work on.
    private static int Whole(double value) =>
        value >= int.MaxValue ? int.MaxValue : value <= int.MinValue ? int.MinValue : (int)value;

    // A number as a count of stack entries. Anything that is not one is more than the stack holds.
    private static int Count(double value) => value >= 0 && value <= StackDepth ? (int)value : int.MaxValue;

    private enum Op : byte
    {
        Push, Jump, JumpIfFalse,
        Abs, Add, Atan, Ceiling, Cos, Cvi, Cvr, Div, Exp, Floor, Idiv, Ln, Log, Mod, Mul, Neg, Round, Sin, Sqrt,
        Sub, Truncate,
        And, Bitshift, Eq, False, Ge, Gt, Le, Lt, Ne, Not, Or, True, Xor,
        Copy, Dup, Exch, Index, Pop, Roll
    }

    private readonly struct Instruction(Op op, double value = 0, int skip = 0)
    {
        internal Op Op { get; } = op;

        /// <summary>What <see cref="Op.Push"/> pushes.</summary>
        internal double Value { get; } = value;

        /// <summary>How many instructions a jump passes over.</summary>
        internal int Skip { get; } = skip;
    }

    /// <summary>The operand stack: numbers, and beside each whether it is a truth value.</summary>
    private ref struct Stack(Span<double> values, Span<bool> truths)
    {
        private readonly Span<double> _values = values;
        private readonly Span<bool> _truths = truths;

        internal int Count { get; private set; }

        /// <summary>Whether the program has done something it cannot: nothing it does after counts.</summary>
        internal bool Failed { get; private set; }

        internal readonly bool TopIsTruth => Count > 0 && _truths[Count - 1];

        internal void Fail() => Failed = true;

        internal void Push(double value) => Push(value, false);

        internal void PushTruth(bool value) => Push(value ? 1 : 0, true);

        internal double Pop() => Pop(wantTruth: false);

        internal bool PopTruth() => Pop(wantTruth: true) > 0;

        internal double PopEither() => Pop(wantTruth: TopIsTruth);

        internal void Drop() => PopEither();

        /// <summary>Pushes the top <paramref name="count"/> entries again, in their order.</summary>
        internal void Copy(int count)
        {
            if (count > Count || Count + count > _values.Length)
            {
                Failed = true;
                return;
            }

            _values.Slice(Count - count, count).CopyTo(_values.Slice(Count));
            _truths.Slice(Count - count, count).CopyTo(_truths.Slice(Count));
            Count += count;
        }

        /// <summary>Pushes the entry <paramref name="down"/> places below the top again.</summary>
        internal void Index(int down)
        {
            if (down >= Count)
                Failed = true;
            else
                Push(_values[Count - 1 - down], _truths[Count - 1 - down]);
        }

        /// <summary>Turns the top <paramref name="count"/> entries round by <paramref name="by"/> places, towards the top where it is positive.</summary>
        internal void Roll(int count, double by)
        {
            if (count > Count || !double.IsFinite(by))
            {
                Failed = true;
                return;
            }

            if (count < 2)
                return;

            var shift = (int)(Math.Truncate(by) % count);
            if (shift < 0)
                shift += count;
            if (shift == 0)
                return;

            Span<double> values = stackalloc double[count];
            Span<bool> truths = stackalloc bool[count];
            var turned = _values.Slice(Count - count, count);
            var turnedTruths = _truths.Slice(Count - count, count);
            for (var index = 0; index < count; index++)
            {
                values[(index + shift) % count] = turned[index];
                truths[(index + shift) % count] = turnedTruths[index];
            }

            values.CopyTo(turned);
            truths.CopyTo(turnedTruths);
        }

        private void Push(double value, bool truth)
        {
            if (Count >= _values.Length)
            {
                Failed = true;
                return;
            }

            _values[Count] = value;
            _truths[Count++] = truth;
        }

        private double Pop(bool wantTruth)
        {
            if (Count == 0 || _truths[Count - 1] != wantTruth)
            {
                Failed = true;
                return 0;
            }

            return _values[--Count];
        }
    }

    /// <summary>Reads a program's text into instructions.</summary>
    private sealed class Parser(byte[] source)
    {
        private static readonly Dictionary<string, Op> Operators = new(StringComparer.Ordinal)
        {
            ["abs"] = Op.Abs, ["add"] = Op.Add, ["atan"] = Op.Atan, ["ceiling"] = Op.Ceiling, ["cos"] = Op.Cos,
            ["cvi"] = Op.Cvi, ["cvr"] = Op.Cvr, ["div"] = Op.Div, ["exp"] = Op.Exp, ["floor"] = Op.Floor,
            ["idiv"] = Op.Idiv, ["ln"] = Op.Ln, ["log"] = Op.Log, ["mod"] = Op.Mod, ["mul"] = Op.Mul,
            ["neg"] = Op.Neg, ["round"] = Op.Round, ["sin"] = Op.Sin, ["sqrt"] = Op.Sqrt, ["sub"] = Op.Sub,
            ["truncate"] = Op.Truncate, ["and"] = Op.And, ["bitshift"] = Op.Bitshift, ["eq"] = Op.Eq,
            ["false"] = Op.False, ["ge"] = Op.Ge, ["gt"] = Op.Gt, ["le"] = Op.Le, ["lt"] = Op.Lt, ["ne"] = Op.Ne,
            ["not"] = Op.Not, ["or"] = Op.Or, ["true"] = Op.True, ["xor"] = Op.Xor, ["copy"] = Op.Copy,
            ["dup"] = Op.Dup, ["exch"] = Op.Exch, ["index"] = Op.Index, ["pop"] = Op.Pop, ["roll"] = Op.Roll
        };

        private int _at;

        /// <summary>Whether the reading stopped at <see cref="MaximumNesting"/>.</summary>
        internal bool TooDeep { get; private set; }

        /// <summary>The whole program, which is one pair of braces, or null where it is not one.</summary>
        internal Instruction[] ReadProgram()
        {
            if (NextToken() != "{")
                return null;

            var program = new List<Instruction>();
            return ReadBlock(program, 1) && NextToken() == null ? program.ToArray() : null;
        }

        // Reads up to the brace that closes the block, the opening one having been read.
        private bool ReadBlock(List<Instruction> into, int depth)
        {
            if (depth > MaximumNesting)
            {
                TooDeep = true;
                return false;
            }

            while (true)
            {
                var token = NextToken();
                switch (token)
                {
                    case null:
                        return false;

                    case "}":
                        return true;

                    case "{":
                        if (!ReadConditional(into, depth))
                            return false;
                        break;

                    default:
                        if (Operators.TryGetValue(token, out var op))
                            into.Add(new Instruction(op));
                        else if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                                 && double.IsFinite(number))
                            into.Add(new Instruction(Op.Push, number));
                        else
                            return false;
                        break;
                }
            }
        }

        // A block in a block is what an if or an ifelse runs: { then } if, or { then } { else } ifelse.
        private bool ReadConditional(List<Instruction> into, int depth)
        {
            var then = new List<Instruction>();
            if (!ReadBlock(then, depth + 1))
                return false;

            var token = NextToken();
            if (token == "if")
            {
                into.Add(new Instruction(Op.JumpIfFalse, skip: then.Count));
                into.AddRange(then);
                return true;
            }

            var otherwise = new List<Instruction>();
            if (token != "{" || !ReadBlock(otherwise, depth + 1) || NextToken() != "ifelse")
                return false;

            into.Add(new Instruction(Op.JumpIfFalse, skip: then.Count + 1));
            into.AddRange(then);
            into.Add(new Instruction(Op.Jump, skip: otherwise.Count));
            into.AddRange(otherwise);
            return true;
        }

        private string NextToken()
        {
            while (_at < source.Length && (IsSpace(source[_at]) || source[_at] == '%'))
            {
                if (source[_at++] == '%')
                {
                    while (_at < source.Length && source[_at] is not ((byte)'\n' or (byte)'\r'))
                        _at++;
                }
            }

            if (_at >= source.Length)
                return null;

            var start = _at++;
            if (source[start] is (byte)'{' or (byte)'}')
                return source[start] == '{' ? "{" : "}";

            while (_at < source.Length && !IsSpace(source[_at]) && source[_at] is not ((byte)'{' or (byte)'}' or (byte)'%'))
                _at++;

            var token = new char[_at - start];
            for (var index = 0; index < token.Length; index++)
                token[index] = (char)source[start + index];
            return new string(token);
        }

        private static bool IsSpace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;
    }
}
