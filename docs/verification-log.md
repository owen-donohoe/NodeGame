---
type: Reference
title: Verification log
description: The full history of who verified which document and when. Moved out of the documents themselves on 2026-09-20; each document keeps only its most recent entry.
tags: [okf, provenance]
generated: { by: claude-opus-5, at: 2026-09-20T13:20:00Z }
status: stable
---

# Verification log

Every `verified:` entry the documents in this bundle have ever carried.

They used to live in full at the top of each document, which put an
append-only list that only grows into the first thing any reader loads —
`architecture.md` alone carried seven entries, in the file agents read before
anything else. `okf-stale.ps1` never read them; it reads `verified_at_commit`
only. So the list was costing a read on every visit and paying nobody back.

Each document now keeps its single most recent entry, which is the one that
says who last stood behind it. The history is here. Nothing below was added,
edited or re-dated in the move — a verification is a claim about a human act,
and inventing one is the one thing this system exists to prevent.

## `docs/adding-a-feature.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }

## `docs/architecture.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T04:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }

## `docs/simulation-rules.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }

## `docs/skills/run-dotnet-tests.md`

- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }

## `.claude/skills/cs-review.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T02:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }
- { by: claude-opus-5, at: 2026-09-14T00:00:00Z }

## `.claude/skills/determinism-guard.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-13T01:00:00Z }

## `.claude/skills/write-sim-test.md`

- { by: claude-opus-5, at: 2026-08-31T00:00:00Z }
- { by: claude-opus-5, at: 2026-09-02T00:00:00Z }

