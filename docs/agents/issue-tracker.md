# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues on **PinataLabs/PdfPinata**. Use the `gh` CLI
for all operations.

**Always name the repo.** This clone has two remotes: `origin` is the fork (jafin/PdfPinata) and
`upstream` is PinataLabs/PdfPinata. No `gh` default repo is set, so a bare `gh issue` call either asks
which one to use or lands on the fork. Pass `-R PinataLabs/PdfPinata` to every `gh issue`/`gh pr`
call, and write every `gh api` path as `repos/PinataLabs/PdfPinata/...`.

## Conventions

- **Create an issue**: `gh issue create -R PinataLabs/PdfPinata --title "..." --body-file <file>`. Write multi-line bodies to a file first.
- **Read an issue**: `gh issue view <number> -R PinataLabs/PdfPinata --json number,title,body,labels,comments`.
- **List issues**: `gh issue list -R PinataLabs/PdfPinata --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Make an issue a sub-issue of a parent**: `gh issue create -R PinataLabs/PdfPinata --parent <parent> ...`, or `gh issue edit <parent> -R PinataLabs/PdfPinata --add-sub-issue <child>` afterwards (`gh` 2.94+). Fallback: `gh api --method POST repos/PinataLabs/PdfPinata/issues/<parent>/sub_issues -F sub_issue_id=<child-db-id>` (database id, as in **Blocking** below).
- **Comment on an issue**: `gh issue comment <number> -R PinataLabs/PdfPinata --body "..."`
- **Apply / remove labels**: `gh issue edit <number> -R PinataLabs/PdfPinata --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> -R PinataLabs/PdfPinata --comment "..."`

## Pull requests

Push the branch to `origin` (the fork) and open the PR on PinataLabs/PdfPinata:
`gh pr create -R PinataLabs/PdfPinata --head jafin:<branch>`. The **PR title** takes a Conventional
Commits prefix (`feat:`, `fix:`, `feat!:` …), because Release Drafter labels PRs by it; commit
subjects stay plain prose (see `CLAUDE.md`).

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> -R PinataLabs/PdfPinata --comments` and `gh pr diff <number> -R PinataLabs/PdfPinata` for the diff.
- **List external PRs for triage**: `gh api --paginate 'repos/PinataLabs/PdfPinata/pulls?state=open' --jq '.[] | select(.author_association | IN("OWNER","MEMBER","COLLABORATOR") | not) | {number, title, author: .user.login, author_association, labels: [.labels[].name]}'`.
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`, each with `-R PinataLabs/PdfPinata`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either: resolve with `gh pr view 42 -R PinataLabs/PdfPinata` and fall back to `gh issue view 42 -R PinataLabs/PdfPinata`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue on PinataLabs/PdfPinata.

## When a skill says "fetch the relevant ticket"

Read it as in **Read an issue** above.

## Wayfinding operations

Used by `/wayfinder`. The **map** is a single issue with **child** issues as tickets.

- **Map**: a single issue labelled `wayfinder:map`, holding the Notes / Decisions-so-far / Fog body. `gh issue create -R PinataLabs/PdfPinata --label wayfinder:map`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue (see **Make an issue a sub-issue of a parent**). Labels: `wayfinder:<type>` (`research`/`prototype`/`grilling`/`task`). Once claimed, the ticket is assigned to the driving dev.
- **Blocking**: GitHub's **native issue dependencies**, the canonical, UI-visible representation. Add an edge with `gh api --method POST repos/PinataLabs/PdfPinata/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`, where `<blocker-db-id>` is the blocker's numeric **database id** (`gh api repos/PinataLabs/PdfPinata/issues/<n> --jq .id`, _not_ the `#number` or `node_id`). The POST answers with the **blocked** issue, so its `.number` is `<child>`, not the blocker. To check the edges, read them back with `gh api repos/PinataLabs/PdfPinata/issues/<child>/dependencies/blocked_by --jq '[.[].number]'`. GitHub reports `issue_dependencies_summary.blocked_by` (open blockers only, the live gate). A ticket is unblocked when every blocker is closed.
- **Frontier query**: list the map's open children (`gh issue list -R PinataLabs/PdfPinata --state open`, scoped to the map's sub-issues), drop any with an open blocker (`issue_dependencies_summary.blocked_by > 0`) or an assignee; first in map order wins.
- **Claim**: `gh issue edit <n> -R PinataLabs/PdfPinata --add-assignee @me`, the session's first write.
- **Resolve**: `gh issue comment <n> -R PinataLabs/PdfPinata --body "<answer>"`, then `gh issue close <n> -R PinataLabs/PdfPinata`, then append a context pointer (gist + link) to the map's Decisions-so-far.
