# Shadow Steps — granular task checklist

Legend: `[x]` done with evidence (see PROGRESS.md) · `[~]` implemented, verification pending · `[ ]` open.

## M0 — Repository audit and foundation
- [x] Audit project: Unity 2022.3.62f3, URP 14.0.12, Input Manager (legacy), Android build target, TMP 3.0.7, Test Framework 1.1.33
- [x] Place handoff docs at project root (CLAUDE.md, MASTER_GAME_SPEC.md, IMPLEMENTATION_PLAN.md, reference/, prompts/)
- [x] Copy reference/levels.json → Assets/StreamingAssets/ShadowSteps/levels.json (byte-identical, test enforced)
- [x] Folder layout Assets/_ShadowSteps/{Scenes,Scripts/{Core,Data,Runtime,UI,Editor},Prefabs,Materials,Art,Audio,Animations,ScriptableObjects,Tests}
- [x] Assembly definitions: Core (no engine refs), Data (no engine refs), Runtime, UI, Editor, Tests.EditMode, Tests.PlayMode
- [x] Deterministic .meta generator (Tools/gen_meta.py)
- [x] Domain schema: GridCoord, Direction, GameAction, GuardDefinition/GuardMode, LevelDefinition
- [x] Dependency-free JSON parser/writer (MiniJson) + strict LevelPackLoader with format_version + migration hook
- [x] LevelValidator (bounds, overlaps, routes, loop closure, key/door pairing, reachability, duplicate ids)
- [x] SaveData / SaveSerializer / SaveRepository (temp write, .bak rotation, corrupt fallback)
- [x] EditMode tests: pack loads 20 levels, DTO errors carry JSON paths, round-trip, validator cases, save cases
- [x] dotnet-based runner for Core/Data/EditMode tests (Tools/run_core_tests.sh)
- [~] Unity Test Runner execution of the same tests (blocked: editor GUI open → batch mode locked)
- [x] Docs/PROGRESS.md, DECISIONS.md, KNOWN_ISSUES.md

## M1 — Pure rules engine and solver parity
- [x] GuardSchedule (fixed/rotate/patrol/patrol_rotate/loop, step, rotation, period = lcm)
- [x] VisionSystem (discrete 90° cone + 12-sample centre-to-centre LoS; walls and locked door block)
- [x] GameState (immutable) + reference-tuple trace output
- [x] RulesEngine: Validate, Step, Preview, GetVisionCells, ValidateAll
- [x] GameSession: Apply/Undo/Restart/Replay with per-step trace
- [x] Solver: BFS port driven by RulesEngine; constrained solves (no smoke / no EMP)
- [x] Tests: schedule, vision (4 facings, range, diagonals, wall/door occlusion), smoke 2-action, occupancy under smoke, swap, EMP freeze + sight, key/door, capture-before-win, invalid actions, undo, restart, determinism
- [x] Parity: all 20 validation.json solutions win with exact turn count; solver reproduces exact path and explored-state count for all 20
- [x] Constrained-solve report of truly required gadgets (documented in DECISIONS.md)

## V1 M2–M8 — superseded by the V2 roadmap below (V2_CHANGE_CONTROL.md)

# V2 roadmap (V2_IMPLEMENTATION_ROADMAP.md)

## V2 M0 — Audit and checkpoint
- [x] Docs/V2_MIGRATION_AUDIT.md with reuse/adapt/replace/missing per file
- [x] Checkpoint commits; no source lost; V1 suite green before changes

## V2 M1 — Pure C# graph gameplay
- [x] LevelGraph / Edge / EdgeType / NodeInfo / LevelGraphBuilder (explicit directed cardinal edges, node mask, uniform nodeSpacing)
- [x] LevelSpec + LevelDefinition schema-2 fields (cover, cadences, gadgets turns, restrictions, scoring, metadata)
- [x] GuardSchedule with independent moveEvery/rotateEvery (legacy-identical when uniform)
- [x] RulesEngine movement over edges (NoEdge / locked / missing node), noWait, configurable smokeTurns
- [x] VisionSystem occluders = nodes only; edges never block sight
- [x] LevelValidator: dangling/self-loop/non-adjacent edges, edges into walls, route edge legality (ping-pong reverse, loop closing), locked edges need key, guard overlap policy, reachability over edges, issue positions for editor highlighting
- [x] Legacy migration (schema 1 → 2) with conversion notes; schema-2 loader and canonical writer; legacy writer retained
- [x] LevelMetricsAnalyzer (shortest, no-WAIT/no-smoke/no-EMP/no-gadgets, period, safe-node ratio, branching, uniqueness)
- [x] Tests: 169 passing incl. 20/20 legacy replays + 20/20 solver parity through the graph engine and after schema-2 round trip
- [~] Unity Test Runner execution (editor lock)

## V2 M2 — 2D orthographic rendering
- [ ] BoardView/NodeView/EdgeView/CharacterView/VisionOverlay sprite prefabs (generated originals, no external art)
- [ ] world(x,y) = origin + (x·spacing, −y·spacing, 0); moves are exactly nodeSpacing and end on nodes
- [ ] Orthographic camera portrait fit with safe area; URP 3D renderer kept unless 2D renderer proves compatible
- [ ] GameSessionController + TurnAnimationCoordinator driven by TurnResult; input (swipe/tap/keyboard) with lock during animation
- [ ] HUD (turns, charges, WAIT/SMOKE/EMP/UNDO/RESTART) as prefab; vision overlay equals VisionSystem cells
- [ ] PlayMode tests: scene loads level, node positions, overlay membership, no movement distance variance

## V2 M3 — Level schema/editor migration
- [ ] ShadowStepsLevelEditorWindow: node/wall/cover painting, edge toggle (open/locked/oneWay), route drawing, guard params, start/exit/key/door, charges, validate/solve/metrics, import/export schema 2 with backups
- [ ] Migration command for legacy packs (writes schema-2 file + backup of the original)
- [ ] Editor round-trip tests

## V2 M4 — First five tutorial levels
- [ ] Author exact graphs per LEVEL_DESIGN_PARAMETERS §4; solve; constrained solves; replay tests; no fabricated labels

## V2 M5 — Revalidate all 20 legacy levels
- [ ] Redesign level 18 (guard overlap) and any readability fixes; fresh V2 solver traces → validation_v2.json; document differences

## V2 M6 — Presentation and UX
- [ ] Home/Level Select/Results/Pause/Settings prefabs, onboarding, SFX, haptics toggles, safe area, resume handling

## V2 M7 — QA and release
- [ ] Unity EditMode/PlayMode runs, device checks, Android AAB, iOS export, store placeholders
