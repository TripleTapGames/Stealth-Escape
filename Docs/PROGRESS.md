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

## 2026-10-28 — V2 M2 2D orthographic rendering

**Milestone:** V2 M2 — implemented and compile-verified; runtime behaviour verified only by code review and PlayMode test design (Unity run pending, see below).

**Files created**
- Runtime: BoardLayout.cs, SpriteFactory.cs (procedural original sprites), BoardTheme.cs (ScriptableObject), Views.cs (NodeView, EdgeView, PropView, CharacterView, VisionOverlay), BoardView.cs, TurnAnimationCoordinator.cs, GameSessionController.cs, InputController.cs, CameraFitter.cs, AppServices.cs (LevelCatalog, save), GameSceneComposer.cs (composition root, IGameHud, GameHudFactory)
- UI: UiBuilder.cs (UiText with TMP/legacy fallback, UiButton, SafeAreaFitter), GameHudController.cs
- Editor: ShadowStepsSetupMenu.cs (theme asset, HUD prefab, Game scene, build settings, TMP essentials import), LevelValidationMenu.cs (validate / solve + metrics report / schema-2 export with backup)
- Scenes/Game.unity (hand-written minimal scene: Main Camera + GameSceneComposer), added to EditorBuildSettings
- Tests/PlayMode/GameScenePlayModeTests.cs
- Tools/unity_compile_check.py

**Design points**
- `world(x,y) = origin + (x·spacing, −y·spacing, 0)`; camera is orthographic, rotation identity, looking down −Z. `CameraFitter` fits the board into the band between the HUD bars and inside the safe area for any portrait aspect.
- Every move tween starts and ends on node centres; the final frame is always `BoardView.SyncToState(result.StateAfter)`, so animation can never author state. Reduced motion snaps.
- The cone overlay is fed by `RulesEngine.GetAllVisionCells`; previews use the same `RulesEngine.Preview`.
- HUD and scene are built from code (`BuildHierarchy`, `GameSceneComposer.BuildScene`) and the editor menu saves the same hierarchy as a prefab, so prefab and runtime fallback never diverge.
- TextMeshPro is used when its essential resources are imported; otherwise legacy `Text` is used so the scene still runs. Menu: Shadow Steps ▸ Setup ▸ Import TextMeshPro essentials.

**Tests run**
- `python3 -I Tools/unity_compile_check.py`: Core + Data + Runtime + UI + Editor + EditMode + PlayMode sources compile against the Unity 2022.3.62f3 managed DLLs with the project's defines: **0 errors**.
- `Tools/run_core_tests.sh`: 169 passed (unchanged).
- Editor log evidence (`~/Library/Logs/Unity/Editor.log`): the open editor imported all asmdefs and compiled ShadowSteps.Core/Data/Tests.EditMode ("Tundra build success") at the M1 state; Runtime/UI/Editor had no scripts at that time.

**Verified in Unity editor?** Not yet for M2 code (editor lock). **Verified on device?** No.

**Manual steps for user**
1. Focus Unity so it recompiles; Console must show no errors.
2. Shadow Steps ▸ Setup ▸ Generate All (creates BoardTheme.asset, GameHud.prefab, rewrites Game.unity with references).
3. Open Assets/_ShadowSteps/Scenes/Game.unity and press Play: level 1 renders top-down; arrows/WASD or swipes move; Space waits; Z undoes; R restarts.
4. Window ▸ General ▸ Test Runner ▸ PlayMode ▸ Run All (7 tests) and EditMode ▸ Run All (169 tests).

**Next milestone:** V2 M3 level editor + schema migration command.

## 2026-10-28 — V2 M3 Level schema/editor migration

**Milestone:** V2 M3 — implemented; editing rules unit-tested with dotnet; the EditorWindow UI itself is compile-verified only (needs an editor session to click through).

**Files**
- Editor/ShadowStepsLevelEditorWindow.cs — Shadow Steps ▸ Level Editor: tools Node / Wall / Cover / Edge / Start / Exit / Key / Door / Route, edge cycle none → open → one-way → locked → none, guard list (mode, facing, range, rotation step, moveEvery, rotateEvery, route drawing with right-click to pop), level fields, gadgets, restrictions, Validate, Solve + Metrics (prints necessity flags), one-turn simulation (N/E/S/W/WAIT/SMOKE/EMP/Undo/Reset with the live cone overlay), Import JSON (schema 1 or 2), Export JSON (schema 2, timestamped backup if the file exists), edit undo stack.
- Data/LevelAuthoringModel.cs — the engine-free editing model behind the window (round trip, CycleEdge, AutoEdges, Resize).
- Editor/LevelValidationMenu.cs — Validate packaged levels.json; Solve all + write Docs/Reports/solver_metrics.md; Export packaged levels as schema 2 to levels_v2.json with a backup (the original is never replaced).
- Tests/EditMode/LevelAuthoringModelTests.cs.

**Tests run:** `Tools/run_core_tests.sh` — see M4 entry for the combined count. Round trip of all 25 levels (20 legacy + 5 tutorial) through the authoring model and through JSON preserves topology, walls, guards and the solver's path.

**Manual steps:** open Shadow Steps ▸ Level Editor, Import JSON → Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json, click Validate and Solve + Metrics, step the simulation. Export to a new file and re-import.

## 2026-10-28 — V2 M4 First five tutorial levels

**Milestone:** V2 M4 — complete (gate: five freshly solved levels with exported traces and replay tests).

**Files**
- Assets/StreamingAssets/ShadowSteps/levels_tutorial_v2.json (ids 101–105, schema 2, authored as explicit graphs via `autoCardinalExceptBlocked` + walls + one locked edge)
- Assets/StreamingAssets/ShadowSteps/validation_tutorial_v2.json (fresh C# solver traces + metrics; produced by `Tools/CoreTestRunner --export-validation`)
- Tools/level_metrics.sh, Runner `--metrics / --solve / --search / --export-validation` modes (designer tooling, not shipped)
- Runtime/AppServices.cs loads the tutorial pack before levels.json (catalog = 101–105, then 1–20)
- Tests/EditMode/TutorialPackTests.cs

**Measured facts (C# solver, every claim below is a test assertion)**
| id | name | board | guards | shortest | no WAIT | no smoke | key required | shortest solutions | lesson |
|---|---|---|---|---|---|---|---|---|---|
| 101 | Behind the Watcher | 5×5 | 1 fixed | 8 | 8 | 8 | — | 5 | cone occlusion: the right-hand route is a visible trap |
| 102 | Clockwise Window | 5×5 | 1 rotate | 9 | **unsolvable** | 9 | — | 1 | WAIT is strictly required |
| 103 | Crossing Time | 6×5 | 1 ping-pong | 9 | 9 | 9 | — | 2 | cross the patrol row at the safe phase |
| 104 | Locked Route | 6×6 | 1 rotate | 12 | 12 | 12 | **yes** (exit only via locked edge) | 2 | key before the locked path |
| 105 | Cloud Corridor | 7×6 | 1 fixed + 1 ping-pong | 14 | 14 | **unsolvable** | — | 3 | smoke is strictly required (one unavoidable seen node) |

Observations (design, not proven fun): 104 and 105 exceed the "Tutorial 4–9 actions" target band in LEVEL_DESIGN_PARAMETERS §3 (they sit in the Easy/Medium bands). Both are kept because the mechanic necessity is proven; shortening them is a playtest decision. Level 102 has a unique shortest solution, which is tight for a tutorial; 101/103 offer 5 and 2.

**Verified in Unity editor?** No (lock). **Verified on device?** No.

**Next milestone:** V2 M5 — revalidate all 20 legacy levels as V2 content (level 18 overlap), export validation_v2.json.

## 2026-10-28 — V2 M5 Revalidate all 20 legacy levels

**Milestone:** V2 M5 — complete (gate: 20/20 V2-solvable with actual trace logs in `validation_v2.json`, replayed by tests).

**What changed**
- `Assets/StreamingAssets/ShadowSteps/levels.json` is now the **shipped schema-2 pack**: the 20 reference levels migrated to explicit edges (every cell a node, walls as wall nodes, bidirectional open edges, door = locked incoming edges + occluding node), with `migratedFromSchema: 1` and conversion notes on every level. `reference/levels.json` stays the untouched V1 fixture and remains the data for the V1 regression suite.
- **Level 18 "Shift Change" redesigned** to satisfy the V2 guard-collision rule: patrol guard A's route moved from row 0 `(3,0)→(0,0)` to row 1 `(3,1)→(0,1)`; rotating guard C stays at (2,0). Measured: no overlap at any phase, shortest solution still 14 moves and byte-identical to the reference solution, 36 shortest solutions, WAIT/smoke/EMP not required. Three other variants were measured and rejected (moving C to (2,1) or (1,1): 15 moves and WAIT becomes required; shortening A: unsolvable).
- `validation_v2.json`: fresh C# solver traces + metrics for ids 1–20 (`Tools/CoreTestRunner --export-validation`).
- Runner `--migrate <in> <out>` writes any pack as schema 2.

**Tests**
- PackagedPackTests: packaged levels equal the migrated reference in topology, objects, guards and charges (except the documented level-18 route); level 18 passes the strict overlap check; no overlaps anywhere in the pack; every validation_v2 trace replays to a win with the exact move count and equals the current solver's path and explored-state count; the V2 shortest lengths equal the V1 reference lengths for all 20 (movement-equivalent topology).
- V1 regression suite now reads `reference/levels.json` through the migrator; still 20/20 replays and 20/20 solver parities.

**Measured V2 facts for the shipped 20** (same numbers as the M1 table, which was computed on the equivalent topology; level 18 re-measured after redesign): WAIT required on 2, 5, 9, 10, 17, 20; smoke required on 11, 17, 19, 20; EMP never required; key structurally required on 9, 10, 13, 16, 19, 20 (door levels).

**Verified in Unity editor?** No (lock). **Next milestone:** V2 M6 presentation and UX (menus, level select, results, settings, audio, onboarding).

## 2026-10-28 — V2 M6 Presentation and UX

**Milestone:** V2 M6 — implemented and compile-verified; behaviour covered by PlayMode tests that still need a Unity run.

**Files**
- UI: UIScreen.cs (UIScreen base + ScreenRouter stack with modal input locking and reduced-motion fades), Screens.cs (UiPrefabSet, HomeScreen, LevelSelectScreen, SettingsScreen, PauseScreen, ResultsScreen, FailureScreen, OnboardingScreen — each self-builds via BuildHierarchy and is also saved as a prefab by the setup menu), GameFlowController.cs (session events → results/failure/onboarding, win recording, next level, audio/haptics, pause), MenuSceneComposer.cs (Home scene root), GameHudController.cs (+ pause button)
- Runtime: BootstrapController.cs (loads packs/pars/save then opens Home; visible error on failure), AudioService.cs (procedural original SFX: footstep, tick, rotate, gadget, alert, unlock, clear, tap, error; generated ambient loop; toggles), HapticsService, InputController resume guard (swallows input for 0.35 s after focus/pause), BoardView high-contrast cone mode, GameSceneHooks (UI attaches to any composed Game scene)
- Data: Progression.cs (LevelCatalog, Progression: unlock by catalog order, stars = par / par+2 / any, RecordWin; ParTable from validation files)
- Scenes: Bootstrap.unity, Home.unity (hand-written minimal scenes; setup menu regenerates them with prefab references); build settings Bootstrap → Home → Game
- Tests: ProgressionTests (EditMode), GameFlowPlayModeTests (win → results + lock, capture → failure → undo, pause lock)

**Flow:** Bootstrap → Home (Play = first unfinished level, Level Select grid with lock/complete/stars/best, Settings modal) → Game (HUD, pause, results with stars/par/next, failure with undo/retry, first-time onboarding card from the level hint + control hint, settings in pause). Settings persist immediately (music, SFX, haptics, reduced motion, high-contrast cones); the privacy policy button opens a placeholder URL.

**Tests run:** `Tools/run_core_tests.sh` → 191 passed. `Tools/unity_compile_check.py` → 0 errors.

**Verified in Unity editor?** No (lock). **Verified on device?** No.

**Manual steps:** Shadow Steps ▸ Setup ▸ Generate All; (optional) Setup ▸ Import TextMeshPro essentials for TMP text; open Bootstrap.unity and press Play; walk Home → Level Select → level 101 → win → Results → Next; Test Runner PlayMode (10 tests).

**Next:** V2 M7 QA/optimisation and release configuration.

## 2026-10-28 — V2 M7 QA tooling and V2 M8 release configuration

**Milestone:** V2 M7 — tooling complete, device QA pending; V2 M8 — configuration and build pipeline complete, no build produced yet (editor lock, no signing credentials).

**Files**
- Editor/BuildMenu.cs — Shadow Steps ▸ Build ▸ Android AAB (release) / Android APK (development) / iOS Xcode project / Apply release player settings. Signing comes only from environment variables `SS_KEYSTORE_PATH`, `SS_KEYSTORE_PASS`, `SS_KEY_ALIAS`, `SS_KEY_PASS`; without them a debug-signed build is produced and a warning is logged. Headless entry points for CI.
- Tools/run_unity_tests.sh — headless EditMode/PlayMode runs with NUnit XML summaries into Docs/Reports (refuses to run while the editor holds the lock).
- Tools/build_android.sh — headless AAB/APK build.
- ProjectSettings/ProjectSettings.asset — productName "Shadow Steps", companyName "Triple Tap Games", bundle id `com.tripletapgames.shadowsteps` (Android/iOS/Standalone), portrait only, IL2CPP for Android and iOS, ARM64 only. **Caveat:** the Unity Editor was open while this file was edited; if the editor rewrites ProjectSettings on exit, re-run Shadow Steps ▸ Build ▸ Apply release player settings (it re-applies all of the above except bundle ids and company name, which are checked by the release checklist).
- Docs/RELEASE_CHECKLIST.md, Docs/Store/PLAY_LISTING_PLACEHOLDER.md, Docs/Store/icon_placeholder_512.png (+ copy in Assets/_ShadowSteps/Art), Assets/_ShadowSteps/Art/ART_BIBLE.md.

**QA status**
- Automated: 191 EditMode tests (dotnet) green; 10 PlayMode tests written (scene fidelity, overlay parity, move distance, HUD, camera fit, win/failure/pause flow); Unity compile check 0 errors.
- Allocation/performance: vision is recomputed only on state change and previews; overlay cells are pooled; sprites are generated once and cached; no per-frame allocations in Update paths except the input poll. No profiler evidence yet (needs device).
- Not done (requires a Unity session or device): Unity Test Runner execution, Play-in-editor walk-through, device checks in RELEASE_CHECKLIST.md, any build.

**Verified in Unity editor?** No. **Verified on device?** No. **Store submission:** not started; placeholders only.

**Next action for the user:** close the Unity Editor (or focus it and run the Test Runner), then `Tools/run_unity_tests.sh All` and `Tools/build_android.sh apk`; install the APK on a phone and work through the device QA list.

## 2026-10-28 — Detection rule change: seen before and after the guard move (D-025)

**Request:** after the player moves, capture only if the player is within enemy vision both before and after the enemies move.

**Files**
- Core: DetectionRule.cs (new enum + tokens), LevelDefinition.cs (`Detection` on LevelSpec/LevelDefinition, legacy ctor = `AfterGuardMove`, schema 2 default = `BeforeOrAfterGuardMove`, `With(detection:)`), RulesEngine.cs (cone check now requires a sighting at the pre-move poses as well as the post-move poses when the level uses the V2 rule; union across guards; `GetDangerCells` helper), TurnResult.cs (doc).
- Data: LevelRepository.cs (`rules.detection` parse/validate/write; migrator keeps V1 semantics and records it), LevelAuthoringModel.cs (field round trip).
- Editor: ShadowStepsLevelEditorWindow.cs (Detection popup). UI: Screens.cs failure reason text.
- Tools: Runner `--migrate <in> <out> [v2rules]`.
- Data files: levels.json and levels_tutorial_v2.json now state `rules.detection` explicitly (levels.json round-tripped through the writer: only the 20 rules blocks changed); validation_v2.json and validation_tutorial_v2.json regenerated by the C# solver under the new rule.
- Tests: DetectionRuleTests.cs (13 tests: defaults, fixed/rotating/patrol scenarios, cross-guard union, EMP, smoke, "V2 never stricter than V1" over the reachable states of all 20 levels, danger cells, JSON round trip and error, legacy notes, authoring model); PackagedPackTests (moves ≤ V1, measured necessity table), TutorialPackTests and ProgressionTests expectations updated to measured values.
- Docs: LEVEL_DESIGN_PARAMETERS.md §5.7 and §6, MASTER_GAME_SPEC.md, V2_CHANGE_CONTROL.md, DECISIONS D-025, KNOWN_ISSUES #2, #9, #16, #17.

**Measured impact (C# solver, shipped packs):** shortest paths 1–20 now 8, 9, 8, 8, 8, 8, 8, 8, 12, 10, 10, 10, 12, 10, 12, 12, 14, 13, 13, 12 (V1: 8, 9, 8, 8, 11, 10, 10, 8, 15, 13, 11, 10, 12, 11, 12, 12, 16, 14, 25, 23). Tutorial: 101 8, 102 8, 103 9, 104 12, 105 12. WAIT forced only on 17; smoke only on 17, 19, 105; EMP never; key on 9, 10, 13, 16, 19, 20, 104. Reference parity (V1 rule) unchanged: 20/20 paths and state counts.

**Tests run:** `Tools/run_core_tests.sh` → passed=205 failed=0. `Tools/unity_compile_check.py` → 0 errors.

**Verified in Unity editor?** No (editor lock). **Verified on device?** No.

**Manual steps:** Test Runner EditMode (expect 205) and PlayMode (10); play level 2 or 102 in Play mode and confirm that stepping into a cone which then turns away does not capture; optionally open Shadow Steps ▸ Level Editor and check the new Detection popup.

**Next:** decide on KNOWN_ISSUES #16 (redesign tutorials 102/103 and gadget levels 11/14/20 under the new rule) and #17 (danger overlay).

## 2026-10-28 — Detection rule corrected: seen before OR after the guard move (D-025 revised)

**Request:** "after player move, if it is in cone range or in vision before the enemy moves or after, the player gets caught." The earlier "before and after" reading was wrong; this entry supersedes it.

**Files**
- Core: DetectionRule.cs (`BeforeOrAfterGuardMove`, token `beforeOrAfterGuardMove`), RulesEngine.cs (capture if seen at the pre-move poses or the post-move poses; captor = after-guard, else before-guard; `GetDangerCells` = current ∪ next cones).
- Data: Progression.cs (ParTable skips `moves < 0`). Tools: Runner `--export-validation` records unsolvable levels with `moves: -1` and exit code 2 instead of refusing.
- Data files: both packs state `rules.detection: beforeOrAfterGuardMove`; validation files regenerated (see impact).
- Tests: DetectionRuleTests rewritten for OR (14 tests incl. "V2 never more lenient than V1" over all reachable states of the 20 levels); PackagedPackTests/TutorialPackTests/ProgressionTests assert the measured solvable sets and pars.
- Docs: LEVEL_DESIGN_PARAMETERS.md §5.7, MASTER_GAME_SPEC.md, V2_CHANGE_CONTROL.md, DECISIONS D-025, KNOWN_ISSUES #2, #9, #16, #17, **#18 (blocking)**.

**Measured impact (C# solver):** solvable under the rule: 1 (8), 4 (8), 12 (10), 14 (11, smoke required), 101 (8). Unsolvable: 2, 3, 5, 6, 7, 8, 9, 10, 11, 13, 15, 16, 17, 18, 19, 20, 102, 103, 104, 105. Traces of the V1 solutions show the capture at the first node entered while a guard is looking at it before it turns/moves (level 2 action 8, level 3 action 3, level 5 action 5). Reference parity under the V1 rule is unchanged (20/20).

**Tests run:** `Tools/run_core_tests.sh` → 206 passed, 0 failed. `Tools/unity_compile_check.py` → 0 errors.

**Verified in Unity editor?** No (editor lock). **Verified on device?** No.

**Manual steps:** Test Runner EditMode/PlayMode; in Play mode on level 1 step into the fixed guard's cone (captured). Level 2 cannot be completed until it is redesigned.

**Next (user decision, KNOWN_ISSUES #18):** redesign the 20 unsolvable levels for the new rule, or keep legacy levels on `afterGuardMove` per level.

## 2026-10-28 — Solvability check under the corrected rule

Exhaustive check: the solver's explored-state counts for the unsolvable levels are 1–1124 against a 250,000 budget, so "unsolvable" is a full-state-space result, not a timeout. Added `--reach` to the runner (`Tools/level_reach.sh`): per level, an ASCII map of every node the player can ever stand on under the level's detection rule plus the fringe nodes where every entry attempt is a capture. Result on the shipped packs: solvable 1, 4, 12, 14, 101; on 13, 16 and 17 the start node is inside a cone at phase 0 (only 1 reachable node); on 8 the two patrol cones cover both exits from the start corner; the other levels dead-end at the first guard crossing. No content changed.

## 2026-10-28 — Level generator and regenerated content for the V2 detection rule (D-026)

**Request:** change the level generator, fix the unsolvable levels, and build a good progression/learning curve under the new rule.

**Files**
- Tools/CoreTestRunner/Generator.cs (new, `--generate <spec> <out> [--verbose]`): spec-driven randomised search validated by the real engine (validator, solver, constrained solves, uniqueness, reach ratio, guards-matter, door as exit chokepoint, idle penalty). Tools/LevelSpecs/tutorial_v2.json and world1_v2.json (curriculum specs, seed 20261028). Tools/generate_levels.sh (regenerate + re-export). Runner `--reach` from the previous entry is the review tool.
- Content: levels.json (20 levels: 1 and 4 kept, 18 generated), levels_tutorial_v2.json (101 kept, 102–105 generated), validation_v2.json, validation_tutorial_v2.json (fresh traces, all solvable).
- Tests: PackagedPackTests rewritten (valid/warning-free, 1 and 4 = reference, traces replay + solver parity, tier averages rise, measured necessity sets, gadget/key placement per curriculum); TutorialPackTests measured cases + hint check; ProgressionTests pars.
- Docs: LEVEL_CURRICULUM.md (new), DECISIONS D-026, KNOWN_ISSUES #2, #9, #18 resolved, #19 (EMP no-op).

**Measured:** pars 101–105 = 8, 9, 10, 11, 12; levels 1–20 = 8, 10, 9, 8, 10, 11, 11, 12, 12, 12, 11, 12, 13, 12, 15, 15, 16, 14, 20, 21. Tier averages 8.75 → 11 → 11.75 → 13.75 → 17.75. Every generated level has one shortest solution (19 has two). Generation time ≈ 100 s for both packs.

**Tests run:** `Tools/run_core_tests.sh` → 206 passed, 0 failed. `Tools/unity_compile_check.py` → 0 errors. **Verified in Unity editor?** No. **Verified on device?** No.

**Manual steps:** Test Runner EditMode/PlayMode; play 101 → 105 → 1 → 20 in order and note where the par feels wrong; adjust the spec bands and regenerate.

**Next:** decide KNOWN_ISSUES #19 (EMP semantics) and #17 (danger overlay), then device playtest of the curve.

## 2026-10-29 — V3 handover: M0 audit, M1 canonical core, M2 gadgets/alarm/score, M3 solver/validator, M4 30-level import

**Handover:** `reference/v3_handover_2026-10-29/` (SHADOW_STEPS_CLAUDE_CODE_HANDOVER.md is the rules authority). Audit: `Docs/V3_MIGRATION_AUDIT.md`. V2 packs and specs backed up to `reference/v2_shadowsteps_packs_backup/`.

**Files**
- Core/V3: Stage.cs (Stage, StageBuilder, GuardSpec, CameraSpec, DoorSpec, EdgeKey), Tracks.cs (GuardTrack incl. NEXT step, CameraTrack), Sight.cs (HitsRect/SegmentsMeet/Obstructed shared by guards, cameras and smoke throws; 2-node guard LOS; 3×2 camera footprint), StageState.cs (immutable; T = counted actions; clouds, EMPs, door bitmask, alarm, alarmEver), StageRules.cs (TryApply in the §3.1 order, ActionOutcome with capture phase/cause, StageReplay), StageSolver.cs (layered 0-1 BFS with free-gadget closure, StageScoring provisional formula, StageMetrics incl. mandatory-gadget and alarm-free solves), StageValidator.cs (§11 checks: board, cameras on walls facing floor, routes guard-passable and door-free, full-cycle overlap with swaps allowed, start safety, solver gates, Ghost/forced-alarm, endpoint-extension warning).
- Data/StageJson.cs: schema 3 read/write; BrowserLevelImporter (camera notch → wall).
- Tools: V3Tools.cs (`--v3-import <browser> <out> [validation]`, `--v3-validate`, `--v3-export-validation`, `--v3-trace`).
- Content: `Assets/StreamingAssets/ShadowSteps/levels_v3.json` (30 stages, solver scoring), `validation_v3.json` (traces + metrics).
- Tests: Tests/EditMode/V3 (V3TestKit, V3TrackTests, V3SightTests, V3RulesTests, V3CameraAlarmTests, V3GadgetTests, V3SolverTests, V3PackParityTests) — the §13 fixtures plus 30-level parity.

**Measured (C# solver):** 30/30 pars equal the browser pars and `playtest_validation_report.json`; Ghost offered on 16, 18, 20, 24, 25 (equal to browser); forced alarm on 17, 19, 23; smoke mandatory on 21 and 25; EMP mandatory on 23 and 25; no guard overlaps over any full cycle; all solver paths replay to wins with exactly par counted actions.

**Tests run:** `Tools/run_core_tests.sh` → 260 passed, 0 failed (54 new). `Tools/unity_compile_check.py` → 0 errors.

**Verified in Unity editor?** No (editor lock). **Verified on device?** No.

**Next:** M5 runtime/UI migration to the V3 engine (board, guards with facing + NEXT markers, wall cameras with footprint, smoke/EMP targeting modes, alarm HUD, retry without undo), then M6 progression/persistence of stars + Ghost.

## 2026-10-29 — V3 M5 runtime/UI migration and M6 progression (compile-verified, Unity run pending)

**Files**
- Data: StageCatalog.cs (StageCatalog, StageProgression: unlock by catalog order, stars via StageScoring, RecordWin with Ghost), SaveData/SaveRepository (BestStars, Ghost, DebugUnlockAll; optional JSON fields, old saves load).
- Runtime (rewritten on `ShadowSteps.V3`): BoardTheme (hazard/camera/marker colours), Views (NodeView floor/wall, ConnectionView with door padlock, PropView, CharacterView with facing + cyan NEXT marker + rotation marker, CameraView on the wall face with lens/sweep marker/disabled pips/EMP highlight, CloudView with pips, VisionOverlay fill+border), BoardView (connections from the stage graph, guard red / camera amber overlays from `Sight`, reachable cyan nodes, smoke targets, camera highlight, one-action preview with ghosts and outcome marker), TurnAnimationCoordinator (player first; guards/cameras only if the update happened; expiry flash; win/lockdown pulses), GameSessionController (TryApply, modes Move/SmokeTarget/EmpTarget, TapNode/TapCamera, Retry, debug-only undo), InputController (tap/swipe/long-press preview state machine, gadget modes never move), GameSceneComposer, AppServices (loads `levels_v3.json` only), CameraFitter.
- UI: GameHudController (Actions, 3★/2★ targets, Ghost/alarm/key status, gadget mode toggles, RETRY, debug UNDO), GameFlowController (results with stars/Ghost/best, failure with cause + phase, lockdown, onboarding from stage intro), Screens (LevelSelect with stars/best/ghost, Results, Failure), MenuSceneComposer.
- Editor: LevelValidationMenu → Shadow Steps ▸ Stages ▸ Validate / Re-export scoring.
- Tests: PlayMode rewritten (8 scene tests: board fidelity + overlay parity on FTUE-3, one-node move + HUD, free invalid move, solver path wins with par actions on LEVEL-3, retry, smoke targeting free action, EMP disables camera footprint, camera fit on a 7-column camera stage; 4 flow tests: win → results/stars/unlock, capture → failure cause → retry, lockdown failure + Ghost on LEVEL-16, pause lock).

**Scope boundaries honoured:** no hints, no checkpoints, no production undo (debug builds only), no monetisation, no final art (procedural placeholders through BoardTheme).

**Tests run:** `Tools/run_core_tests.sh` → 260 passed, 0 failed. `Tools/unity_compile_check.py` → 0 errors (Core, Data, Runtime, UI, Editor, EditMode, PlayMode).

**Verified in Unity editor?** No (editor lock). **Verified on device?** No.

**Manual steps:** close the Unity Editor; `Tools/run_unity_tests.sh All`; open Bootstrap.unity, Play: Home → Level Select → FTUE 1; on FTUE 3 long-press an adjacent node to see the preview; on Level 21 tap SMOKE then a cyan node; on Level 23 tap EMP then the camera; confirm NEXT markers on Level 3 / 5 and the amber footprint on Level 16.

**Next:** Unity run and device QA (handover M5/M6 acceptance), then retire the V2 core/packs (D-027, KNOWN_ISSUES #23/#25).

## 2026-10-29 — First Unity run: Home PLAY button dead (fixed)

Editor log of the user's Play attempt: Bootstrap loaded 30 stages and the save, opened Home, no errors, then nothing. Cause: the rewritten `MenuSceneComposer` no longer created an `EventSystem`, so uGUI received no taps in the Home scene. Fix: `UiBuilder.CreateCanvas` now calls `EnsureEventSystem()` (every canvas, every scene) and `MenuSceneComposer` calls it too; `MenuScenePlayModeTests` asserts the EventSystem and the wired PLAY action. Compile check 0 errors. Unity Play-mode verification still pending on the user's side.

## 2026-10-30 — V4 blocked cover nodes and the 30 updated levels

**Handover:** `reference/v4_handover_2026-10-30/` (SHADOW_STEPS_CLAUDE_CODE_HANDOVER_V4.md §2.1, 10, 11.1). V3 shipped pack backed up to `reference/v3_shipped_backup_2026-10-30/`.

**Files**
- Core/V3: Stage.cs (`Blocked` set from 'X', `IsBlocked`, `Occluders`, `IsFloor` excludes cover, rows round-trip, unknown characters fail with the cell), Sight.cs (occlusion over walls ∪ blocked), StageValidator.cs (cell-kind messages, cover cannot host cameras, both-wall-and-cover error).
- Data/StageJson.cs: importer reads V4 `optimalActions`/`ghostAvailable`, cross-checks `blockedNodes` against rows and fails on mismatch; routes imported as serialized.
- Runtime: SpriteFactory.Crate (temporary cross-braced crate), BoardTheme cover colours/size, NodeView `CellKind.Cover` (no node disc, no reachable ring, no connections), AppServices loads `levels_v4.json`. Editor menu paths updated.
- Content: `Assets/StreamingAssets/ShadowSteps/levels_v4.json` (30 stages, 55 `X` on 25 stages, solver scoring), `validation_v4.json`.
- Tests: V4BlockedNodeTests (9 fixtures from §11.1: rows parsing/unknown char, move onto X/# free and no enemy update, guard sight stopped by X, camera 3×2 filtered per node incl. corner contact, corner-grazing rays, smoke target/throw rules, cut vs cover, validator rejections, importer cross-check); V3PackParityTests retargeted to V4 (30/30 par + Ghost, browser report, 55 obstacles, Ghost set, mandatory gadget sets); PlayMode flow test uses the scoring policy instead of a literal.

**Parity with the browser prototype (C# solver vs `v4_validation_report.json`):** 30/30 pars equal, 30/30 Ghost offers equal, 0 structural errors, no guard overlap over any full cycle. Differences from V3 are listed in KNOWN_ISSUES #27; design flags in #26.

**Tests run:** `Tools/run_core_tests.sh` → 270 passed, 0 failed. `Tools/unity_compile_check.py` → 0 errors.

**Verified in Unity editor?** No. **Verified on device?** No.

**Manual steps:** press Play; FTUE 3 shows a crate at (2,3); Level 1 shows two crates; try to tap or swipe into a crate (nothing happens, no action counted); on Level 21/22 open SMOKE mode and confirm crates are never highlighted; on Level 16 the amber footprint excludes the crate column.

**Next:** Unity run of the 13 PlayMode tests and a play-through of all 30 stages; design review of the wait-heavy openings (#26).

## 2026-10-30 — 3D courtyard presentation layer (starter package integrated, Unity run pending)

**Inputs:** `StarterPack/ShadowSteps_Unity3D_Claude_Complete_Starter.zip` (docs, scripts and models archived under `reference/3d_starter_package/`; models copied to `Assets/_ShadowSteps/Art3D/Models/`), `Assets/characters_5_02/` (user-imported rigged characters; material converted to URP Lit earlier today).

**Files**
- `Assets/_ShadowSteps/Art3D/Scripts/Runtime`: `ShadowSteps.Art3D.asmdef`, SS3DArtPalette, SS3DGrid, SS3DLevelRenderer (geometry from `Stage`; cover only on `X`; pooled red/amber/preview/target overlays; NEXT + CW/CCW markers; camera bracket + head; EMP pips; smoke clouds; reachable tint; colliders stripped), SS3DCourtyardFrame (decor strictly outside tile centres), SS3DBoardCameraFitter (fixed 63° ortho, HUD bands, safe area), SS3DPreviewController, SS3DSessionBridge (runtime adapter via `GameSceneHooks`, plane-raycast picking, tweens in step with the 2D animator).
- `Art3D/Scripts/Editor/SS3DSetupMenu.cs`: Validate imported models, Create/update Art Palette (characters_5_02 m_1/m_2 by default), Create demo scenes (Clockwise + Camera Basics), all 30, Enable 3D presentation in Game scene.
- Runtime: `GameSceneComposer.Use3DPresentation` + `Art3DPalette`; `GameSceneHooks.OnComposed` now multicast; `InputController` picking overrides; `GameSessionController.PreviewShown/PreviewHidden`.
- `Packages/manifest.json`: `com.unity.cloud.gltfast` 6.8.0. `Tools/unity_compile_check.py` compiles Art3D.
- Docs: ART3D_SETUP.md, DECISIONS D-030, KNOWN_ISSUES #28–#31.

**Rules untouched:** no Core/Data change; 270 dotnet tests still green; the 3D layer only consumes `Sight`, `GuardTrack`, `CameraTrack`, `StageRules.SmokeTargets` outputs.

**Starter-package discrepancies:** its DTO/Newtonsoft reader, its `extendStraightPatrols` and its camera-anchor warning for Levels 16/18/19 ("[3,0] on a walkable cell") are not applicable — our importer already converts that notch to a wall, so brackets mount on real walls with identical vision. Proxy characters are kept only as fallbacks.

**Tests run:** `Tools/unity_compile_check.py` → 0 errors (incl. Art3D runtime + editor). `Tools/run_core_tests.sh` → 270 passed.

**Verified in Unity editor?** No. **Next:** open Unity, let glTFast import, run the three menus, review the two demo scenes, then enable the 3D presentation in Game.unity and play LEVEL-2 and LEVEL-16.

## 2026-10-30 — 3D visual validation milestone, part 1 (what could be verified without rendering)

**Verified from the project files (editor open, no render possible from here)**
- glTFast `com.unity.cloud.gltfast` 6.8.0 resolved (`Packages/packages-lock.json`, `Library/PackageCache`); shader graph 14.0.12 present for its URP shaders.
- All 26 `.glb` models imported: 26 `.glb.meta` files written by glTFast's ScriptedImporter, every one with `reportItems: []` (no import warnings/errors).
- No palette asset and no demo scenes existed: the Shadow Steps ▸ 3D menus had not been run.

**Changes**
- `SS3DSetupMenu`: palette rebuild now fills **empty slots only** and never overwrites `spyCharacter`/`guardCharacter` (`lockCharacterSelection`, auto-set once a character is assigned); a separate "Reset model slots (keeps characters)" menu re-resolves models; new "Framing report (portrait 9:16 and 9:19.5)".
- `SS3DAutoSetup` ([InitializeOnLoad]): on the next script reload builds the palette if missing and creates `Assets/_ShadowSteps/Art3D/Demo/Clockwise_Rotating_Guard.unity` (LEVEL-2) and `Camera_Basics.unity` (LEVEL-16) **additively**, so the open scene is untouched; never overwrites existing files.
- `SS3DArtPalette.wallHeightScale` (0.7) and `coverHeightScale` (0.85) applied by the renderer; `SS3DFraming` pure math for fit/occlusion checks.

**Framing arithmetic (63° pitch, 1-tile padding, HUD bands 14 % top / 22 % bottom):** all demo boards are width-bound in portrait and fit: LEVEL-2 uses 100 % of width and 67 % (9:16) / 55 % (9:19.5) of the usable height; LEVEL-16 68 % / 56 %; the tallest board (LEVEL-20, 7×6) 77 % / 63 %. A full-height wall (0.93) would cover 47 % of the tile behind it on screen, i.e. almost the node centre; at 0.7 scale it covers 33 % and cover crates at 0.85 scale 38 %, so node markers (tile centre) and the floating NEXT/rotation markers (0.9 units up) stay visible.

**Not verified:** rendered look, material colours, character facing/scale on screen, actual portrait screenshots. Unity must be focused once so the auto-setup runs; then open the two scenes.

**Tests run:** `Tools/unity_compile_check.py` → 0 errors. Rules untouched (no Core/Data change).

## 2026-10-30 — Camera coverage shown as projected light (3D layer)

Request: cameras should project light to show coverage instead of only tinting nodes. In `SS3DLevelRenderer` each camera head now carries a URP spot light (palette: colour, intensity, range, angle), and `SetCameraCoverage(camera, cells)` draws soft radial light pools and faint rays from the lens to **exactly the cells the engine reports** for that camera, so walls, cover and smoke cut the projected coverage the same way they cut detection. The amber hazard ring stays as the authoritative border; the flat amber fill is dropped while projection is on. An EMP-disabled camera switches its light off and its pools disappear until the two protected actions elapse. Bridge and preview feed per-camera cells. Compile check 0 errors; not rendered in Unity.

## 2026-10-30 — 3D milestone 1: camera framing, courtyard composition and lighting (no gameplay, data or model changes)

**Diagnosis:** the board is width-bound in portrait, so the orthographic size is fixed by the board width and the decoration (one tile beyond the parapets) left empty cyan bands above and below. Zooming in would crop the grid; the fix is composition.

**Changes (Art3D only)**
- `SS3DBoardCameraFitter`: pitch 63° → 58° (closer to the reference), padding 0.9 tiles, `designAspect` 9:19.5, `[ExecuteAlways]` so edit-mode Game-view aspect changes refit; `SizeFor`/`VisibleGroundRect` compute the ground rectangle visible for any aspect without moving the camera.
- `SS3DCourtyardFrame`: new composition that fills the visible rectangle for the tallest aspect: north façade line (wall blocks, entrance arch, double door, lanterns, banners, pillars, red carpet strip to the parapet gap), parapets with corner pillars and side benches, south terrace with a double staircase and pillar pair, then alternating canal water and flowerbed rows/columns outward to the screen edge; the staircase lane stays clear. All objects remain outside tile centres; the playable area receives only `X` obstacles.
- `SS3DArtTuning`: per-role tints on material **instances** (cream floor A/B, ivory walls, warm stone, vibrant turquoise water with high smoothness and faint emission, wood, foliage, green exit, crate) and `ApplyLighting` (warm sun 1.55, soft shadows 0.55, trilight warm ambient, warm-grey background) used by preview, demo generation and the runtime bridge. Imported assets and the character pack are untouched.
- Demo scenes: `SS3DAutoSetup.GeneratorVersion = 2` regenerates `Art3D/Demo/Clockwise_Rotating_Guard.unity` and `Camera_Basics.unity` additively on the next script reload (skips a scene that is currently open and says so).

**Calculated framing at 58° (HUD bands 14 % / 22 %, padding 0.9):** LEVEL-2 uses 100 % of width and 65 % (9:16) / 53 % (9:19.5) of the usable band; LEVEL-16 66 % / 54 %; LEVEL-20 75 % / 61 %. The visible ground extends about ±4 tiles sideways and 5.5–7.8 tiles beyond the north edge and 6.8–9.6 beyond the south edge, which the frame now fills with façade, water and greenery.

**Visually verified:** nothing; no render was possible from here. Rendering review is the next step.

## 2026-10-30 — Fix: "Assign an SS3DArtPalette asset first" during demo scene generation

**Root cause:** `SS3DSetupMenu.CreateScene` received the palette as a C# parameter and then called `EditorSceneManager.NewScene(…, Single)`. Switching scenes makes Unity unload assets that no serialized field references; a managed local does not keep an asset alive, so the palette became a destroyed object and `renderer.palette = palette` assigned Unity-null, which `SS3DLevelRenderer.Build` reported. The additive auto-setup path did not unload, which is why the first generation succeeded.

**Fix (Art3D editor/runtime only, no gameplay, data, model, lighting or framing changes):** `SS3DSetupMenu.LoadValidatedPalette()` loads the palette **by path after every scene switch** and validates it; `CreateScene`/`CreateAdditive` assign it to the renderer, the frame and the preview controller before `BuildPreview()`; `SS3DPreviewController` resolves its own palette (field → renderer → default asset path in the editor) and validates; `SS3DArtPalette.Validate` lists empty core slots, an empty cover list and missing characters; `SS3DLevelRenderer.Build` explains the asset path in its error. Palette rebuilds fill only empty character slots (never overwrite). Scenes are regenerated at a fixed path per stage (overwrite, no duplicates); `GeneratorVersion` 3 triggers one automatic regeneration on the next script reload. Compile check 0 errors; not executed in Unity.

## 2026-10-30 — 3D milestone 2: board prominence and designed perimeter (no gameplay, data, model or lighting changes)

- **Board prominence:** the board is width-bound in portrait, so the fitter's side padding is what sets its on-screen size. Padding 0.9 → 0.55 tiles: a 6-wide board now spans 85 % of the screen width (was 77 %), a 7-wide one 86 % (was 80 %). No cropping; coordinates untouched. The grid also sits on a **raised terrace**: a one-tile floor border around the grid, a low stone skirt on four sides, and the surroundings dropped 0.35 units, so the playable platform reads as the main object.
- **Perimeter redesign** (`SS3DCourtyardFrame` rewritten): the repeated 1×1 water/bush grid is gone. Outside the terrace there is one calm ground plane, two long side canals and a cross channel behind the façade (stretched water tiles), grouped hedges (scaled flowerbeds) at the four far corners and a few along the canals, a topiary pair flanking the entrance, two planter groups beside the stairs; façade = two wall courses on the axis with arch, double door, pillars, lanterns, banners; south = double staircase with sphere pillars; a red carpet links door → terrace gap → stairs. All objects remain outside tile centres and use the existing modular prefabs (scaled), no primitives, no new models.
- `GeneratorVersion` 4 regenerates both demo scenes on the next script reload (or Shadow Steps ▸ 3D ▸ Create demo scenes). Compile check 0 errors; not rendered here.

## 2026-10-30 — 3D milestone 3: the central courtyard (no gameplay, data, model-file or outer-perimeter changes)

- `SS3DMeshes.ChamferedBox`: procedural flat-shaded bevelled block (bottom-centre origin), cached by size; the frame's new `Box()` helper builds every trim from it with lit materials in palette colours. No external modelling.
- **Platform:** inset floor stones (`tileInset` 0.94) over a grout slab in the renderer (so the Game scene gets it too) plus the terrace grout, a two-step bevelled plinth under the whole platform (upper step in plinth stone, wider lower step in warm stone) and a thin ivory bevelled trim frame just inside the parapets.
- **Parapets:** thick base course + bevelled ivory cap rail + pilasters every two tiles, corner piers with caps; gaps on the axis for the entrance and the stairs; benches kept.
- **Entrance:** stepped plinth, tall wall with cornice and attic, recessed door bay with lintel, pilasters either side, arch and double door models inside, lanterns, banners, three steps descending to the terrace, carpet from the door to the terrace gap.
- **Staircase:** landing slab tied to the plinth, double stairs, cheek walls, newel piers with caps, carpet pad at the terrace gap.
- Palette: `groutColor`, `trimIvory`, `plinthStone`, `tileInset`. `GeneratorVersion` 5 regenerates the demo scenes on the next script reload. Compile check 0 errors; not rendered here.


## 2026-10-30 — 3D milestone 4: rendering pass (perspective camera, post-processing, SSAO, stone floor; no gameplay, data or model-file changes)

Decision after comparing milestone 3 with the mock art: procedural blockouts cannot reach the mock's prop quality, so this pass raises the **rendering** quality with what URP already provides, while the user decides between an environment kit and Blender hero models for the props (D-031).

- `SS3DBoardCameraFitter`: `usePerspective` (default on) with a narrow `fieldOfView` 26° gives the mild depth of the reference; the fit iterates the camera distance so the near corners (which project larger) still fit the band, `VisibleGroundRect` traces perspective corner rays, far clip follows the distance. Orthographic remains a one-flag fallback. Calculated for LEVEL-2 at 9:19.5: board 85 % of width, band 36–71 % of height, camera 40.7 units away, near row 14 % larger than the far row; at 9:16: 85 %, 32–75 %, 33.9 units.
- `SS3DArtTuning.ApplyPostProcessing`: global `Volume` built at runtime (no profile asset): ACES tonemapping, bloom 0.35 at threshold 1.0, colour adjustments (+0.15 EV, saturation +12, contrast +8), vignette 0.22, Gaussian depth of field starting 2.5 units past the board centre depth so only the perimeter softens; the camera gets post-processing, SMAA and a depth texture. All values are palette fields.
- `SS3DArtTuning.StoneTexture` + `SS3DLevelRenderer.CreateStoneTile`: the floor is now bevelled `ChamferedBox` slabs (UVs added) with a generated 256² cream-limestone albedo (value noise, faint veining, rim darkening, specks), six shared material variants (A/B tone × ±5 % value) and per-tile 90° texture rotation; the glb floor tiles remain available with `proceduralFloorTiles = false`.
- Shadow Steps ▸ 3D ▸ **Configure URP rendering**: via SerializedObject on the three pipeline assets and renderers in `Assets/Settings` sets HDR, depth texture, soft shadows, 80 m shadow distance, 2 cascades, 2048 shadow map, MSAA 2, and adds or retunes the Screen Space Ambient Occlusion feature (intensity 0.9, radius 0.35, direct 0.5, downsampled; the feature class is internal in URP 14, so it is created by reflection). `SS3DAutoSetup` runs it once when upgrading to `GeneratorVersion` 6, which also regenerates the two demo scenes.
- `SS3DPreviewController.rebuildOnLoad`: the preview rebuilds itself when its scene loads (delayed in edit mode), so saved demo scenes always reflect the current palette, lighting and post settings; the stone texture is `DontSave` to keep scene files small (generated meshes/materials are still embedded by Unity, as before).
- Art3D asmdefs now reference `Unity.RenderPipelines.Core.Runtime` and `Unity.RenderPipelines.Universal.Runtime`.
- Verified: compile check 0 errors; dotnet core suite 270 passed. Not rendered here; the Game-view screenshot is the next checkpoint.

## 2026-10-30 — 3D milestone 5: Synty POLYGON Fantasy Kingdom kit mapped into the palette (no gameplay, data or character changes)

The user imported the pack to `Assets/PolygonFantasyKingdom` (2,187 prefabs, 52 materials on the Built-in Standard shader, FBX in Synty's 2.5/5 m modules). Integration is adapter-only:

- `SS3DKitFit` (runtime): fits any instantiated prefab from its measured renderer bounds (per-axis, uniform, or kit scale) and aligns bounds centre/bottom to a ground point, so pivots, authored scale and kit units never matter. Sizes are in the object's unrotated axes.
- Palette: `kitMode`, `kitScale` 0.5 (one kit metre = half a tile; characters are 0.95 tiles), `kitTint` warm multiply for textured materials, `kitFacadeYaw`/`kitStairsYaw`, and optional kit slots (ground, parapet rail/post, wall run, gate, battlement, tower, stairs, hedge, tree, urn, fountain, statue + plinth, carpet, lamp, bush, flowers). `Tint` now leaves textured (kit) materials alone apart from the warm bias; water keeps its own alpha and takes the palette turquoise.
- Renderer: in kit mode walls are fitted to 0.92 × `wallHeightScale` × 0.92 of the cell and cover props uniformly inside 0.74 of the cell and `coverHeightScale` high, rotated per cell. The board floor stays the procedural cream stone; the terrace border uses the same tiles.
- `SS3DCourtyardFrame` rewritten with per-slot fallbacks: kit grass ground slab, balustrade (stretched rail segments + posts, gaps on the axis), piers with urns, palace line across the whole visible width (gate house centred, fancy-window walls with battlements, two towers beside the gate, banners, knight statues on plinths, urns), stone stair modules (three abreast, low cheek walls), side canals with kerbs stopping at the wall, continuous hedge runs, round trees rising behind the wall and at the south corners, fountains at the south corners, flower clusters, street lamps at the north gap, rug carpets, kit benches. Nothing south of the board is taller than the 1.6 × distance occlusion limit at 58°.
- `SS3DKitSetup` (editor): Shadow Steps ▸ 3D ▸ Kit ▸ *Convert Synty materials to URP Lit* (Unity's `StandardUpgrader`, 46 Standard materials; particle/legacy shaders skipped) and *Map Synty Fantasy Kingdom into Art Palette* (paths verified against the imported folder; slots left unchanged when a prefab is missing, with a warning). `SS3DAutoSetup` version 7 runs both once when the pack folder exists and regenerates the demo scenes. Editor asmdef now references the URP/Core editor assemblies; `Tools/unity_compile_check.py` references them too.
- Kit sizes measured from the FBX files (cm, scale 1): castle walls 5 × 5 m, gate 5.7 × 5.9 m, tower 2.4 × 7.5 m, walkway railing 2.5 × 0.95 m, house stone stairs 1.9 × 1.3 × 1.9 m, hedge 5 × 1.4 m, fountain 4.9 × 3 m, crate 1.07 m, barrel 0.72 × 0.91 m; the fit code does not depend on these numbers.
- Verified: compile check 0 errors; nothing rendered here. Unknowns to confirm from the first screenshot: the façade pieces' front face (`kitFacadeYaw`), the stair descent direction (`kitStairsYaw`), and how the grey castle atlas reads under the warm tint.
- Licensing note: the imported folder's README says it was redistributed by a third-party site, not Synty's store build. Only the licensed download from Package Manager ▸ My Assets may ship; re-importing it over the same folder keeps every path and the mapping valid (run the Kit menu again afterwards).

## 2026-10-30 — 3D milestone 6: Synty composition pass (board prominence, low wall border, connected courtyard; no gameplay or data changes)

User review of the first kit render: board too small, perimeter cluttered, "dark boulders" around the grid. Those blocks are the authored V4 wall cells, so they stay; only their drawing changed.

- **Wall cells as architecture** (`SS3DLevelRenderer.BuildKitWall`): in kit mode each wall cell gets a castle-stone post and a low segment toward each wall neighbour to the east and south (`kitWallSegment` = `SM_Bld_Castle_Wall_Bottom_01`, `kitWallPost` = `SM_Bld_Castle_Wall_Block_01`, both on the light castle atlas); isolated wall cells fall back to a fitted block. `wallHeightScale` 0.5 in kit mode. The rule still treats the whole cell as solid.
- **Board prominence**: the terrace border is half a tile in kit mode (`CreateStoneTile` takes explicit sizes), the moat no longer runs along the sides, and the fitter padding is 0.78 tiles so the whole platform and its plinth are inside the frame with nothing cropped. Calculated: a 7-wide board spans about 80 % of the width at 9:19.5 and 9:16; the platform with plinth about 100 %.
- **Connected courtyard** (`SS3DCourtyardFrame.BuildKitSurroundings`): one water strip north and one south of the plinth with kerbs on three sides; a paved forecourt from the north strip to the palace wall (wall at 2.2 tiles from the parapet, gate house, towers, battlements, trees behind), bridge + carpet + two steps onto the terrace, lamps on the forecourt; south a landing and four cream steps bridging the water onto a paved apron, then a stone path to the screen edge with hedge runs and a tree per lower corner. Accents remain off, nothing repeats more than the hedge segments.
- Generator version 9 re-maps the kit and regenerates both demo scenes. Compile check 0 errors; not rendered here.

## 2026-10-30 — First10: SunlitCourtyard theme and ten art-preview scenes (no gameplay, data, character or model-file changes)

Executed `ShadowSteps_First10_Courtyard_Art_Pack/CLAUDE_FIRST10_IMPLEMENTATION_PROMPT.md` (pack copied to the project root, outside Assets).

- **A. Audit and data.** Checkpoint e5c5ed4. `Data/first10_v4_levels.json` is byte-identical (sha256 f88432a0…) to `reference/v4_handover_2026-10-30/shadow_steps_levels_v4.json`, and every gameplay field of its ten levels (rows, blockedNodes, start, exit, cuts, doors, guard kind/route/facing/offset/clockwise, cameras, gadgets, optimalActions = par) equals the shipped `levels_v4.json` stages with ids 1–10 (FTUE-1…5, LEVEL-1…5). The shipped pack stays the authority; nothing was overwritten. Synty prefab paths were verified on disk (see milestones 5–6); categories without a suitable light-stone kit piece (ivory walls, parapets, plinths, bridges, stairs, niches) are procedural chamfered blocks, which the prompt allows.
- **B. Theme.** `SS3DArtPalette.sunlitCourtyard` switches the frame to `SS3DCourtyardFrame.Sunlit.cs`: ivory platform with a half-tile border (at least a 6 × 4 platform for small boards), bevelled two-step plinth standing in a turquoise moat with stone kerbs, low parapet with pilasters and ball-topped piers, benches, symmetric entrance façade (plinth, wall, cornice, attic, door bay with the starter arch and wooden double doors, pilasters, lanterns, blue/gold banners), optional waterfall niches with pools, three steps, paved forecourt, bridge and red carpet that ends on the border strip outside the grid, four ivory steps bridging the south moat between ball-topped newels, paved apron, path, flower beds, grouped trees and bushes beyond the water, hedge runs beside the path. Wall cells (`#`) are ivory masonry drawn as a continuous low wall with capped posts (`SS3DLevelRenderer.BuildMasonryWall`); `X` cells keep the Synty crate/barrel/planter props fitted low (cover height 0.62). Water is a shared lit material with a generated caustic texture. Per-level variation is `SunlitVariant.For(stageId)` from the manifest (waterfall niches 5, 9; benches 1, 5, 7, 10; corner trees 3, 7, 9, 10; flower beds 1, 4, 6, 10; newel lanterns 2, 3, 8, 9; pink flowers 7); later ids hash deterministically. Characters stay the assigned characters_5_02 prefabs (palette rebuilds never touch them).
- **C. Scenes.** Shadow Steps ▸ 3D ▸ **Generate First 10 Courtyard Levels** (`SS3DFirst10Generator`) reads the manifest, maps `source_level_id` to the shipped stage, applies the theme to the palette (orthographic fixed camera, padding 1.5 tiles, minimum framed 6 × 4), creates `Assets/_ShadowSteps/Art3D/Levels/First10/SS3D_01_First_Steps.unity` … `SS3D_10_Corner_Patrol.unity` via `SS3DSetupMenu.CreateSceneAt` (overwrite in place, skips an open scene with a message, no duplicate lights/cameras), and validates each built scene against its stage: node markers = floor nodes, cover props = X cells, wall pieces = # cells, connection lines = graph edges, guard actors = guards, exit marker on the exit node, no cameras. Results go to `Docs/Reports/First10_Scene_Manifest.json`. Auto-setup version 10 runs the theme and the generation once. The scenes are **static art previews** (root `SS3D_First10_ArtPreview_Only`, self-rebuilding on load); the playable path is the Game scene with `Use3DPresentation`, which uses the same renderer, frame and palette.
- **D. Verified here.** Compile check 0 errors; dotnet core suite 270 passed. Calculated orthographic framing (padding 1.5, min 6 × 4): board width 44 % (FTUE-1), 33 % (FTUE-2), 62 % (5 × 5), 67 % (6 × 4), 70 % (7 × 5) of the screen; platform 76–88 %; identical at 9:16 and 9:19.5 since the fit is width-bound. **Not rendered**: no screenshot exists; the Game-view captures (9:16 for all ten, 9:19.5 for LEVEL-4 and LEVEL-5) are the user's next step.

## 2026-10-30 — Town environments: POLYGON Town Pack, 10 profiles, 10 scenes and a switcher (no gameplay, data or character changes)

- **Pack audit.** `Assets/PolygonTown` (700 prefabs, 25 materials on the Built-in Standard shader, 5 m road/sidewalk/ground modules, houses 5.7–18 m, shops 10–15 m, vehicles 5–11 m). Every prefab path used by the profiles was checked on disk; sizes from the FBX files. Materials are converted with the same `StandardUpgrader` routine as the Fantasy Kingdom pack (`SS3DKitSetup.ConvertMaterials(root)`).
- **Architecture.** `TownEnvironmentProfile` (ScriptableObject: ground/pavement, plaza floor tints and margin, cover/wall props for authored cells, north road/sidewalk/buildings/vehicles, south road/vehicles/props, fences/hedges, trees, bushes, lamp/bench/bin/accents, lighting, seed) and `TownEnvironmentGenerator` (open plaza with a pavement margin and low kerb, no enclosure; sidewalk–road–sidewalk and a row of buildings facing the plaza across the visible width in the far band; low props, fences or hedges, benches, bins and trees in the near band; ground over the visible area). All placements are outside the plaza, deterministic from seed + stage id, colliders stripped, and near-band heights are clamped so nothing can hide the last node row at the 58° view. `SS3DLevelRenderer.overrides` lets a profile supply cover props, the wall prop and floor tints without touching the shared palette; `SS3DPreviewController.townEnvironment` swaps the courtyard frame for the generator.
- **Profiles (Assets/_ShadowSteps/Art3D/Town/Profiles).** 01 Quiet Neighborhood (houses, white fences, lawns), 02 Garden Path (hedges, benches, flower patches, shed), 03 Residential Driveway (garage house, pickup + convertible, wood fence), 04 Street Corner (shops, bus, roads north and south, stop and bus-stop signs), 05 Delivery Yard (concrete, delivery truck, boxes, barrels, carts, wood fence with gate), 06 Neighborhood Park (trees, benches, playground, fountain), 07 Town Plaza (shops + church, fountain, bike stand, warm paving), 08 Backstreet Alley (shop backs, dumpsters, trash, garbage truck, cooler light), 09 Garage Compound (two garages, parking strip with pickup and truck), 10 Gated Community (large houses, white fences with gates, security lights). Cover props follow the theme (garden boxes/pot plants/compost, cardboard boxes/barrels, dumpsters/trash bags); wall cells are hedge blocks, garden boxes, propane tanks or dumpsters fitted to the cell.
- **Scenes.** Shadow Steps ▸ 3D ▸ Town ▸ *Generate 10 town environment scenes + preview* writes `Assets/_ShadowSteps/Art3D/Levels/Town/SS3D_Town_01_Quiet_Neighborhood.unity` … `SS3D_Town_10_Gated_Community.unity` (stages FTUE-1…5, LEVEL-1…5), validates node/cover/wall/guard counts per scene into `Docs/Reports/Town_Scene_Manifest.json`, and `SS3D_Town_Preview.unity` with `TownPreviewSwitcher` (inspector buttons 1–10, previous/next) for flipping between all ten. Auto-setup version 14 runs it once.
- **Camera.** The perspective fitter from the rendering pass, padding = margin + 0.5 tiles for every town scene, so the angle and framing are identical across the ten; width-bound at 9:16 and 9:19.5.
- Verified: compile check 0 errors. Not rendered; the Synty façade orientation (assumed +Z, rotated to face the plaza) and vehicle orientation along the road are unverified.

## 2026-11-01 — Town Plaza: one authored environment recreating the approved mock (no gameplay, data or character changes)

Brief: `ArtReferences/ShadowSteps_Town_Target.png` is the art-direction reference; the ten generated town scenes did not match it. One deliberately composed scene replaces random placement for this look.

- **Headless rendering is now possible while the editor is open.** The Unity GUI holds `Temp/UnityLockfile`, so `Tools/render_batch.sh` syncs the working tree into a scratch clone (`../StealthEscape_render`, Library included, 4.6 GB, not in git) and runs an editor method there in `-batchmode` with graphics. `SS3DBatchRender` (editor) renders a scene's main camera to a PNG at 1080×1920 (optionally with the HUD reserved bands drawn) and renders prefab lists into contact sheets with a bounds/material JSON. Also in the editor: Shadow Steps ▸ 3D ▸ Render ▸ *Render open scene to PNG*. Outputs live in `Docs/Reports/Renders/`.
- **Pack audit by eye.** `sheet_town.png` (125 Town prefabs) and `sheet_fk.png` (73 Fantasy Kingdom prefabs) with their `.json` index. Findings: the Town pack is suburban-American (white siding houses, dark roofs, flat-roof shops, picket/wood fences, cardboard boxes); it has no wooden crates, no flower planters and no iron railing. Fantasy Kingdom supplies crates and flower planters; the white picket fence tinted near-black stands in for the iron railing; the project's own glb banner, lantern and gate pillars are reused.
- **Authored layout system.** `TownPlazaLayout` (ScriptableObject): plaza margins and tints, step ring, cover/wall props, connection colour/width, exit scale, camera (orthographic, pitch 50°, yaw 45°, padding, HUD bands), lighting (sun, shadowless fill along the view, ambient, post values) and an ordered list of `Placement`s, each anchored to the plaza edges (−1…1 per axis) with a tile offset, yaw, fit mode (kit scale / height / footprint / box), tint, elevation and optional repeats, plus the mock element it stands for. `TownPlazaComposer` (runtime) builds it around the renderer: pavement margin in its own flagstone tints, grout, kerb and step ring, ground slab over the visible rect, then every placement; no random numbers, colliders stripped, nothing inside the grid. `SS3DPreviewController.plazaComposer` takes precedence over the town generator and the courtyard frame. Renderer additions behind `SS3DPropOverrides`: cover height, grout colour, connection colour/width, exit scale, plain flagstone texture (`SS3DArtTuning.FlagstoneTexture`), `FillStoneTiles` for pavement.
- **Scene.** Shadow Steps ▸ 3D ▸ Town ▸ *Generate authored Town Plaza scene* (`TownPlazaSetup`) writes `Art3D/Town/TownPlaza_Layout.asset` (37 placements, code-authored; re-running overwrites it) and `Art3D/Levels/Town/SS3D_Town_Plaza_Authored.unity` for LEVEL-4 "Double Trouble" (7×5, two guards, three X cells), validates node markers = floor nodes (32), cover props = X cells (3), guard actors = guards (2), no decoration centred inside the grid, and writes `Docs/Reports/Town_Plaza_Manifest.json` and the mapping table `Docs/ART3D_TOWN_PLAZA_MAPPING.md`. Camera yaw 45° puts the west edge at the bottom-left, north top-left, east top-right, south bottom-right; tall pieces sit only on the north and east edges.
- **Rendered and compared** (`Docs/Reports/Renders/Town_Plaza_Authored_1080x1920.png`, `_guides.png`): four iterations. Matches the mock: diamond lattice with ~150 px node spacing, plaza cropped at both sides, yellow house left, railing + gate + fountain + buildings top, grey house right, railing/planters/bench/barrels/crates lower-left, tree and a roof in the bottom-right foreground, warm beige flagstones, white nodes and paths, red detection rings, blue guards and a black-suited spy, exit pad enlarged ×1.7. Remaining differences: Town-pack houses are American clapboard with black roofs, not Mediterranean render with shutters/balconies (a whole-prefab tint cannot recolour roofs separately); the fence is a picket silhouette, not wrought iron; no security camera (the stage has none); no stone steps at the bottom-left; the façade banner and the yellow house's lantern are hidden by the fitted building; guard vision is drawn as cells, not cones; shadows run to the right from a sun on the left while the fill light keeps the plaza-facing façades lit.
- Verified: dotnet compile check 0 errors; three headless Unity runs with the scene validated clean; frames inspected. Not done in the GUI editor (it is open with the project lock); the Game view should look identical when `SS3D_Town_Plaza_Authored.unity` is opened at 9:16 since the scene self-rebuilds from the layout asset on load. The other nine environments were left untouched.

## 2026-11-01 — Town Plaza composition V2: near-upright camera, foreground filled, subtler paths (no gameplay or data changes)

Review of the first frame asked for an upright board, less empty ground at the bottom, a cohesive town and quieter path lines.

- Camera: orthographic, pitch 52°, yaw 12° (was 45°), padding 0.85 tiles, HUD bands 10 % top / 18 % bottom. Rows now read as rows; the board is width-bound at about 124 px per tile. Character facing is unchanged (N/E/S/W still map to +Z/+X/−Z/−X).
- Layout re-anchored for that view: yellow house on the west edge facing east, grey house on the east edge facing west, railing with a central gate between stone pillars along the north edge with the fountain, shop, church and trees beyond, and the south edge filled with railing, planters, bench, barrels, crates, hedge, bushes, a tree and the terracotta-roofed house at the bottom right. Side plaza margins 0.4 tiles so the façades sit at the frame edges.
- Ground: the flat slab is now covered by half-tile flagstone pavers in two close tones (`TownPlazaLayout.outerPavement`, `outerPaverSize`; `SS3DLevelRenderer.FillStoneTiles` takes a tile size), lowered 6 cm under the plaza so the raised plaza keeps its step ring.
- Paths: connection colour (0.93, 0.90, 0.84) at width 0.028 (was near-white at 0.06); node markers, red detection rings and the ×1.7 exit pad unchanged. The edge-on façade banner was dropped.
- Render: `Docs/Reports/Renders/Town_Plaza_Composition_V2.png` (+ `_guides.png`), generated via `GenerateAndRender -ssName Town_Plaza_Composition_V2`. Manifest clean: 32/32 node markers, 3/3 cover props, 2/2 guards, 39 placements, no decoration inside the grid. dotnet compile check 0 errors. Not opened in the GUI editor.

## 2026-11-02 — Town Plaza composition V3: board-fitted upright camera (no gameplay or data changes)

Review of V2: board too small, too top-down, empty pavement at the bottom. Changes, all on the layout asset and its generator:

- Camera: orthographic, yaw 0°, 46° down (shallower so façades and characters show depth), fitted from the playable bounds only (`SS3DLevelRenderer.BoardBounds`, never the decoration) with 0.6 tiles of padding: 7 + 1.2 = 8.2 tiles across 1080 px, so the grid floor spans about 85 % of the width and the node centres about 74 %. The framing band starts at 30 % so the board sits low in the frame while the town fills the top; the HUD safe areas stay 14 % / 22 % (`TownPlazaLayout.hudTop/hudBottom`, drawn by the render guides) and no node, actor or hazard touches them.
- Town re-anchored for the upright view: with the board at 85 % of the width there is no room for side buildings, so the yellow and grey houses now frame the top with the gate, pillars and short railings between them, fountain, shop, church and trees beyond; the side margins carry only low props (lamps, bench, barrels, crate, planters, bush) outside the grid; the bottom carries the railing with planters, crates, barrel, hedge, bushes, a tree and the terracotta-roofed house under the control band. Cover height 0.5 so crates hide less of the node behind them at 46°.
- Sun from the front-left (46°/46°) so the south-facing façades are lit; shadows run up-right.
- Render: `Docs/Reports/Renders/Town_Plaza_Composition_V3.png` and `_guides.png`. Manifest clean (32/32 nodes, 3/3 cover, 2/2 guards, 38 placements, nothing inside the grid); dotnet compile check 0 errors; not opened in the GUI editor.
- Inherent limit: a 7×5 stage in 9:16 is width-bound, so the board can only ever be about 3.4 tiles tall on screen at this pitch (≈ 30 % of the height); the mock's proportions come from a 4-wide board.

## 2026-11-02 — Minimalist tabletop prototype: isolated scene, procedural slab only (no gameplay, data or character changes)

Brief: a premium miniature tabletop look from simple Unity geometry, independent of every town/courtyard/palace scene.

- `MinimalBoardComposer` (runtime, `Art3D/Scripts/Runtime/Minimal/`): floating slab under the grid (chamfered box, top face at tile level, open 0.7-tile margin, bevelled top edge, darker side box and an inset base plate), pastel ground plane 1.4 units below that catches the soft drop shadow, and nothing else. It supplies renderer overrides: untextured warm tiles (`plainTiles`, inset 0.965 for fine seams, smoothness 0.45), seam colour, thin warm-grey connection lines (0.026), exit pad ×1.5, cover obstacles = the project's own `Obstacle_Crate_1x1` and `Obstacle_Stone_Block_1x1` glb models at 0.62 tiles. Camera orthographic, 46° down, yaw 0°, 1.0 tile padding, HUD bands 14 % / 22 %. Lighting: warm sun (48°/30°, soft shadows 0.6), cool fill, pastel ambient; post: slight negative exposure, low bloom, vignette 0.14, no depth of field.
- Renderer: `SS3DPropOverrides.plainTiles / tileSmoothness / tileInset` (used by `CreateStoneTile`). `SS3DPreviewController.minimalBoard` takes precedence over all other environments.
- Shadow Steps ▸ 3D ▸ Minimal ▸ **Generate minimalist tabletop scene** (`MinimalBoardSetup`) writes `Art3D/Levels/Minimal/SS3D_Minimal_Tabletop.unity` (LEVEL-4) and validates it into `Docs/Reports/Minimal_Tabletop_Manifest.json`: 32/32 node markers, 3/3 cover props, 2/2 guards, exactly 4 decoration objects (slab top, side, base, ground).
- Rendered headlessly: `Docs/Reports/Renders/Minimal_Tabletop_1080x1920.png` (+ `_guides.png`); first pass was over-exposed (near-white tiles and ground), second pass darkened the palette and dropped exposure. dotnet compile check 0 errors. Not opened in the GUI editor. The brief received ended after its art-direction section; camera, framing and lighting followed the earlier Town Plaza feedback (upright board, 14 % / 22 % HUD bands).

## 2026-11-02 — 2D presentation prototype: ShadowSteps.Art2D (generated sprites, captured character frames, isolated LEVEL-2 scene; no gameplay or data changes)

Brief: replace the 3D presentation with 2D artwork wherever practical, keep the engine independent, one isolated prototype scene on LEVEL-2 Clockwise.

- **Architecture.** New assembly `ShadowSteps.Art2D` (refs Core, Data, Runtime) beside the placeholder sprite board and the 3D layer; same adapter pattern as D-030. `ShadowSteps2DRenderer` builds every visual from the `Stage` (board panel + shadow, floor tiles, nodes, 9-sliced connection lines, padlock doors, crates on X cells, stone walls on # cells, exit, key, wall cameras with bracket + rotating head, EMP badge + pips, smoke clouds + pips, characters, NEXT and rotation markers, reachable rings, hazard/preview/target overlays) and exposes the same setters as the 3D renderer; it never computes rules. Coordinates are the runtime `BoardLayout` (x → +X, row → −Y), so the existing camera fit and tap/swipe picking apply unchanged. Nine sorting layers were added to TagManager (Board, Floor, Paths, Nodes, Overlays, Props, Actors, Markers, FX); actors are Y-sorted within their layer.
- **Sprites.** `SS2DSpriteGenerator` (editor) paints 25 original PNGs (256², SDF shapes with vertical gradients, soft drop shadows, outlines and top highlights; 9-slice borders on the panel, panel shadow and line) into `Art2D/Sprites/`, sets the importers (sprite, PPU 256 so one sprite = one node, no mips, bilinear, uncompressed) and fills `Art2D/SS2DSpriteLibrary.asset` (sprites, sizes, the colour palette from the brief: cream board, muted teal background, navy player accent, blue guards, red/amber zones, green exit).
- **Characters.** `SS2DCharacterCapture` (editor) renders the characters_5_02 prefabs the game already uses (m_1 spy, m_2 guard) with their own Idle/Walk clips via `AnimationMode.SampleAnimationClip` into transparent 256² frames: 4 facings × (2 idle + 6 walk) × 2 characters = 64 PNGs in `Art2D/Sprites/Characters/`, orthographic 34° three-quarter view, feet at 12 % (sprite pivot), identical framing per character so frames line up; root-motion drift is re-centred per frame. The originals are untouched; the runtime references only the PNGs. `SS2DCharacter` plays the frames (idle 2 fps, walk 10 fps) and keeps a soft contact shadow and, for guards, a facing chevron.
- **Scene and HUD.** `SS2DPrototypeSetup` writes `Art2D/Demo/ShadowSteps_2D_Prototype.unity`: renderer + `SS2DPreviewController` (initial engine state: hazards, reachable nodes, markers), orthographic `SS2DCameraFitter` (HUD bands 14 % / 22 %), and `SS2DHudCanvas`, a screen-space-camera Unity UI canvas (title band with stage name and intro, MOVE / TOOLS controls, action counter). The playable path is `SS2DSessionBridge`: set `Use2DArtPresentation` + the library on the Game scene's composer and it mirrors the session events (moves tween with walk frames, guards/cameras turn, doors, key, clouds, EMP, previews, smoke targets, capture/won/lockdown tints) while the placeholder board stays hidden but keeps its fitter and picking.
- **Verified (headless, clone).** `Docs/Reports/Art2D_Prototype_Manifest.json`: 22/22 node sprites, 30/30 connection lines, 2/2 crates, 1/1 guard, 1 exit, 2 vision overlay cells = 2 engine cells, 0 MeshRenderers, 0 SkinnedMeshRenderers, 118 SpriteRenderers, scene dependencies contain no characters_5_02 / Polygon / Art3D / fbx / glb asset, 64 character frames complete, capture report clean (Idle 25.0 s, Walk 1.71 s). Renders: `Docs/Reports/Renders/Art2D_Prototype_LEVEL-2_1080x1920.png` (+ `_guides.png`) and verification frames `Art2D_Check_LEVEL-16_1080x1920.png` (walls, camera, amber cells) and `Art2D_Check_LEVEL-11_1080x1920.png` (key, padlock door). dotnet compile check 0 errors. Not opened in the GUI editor; PlayMode with the bridge not yet run.
- Not converted: the other 29 levels (same renderer, no per-level work needed), the Game scene flag is off by default, the 3D scenes are untouched.

## 2026-11-02 — 2D visual polish milestone 1: portrait rotation, flat artwork, mobile HUD (LEVEL-2 only; no gameplay or data changes)

- **Portrait rotation.** `ShadowSteps2DRenderer.autoRotatePortrait` draws wider-than-tall stages turned 90° counter-clockwise (logical north on the left, logical east at the top) so LEVEL-2's 6×4 shows as 4 columns × 6 rows. Only drawing and input transform: `Pos(c)` (drawn position), `ToGrid(world)` (inverse, for taps), `View(d)` / `Logical(d)` (facing art and swipe/arrow-key mapping) and `RotationFor(d)`. `ViewLayout` (width/height swapped) feeds `SS2DCameraFitter`. `InputController.ScreenToLogicalDirection` is a new optional hook the 2D bridge sets, next to the existing node/camera picking overrides; the bridge now retires the runtime `CameraFitter` and uses its own. Level data, rules, `BoardLayout` and the placeholder board are untouched.
- **Verified by tests.** New `ShadowSteps.Art2D.Tests.EditMode` assembly (`SS2DPortraitRotationTests`, run headlessly in the clone: 4/4 passed, `Docs/Reports/tests_Art2D_EditMode.xml`): every cell of LEVEL-2 round-trips through `ToGrid(Pos(c))` including off-centre picks and no two cells share a drawn position; the drawn node extents are 3 × 5 units; for all four directions and both rotation states `Logical(View(d)) == d`, the drawn delta of a logical move equals the unit vector of `View(d)`, and a swipe in the drawn direction requests the original logical direction; the guard-vision overlay shows exactly the engine's 2 cells at their rotated positions above the guard (logical East → drawn North); square/tall stages are not rotated.
- **Flat artwork.** Regenerated sprites: flat board panel with a soft outline and a light contact shadow (no black drop shadow), flat rounded tiles tinted cream / warm grey in alternation, flat white nodes with a thin outline, thin connection lines (0.045), a redesigned flat crate (dark frame, three planks, seams, straps, nails), flat walls, exit as a baked bright-green disc with a white ring and chevrons that pulses at runtime (`SS2DPulse`, 8 %), and shadows/highlights removed from key, padlocks, camera, cloud, badges and markers. Same 256 px, PPU 256 assets.
- **Readability.** Characters at 1.25 node spacings with a softer contact shadow; guard facing badge, NEXT arrow and rotation marker float above the head so they read for every facing; danger cells stay translucent red (guards) / amber (cameras) exactly as the engine reports them.
- **HUD.** `SS2DHudCanvas` redesigned: top rounded panel with LEVEL n + title, ACTIONS counter, pause and retry; 3★/2★ thresholds under it; bottom rounded panel with a wide WAIT and SMOKE / EMP buttons with counts only when the stage carries charges; a hint line; no Undo. The board is fitted between 11.5 % / 16.5 % bands (library `hudTop`/`hudBottom`), so the rotated 4×6 board fills the width.
- **Verified.** Manifest clean (22/22 nodes, 30/30 lines, 2/2 crates, 1 guard, 2 = 2 vision cells, 0 mesh renderers, no 3D dependencies, 64 frames); renders `Art2D_Prototype_LEVEL-2_1080x1920.png` (+ guides) and the LEVEL-16 / LEVEL-11 check frames (7×5 stages rotate to 5×7 as well). dotnet compile check 0 errors. Not run in Play mode. One regression caught on the way: a blanket text replace made `Pos()` call itself (stack overflow crash in the editor process); fixed and covered by the round-trip test.

## 2026-11-02 — 2D visual style phase 2 (LEVEL-2 only; no gameplay, data or framing changes)

- Board: softer, lighter contact shadow (alpha 0.16, wider blur, smaller offset), tile seams at 30 % alpha with closer cream / warm-grey tints, flat nodes enlarged to 0.34 with a darker outline and centre dot, thin lines kept at 0.045, reachable highlight a calmer teal ring.
- Characters: frames recaptured at 512 px (64 PNGs, PPU 512) for crisp edges, height 1.32 spacings, a coloured base ring under the feet (navy for the spy, blue for the guard) anchoring each token to its node, facing badge enlarged above the head, rotation marker 0.30.
- Effects: guard/camera danger cells with lighter fills (alpha 0.24) and thinner borders plus a slow alpha fade (`SS2DFade`, 16 %, runtime only); exit toned down (0.70 size, 6 % pulse, softer glow); cells remain exactly the engine's.
- HUD: compact pill at the top (LEVEL n · title, actions counter with caption, round pause / retry), 3★/2★ thresholds under it, large circular WAIT bottom-right, round SMOKE / EMP buttons with charge badges bottom-left only when the stage carries charges; no panels, no hint text. UI shapes are generated at exact reference-pixel size (`SS2DRoundedRect.Shape`); the HUD hierarchy is rebuilt on every `Show` because its generated sprites are DontSave and were null after the saved scene reloaded (that, not slicing, was why the panels rendered as plain squares).
- Headless runs: two consecutive SIGSEGV crashes in Unity's Burst compiler background thread during sprite import (not project code); fixed by clearing the clone's `Library/BurstCache` and passing `--burst-disable-compilation` to the batch run (now the default for `render_batch.sh` callers to use). Manifest clean (22/22, 30/30, 2/2, 1 guard, 2 = 2 vision cells, 0 mesh renderers, no 3D deps, 64 frames); render `Art2D_Prototype_LEVEL-2_1080x1920.png`. dotnet compile check 0 errors. Not run in Play mode.

## 2026-11-02 — 2D Clockwise level playable in Play mode (vertical slice; visuals unchanged, no rule/data changes)

- **Composition.** `ShadowSteps_2D_Prototype.unity` now carries a `GameSceneComposer` (`Use2DArtPresentation` on, `Art2DLibrary`, `DebugStageId` = LEVEL-2, `HudPrefab` = `Art2D/Prefabs/SS2DHud.prefab`). In Play mode the composer builds the session, input, placeholder board (hidden) and HUD exactly as the Game scene does; `SS2DSessionBridge` attaches through `GameSceneHooks`, `GameFlowController` attaches too (pause/settings/results/failure/onboarding screens, win recording, sounds), so Ghost, stars and progression are the existing code paths. The art preview (`SS2DPreviewController`) clears itself and steps aside when a composer is present; in edit mode it still draws the approved frame.
- **HUD.** `SS2DHudCanvas` implements `IGameHud` (+ new `IGameHudPause` in Runtime, honoured by `GameFlowController`): WAIT submits `StageAction.Wait` in Move mode, Retry calls `Session.Retry()`, Pause raises `PauseRequested` → `PauseScreen`, SMOKE/EMP toggle targeting modes (buttons exist only when the stage carries charges) and highlight while active; actions counter and thresholds come from the session/stage; rejections, capture, lockdown and win show a short message; a red alert flash on capture/alarm. No Undo.
- **Input with the rotated board.** `InputController` gained `ScreenToLogicalDirection` (swipes and arrow keys); the bridge sets it plus the node/camera picking overrides, retires the runtime `CameraFitter` and fits with `SS2DCameraFitter`. Capture plays a short shake on the spy (bridge) with the red HUD flash; the failure screen offers Retry.
- **Verified in Play mode (headless, clone, `Docs/Reports/tests_Art2D_PlayMode.xml`, 7/7 passed; EditMode rotation suite 4/4).** The tests load the real scene by path with an in-memory save and drive the same entry points the touch input uses:
  - scene composes with the 2D presentation, no Mesh/SkinnedMesh renderers, placeholder board hidden, HUD is the 2D prefab, art preview cleared;
  - tap on the drawn (1,2) node resolves to the logical cell and moves one step (T 0→1, HUD "1"); taps on a non-adjacent node and on the crate cell are rejected and cost nothing; an off-board move from the start is rejected;
  - swipe up maps to logical East (toward the exit) and the spy moves up on screen; swipe left maps to logical North and moves left;
  - four WAITs each count one action, the rotating guard's drawn facing equals the rotated `GuardTrack` pose every turn, the hazard overlay equals `Sight.GuardVision` every turn, period 4 restores the authored facing;
  - E then N walks into the guard's post-update sight: result Caught, failure screen visible, input locked, spy tinted; Retry resets T, position, tint, facing, hazards and unlocks input; the HUD retry button does the same;
  - the solver's 7-action solution wins with 3 stars (`StageScoring.Stars(7, par 7)`), results screen shown, save records Completed/BestTurns 7/BestStars 3/no Ghost and unlocks the next stage;
  - Pause opens the pause screen and locks input, Pop unlocks; SMOKE/EMP buttons are absent on LEVEL-2.
- Not exercised: real touch gestures through `InputController.HandlePointer` (the tests call the overrides and the session directly), audio/haptics, and the other 29 levels. dotnet compile check 0 errors. Render `Art2D_Prototype_LEVEL-2_1080x1920.png` unchanged in look.

## 2026-11-03 — 2D level structure locked: background layer, exit door, completion transition, asset slots (LEVEL-2 slice; no rule, data or scoring changes)

- **Standard structure.** `SS2DPrototypeSetup.CreateLevelScene(code)` builds any stage as `Art2D/Levels/SS2D_<code>.unity` with the vertical-slice composition (composer with the 2D flag + HUD prefab + edit-mode preview + fitted camera); the slice itself stays at `Art2D/Demo/ShadowSteps_2D_Prototype.unity`. Only LEVEL-2 was generated.
- **Background layer.** New sorting layer *Background*; `backgroundSprite`/`backgroundTint` on the library; the renderer places it behind the board and `FitBackground(cam)` scales it to cover the camera view after every fit (fitter, bridge and preview call it). Placeholder: 256×512 teal gradient with soft discs.
- **Exit becomes a door.** The exit node keeps its logical role and gets an `exitHighlight` ring (pulsing, green tint). The renderer computes the board edge the exit sits on (`ExitEdge`: E/W/N/S, nearest edge if interior), maps it through the portrait rotation (`DoorOutward`) and draws `exitDoorClosed` one `doorOffset` beyond the node, rotated to face the board, with a short doorstep path from the node. `ViewBounds` (panel ∪ door) now drives `SS2DCameraFitter`, so the door is always framed. `SetExitDoorOpen` swaps the sprite.
- **Completion transition.** `GameSceneComposer.WinTransition` is a new hook; `GameFlowController.OnWon` records the win (stars, Ghost, unlock, save) first and then hands the continuation (results screen) to the hook. `SS2DSessionBridge.DoorSequence`: success beat (0.45 s) → spy faces the door and walks from the exit node to the threshold with walk frames (0.55 s) → door opens (0.2 s) → spy steps in while scaling and fading out (0.35 s) → door closes (0.15 s) → with `autoAdvanceAfterDoor` the next stage opens in the same scene (`Composer.OpenStage`), otherwise the results screen appears. Capture/retry unchanged.
- **Asset-ready slots** documented in `Docs/ART2D_ASSET_SLOTS.md`: one ScriptableObject (`SS2DSpriteLibrary`) holds background, panel, tiles, nodes, lines, crates, walls, key doors, exit highlight, exit door (closed/open), key, cameras, cloud, badges, shadows, hazard cells, markers, VFX (`vfxGlow`, `vfxSparkle`, reserved) and the two character frame sets, plus sizes, colours and timings. Final art replaces placeholders by assigning sprites on the asset; no gameplay code references sprites.
- **Verified (headless, clone).** EditMode 5/5 (adds: door drawn beyond the exit on the east edge → above the board after rotation, off the logical board, inside the camera frame, sprite swap open/closed). PlayMode 7/7 (the win test now proves: win recorded before the sequence, no results modal, the spy walks up from the exit node, the door opens during the entry, then LEVEL-3 loads automatically in the same scene with the HUD and renderer rebuilt at T 0). Manifest clean. dotnet compile check 0 errors. Render `Art2D_Prototype_LEVEL-2_1080x1920.png` shows the door above the board.
- Placeholders still in use: every generated PNG in `Art2D/Sprites/` (board, tiles, nodes, lines, crate, wall, padlocks, exit highlight, door, background, camera, cloud, badges, markers, VFX) and the 64 character frames captured from `characters_5_02`; HUD shapes and icons are generated at runtime with the built-in font.
- Painter fix found on the way: `SS2DPainter.Shadow` used `Mathf.SmoothStep` as a GLSL-style smoothstep, so every "soft" shadow/glow (board shadow, crate shadow, character blob, exit glow, door shadow, background discs) was a hard, over-bright fill — the source of the earlier dark board band. Now an inverse-lerp falloff with clamped compositing; all placeholders regenerated. The HUD canvas moved to the FX sorting layer so the full-screen background cannot cover it.


## 2026-11-03 — Polished 2D camera vision (reference: ArtReferences/Shadow Steps Camera Vision Guide.png; no rule, data or solver changes)

- **`CameraVisionOverlayRenderer`** (`Art2D/Scripts/Runtime/`): reusable overlay fed the exact cells `Sight.CameraVision` reports. Translucent amber fill as one flat 2D mesh (one quad per detected cell; a side extends to the full cell when the neighbour is detected, so adjacent cells merge with no seams; boundary sides stay inset at 0.44), a bright outline mesh along the region boundary only, a soft glow sprite under each cell, an amber ring on every detected node, and a gentle alpha pulse plus a brief flash on alarm. Meshes use the engine's default sprite material on the Overlays sorting layer (glow 1, fill 2, outline 3, rings 4); guard cells keep their red rounded sprite with the red border raised to order 6, so an overlapped node shows red outer border + amber inner region. Positions come from the board renderer, so the portrait rotation is inherited. Blocked, occluded, smoked or EMP-disabled nodes are never drawn because the engine never reports them.
- **Camera sprites**: redesigned bracket (`camera_body`), housing with hood and status LED (`camera_head`, authored lens-up, turned to the facing; the bridge already slerps heads so rotating/sweeping turns animate), a separate tintable lens (`camera_lens`: amber active, cyan EMP-disabled), `sweep_marker` (two-way arc) for sweeping cameras and the circular arrow for rotating ones (flipped for counter-clockwise); camera size 0.80. Detected nodes' discs are tinted light amber (`nodeCameraTint`).
- **HUD**: lockdown pill under the top bar while the alarm budget counts down ("LOCKDOWN · n actions to escape"), amber alert flash on camera detection and lockdown (red stays for guard capture), "Camera spotted you!" message. Bridge flashes the camera region on `AlarmTriggered`.
- **Preview states**: `SS2DPreviewController.previewActions` applies action tokens (N/E/S/W, WAIT, SMOKE@x,y, EMP#i) through the real rules before drawing, so art reviews show genuine engine states (used for the EMP, rotating t=1 and sweeping t=2 frames).
- **Camera demo**: `SS2DPrototypeSetup.GenerateCameraDemo` builds the standard level scene for LEVEL-16 (`Art2D/Levels/SS2D_LEVEL_16.unity`, playable like the slice) and renders `Art2D_Camera_Active_LEVEL-16`, `Art2D_Camera_EMP_LEVEL-23` (after EMP#0: cyan lens, badge + 2 pips, no region), `Art2D_Camera_Rotating_t1_LEVEL-18`, `Art2D_Camera_Sweeping_t2_LEVEL-19` (the camera faces into cover at t=2: empty region is the true engine state) and `Art2D_Camera_Overlap_LEVEL-25` (guard + camera on one node, locked door and key in view).
- **Validated (EditMode `SS2DCameraVisionTests`, 8 new, 13/13 total; PlayMode 7/7).** For every state the drawn set equals the engine set, rings sit on detected nodes, the region mesh covers them, only floor nodes are drawn: fixed (LEVEL-16, unchanged after WAIT, cover X at (3,2) never highlighted); rotating (LEVEL-18, t=0..4: facing = authored rotated t steps, head rotation follows, period 4, rotation marker + flip); sweeping (LEVEL-19, t=0..5: facings S,S,E,E,S,S i.e. two adjacent facings held two actions each, sweep marker); closed door (synthetic 5×4 stage: node behind the door hidden, visible once `OpenDoors` has the bit); smoke (real SMOKE@ action: smoked node and the one behind it vanish); EMP (LEVEL-23: EMP#0 → 2 actions left, empty region, cyan lens, badge + 2 pips; after one WAIT 1 pip; after two WAITs vision and lens restored); guard + camera overlap (LEVEL-25: red borders above the amber region on shared nodes); portrait rotation (LEVEL-17: rotated positions map back to cells, head faces drawn West). The 3D-asset audit now exempts the overlay's flat meshes (they live on a sprite layer).
- dotnet compile check 0 errors; not run in the GUI editor. Reference side panel deliberately not reproduced in the gameplay UI.

## 2026-11-03 — Game scene switched to the 2D presentation (menus and pause ▸ Level Select)

User report: choosing a level from the pause menu showed the old environment. Cause: Level Select loads Home → Game.unity, whose composer had no presentation settings, so it built the placeholder sprite board. `Assets/_ShadowSteps/Scenes/Game.unity` now sets `Use2DArtPresentation` on, `Art2DLibrary` = `Art2D/SS2DSpriteLibrary.asset`, `HudPrefab` = `Art2D/Prefabs/SS2DHud.prefab` (3D flag off). New PlayMode test `GameScene_OpenedFromMenus_UsesThe2DPresentation` loads the real Game scene with a pending stage (LEVEL-16) and asserts the 2D bridge, HUD prefab, hidden placeholder board and camera region: PlayMode 8/8. All 30 levels now open with the 2D layer through the normal flow; nothing else changed.

## 2026-11-03 — Camera "node directly in front" request: solver impact measured, shipped rule kept pending decision

The user asked for the camera to cover the node directly in front of it. That node is the engine's blind spot (footprint starts one node out from the lens, browser parity, KNOWN_ISSUES #20); the 2D overlay already draws exactly what the engine computes. Switching the rule is a gameplay change, so it was measured with the solver (`--v3-export-validation`) before anything shipped:

| Rule | Result on the 30-stage pack |
|---|---|
| Shipped: 3 wide × 2 forward, starting one node out | parity 30/30 with `validation_v4.json` |
| Front node watched, 3 wide × 2 forward from the front node | **LEVEL-18 and LEVEL-19 unsolvable**; LEVEL-16/17/20/24 become forced-alarm (Ghost lost); LEVEL-20 par 10→6; LEVEL-23 no longer needs EMP; LEVEL-25 no longer needs EMP |
| Front node watched, 3 wide × 3 forward | LEVEL-18 and LEVEL-19 unsolvable; LEVEL-16/20/24/25 forced-alarm (Ghost lost) |

`Sight.CameraFootprintStart` (new constant, currently 1 = shipped rule) is the single switch; 0 watches the front node. Nothing else changed; dotnet compile check 0 errors; parity after restoring the rule: 0 differences. Adopting the new footprint requires redesigning LEVEL-18 and LEVEL-19 and re-exporting scoring for the other camera stages (`--v3-export-validation` into `validation_v4.json` and the stored pars in `levels_v4.json`), plus updating the parity tests and the camera test that asserts the blind spot.

## 2026-11-03 — Camera front-node footprint adopted (user "option 2", D-035)

- **Rule:** `Sight.CameraFootprintStart = 0`. A camera now watches the node directly in front of its lens plus the usual 3-wide × 2-deep region counted from that node; occlusion, cover, smoke, EMP and door handling unchanged. Facing into its own wall a camera still watches its front row (lens node and the two beside it).
- **Content (`levels_v4.json`, re-exported `validation_v4.json`):** LEVEL-18 redesigned (rows `#.....# / #...X.# / #..X..#`, start (1,3), rotating camera at (1,0) now counter-clockwise; par 9, Ghost); LEVEL-19 redesigned (rows `#...X.# / #.....# / #X....#`, start (1,1), sweeping camera unchanged; par 7, Ghost); LEVEL-23 corridor +1 node, exit (5,2) (par 5, EMP mandatory, forced alarm); LEVEL-25 corridor +2 nodes, exit (6,2), budget 1 (par 6, Smoke + EMP mandatory, Ghost). Re-scored: LEVEL-16 par 8 forced-alarm (Ghost lost), LEVEL-17 par 8, LEVEL-20 par 6 forced-alarm (was 10, Ghost lost), LEVEL-24 par 8 forced-alarm (Ghost lost). Blocked-node total still 55. Mandatory sets now: Smoke {21, 25}; EMP {23, 24, 25}; forced alarm {16, 17, 20, 23, 24}; Ghost {18, 19, 25}.
- **Tests:** camera-footprint, occlusion, smoke, wall-facing, EMP, blocked-node and alarm/solver fixtures rewritten for the new footprint (Room fixture camera moved to the bottom wall so every scripted step keeps its meaning; corridor fixtures extended one node). Parity tests assert browser par/Ghost only for the 22 stages without cameras and the new gadget/forced/Ghost sets. `Tools/run_core_tests.sh`: **270 passed, 0 failed**. Art2D in the headless clone: EditMode **13/13**, PlayMode **8/8** (rotation test now follows the authored turn direction).
- **Renders regenerated** (`GenerateCameraDemo`, `GenerateAndRender`): `Art2D_Camera_Active_LEVEL-16` shows the amber region touching the node in front of the lens; `Rotating_t1_LEVEL-18`, `Sweeping_t2_LEVEL-19`, `EMP_LEVEL-23`, `Overlap_LEVEL-25`, prototype LEVEL-2 and check frames for LEVEL-16/11 updated.
- Not run in the GUI editor; a device playthrough of LEVEL-16…25 is still pending (KNOWN_ISSUES #20 resolved, #42 resolved).

## 2026-11-03 — Final 2D art integrated into the playable prototype (courtyard background + directional characters)

- **Inputs (user-supplied, untouched):** `Art2D/Sprites/Backgrounds/Courtyard-Natural-Sides-1400x2000.png`, `Characters/Player/Suit-Faceless-{Up,Right,Down,Left}.png`, `Characters/Guards/Blue-Guard-Faceless-{Up,Right,Down,Left}.png` (1024×2048, figure on a painted base disc). PNG bytes verified identical before/after (md5).
- **`SS2DFinalArtImporter`** (new, `Art2D/Scripts/Editor/`): menu *Shadow Steps ▸ 2D ▸ Import final art*, batch `SS2DPrototypeSetup.ApplyFinalArtAndRender`. Sets importers to Sprite (2D and UI), single, alpha from input + alphaIsTransparency, no mips, bilinear, clamp, sRGB, compressed; characters PPU 2048 with pivot (0.5, 0.363) at the base-disc centre (measured from the alpha of all eight frames), max 1024; background PPU 256, max 2048. Assigns the four facings per set in `Direction` order (Up = N, Right = E, Down = S, Left = W), `includesBase`, `characterHeight` 1.5, the background with `FrameBoard` fit and the measured courtyard floor rect, `showBoardPanel` off, water clear colour. `Generate()` re-applies it after the placeholder generator and capture.
- **Runtime:** `SS2DSpriteLibrary` gains `backgroundFit` (CoverView / FrameBoard), `backgroundBoardRect`, `backgroundBoardPadding`, `showBoardPanel`, and `SS2DCharacterSet.includesBase`. `ShadowSteps2DRenderer.FitBackground` frames the painted ground on the tile grid with a uniform scale and only grows it to keep the view covered (no stretching); the board panel + shadow are optional. `SS2DCharacter` hides its ring/shadow for sets with a painted base and places the facing badge from the drawn sprite's bounds. `SS2DHudCanvas`: star thresholds and messages moved onto a slim second pill (they were unreadable over the bright mansion); lockdown pill shifted down to match.
- **Gameplay untouched:** no change to `ShadowSteps.V3`, stage data, input mapping or the portrait rotation; the rotated-input and camera-vision suites still pass.
- **Verified:** dotnet compile check 0 errors; headless clone EditMode `ShadowSteps.Art2D.Tests` **13/13**, PlayMode **8/8**; render `Docs/Reports/Renders/Art2D_FinalArt_LEVEL-2_1080x1920.png` (LEVEL-2 prototype, HUD, guard cone, exit, crates, both characters on their nodes). GUIDs of the new `.meta` files resolve in the main project.
- **Not done / notes:** only the LEVEL-2 prototype scene was rebuilt (the Game scene uses the same library, so it inherits the art at Play). Characters have one still per facing (no walk cycle yet). The placeholder exit door still sits on the mansion's red-carpet entrance for north exits. Stray user folders `Sprites/Player/` (duplicate copies) and `Sprites/Characters/untitled folder/` were left as found.
- **Follow-up (user):** floor tiles removed over the courtyard. New library flag `showFloorTiles` (default on; the importer sets it off), `ShadowSteps2DRenderer` skips the per-cell tile sprites when off. Re-rendered `Art2D_FinalArt_LEVEL-2_1080x1920.png`; clone EditMode 13/13, PlayMode 8/8.

## 2026-11-03 — Camera mounts: wall stubs and poles replace grey block mounts (presentation only)

- **Rule (presentation):** `ShadowSteps2DRenderer.UsesPoleMount(stage, cam)`: a camera whose wall cell is on the grid boundary or has a wall neighbour is **wall-mounted** (camera on the wall-cell edge toward the lens node, over a new `cameraWallPlate` stub that lies just inside the wall cell, rotated with the mount face); a camera on a **lone wall cell inside the floor** is **pole-mounted** (`cameraPole`, pivot at the base on the cell centre, screen-upright regardless of the portrait rotation, head `cameraPoleHeight` = 0.36 spacings above; that cell's grey block is not drawn, the post base marks it). New library flag `showPerimeterWalls` (off for the courtyard via the importer) hides the outer ring of grey blocks that the background already paints. Logical camera cell, facing, `Sight.CameraVision`, solver and level data unchanged (core 270/270, no diff in Core/ or StreamingAssets/ from this milestone).
- **Sprites:** `camera_wall_plate.png`, `camera_pole.png` added to the generator; `SS2DPreviewController.OverrideStage` lets review frames use a stage built in code; batch `SS2DPrototypeSetup.RenderCameraMounts` regenerates sprites, re-applies the final art and renders the frames.
- **Validated (EditMode `SS2DCameraVisionTests`, 3 new → 16/16; PlayMode 8/8):** LEVEL-19 wall mount (stub sprite, bracket shown, no `Wall_3_0` block, camera exactly on the rotated wall edge, stub behind it inside the wall cell, overlay = engine cells); synthetic lone-wall stage `T-POLE` (pole sprite, bracket hidden, no block, post upright on a rotated board, base on the cell, head faces the logical facing, region = the three front-row nodes); perimeter walls draw with the flag on and vanish with it off while the stub stays. Shared check: Mount/Bracket/Head positions stay ≥ 0.45 spacing from every floor node (this caught the first pole height of 0.55 on the rotated board; lowered to 0.36).
- **Renders:** `Art2D_Camera_Mounts_LEVEL-19_1080x1920.png` (deliverable: sweeping camera on a wall stub, active amber region), `…_Pole_Demo…` (pole), `…_Rotating_t1_LEVEL-18…`, `…_EMP_LEVEL-23…` (cyan lens, badge, pips, no region).
- **Open:** interior wall runs (LEVEL-20/21/23/25 corridors) still draw as grey blocks because they are real level structure, not mounts (KNOWN_ISSUES #46).

## 2026-11-03 — Cone-based camera vision (70°, 2 spacings) replaces the rectangle; level data untouched pending review (D-036)

- **Engine (`Core/V3/Sight.cs`):** `CameraOrigin` = wall face (½ spacing from the wall cell toward the mount face); `InCameraCone` (forward ∈ (0, 2], |lateral| ≤ forward·tan 35°, tolerance 1e-6); `CameraVision` scans the floor nodes around the origin, applies the cone, `Obstructed` (walls, cover, closed doors; the mount wall ignored; corner contact blocks) and `Smoked`; overload taking (openDoorMask, clouds, cam, facing) shared by gameplay, preview and the renderer. New `RayDistance` / `CameraConeOutline` trace the visible silhouette through the same occluders for drawing. Guard vision, `StageRules` (detection/lockdown/EMP timing), tracks and solver unchanged.
- **Renderer:** `ShadowSteps2DRenderer.PosF` maps continuous logical points with the portrait rotation; it records facings / EMP state / door mask / clouds from `SetCamera` / `SetDoor` / `SetClouds` and, in `SetVision`, traces one cone per active camera (`CameraCones`). `CameraVisionOverlayRenderer.Show(cells, cones)` draws each cone as a translucent amber fan with a soft boundary ribbon plus a ring/glow on every engine-detected node; the merged-cell region code is gone. Mount art (wall stub / pole) unchanged and independent of the detection.
- **Tests (core 271/274; Art2D EditMode 17/17, PlayMode 8/8):** new — cone boundaries with tolerance (`InCameraCone` edge/reach/behind/beside), footprint from all four wall faces and facings, sideways and into-wall facings, closed vs unlocked door, smoke on the lens node and expiry restore, cone outline apex/clipping/forward plane/smoke, cover beside the lens; rewritten — occlusion per node, blocked-cover filter, EMP corridor script, lockdown/budget scripts (Room fixture, trigger on the second step), forced-alarm corridor (budget 2), solver lockdown (budget 3); Art2D — overlay assertions now check the cone polygon contains every detected node (and no node when nothing is detected), one cone per active camera, LEVEL-17 rotated cone apex/direction, pole demo expects only the lens node. The three failing core tests are the shipped-data parity tests (see KNOWN_ISSUES #47).
- **Validation report:** `Docs/Reports/CAMERA_CONE_VALIDATION_2026-11-03.md` — all 30 stages, previous vs new par, solvability, Ghost, gadget requirements, problems, proposals. Problems: LEVEL-23 unsolvable; LEVEL-24 EMP optional + Ghost; LEVEL-18 par 9 → 8. Nothing in `Assets/StreamingAssets` was modified.
- **Renders:** `Art2D_Camera_Cone_LEVEL-19_1080x1920.png` (deliverable: sweeping camera on its wall stub, 70° cone, three detected nodes ringed), `…Cone_t2_LEVEL-19…` (cone clipped to a stub by the crate), `…Cone_Rotating_t1_LEVEL-18…` (cone along the wall), `…Cone_EMP_LEVEL-23…` (no cone, cyan lens), `…Cone_Pole_Demo…`.

## 2026-11-03 — Smart environment for LEVEL-19: bush planters, stone walls, stone camera mount (presentation only)

- **Assets used:** `Sprites/Obstacles/obstacle bush.png`, `obstacle wall side.png`, `obstacle wall front.png` (copied from the user's Downloads; byte-identical), `Backgrounds/Courtyard-Natural-Sides-1400x2000.png`, Suit-Faceless player and Blue-Guard directional sprites (LEVEL-19 authors no guards, so none appear). Imported via `SS2DFinalArtImporter` (PPU 1000 = one spacing; measured pivots). No newer mock exists in `ArtReferences/`; the courtyard render and the camera guide were the target.
- **`SS2DLevelArtLayout`** (new ScriptableObject, `Art2D/LevelArt/SS2DArt_LEVEL-19.asset`): per X cell BushPlanter / StoneWall (+ logical wall axis, offset, scale, sorting offset), per camera id StoneWall / Pole / Plate / Auto. LEVEL-19: (4,1) stone wall running off the camera boundary (logical Y → front piece after rotation), (1,3) bush planter, cam1 on a stone side piece. Library: `bush`, `wallSide`, `wallFront`, `bushSize`, `stoneWallSize`, `levelArt[]`, `defaultObstacleArt` (BushPlanter), `preferStoneCameraMounts`.
- **Renderer:** `DrawCover` chooses the art per cell (`CoverArt`), `StoneSpriteFor(axis)` picks side/front from the drawn axis, offsets rotate with the board; `Cover_X_x_y` names kept so the manifest audit still equals the X count. Camera mounts: StoneWall → the real wall piece flush with the wall face; Pole / Plate unchanged. Gameplay graph, `Sight`, solver and level data untouched.
- **Verified:** compile 0 errors; EditMode `ShadowSteps.Art2D.Tests` 20/20 (new: LEVEL-19 sprites per cell, cover count = `BlockedCount`, camera anchor unchanged, stone side mount, overlay = engine; unrotated board flips side/front; levels without a layout use the default art), PlayMode 8/8; core untouched (272/275, the 3 pending parity tests from D-036). Render `Art2D_Environment_LEVEL-19_1080x1920.png` (+ `…_t2…` with the sweep turned into the stub).
- **Placeholders remaining on this level:** exit door sprite (generated), node discs / path lines / rings (generated), HUD shapes; camera body/head/lens and the pole (generated); `camera_wall_plate` is no longer used on LEVEL-19.
- **Follow-up (user, same day): grey interior wall blocks removed.** `ShadowSteps2DRenderer.PlanInteriorWalls`: interior `#` cells (inside the boundary ring) are grouped into runs by their wall neighbours; each run is drawn with one stone piece per cell along its axis (side/front chosen from the drawn axis after rotation), a run carrying a camera is slid `wallRunCameraHug` = 0.36 toward that camera's face so the camera sits on the stone (its separate mount piece is dropped, logical anchor unchanged), and a lone interior wall becomes a bush planter. Library: `interiorWallArt` (StoneWall via the importer; Crate = old grey block), `wallRunCameraHug`. Layout entries may override any `#` cell. Tests: LEVEL-23 (5 interior cells → 5 stone pieces, 0 grey, hugged positions, camera on the run, overlay = engine, 22 wall cells unchanged) and LEVEL-11 (lone walls → planters): EditMode 22/22, PlayMode 8/8. Renders `Art2D_Environment_LEVEL-23_1080x1920.png`, `…LEVEL-11…`.
- **Follow-up (user): nodes and paths restyled to the Camera Vision Guide mock.** `node.png` is now a plain solid white disc with a soft contact shadow (no outline, no centre dot); `line.png` keeps its white bar with a faint shadow; the importer sets `nodeSize` 0.27, `lineWidth` 0.05, `nodeTint`/`lineTint` first white, then (user) **#463E3E** dark ink for both; the path shadow was strengthened. Camera-detected nodes still tint light amber, reachable rings stay cyan. Sprites regenerated, final art re-applied, LEVEL-19/23/11 re-rendered; EditMode 22/22, PlayMode 8/8.
- **Follow-up (user): guards cast a sight fan instead of red cell squares.** Presentation only — the rule stays two nodes straight ahead. `Sight.GuardVision` gained an overload taking (openDoorMask, clouds, pose) used by both gameplay and the renderer, and `Sight.GuardConeOutline` traces a narrow fan (`guardConeHalfAngleDegrees` = 22°, library) from the guard's node along its facing, reaching half a spacing past the last seen node and clipped by the same occluders / smoke as the detection; empty when the guard sees nothing. The renderer now owns a second `CameraVisionOverlayRenderer` instance (`GuardOverlay`, root `Overlays/GuardVision`, hazard red, sorting above the amber cones) fed the engine's guard cells + the fans; the old red `Overlay_Fill` cell sprites are gone (the `Overlay` pool still serves previews and smoke targets). Each overlay component now owns its root object (`CameraVision` / `GuardVision`) so audits find the meshes under the component. Tests: fan apex on the guard, covers exactly the seen nodes (and no neighbour), ends on the blocking wall's edge, vanishes in smoke; LEVEL-2 rotated fan; LEVEL-25 red rings above amber, one fan per guard; PlayMode hazard check reads `GuardOverlay.DrawnCells`. EditMode 23/23, PlayMode 8/8, core 272/275 (same 3 pending parity tests). Renders `Art2D_Environment_LEVEL-2_1080x1920.png`, `…LEVEL-25…`.
- **Follow-up (user): guard fan controls.** Visual only, on `SS2DSpriteLibrary.asset`: `guardConeHalfAngleDegrees` (10–30°, default 22) and new `guardConeOvershoot` (0–1 spacing past the last seen node, default 0.5 = the next cell edge). `Sight.GuardConeOutline` takes the overshoot; the rule (two nodes straight ahead) and the ringed nodes are unaffected. Test extended: 12° / 0 overshoot shrinks the fan, rings unchanged. EditMode 23/23.
- **Follow-up (user): curved far edge.** Both silhouettes are now circular sectors: `Sight.CameraConeOutline` clips every ray at radius `CameraRange` (not the forward plane), widened to the distance of any detected node that lies beyond it (a sideways camera's node at forward 2 / lateral ½ sits at 2.06) so the drawn sector always contains every detected node; `Sight.GuardConeOutline` uses one radius (`seen + guardConeOvershoot`) on every ray. Detection rules unchanged. Core outline test rewritten for the arc (edge rays at radius 2, central ray at forward 2, sideways widening to √4.25); the guard-fan test now reads `guardConeOvershoot` from the library (the user had tuned the asset to 14.9° / 0.308 in the Inspector). EditMode 23/23, PlayMode 8/8, core 272/275.
- **Follow-up (user): curve radius control.** `Sight.FarEdgeRayLength(reach, angle, curvature)` defines the drawn far edge: curvature 1 = circular sector centred on the apex (arc radius = reach), lower values move the arc centre behind the apex (radius = reach ÷ curvature, flatter edge), 0 = straight far edge; never shorter than the sector so detected nodes stay inside. Library fields `cameraConeCurvature` and `guardConeCurvature` (0–1, default 1) feed the tracers; the camera reach itself stays the gameplay constant `Sight.CameraRange`, the guard reach `seen + guardConeOvershoot`. Core test added (273/276), Art2D suites rerun.

## 2026-11-03 — Capture waits for the guard to finish its step or turn (2D presentation)

- `GameSceneComposer.CaptureTransition` (mirror of `WinTransition`): `GameFlowController` wraps the failure presentation (alert sfx, haptics, `FailureScreen`) in a continuation and lets the presentation own the timing; the placeholder flow is unchanged when no hook is set.
- `SS2DSessionBridge.OnCaptureTransition`: the running enemy tween completes first (guards walk, guards turning in place now switch facing halfway through the phase instead of snapping at the start, camera heads slerp), then `CaptureSequence`: capturing guard tinted, `captureBeat` (library, 0.30 s) with its red fan already on the new facing, HUD alert + "Spotted!" message (`SS2DHudCanvas.deferCaptureFeedback` / `PresentCapture`), spy shake, then the failure screen. Captures resolved before the enemy update (collision / seen before moving) skip the guard phase as before. `CaptureSequenceRunning` exposed for tests. State and rules untouched (the capture is already final when the choreography starts).
- PlayMode capture test now asserts no modal while the choreography runs, the guard's drawn facing equals the logical facing before the screen, and the screen afterwards: PlayMode 8/8, EditMode 23/23, core 273/276 (same 3 pending parity tests).
- **Follow-up (user): final character cast shadow.** The user's shadow art is now `Sprites/Characters/character_shadow.png` (2048², soft black, light from the upper right like the planters). Importer: PPU 2048, pivot (0.576, 0.424) = centre of the round base-disc lobe (measured from the alpha), max 1024; library `characterCastShadow`, `characterCastShadowSize` 1.85 (lobe ≈ the painted base disc), `characterCastShadowTint` white α 0.85. `SS2DCharacter` draws it under the body for player and guards (screen-space, never rotated, shown even though the frames include a base disc; the generated blob remains the fallback), the door fade uses the shadow's own alpha. Test added in the LEVEL-19 art test. EditMode 23/23, PlayMode 8/8.
- **Follow-up (user): guard snapped back after a capture and its fan did not turn.** Cause: the rules keep the clock at the old index on a capture (`StageRules.Caught`: "the failed action is shown at the old schedule index"), and the bridge derived guard/camera poses and vision from `state.T`, so after a capture by turning the guard was shown pre-turn with the old fan; `OnStateChanged` also cut the tween short. Fix (presentation only): `SS2DSessionBridge.ShownClock` presents a Caught state at T+1 when the capture came after the enemy update / gadget expiry (`ActionCompleted` outcome remembered, cleared on retry/load); a state published while the tween runs is applied when the tween finishes (`FinishTween`, pending state), never mid-step. The capturing guard stays tinted in the snapped state. PlayMode capture test now asserts the guard keeps its new facing (not the pre-turn one) and that the drawn fan is the T+1 fan covering the player: PlayMode 8/8, EditMode 23/23.
- **Follow-up (user): exit door removed.** Library `showExitDoor` (door sprite + door path) and `playExitDoorSequence` (walk/open/enter choreography) default off and are set off by the importer; the renderer skips the door objects (`HasExitDoor`), the bridge leaves `WinTransition` unset so a win shows the results screen directly with the spy on the exit node (the green exit highlight marks the goal; `DoorPosition`/`DoorOutward` are still computed for the optional door). Tests: door-off default + door-on-copy EditMode tests, PlayMode win test now expects the results modal and no auto-advance. KNOWN_ISSUES #44 resolved.
