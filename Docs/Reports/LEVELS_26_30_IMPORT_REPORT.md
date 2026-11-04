# Levels 26–30 import report (2026-11-03/04)

Source: `ShadowSteps_Levels_26_30_Unity_Import_Kit.zip` (repo root), extracted to a scratch folder; instructions in
`CLAUDE_CODE_UNITY_IMPORT.txt`. Nothing from the kit was executed inside the project; the kit's Node verifier was run from
the scratch folder only as the offline baseline.

## 1. Offline baseline
`node verify_reference.cjs` (Node 24): all five levels replay and shortest-path checks pass, Ghost passes on 28/29/30.

## 2. Conversion
The kit uses the earlier browser-reference schema, which the project's `BrowserLevelImporter` already reads
(`CoreTestRunner --v3-import`). The import recomputes scoring with the C# solver, checks `blockedNodes` against the rows
(redundant list, must match exactly), keeps every coordinate, row, door edge, camera `mountDir`/`dir`, schedule, offset,
charge and budget, and writes schema-3 records. Mapping: reference ids 31–35 → project ids **31–35**, codes
**LEVEL-26 … LEVEL-30**, labels "Level 26 … 30", group Main. Existing records 1–30 untouched (no id/code conflicts).
The merge is keyed by code and idempotent (rerunning replaces the same five records). Each record carries a `waitLimit`
per the project's rule (max(3, needed waits + 2), covering the supplied win and Ghost paths) and provenance notes.
`validation_v4.json` gained the five entries; entries for levels whose scoring is unchanged were refreshed with the
current solver's recorded path (LEVEL-3/6/10's old recorded paths used 4–5 consecutive WAITs, now beyond their limits);
LEVEL-18 and LEVEL-24 entries are deliberately left stale (pending cone decision, KNOWN_ISSUES #47).

## 3. Replays against the actual Unity rules engine (`--v3-replay`, shipped pack, final rules)

| Level | Size | Reference optimal | C# par | Ghost (reference → C#) | Smoke | EMP | Shortest without WAIT | Supplied win path | Wait limit |
|---|---|---|---|---|---|---|---|---|---|
| LEVEL-26 Courtyard Relay | 7×9 | 20 | 20 ✓ | n/a (no camera) | — | — | impossible | won, 20 actions | 4 |
| LEVEL-27 Borrowed Cover | 7×9 | 20 | 20 ✓ | n/a (no camera) | Smoke | — | 20 | won, 20 actions | 4 |
| LEVEL-28 Divided Watch | 9×9 | 21 | 21 ✓ | 21 ✓ (replayed, no alarm) | — | optional (noEMP 22) | 22 | won, 21 actions | 5 |
| LEVEL-29 Two Locks | 7×11 | 24 | 24 ✓ | 24 ✓ (replayed, no alarm) | — | optional (noEMP 24) | 24 | won, 24 actions | 6 |
| LEVEL-30 Inner Citadel | 9×11 | 28 | 28 ✓ | 28 ✓ (replayed, no alarm) | Smoke | optional (noEMP 29) | 32 | won, 28 actions | 4 |

All five supplied winning paths and all three supplied Ghost paths replay to **victory with exactly the stated counted
actions**; the Ghost replays end with `alarmEver = false`. Gadget-removal checks agree with the kit (Smoke mandatory on 27
and 30; EMP optional everywhere — the kit's `noEMP: win` on 28/29/30). Key reuse (29: two locks, 30: two doors) and
Smoke expiry (27, 30) are exercised by the replays; camera phases (28 sweep, 30 sweep + rotating) and door LOS by the
cone comparison below.

## 4. Detection parity with the reference engine
`--v3-cones` (C#) vs a dump of the kit's `camVision` / `guardVision` for every camera and guard at t = 0..3 of all five
levels: **52 entries compared, 0 differences** (origins at the wall face; the reference's 0.001 outward epsilon makes no
difference on these boards). The renderer draws exactly `Sight.CameraVision` / `GuardVision` (EditMode tests), so
in-game highlights equal the reference traces.

## 5. Rule mismatch found and resolved — doors (D-038)
Before this import the C# engine opened a locked edge as soon as a key-holder **arrived at either endpoint** (one step
before crossing), so the door stopped blocking sight early. The kit's rules ("the key opens each crossed locked edge;
closed doors block movement and sight") and its reference engine open the door only on the crossing. On Level 30 the
early opening let the sweeping camera at wall (0,7) see the player on (1,8) through the open door at t = 14, so the
supplied path locked down (C# par 29, Ghost 36). Measured with a rule switch: the 30 shipped levels have identical par,
Ghost and gadget results under both rules. The crossing-only rule is now the default (`StageRules.DoorsOpenOnApproach`
= false, kept as a measurement switch); four door tests were rewritten. After the switch Level 30 matches the reference
exactly (par 28, Ghost 28). No layout, schedule, charge or budget in the kit was altered.

## 6. EMP-expiry finding (Main 23)
The approved ordering is implemented correctly: on the action where the EMP expires the camera rechecks, an expiry-
triggered detection starts the lockdown with the **full** budget (the triggering action does not decrement it), and
reaching the exit on the last permitted action wins. New regression test
`EmpExpiry_TriggersTheAlarmWithTheFullBudget_AndTheExitOnTheLastPermittedActionWins` proves both halves on a corridor.
The failure the user saw was not an ordering defect: under the 70° cone LEVEL-23's camera lights (3,2) and (2,2) and the
level shipped with `alarmBudget` 1, so after the EMP expired on (3,2) the one permitted action could never reach the exit
two nodes away — the level was unsolvable (reported in `CAMERA_CONE_VALIDATION_2026-11-03.md`). The user has since set
LEVEL-23's budget to 2; the shipped solution `E EMP#0 E E E E` now wins on both the C# engine and the kit's reference
engine with identical alarm values at every step (trace in this session). No EMP duration or budget was changed by me.

## 7. Progression, saves, presentation
- Catalog order is by index; `NextAfter(30)` = LEVEL-26 … `NextAfter(35)` = null, so Main 25 → 26 and the flow after
  Main 30 returns to level select as before. Unlocking stays sequential (`StageProgression.IsUnlocked` requires the
  previous stage completed); a save that finished the old campaign unlocks Main 26 only. Nothing is pre-marked. Saves key
  results by stage id; ids 1–30 unchanged. Core tests `V3CampaignContinuityTests` cover this; PlayMode
  `Campaign_Main25ProceedsToMain26_AndTheNewLevelsCompose` walks the real Game scene 25 → 26 → … → 30.
- Art: `SS2DArt_LEVEL-26 … 30` layouts from `art_layout_proposals.json` (bush / stone-wall per X cell; cameras on stone
  wall brackets); interior `#` runs draw as stone walls automatically. Boards 7×9 and 9×11 are taller than wide, so no
  portrait rotation; the courtyard framing fits the 9×11 board with HUD and controls clear (see renders).
- Exit/door transition: the walk-to-door sequence is currently off by the user's choice (`showExitDoor` /
  `playExitDoorSequence`); completion is at the logical exit either way.

## 8. Verification status
- Core (`Tools/run_core_tests.sh`): **289 passed, 1 failed** — the failure is the pre-existing stale LEVEL-18/24
  validation entries (pending decision), not the import.
- Unity (headless clone): compile 0 errors; Art2D EditMode **25/25**; PlayMode **10/10** (incl. the new campaign test).
- Renders (actual Unity Game-view captures, 1080×1920): `Docs/Reports/Renders/Art2D_Level26_GameView_1080x1920.png`
  (Main 26 initial state), `Art2D_LEVEL30_GameView_1080x1920.png` (Main 30 fit check), plus 27–29.
- Blocked by the local environment: nothing was run in the GUI editor, no device build/test.

## 9. Changed files
Core: `Scripts/Core/V3/StageRules.cs` (door rule switch, default crossing-only), tests `Tests/EditMode/V3/V3RulesTests.cs`,
`V3GadgetTests.cs` (EMP-expiry regression), `V3PackParityTests.cs` (35 levels, 63 cover nodes, new sets),
`V3CampaignContinuityTests.cs` (new). Tools: `Tools/CoreTestRunner/Runner.cs`, `V3Tools.cs` (`--v3-replay`, `--v3-cones`,
door switch env). Data: `Assets/StreamingAssets/ShadowSteps/levels_v4.json` (+5 records), `validation_v4.json` (+5 entries,
refreshed recorded paths). Art2D: `SS2DFinalArtImporter.cs` (generic layouts + 26–30), `SS2DPrototypeSetup.cs`
(`RenderLevels26To30`), `LevelArt/SS2DArt_LEVEL-26..30.asset`, `Levels/SS2D_LEVEL_26.unity`, PlayMode campaign test.
Docs: this report, D-038, KNOWN_ISSUES #47, PROGRESS.
