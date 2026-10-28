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

## V2 decisions

## D-010 Legacy door becomes locked incoming edges plus an occluding node
V1 modelled the door as a tile that blocks the player and sight until the key is held. V2 edges carry locks, so `LevelGraph.BuildLegacyGrid` locks every edge *into* the door node and `LevelDefinition.Door` keeps the node as a sight occluder while the key is not held. Edges *out* of the door stay open. This reproduces V1 exactly (proved by the 20/20 replay and solver parity) and is the shape the migrator exports.

## D-011 Guard routes need edges, not permissions
A guard segment is legal when an edge exists in the travel direction (ping-pong routes also need the reverse edge, loops need the closing edge). Locks are ignored for guards: legacy levels 10 and 19 patrol through the door node. Guards moving through walls or missing nodes are errors.

## D-012 Guard overlap policy
Two guards sharing a node at the same phase is an **error** for schema-2 levels (LEVEL_DESIGN_PARAMETERS §5 default: reject rather than invent AI) and a **warning** for migrated legacy levels, because level 18 ("Shift Change") does it at phases 1, 5, 7, 11. Level 18 is scheduled for redesign in V2 M5.

## D-013 Independent cadences generalise the legacy step
`moveEvery` and `rotateEvery` replace the single `step`. Position uses `phase / moveEvery`, scheduled rotation uses `phase / rotateEvery`, travel facing derives from the previous movement update. With both equal to `step` the schedule and the period are identical to reference/build.py (tested for all 20 levels), so the phase-0 facing reset quirk is preserved.

## D-014 Schema 2 shape and loader policy
Root `schemaVersion: 2` with `levels[]` (or a single level object for editor exports). Board: `width`, `height`, `nodeSpacing`, `nodes`, `walls`, `cover[{pos, traversable}]`, `edges` = `"autoCardinalExceptBlocked"` with `blockedEdges`/`lockedEdges`, or an explicit `[{from, to, type: open|locked|oneWay, bidirectional}]` list. The writer always exports the explicit expanded list. Unknown fields warn; unsupported features (switchable edges, switches, decoy charges, cone angles other than 90, objectives other than reachExit) are rejected with a path so no content can depend on them silently. Legacy `format_version: 1` files still load and are migrated in memory with conversion notes that are exported with the level.

## D-015 Vision occluders are nodes only
`VisionSystem.BlockedPredicate` tests node flags (walls, cover) plus the locked door. Edges are never consulted, which satisfies "a missing edge stops movement but does not block sight" and is covered by a dedicated test.

## D-016 Restrictions actually simulated
`noWait` is enforced by `RulesEngine.Validate` (so the solver honours it). `maxMoves`, `noUndo`, `requiredGadget` are stored for UI/analysis only; the validator warns that `maxMoves` is not enforced. `smokeTurns` is configurable (default 2); `empTurns` must be 1.

## D-017 Tutorial pack and catalog merge
The five V2 tutorial levels live in their own schema-2 file (`levels_tutorial_v2.json`, ids 101–105, world 1 chapter 0). `AppServices` merges packs in a fixed order (tutorial, then legacy `levels.json`) into one catalog and rejects duplicate ids. Keeping files separate means the legacy file stays byte-identical to the reference while new content is authored freely. Level ordering in menus follows catalog order, not numeric id.

## D-018 Fresh traces are the only validation evidence
`validation_tutorial_v2.json` (and later `validation_v2.json` for the legacy set) is generated by the C# solver (`--export-validation`) and consumed by replay tests that also assert the solver still returns the same path and explored-state count. No trace is ever hand-written. Mechanic necessity (`WaitRequired`, `SmokeRequired`, `EmpRequired`, structural `KeyRequired`) is stored alongside and asserted per level.

## D-019 Authoring model lives in Data, the window only draws
`LevelAuthoringModel` holds all editing rules (edge cycling, resize, auto edges, conversion) so they run under dotnet tests; the EditorWindow is a thin IMGUI layer. The random wall search in the runner is a designer aid and never ships content automatically: every candidate must be inspected and committed by hand.

## D-020 Shipped pack is schema 2; the reference stays V1
`Assets/StreamingAssets/ShadowSteps/levels.json` is generated (`--migrate`) from `reference/levels.json` plus the documented level-18 patch and is never hand-edited; `reference/` is immutable V1 evidence. If a legacy level is changed again, the change goes into the exported file via the Level Editor and the diff is recorded in PROGRESS.md and in that level's `conversionNotes`.

## D-021 Level 18 redesign choice
Among the measured variants, moving the patrol to row 1 was the only one that removed the overlap *and* preserved the original 14-move solution and gadget/WAIT profile. Visual readability of the top row (rotating guard alone) improves as a side effect.

## D-022 Unlocking by catalog order
Unlock state is derived from completion records and the catalog order (`Progression.IsUnlocked`): a level is open when it is first in the catalog or its predecessor is completed. `SaveData.HighestUnlockedLevelId` is still written for compatibility but no longer decides anything, so packs can be reordered without migrating saves.

## D-023 Screens self-build; prefabs are generated from the same code
Every screen implements `BuildHierarchy()`; `UiPrefabSet.Create<T>` instantiates the prefab when assigned and otherwise builds the screen at runtime. The setup menu generates the prefabs by calling the same method, so designers can restyle prefabs in the editor while the fallback keeps the game runnable without any generated assets (important for tests and for a fresh checkout).

## D-024 Procedural audio until licensed assets exist
`AudioService` synthesises short tones and an ambient pad at startup (original, licence-free). Clips are addressed by the `Sfx` enum so real recordings can replace them later without touching callers. Audio never influences simulation.
