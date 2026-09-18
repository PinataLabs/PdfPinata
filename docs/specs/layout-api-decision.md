# Decision — what to do about the authoring API

Gap **G8** of `autoresearch/improve-260816-1032/improvement-plan.md`. This is a decision note, not a
proposal: the choice is unmade, and it is the largest strategic call in the gap analysis. Nothing here
is built and nothing should be until the question below is answered.

**The question.** MigraDoc's document model is a word-processor flow model. QuestPDF's is a constraint
solver. New users overwhelmingly want the second. Do we build one, or make the first pleasanter to use?

---

## What is actually missing

Not features — a *model*. MigraDoc lays out a stream of block elements down a page, in order, and asks
each how tall it is. That is the right model for a report and the wrong one for a layout.

Two categories have no MigraDoc analogue at all.

**Sizing and positioning.** QuestPDF has `Width` and `Height` with **exact, minimum and maximum**
variants, `AspectRatio`, `ScaleToFit`, `Extend`, `Shrink`, `Unconstrained`, `ZIndex`, `Layers`,
`Inlined`, `MultiColumn`, `Flip`, and `Rotate` by an arbitrary angle. MigraDoc has a width, a height,
and a position relative to something.

**Pagination control.** QuestPDF has `PreventPageBreak`, `EnsureSpace`, `ShowEntire`, `Repeat`,
`ShowOnce`, `SkipOnce`, `StopPaging` and `ShowIf`. MigraDoc has `KeepWithNext`, `KeepTogether` and an
explicit `PageBreak`. "Put this table on one page or start it on the next" is a one-word element there
and a manual measurement here.

The published comparisons put it plainly: PdfSharp gives *"lower-level control but requires
significantly more code for equivalent layouts."* That is the whole finding.

It is worth being precise about what is **not** missing, because this fork has moved: `Drawing.Layout/`
already has multi-column text, flow-around obstacles, drop caps, text flow regions and variable line
measure — several of which QuestPDF does not have. The gap is in *composition*, not in text layout.

---

## The three options

### Option A — a new parallel package, `PdfPinata.Layout`

A composable element tree over `XGraphics`, sitting beside MigraDoc rather than replacing it.

The core is a two-phase protocol, the same one QuestPDF and Flutter use:

```csharp
SpacePlan Measure(Size available);      // what would you do with this much room?
void      Draw(ICanvas canvas, Size size);
```

`SpacePlan` is the load-bearing idea, and it has three answers, not two:

| answer | means |
|---|---|
| `Wrap` | does not fit at all — move me to the next page |
| `PartialRender(size)` | some of me fits; draw that and ask again on the next page |
| `FullRender(size)` | all of me fits |

**Pagination falls out of that distinction** rather than being special-cased per element type, which is
exactly what MigraDoc cannot do: its renderers each know how to break themselves, so every new element
re-solves the problem. Element set: `Container`, `Row`, `Column`, `Constrained`, `Padding`,
`Alignment`, `Background`, `Border`, `Text`, `Image`, `Table`, `Decoration`, `Layers`, `PageBreak`.

Because it draws onto the existing `XGraphics`, everything already built comes along unchanged —
barcodes, charting, `XTextFormatter` with its obstacles and drop caps, gradients, transparency.

**Effort: 10–16 engineer-weeks** for a credible subset. **Breaks nothing.**

### Option B — retrofit constraints into the MigraDoc DOM

**Rejected.** `PinataLayout.Rendering` assumes a linear flow throughout — the renderers, the paragraph
iterator, the table renderer with its repeating headings, and the charting renderers that sit on top.
Constraint propagation would move all of them. 12–20 weeks to arrive somewhere worse than Option A,
while breaking every existing MigraDoc user. The only argument for it is having one API instead of two,
and that is not worth this price.

### Option C — a fluent façade over MigraDoc

Keep the model, fix the verbosity:

```csharp
doc.Section(s => s
    .Heading("Invoice 2026-0042")
    .Table(t => t.Columns(3.Cm(), 8.Cm(), 3.Cm())
                 .HeaderRow("Item", "Description", "Amount")
                 .Rows(lines, l => [l.Code, l.Text, l.Amount.ToString("C")])));
```

**Effort: 3–4 engineer-weeks.** Genuinely useful — most of the "too much code" complaint is ceremony,
not model — and it does nothing whatever about constraints or pagination control.

---

## The recommendation

**Do Option C now. Decide on Option A after the compliance work ships.**

Option C is four weeks, breaks nothing, addresses the loudest half of the complaint, and — the real
argument — **buys time to answer the question with evidence instead of taste.** Ship the fluent façade,
see whether the remaining complaints are about ceremony or about layout, and let that decide whether
Option A is worth a quarter.

Three things argue for waiting on Option A specifically:

1. **It should be built on shaped text.** Building a new text-bearing API on
   `docs/specs/text-shaping-and-bidi.md`'s "before" state means rebuilding its measurement layer
   afterwards. Sequence it after, not beside.
2. **It competes for the same quarter as shaping**, and both are quarter-plus commitments. One engineer
   cannot do both, and shaping serves an audience that currently *cannot use the library at all*, while
   layout serves one that finds it verbose.
3. **Two authoring APIs is a permanent documentation and support cost.** Worth paying if it wins users;
   not worth paying speculatively.

The counter-argument deserves stating fairly: layout is the ground QuestPDF actually competes on, it is
the first thing a new user evaluates, and compliance features win procurement while API quality wins
adoption. If the goal is new users rather than qualified deals, Option A is the higher-value bet and
should go first.

---

## What would settle it

- Do the existing issues and questions ask for *less code* or for *layout control*? That distinction is
  the decision, and the issue tracker already has the data.
- After Option C ships, does the complaint change shape?
- Is anyone asking for both MigraDoc and a new API in the same document? If so, Option A needs an
  interop story — probably "a MigraDoc `Document` renders into a `Layout` element" — and that changes
  the estimate.

## Open questions if Option A is chosen

- **Does it replace MigraDoc or sit beside it?** Beside, initially. Replacing means porting charting
  and every renderer.
- **Does MigraDoc get deprecated?** It should not be, for a long time. It renders things the new model
  would take years to reach — footnotes, MDDDL, the whole word-processor lineage.
- **Tagged output.** A new API must be taggable from day one, or it ships already behind
  `docs/specs/tagged-pdf-accessibility.md`. The `Measure`/`Draw` protocol makes this easier than
  MigraDoc does — an element knows its own semantics — so this is an argument *for* Option A, not
  against.

## Related

- `docs/specs/text-shaping-and-bidi.md` — sequence this after it.
- `openspec/specs/text-flow-regions`, `drop-cap`, `shape-side-wrap` — what `Drawing.Layout` already
  does, and what a new API would compose rather than replace.
