# V3 migration audit — handover of 2026-10-29

Scope: `reference/v3_handover_2026-10-29/SHADOW_STEPS_CLAUDE_CODE_HANDOVER.md` (authority), the browser prototype
`shadow_steps_30_level_playtest_v3.html` and `levels_v3_playtest_reference.json` (reference implementations),
`playtest_validation_report.json` (browser self-parity only). This audit was written before any code change (M0).

## 1. What exists today (V2 stack, commit fc8dd69)

| Area | Files | State |
|---|---|---|
| Pure rules core (no engine refs) | `Scripts/Core/*.cs` (16 files, ~1 900 lines) | Working, 206 dotnet tests green. Rules: 90° cone guards, move/rotate cadences, door *node*, smoke = player invisibility turn action, EMP = clock freeze, before-or-after cone detection, unlimited undo. |
| Data layer | `Scripts/Data/*` (schema 1 + 2 JSON, MiniJson, save, progression, authoring model) | Working. |
| Runtime presentation | `Scripts/Runtime/*` (board/edge/prop/character views, vision overlay, swipe/tap input, animation coordinator, camera fit, bootstrap, audio, haptics) | Compiles; never run in Unity (editor lock). |
| UI | `Scripts/UI/*` (HUD, Home/LevelSelect/Settings/Pause/Results/Failure/Onboarding, screen router, prefab generation) | Compiles; never run in Unity. |
| Editor tools | Level editor window, setup menu, build menu | Compiles. |
| Tooling | dotnet runner (tests, metrics, solver, reach map, generator, migration), compile check | Working. |
| Content | 20 + 5 generated V2 levels | Solvable under V2 rules; **obsolete under V3 rules**. Backed up to `reference/v2_shadowsteps_packs_backup/`. |

## 2. Rule-by-rule comparison (V2 code vs approved V3)

| Topic | V2 code | V3 approved | Verdict |
|---|---|---|---|
| Guard sight | 90° cone, range 1–4, 12-sample LoS | exactly 2 nodes straight ahead; nearest blocker stops sight | **replace** |
| Guard cadence | `moveEvery` / `rotateEvery` | 1 node or 90° per counted action, no slow variants | **replace** (schedule function rewritten) |
| Guard modes | fixed, rotate, pingPong, moveRotate, loop | static, rotating (CW/CCW), ping-pong (to true endpoints, `offset`), loop | adapt; `moveRotate` retired |
| Guard facing while moving | travel direction, legacy phase-0 quirk | travel direction derived from position(t)−position(t−1); endpoint turn-around rule from the prototype | **replace** |
| Occlusion geometry | node-based sampled LoS | segment vs wall rectangles with corner contact = blocked; closed door = perpendicular segment across the edge; same routine for guards, cameras, smoke throws | **replace** |
| Door | door *node*; locked incoming edges; opens on key possession; blocks sight until key held; win requires key | locked *connection* (edge); opens on physical crossing with key; blocks sight while closed; no generic key-for-win | **replace** |
| Key | single key, win gate | reusable key, only gates door edges | adapt |
| Smoke | turn action, player invisible for 2 actions | free targeted action; stationary opaque one-node cloud within 3×3, throw obstructed by walls/doors/corners; lasts 2 counted actions; expiry recheck same action | **replace** |
| EMP | turn action, clock freeze (no-op) | free targeted action; disables one camera for 2 counted actions; camera keeps rotating | **replace** |
| Cameras / alarm | none | wall-mounted (no node), fixed / rotating / 2-turn sweeping; 3×2 per-node footprint; alarm = permanent lockdown with per-level budget; Ghost achievement | **new** |
| Detection timing | before-or-after cone check | old-state collision+vision → camera → enemy update → new-state collision/swap/vision → camera → count → win → gadget expiry recheck → lockdown | **replace** (order ported from prototype `attempt()`) |
| Undo | unlimited, in HUD | debug only | adapt (hide in release builds) |
| Stars | par / par+2 | `T3 = P + max(1, min(3, ⌊P/10⌋))`, `T2 = P + max(2, ⌈0.3P⌉)` (provisional) | adapt |
| Solver | BFS, every action costs 1 | 0–1 BFS: gadgets cost 0, Move/Wait cost 1 | **replace** |
| Validator | V2 checks | §11 list incl. full-cycle guard overlap, start not in vision, Ghost eligibility, mandatory gadgets | **replace** |
| Content | 25 V2 levels | 30 V3 levels from the browser export | **replace** (import + revalidate) |
| Board size | up to 16×16 | 5–7 columns, portrait fit | keep engine limit, UI fit already exists |

## 3. Decision: new V3 core beside the V2 core, then retire V2 (D-027)

The V3 rules share almost nothing with the V2 engine except coordinates, directions, JSON plumbing and the test
infrastructure. Rewriting `RulesEngine`/`VisionSystem`/`GuardSchedule`/`LevelDefinition` in place would destroy the
V1/V2 parity suite while the new engine is still unproven. Therefore:

- New pure C# namespace `ShadowSteps.V3` inside the existing `ShadowSteps.Core` assembly (still no engine references):
  `Stage` (level), `StageState`, `StageRules.TryApply` (the single transition used by runtime, preview, solver),
  `GuardTrack` / `CameraTrack` (schedules), `Sight` (shared occlusion geometry), `StageSolver` (0–1 BFS),
  `StageValidator`, `StageMetrics`.
- Data: `StageJson` (schema 3, `schemaVersion: 3`) and `BrowserLevelImporter` (prototype export → schema 3).
  Shipped file: `Assets/StreamingAssets/ShadowSteps/levels_v3.json` + `validation_v3.json`. V2 files stay on disk
  until the runtime switch is verified, then move out of StreamingAssets.
- Runtime/UI are migrated to `ShadowSteps.V3` in M5 (board/guard/camera/gadget views, input modes, HUD, failure,
  progression). V2 views that are purely presentational (sprite factory, layout, camera fit, UI builder, screen router,
  save repository, audio) are reused.
- When M5 is proven in Unity, the V2 core, its tests and the V2 generator are deleted or moved to `reference/`.

Reused as-is: `GridCoord`, `Direction`/`DirectionUtil`, `MiniJson`/`JsonAccess`, `ValidationReport`, `SaveRepository`,
`UiBuilder`, `UIScreen`/`ScreenRouter`, `SpriteFactory`, `BoardLayout`, `CameraFitter`, `AudioService`, test runner,
compile check, meta generation.

## 4. Prototype behaviours that are ported exactly (and must be confirmed by the designer)

1. **Camera footprint starts one node out.** The prototype offsets the camera origin 0.501 outside the wall and rounds
   it to the adjacent floor node, then counts `forward = 1..2` *from that node*. The node directly in front of the
   camera is therefore a blind spot and the 3×2 footprint covers nodes 2 and 3 away. Browser pars and Ghost flags
   depend on this (e.g. Level 16's Ghost route passes through the blind spot). Ported as-is; flagged in KNOWN_ISSUES.
   **Superseded again 2026-11-03 (D-036):** cameras now use a 70° cone from the wall face, 2 spacings deep (`Sight.InCameraCone`); see `Docs/Reports/CAMERA_CONE_VALIDATION_2026-11-03.md` for the pack impact.
   **Superseded 2026-11-03 (D-035):** the footprint now starts at the front node (`Sight.CameraFootprintStart = 0`); camera stages were re-scored by the C# solver and browser parity is asserted only for stages without cameras.
2. **Ping-pong facing at the endpoint** comes from the previous position, so at an endpoint the guard still faces the
   endpoint while its NEXT arrow points back. Ported.
3. **Rotating guards and cameras** with `ccw` turn the other way; sweep alternates `dir` and `dir ± 1` every 2 actions.
4. **Swap capture** in the prototype is unreachable (old-node collision triggers first); the C# keeps both checks.
5. **Alarm budget** `L.budget || 4`: a budget of 0 silently becomes 4. The importer writes the effective value and the
   validator requires ≥ 1.
6. Guards/cameras ignore the key, doors and alarms entirely (schedules depend only on `t`).
7. Win needs only the exit node (no key check), evaluated after the enemy update and before gadget expiry.

## 5. Migration map for the 30 levels

- Source: `levels_v3_playtest_reference.json` (post-extension routes, `offset` preserved). Rows strings encode walls.
- Importer: walls from rows; connections = all adjacent floor pairs minus `cuts`; doors = listed edges; guards, cameras,
  gadgets, budget, intro, group, title copied; ids 1–30 with codes `FTUE-1..5`, `LEVEL-1..25`.
- Pars, star thresholds and Ghost offers are **recomputed by the C# solver** and compared with the browser values in a
  parity test; the browser values are fixtures, not truth.
- The V2 generator (`Tools/CoreTestRunner/Generator.cs`) is **not** reused for V3 content; a V3 generator is out of
  scope for this handover (content is authored).

## 6. Milestones for this pass

| Milestone | Deliverable | Proof |
|---|---|---|
| M0 | this audit, backups, reference copy | commit |
| M1 | V3 core model, schedules, geometry, turn resolution | fixture tests (§13 player/guards, doors) |
| M2 | smoke, EMP, alarm, win, Ghost, star thresholds | fixture tests (§13 cameras, gadgets) |
| M3 | 0–1 solver, validator, metrics | tests; validator on all 30 |
| M4 | importer, `levels_v3.json`, `validation_v3.json` | parity: 30/30 pars equal browser, traces replay to wins, no guard overlap, Ghost flags |
| M5 | runtime: board/guard/camera/gadget views, input modes, HUD, failure/retry, progression | compile check; PlayMode tests; Unity run pending (editor lock) |
| M6 | sequential unlock, debug-only undo, persistence of stars + Ghost | tests |

Not in this pass: final art, hints, checkpoints, monetisation, switches, endless mode, a V3 generator.
