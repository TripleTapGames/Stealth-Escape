# Global Stealth / Knockout modes, guard first-encounter cards, rotating-guard arcs and full camera cones — implementation report (2026-11-04)

> **D-044 (no WAIT at all), same day:** the review package `Handoff/ShadowSteps_Knockout_NoWait_Review.zip` was compared with the shipped pack (its source SHA-256 matched; the semantic diff was exactly the listed edits: waitLimit 0 ×35, LEVEL-19 start (1,1)→(1,2), six knockout pars, three intros, LEVEL-24 ghostOffered) and installed. Code: Space and long-press no longer submit WAIT, `GameSessionController.WaitAllowed` (false) refuses WAIT from any caller, no WAIT counter/hint, authoring default waitLimit 0, campaign validated under Knockout + no WAIT (`RequireNoWait`), exporter and `validation_v4.json` under the shipped rules (mode + rules fields; resolves #47), rules revision `knockout-nowait-v3` with older Knockout records preserved as labelled legacy. Checks: the package's own harness on the installed pack and fresh engine — 35/35 solved and replayed with zero WAIT, stored scores matched, WAIT rejected without state change at every step, all offered Ghost routes replayed, no search limit hit (`Docs/Reports/KNOCKOUT_NO_WAIT_VALIDATION.json`); my `--v3-knockout-pars` re-solve agrees with the review pars on all 35 stages; core (dotnet) 322/322; Unity PlayMode 25/30 (same five pre-existing placeholder-suite failures, #55); Unity EditMode 359/359. Under Knockout/no-WAIT the mandatory-gadget sets are smoke {21, 25, 27}, EMP {23, 25, 30}, forced alarm {16, 17, 20, 23}. Game-view input (touch hold, keyboard Space) cannot be driven headlessly; the PlayMode suite proves `InputController.LongPress` and the session gate never advance time, and the keyboard WAIT binding is simply gone from the code.

> **Later the same day (D-043):** the user decided to ship Knockout only and to remove the WAIT button. The mode toggle and the "retry/replay in the other mode" buttons are gone from the HUDs and cards, the game always opens in Knockout, the level picker shows the Knockout best, and WAIT is the long-press on the board (or Space) with the remaining WAITs shown in the sub pill. Stealth remains an engine/tools/tests capability. Final counts after that change: core (dotnet) 321/322, Unity EditMode 358/359, Unity PlayMode 25/30 (same five pre-existing placeholder-suite failures, #55), Art2D PlayMode 17/17.

Handoffs implemented, in order: `ShadowSteps_Global_Modes_Handoff.zip` (repo root), `Handoff/ShadowSteps_Global_Modes_Guard_FTUE_Handoff.zip`, `Handoff/ShadowSteps_Guard_Arrows_And_Full_Camera_Vision_Handoff.zip`. Each was extracted into its own `/tmp` folder and read; nothing from the reference snapshots (`reference_levels_for_audit.json`, `level_26_combat_prototype.json`) was imported. Decisions: D-040 (modes), D-041 (guard cards), D-042 (full cones + arcs). Known issues added: #51–#56.

## 1. Changed files

**Core (plain C#, `ShadowSteps.V3`)**
- `Core/V3/StageState.cs` — `StageMode`, `StageModeUtil` (`RulesVersion = knockout-move-v2`), `StageState.Mode`, `Defeated` (64-guard bitmask, `MaxGuards`), `Initial(stage, mode)`, `IsDefeated`, `DefeatedCount`, mode/defeated in `Key()` and `ToString()`.
- `Core/V3/StageRules.cs` — `StageActionKind.Knockout`, `StageAction.Knockout(i)` / `KO#i` token + parse, reject reasons (`KnockoutUnavailable`, `BadGuard`, `GuardAlreadyDown`, `GuardNotAdjacent`, `FrontalApproach`, `DoorBlocksKnockout`), `KnockoutSide`, outcome fields (`Knockout`, `KnockedOutGuard`, `KnockoutSide`); `TryApply` normalises a move onto a live guard into the knockout (Knockout mode), validates it through `CheckKnockout` (current pose, behind/left/right, connected, door-free, not frontal, smoke irrelevant), removes the target and runs the unchanged counted pipeline against the survivors; `LiveGuardAt`, `KnockoutTargets`; `StageReplay.Run(stage, actions, mode)`.
- `Core/V3/StageSolver.cs` — `SolveOptions.Mode`, `MaxKnockouts`; `SolveResult.Knockouts`; metrics `Mode`, `KnockoutsInPar`, `ShortestWithoutKnockout`.
- `Core/V3/Stage.cs` — `KnockoutPar`, `KnockoutThreeStarActions`, `KnockoutTwoStarActions`, `HasKnockoutScoring`, `ParFor/ThreeStarFor/TwoStarFor(mode)` (+ builder, `ToBuilder`).
- `Core/V3/StageValidator.cs` — error above 64 guards.
- `Core/V3/Sight.cs` — `CameraConeGeometry` (nominal cone for graphics; detection untouched).

**Data**
- `Data/StageJson.cs` — optional `scoring.knockoutPar/knockoutThreeStar/knockoutTwoStar`.
- `Data/SaveData.cs`, `Data/SaveRepository.cs` — `Results` (Stealth/legacy) + `KnockoutResults`, `SelectedMode`, `LevelResult.RulesVersion`, save version 2 (v1 loads unchanged; untagged results = Stealth; knockout records with another rules version are dropped), per-mode `RecordWin`, shared `IsCompleted`/`CompletedCount`.
- `Data/StageCatalog.cs` — `StageProgression.RecordWin/Stars(…, mode)`.
- `Data/GuardOnboarding.cs` (new) — guard-card copy and contract, queue builder from live guards, per-mode seen keys in `SeenTutorials`, `MarkAllSeen`, superseded intros (FTUE-3/4/5).
- `Assets/StreamingAssets/ShadowSteps/levels_v4.json` — knockout scoring added to every stage by `--v3-knockout-pars`; layouts, guards, cameras, doors, charges, budgets, ids untouched (semantic diff: 0 stages differ beyond the three new fields).

**Runtime / UI**
- `Runtime/GameSessionController.cs` — `RulesMode`, `RulesModeChanged`, `SetRulesMode` (no-op on the active mode; otherwise clean restart that cancels the animator, preview and queued callbacks), `PresetRulesMode`, `TapGuard`, `KnockoutTargets`; retry keeps the mode.
- `Runtime/GameSceneComposer.cs` — presets the saved mode before the first stage, persists `SelectedMode`, `GuardHighlight` hook.
- `Runtime/TurnAnimationCoordinator.cs` — `Cancel()`. `Runtime/BoardView.cs` — defeated guards hidden, no vision, previews skip them.
- `UI/GameFlowController.cs` — wins recorded per mode, results per mode, "retry/replay in the other mode", guard-card queue (one at a time after the stage intro, board-session token, stale "Got it" ignored, highlight), queue cancelled/rebuilt on load/retry/mode switch.
- `UI/Screens.cs` — `ResultsScreen` mode line + switch button, `FailureScreen` switch button, `LevelSelectScreen` per-mode bests, `GuardIntroScreen` (new).
- `UI/GameHudController.cs` — STEALTH | KNOCKOUT buttons, mode hints, knockout rejection texts, per-mode thresholds.

**Art2D**
- `SS2DHudCanvas.cs` — two-segment mode toggle (selected segment accent), hints, rejection texts, per-mode thresholds. `SS2DSpriteLibrary.cs` — `knockoutTarget`, `knockedOut`, rotation-arrow slots (`rotationArrowArt`, `rotationArrowSize` 0.7, `rotationArrowSideOffset` 0.30, `rotationArrowSideHeight` 0, tint), `fullCameraCone`, `cameraConeAboveObstacles`, `cameraConePropsOrder`.
- `ShadowSteps2DRenderer.cs` — `SetGuardDefeated`, `IsGuardDefeated`, `SetKnockoutTargets`, `SetGuardHighlight`, `IsScreenClockwise`, two-arc rotation marker (user art, left/right of the base disc, mirrored for clockwise), full geometric camera cone on the Props layer (order 5000), no fan for knocked-out guards. `SS2DCharacter.cs` — `SetDefeated`. `CameraVisionOverlayRenderer.cs` — mesh layer/order override.
- `SS2DSessionBridge.cs` — knockout choreography, defeated guards, target discs, `CancelPresentation` on retry/mode switch, `TweenRunning`, guard/arc highlight for cards. `SS2DPreviewController.cs` — `previewMode`.
- `Scripts/Editor/SS2DFinalArtImporter.cs` — imports `Sprites/Markers/rotation_arrow.png` (user art, PPU 2048, centre pivot). `Scripts/Editor/SS2DPrototypeSetup.cs` — `RenderKnockoutReview`, `ImportRotationArrowAndRender`, mode-aware `RenderReview`.
- `Art2D/SS2DSpriteLibrary.asset` — rotation arrow assigned and tuned.

**Tools** — `Tools/CoreTestRunner/V3Tools.cs`, `Runner.cs`: `--v3-modes`, `--v3-knockout-pars`, `--v3-replay-ko`.

**Tests** — new: `Tests/EditMode/V3/V3KnockoutTests.cs` (25), `Tests/EditMode/SaveModeTests.cs` (5), `Tests/EditMode/GuardOnboardingTests.cs` (4), `V3SightTests.CameraConeGeometry…` (1), `Art2D/Tests/EditMode/SS2DKnockoutPresentationTests.cs` (3), `Art2D/Tests/EditMode/SS2DFullCameraConeTests.cs` (5), `Art2D/Tests/PlayMode/SS2DModesTests.cs` (4), `Art2D/Tests/PlayMode/SS2DGuardIntroTests.cs` (3). Updated: `SS2DCameraVisionTests` (cone contract), `SaveRepositoryTests` (version), gameplay-driving PlayMode suites start with guard cards acknowledged.

## 2. What was actually run

| Suite | Result | Notes |
|---|---|---|
| Core + Data + EditMode via dotnet (`Tools/run_core_tests.sh`) | 321 passed, 1 failed | The failure is the pre-existing stale LEVEL-18/24 parity entry (#47). |
| Unity EditMode (clone, batch) | 358 passed, 1 failed | 359 tests; the failure is the stale parity entry (#47). |
| Unity PlayMode, all suites (clone, batch, headless, final run after D-043) | 25 passed, 5 failed | All 7 modes/guard-card tests pass; the 5 failures are in the older placeholder-scene suites (`GameScenePlayModeTests`, `GameFlowPlayModeTests`): their `Open()` never dismisses the onboarding modal so the first `TrySubmit` is refused, and the LEVEL-17 lockdown expectation predates the 70° cone (#55). A baseline run of that suite at commit a8a2393 hung twice in batch mode, so "pre-existing" rests on inspection, not a recorded run. |
| Unity PlayMode `SS2DGuardIntroTests` with graphics | 3 passed | Produces the guard-card Game-view capture. |
| `--v3-modes` audit | problems = 0 | `Docs/Reports/MODES_AUDIT.json`. |

### 2.1 Unity EditMode
Final run: 358 passed / 1 failed of 359 (only #47). Two earlier fixture errors in the new cone tests were corrected on the way (edge-camera fixture with its far corners still inside the grid; a float-exact bounds check on a flat mesh).

## 3. Unity validation by level and mode

From `--v3-modes` on the shipped pack (both modes solved on every stage; Stealth solution replayed under Knockout rules with identical state and no knockout; the handoff's offline paths replayed where its snapshot layout is identical to the Unity level):

| Level | Stealth par | Knockout par | Knockouts on par path | Snapshot |
|---|---|---|---|---|
| FTUE-1 / 2 / 4 / 5 | 3 / 6 / 5 / 6 | same | 0 | replays |
| FTUE-3 | 8 | 8 | 1 (optional) | replays |
| LEVEL-1, 2, 5, 11–13, 15–17, 20, 21, 23–25, 27, 28 | unchanged | = Stealth | 0 | replays (18, 19, 23, 25: layouts differ from the snapshot, not replayed) |
| LEVEL-3 | 11 | 8 | 1 | knockout replays; snapshot Stealth path exceeds `waitLimit` |
| LEVEL-4 | 14 | 10 | 1 | replays |
| LEVEL-6 | 13 | 10 | 2 | knockout replays; snapshot Stealth path exceeds `waitLimit` |
| LEVEL-7 | 14 | 10 | 1 | replays |
| LEVEL-8 / 9 | 10 / 10 | 9 / 9 | 1 | replays |
| LEVEL-10 | 13 | 8 | 1 | knockout replays; snapshot Stealth path exceeds `waitLimit` |
| LEVEL-14 | 12 | 8 | 1 | replays |
| LEVEL-18 | 9 (stored, stale #47; solver 8) | 8 | 0 | layout differs |
| LEVEL-22 | 10 | 9 | 1 | replays |
| LEVEL-26 | 20 | 18 | 2 | replays (the handoff's Level 26 fixture) |
| LEVEL-29 / 30 | 24 / 28 | 24 / 28 | 1 / 2 (optional) | replays |

Every shipped level is solvable in both modes on the actual Unity resolver (`V3KnockoutTests.ShippedLevels_SolvableInBothModes…`, Unity EditMode and dotnet). No level was unsolved; no search limit was hit.

Source-data / timing mismatches found: (a) the snapshot's Stealth paths for LEVEL-3/6/10 use more WAITs than the shipped `waitLimit` of 3 allows (the Unity solutions use 1, 2 and 0); (b) LEVEL-18/19/23/25 snapshot layouts differ from the redesigned Unity levels (#51); (c) LEVEL-18's stored Stealth par is the stale entry already tracked in #47; (d) the Main 23 EMP-expiry report was re-checked by `V3KnockoutTests.EmpExpiryOnTheKnockout…` and the earlier regression test: no ordering defect, the trigger action does not consume the fresh budget and the last-action exit wins.

## 4. Game-view evidence (`Docs/Reports/Renders/`)
- `Art2D_Knockout_LEVEL-1_target_1080x1920.png` — Knockout selected on the toggle, target disc under the guard the spy can take from its side.
- `Art2D_Knockout_LEVEL-1_after_KO_1080x1920.png` — completed knockout: spy on the guard's node, guard down, no fan, 3 actions.
- `Art2D_Knockout_LEVEL-26_after_KO_1080x1920.png` — the handoff's Level 26 after its first knockout (second guard still patrolling).
- `Art2D_Stealth_LEVEL-1_1080x1920.png` — Stealth selected, no target controls.
- `Art2D_GuardCard_FTUE-3_1080x1920.png` — stationary-guard card on FTUE-3 (board camera capture with the screen canvases).
- `Art2D_RotationArrow_LEVEL-2_1080x1920.png` / `…_t1…` — clockwise rotating guard with the two arcs beside its base disc.
- `Art2D_Debug_LEVEL-19_t1.png` (cone crossing the stone wall) and `Art2D_Debug_LEVEL-18_t0.png` (cone beyond the board edge); `…LEVEL-24…` sweeping states.

## 5. Not verified / needs the user
- Device run (Android/iOS) and touch feel of the toggle and the "Got it" card: not possible here.
- The placeholder-scene PlayMode suite's five failures (#55) are left for a separate fix of its `Open()` helper.
- Rotation-arc size and the cone's painted area over safe nodes are design review items (#53, #56).
