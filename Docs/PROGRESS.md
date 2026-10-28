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
