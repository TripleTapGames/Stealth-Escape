# Levels 38–50 import report (2026-11-11)

Source: `Handoff/ShadowSteps_Levels_38_to_50_Design_Handoff.zip`, extracted to `Handoff/Levels_38_50_extracted/` (read only; nothing
from the kit was executed inside the project). The kit's own Python solver results (`LOCAL_VALIDATION.json`, `EXAMPLE_SOLUTIONS.json`)
were treated as provisional and every claim below was re-derived with the project's C# validator and solver
(`Tools/CoreTestRunner`, shipped rules: Knockout, no WAIT, 2-square guard sight, 70° camera cone reaching 3 squares from the wall face,
two-move Smoke/EMP, lockdown budget, doors opened by crossing).

## 1. Import status

| Kit level | Project id | Code | Title | Board | Nodes | Guards | Cameras | Key | Smoke / EMP | Alarm budget | Status |
|---:|---:|---|---|---|---:|---:|---:|---|---|---:|---|
| 38 | 38 | LEVEL-33 | Crossed Blades | 11×9 | 20 | 2 | 0 | — | 0 / 0 | 4 | imported, valid |
| 39 | 39 | LEVEL-34 | Eyes on the Junction | 13×9 | 23 | 2 | 1 | — | 0 / 0 | 3 | imported, valid |
| 40 | 40 | LEVEL-35 | Sweep and Strike | 13×9 | 23 | 2 | 1 | — | 0 / 0 | 3 | imported, valid |
| 41 | 41 | LEVEL-36 | Key Under Watch | 13×11 | 25 | 2 | 1 | (3,9) | 0 / 0 | 4 | imported, valid |
| 42 | 42 | LEVEL-37 | Three-Way Watch | 13×11 | 25 | 3 | 1 | — | 0 / 0 | 4 | imported, valid |
| 43 | 43 | LEVEL-38 | Smoke at the Fork | 13×11 | 25 | 3 | 1 | — | 1 / 0 | 3 | imported, valid |
| 44 | 44 | LEVEL-39 | Overlapping Lenses | 13×11 | 25 | 3 | 2 | — | 0 / 0 | 3 | imported, valid |
| 45 | 45 | LEVEL-40 | Gatehouse Circuit | 15×11 | 25 | 3 | 2 | (3,9) | 0 / 0 | 3 | imported, valid |
| 46 | 46 | LEVEL-41 | Power Cut | 15×11 | 25 | 3 | 2 | — | 0 / 1 | 1 | imported, valid |
| 47 | 47 | LEVEL-42 | Broken Line of Sight | 15×11 | 25 | 3 | 2 | (5,9) | 1 / 1 | 2 | imported, valid |
| 48 | 48 | LEVEL-43 | The Four Sentinels | 15×13 | 25 | 4 | 2 | — | 1 / 0 | 3 | imported, valid |
| 49 | 49 | LEVEL-44 | Three Eyes, One Key | 15×13 | 25 | 4 | 3 | (9,11) | 0 / 1 | 3 | imported, valid |
| 50 | 50 | LEVEL-45 | The Last Exit | 15×13 | 25 | 4 | 3 | (3,11) | 1 / 1 | 1 | imported, valid |

- **Numbering.** The kit numbers levels by pack id. The campaign's player-facing codes run FTUE-1…5 then LEVEL-1…32 (ids 6–37), so the
  thirteen new ids 38–50 are **LEVEL-33 … LEVEL-45** (the same rule D-078 applied to the kit's "LEVEL-36/37", shipped as LEVEL-31/32).
  Each record's first note keeps the kit name and number. The kit's `base: "LEVEL-30"` field is an inheritance hint only and was dropped;
  **LEVEL-30 (id 35) and every other existing record are byte-for-byte unchanged** (git diff of the pack: 13 appended records only, plus
  the user's own earlier uncommitted LEVEL-31/32 edit, see §4).
- **Geometry.** Rows, start, exit, key, doors, guards (kind, route, facing, rotation sense, offset), cameras (wall cell, mount face,
  facing, kind, rotation sense), charges and alarm budgets are exactly the kit's. The kit's schema is the pack schema, so no conversion
  was needed; the pack was written through the C# serializer (`--v3-append`, new runner command) and scoring comes from the C# solver
  (`--v3-rescore`). `waitLimit` is 0 on all thirteen (no WAIT). The ten levels without a knockout-free route carry a
  "Knockout-only design (D-081)" note, which is how the legacy Stealth check knows they demand a knockout by design.
- **Validator.** `--v3-validate`: all 50 stages valid. Warnings only: the portrait-width advisory on every 11–15 column board (handled
  by horizontal panning, §5) and "straight ping-pong route does not reach its terminal node" on the 3-node patrols (informational: the
  engine walks the authored route; it does not extend it).

## 2. Validation results per level (C# solver, Knockout, no WAIT)

| Code (kit) | C# par | Kit local | Knockouts in par | Knockout-free shortest | Ghost route (alarm-free shortest) | Forced alarm | Smoke | EMP | Key mandatory | Solver route |
|---|---:|---:|---:|---|---|---|---|---|---|---|
| LEVEL-33 (38) | 6 | 6 ✓ | 1 | 8 (kit 8 ✓) | no camera | — | — | — | n/a | S N E KO#0 E E |
| LEVEL-34 (39) | 9 | 9 ✓ | 1 | none (kit none ✓) | 11, Ghost offered (kit 11 ✓) | no | — | — | n/a | N E E E KO#0 E N E S |
| LEVEL-35 (40) | 7 | 7 ✓ | 1 | 9 (kit 9 ✓) | 7, Ghost offered (kit 7 ✓) | no | — | — | n/a | N E KO#0 E E N E |
| LEVEL-36 (41) | 11 | 11 ✓ | 2 | none ✓ | 11, Ghost offered ✓ | no | — | — | **yes** (unsolvable without) | E S S N N KO#0 E KO#1 E S E |
| LEVEL-37 (42) | 11 | 11 ✓ | 2 | 15 (kit 15 ✓) | 11, Ghost offered ✓ | no | — | — | n/a | N S E S E N KO#0 E KO#1 N E |
| LEVEL-38 (43) | 11 | 11 ✓ | 2 | none ✓ | none, no Ghost ✓ | **yes** | **mandatory** (no Smoke: unsolvable) ✓ | — | n/a | E E KO#0 SMOKE@7,5 E S E E KO#2 W N E |
| LEVEL-39 (44) | 11 | 11 ✓ | 2 | none ✓ | none, no Ghost ✓ | **yes** | — | — | n/a | N S E E KO#0 N KO#1 S E N E |
| LEVEL-40 (45) | 12 | 12 ✓ | 2 | none ✓ | none, no Ghost ✓ | **yes** | — | — | **yes** | E S S N N KO#0 E E KO#1 E S E |
| LEVEL-41 (46) | 10 | 10 ✓ | 2 | none ✓ | 10, Ghost offered ✓ | no | — | **mandatory** (no EMP: unsolvable) ✓ | n/a | N E N E KO#0 EMP#0 E E KO#1 E E |
| LEVEL-42 (47) | 12 | 12 ✓ | 2 | none ✓ | 12, Ghost offered ✓ | no | optional (12 without) | optional (12 without) | **yes** | N S E E S N KO#0 E KO#1 N E E |
| LEVEL-43 (48) | 10 | 10 ✓ | 2 | none ✓ | 10, Ghost offered ✓ | no | optional (10 without) | — | n/a | N S E E KO#0 E KO#1 N E E |
| LEVEL-44 (49) | 14 | 14 ✓ | 3 | none ✓ | 14, Ghost offered ✓ | no | — | optional (14 without) | **yes** | KO#2 E E KO#0 S E E S S N N KO#1 E E |
| LEVEL-45 (50) | 16 | 16 ✓ | 1 | none ✓ | 16, Ghost offered ✓ | no | **mandatory** ✓ | **mandatory** ✓ | **yes** | E W S E S S N N W N E SMOKE@5,5 E E EMP#0 KO#1 E E |

Every kit claim the handoff asked to prove holds on the C# engine: Smoke mandatory on kit 43 and 50; EMP mandatory on kit 46 and 50;
keys mandatory on kit 41/45/47/49/50 (with the door edge cut and no key the exit is unreachable); the knockout route is shorter than the
knockout-free one on kit 38 (6 vs 8), 40 (7 vs 9) and 42 (11 vs 15); kit 39 offers a longer clean route (Ghost 11 vs par 9). The kit's
"alarm triggered in example" flags on 40/46/49 are properties of its sample routes only: the C# solver finds alarm-free routes at par
there (Ghost offered). Isolated sentries: kit 43 g2 at (7,3) and kit 50 g1 at (5,3) have no connection into their node (checked on all
four sides) and cannot be knocked out, as designed; no immunity flag is needed.

Kit example routes replayed on the engine (`--v3-replay-ko`): **12 of 13 win with the kit's action counts.** The kit-38 route
("N E S KO E E") is caught before its first move: the ping-pong guard standing on its first route node (3,3) faces W at t = 0 (toward
(1,3)), the same endpoint-facing rule reported for the kit's LEVEL-37 (D-078, KNOWN_ISSUES #65), whereas the kit prototype faces the
authored "E". Par is unaffected (the solver's 6-action route "S N E KO E E" wins).

## 3. Optimal actions and star thresholds (written into `scoring`, policy `StageScoring`)

| Code | Par (3★ ≤) | 2★ ≤ | Ghost offered |
|---|---:|---:|---|
| LEVEL-33 | 6 (7) | 8 | no (no camera) |
| LEVEL-34 | 9 (10) | 12 | yes |
| LEVEL-35 | 7 (8) | 10 | yes |
| LEVEL-36 | 11 (12) | 15 | yes |
| LEVEL-37 | 11 (12) | 15 | yes |
| LEVEL-38 | 11 (12) | 15 | no (forced alarm) |
| LEVEL-39 | 11 (12) | 15 | no (forced alarm) |
| LEVEL-40 | 12 (13) | 16 | no (forced alarm) |
| LEVEL-41 | 10 (11) | 13 | yes |
| LEVEL-42 | 12 (13) | 16 | yes |
| LEVEL-43 | 10 (11) | 13 | yes |
| LEVEL-44 | 14 (15) | 19 | yes |
| LEVEL-45 | 16 (17) | 21 | yes |

`validation_v4.json` was re-exported for the whole pack (50 entries, solver routes above).

## 4. Problems, shortcuts and required decisions (nothing redesigned)

1. **Threat audit: par-neutral guards and cameras.** For every level each guard and each camera was removed in turn and the level
   re-solved. A threat is "par-neutral" when its removal changes neither par, knockouts on the par route, knockout-free status, Ghost
   offer, forced alarm, Smoke/EMP necessity, nor the solver's own path. The solver's path is one shortest route, so a par-neutral threat
   may still close other routes of the same length; this is a design prompt, not a defect. Findings:
   - LEVEL-35 (kit 40): patrol **g2** (3,3)–(7,3), offset 2.
   - LEVEL-39 (kit 44): sweeping **cam2** at (9,0). (cam1 and all three guards matter.)
   - LEVEL-40 (kit 45): **cam1** (14,3), **cam2** (9,0) and patrol **g3**: with all three gone the solve is identical (12, two knockouts).
   - LEVEL-41 (kit 46): sweeping **cam2** (11,0) and patrol **g3** (cam1 is the EMP gate and is essential).
   - LEVEL-42 (kit 47): both cameras **cam1** (14,5) and **cam2** (9,0); the key detour and g2 carry the level.
   - LEVEL-43 (kit 48): both cameras and the southern sentry **g4** at (9,9).
   - LEVEL-44 (kit 49): all three cameras and sentry **g4** at (3,9); the three-knockout key run is set entirely by guards g1–g3.
   - LEVEL-45 (kit 50): sweeping **cam2** (5,0), patrol **g3** and sentry **g4** (11,7); cam1 (the EMP bridge), cam3, g1 (the smoked bridge) and g2 are essential.
   Proposed correction, for the designer's approval: either accept them as pressure on alternative routes (the kit's "three cameras divide the
   level" intent) or move/re-aim them so the solver's path has to react. Not changed.
2. **Threats that shorten the level when removed** (meaningful, for the record): kit 38 g1 (6→4), kit 39 g1 (9→7), kit 42 g1 (11→7) and g2
   (11→9), kit 43 g2 and g3 (11→7), kit 44 g2 and g3 (11→9), kit 47 g2 (12→10), kit 48 g2 (10→8); every other guard changes the route or
   the knockout count.
3. **Kit-38 example route** caught by the engine's endpoint-facing rule (see §2); kit author to align the prototype, nothing to change here.
4. **LEVEL-31 / LEVEL-32 (ids 36/37) have stale scoring from the user's own uncommitted stage-editor edit** (not part of this import,
   not touched): LEVEL-31 now solves in 10 (stored 8) and LEVEL-32 in **4** ("E E KO#0 N", stored 8; the edit moved g2 to (2,2)–(6,2) and
   made g1 rotating). Three dotnet tests fail on exactly this (`StagePackToolsTests.RecomputedScoring…`, `V3KnockoutTests.ShippedLevels…`,
   `V3PackParityTests.ShippedValidation…`) and `SS2DNewStagesTests` expects par 8 for both. Fix: open Shadow Steps ▸ Stage Editor, Reload,
   "Recompute scoring", Save (or `--v3-rescore`), and decide whether the 4-action LEVEL-32 is intended.
5. No unintended shortcut was found on the thirteen new levels: every par route uses the authored mechanics the level is named for
   (knockout flank, key detour, Smoke on the watched bridge, EMP on the camera bridge).

## 5. Visual integration

- **Horizontal panning (D-080).** Boards at least `SS2DSpriteLibrary.horizontalPanMinWidth` (11) columns wide are drawn landscape (not
  rotated to portrait), zoomed so their full height fits the band between the HUD bars (never tighter than the shared reference zoom), and
  the width beyond the view becomes a pan range. The view opens on the player, glides back onto the player after every counted action,
  and can be dragged with two fingers (right/middle mouse drag in the editor). Dragging only moves the camera: the session is never
  touched, no turn is consumed (`SS2DWideBoardPanTests`). Every board of the earlier campaign is at most 9 columns wide and is unaffected.
- **Backgrounds.** The four painted settings are campaign chapters by board tier: ids 1–40 courtyard v2 (the library's single background,
  unchanged), **41–44 Museum Sculpture Court**, **45–47 Desert Palace Terrace**, **48–50 the original courtyard** (`backgroundThemes` on
  the sprite library and the importer's theme table; id ranges are editable in the inspector). The painted floor rect encloses the whole
  board, so on 15-column boards the image is scaled up more than on earlier levels (uniform scale, never stretched; the extra enlargement
  is the cost of keeping the ground under the board while panning).
- **Props.** Per-level `SS2DArt_LEVEL-33…45` layouts: every authored X cell is a bush planter or a stone-wall piece (both appear on every
  level), perimeter cameras sit on stone mounts, the two interior-wall cameras (kit 46 cam1 at (7,4), kit 50 cam1 at (9,4)) on poles.
  Decoration never changes obstruction (the layouts are presentation only).
- **Captures** (Game view, 1080×1920, guard fans and camera cones drawn): see §6.

## 6. Verification record (2026-11-11)

- **dotnet core suite** (`Tools/run_core_tests.sh`, 354 tests): 351 passed, 3 failed; the three failures are the pre-existing LEVEL-31/32 stale
  scoring (§4.4, KNOWN_ISSUES #66). `V3Levels33To45Tests` 8/8. `--v3-validate`: all 50 stages valid. Unity compile check: 0 errors.
- **Unity EditMode, scratch clone, batch** (`tests_38_50_EditMode.xml`): 420 tests, 409 passed, 7 failed, 4 skipped. The failures are the three
  LEVEL-31/32 scoring tests above and four `SS2DCameraVisionTests` that still expect the pre-2026-11-10 LEVEL-18/19 boards (KNOWN_ISSUES #70);
  none touch D-080/D-081 code. Every new and updated Art2D test passes (fitter pan range, landscape wide boards, chapter themes, coverage at the pan extremes,
  level-select count).
- **Unity PlayMode, scratch clone, with graphics** (`tests_38_50_PlayMode.xml`): `SS2DNewStagesTests` LEVEL-38/41/44/45 — each opens in the Game scene,
  draws landscape with a pan range, captures the view, and replays the solver's par route through the session to a win (alarm popups dismissed as a
  player would; the hidden placeholder board runs in reduced motion); `SS2DWideBoardPanTests` 3/3 (opens on the player, drag pans without a turn or
  state change, clamps, follow resumes on the next move, LEVEL-30 unaffected); `SS2DPlayablePrototypeTests` 10/10 (campaign sequence Main 26 → 45,
  Game scene on the 2D layer). The two LEVEL-31/32 replays fail on the stale par (§4.4).
- **Captures** (`Docs/Reports/Renders/`, 1080×1920, guard fans and camera cones drawn): `Art2D_Level38_Kit43_SmokeAtTheFork`, `Art2D_Level41_Kit46_PowerCut`,
  `Art2D_Level44_Kit49_ThreeEyesOneKey`, `Art2D_Level45_Kit50_TheLastExit`. The view opens on the player, so the right-hand part of each board is
  reached by panning (two-finger drag on device, right/middle mouse drag in the editor).
- **Not verified here:** on-device touch panning (two fingers) and the look of the three chapter backdrops on a phone; the PNG import settings of the
  museum/desert/original-courtyard backdrops were aligned to the courtyard v2 by editing their `.meta` files (PPU 256, full-rect mesh) and Unity
  re-imports them on the next editor refresh.
