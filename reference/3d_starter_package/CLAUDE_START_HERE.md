# SHADOW STEPS 3D — CLAUDE CODE START HERE

**Deliverable:** Transform the EXISTING Unity Shadow Steps project to use modular stylized 3D visual assets in a fixed angled orthographic 2.5D presentation. **Keep the existing deterministic 2D grid gameplay engine.** Do not start a new game. Do not discard the tested V4 blocked-node levels.

## Package map

- `UnityDrop/Assets/_ShadowSteps/Art3D/Models/` — **26 real `.glb` prototype models**, Y-up and unit-scaled, including floor, walls, props, cameras, doors, and proxy player/guard.
- `UnityDrop/Assets/_ShadowSteps/Art3D/Runtime/` — C# visual-only V4 data adapter, modular level renderer, fixed orthographic camera fitter, decorative courtyard frame and preview controller.
- `UnityDrop/Assets/_ShadowSteps/Art3D/Editor/` — Menu command `Tools > Shadow Steps > 3D > Create Both Demo Scenes` and auto art-palette importer.
- `UnityDrop/Assets/_ShadowSteps/Art3D/Data/` — latest `shadow_steps_levels_v4.json`, all **5 FTUE + 25 main** levels.
- `UnityDrop/Assets/_ShadowSteps/Art3D/References/` — visual images: original colorful 2.5D gameplay reference and cleared courtyard background reference.
- `ReferenceV4/` — the previous complete V4 rules handover, HTML playtest and validation; **reference only, do not reinstall/overwrite indiscriminately**.
- `Tools_generate_models.py` — reproducible source generator (Python numpy + trimesh), to change mesh geometry/solid colors and regenerate `.glb` files.
- `MODEL_VALIDATION_REPORT.json` — actual mesh/data inspection, plus camera mount warnings.
- `ART_DIRECTION_AND_TECH_SPEC.md` — target look, geometry and visual rules.
- `CLAUDE_IMPLEMENTATION_PLAN.md` — implementation order, acceptance checks and risks.

## Read this exact order

1. Read this file and `ART_DIRECTION_AND_TECH_SPEC.md`.
2. Read `CLAUDE_IMPLEMENTATION_PLAN.md`.
3. Read the existing project folder structure, current V4 level classes, engine, level renderer, guard/camera effects, and URP settings **before modifying files**.
4. Refer to `ReferenceV4/ShadowSteps_ClaudeCode_Handover_V4/SHADOW_STEPS_CLAUDE_CODE_HANDOVER_V4.md` for authoritative gameplay; legacy file inside that folder is NOT the latest rule set.
5. Read the shipped Unity art scripts; they are a **starting integration layer, not verified compiled code**. Fix any conflicts and test in the user's Unity Editor.

## Unity dependencies

- Existing Unity URP project, ideally the user's current **Unity 2022.3.62f1** project (or their installed working 2022.3 LTS patch).
- Install Unity glTFast (`com.unity.cloud.gltfast`) in Package Manager to import `.glb` into Editor as reusable model prefabs.
- Install Unity Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`) only if absent; the starter V4 JSON DTO uses it. You may adapt to the project's existing JSON reader instead.
- Never add a second glTF importer as default if glTFast is already installed.

## First operation

- Back up / commit the working Unity project.
- Copy **only `UnityDrop/Assets/_ShadowSteps/Art3D`** into the matching Unity project's `Assets/_ShadowSteps/` location. Keep existing game scripts and V4 runtime data untouched.
- Open Unity; install/import packages; let 26 `.glb` models finish importing.
- Run `Tools > Shadow Steps > 3D > Create Both Demo Scenes`.
- Verify `Assets/_ShadowSteps/Art3D/Demo/Clockwise_Rotating_Guard.unity` and `.../Camera_Basics.unity` generate and display the real meshes.
- Fix import material issues, camera framing and Unity compile errors in this new isolated art layer only. These **art demo scenes are not yet playable**.

## Then connect gameplay

- Adapt the art renderer to your existing `LevelDefinition` and runtime snapshot/events, using the authoritative Unity engine. Do not let a second renderer independently compute detection or patrol directions.
- Bind player/guard transforms to actual grid nodes after each action; show current-facing and **next-step** direction for moving guards and correct clockwise/counterclockwise markers.
- Attach two separate visible parts to each wall camera: fixed bracket and rotating/sweeping head; render amber 3-wide × 2-forward danger nodes accurately, using engine LOS.
- Bind locked/open door visual state, key pickup, Smoke, EMP-disabled state with two-action countdown, alarms and exits. Keep blocked `X` obstacles opaque both visually and logically.
- Preserve the full-board mobile fit, tap/swipe controls, optional preview, all 30 levels and scoring.
- After Clockwise and Camera Basics pass on a phone, roll out visual art to all 30 levels and profile performance.

## Critical caveat discovered during packaging

In **V4 Level 21 (Camera Basics), Level 23 (Rotating Security), and Level 24 (Sweep Window)**, a camera `wall` anchor `[3,0]` happens to be on a **walkable `.` cell**, not an actual `#` wall cell. This is pre-existing level data. The art renderer currently positions the camera as the browser prototype does, but the bracket might visually float. Resolve *cosmetically* by creating a wall-facing bracket just outside/above the board, or explicitly migrate & re-solve that level's camera topology. **Do not change vision origin, block a playable node, or silently change solver outcomes.** See report for details.

## Definition of done

- Two genuine 3D art preview scenes built in Unity, rendering modular 3D meshes, not flat image billboards.
- At least two playable vertical-slice levels (rotating guard, wall camera) driving the 3D view from the existing rules engine.
- Full 30-level import preserved; blocked nodes and every LOS/movement rule unchanged.
- Camera hazards correspond to actual dangerous nodes; physical walls and planters properly block LOS.
- No bushes/planters in gameplay space except nodes **explicitly designated `X`**. Decorative greenery can be outside the grid behind the walls.
- Device-level checks on a compact portrait Android screen; readable HUD and no obscured nodes.
- Clean build/log, script tests and recorded list of differences between reference and Unity.

**The current starter kit is art blockout geometry, not final polished models or rigged animations. Final look needs iterative Blender art polish and character rigging.**
