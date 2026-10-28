# Shadow Steps — V2 migration audit (PRELIMINARY)

**Status:** preliminary. Written on 2026-10-28 from the existing codebase (commits `439591b` M0, `5796da2` M1) and the V2 summary given in chat. The documents `V2_CHANGE_CONTROL.md`, `LEVEL_DESIGN_PARAMETERS.md`, `V2_IMPLEMENTATION_ROADMAP.md` and the updated `CLAUDE.md` were **not present** in the project, the handoff zip, or the user's Downloads/Desktop/Documents at the time of writing. Every classification below must be re-checked against those documents before implementation; open questions they must answer are listed in §5.

V2 summary as understood: (1) movement becomes fixed-distance, node-based, using explicit connections instead of implicit 4-neighbour grid adjacency; (2) presentation becomes top-down orthographic 2D sprite art; (3) level editor and JSON schema are updated; (4) all levels are revalidated under the new movement rules; (5) existing turn manager, enemy behaviours, gadgets and progression are preserved where compatible; nothing working is deleted or rewritten.

## 1. What exists today (V1, all engine-free unless noted)

| Component | File | Depends on grid adjacency? | Notes |
|---|---|---|---|
| `GridCoord` | Scripts/Core/GridCoord.cs | `Neighbor(Direction)` is the only adjacency helper | Coordinates themselves remain valid as node positions |
| `Direction`, `GameAction` | Core/Direction.cs, Core/GameAction.cs | Actions N/E/S/W are directions, not edges | Node graphs with ≤4 cardinal connections per node can keep these; otherwise the action set must change |
| `GuardDefinition`, `GuardMode` | Core/GuardDefinition.cs | Route is a list of coords; validator requires consecutive route tiles to be orthogonally adjacent | Routes become node sequences; adjacency check becomes "connection exists" |
| `LevelDefinition` | Core/LevelDefinition.cs | `IsWall`, `IsInside`, `AllCells()` | Needs a node/connection set; walls may become "no node" or "blocked connection" |
| `LevelValidator` | Core/LevelValidator.cs | Reachability flood uses 4-neighbours; route adjacency | Replace the neighbour enumeration with connection lookup; everything else reusable |
| `GuardSchedule` | Core/GuardSchedule.cs | None (pure index arithmetic on the route) | **Reusable as-is**, including ping-pong/loop/rotation/step and the phase-0 facing quirk |
| `VisionSystem` | Core/VisionSystem.cs | Cone geometry in tile space; LoS samples cells on a centre-to-centre line and tests `isBlocked(cell)` | Reusable if nodes stay on an integer lattice and walls remain cells. If connections carry "sight blocked" flags or nodes are off-lattice, this needs a new LoS rule |
| `GameState`, `TurnResult` | Core/GameState.cs, Core/TurnResult.cs | None | Reusable |
| `RulesEngine` | Core/RulesEngine.cs | `Validate` computes target = `pos.Neighbor(dir)` and checks bounds/wall/door | Only the movement-target resolution changes; turn order (validate → move/key → phase → occupancy/swap → vision → win) is preserved |
| `GameSession` (undo/restart/replay) | Core/GameSession.cs | None | Reusable |
| `Solver` (BFS) | Core/Solver.cs | None directly; expands via `RulesEngine.Step` | Reusable; parity targets (validation.json) become invalid and must be regenerated under V2 rules |
| `MiniJson`, `JsonAccess` | Data/MiniJson.cs | None | Reusable |
| `LevelPackLoader` / `LevelPackWriter` | Data/LevelRepository.cs | Parses `size`, `walls`, `route` | Add `format_version: 2` parsing + a V1→V2 migration that synthesises connections from grid adjacency |
| `SaveData` / `SaveRepository` | Data/SaveData.cs, Data/SaveRepository.cs | None | Reusable |
| EditMode tests (130) | Tests/EditMode | Parity tests pin V1 behaviour | Keep as the V1 regression suite behind a V1 data path; add V2 suites |
| Tooling | Tools/run_core_tests.sh, Tools/gen_meta.py, Tools/CoreTestRunner | None | Reusable |
| Runtime / UI / Editor assemblies | asmdefs only | — | Nothing implemented yet, so the 3D graybox plan from M2 can be replaced by the 2D sprite plan with zero loss |

## 2. Classification under the V2 summary

### Reuse unchanged
- GuardSchedule (schedules, period/lcm, rotation, step).
- GameState, TurnResult, GameSession (undo/restart/replay/trace), Solver, MiniJson, SaveData, SaveRepository, test runner tooling, meta generator, asmdef layout.
- Turn order and capture semantics (occupancy, edge swap, cone sight, smoke 2-action concealment, EMP phase freeze, key/door, capture-before-win).

### Modify
- `LevelDefinition`: add `Nodes` and `Connections` (explicit, directional or symmetric per V2 doc) alongside the existing fields; keep `Walls` for vision occlusion if V2 keeps them.
- `RulesEngine.Validate/Step`: resolve a move by looking up the connection from the current node in the requested direction (or by edge id, see §5 Q1); keep every later step identical.
- `LevelValidator`: node/connection integrity (both endpoints exist, no duplicate edges, symmetric pairs if required), route steps must follow connections, reachability over the graph.
- `LevelPackLoader`/`Writer`: `format_version` 2 with migration from 1 (derive connections from 4-adjacency of non-wall cells so the 20 V1 levels load without hand edits).
- `VisionSystem`: unchanged only if V2 keeps lattice coordinates and cell-based occlusion; otherwise see §5 Q3.
- Tests: parity tests parameterised by rule-set version; new V2 fixtures.

### Replace
- `validation.json` for V1 is no longer evidence for V2. A V2 `validation_v2.json` must be regenerated by the updated solver (C#), and ideally cross-checked by an updated `reference/build.py` so two independent implementations agree.
- The M2 plan for 3D primitive graybox presentation (never implemented): replaced by a 2D sprite presentation (SpriteRenderer-based board, orthographic camera on the XY plane, sorting layers).

### Add
- Node/connection data model and graph helpers (`NodeGraph`, adjacency lookup, edge enumeration) in Core.
- V2 level editor: node painting, connection toggling, guard route authoring over connections, validation, solver, export with backup.
- 2D presentation: tile/node sprites, connection lines, guard/player sprites, cone overlay drawn from `VisionSystem` cells, next-turn ghosts.

## 3. Risks
- **Rule drift**: changing adjacency changes which V1 solutions are legal. The 20 V1 levels are *not* validated for V2 until the V2 solver passes them. Expect some to become unsolvable or trivially different if connections are not simply 4-adjacency.
- **Vision semantics**: if V2 wants sight to follow connections rather than cells, the sampled LoS must be redefined and the V1 parity suite will diverge by design.
- **Action set**: if nodes may have more than four connections or non-cardinal ones, N/E/S/W actions and swipe input no longer map one-to-one; the input model must change.

## 4. Proposed order (to be reconciled with V2_IMPLEMENTATION_ROADMAP.md)
1. Finalise this audit against the V2 documents.
2. Core graph model + loader migration + validator; V1 levels load as V2 (connections synthesised).
3. RulesEngine movement over connections; V2 solver run on all 20 levels; publish results (solvable / shortest length / gadget requirement) in PROGRESS.md.
4. Decide, per `V2_CHANGE_CONTROL.md`, how to treat levels whose V2 result differs from V1.
5. 2D sprite presentation and input; HUD; progression (unchanged systems).
6. V2 level editor and schema export; two sandbox levels round-trip.

## 5. Questions the V2 documents must answer before coding
1. Are connections symmetric and cardinal (so N/E/S/W actions remain), or arbitrary edges selected by tapping a neighbouring node?
2. Is a "fixed distance" always one connection per turn, and do guards traverse the same connection graph?
3. Does vision stay a tile-space cone with cell occlusion, or does it follow connections/edges? What blocks sight in V2 (walls as cells, or missing connections)?
4. Do walls still exist as cells, or are they expressed purely as absent nodes/connections?
5. Is the V1 level pack to be migrated automatically (connections = 4-adjacency) and then re-solved, or re-authored by hand?
6. What are the acceptance thresholds in `LEVEL_DESIGN_PARAMETERS.md` (board size, node count, solution length bands, gadget requirements)?
7. JSON schema: exact field names for nodes/connections, `format_version` number, and whether V1 files must remain loadable.
