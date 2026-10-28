# Shadow Steps — V2 migration audit

**Date:** 2026-10-28 · **Checkpoint:** commit `a9b7ca7` (V2 docs installed) on top of `5796da2` (V1 M1 complete, 130 EditMode tests green via `Tools/run_core_tests.sh`).
**Authority:** `V2_CHANGE_CONTROL.md` > `LEVEL_DESIGN_PARAMETERS.md` > `V2_IMPLEMENTATION_ROADMAP.md` > `MASTER_GAME_SPEC.md` > legacy reference (V1 data only).

## 0. State of the repository at audit time
- Unity 2022.3.62f3, URP 14.0.12 (3D template, Performant/Balanced/HighFidelity renderers), legacy Input Manager, Android build target. No scenes, prefabs or MonoBehaviours have been written yet: the Runtime/UI/Editor assemblies contain only asmdefs.
- Implemented and tested (engine-free): V1 rules engine, guard schedules, cone vision, undo session, BFS solver, JSON layer, level loader/writer (format_version 1), validator, save repository.
- Uncommitted: `Assets/UniversalRenderPipelineGlobalSettings.asset` modified by the open Unity Editor (not by this work; left untouched). `.vscode/` is editor-generated and untracked.
- Unity Editor GUI is open on the project, so batch-mode compile/tests are locked out; evidence so far is dotnet-based.

## 1. Subsystem classification

| Subsystem | Files | Class | What changes | Tests affected |
|---|---|---|---|---|
| Coordinates, directions, actions | Core/GridCoord.cs, Core/Direction.cs, Core/GameAction.cs | **reuse** | None. V2 keeps integer lattice, origin top-left, cardinal-only moves, N/E/S/W/WAIT/SMOKE/EMP. | none |
| Guard definition | Core/GuardDefinition.cs | **adapt** | Add `MoveEvery` / `RotateEvery` (V2 independent periods); V1 `step` maps to both. Mode names: V2 `pingPong`→Patrol, `moveRotate`→PatrolRotate (aliases in loader, enum unchanged). Add optional `Id`. | GuardScheduleTests (extend) |
| Guard schedule | Core/GuardSchedule.cs | **adapt** | Generalise `q` into `qMove = phase / MoveEvery` and `qRot = phase / RotateEvery`; cycle = lcm(routeCycle·MoveEvery, 4·RotateEvery/gcd). Identical to V1 when both equal `step`. | parity suite must stay green |
| Level definition | Core/LevelDefinition.cs | **adapt** | Add `Graph` (`LevelGraph`: node set, node kinds, directed typed edges), `NodeSpacing`, `SmokeTurns`, `EmpTurns`, `SchemaVersion`, metadata (world/chapter/theme/hint), `Cover`. Keep `Walls`, `Key`, `Door`, `Guards`, charges. | loader tests |
| Graph model | Core/LevelGraph.cs (new) | **missing** | Nodes (existence + kind: walkable / wall / cover), edges keyed `(from,to)` with `EdgeType` open/locked/oneWay (switchable parsed but rejected until switches exist), auto-cardinal expansion with `blockedEdges`, canonical edge enumeration for export and previews. | new LevelGraphTests |
| Rules engine | Core/RulesEngine.cs | **adapt** | Move validation becomes: target node exists → traversable → edge exists in that direction → edge not locked (or key held). Everything after validation (key pickup, phase advance, occupancy, swap, vision, win) is unchanged. Smoke duration from `SmokeTurns`. | RulesEngineTests (extend), parity suite |
| Vision | Core/VisionSystem.cs | **adapt** | Keep the reference discrete cone and 12-sample LoS. Occluder predicate becomes walls ∪ locked door node ∪ sight-blocking cover. Missing edges never occlude (tested explicitly). | VisionSystemTests (extend) |
| Game state / turn result / session | Core/GameState.cs, TurnResult.cs, GameSession.cs | **reuse** | Add `InvalidActionReason.NoEdge` / `NoNode`. Undo snapshot already covers all state. | none |
| Solver | Core/Solver.cs | **adapt** | Already expands through `RulesEngine.Step`, so graph rules apply automatically. Add `AllowWait=false` option for the no-WAIT necessity check. | ReferenceParityTests (reinterpreted, see §3) |
| Metrics | Core/LevelMetrics.cs (new) | **missing** | `shortestWinTurns`, `shortestWithoutWait/Smoke/EMP`, `guardCycleLCM`, `safeNodeRatio`, average branching factor along the optimum, capped uniqueness count. | new tests |
| Validator | Core/LevelValidator.cs | **adapt** | Add: dangling/duplicate/non-adjacent edges, edges touching missing or wall nodes, start/exit on walkable nodes, guard route segments must be legal edges in travel direction, graph reachability over edges, guard-overlap simulation over one period (policy: error for schema 2, warning for migrated legacy levels). | LevelValidatorTests (extend) |
| JSON layer | Data/MiniJson.cs | **reuse** | None. | none |
| Level loader/writer | Data/LevelRepository.cs, Data/LevelDtos.cs | **adapt** | `schemaVersion: 2` parser (board/nodes/edges/blockedEdges/walls/cover, player/objective, guards with V2 field names, gadgets, restrictions, presentation, scoring). Keep `format_version: 1` parser as the legacy path. Writer exports canonical schema 2 with expanded edges. | LevelPackLoadingTests (extend) |
| Legacy migration | Data/LegacyLevelMigrator.cs (new) | **missing** | V1→V2: every cell is a node; walls are wall nodes; bidirectional open edges between cardinally adjacent walkable nodes; edges *into* the door node are `locked` (unlocked by key); door node occludes while locked. Produces a conversion record per level. | new migration + equivalence tests |
| Save system | Data/SaveData.cs, Data/SaveRepository.cs | **reuse** | Already versioned with backup. No in-progress-turn state is persisted, so no migration needed yet. | none |
| EditMode test suite | Tests/EditMode/* | **reuse + extend** | Existing 130 tests become the legacy regression gate (gate F). New V2 suites added alongside. | — |
| Tooling | Tools/run_core_tests.sh, Tools/gen_meta.py, Tools/CoreTestRunner | **reuse** | None. | — |
| Presentation (3D primitive graybox plan) | not implemented | **replace** | Replaced before it existed by top-down orthographic 2D sprites (SpriteRenderer board on the XY plane, `world(x,y) = origin + (x·spacing, −y·spacing, 0)`). | PlayMode tests (new) |
| Level editor | not implemented | **missing** | Node/edge/occluder authoring window with validate/solve/metrics, schema 2 import/export with backups. | editor round-trip tests |
| Docs | Docs/* | **adapt** | PROGRESS/DECISIONS/KNOWN_ISSUES/TASKS re-baselined on the V2 roadmap. | — |

## 2. Conflicts between V1 work and V2 contract, with resolutions
1. **Door as node vs. locked edge.** V1 models the door as a tile that blocks player movement and sight until the key is held. V2 edges carry the lock. Resolution: legacy import converts the door into locked *incoming* edges plus a door node that occludes while locked. Guards keep walking through it (legacy levels 10 and 19 do), so guard route validation checks edge *existence and direction*, not lock state (recorded in DECISIONS).
2. **Guard overlap.** V2 validator defaults to rejecting guards sharing a node at the same phase. Measured on the legacy pack with the reference scheduler: only level 18 ("Shift Change") overlaps (guard A walks over guard C at (2,0) on phases 1, 5, 7, 11). Resolution: migrated legacy levels get a warning; level 18 is flagged for redesign in V2 M5; schema-2 levels get an error.
3. **Single `step` vs. `moveEvery`/`rotateEvery`.** Resolution: schedule generalised; legacy sets both from `step`; parity preserved by construction and by test.
4. **Phase-0 facing quirk** of travel-facing guards (facing resets to authored at wrap). V2 says "preserve the reference formula where sensible". Resolution: preserved, because the legacy equivalence gate requires it; documented as a known quirk for designers.
5. **Validation claims.** `validation.json` proves nothing for V2. Resolution: the parity suite is reframed as "legacy-mode equivalence of the migrated graph" (gate F); V2 traces are produced fresh by the C# solver in M5 and stored as `validation_v2.json`.
6. **Renderer.** The URP 3D template stays; sprites render fine in the 3D renderer with an orthographic camera. URP 2D Renderer is adopted only after a compatibility test (change control).

## 3. Risks
- Any divergence in the legacy conversion silently changes rules for the 20 levels; mitigated by re-running all 20 replays *and* the solver path/state-count parity through the graph engine.
- `cover` with "traversable but blocks sight" creates nodes you can stand on but not see through; vision sampling treats such a node as an occluder even when the player stands on it (the endpoint is excluded, so the player's own node never hides them). Tested.
- Guard overlap policy may reject future authored levels unexpectedly; the editor must show the offending phase and node.
- Switchable edges and decoy are in the schema but not in the simulator; the loader rejects them with a clear message so no level can silently depend on them.

## 4. Ordered steps (no durations)
1. **V2 M0** — this audit; checkpoint commits (`a9b7ca7`).
2. **V2 M1** — `LevelGraph`, `EdgeType`, guard `MoveEvery/RotateEvery`, edge-based `RulesEngine.Validate`, occluder predicate, validator graph rules + overlap policy, `LegacyLevelMigrator`, schema-2 loader/writer, `LevelMetrics`, solver `AllowWait`. Tests: graph edges (missing/one-way/locked/duplicate/dangling), independence of edges and sight, guard route legality, schedule periods, legacy equivalence (20 replays + 20 solver parities through the graph engine), schema-2 round trip, metrics. Gate: all green under `Tools/run_core_tests.sh`, Unity Test Runner when the editor is available.
3. **V2 M2** — 2D orthographic presentation (board/node/edge/character/cone sprite views, portrait camera fit, input, HUD) driven by `TurnResult`.
4. **V2 M3** — schema-2 editor window, migration command with backups, round-trip tests.
5. **V2 M4** — five new tutorial levels authored as explicit graphs, solved, constrained-solved, replayed.
6. **V2 M5** — all 20 legacy levels revalidated as V2 (fresh traces), level 18 redesigned for the overlap rule, differences documented.
7. **V2 M6/M7** — presentation polish, QA, builds.
