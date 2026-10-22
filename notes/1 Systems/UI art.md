---
shell: 1
up:
  - "[[Lobby]]"
  - "[[Match]]"
down:
  - "[[Semantic icons and fallbacks]]"
  - "[[Shared UI theme]]"
concepts: []
tags: [shell/1, realm/lobby, realm/match, system/ui-art]
card: docs/systems/ui-art.md
sources:
  - Assets/UI/Scripts/UIArtTheme.cs
  - Assets/UI/Scripts/UIArtLookup.cs
  - Assets/UI/Scripts/LobbyIcon.cs
  - Assets/UI/Scripts/ItemFamily.cs
  - Assets/UI/Scripts/ItemTint.cs
  - Assets/UI/Scripts/UiRequired.cs
  - Assets/UI/Resources/UIArtTheme.asset
  - Assets/UI/UnityDefaultRuntimeTheme.tss
  - Assets/UI/Styles/Tokens.uss
  - Assets/UI/Styles/Components.uss
verified: 56ab2583
---

# UI art

> [!question] The question
> How can you replace placeholder art across the lobby and match without rewriting the screens or breaking their controls?

**Where you are:** [[Home]] › [[Lobby]] / [[Match]] › **UI art** (system)

## The problem

An icon means something before it looks like something. Food in a HUD and food in a node sheet mean the same resource, but may need different artwork. If each screen owns its own sprite decisions, a reskin becomes a hunt through controllers. If every sprite is mandatory, unfinished art makes the game harder to test.

## The idea

**Ask for an icon by meaning and location, resolve its art centrally, and keep a usable procedural version underneath.**

## How it works here

| Part | Its job | Read next |
|---|---|---|
| `UIArtTheme` | Inspector-authored icon entries and a district-visual table reference | [[Semantic icons and fallbacks]] |
| `UIArtLookup` | Exact location → Anywhere → generated-art result | [[UIArtLookup]] |
| `LobbyIcon` | Draw a Phosphor SVG (lobby), a sprite or a cached vector glyph; preserve tint and picking behavior | [[LobbyIcon]] |
| Runtime theme | Share tokens and components across Toolkit panels | [[Shared UI theme]] |
| `ItemFamily` / `ItemTint` | Give unfinished item art a shape, stable tint and monogram | Source files in this note's frontmatter |
| `UiRequired` | Diagnose missing or wrongly typed named elements during binding | [[Tooling]] |

The checked-in theme has `icons: []`. Procedural art is therefore a working path today, not just emergency code. `ItemFamily` describes round as “Produces,” square as “Holds,” and triangle as “Strikes.” Unknown district IDs use Square. `ItemTint` hashes item IDs into eight tint classes; these are item identities, not player ownership.

[[World view]] and UI art connect through district visuals; [[Lobby UI]] and [[Match UI]] consume the presentation layer. Choosing which UI stack runs remains the separate concern explained in [[Three UI stacks]].

## Why this, not that

| Decision | Alternative | Gain | Cost |
|---|---|---|---|
| Semantic kind plus context | A sprite field at every call site | One art policy, with deliberate local overrides | Usage metadata and enum compatibility need care |
| Generated fallback | Require a complete art pack | Screens remain usable during art work | Glyph painters remain code to maintain |
| Shared theme, separate panels | Duplicate styles or one coupled panel | Common visual language with separate panel settings | Page/theme precedence must be understood |
| Stable item-ID tint | Color by list position | An item keeps its identity between views | Different items can share a tint |

## What it costs later

`UIArt.Theme` caches its resource load until subsystem registration resets it. Treat Play Mode restart as the reliable refresh step after authoring changes. Adding an icon means updating usage locations and fallback drawing as well as assigning art. The current family table has an explicit TODO to move the family onto `DistrictDefinition` when that data is revised.

## Traps

- Renumbering serialized icon enums can make old themes mean something else. Retired values remain reserved.
- Changing behavior-bearing UXML names or types during a reskin can break bindings. `UiRequired` reports problems in Editor/development builds; it does not repair them.
- A successful type check cannot judge clipping, tint, font weight or touch delivery. Those need Unity checks.
- Item-family colors must not become an alternative ownership signal. The token header says, “OWNERSHIP IS NEVER COLOUR ALONE.”

## Patterns you've seen before

- 🔁 [[Presentation reads, never writes]]: artwork does not gain authority over game state.
- 🔁 [[Pure core, Unity shell]]: lookup policy is testable without Unity; asset loading and painting stay outside it.
- 🔁 [[Numbers on the wire are forever]]: serialized enum values deserve the same compatibility discipline as packet tags.
- 🔁 [[Treat bad input as a case you already handle]]: missing art follows the procedural path.

## Check yourself

> [!question]- You assign a Food sprite only for NodeSheet. What should the HUD use?
> A Food/Anywhere sprite if one exists; otherwise its generated glyph. A different specific context does not match the HUD.

> [!question]- Why shouldn't changing a card's tint change which player owns it?
> Tint and family are presentation identities. Ownership and command eligibility belong to other systems; art must preserve their cues and bindings.

## Go deeper ↓

- Mechanisms: [[Semantic icons and fallbacks]] · [[Shared UI theme]]
- Scripts: [[UIArtTheme]] · [[UIArtLookup]] · [[LobbyIcon]]

Sideways → [[Three UI stacks]] · [[World view]] · [[Tooling]]

## My understanding

<!-- Yours. AI never edits below this line. -->
