---
type: Reference
title: The Notion workspace — identifiers and schemas
description: Page and database IDs for the Node workspace, and the property schemas for Phases and Tasks. Needed only by /update and /audit.
tags: [notion, reference, planning]
generated: { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
status: draft
---

# The Notion workspace

Read this when running `/update` or `/audit`, and at no other time. Nothing
else in the repo needs these identifiers, which is why they do not live in
`CLAUDE.md`.

Notion is written **only** during `/update`. Notion content is never copied
into the repo, and repo content is never restated in Notion.

## Identifiers

- Page **Node** — `3cde745f-f2df-8139-8e3d-e93fac741de0`
- DB **Phases** — `5b7c3aa7-93a1-4eb3-ba25-29b86e743514`
  data source `3664290f-3ad4-4308-8deb-d4c5b057090a`
- DB **Tasks** — `8706a60b-0572-4fb7-b933-2c48c275607d`
  data source `b8f545c7-316b-4d66-9d2e-19f42d5d27f5`
- View **Tasks · Open** — `ee6d187b-368d-473a-bd80-8b75ccf84958`
- View **Tasks · Loose** — `3cde745f-f2df-814a-bfcf-000c6ae4e1d7`
- View **Tasks · Quick wins** — `3cde745f-f2df-81ab-8526-000cd4865ae4`
- View **Phases · Roadmap** — `3cde745f-f2df-816a-ab45-000cac100ef7`

## Phases

Books and chapters. A *Book* is a long-lived area (Networking, Drafting) and
carries no status. A *Chapter* is a specific system or panel and carries the
plan, status, and tasks. The plan lives in the page body, never in properties.

| Property | Type | Values |
|---|---|---|
| `Phase` | Title | — |
| `Type` | Select | `Book` · `Chapter` |
| `Status` | Select | `Half-formed idea` · `Idea with a plan` · `Audit plan against code` · `Active` · `Shipped` · `Parked` |
| `Parent Phase` | Relation → Phases (self, dual, other side `Sub-phases`) | — |
| `Last Reviewed Commit` | Text | short SHA |

Chapter body: `## Goal` · `## Approach` · `## Open questions` · `## Notes`.
For `Half-formed idea`, one line is enough — that is the point of the status.

`Last Reviewed Commit` is phase-granularity, and is independent of the
`verified_at_commit` key on documents in this bundle. Neither mirrors the
other.

## Tasks

| Property | Type | Values |
|---|---|---|
| `Task` | Title | — |
| `Phase` | Relation → Phases (dual, other side `Tasks`) | may be empty |
| `Status` | Select | `Todo` · `In Progress` · `Blocked` · `Done` · `Dropped` |
| `Size` | Select | `S` · `M` · `L` |
| `Priority` | Select | `ASAP` · `Normal` · `Later` |

Tasks with no Phase are legitimate — bugs hit while doing something else.
`ASAP` is the quick-win lane: small, cheap, disproportionately useful.

A task reaches `Done` only through a `done: <task title>` trailer in a commit
message. File-touching evidence moves it to `In Progress` at most — a commit
that touches a system is not work that finished.

## Why this file declares no sources

Its subject is a Notion workspace, not code in this repo, so there is nothing
under `Assets/` or `scripts/` whose movement could make it stale. The freshness
check has nothing to say about it by design. If these identifiers go wrong it
is because the workspace changed, and only a person looking at Notion can tell.
