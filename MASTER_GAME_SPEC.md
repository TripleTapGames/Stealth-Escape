> **V2 override (2026-10-28):** Use fixed-distance cardinal graph nodes, explicit connections, a top-down orthographic camera, and sprite-based 2D characters/environments. Any directions below referring to 3D miniature meshes, angled camera, tile-only adjacency, or primitive 3D final art are legacy V1 suggestions and superseded by `V2_CHANGE_CONTROL.md` and `LEVEL_DESIGN_PARAMETERS.md`. The original 20-level solver remains a legacy reference until migrated and revalidated.

# Shadow Steps — Complete Production Game Design & AI Implementation Specification

**Version:** 1.0 | **Date:** 2026-10-28 | **Product:** Original single-player mobile stealth-puzzle game | **Status:** AI implementation brief

## 0. Product goal and non-negotiables
Deliver a shippable **premium-feeling, portrait, turn-based tactical stealth puzzle game** for Android first and iOS second, with an original identity, 100-level expandable design, and a high-quality first release of **20 fully playable, regression-tested levels**. The player navigates a square orthogonal board. Each valid player action advances an otherwise deterministic enemy simulation. Enemies have facing and 90° cone vision; player can plan around rotations/patrol cycles and use limited tactical gadgets. The game should be understandable from visual cues alone after a short onboarding. Make it enjoyable *before* art polish.

**V1 release scope**: 20 levels, 5 baseline guards, cone vision, key/locked door, Smoke, EMP, WAIT, Undo, Restart, Next Level, level selection, Settings, sound toggles, haptics toggle, in-game tutorial overlays, progression, local persistence, mobile input, background/foreground handling, QA instrumentation, legal/store placeholders. **Not V1**: real-time combat, procedurally unverified live levels, online accounts, cloud backend, multiplayer, in-app purchases, ad SDKs, chase AI, switches, teleports, laser puzzles, advanced guards. These are optional future backlog items with no impact on V1 rules.

## 1. Experience and loop
### High-level loop
Home -> Level Select -> Level Start (brief mechanic hint) -> Inspect board and next-turn preview -> choose Move / WAIT / SMOKE / EMP -> resolve a deterministic turn with quick readable animations -> Capture/Loss or Objective/Win -> Retry/Undo or results -> Next Level. Include Pause and Settings.

### User controls
- One-tile cardinal movement (swipe on board or optional on-screen directional controls); optional keyboard WASD/arrows for desktop testing.
- WAIT button; SMOKE and EMP action buttons with remaining counts; Undo; Restart; preview toggled/visible as clear ghost paths. Tap directional destination if adjacent as alternative.
- Only a **valid** action consumes a turn. Out-of-bounds, wall, locked-door, blocked invalid moves do not consume charge/turn. Buttons disable during a turn animation and while modal open; no double input.
- Entire board and HUD must fit portrait screens; account for notches and Android gesture areas; no unreadable tiny text. Support a range of phone aspect ratios and 30+ fps on ordinary midrange devices.

### End conditions
- Capture if guard occupies the player's destination after its turn, guard swaps edges with player, or (V2 detection rule, 2026-10-28) the player's tile is inside an unobstructed cone either before or after the guards move (except when Smoke active, occupancy still captures). Schema 1 fixtures keep the V1 single after-move cone check via `rules.detection`.
- Win when player reaches exit and key objective fulfilled (when a key exists), after all hazards for the turn resolve.
- Win and loss are mutually exclusive per turn: capture precedes win.

## 2. Canonical simulation contract
**Canonical source**: `reference/DESIGN.md` and `reference/build.py`. Mirror reference exactly, including unusual definitions. Do not alter V1 original level behavior for the sake of convention.
- Logical origin (0,0) top-left; X east, Y south. Four directions: N=0, E=1, S=2, W=3.
- Action set `N`, `E`, `S`, `W`, `WAIT`, `SMOKE`, `EMP`. Smoke/EMP keep player stationary and spend a turn and charge. WAIT spends turn without moving.
- On valid action: player movement, key pickup/door unlocking; enemy phase advance unless EMP; simultaneous enemy destination and edge-swap capture; cone vision evaluation (V2: seen before **or** after the guard move, see LEVEL_DESIGN_PARAMETERS.md §5.7); win check. Smoke conceals on activation and following player action, then expires; it does *not* prevent guard occupancy capture. EMP freezes the **global** enemy phase for its activation action, but not vision.
- Cone: exact discrete grid rule `1 <= forward <= range && abs(lateral) <= forward`, sampled LoS and walls/locked doors block vision. Orientation/facing from canonical solver. Never use a physics raycast as the authoritative detector; physics can only visualize it.
- Guard routes/facing behavior and cadence are defined by `reference/DESIGN.md`. Calculate all enemy states from shared logical turn/phase rather than by moving transforms.
- Undo restores full snapshot: player, phase, key, gadgets, smoke lifetime, turn count, terminal state. Undo of a terminal move permitted if allowed by UX. Restart restores initial snapshot.
- Enemy movement/vision/animations must be deterministic. Animation may lag logical state visually, but must not author game state.
- Flag any discrepancy between Python and C# behavior by level number, action number, expected state and actual state. Build parity tests for all shortest solutions from `validation.json`.

## 3. Enemy design
| Enemy | Mode/config | Behavior | Telegraphed to player |
|---|---|---|---|
| Static watcher | `fixed` | Holds tile and direction permanently | Single facing arrow and stable cone |
| Clockwork watcher | `rotate` | Fixed tile, rotates one 90° step per authored cadence | Next-facing arrow |
| Straight patrol | `patrol` | Advances on straight authored route and ping-pongs | Route markers and next position ghost |
| Rotating patrol | `patrol_rotate` | Moves and rotates on schedule | Both next tile and next direction |
| Loop patrol | `loop` | Repeats authored loop route | Loop path dots |

Variants available through config (not separate scripts): `range`, `step`, `rotation`, starting facing and authored route. Future types (not implemented in V1) include oscillating watcher, alternating cadence, alerted tracker, sound responder, camera, heavy sentry, and switch-triggered guard. Keep extensibility via interfaces and data-driven configuration but do not create unused complex frameworks.

## 4. Gadgets and objective systems
### Smoke
Uses one charge, no movement that action, applies player concealment on deployment action and next player action. Represent as a simple player-centered visual bubble/tile overlay; authoritative effect is state field, not particle collision. Clearly show smoke remaining turns. No stacking or free uses.
### EMP
Uses one charge, no movement that action, freezes phase progression on that single action. Guard sight at its existing phase still counts. Display pulse and show pause icon on guards. Charge count decremented once.
### Key/door
Single key and single locked door per specified level in V1. Key collected on entry. Door ceases to block player and LoS from that turn onward. For referenced levels, key is required to finish even if the door route can be bypassed.
### Undo & Restart
Undo should snapshot before a valid action and unwind one step with animation cancellation and HUD refresh. Restart asks for confirmation only when appropriate; for fast puzzle replay avoid friction.

## 5. Levels and progression
**Source of truth**: `reference/levels.json` (IDs 1–20). Never replace these with hand-waved generated levels. `reference/validation.json` contains shortest winning solutions under `reference/build.py` rules. Load them as authored; detect malformed boards, invalid start/exit or routes, inconsistent obstacles, duplicate IDs, invalid initial placements and unreachable solutions. Build a debug level selector permitting direct loading of all 20. The release level selector unlocks stage 1 initially and then sequentially based on local save.

### Original 20-level curriculum (use existing level titles and exact data)
- 01–04: navigation and individual guard patterns.
- 05–08: combined patterns, loops, cone timing.
- 09–12: keys/doors and limited gadget introduction.
- 13–16: interactions under multiple threats.
- 17–20: layered timing, cones, restricted resources.

**Crucial**: A solver-confirmed route only proves solvability, not that a level requires its theme mechanic, is interesting, or is uniquely solved. For each level, run three checks: (a) reference solution wins; (b) shortest path length is as expected; (c) any claimed *required* mechanic is actually required using constrained solve (e.g. no smoke, no EMP). Mark anything not verified as design intent, not proven. Manual human playtesting is still required.

### Future 100-level content roadmap (after V1)
World 1 / 1–20: Patrol Academy (current pack). World 2 / 21–40: security doors and alternating cone timings. World 3 / 41–60: switches and physical cover. World 4 / 61–80: dynamic traversal (teleports, one-way floor). World 5 / 81–100: mastery with sparse gadgets and multiple timing schedules. Every new rule needs a simulator extension, solver tests and editor controls before content ships. Never auto-generate level content with unverified playability.

### Optional mastery scoring
V1 use completion + best valid turns; optionally show 1–3 stars based on curated level-specific thresholds, not arbitrary guessed formulas. Avoid punishing experimentation with lives or energy. Store best result only after verified level win.

## 6. Unity architecture and folder layout
```
Assets/
  _ShadowSteps/
    Scenes/Bootstrap.unity, Home.unity, Game.unity
    Scripts/
      Core/                 # plain C#, no UnityEngine
        GridCoord.cs, Direction.cs, GameAction.cs, LevelDefinition.cs,
        GameState.cs, GuardState.cs, RulesEngine.cs, VisionSystem.cs,
        GuardSchedule.cs, TurnResult.cs, GameSnapshot.cs, LevelValidator.cs
      Data/                 # JSON DTOs and safe mapping
        LevelDtos.cs, LevelRepository.cs, SaveData.cs, SaveRepository.cs
      Runtime/              # Unity orchestrators
        GameBootstrap.cs, GameSessionController.cs, InputController.cs,
        BoardPresenter.cs, GuardPresenter.cs, EffectPresenter.cs,
        TurnAnimationCoordinator.cs, CameraFitter.cs
      UI/
        HomeController.cs, LevelSelectController.cs, GameHudController.cs,
        ResultsController.cs, SettingsController.cs, TutorialController.cs
      Editor/
        ShadowStepsLevelEditorWindow.cs, LevelValidationMenu.cs
    Prefabs/ Tiles/, Units/, FX/, UI/
    Materials/, Art/, Audio/, Animations/, ScriptableObjects/
    Tests/EditMode/, Tests/PlayMode/
  StreamingAssets/ShadowSteps/levels.json
Docs/PROGRESS.md, DECISIONS.md, KNOWN_ISSUES.md
```
Recommended pure domain API:
```
GameState CreateInitialState(LevelDefinition level);
ActionValidation Validate(GameState state, GameAction action);
TurnResult Step(GameState state, GameAction action); // pure result/state copy
IReadOnlyCollection<GridCoord> GetVisionCells(GameState state, int guardIndex);
TurnPreview Preview(GameState state, GameAction action); // same core rules; no side effects
GameState Undo(GameSession session);
```
- `RulesEngine` is authoritative; presenters render it.
- `BoardPresenter` creates pooled or cached tile prefabs, walls, exit, doors, key, guards. Reuse instances; reset cleanly between levels.
- `InputController` translates touch and desktop input into actions; disable concurrent input during transitions. For swipes: threshold and directional dominance, no accidental double swipe.
- `CameraFitter` sizes orthographic camera to grid and safe zone. Use 2.5D isometric-ish art only if it preserves logical clarity; keep cardinal controls intuitive.
- JSON must be converted via DTO validation into immutable-ish domain definitions; avoid relying on `JsonUtility` to deserialize nested arrays directly without verifying behavior.
- Persist progress in a single versioned JSON file in `Application.persistentDataPath`, using safe temp write + backup, with fallback for corrupt save. Never store secret keys. Save after level completion and settings changes.
- Do not create an architecture that requires network, subscriptions, backend or commercial tooling.

## 7. Camera, presentation, and original art direction
### Visual theme
**Miniature Midnight Heist**: original small security-diorama world with clean elegant board tiles, cool navy/slate neutral floor, teal interactables, warm amber danger outlines, bright white accessible indicators, and friendly but readable silhouette-based guards. Premium stylized 2.5D/low-poly rather than realistic violence. Player is an original stealth operative silhouette with a unique identity (avoid copying any recognizable franchise protagonist). Guards communicate mode with hat/shoulder shape and movement arrows, never color alone.

### Art pipeline suitable for AI-first solo developer
- First prototype uses Unity primitives and flat materials. No image is required to block engineering.
- Maintain an `Art/ART_BIBLE.md` covering shape language, tile size, orthographic camera angle, lighting, materials and icon contrast before making final assets.
- Generate concept sheets and UI exploration using an available generative image service manually; **Claude Code cannot magically generate/import every production-ready rigged model**. For final low-poly game assets, produce original procedural geometry using Unity primitives, ProBuilder where installed, or Blender scripting if available. Record source/rights for any imported asset.
- Consistent modular tile kit: floor normal, wall, safe tile, exit, locked/unlocked door, key, smoke, EMP, guard route markers. 1x1 footprints; 1 world unit tile spacing initially.
- Guard prefab variations: fixed, rotary, patrol, rotary-patrol, loop. Friendly stylized capsule body with arrow/visor pointing forward; avoid fine details that are invisible on phones.
- Indicators take precedence over decoration: occupied tile, vision cone, direction arrow, intended move ghost and objective remain legible at small sizes. Visibility geometry visualization must agree with `VisionSystem` cell membership.
- Build art in phases: debug graybox -> coherent colors and modular 3D toy props -> character polish -> particles/animation -> store marketing. Do not block the game on AI art.

### UI screens
1. Splash/Bootstrap: fast startup, fails gracefully on malformed data.
2. Home: Play, Level Select, Settings; original logo placeholder.
3. Level Select: 20 numbered stages, locked/completed/best-turn states, progress badge.
4. Gameplay HUD: level/title, moves, exit objective, gadget charges, Undo/Restart/Pause, WAIT. Onboarding hints; accessible buttons.
5. Pause/Settings: resume, restart, sound/music/haptics toggles, privacy-policy link placeholder, credits.
6. Result: completion, turns, best, next level, replay.
7. Failure: succinct caught state, Undo/Retry affordance, no long penalty.
Use TextMeshPro, 9-slice backgrounds, safe-area anchors, vector/SDF icons where available; match layout from Figma/concept only after gameplay is stable. Responsive phone screenshots at e.g. 1080x1920 and 1170x2532, not hard-coded device dimensions. Add subtle tween animation without introducing heavy dependencies.

## 8. Audio, haptics and accessibility
- Distinct sounds for footstep, clock tick, patrol rotate, gadget, alert, unlock, clear. Build without requiring copyrighted audio: temporary generated tones or properly licensed sound files, documented origins.
- User toggles for Music, SFX, Haptics with persistence; no audio required to solve a level.
- Colorblind-friendly icon/shape distinction; high-contrast cone overlay; reduced-motion option if feasible; minimum touch targets roughly 44–48 device-independent points; no flashing hazard patterns.
- Smooth pause/resume after app interruption, no accidental action on resume.

## 9. Performance, builds and platform
- Target 60 fps on ordinary mobile hardware; stable at 30 on lower-end phones. Tiny boards should not need heavy optimization; avoid per-frame allocation/GC from vision updates. Vision recalc only on logical turns and previews.
- Unity URP mobile-friendly renderer; avoid expensive real-time shadows and postprocessing as a dependency.
- Provide Android test build path, signing instructions without asking for passwords in code, portrait orientation, IL2CPP+ARM64 for release and standard Gradle/SDK configuration compatible with installed Unity version.
- iOS Xcode export checklist, safe-area testing, Metal device validation and privacy usage descriptions only if actually required by selected SDKs.
- Offline first, no telemetry or ad SDK by default. If analytics/ads are added later, audit actual data collection and update privacy disclosures/store declarations. No invented SDK behavior.
- Provide store-ready placeholders for app icon, screenshots, feature graphic, Play listing and privacy policy, but do not treat placeholders as submitted assets.

## 10. Testing plan and exit criteria
### EditMode tests
- Invalid move doesn't advance time/charge.
- Patrol ping-pong and loop wrap, rotation 90°, intervals and global phase semantics.
- Vision cone boundary, range, wall/locked door occlusion, diagonal inclusion; cross-check sampled LOS edge cases.
- Smoke two-action concealment and occupancy exception; EMP freeze one action and sight exception.
- Key collection, locked door, exit requiring key.
- Simultaneous guard movement, guard-player edge-swap, loss-before-win.
- Undo snapshot restoration and deterministic replay.
- DTO loading for all 20 levels, level validation, corrupted save fallback.
- Replay every reference shortest solution from `validation.json`: should win with exact action count; replay all prefixes and compare state traces if instrumented; invalid/gadget-constrained paths as relevant.

### PlayMode tests
- Scene loads and board maps tiles correctly; HUD reflects state; no duplicate transitions.
- Swipe/keyboard/tap controls; pause/resume; Restart/Undo; level select unlock and return flow.
- Camera and UI safe area on narrow/tall phones; animation uses logical state, not vice versa.

### Manual quality gates
- All 20 levels won at least once from a real Unity build/device; no softlocks or unreadable cones.
- On-device touch reliability; portrait orientation; audio toggle; app background/resume; no crash/console error.
- Human playtest: capture confusing rules, unintentional easy bypasses, frustration, tutorial usefulness; iterate without violating canonical reference unnoticed.
- No mock menus, broken links, license issues, copyrighted borrowed assets, debug overlays or test-only hardcoded levels in release build.

## 11. Level editor requirement
Make a custom `EditorWindow` to select/create/edit JSON definitions; paint floor/walls, place start/exit/key/door, place a guard, set behavior/path/facing/range/step/rotation/gadget charges, simulate a single turn, reset, and export. Include validation diagnostics. Optional Run Solver command can invoke a Python subprocess only with explicit supported environment and error handling; otherwise port BFS/validation to editor-side C# and show exact route. Editor must never overwrite original 20 validated levels without a backup and an intentional action.

## 12. Production priorities and definition of done
Prioritize in this order: correct simulation > content valid > controls and clarity > progression > modular toolchain > cohesive art > polish > devices/store. A milestone is done only when its declared acceptance tests pass. Final release is only done after device validation and store/compliance review. Maintain a backlog for future enemies/switches rather than silently expanding scope.
