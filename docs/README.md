# Node War docs

Load a slice, not the set. Each line says when to read the file.

## Always-on (auto-loaded)
- `CLAUDE.md` / `AGENTS.md`: rules every agent needs every turn.

## By role
- Orchestrator: the `orchestrate` skill, then [map.md](map.md), then the cards in scope.
- Engineer: [roles/engineer.md](roles/engineer.md), then `scripts/context.ps1 -Systems <...>`.
- Editing docs: the `system-docs` skill.

## Project level
- [map.md](map.md): every system and connector, the three flows, versions and
  boundaries, the top pressure points. Read before planning.
- [game.md](game.md): what the game is. Read before a gameplay or balance question.
- [style-guide.md](style-guide.md): how the menus look. Read before any UI layout or styling change.
- [contracts/determinism.md](contracts/determinism.md): the simulation contract.
  `context.ps1` includes it automatically for simulation systems.

## System cards (`docs/systems/`)
Match rules (all in `Simulation/`):
- [systems/simulation.md](systems/simulation.md): state, tick loop, movement, combat, claiming, win, hashing.
- [systems/economy.md](systems/economy.md): resources, production, districts, banks, recruiting, minions.
- [systems/commands.md](systems/commands.md): `GameCommand` from intent to `CommandProcessor`.
- [systems/board.md](systems/board.md): maps, terrain, `MatchFactory`, balance data, version hashes.
- [systems/draft.md](systems/draft.md): the pre-match placement draft.

Running a match:
- [systems/match-flow.md](systems/match-flow.md): `GameManager` lifecycle, tick drivers, scenes.
- [systems/netcode.md](systems/netcode.md): lockstep transport, delay, rollback, holds, emotes.
- [systems/input.md](systems/input.md): gestures, selection, orders, camera.
- [systems/world-view.md](systems/world-view.md): world presentation of nodes and villagers.
- [systems/match-ui.md](systems/match-ui.md): in-match HUD, node sheet, draft screen, end card.
- [systems/bots.md](systems/bots.md): the bot and the headless balance rig.

Around the match:
- [systems/match-log.md](systems/match-log.md): log format, recorder, replay, referee.
- [systems/backend-client.md](systems/backend-client.md): client services and ranked flow presenters.
- [systems/cloud.md](systems/cloud.md): the Cloud Code module, records, settlement, matchmaking.
- [systems/progression.md](systems/progression.md): catalog, variants, inventory, rating, arenas.
- [systems/lobby-ui.md](systems/lobby-ui.md): the lobby screens and their data layer.
- [systems/ui-art.md](systems/ui-art.md): the UI art theme and icon pipeline.
- [systems/tooling.md](systems/tooling.md): tests, CI, guards, scripts, editor tooling.

## Human learning
- [Obsidian vault](../notes/Home.md): explanations, concepts and patterns; open `notes/` as a vault.
- [Merge review](../notes/Merge%20review.md): generated card and vault review queue after PR merges.

## Procedures
- `docs/skills/`: how to run things (tests, the Editor CLI).
- `docs/computations/`, `docs/attesters/`: the determinism baseline CI runs.
- `docs/design-history/`: the original design document, the lobby mockup and panel-language contract, and point-in-time lobby audits. Historical except `panel-language.html`, which the `game-ui` skill treats as the visual contract.

Future work is never in this folder: it is the Notion Roadmap.
