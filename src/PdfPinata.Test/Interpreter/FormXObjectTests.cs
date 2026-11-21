using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Form XObjects, and the two things a <c>Do</c> may name that are not drawn yet.
/// </summary>
public class FormXObjectTests
{
    [Test]
    public void AFormIsDrawnUnderItsMatrixAndClippedToItsBox()
    {
        var drawn = Drawn.Page("1 0 0 1 10 0 cm /Fm1 Do", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            form.Elements.SetMatrix("/Matrix", new XMatrix(2, 0, 0, 2, 0, 20));
            form.Elements["/BBox"] = new PdfRectangle(new XRect(0, 0, 10, 10));
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        // The form's space is doubled and raised by 20, inside a page space moved right by 10.
        drawn.Device.Body.Should().Equal(
            "save",
            "clip nonzero M10,80 L30,80 L30,60 L10,60 Z",
            "fill nonzero M10,80 L20,80 L20,70 L10,70 Z rgb(0,0,0)",
            "restore");
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void WhatAFormChangesInTheGraphicsStateIsUndoneWhenItEnds()
    {
        var drawn = Drawn.Page("/Fm1 Do 0 0 5 5 re f", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "1 0 0 rg 3 0 0 3 0 0 cm q q")));

        drawn.Device.Fills.Single().ToString().Should().Be("fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)");
        drawn.Device.Calls.Count(call => call.Kind == "save")
            .Should().Be(drawn.Device.Calls.Count(call => call.Kind == "restore"));
    }

    [Test]
    public void ARestoreInAFormCannotReachPastWhereTheFormBegan()
    {
        var drawn = Drawn.Page("q 1 0 0 rg /Fm1 Do 0 0 5 5 re f Q", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "Q Q Q")));

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0), "the page's own saved state is still the page's");
        drawn.Problems.Should().Equal("OperatorOutOfPlace Q");
        drawn.Result.Diagnostics.Single().Stream.Should().Be("page/Fm1");
    }

    [Test]
    public void AFormStartsFromTheGraphicsStateInForceWhereItIsDrawn()
    {
        var drawn = Drawn.Page("0 1 0 rg /Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f")));

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(0, 1, 0));
    }

    [Test]
    public void AFormLooksNamesUpInItsOwnResources()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            var inner = Drawn.Form(document, "1 0 0 rg 0 0 5 5 re f");
            var outer = Drawn.Form(document, "/Inner Do");
            var own = new PdfDictionary(document);
            outer.Elements["/Resources"] = own;
            Drawn.Add(document, own, "/XObject", "/Inner", inner);
            Drawn.Add(document, resources, "/XObject", "/Fm1", outer);
        });

        drawn.Device.Fills.Single().Paint.Should().Be(new PagePaint(1, 0, 0));
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AFormWithNoResourcesOfItsOwnUsesThoseOfWhateverDrewIt()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            Drawn.Add(document, resources, "/XObject", "/Inner", Drawn.Form(document, "0 0 5 5 re f"));
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "/Inner Do"));
        });

        drawn.Device.Fills.Should().ContainSingle();
    }

    // On a thread of its own and under a timeout: a guard that stopped guarding would recurse
    // until the stack ran out, which ends the test host and fails no test.
    [Test, Timeout(30_000)]
    public async Task AFormThatDrawsItselfIsDrawnOnceAndReported()
    {
        var drawn = await Interruptibly.Run(() => Drawn.Page("/Fm1 Do", (document, resources) =>
            Drawn.Add(document, resources, "/XObject", "/Fm1", Drawn.Form(document, "0 0 5 5 re f /Fm1 Do"))));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("LimitReached /Fm1");
    }

    [Test]
    public void AFormWithNoBoxIsDrawnUnclippedAndReported()
    {
        var drawn = Drawn.Page("/Fm1 Do", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 5 5 re f");
            form.Elements.Remove("/BBox");
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Body.Should().Equal("save", "fill nonzero M0,100 L5,100 L5,95 L0,95 Z rgb(0,0,0)", "restore");
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
    }

    [Test]
    public void AFormWhoseMatrixIsNotSixNumbersIsNotDrawn()
    {
        var drawn = Drawn.Page("/Fm1 Do 0 0 5 5 re f", (document, resources) =>
        {
            var form = Drawn.Form(document, "0 0 50 50 re f");
            form.Elements["/Matrix"] = new PdfArray(document, new PdfReal(1), new PdfReal(0));
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Problems.Should().Equal("InvalidValue /Fm1");
    }

    private static Drawn GroupDrawnUnder(string state, Action<PdfDictionary> group = null) =>
        Drawn.Page("/GS1 gs /Fm1 Do", (document, resources) =>
        {
            var parameters = new PdfDictionary(document);
            foreach (var entry in state.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                parameters.Elements.SetReal(entry.Split('=')[0], double.Parse(entry.Split('=')[1], System.Globalization.CultureInfo.InvariantCulture));
            Drawn.Add(document, resources, "/ExtGState", "/GS1", parameters);

            var form = Drawn.Form(document, "0 0 50 50 re f 25 25 50 50 re f");
            var transparency = new PdfDictionary(document);
            transparency.Elements.SetName("/S", "/Transparency");
            group?.Invoke(transparency);
            form.Elements["/Group"] = transparency;
            Drawn.Add(document, resources, "/XObject", "/Fm1", form);
        });

    [Test]
    [Arguments("/ca=0.5")]
    [Arguments("/CA=0.5")]
    public void ATransparencyGroupDrawnAtAnAlphaIsReportedBecauseItIsDrawnPieceByPiece(string state)
    {
        var drawn = GroupDrawnUnder(state);

        // Two fills that overlap, each at the alpha, where a group is one thing at the alpha.
        drawn.Device.Fills.Should().HaveCount(2);
        drawn.Problems.Should().Equal("UnsupportedFeature /Group");
        drawn.Result.IsComplete.Should().BeFalse();
    }

    [Test]
    public void ATransparencyGroupDrawnWithNoAlphaIsDrawnAsAGroupWouldBeAndIsNotReported()
    {
        var drawn = GroupDrawnUnder("/ca=1 /CA=1");

        drawn.Device.Fills.Should().HaveCount(2);
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AKnockoutGroupIsReportedWhateverTheAlpha()
    {
        var drawn = GroupDrawnUnder("/ca=1", group => group.Elements.SetBoolean("/K", true));

        drawn.Problems.Should().Equal("UnsupportedFeature /Group");
    }

    /// <summary>
    /// A page whose content is in the optional content group <c>/L1</c>, with a second group
    /// <c>/L2</c> beside it, in a document whose default configuration is <paramref name="configure"/>d.
    /// </summary>
    internal static Drawn Layered(string content, Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> configure,
        Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> more = null,
        Func<PageOptionalContent> optionalContent = null) =>
        Drawn.Page(content, (document, resources) =>
        {
            var first = Group(document);
            var second = Group(document);
            Drawn.Add(document, resources, "/Properties", "/L1", first);
            Drawn.Add(document, resources, "/Properties", "/L2", second);

            var configuration = new PdfDictionary(document);
            configure(document, configuration, first, second);
            var properties = new PdfDictionary(document);
            properties.Elements["/OCGs"] = new PdfArray(document, first.Reference, second.Reference);
            properties.Elements["/D"] = configuration;
            document.Internals.Catalog.Elements["/OCProperties"] = properties;
            more?.Invoke(document, resources, first, second);
        }, optionalContent: optionalContent);

    internal static PdfDictionary Group(PdfDocument document)
    {
        var group = new PdfDictionary(document);
        group.Elements.SetName("/Type", "/OCG");
        document.Internals.AddObject(group);
        return group;
    }

    private const string InFirstGroup = "/OC /L1 BDC 0 0 5 5 re f EMC";

    [Test]
    public void ContentOfAGroupTheDocumentSwitchesOffIsNotDrawnAndNotReported()
    {
        var drawn = Layered(InFirstGroup, (document, configuration, first, _) =>
            configuration.Elements["/OFF"] = new PdfArray(document, first.Reference));

        drawn.Device.Fills.Should().BeEmpty();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void ContentOfAGroupThatIsOnIsDrawn()
    {
        var drawn = Layered(InFirstGroup, (document, configuration, _, second) =>
            configuration.Elements["/OFF"] = new PdfArray(document, second.Reference));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void WhereEveryGroupStartsOffOnlyTheGroupsListedAreOn()
    {
        var on = Layered(InFirstGroup, (document, configuration, first, _) =>
        {
            configuration.Elements.SetName("/BaseState", "/OFF");
            configuration.Elements["/ON"] = new PdfArray(document, first.Reference);
        });
        var off = Layered(InFirstGroup, (_, configuration, _, _) => configuration.Elements.SetName("/BaseState", "/OFF"));

        on.Device.Fills.Should().ContainSingle();
        off.Device.Fills.Should().BeEmpty();
        off.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    [Arguments(null, "/L1", true)]
    [Arguments(null, "/L1 /L2", false)]
    [Arguments("/AnyOn", "/L1 /L2", false)]
    [Arguments("/AllOn", "/L1 /L2", true)]
    [Arguments("/AnyOff", "/L1 /L2", false)]
    [Arguments("/AnyOff", "/L2", true)]
    [Arguments("/AllOff", "/L1", false)]
    [Arguments("/AllOff", "/L1 /L2", true)]
    public void AMembershipDictionaryIsOffByItsPolicyOverItsGroups(string policy, string groups, bool hidden)
    {
        // The first group is off and the second is on.
        var drawn = Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, second) =>
            {
                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/OCGs"] = new PdfArray(document,
                    groups.Split(' ').Select(name => (PdfItem)(name == "/L1" ? first : second).Reference).ToArray());
                if (policy != null)
                    membership.Elements.SetName("/P", policy);
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

        drawn.Device.Fills.Should().HaveCount(hidden ? 0 : 1);
        drawn.Result.IsComplete.Should().BeTrue("hidden content is left out and not reported");
    }

    [Test]
    [Arguments("/Not", false, false)]
    [Arguments("/Not", true, true)]
    [Arguments("/And", false, true)]
    [Arguments("/Or", false, false)]
    public void AMembershipDictionaryWithAVisibilityExpressionIsOffByTheExpression(string operation, bool ofTheGroupThatIsOn,
        bool hidden)
    {
        // The first group is off and the second is on. The policy says visible, and is not used.
        var drawn = Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, second) =>
            {
                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/OCGs"] = second.Reference;
                membership.Elements["/VE"] = operation == "/Not"
                    ? new PdfArray(document, new PdfName("/Not"), (ofTheGroupThatIsOn ? second : first).Reference)
                    : new PdfArray(document, new PdfName(operation), first.Reference,
                        new PdfArray(document, new PdfName("/Not"), new PdfArray(document, new PdfName("/Not"), second.Reference)));
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

        drawn.Device.Fills.Should().HaveCount(hidden ? 0 : 1);
        drawn.Result.IsComplete.Should().BeTrue("hidden content is left out and not reported");
    }

    [Test, Timeout(30_000)]
    public async Task AVisibilityExpressionThatNamesItselfTwiceIsReadToALimitAndNoFurther()
    {
        // [/And self self]: every level asks about two more, and nothing ever answers false,
        // so read to its depth alone this is two to the thirty-second questions.
        var drawn = await Interruptibly.Run(() => Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (_, _, _, _) => { },
            (document, resources, _, _) =>
            {
                var expression = new PdfArray(document);
                document.Internals.AddObject(expression);
                expression.Elements.Add(new PdfName("/And"));
                expression.Elements.Add(expression.Reference);
                expression.Elements.Add(expression.Reference);

                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/VE"] = expression.Reference;
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue("an expression that cannot be read to its end is taken to be visible");
    }

    /// <summary>A page whose content is in the membership dictionary <c>/M</c>, whose <c>/VE</c> <paramref name="expression"/> builds.</summary>
    private static Drawn WithExpression(string content, Func<PdfDocument, PdfDictionary, PdfDictionary, PdfItem> expression) =>
        Layered(content,
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, second) =>
            {
                var membership = new PdfDictionary(document);
                membership.Elements.SetName("/Type", "/OCMD");
                membership.Elements["/VE"] = expression(document, first, second);
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

    private static PdfArray Expression(PdfDocument document, params PdfItem[] items) => new(document, items);

    private static PdfDictionary Membership(PdfDocument document)
    {
        var membership = new PdfDictionary(document);
        membership.Elements.SetName("/Type", "/OCMD");
        return membership;
    }

    [Test, Timeout(30_000)]
    [Arguments("an operation that is not one")]
    [Arguments("an operand that is neither a group nor an expression")]
    [Arguments("a not of two operands")]
    [Arguments("a malformed operand after one that already decides")]
    [Arguments("an operand that is a membership dictionary rather than a group")]
    public async Task AVisibilityExpressionWithAPartThatCannotBeReadIsVisibleEvenUnderANot(string malformed)
    {
        // The first group is off and the second is on, so each of these reads as hidden were the
        // malformed part taken to be visible and then inverted.
        var drawn = await Interruptibly.Run(() => WithExpression("/OC /M BDC 0 0 5 5 re f EMC", (document, first, second) =>
            malformed switch
            {
                "an operation that is not one" =>
                    Expression(document, new PdfName("/Not"), Expression(document, new PdfName("/Xor"), second.Reference)),
                "an operand that is neither a group nor an expression" =>
                    Expression(document, new PdfName("/Not"), Expression(document, new PdfName("/And"), second.Reference, new PdfReal(7))),
                "a not of two operands" =>
                    Expression(document, new PdfName("/And"), second.Reference,
                        Expression(document, new PdfName("/Not"), second.Reference, first.Reference)),
                "an operand that is a membership dictionary rather than a group" =>
                    Expression(document, new PdfName("/Not"), Membership(document)),
                _ => Expression(document, new PdfName("/And"), first.Reference, new PdfName("/Nope"))
            }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue("an expression with a part that cannot be read is visible");
    }

    /// <summary>
    /// An <c>/And</c> over the level below twice, <paramref name="levels"/> deep over the group
    /// that is on: two to the power of one more than that, less two, operands to read.
    /// </summary>
    private static PdfItem Doubling(PdfDocument document, PdfDictionary group, int levels)
    {
        PdfItem level = group.Reference;
        for (var depth = 0; depth < levels; depth++)
        {
            var expression = Expression(document, new PdfName("/And"), level, level);
            document.Internals.AddObject(expression);
            level = expression.Reference;
        }

        return level;
    }

    [Test, Timeout(30_000)]
    [Arguments("deeper than the limit")]
    [Arguments("longer than the operand budget")]
    public async Task ANotOverAnExpressionPastALimitIsVisible(string past)
    {
        // [/Not [/And self self]] is never read to its end, and [/Not <ten doublings>] asks for
        // 2,046 operands. What cannot be read to its end is visible whatever stands over it.
        var drawn = await Interruptibly.Run(() => WithExpression("/OC /M BDC 0 0 5 5 re f EMC", (document, _, second) =>
        {
            if (past == "longer than the operand budget")
                return Expression(document, new PdfName("/Not"), Doubling(document, second, 10));

            var inner = new PdfArray(document);
            document.Internals.AddObject(inner);
            inner.Elements.Add(new PdfName("/And"));
            inner.Elements.Add(inner.Reference);
            inner.Elements.Add(inner.Reference);
            return Expression(document, new PdfName("/Not"), inner.Reference);
        }));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test, Timeout(120_000)]
    [NotInParallel]
    public async Task AMembershipIsReadOncePerRenderHoweverOftenContentNamesIt()
    {
        // 40,000 runs in one membership whose expression is a group and nine levels of /And
        // over the level below twice: 1,022 operands, every one readable, read in full each
        // time it is read. Against as many runs in a plain group, which costs nothing to ask about.
        var runs = string.Concat(Enumerable.Repeat("/OC /M BDC EMC ", 40_000));
        Drawn Plain() => Layered(runs.Replace("/M", "/L1"), (_, _, _, _) => { });
        Drawn Doubled() => Layered(runs, (_, _, _, _) => { }, (document, resources, _, second) =>
        {
            var membership = new PdfDictionary(document);
            membership.Elements.SetName("/Type", "/OCMD");
            membership.Elements["/VE"] = Doubling(document, second, 9);
            Drawn.Add(document, resources, "/Properties", "/M", membership);
        });

        // Read once a run, the expression costs about a hundred times the plain group. The two are
        // timed in turn, so that a stall on a loaded machine falls on both alike.
        var (plain, doubling) = await Interruptibly.Run(() =>
        {
            var fastest = (Plain: TimeSpan.MaxValue, Doubling: TimeSpan.MaxValue);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Plain();
                if (watch.Elapsed < fastest.Plain)
                    fastest.Plain = watch.Elapsed;

                watch.Restart();
                Doubled();
                if (watch.Elapsed < fastest.Doubling)
                    fastest.Doubling = watch.Elapsed;
            }

            return fastest;
        });

        doubling.Should().BeLessThan(plain * 10, $"the expression is read once, not once a run, and a plain group took {plain}");
    }

    [Test]
    [Arguments(null, "/Design", false)]
    [Arguments(null, null, true)]
    [Arguments(null, "[/View /Design]", true)]
    [Arguments("/Design", "/Design", true)]
    [Arguments("/Design", null, false)]
    [Arguments("[/View /Design]", "/Design", true)]
    [Arguments("/All", "/Design", true)]
    [Arguments("[/View /All]", "/Whatever", true)]
    public void AGroupCountsOnlyWhereTheConfigurationNamesOneOfItsIntents(string configured, string ofTheGroup, bool hidden)
    {
        static PdfItem Intent(PdfDocument document, string intent) => intent.StartsWith('[')
            ? new PdfArray(document, intent.Trim('[', ']').Split(' ').Select(name => (PdfItem)new PdfName(name)).ToArray())
            : new PdfName(intent);

        var drawn = Layered(InFirstGroup, (document, configuration, first, _) =>
        {
            configuration.Elements["/OFF"] = new PdfArray(document, first.Reference);
            if (configured != null)
                configuration.Elements["/Intent"] = Intent(document, configured);
            if (ofTheGroup != null)
                first.Elements["/Intent"] = Intent(document, ofTheGroup);
        });

        drawn.Device.Fills.Should().HaveCount(hidden ? 0 : 1);
        drawn.Result.IsComplete.Should().BeTrue("hidden content is left out and not reported");
    }

    /// <summary>
    /// A page whose content is in the membership dictionary <c>/M</c>, whose groups and policy or
    /// expression <paramref name="fill"/> writes. The first group is off and the second is on.
    /// </summary>
    private static Drawn WithMembership(Action<PdfDocument, PdfDictionary> configure, Action<PdfDocument, PdfDictionary, PdfDictionary, PdfDictionary> fill) =>
        Layered("/OC /M BDC 0 0 5 5 re f EMC",
            (document, configuration, first, _) =>
            {
                configuration.Elements["/OFF"] = new PdfArray(document, first.Reference);
                configure(document, configuration);
            },
            (document, resources, first, second) =>
            {
                var membership = Membership(document);
                fill(document, membership, first, second);
                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

    private static PdfDictionary DesignGroup(PdfDocument document)
    {
        var group = Group(document);
        group.Elements.SetName("/Intent", "/Design");
        return group;
    }

    [Test]
    [Arguments("/AllOff over the group that is on, where the configuration names no intent", false)]
    [Arguments("/AnyOff over the group that is on, where the configuration names no intent", false)]
    [Arguments("[/Not onGroup], where the configuration names no intent", false)]
    [Arguments("/AllOff over a /Design group listed /OFF", false)]
    [Arguments("/AllOff over the group that is off and a /Design group", false)]
    [Arguments("[/Not designGroup]", false)]
    [Arguments("/AnyOn over the group that is off and a /Design group", true)]
    [Arguments("[/Or offGroup designGroup]", true)]
    public void AGroupThatDoesNotCountIsLeftOutRatherThanReadAsOn(string membership, bool hidden)
    {
        // ISO 32000-1 8.11.2.3: a group none of whose intents the configuration names has no
        // effect on visibility, and an empty configuration /Intent makes all content visible.
        // Read as on, such a group turns hidden under a negating policy or a /Not; left out, a
        // membership with nothing left has no effect, as one that names no group has none.
        var noIntent = membership.EndsWith("names no intent", StringComparison.Ordinal);
        var drawn = WithMembership(
            (document, configuration) =>
            {
                if (noIntent)
                    configuration.Elements["/Intent"] = new PdfArray(document);
            },
            (document, m, first, second) =>
            {
                switch (membership)
                {
                    case "/AllOff over the group that is on, where the configuration names no intent":
                        m.Elements["/OCGs"] = second.Reference;
                        m.Elements.SetName("/P", "/AllOff");
                        break;
                    case "/AnyOff over the group that is on, where the configuration names no intent":
                        m.Elements["/OCGs"] = second.Reference;
                        m.Elements.SetName("/P", "/AnyOff");
                        break;
                    case "[/Not onGroup], where the configuration names no intent":
                        m.Elements["/VE"] = Expression(document, new PdfName("/Not"), second.Reference);
                        break;
                    case "/AllOff over a /Design group listed /OFF":
                        first.Elements.SetName("/Intent", "/Design");
                        m.Elements["/OCGs"] = first.Reference;
                        m.Elements.SetName("/P", "/AllOff");
                        break;
                    case "/AllOff over the group that is off and a /Design group":
                        m.Elements["/OCGs"] = new PdfArray(document, first.Reference, DesignGroup(document).Reference);
                        m.Elements.SetName("/P", "/AllOff");
                        break;
                    case "[/Not designGroup]":
                        m.Elements["/VE"] = Expression(document, new PdfName("/Not"), DesignGroup(document).Reference);
                        break;
                    case "/AnyOn over the group that is off and a /Design group":
                        m.Elements["/OCGs"] = new PdfArray(document, first.Reference, DesignGroup(document).Reference);
                        m.Elements.SetName("/P", "/AnyOn");
                        break;
                    default:
                        m.Elements["/VE"] = Expression(document, new PdfName("/Or"), first.Reference, DesignGroup(document).Reference);
                        break;
                }
            });

        drawn.Device.Fills.Should().HaveCount(hidden ? 0 : 1);
        drawn.Result.IsComplete.Should().BeTrue("hidden content is left out and not reported");
    }

    private static PdfDictionary Typed(PdfDocument document, string type)
    {
        var dictionary = new PdfDictionary(document);
        dictionary.Elements.SetName("/Type", type);
        return dictionary;
    }

    [Test]
    [Arguments("named by /OC")]
    [Arguments("in /OCGs under a policy")]
    [Arguments("in /OCGs beside the group that is off")]
    [Arguments("an operand of /VE")]
    [Arguments("a membership dictionary in /OCGs")]
    [Arguments("a membership dictionary operand of /VE")]
    public void ADictionaryThatIsNotAGroupIsVisibleWhereverItIsNamed(string where)
    {
        // Every group starts off, so anything read as a group, or skipped while the group that
        // is off is not, would be reported. A policy and an expression read it alike.
        var drawn = Layered(where == "named by /OC" ? "/OC /F BDC 0 0 5 5 re f EMC" : "/OC /M BDC 0 0 5 5 re f EMC",
            (_, configuration, _, _) => configuration.Elements.SetName("/BaseState", "/OFF"),
            (document, resources, first, _) =>
            {
                var other = where.StartsWith("a membership", StringComparison.Ordinal) ? Membership(document) : Typed(document, "/Foo");
                var membership = Membership(document);
                switch (where)
                {
                    case "named by /OC":
                        Drawn.Add(document, resources, "/Properties", "/F", other);
                        return;
                    case "in /OCGs beside the group that is off":
                        membership.Elements["/OCGs"] = new PdfArray(document, first.Reference, other);
                        break;
                    case "in /OCGs under a policy" or "a membership dictionary in /OCGs":
                        membership.Elements["/OCGs"] = new PdfArray(document, other);
                        break;
                    default:
                        membership.Elements["/VE"] = Expression(document, new PdfName("/Or"), other);
                        break;
                }

                Drawn.Add(document, resources, "/Properties", "/M", membership);
            });

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AFormThatBelongsToAGroupSwitchedOffIsNotDrawnAndNotReported()
    {
        var drawn = Layered("/Fm1 Do",
            (document, configuration, first, _) => configuration.Elements["/OFF"] = new PdfArray(document, first.Reference),
            (document, resources, first, _) =>
            {
                var form = Drawn.Form(document, "0 0 5 5 re f");
                form.Elements["/OC"] = first.Reference;
                Drawn.Add(document, resources, "/XObject", "/Fm1", form);
            });

        drawn.Device.Fills.Should().BeEmpty();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void MarkedContentInADocumentWithNoOptionalContentIsDrawn()
    {
        var drawn = Drawn.Page("/OC /L1 BDC /Span << /ActualText (a) >> BDC 0 0 5 5 re f EMC EMC", (document, resources) =>
            Drawn.Add(document, resources, "/Properties", "/L1", Group(document)));

        drawn.Device.Fills.Should().ContainSingle();
        drawn.Result.IsComplete.Should().BeTrue();
    }

    [Test]
    public void AnXObjectTheResourcesDoNotHoldIsReported()
    {
        Drawn.Page("/Nope Do").Problems.Should().Equal("MissingResource /Nope");
    }
}
