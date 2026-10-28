# Shadow Steps — progress log

Each entry follows the milestone report format from IMPLEMENTATION_PLAN.md. "Verified" means a command was run and its output inspected; nothing here is assumed.

## 2026-10-28 — M0 Repository audit and foundation

**Milestone:** M0 — complete (gate met with the dotnet runner; Unity Test Runner execution pending, see below).

**Repository audit**
- Unity 2022.3.62f3 (ProjectSettings/ProjectVersion.txt), URP 14.0.12 with Performant/Balanced/HighFidelity assets (Assets/Settings), Input Manager (`activeInputHandler: 0`), portrait default orientation (`defaultScreenOrientation: 4` = auto? see KNOWN_ISSUES), build target currently Android (editor process args), scripting backend default (Mono) — IL2CPP/ARM64 to be configured in M8.
- Packages: TextMeshPro 3.0.7, Test Framework 1.1.33 (NUnit 3.5 custom), Timeline, Visual Scripting, uGUI 1.0.0. No Newtonsoft JSON, no DOTween, no Zenject.
- Existing user content: URP template only (SampleScene, TutorialInfo/Readme). Nothing deleted.
- Git: two commits before this work ("Initial commit", "First commit"); branch `main`.

**Files created/changed**
- Root: CLAUDE.md, MASTER_GAME_SPEC.md, IMPLEMENTATION_PLAN.md, HANDOFF_README.md, README.md, reference/, prompts/, .gitignore (+zip ignore)
- Assets/StreamingAssets/ShadowSteps/levels.json (copy of reference/levels.json)
- Assets/_ShadowSteps/Scripts/Core: GridCoord, Direction, GameAction, GuardDefinition, LevelDefinition, LevelValidator (+ asmdef)
- Assets/_ShadowSteps/Scripts/Data: MiniJson, LevelDtos, LevelRepository (LevelPackLoader/Writer), SaveData, SaveRepository (+ asmdef)
- Assets/_ShadowSteps/Scripts/{Runtime,UI,Editor} asmdefs (empty until M2)
- Assets/_ShadowSteps/Tests/EditMode: TestPaths, LevelPackLoadingTests, LevelValidatorTests, SaveRepositoryTests, MiniJsonTests (+ asmdef)
- Tools/gen_meta.py, Tools/run_core_tests.sh, Tools/CoreTestRunner/

**Tests run**
- `Tools/run_core_tests.sh` (dotnet 9 SDK, NUnit 3.14 from NuGet, C# 9): compiles Core + Data + EditMode tests and runs them. Result: `passed=130 failed=0 skipped=0` (count includes the M1 tests below).

**Verified in Unity editor?** No. The Unity Editor GUI is open on this project (lock file present), so `-batchmode` cannot start. The same test files are standard NUnit and sit in a `UNITY_INCLUDE_TESTS` EditMode assembly, so they will appear in Window ▸ General ▸ Test Runner once the editor refreshes.

**Verified on device?** No (nothing to run on device yet).

**Open issues:** see KNOWN_ISSUES.md #1 (editor lock), #2 (level 14 / EMP design intent).

**Manual steps for user**
1. Focus the Unity Editor so it imports the new folders (Assets ▸ Refresh if auto-refresh is off). Expect zero compile errors in the Console.
2. Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All. Expected: 130 passed.

**Next milestone:** M2 Unity graybox level.

## 2026-10-28 — M1 Pure rules engine and solver parity

**Milestone:** M1 — complete (gate met).

**Files created/changed**
- Core: GuardSchedule, VisionSystem, GameState, TurnResult, RulesEngine, GameSession (+ReplayReport), Solver
- Tests: GuardScheduleTests, VisionSystemTests, RulesEngineTests, ReferenceParityTests

**Tests run (same command as M0)**
- ReferenceSolution_WinsWithExactTurnCount(1..20): 20/20 pass — every validation.json solution wins with exactly `moves` turns and no prefix is captured.
- Solver_ReproducesReferencePathAndStateCount(1..20): 20/20 pass — the C# BFS returns the identical action string and the identical `states_checked` count as the Python solver for all 20 levels. This is the strongest parity evidence available without running Python inside Unity: any divergence in validation, movement, phase, capture, vision or smoke/EMP would change either the path or the explored-state count.
- Period_MatchesReferenceForAll20Levels: pass.
- Edge cases: invalid moves spend nothing; gadget without charge invalid; WAIT advances phase; phase wraps at period; smoke conceals for deployment + next action only; occupancy and swap capture under smoke; EMP freezes phase, spends turn, sight still counts; key unlocks door and is required to win; capture precedes win; actions after terminal rejected; undo restores every field (level 20, including charges and cloud); undo after capture; restart; preview has no side effects; replay deterministic.
- Constrained solves (ConstrainedSolves_ReportWhichGadgetsAreTrulyRequired):
  | level | no smoke | no EMP | no gadgets |
  |---|---|---|---|
  | 11 Smoke Passage | unsolvable | 11 | unsolvable |
  | 12 Power Cut | 10 | 10 | 10 |
  | 14 Smoke and Mirrors | 11 | 11 | 11 |
  | 15 Blackout Window | 12 | 12 | 12 |
  | 17 Three Blind Angles | unsolvable | 16 | unsolvable |
  | 18 Shift Change | 14 | 14 | 14 |
  | 19 High Security | unsolvable | 25 | unsolvable |
  | 20 Final Extraction | unsolvable | 23 | unsolvable |

**Verified in Unity editor?** No (same lock as M0). **Verified on device?** N/A.

**Next milestone:** M2.

---

# V2 (node-graph movement, top-down 2D) — progress

## 2026-10-28 — V2 M0 Audit and checkpoint

**Milestone:** V2 M0 — complete. Checkpoint commits `a9b7ca7` (V2 docs installed) and `2b047d6` (audit). See `Docs/V2_MIGRATION_AUDIT.md` for the file-by-file reuse/adapt/replace/missing classification. No source was lost; the V1 EditMode suite stayed green (130 tests) before V2 work began.

## 2026-10-28 — V2 M1 Pure C# graph gameplay

**Milestone:** V2 M1 — complete (gate met with the dotnet runner; Unity Test Runner still pending, editor lock).

**Reused vs changed**
- Reused unchanged: GridCoord, Direction, GameAction, GameState, GameSession/ReplayReport, MiniJson, SaveData, SaveRepository, test tooling.
- Adapted: GuardDefinition (moveEvery/rotateEvery/id, legacy ctor kept), GuardSchedule (independent cadences, identical to V1 when uniform), LevelDefinition (LevelSpec + LevelGraph, schema fields, cover, restrictions, scoring), RulesEngine (edge-based validation, configurable smokeTurns, noWait), VisionSystem (occluder predicate: wall/cover nodes + locked door; edges never occlude), Solver (AllowWait), LevelValidator (graph/edge/route/overlap/reachability rules with positions for editor highlighting), LevelRepository (schema 1 legacy path + schema 2 parser, canonical schema-2 writer, legacy writer).
- New: Core/LevelGraph.cs (Edge, EdgeType, NodeInfo, LevelGraph, LevelGraphBuilder), Core/LevelMetrics.cs (LevelMetricsAnalyzer), Data LegacyLevelMigrator.

**Files created/changed**
- Core: GuardDefinition.cs, GuardSchedule.cs, LevelDefinition.cs, LevelGraph.cs (new), LevelMetrics.cs (new), LevelValidator.cs, RulesEngine.cs, Solver.cs, TurnResult.cs, VisionSystem.cs
- Data: LevelRepository.cs (LevelPackLoader, LegacyLevelMigrator, LevelPackWriter)
- Tests (new): LevelGraphTests.cs, GraphRulesTests.cs, SchemaV2Tests.cs, LevelMetricsTests.cs; (updated) LevelPackLoadingTests.cs, LevelValidatorTests.cs

**Tests run** — `Tools/run_core_tests.sh` (dotnet 9, NUnit 3.14): `passed=169 failed=0 skipped=0`.
- Gate A (engine/graph): legacy grid generation (edge counts, no edges into walls, locked edges into door), duplicate-edge diagnostics, one-way edges, blocked/locked pairs, node mask holes, node spacing, missing edge rejects a move without spending a turn, missing node = out of bounds, locked edge needs key, guard route without edge / without reverse edge rejected, independent moveEvery/rotateEvery schedules, noWait restriction, configurable smokeTurns.
- Gate B (vision): missing edge does not block sight; cover blocks sight (passable and solid variants); all V1 vision/smoke/EMP/collision tests still pass.
- Gate C (data): design-doc sample level loads and validates; schema-2 export/import round-trips topology; one-way/locked edges round-trip; malformed fields report JSON paths; switchable edges, decoy, cone angles other than 90, unknown schema versions rejected explicitly; legacy writer still produces schema 1.
- Gate F (legacy regression through the graph engine): all 20 reference solutions replay to a win with exact turn counts; the C# solver reproduces the reference path and explored-state count for all 20; after exporting the migrated pack as schema 2 and reloading, all 20 replays and solver paths are unchanged.
- Validator on the migrated legacy pack: 0 errors, 4 warnings (routes over key/door nodes in 10, 16, 19; guard overlap in 18 tolerated as legacy).

**Solver-backed metrics for the migrated legacy levels** (V2 engine, legacy-equivalent topology; "unsolvable" = mechanic required):
| level | shortest | no WAIT | no smoke | no EMP | no gadgets | period | safe node ratio | branching | shortest solutions |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 8 | 8 | 8 | 8 | 8 | 1 | 0.95 | 3.75 | 19 |
| 2 | 9 | unsolvable | 9 | 9 | 9 | 4 | 0.85 | 3.78 | 28 |
| 3 | 8 | 8 | 8 | 8 | 8 | 6 | 0.95 | 3.13 | 3 |
| 4 | 8 | 8 | 8 | 8 | 8 | 12 | 0.95 | 3.25 | 8 |
| 5 | 11 | unsolvable | 11 | 11 | 11 | 4 | 0.80 | 2.73 | 16 |
| 6 | 10 | 10 | 10 | 10 | 10 | 12 | 0.82 | 3.50 | 37 |
| 7 | 10 | 10 | 10 | 10 | 10 | 4 | 0.95 | 3.30 | 24 |
| 8 | 8 | 8 | 8 | 8 | 8 | 4 | 0.70 | 2.88 | 4 |
| 9 | 15 | unsolvable | 15 | 15 | 15 | 4 | 0.76 | 3.33 | 128 |
| 10 | 13 | unsolvable | 13 | 13 | 13 | 4 | 0.73 | 3.38 | 84 |
| 11 | 11 | 11 | unsolvable | 11 | unsolvable | 4 | 0.68 | 3.55 | 3 |
| 12 | 10 | 10 | 10 | 10 | 10 | 4 | 0.61 | 4.50 | 17 |
| 13 | 12 | 12 | 12 | 12 | 12 | 12 | 0.53 | 3.33 | 1 |
| 14 | 11 | 11 | 11 | 11 | 11 | 4 | 0.71 | 3.55 | 136 |
| 15 | 12 | 12 | 12 | 12 | 12 | 12 | 0.84 | 4.33 | 67 |
| 16 | 12 | 12 | 12 | 12 | 12 | 4 | 0.64 | 3.33 | 8 |
| 17 | 16 | unsolvable | unsolvable | 16 | unsolvable | 4 | 0.61 | 2.88 | 42 |
| 18 | 14 | 14 | 14 | 14 | 14 | 12 | 0.76 | 4.43 | 36 |
| 19 | 25 | 25 | unsolvable | 25 | unsolvable | 12 | 0.50 | 3.60 | 2000+ |
| 20 | 23 | unsolvable | unsolvable | 23 | unsolvable | 12 | 0.65 | 4.83 | 2000+ |

These numbers describe the *migrated* legacy topology (full grid adjacency). They are not a V2 validation of the levels as shipped content: V2 M5 re-solves after any redesign (level 18 overlap, readability changes) and stores fresh traces in `validation_v2.json`.

**Verified in Unity editor?** No (editor GUI holds the project lock). **Verified on device?** N/A.

**Manual steps for user:** focus Unity so it imports the new scripts; expect zero compile errors; Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All, expected 169 passed.

**Next milestone:** V2 M2 — top-down orthographic 2D sprite presentation (board/node/edge/character/cone views, portrait camera fit, input, HUD).
