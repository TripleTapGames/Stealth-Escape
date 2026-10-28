# Shadow Steps — granular task checklist

Legend: `[x]` done with evidence (see PROGRESS.md) · `[~]` implemented, verification pending · `[ ]` open.

## M0 — Repository audit and foundation
- [x] Audit project: Unity 2022.3.62f3, URP 14.0.12, Input Manager (legacy), Android build target, TMP 3.0.7, Test Framework 1.1.33
- [x] Place handoff docs at project root (CLAUDE.md, MASTER_GAME_SPEC.md, IMPLEMENTATION_PLAN.md, reference/, prompts/)
- [x] Copy reference/levels.json → Assets/StreamingAssets/ShadowSteps/levels.json (byte-identical, test enforced)
- [x] Folder layout Assets/_ShadowSteps/{Scenes,Scripts/{Core,Data,Runtime,UI,Editor},Prefabs,Materials,Art,Audio,Animations,ScriptableObjects,Tests}
- [x] Assembly definitions: Core (no engine refs), Data (no engine refs), Runtime, UI, Editor, Tests.EditMode, Tests.PlayMode
- [x] Deterministic .meta generator (Tools/gen_meta.py)
- [x] Domain schema: GridCoord, Direction, GameAction, GuardDefinition/GuardMode, LevelDefinition
- [x] Dependency-free JSON parser/writer (MiniJson) + strict LevelPackLoader with format_version + migration hook
- [x] LevelValidator (bounds, overlaps, routes, loop closure, key/door pairing, reachability, duplicate ids)
- [x] SaveData / SaveSerializer / SaveRepository (temp write, .bak rotation, corrupt fallback)
- [x] EditMode tests: pack loads 20 levels, DTO errors carry JSON paths, round-trip, validator cases, save cases
- [x] dotnet-based runner for Core/Data/EditMode tests (Tools/run_core_tests.sh)
- [~] Unity Test Runner execution of the same tests (blocked: editor GUI open → batch mode locked)
- [x] Docs/PROGRESS.md, DECISIONS.md, KNOWN_ISSUES.md

## M1 — Pure rules engine and solver parity
- [x] GuardSchedule (fixed/rotate/patrol/patrol_rotate/loop, step, rotation, period = lcm)
- [x] VisionSystem (discrete 90° cone + 12-sample centre-to-centre LoS; walls and locked door block)
- [x] GameState (immutable) + reference-tuple trace output
- [x] RulesEngine: Validate, Step, Preview, GetVisionCells, ValidateAll
- [x] GameSession: Apply/Undo/Restart/Replay with per-step trace
- [x] Solver: BFS port driven by RulesEngine; constrained solves (no smoke / no EMP)
- [x] Tests: schedule, vision (4 facings, range, diagonals, wall/door occlusion), smoke 2-action, occupancy under smoke, swap, EMP freeze + sight, key/door, capture-before-win, invalid actions, undo, restart, determinism
- [x] Parity: all 20 validation.json solutions win with exact turn count; solver reproduces exact path and explored-state count for all 20
- [x] Constrained-solve report of truly required gadgets (documented in DECISIONS.md)

## M2 — Unity graybox level
- [ ] BoardTheme ScriptableObject (prefabs, colours, tile size)
- [ ] Tile/wall/exit/key/door/player/guard prefabs (primitives)
- [ ] BoardPresenter (pooled instances, rebuild per level)
- [ ] Vision overlay + guard facing arrow + route markers + next-turn ghost
- [ ] GameSessionController (events, input lock, undo/restart)
- [ ] InputController (swipe, tap adjacent, keyboard), no double input
- [ ] TurnAnimationCoordinator (move/rotate tweens, capture/win feedback)
- [ ] CameraFitter (orthographic, portrait, safe area)
- [ ] Minimal HUD (turns, charges, WAIT/SMOKE/EMP/UNDO/RESTART)
- [ ] Game scene bootstraps level 1 standalone in editor
- [ ] PlayMode test: scene loads, board matches level 1, HUD reflects state

## M3 — All 20 levels and progression
- [ ] App services (level catalog, save, settings) + Bootstrap scene
- [ ] Home, Level Select (lock/complete/best), Results, Failure, Pause/Settings prefabs
- [ ] Unlock sequencing + best-turn save
- [ ] Tutorial hints (data-driven), invalid-action feedback
- [ ] Debug level selector (editor/dev builds only)

## M4 — Level authoring tools
- [ ] ShadowStepsLevelEditorWindow (paint, place, guards, charges, validate, step, export with backup)
- [ ] LevelValidationMenu (validate pack, solve all, report)
- [ ] Two sandbox levels in a separate non-shipping pack; round-trip test

## M5 — Original art and animation kit
- [ ] ART_BIBLE.md, materials/palette, procedural meshes, guard silhouettes by mode
- [ ] Move/rotate/capture/clear effects, reduced-motion path

## M6 — Full UI, onboarding, sound, UX
- [ ] Final screens, onboarding sequence, generated SFX, audio/haptics toggles, resume handling

## M7 — QA and optimisation
- [ ] Full EditMode/PlayMode in Unity, device checks, allocation audit, issue log

## M8 — Android RC and iOS export
- [ ] Bundle id, icons/splash, IL2CPP/ARM64, AAB build script, store placeholders, privacy policy placeholder
