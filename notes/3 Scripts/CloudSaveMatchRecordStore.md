---
shell: 3
up:
  - "[[Player records in Cloud Save]]"
  - "[[Write locks]]"
concepts:
  - "[[Key-value stores vs SQL]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs
verified: ce9fb6bf
---

# CloudSaveMatchRecordStore

> [!question] The question
> How is a match stored, and what happens when the database has no "create only if it doesn't exist" operation?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Player records in Cloud Save]] › **CloudSaveMatchRecordStore** (script)

**Open:** [CloudSaveMatchRecordStore.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/CloudSaveMatchRecordStore.cs)

## What it is

80 lines implementing `IMatchRecordStore` over Cloud Save **private custom items**, one item per
match: `match-<id>` with keys `record`, `presence-0/1`, `log-0/1`.

| Method | Cloud Save call | Lock? |
|---|---|---|
| `ReadAsync` | `GetPrivateCustomItemsAsync(["record"])` | Returns the `WriteLock` |
| `WriteAsync(record, lock)` | `SetPrivateCustomItemAsync("record", …, lock)` | Yes. 409 becomes `RecordConflictException` |
| `ReadPresenceAsync` / `WritePresenceAsync` | `presence-0/1` | No. Each slot writes only its own key |
| `SaveLog` | `log-0/1` (base64) | No. Written once, by the report that won |

## The honest comment

```csharp
// Cloud Save has no atomic create-if-absent: null bypasses write locks.
// Re-check here to refuse a known existing record; cross-worker first
// creation races still need validation in the live matchmaking spike.
```

Creating with a null lock means "write unconditionally". The code reads first to catch the
*common* duplicate, and the comment admits that a true simultaneous creation across two
servers is still an open risk. A SQL database would solve this with a primary key constraint
([[Key-value stores vs SQL]]).

## Check yourself

> [!question]- Why do presence writes skip the write lock?
> Each slot owns its key exclusively, so nobody else writes it, and a heartbeat that overwrites
> the previous heartbeat is exactly what's wanted.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
