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
