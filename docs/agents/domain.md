# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`GLOSSARY.md`** at the repo root.
- **`docs/specs/`**: this repo's equivalent of ADRs. Read the specs that touch the area you're about to work in (see below).
- **`docs/adr/`**: read ADRs that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## Design notes in `docs/specs/`

This repo records its decisions in `docs/specs/*.md` rather than in numbered ADRs. Each note covers
one feature area and is tied to an upstream issue number. It records what was built, why it is built
that way, and what was deliberately left out. Treat these notes as ADRs: read the relevant one before
extending that feature area, and flag a conflict with one the same way you would flag an ADR conflict
(below). `CLAUDE.md` names the note to read for most areas.

## File structure

Single-context repo:

```
/
├── GLOSSARY.md
├── docs/
│   ├── adr/
│   └── specs/          ← design notes, one per feature area
└── src/
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `GLOSSARY.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR or design note, surface it explicitly rather than silently overriding:

> _Contradicts `docs/specs/field-and-widget-model.md` (a field and its widget are always separate objects), but worth reopening because…_
