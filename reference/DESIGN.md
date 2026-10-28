# Shadow Steps — Unity Game Design & Validated Level Pack

**Version:** Prototype 1.0 | **Platform:** iOS / Android | **Engine:** Unity 2022.3 LTS or Unity 6 | **Orientation:** Portrait | **Genre:** Deterministic turn-based stealth puzzles.

## Vision and pillars

Original square-grid stealth puzzle with readable enemy cones, predictable enemy schedules, one-action turns, limited-use tactical gadgets, and small replayable levels. Do not copy the named inspiration's maps, art, character silhouettes, or UI. Game is designed for one developer with procedural board assembly and a compact reusable asset kit.

**Player loop:** Inspect guards and their next-turn ghost -> choose move, WAIT, SMOKE, or EMP -> resolve full turn -> collect key if needed -> reach green exit -> grade and unlock next stage.

**MVP scope:** 20 supplied verified levels, fixed/rotating/patrol/patrol+rotation/loop enemy configs, keys+doors, two gadgets, undo/restart, turn previews, a compact board editor. No combat, alert-state AI, push blocks, dynamic switching, or live-service systems in v1.

## Exact deterministic simulation contract (authoritative)

- Grid: `size=[width,height]`; `(0,0)` top-left, x increases right, y down. Four-way movement, each action costs one turn. No diagonal movement.
- Each action is `N/E/S/W/WAIT/SMOKE/EMP`. WAIT, SMOKE, EMP keep player in same tile. Moves into walls/out of board invalid and do not spend a turn.
- Sequence: (1) validate action; (2) move player/collect key and unlock door for this and later turns; (3) advance all guard schedules by one phase, **unless EMP**, which freezes the global enemy phase for that action only; (4) test enemy destination occupancy and edge-swaps; (5) test enemy cone vision; (6) win if player is at exit and key held when door exists.
- Guards are always hostile; moving onto a guard's post-turn tile is a capture. An edge swap with a guard is a capture. Guard route points can be inspected at turn start and in the ghost preview. Guards do not pathfind around the player.
- Cones: 90-degree discrete cone up to `range` tiles. A cell is in geometric cone if `forward>=1`, `forward<=range` and `abs(lateral)<=forward` in guard-facing coordinates. Vision blocked by wall and by a locked door using a sampled line-of-sight from tile center to tile center. **Use the reference solver for parity**; floating-boundary cases may need tests before shipping.
- If smoke is active, player is hidden even if standing in a cone (but never from guard occupancy). SMOKE action grants coverage on the deployment action and the **next player action**, then expires. Each level's `smoke` indicates starting charges. Smoke does not block a guard's view of other tiles.
- EMP action uses one charge and freezes all guard phase updates on that action only. It does not conceal the player; standing in current sight during EMP still loses. The MVP EMP is a gameplay abstraction, not a realistic electronics simulation.
- A locked door blocks player movement and vision until player reaches the key. In levels with key+door, obtaining the key is an explicit win requirement even if the exit can be reached without walking through the door.
- Guard rotation `facing` uses 0=N,1=E,2=S,3=W; `rotation` is +1 clockwise or -1 counterclockwise. For `rotate`, orientation changes every `step` turns. For `patrol_rotate`, it changes on the same update cadence as position. Patrol goes forward and back along authored `route`, loop wraps to its start. `fixed` stays at route[0].
- Guard phase is stored mod the least-common-multiple of individual guard cycles. Undo restores player position, guard phase, key, remaining gadget charges and smoke lifetime; no resimulation from animation state.
- All enemy states are resolved simultaneously from the same pre-turn snapshot. Animations do not modify game logic.

## Guard archetypes

| Type | Config | Design role |
|---|---|---|
| Static watcher | `fixed`, one route point | Permanent threat zone |
| Rotating watcher | `rotate`, one point | Timing puzzle |
| Straight patrol | `patrol`, straight route | Moving blocking lane |
| Rotating patrol | `patrol_rotate`, straight route | Two-layer timing puzzle |
| Loop sentry | `loop`, closed rectangle route | Recurring route pressure |
| Slow watcher | Any of above, `step=2` | Different enemy clock (future content) |
| Alternating watcher | Rotate with custom two-direction sequence | Narrow windows (future) |
| Alarm watcher | Scripted state changes after a switch | Reactive puzzle (future) |

## Difficulty progression and 20-level curriculum

Levels 1–4 teach basic fixed/rotating/patrol patterns. 5–8 introduce combinations and loops. 9–12 introduce key-lock and gadgets. 13–16 require managing multiple threats or environmental routes. 17–20 mix high-density cones, tools, and multi-enemy timing. Size starts 5x5 and increases to 7x7. `validation.json` gives reproducible shortest winning action sequences; progression is curated, but shortest move counts are not strictly increasing.

## Board notation

`P` player start, `X` exit, `#` wall, `.` normal tile, `K` key, `D` locked door, `A/B/C` guard starts. **Guard route and facing are defined in JSON**; the map is only the initial snapshot. Top row is y=0. Use JSON, not these diagrams, as the authoritative level source.

## Level layouts and validated shortest plans


### 01. First Watch — 5x5, 8 optimal turns

```text
# . A . X
. # . . .
. . . . .
. . . . .
P # # . .
```

**Guards:** A=fixed path=[[2, 0]] facing=N rotation=+1 range=1. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `N N E E N E N E`. Search visited 19 states; cycle length 1 turns.


### 02. Clockwork Eye — 5x5, 9 optimal turns

```text
# # . A X
# . . . .
. . . . .
# . # . .
P . . . .
```

**Guards:** A=rotate path=[[3, 0]] facing=W rotation=+1 range=1. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E N N E E E WAIT N N`. Search visited 66 states; cycle length 4 turns.


### 03. The Corridor — 5x5, 8 optimal turns

```text
. . # A X
. . # . .
. . # . .
. # . . .
P . . # .
```

**Guards:** A=patrol path=[[3, 0], [3, 1], [3, 2], [3, 3]] facing=N rotation=-1 range=1. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E E N E N N E N`. Search visited 87 states; cycle length 6 turns.


### 04. Double Rhythm — 5x5, 8 optimal turns

```text
# # . . X
. A # . .
. . # . .
. . . . .
P . . . #
```

**Guards:** A=patrol_rotate path=[[1, 1], [1, 2], [1, 3], [1, 4]] facing=E rotation=-1 range=1. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E E N E N N N E`. Search visited 67 states; cycle length 12 turns.


### 05. Two Watchers — 5x5, 11 optimal turns

```text
# # . . X
. . A . .
. . # # .
. # . . .
P . . B .
```

**Guards:** A=fixed path=[[2, 1]] facing=S rotation=+1 range=1; B=rotate path=[[3, 4]] facing=W rotation=+1 range=1. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `N S E WAIT E N E E N N N`. Search visited 45 states; cycle length 4 turns.


### 06. Crossing Paths — 5x5, 10 optimal turns

```text
. . . . X
A . . . .
. . # # .
. . # . .
P . . B .
```

**Guards:** A=patrol path=[[0, 1], [1, 1], [2, 1], [3, 1]] facing=W rotation=-1 range=2; B=rotate path=[[3, 4]] facing=W rotation=+1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `N N E S N N N E E E`. Search visited 77 states; cycle length 12 turns.


### 07. Square Route — 5x5, 10 optimal turns

```text
# # . A X
. . . . .
# . . . .
. . # # .
P . # . .
```

**Guards:** A=loop path=[[3, 0], [2, 0], [2, 1], [3, 1]] facing=N rotation=+1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `N S N E N N E E N E`. Search visited 43 states; cycle length 4 turns.


### 08. Side Step — 5x5, 8 optimal turns

```text
# . . . X
. . . . .
. . # . .
. . A # .
P . # # B
```

**Guards:** A=patrol_rotate path=[[2, 3], [1, 3], [0, 3]] facing=N rotation=-1 range=2; B=fixed path=[[4, 4]] facing=N rotation=+1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E N N N N E E E`. Search visited 42 states; cycle length 4 turns.


### 09. The Brass Key — 6x6, 15 optimal turns

```text
. . . . . X
. . . . B .
. . . . . .
. . . . A .
# . # . . D
P . K . . .
```

**Guards:** A=rotate path=[[4, 3]] facing=E rotation=+1 range=2; B=rotate path=[[4, 1]] facing=N rotation=+1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** Yes.

**One verified shortest solution:** `E N S E W N N N N N WAIT E E E E`. Search visited 128 states; cycle length 4 turns.


### 10. Locked Timing — 6x6, 13 optimal turns

```text
. . # . . X
. # . . . .
. # . A # .
. D . # . .
. . . . K .
P B . . # .
```

**Guards:** A=patrol path=[[3, 2], [3, 1], [3, 0]] facing=E rotation=-1 range=2; B=patrol path=[[1, 5], [1, 4], [1, 3]] facing=E rotation=-1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** Yes.

**One verified shortest solution:** `N N S E E E E WAIT N E N N N`. Search visited 106 states; cycle length 4 turns.


### 11. Smoke Passage — 6x6, 11 optimal turns

```text
. . . . . X
. . . . . #
. A . . . .
. . . . . .
. . . B . .
P . . . . #
```

**Guards:** A=patrol_rotate path=[[1, 2], [0, 2]] facing=W rotation=-1 range=2; B=rotate path=[[3, 4]] facing=W rotation=+1 range=2. **Loadout:** Smoke 1, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E N N SMOKE N N E N E E E`. Search visited 120 states; cycle length 4 turns.


### 12. Power Cut — 6x6, 10 optimal turns

```text
# . . . . X
. . # . . .
. . . . # .
. . . . . .
. A . . . .
P . . . B .
```

**Guards:** A=loop path=[[1, 4], [2, 4], [2, 5], [1, 5]] facing=E rotation=+1 range=2; B=fixed path=[[4, 5]] facing=N rotation=-1 range=2. **Loadout:** Smoke 0, EMP 1. **Key/door:** No.

**One verified shortest solution:** `N N N N E N E E E E`. Search visited 135 states; cycle length 4 turns.


### 13. Guarded Vault — 6x6, 12 optimal turns

```text
. . . . B X
# . . . . .
. . . . . .
. . K . . .
. . . # . D
P . C A . .
```

**Guards:** A=fixed path=[[3, 5]] facing=N rotation=-1 range=2; B=patrol path=[[4, 0], [4, 1], [4, 2], [4, 3]] facing=W rotation=+1 range=2; C=rotate path=[[2, 5]] facing=W rotation=-1 range=2. **Loadout:** Smoke 0, EMP 0. **Key/door:** Yes.

**One verified shortest solution:** `N N N E E S N E E E N N`. Search visited 198 states; cycle length 12 turns.


### 14. Smoke and Mirrors — 6x6, 11 optimal turns

```text
. . . . . X
. # . . . .
B . . . . .
. # . . . .
. A . . . .
P . . . . .
```

**Guards:** A=loop path=[[1, 4], [2, 4], [2, 5], [1, 5]] facing=N rotation=-1 range=3; B=rotate path=[[0, 2]] facing=E rotation=-1 range=3. **Loadout:** Smoke 1, EMP 0. **Key/door:** No.

**One verified shortest solution:** `N E E E SMOKE N N E N N E`. Search visited 308 states; cycle length 4 turns.


### 15. Blackout Window — 6x6, 12 optimal turns

```text
. . # . . X
. . # . . .
. . . . . .
# . . . . .
# . . . A B
P . . . . .
```

**Guards:** A=patrol_rotate path=[[4, 4], [5, 4]] facing=S rotation=+1 range=3; B=patrol path=[[5, 4], [5, 3], [5, 2], [5, 1]] facing=E rotation=-1 range=3. **Loadout:** Smoke 0, EMP 1. **Key/door:** No.

**One verified shortest solution:** `E N N N N S E E N N E E`. Search visited 283 states; cycle length 12 turns.


### 16. The Service Door — 6x6, 12 optimal turns

```text
. . D # # X
. . . . . .
. . . . # .
K C . . # B
. . # A # #
P . . . . #
```

**Guards:** A=fixed path=[[3, 4]] facing=S rotation=-1 range=3; B=rotate path=[[5, 3]] facing=W rotation=+1 range=3; C=loop path=[[1, 3], [0, 3], [0, 4], [1, 4]] facing=S rotation=+1 range=3. **Loadout:** Smoke 0, EMP 0. **Key/door:** Yes.

**One verified shortest solution:** `N E N W N N E E E E E N`. Search visited 138 states; cycle length 4 turns.


### 17. Three Blind Angles — 7x7, 16 optimal turns

```text
. B . . . . X
# . . . . . .
. . A . . . .
. . . C . . .
. . . . . # .
. . . # # # #
P . . # . . #
```

**Guards:** A=patrol_rotate path=[[2, 2], [1, 2], [0, 2]] facing=W rotation=-1 range=3; B=rotate path=[[1, 0]] facing=N rotation=+1 range=3; C=fixed path=[[3, 3]] facing=W rotation=+1 range=3. **Loadout:** Smoke 1, EMP 0. **Key/door:** No.

**One verified shortest solution:** `E E N WAIT SMOKE N E E WAIT WAIT N N E N N E`. Search visited 102 states; cycle length 4 turns.


### 18. Shift Change — 7x7, 14 optimal turns

```text
. . C A . . X
. . . . . . .
. . . . . . .
. . . . . . .
. . . . # # .
. . . . . . .
P # . . . . B
```

**Guards:** A=patrol path=[[3, 0], [2, 0], [1, 0], [0, 0]] facing=N rotation=-1 range=3; B=loop path=[[6, 6], [5, 6], [5, 5], [6, 5]] facing=S rotation=+1 range=3; C=rotate path=[[2, 0]] facing=E rotation=-1 range=3. **Loadout:** Smoke 0, EMP 1. **Key/door:** No.

**One verified shortest solution:** `N N E N S E E N E E E N N N`. Search visited 375 states; cycle length 12 turns.


### 19. High Security — 7x7, 25 optimal turns

```text
. # B . . . X
. . # . . . .
. C . # . A D
K . . . . . .
. . . . . # .
. # . . . . #
P . . . # . .
```

**Guards:** A=loop path=[[5, 2], [6, 2], [6, 3], [5, 3]] facing=S rotation=+1 range=3; B=patrol_rotate path=[[2, 0], [3, 0], [4, 0], [5, 0]] facing=E rotation=-1 range=3; C=fixed path=[[1, 2]] facing=W rotation=+1 range=3. **Loadout:** Smoke 1, EMP 0. **Key/door:** Yes.

**One verified shortest solution:** `N N E N E N WAIT S N S W WAIT SMOKE W E E E E S N N N E E N`. Search visited 1371 states; cycle length 12 turns.


### 20. Final Extraction — 7x7, 23 optimal turns

```text
. C . . . . X
# . . . . . .
. . . . . . .
. K D . . # .
. . . . . . A
. # . . . . .
P . . . . B .
```

**Guards:** A=patrol_rotate path=[[6, 4], [5, 4], [4, 4], [3, 4]] facing=S rotation=+1 range=3; B=loop path=[[5, 6], [4, 6], [4, 5], [5, 5]] facing=E rotation=+1 range=3; C=rotate path=[[1, 0]] facing=E rotation=+1 range=3. **Loadout:** Smoke 1, EMP 1. **Key/door:** Yes.

**One verified shortest solution:** `N N N N WAIT E N S W E S N W WAIT E E SMOKE E E E N N E`. Search visited 2314 states; cycle length 12 turns.


## Unity architecture (code-ready)

```
Assets/_ShadowSteps/
  Art/{Characters,Environment,UI,VFX}/
  Audio/{Music,SFX}/
  Prefabs/{Board,Player,Enemies,Tiles,UI}/
  Scenes/{Bootstrap,Menu,Gameplay,LevelEditor}/
  Scripts/
    Core/{GameBootstrap,TurnManager,TurnAction,GamePhase,GridCoord}.cs
    Domain/{BoardState,EnemyState,InventoryState,LevelDefinition}.cs
    Simulation/{TurnResolver,ConeVisibility,PatrolScheduler,CollisionRules}.cs
    Authoring/{LevelImporter,LevelValidator,LevelEditorController}.cs
    Presentation/{BoardView,EnemyView,VisionOverlay,TurnPreviewView}.cs
    UI/{HudController,LevelSelectController,ResultScreen}.cs
    Persistence/{ProgressStore,SettingsStore}.cs
    Tests/{TurnResolverTests,ConeVisibilityTests,LevelPackTests}.cs
  Resources/Levels/level_001.json ... level_020.json
```

**Domain:** `GridCoord` readonly struct (x,y). `LevelDefinition` serializable DTO for width, height, start/exit, walls, optional key/door, enemy authoring arrays and loadout. `BoardState` immutable turn snapshot (phase, player coord, inventory, smokeTTL, key flags, win/loss). `EnemyDefinition` immutable route/mode/facing/range/step/rotation.

**Simulation:** `TurnResolver.TryStep(BoardState, Action, LevelDefinition) -> TurnResult` pure C#; `PatrolScheduler.Evaluate(def, phase)` pure; `ConeVisibility.CanSee` matches Python reference. `TurnManager` owns history stack and emits `TurnCommitted`, `TurnFailed`, `LevelWon`. Views subscribe to results but never modify simulation. Resolve first, then tween sprites/models (0.12–0.2s), lock input during animation. Tap or swipe for directions and explicit gadget buttons.

**Data:** Keep level JSON in Resources for the MVP (20 tiny files). Later move to Addressables if level count scales. Import JSON into ScriptableObjects only as an editor pipeline; avoid two separately editable sources of truth. Use Unity Test Framework EditMode tests to replay every solution from validation.json; verify win and shortest via a debug BFS validator. Progress via versioned JSON stored in `Application.persistentDataPath` with stars, best moves, unlocked level.

**Visuals:** Portrait 6–8 tile board fitted into upper 65% of view; camera perspective orthographic or shallow isometric. Use a stylized toy-floor aesthetic, cones translucent with an opaque boundary, facing arrow on every guard, line route preview, and next-turn ghost overlays. Contrast must distinguish red hazard, blue EMP, pale smoke, key gold, and green exit. Provide reduced-motion and color-blind overlays (hatching/icons).

**UX:** Undo (unlimited), Restart, Wait, Smoke, EMP; predicted enemy motion/vision toggled on while selecting an action. Show gadget charge counts. First levels should have no modal tutorial; teach with short captions and one task per board. 3-star target: optimal, optimal+2, any completion; don't punish exploration by forcing consumable purchases.

## Verification and acceptance criteria

The included Python BFS explores all reachable states until it reaches an exit, so the returned solution is shortest **under the specific reference rule set above**. It checks walls, time-varying guard cones, guarded tile collisions, guard-player edge swaps, key collection, door entry, smoke duration, and EMP phase pausing. It does **not** establish aesthetic quality, uniqueness of solutions, optimality under different Unity rules, or that every named mechanic is necessary. For example, an EMP charge being available doesn't imply the shortest solution uses it.

Acceptance checks before content ships:
1. All 20 JSON entries have a winning replay in the Unity EditMode runner.
2. Any blocked move leaves phase and inventory unchanged.
3. Undo restores all gameplay state, including gadget charges and per-guard phase.
4. Test every one of the 4 facing directions against edge/corner cone cells, obstacles and doors.
5. Verify stationary guards, rotating guards, ping-pong endpoint reversal, and loop wraparound.
6. Verify collision on arrival, guard/player swaps, EMP pause, smoke on current + next action, key unlock on the same action.
7. Check hints never reveal an impossible move and that preview comes from the exact same resolver.
8. Verify at least one route through each level without using debug commands.

## Production milestones

- **M0:** Pure C# turn simulation + automated tests, no art.
- **M1:** Debug-grid game scene, movement, vision, four guard behaviors, replay JSON solutions.
- **M2:** Gadgets, key/door, undo, restart, 20-level level select.
- **M3:** Board and character art, polished feedback, sound, onboarding, accessibility.
- **M4:** Save, analytics (`level_start`, `level_fail`, `level_complete`, `undo`, `gadget_use`), device testing, build/store QA.

## Suggested Codex/Claude implementation prompt

> Implement Shadow Steps in Unity 2022.3 LTS using the attached `DESIGN.md`, `levels.json`, `validation.json` and `build.py`. Start with pure C# game simulation that matches the exact rules, not with art. Build one scene with an orthographic square-grid board, input for move/wait/smoke/EMP, guards and visible cones, an Undo stack, deterministic next-turn previews, and a level selector. Import all 20 JSON levels. Create EditMode tests that replay every solution in validation.json and assert the player wins, plus corner-case cone and collision tests. Preserve identical move order and smoke/EMP semantics. Organize domain and presentation logic separately. Do not add reactive AI or any unrequested mechanics. After tests pass, create reusable prefab-driven presentation and UI.
