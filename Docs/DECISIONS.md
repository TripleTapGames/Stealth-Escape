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

## D-020 Shipped pack is schema 2; the reference stays V1 (superseded by D-026 for content)
`Assets/StreamingAssets/ShadowSteps/levels.json` is generated (`--migrate`) from `reference/levels.json` plus the documented level-18 patch and is never hand-edited; `reference/` is immutable V1 evidence. If a legacy level is changed again, the change goes into the exported file via the Level Editor and the diff is recorded in PROGRESS.md and in that level's `conversionNotes`.

## D-021 Level 18 redesign choice
Among the measured variants, moving the patrol to row 1 was the only one that removed the overlap *and* preserved the original 14-move solution and gadget/WAIT profile. Visual readability of the top row (rotating guard alone) improves as a side effect.

## D-022 Unlocking by catalog order
Unlock state is derived from completion records and the catalog order (`Progression.IsUnlocked`): a level is open when it is first in the catalog or its predecessor is completed. `SaveData.HighestUnlockedLevelId` is still written for compatibility but no longer decides anything, so packs can be reordered without migrating saves.

## D-023 Screens self-build; prefabs are generated from the same code
Every screen implements `BuildHierarchy()`; `UiPrefabSet.Create<T>` instantiates the prefab when assigned and otherwise builds the screen at runtime. The setup menu generates the prefabs by calling the same method, so designers can restyle prefabs in the editor while the fallback keeps the game runnable without any generated assets (important for tests and for a fresh checkout).

## D-024 Procedural audio until licensed assets exist
`AudioService` synthesises short tones and an ambient pad at startup (original, licence-free). Clips are addressed by the `Sfx` enum so real recordings can replace them later without touching callers. Audio never influences simulation.

## D-025 Detection is a per-level rule; V2 means "seen before or after the guard move"
Design change (2026-10-28, revised the same day from "before and after" to "before or after" on the user's correction): after the player acts, a cone captures if the player's landing node is inside guard vision at the guard poses before the phase advance **or** at the poses after it. Stepping into a cone is always lethal, even if the guard turns or walks away on that turn. "Guard vision" is the union of all cones. The guard reported as captor is the one that sees the player after the move, else the one that saw the node before. EMP freezes the phase, so both looks are identical; smoke conceals both looks.

The rule is stored on the level (`rules.detection`: `beforeOrAfterGuardMove` | `afterGuardMove`) rather than hard-coded so that:
- schema 1 files (and `LevelDefinition`'s legacy constructor) keep V1 semantics, which keeps the Python parity suite meaningful as proof of the engine port;
- schema 2 files default to the V2 rule and the shipped packs state it explicitly;
- `--migrate` preserves semantics (a V1 file converts to `afterGuardMove` with a conversion note) and `--migrate … v2rules` opts into the V2 rule.

Consequence: V2 is never more lenient than V1 (tested over the reachable state space of all 20 levels). The V1-era content relied on entering a cone as the guard turned away, so under the V2 rule only levels 1, 4, 12, 14 and tutorial 101 remain solvable; the rest are recorded as unsolvable (`"moves": -1`) in the validation files and carry no par. Content redesign is a separate task (KNOWN_ISSUES #18). The validation export now records unsolvable levels instead of refusing, so the shipped data always states the truth under the active rule.

## D-026 Content is generated from curriculum specs by the C# engine; the V1 reference is history only
The before-or-after detection rule (D-025) left 20 of 25 levels unsolvable, so the shipped packs are now produced by `Tools/CoreTestRunner/Generator.cs` from `Tools/LevelSpecs/*.json`: per level a size, guard templates (mode, range, cadence, route length), gadget charges, key/door, wall/cover/one-way counts, a required mechanic and a par band. The generator randomises within those ranges (deterministic seed) and keeps the best candidate that the real validator, solver and constrained solves accept, scoring for a unique shortest solution, a par in the upper half of the band, little dead space and no long idles. Levels 1 and 4 are kept from the migrated reference because they already fit the curve; 101 is kept as authored. `reference/levels.json` remains V1 evidence for the engine port and is no longer the source of the shipped content. The curriculum and all measured numbers are in `Docs/LEVEL_CURRICULUM.md`; the tests assert the measured mechanic sets so any regeneration is a deliberate change.

Why a generator rather than hand-authoring: 20 levels under a new rule needed to be provably solvable with proven mechanic necessity and a monotonic curve; the solver is the only reliable judge of that, and the specs make the design intent reviewable. Hand polish still happens in the Level Editor (export, edit, re-validate).

## D-027 V3 rules live in a new core (`ShadowSteps.V3`) beside the V2 core until the runtime switch is proven
The 2026-10-29 handover replaces guard cones with 2-node straight sight, adds wall cameras with a lockdown budget, turns doors into connections opened by crossing, makes Smoke a stationary cloud and EMP a camera disable, both free and targeted, and changes the turn order. Rewriting the V2 files in place would have broken the 206-test V1/V2 safety net while the new engine was unproven, so the V3 engine is a parallel namespace in the same engine-free assembly: `Stage`, `StageState`, `StageRules.TryApply` (the one transition used by runtime, preview and solver), `GuardTrack`/`CameraTrack`, `Sight`, `StageSolver`, `StageMetrics`, `StageValidator`, `StageJson`, `BrowserLevelImporter`. Prototype behaviours are ported exactly where the handover does not override them (camera footprint starts one node out, ping-pong endpoint facing, `budget || 4`). Once the Unity runtime runs on V3, the V2 core, its tests, generator and packs move out of the project (they are already backed up under `reference/`).

## D-028 Browser values are fixtures; the C# solver is the authority for pars and Ghost
`levels_v3.json` stores par, 3★/2★ thresholds and `ghostOffered` as written by `StageMetrics` (0-1 BFS, free gadgets). The browser export's `par`/`ghostOffered` and `playtest_validation_report.json` are kept only to assert parity (30/30 today). Any rule or data change re-exports scoring with `--v3-import` or `--v3-export-validation`; tests fail until the stored values match the solver again. The star formula is the provisional v3 one (`T3 = P + max(1, min(3, ⌊P/10⌋))`, `T2 = P + max(2, ⌈0.3P⌉)`) and is isolated in `StageScoring` for final tuning.

## D-029 V4 blocked cover is a third cell kind, not a wall and not a cut
The 2026-10-30 handover adds `X` cells (55 across 25 stages). They are modelled as `Stage.Blocked`: non-walkable (`IsFloor` false, so no node, no connection, no smoke target, no guard route, no key/exit/start) and fully opaque (`Stage.Occluders` = walls ∪ blocked feeds the one `Sight.Obstructed` routine, so guards, cameras, smoke throws and previews all treat the whole cell as solid, corner contact included). Walls remain '#' and may host cameras; blocked cover may not. Missing connections (`cuts`) remain sight-transparent. Rows are authoritative; the importer fails with the exact cell when `blockedNodes` disagrees, and unknown characters fail with coordinates. Routes are imported exactly as serialized (no re-extension). The shipped pack is now `levels_v4.json` / `validation_v4.json` (schema 3 unchanged); the V3 pack is kept in `reference/v3_shipped_backup_2026-10-30/`. Rendering uses a crate placeholder drawn slightly smaller than the cell while the rule uses the full cell (handover §2.1).

