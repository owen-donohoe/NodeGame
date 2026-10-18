---
shell: 3
up:
  - "[[Player records in Cloud Save]]"
  - "[[Write locks]]"
concepts:
  - "[[Optimistic concurrency]]"
  - "[[Key-value stores vs SQL]]"
tags: [shell/3, realm/backend, system/cloud, script]
file: dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
sources:
  - dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs
verified: ce9fb6bf
---

# CloudSavePlayerRecordStore

> [!question] The question
> This is the one file where Node War actually talks to its database. What does a read
> and a conditional write look like in real code?

**Where you are:** [[Home]] › [[Backend]] › [[Cloud]] › [[Player records in Cloud Save]] › **CloudSavePlayerRecordStore** (script)

**Open:** [CloudSavePlayerRecordStore.cs in VS Code](vscode://file/C:/Dev/NodeGame/dotnet/NodeWarCloud/NodeWarCloud/CloudSavePlayerRecordStore.cs)

## What it is

The adapter between the server's rules and the Cloud Save API, for one player's Protected
records. It implements four narrow interfaces (`IPlayerRecordStore`, `ISettlementPlayerStore`,
`ILockedPlayerRecordStore`, `IDisciplinePlayerStore`), so each rule asks only for what it needs, and tests replace it with fakes.

## Read it in this order

### 1. `ReadLockedAsync`: one read, values *and* locks

```csharp
var response = await api.CloudSaveData.GetProtectedItemsAsync(
    context, context.ServiceToken, context.ProjectId, playerId, keys);
foreach (Item item in response.Data.Results) {
    locks[item.Key] = item.WriteLock;            // remember each key's version token
    switch (item.Key) { case "rating": state.Rating = Convert<RatingRecord>(item.Value); … }
}
```

- `context.ServiceToken` means "the server is asking", which is what lets it read and write Protected data.
- Each `Item` comes back with its `WriteLock`. That is the [[Optimistic concurrency]] token.

### 2. `WriteAsync`: a batch, each key with its expected lock

```csharp
items.Add(new SetItemBody("rating", records.Rating, locks?["rating"]));
// … rank, inventory, history
await api.CloudSaveData.SetProtectedItemBatchAsync(…, new SetItemBatchBody(items));
// 409 Conflict → RecordConflictException
```

The batch is atomic for this player: all keys are written, or none.

### 3. `WriteActiveMatchAsync`: fencing a first write

```csharp
// Null locks bypass CAS in Cloud Save. Including the existing rating
// key makes even the first claim a conditional, atomic batch.
new SetItemBody("rating", read.State.Rating, ratingLock),
new SetItemBody("activeMatch", claim, claimLock)   // claimLock may be null (first claim)
```

That's a clever workaround for a missing feature (create-if-absent): piggy-back on a key that
*does* exist. If anything changed `rating` meanwhile, the whole batch conflicts.

### 4. `WriteForSettlementAsync`: refuse to write without locks

It throws unless all four records and all four locks are present. Settlement must never run as last-write-wins.

### 5. `Convert<T>`: JSON round trip

`Item.Value` arrives as a loosely typed JSON object. Serialising and deserialising with Newtonsoft
turns it into the typed record. It's simple, at the cost of a little speed.

## Check yourself

> [!question]- Equip uses `WriteInventoryLockedAsync` with only the inventory lock. Why not all four?
> Equip only decides from and changes inventory. Taking all four locks would make it conflict
> with unrelated changes (a rating write) for no benefit.

> [!question]- What does "CAS" in the comment mean?
> Compare-and-swap: write *only if* the current version equals the one I expect. That's
> exactly what a write lock does.

## Go deeper ↓

This is the lowest shell. Below it is the code itself.

## My understanding

<!-- Yours. AI never edits below this line. -->
