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

