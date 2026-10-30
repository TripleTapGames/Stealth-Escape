# Claude Code Implementation Plan — Existing Unity Project

**Do not replace the 2D turn-based rules engine.** Treat art kit as a visual adapter onto the already-approved V4 30-level dataset.

## Milestone 0 — Audit and protect existing work

- Make a source-control commit. Record Unity editor version, URP pipeline asset and current scripting backend.
- Locate existing `LevelDefinition`, render/GameObject spawn system, guard schedule, camera schedules, vision and LOS, Smoke/EMP, camera alarm, scorer, progression and validation tests.
- Confirm active level data is V4 (`X` = opaque blocked cover); if project still uses older JSON, import V4 via existing normal mechanisms first.
- List scripts to preserve, replace and adapt. Avoid duplicate authoritative gameplay systems.

## Milestone 1 — Import model package

- Copy `UnityDrop/Assets/_ShadowSteps/Art3D` into project. Do NOT copy nested `ReferenceV4` into live Assets.
- Install `com.unity.cloud.gltfast` with Package Manager (supports Editor `.glb` import).
- Check `com.unity.nuget.newtonsoft-json`; install if absent or replace starter DTO with the game's existing JSON package.
- Check material transparency, correct local axes, lengths, import scale 1, surfaces and no magenta materials; adjust URP defaults as needed.
- Click `Tools > Shadow Steps > 3D > Create Both Demo Scenes` to generate two ART-ONLY scenes; check Unity console.

## Milestone 2 — Create playable 3D vertical slice

- Use the existing game controller/level loaded events to call `SS3DLevelRenderer.Build(level)` or an adapted native-data overload.
- Replace legacy sprites in **one level only** with 3D models; actor and guard transforms mirror authoritative snapshot, never simulate their own schedules.
- Map grid `N +Z`, `E +X`, `S -Z`, `W -X`. Use XZ for all world positions, Y for visuals.
- Keep full-board orthographic fit and portrait safe-area HUD. Set down angle near 63° as starting point, not an immutable constant.
- Ensure tap/swipe/long-press input raycasting hits logical node locations (projection from 3D screen space to XZ grid) and doesn't interact with visual-only props.
- Reuse real renderer for camera, blocker, key, door, exit and every existing input mode.
- Make all art meshes non-colliding by default unless interactive colliders are intentionally mapped to game logic, so mesh colliders cannot block touches.

## Milestone 3 — Surveillance, cover and tactical readability

- Use ONLY existing visibility calculations to draw guard 2-node straight detection and camera 3-wide × 2-forward occlusion, including strict wall-corner blocking and Smoke.
- Guard danger: translucent red cell surface + bold red outline; camera danger: amber surface + bold amber outline; overlapping shows both rings.
- Preview: future/during-turn area is dashed, current area solid. Crucially preview must simulate door opening before guard current-vision check, both camera checks and instant expiration detection.
- Camera mount stays on wall; camera head independently rotates or sweeps. EMP disables active amber coverage for exactly 2 counted actions but head animation continues.
- Rotating guards/cameras show CW/CCW indicator; sweeping cameras show the two-direction indicator. Moving guards show NEXT arrow to next node (reverse at true endpoint).
- Blocked `X` geometry must never cause mismatch with authoritative `obstructed()` behavior.
- Explicitly solve the V4 walkable camera anchor warning in three levels visually; do NOT create new `#` cells or change LOS without retesting.

## Milestone 4 — Art polish and animation

- Replace proxy spy/guard with rigged meshes without changing world-space footprint or facing. Add tiptoe, walk, idle, rotate, capture and escape states.
- Bevel, texture and polish courtyard shell from Blender. Keep target triangles/material count within mobile budget; use instanced static props and texture atlas where useful.
- Add camera swivel, Smoke, EMP cyan pulse, failure/retry and exit animations; speed should match completed discrete engine actions.
- Keep all decorative greenery outside logical board except V4 `X` planter cells.
- Tune light and shadows on real Android and iOS devices; avoid shadows obscuring nodes or danger rings.

## Milestone 5 — Full rollout and automated validation

- Apply the single renderer to all 30 V4 levels; no per-level background illustration.
- Compare Unity state transition traces against HTML prototype for a selection of fixed, rotating, straight/loop patrol, camera, doors and gadgets.
- Verify all 30 levels have at least one winning sequence, no same-node guard occupancy at logical turn boundaries, Smoke and EMP duration 2, exact camera alarms and Ghost availability.
- Verify keys are reusable and opening door changes sightlines immediately.
- Validate par + star thresholds, including optional gadget loadout, and distinct Ghost badge; avoid changing achievements silently.
- Verify extreme board sizes and mobile viewport. Test small devices with tap/swipe and long-press, and ensure no accidental spending gadget charge.
- Record screenshots of Clockwise and Camera Basics final playable 3D scenes plus an Android frame profile.

## State-bridge API examples

The starter `SS3DLevelRenderer` exposes:

```csharp
artRenderer.Build(levelDto); // once on level load
artRenderer.SetPlayerVisual(new Vector2Int(playerX,playerY)); // on authoritative state commit
artRenderer.SetGuardVisual(i,new Vector2Int(x,y),facing); // orientation from actual guard schedule
artRenderer.SetCameraVisual(i,facing,empDisabled); // active camera head orientation
artRenderer.SetDoorVisual(i,isOpen); // only when state changes
artRenderer.SetVision(guardDangerCells,cameraDangerCells); // from existing engine
```

These functions are *visual output only*. Use existing camera/guard rules for `facing`, every danger node, future facing, alert budget and gadget effects. Improve the art adapter to minimise instantiate/destroy each action once wired.

## Known limitations of the shipped starter

- The 26 real GLB models are simple colored geometric blockouts. They are not final polished assets matching a concept render.
- The spy and guard stand-ins are **not rigged**; animation still needs implementation.
- The two generated demo scenes are *static art previews*, not integrated games.
- `SS3DLevelRenderer.SetVision` offers a simple tinted quad and double ring; animation, dashed future overlays, alarm beams, debug rays and visual smoothing still need to be integrated.
- The 3D art source was generated and mesh-checked here, but **not compiled or playtested in the user's Unity Editor**. Claude must address any import/component differences and test on actual devices.
- Rebuilding all geometry every turn will be slow. Only rebuild static board on level load, update actor visuals/overlay pools per action.
- `V4` level `cams[].wall` metadata is inconsistently a true `#` tile; see 3 listed warnings. Resolve in visuals or re-author + re-solve levels explicitly.

## Acceptance tests to record

1. Open the two auto-generated art-only scenes: geometry visible, palette material colors visible, camera correctly framed.
2. In Clockwise: guard rotation, marker direction and before/after capture match simulation.
3. In Camera Basics: camera is mounted and camera head rotates without bracket turning; exact camera danger cells match engine LOS.
4. A player can't enter `X` or `#`, but can see through missing-cut graph edges when physically unobstructed.
5. Smoke thrown onto guard works; cloud stationary; expires after second action with instant visibility check.
6. EMP disables a selected wall camera for two full actions; reactivation check and countdown correct.
7. Camera alarm never pauses enemy updates; exit on final permitted action wins but no Ghost.
8. Every Retry resets actors, doors, gadgets and alarm deterministically.
9. All 5 FTUE + 25 main remain selectable in development, sequential on release.
10. No greenery outside the approved `X` cells occupies gameplay nodes; decorative perimeter pieces do not block node taps.
