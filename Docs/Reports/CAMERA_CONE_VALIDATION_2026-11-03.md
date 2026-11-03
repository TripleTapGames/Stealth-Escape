# Camera cone vision — V4 pack validation report (2026-11-03)

Rule under test: 70° cone (35° either side), origin on the camera's logical wall face, forward reach 2 node spacings,
line of sight to the node centre blocked by walls, solid cover, closed doors and smoke with strict corner contact
(`Sight.CameraVision`, D-036). Previous rule: 3-wide × 2-deep rectangle from the lens node (D-035).
Solver: `CoreTestRunner --v3-export-validation` (deterministic BFS); output kept at `/tmp/validation_cone.json`, **not**
written into `Assets/StreamingAssets` (no level data, camera, budget or scoring was changed — awaiting approval).

Stage validator (`--v3-validate`): all 30 stages parse; the only errors/warnings are the pre-existing "consecutive WAITs"
warnings on LEVEL-3/6/10 and **LEVEL-23: no winning route with the supplied loadout**. Guard routes, door/key placement
and start-outside-camera checks pass for every stage. Guard rules are untouched (guard tests unchanged, 271 passing).

## Per-level results

Previous = shipped `validation_v4.json` (rectangle rule). New = cone rule. "Gadget req. valid" = the mandatory Smoke/EMP
set is the same as before (the level still teaches what its brief says).

| Level | Cameras (wall cell, mount face, facing, kind) | Prev par | New par | Solvable | Ghost (cone) | Mandatory gadgets (cone) | Gadget req. valid | Problems / changes |
|---|---|---|---|---|---|---|---|---|
| FTUE-1 | — | 3 | 3 | yes | n/a | none | yes | — |
| FTUE-2 | — | 6 | 6 | yes | n/a | none | yes | — |
| FTUE-3 | — | 8 | 8 | yes | n/a | none | yes | — |
| FTUE-4 | — | 5 | 5 | yes | n/a | none | yes | — |
| FTUE-5 | — | 6 | 6 | yes | n/a | none | yes | — |
| LEVEL-1 | — | 9 | 9 | yes | n/a | none | yes | — |
| LEVEL-2 | — | 7 | 7 | yes | n/a | none | yes | — |
| LEVEL-3 | — | 11 | 11 | yes | n/a | none | yes | — |
| LEVEL-4 | — | 14 | 14 | yes | n/a | none | yes | — |
| LEVEL-5 | — | 8 | 8 | yes | n/a | none | yes | — |
| LEVEL-6 | — | 13 | 13 | yes | n/a | none | yes | — |
| LEVEL-7 | — | 14 | 14 | yes | n/a | none | yes | — |
| LEVEL-8 | — | 10 | 10 | yes | n/a | none | yes | — |
| LEVEL-9 | — | 10 | 10 | yes | n/a | none | yes | — |
| LEVEL-10 | — | 13 | 13 | yes | n/a | none | yes | — |
| LEVEL-11 | — | 12 | 12 | yes | n/a | none | yes | — |
| LEVEL-12 | — | 14 | 14 | yes | n/a | none | yes | — |
| LEVEL-13 | — | 10 | 10 | yes | n/a | none | yes | — |
| LEVEL-14 | — | 12 | 12 | yes | n/a | none | yes | — |
| LEVEL-15 | — | 8 | 8 | yes | n/a | none | yes | — |
| LEVEL-16 | cam1@(3,0) face S facing S fixed | 8 | 8 | yes | no | none | yes | — |
| LEVEL-17 | cam1@(6,2) face W facing W fixed | 8 | 8 | yes | no | none | yes | — |
| LEVEL-18 | cam1@(1,0) face S facing S rotating ccw | 9 | 8 | yes | yes (alarm-free 8) | none | yes | par 9 → 8; stored scoring stale |
| LEVEL-19 | cam1@(3,0) face S facing S sweeping ccw | 7 | 7 | yes | yes (alarm-free 7) | none | yes | — |
| LEVEL-20 | cam1@(6,3) face W facing W fixed | 6 | 6 | yes | no | none | yes | — |
| LEVEL-21 | — | 6 | 6 | yes | n/a | Smoke | yes | — |
| LEVEL-22 | — | 10 | 10 | yes | n/a | none | yes | — |
| LEVEL-23 | cam1@(4,1) face S facing W fixed | 5 | — | **NO** | no | none | no | unsolvable; forced alarm True → False; mandatory gadgets EMP → none |
| LEVEL-24 | cam1@(6,2) face W facing W sweeping ccw | 8 | 8 | yes | yes (alarm-free 10) | none | no | Ghost False → True; forced alarm True → False; mandatory gadgets EMP → none |
| LEVEL-25 | cam1@(4,1) face S facing W fixed | 6 | 6 | yes | yes (alarm-free 6) | Smoke, EMP | yes | — |

Stages without cameras (5 FTUE + 17 main) are byte-identical in par, solution path, Ghost and gadget flags.

## Findings

1. **LEVEL-23 "EMP Training" is unsolvable.** Cone from (4, 1.5) facing W lights (3,2) and (2,2) only (the old rule lit
   (4,2) and (3,2)). With alarm budget 1 the player is seen at (2,2) three moves from the exit; EMP (2 actions) fired at
   (1,2) expires on (3,2), still two moves short. No loadout/route wins.
2. **LEVEL-24 "Security Gap" no longer needs EMP and now offers Ghost.** The cone no longer lights the row-1 detour, so an
   alarm-free 10-action route exists (EMP route 8). The level's brief ("EMP expires after two counted actions, then the
   camera rechecks vision") is no longer enforced. Easier than intended.
3. **LEVEL-18 "Rotating Security" par 9 → 8** (Ghost route unchanged at 8 actions, still Ghost-eligible). Slightly easier;
   stored scoring (par 9, 3★ ≤ 10, 2★ ≤ 12) is stale.
4. LEVEL-16/17/19/20/25: same par, Ghost and gadget requirements as shipped.
5. Mandatory-gadget sets under the cone with the shipped data: Smoke {21, 25} (unchanged), EMP {25} (was {23, 24, 25}),
   forced alarm {16, 17, 20} (was {16, 17, 20, 23, 24}), Ghost {18, 19, 24} (was {18, 19, 25} — 25 still Ghost, 24 new).

## Proposed corrections (NOT applied — for approval)

| Level | Proposal | Measured result under the cone |
|---|---|---|
| LEVEL-23 | Raise `alarmBudget` 1 → **2**; layout, camera and EMP loadout unchanged | par 5 (`E EMP#0 E E E E`), EMP mandatory, forced alarm, no Ghost — same teaching intent as before |
| LEVEL-24 | Option A (minimal): camera kind sweeping → **fixed**, everything else unchanged | par 8, EMP mandatory, forced alarm, Ghost not offered (exit approach always lit) |
| LEVEL-24 | Option B: move the camera to wall (6,1) (top-right), keep sweeping ccw | par 8, EMP mandatory, Ghost offered (alarm-free 9) — keeps the sweep lesson and a Ghost route |
| LEVEL-18 | Accept the re-scored par 8 (3★ ≤ 9, 2★ ≤ 11); no layout change | par 8, Ghost offered (alarm-free 8) |
| All camera stages | After approval: re-export `validation_v4.json` and the `scoring` blocks of `levels_v4.json`; update `V3PackParityTests` expected sets | — |

Rejected candidates (measured): LEVEL-23 budget 3 (EMP no longer mandatory); LEVEL-23 camera at (3,1) budget 2
(unsolvable); LEVEL-24 camera at (3,0) sweeping (par 11, needs three leading WAITs); LEVEL-24 fixed camera at (6,1) or an
extra crate at (2,1) (unsolvable).

## Test status with the shipped (unchanged) data

Core (`Tools/run_core_tests.sh`): **271 passed, 3 failed** — the three failures are the pack-parity tests that compare the
solver against the shipped data and will pass once the approved corrections are exported:
`ShippedValidation_ReplaysToWins_AndMatchesTheSolver` (LEVEL-18/24 drift, LEVEL-23 replay cannot win),
`ShippedPack_Has30Stages_WithSolverScoring_AndNoWarnings` (LEVEL-23 unsolvable; stale stored par/Ghost on 18/24),
`MandatoryGadgetLevels_AreProvenByConstrainedSolves` (EMP set now {25}, forced set {16,17,20}).
Art2D EditMode 17/17, PlayMode 8/8.
