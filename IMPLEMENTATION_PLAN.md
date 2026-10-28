# Implementation plan — execute sequentially with evidence

**Rule:** each phase ends with a runnable, demonstrable output. Keep `Docs/PROGRESS.md` up to date. If a phase cannot be verified locally, do not claim verification; provide the exact manual test.

## M0 — Repository audit and foundation
- Inspect project; record Unity/editor version, render pipeline, existing assemblies/packages and build targets. No destructive operations.
- Copy reference JSON into StreamingAssets and establish folder layout, asmdefs, scenes, bootstrap conventions, project README, test assembly.
- Create game state schemas with valid JSON loading; automated validation of all 20 definitions.
**Gate:** 20 levels load in an EditMode test; zero compile errors; documented known setup actions.

## M1 — Pure rules engine and solver parity
- Implement board movement, action validation, turn/phase logic, guard schedules, geometry/LoS, key/door, Smoke/EMP, capture, win, Undo.
- Expose state trace outputs for independent parity debugging.
- Write EditMode unit tests and replay all 20 validated solutions, matching Python reference action count and winning state.
**Gate:** every reference replay passes in C#; edge-case tests for smoke, EMP, occupancy, swaps, walls, key, invalid actions, undo.

## M2 — Unity graybox level
- Generate a visible board using primitives; spawn player/guard tokens, walls, key/door, exit; draw vision cone tiles and guard facing/path hints.
- Top-down/angled orthographic camera with fit; keyboard and touch input; turn animation and cooldown; simple restart/undo/wait/gadget HUD.
**Gate:** Level 1 is playable from Game scene, capture/exit states are correct, no input double triggering.

## M3 — All 20 levels and progression
- Level loading/selection and unlock; best turn count and save; tutorial prompts; pause/retry/win results; route-accurate previews.
- Show current guard state, tile danger, gadget charges, key state, preview rules and invalid action feedback.
**Gate:** all 20 can be loaded, replayed/won, and unlocked; local save survives restart; UI works at portrait resolutions.

## M4 — Level authoring tools
- EditorWindow for painting grids and configuring guard routes, start/exit, walls, key/door and charges; validation and import/export with backups.
- In-editor preview, one-turn stepping, and solver bridge where practical; create two non-shipping sandbox stages and test round trip.
**Gate:** designer can author/export/import an independent solvable level without hand-editing JSON.

## M5 — Original art and animation kit
- Implement coherent toy-diorama style with original primitive/procedurally generated meshes/materials. Improve guard and operative silhouettes; subtle move/rotate/clear/capture effects; no gameplay view ambiguity.
- Write `Art/ART_BIBLE.md` and asset provenance list. Bring scene/UI color and typography into a consistent look.
**Gate:** readable danger on small portrait screens; no external copyrighted appearance copied; clean material sharing, batching.

## M6 — Full UI, onboarding, sound and UX
- Home, level select, gameplay HUD, pause/settings, win/loss, accessible tutorial sequence, SFX placeholders and audio toggles, haptic control where supported.
- Device-safe responsive UI and resume flow.
**Gate:** complete launch-to-20th-level journey with no debug keyboard needed on device.

## M7 — QA and optimization
- Run EditMode/PlayMode suites; manual replay and exploratory testing; device portrait/tall/compact checks; optimize batching/allocations.
- Log issues and verify fixes. Test fresh install, update install, corrupt save, app suspend, fast tapping, airplane mode.
**Gate:** zero critical defects, no softlocks, stable target device frame pacing.

## M8 — Android release candidate and iOS export
- Configure bundle ID, app metadata, icon/splash/screenshots as approved originals, IL2CPP, ARM64, app bundle, privacy policy URL, data declarations based on actual SDKs.
- Produce Android AAB in supported build environment; iOS export/Xcode guidance and validation. Don't fabricate submission status.
**Gate:** signed release candidate and Google Play internal test where developer has access, iOS archive/test under user's developer account. Human approval for store/legal and production release.

## Milestone report format
```
Milestone:
Files created/changed:
Tests run (command/environment/results):
Verified in Unity editor? (yes/no):
Verified on device? (yes/no):
Open issues:
Manual steps for user:
Next milestone:
```

## Code quality requirements
- Small modules, explicit state, no logic in animations, stable deterministic ordering, no frame-time-dependent enemy decisions.
- Domain tests without loaded scenes; PlayMode tests for scene/presentation integration.
- Level versions, save versions, meaningful error log lines.
- No gratuitous abstract factories and no plugin proliferation.
- Refuse silent rule drift: update Python reference, regenerate levels and re-solve before changing released mechanics.
