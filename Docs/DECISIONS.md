# Shadow Steps — decisions log

Each decision records context, the choice, and what it rules out. Newest at the bottom.

## D-001 Reference simulation is law for levels 1–20
reference/build.py and reference/DESIGN.md define the rules. `GuardSchedule`, `VisionSystem`, `RulesEngine` and `Solver` are line-by-line ports. Quirks are preserved on purpose, notably:
- Guard phase is stored modulo the level period (lcm of guard cycles). Travel-facing guards (`patrol`, `loop`) derive facing from the previous phase, so at phase 0 after a wrap they show their *authored* facing, not their travel facing (level 10 guard A is the visible case). Tests pin this.
- The reference `cone_visible` returns true for the guard's own tile; `GetVisionCells` excludes it for display only. Occupancy is checked before vision anyway.
- Line of sight is a 12-samples-per-tile centre-to-centre walk; cells touched by the sample that are walls (or the locked door) block, endpoints excluded. No physics raycasts in logic.
Any future rule change must: update build.py, regenerate/re-solve, update validation.json, and bump `format_version` if data shape changes.

## D-002 Hand-written JSON parser instead of JsonUtility or Newtonsoft
levels.json uses nested arrays (`[[x,y],...]`), `null` for key/door and will need dictionaries for saves. `JsonUtility` cannot deserialize any of these. Newtonsoft (com.unity.nuget.newtonsoft-json) is not installed and the spec prefers no new dependencies. `MiniJson` (~300 lines, ordered objects, strict errors with char positions) lives in the engine-free Data assembly, so loaders are testable with plain dotnet. If a future need outgrows it (streaming, huge files) swap behind `LevelPackLoader`/`SaveSerializer` only.

## D-003 Assemblies and dependency direction
`Core` (no UnityEngine) ← `Data` (no UnityEngine) ← `Runtime` ← `UI` ← `Editor`. Tests.EditMode references Core + Data only, so it also runs under dotnet (Tools/run_core_tests.sh). Presenters subscribe to `TurnResult` events and never write game state.

## D-004 Solver is driven by RulesEngine.Step
Instead of a second copy of the rules, the C# BFS expands states through the same `Step` the game uses, in the reference action order with a FIFO queue. Parity tests show identical paths and identical explored-state counts to Python for all 20 levels, which validates both the engine and the solver in one go.

## D-005 Gadget "requirement" is a measured fact, not a claim
Constrained solves (see PROGRESS.md M1 table) show smoke is strictly required on levels 11, 17, 19, 20; level 14 ("Smoke and Mirrors") has an 11-move solution without smoke; EMP is never strictly required (12, 15, 18, 20 are solvable without it at the same length). Per CLAUDE.md the 20 levels are not changed. Level 14 and the EMP levels are flagged as "design intent, not proven" in KNOWN_ISSUES.md for the human playtest; the tutorial copy must not claim EMP is required.

## D-006 Verification strategy while the Unity GUI is open
Unity batch mode refuses to start while the editor holds the project lock. Until the user closes the editor (or runs the Test Runner manually), compile/test evidence comes from: (a) dotnet build of Core/Data/tests, (b) dotnet build of Runtime/UI/Editor sources against the Unity managed DLLs in Library/ScriptAssemblies (from M2 on), and (c) the editor's own Console after refresh. PROGRESS.md states which of these produced each claim.

## D-007 Deterministic .meta files
`Tools/gen_meta.py` writes metas with GUIDs derived from the asset path so scripts, folders, asmdefs and JSON can be committed before Unity imports them, and so GUIDs never churn between machines. Unity may add importer fields later; that is harmless.

## D-008 Save file format
Single JSON file in `Application.persistentDataPath/shadowsteps_save.json`, written via temp file + `.bak` rotation. Load order main → backup → defaults, keeping a `.corrupt` copy. Versioned (`version: 1`); unknown/missing fields fall back to defaults; newer versions are rejected rather than guessed.

## D-009 Product naming
The Unity project folder/product name is still "Stealth Escape" from project creation. Code, scenes and assets use the game name "Shadow Steps". `productName`, bundle ids and icons are changed together in M8 so a half-renamed build never ships.
