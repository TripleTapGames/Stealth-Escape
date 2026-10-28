# Shadow Steps — Node-Based Level Design Parameters
Version 2.0 | Unity 2D orthographic | 2026-10-28

## 1. Non-negotiable movement model
- Logical board uses integer coordinates (x,y), origin top-left, x right, y down.
- Nodes appear at equally spaced world positions: `world(x,y) = boardOrigin + (x*nodeSpacing, -y*nodeSpacing, 0)`; default `nodeSpacing = 1 Unity unit`. **No diagonal or variable-length edges.**
- A character can travel **only** across a directed or bidirectional edge between cardinally adjacent existing nodes. One action traverses one edge; all player/enemy move animations are exactly `nodeSpacing` in distance and end centered on nodes.
- Edges and line of sight are independent: missing edge prevents movement, but does not necessarily obstruct vision. Physical wall/occluder nodes or segments can block vision.
- An invalid move does not consume a turn. WAIT and gadgets consume turns. Enemy movement and rotations resolve as discrete states; animations are cosmetic.
- The level validator must reject non-adjacent patrol waypoints, disconnected graph references, impossible starting positions, duplicate or dangling edges, and any enemy route segment without an allowed movement edge.

## 2. Authorable fields and recommended values
| Group | Parameter | Type/default | Notes |
|---|---|---|---|
| Identity | `id`, `name`, `world`, `chapter`, `theme` | integer/string | Unique id, localizable title and thematic set |
| Board | `width`, `height` | 4–9 each | Level size, not a difficulty score |
| Board | `nodeSpacing` | 1.0 world unit | GLOBAL per level, never unequal along a route |
| Board | `nodeMask` | boolean array | Existing node positions; omitted means full rectangular board except blocked wall nodes |
| Board | `edges` | adjacent pairs | Explicit allowed connections; omitted can be auto-generated for a legacy grid |
| Board | `edgeType` | open, locked, oneWay, switchable | Dynamic edge states after objectives/switches |
| Terrain | `walls` | coordinate array | Non-walkable, LoS-blocking objects |
| Terrain | `cover` | coordinate array | Specify sight-blocking but traversable/not traversable per type |
| Terrain | `decor` | cosmetic only | MUST NOT affect collisions/visibility |
| Objective | `start`, `exit` | coordinate | Both on reachable nodes |
| Objective | `key`, `door`, `switches` | optional objects | Triggers and locking rules explicit |
| Guards | `guards` | 0–6 | Configurable, deterministic archetypes |
| Guards | `movementMode` | fixed, rotate, pingPong, loop, moveRotate | Independent of art prefab |
| Guards | `route` | adjacent nodes list | Include authored node sequence |
| Guards | `facing`, `rotationStep` | N/E/S/W; -1/0/+1 | Determine facing after each guard turn |
| Guards | `moveEvery`, `rotateEvery` | positive integer, default 1 | Can differ for variants; state machine defined exactly |
| Vision | `coneAngleDegrees` | 90 default | 90° discrete cone for V1; keep reference solver parity |
| Vision | `visionRangeNodes` | 1–4 default | Fixed guard-dependent line of sight with wall occlusion |
| Vision | `visionBlockers` | walls, closedDoors, cover | Paths/edges do not intrinsically occlude |
| Gadgets | `smokeCharges`, `empCharges`, `decoyCharges` | integers >=0 | V1 smoke and EMP; decoy is future-only until simulator added |
| Gadgets | `smokeTurns` | 2 for V1 | Activation + next action, occupancy always lethal |
| Gadget | `empTurns` | 1 for V1 | Stops guard phase advancement only on use turn; existing cones still detect |
| Restrictions | `maxMoves`, `requiredGadget`, `noWait`, `noUndo` | optional constraints | Only claim 'required' after constrained solver check |
| Presentation | `skin`, `music`, `lighting`, `tutorialHint` | asset IDs | Do not alter deterministic board rules |
| Scoring | `parTurns`, `starThresholds` | solver-backed values | UI indicators, not a replacement for playtest |

## 3. Recommended difficulty measurements
Difficulty is not just board size or guard count. For each level, calculate and store:
- `shortestWinTurns` from BFS/A* search over **full logical state** (position, guard phase, key/door states, gadget counts, smoke, switches).
- `shortestWithoutWait`, `shortestWithoutSmoke`, `shortestWithoutEMP` or `unsolvable` under each banned action, for mechanic necessity.
- `safeNodeRatio`: fraction of accessible nodes outside guard sight at initial state; explanatory only.
- `guardCycleLCM`: combined guard schedule period (large values increase temporal planning).
- `branchingFactor`: typical count of valid player actions; too many choices may obscure intent.
- `backtracking`: shortest path revisits or returns to an earlier zone.
- `chokepoints`: graph bottlenecks and crossings under active vision.
- `uniqueness`: number of shortest winning action sequences (cap counting to avoid explosion).
- `cognitiveLoad`: qualitative designer score 1–5 for rules simultaneously in play.
- `humanPlaytestTurns` and `failedAttempts`: collected later, never invented.

### Difficulty budgets (targets, not guarantees)
| Tier | Nodes (approx.) | Guards | Mechanics at once | Shortest solution (target) |
|---|---:|---:|---:|---:|
| Tutorial | 12–25 | 1 | 1 | 4–9 actions |
| Easy | 16–30 | 1–2 | 1–2 | 6–12 |
| Medium | 20–42 | 2–3 | 2–3 | 9–20 |
| Hard | 25–55 | 3–4 | 3–4 | 14–28 |
| Expert | 30–65 | 4–6 | 4–5 | 20–40 |

## 4. Suggested first-five-level curriculum
1. **Behind the Watcher**: 5×5, one fixed guard, walls create two routes. Teach move and cone occlusion. No gadgets.
2. **Clockwise Window**: 5×5, one rotating guard. Teach WAIT and direction prediction; validate WAIT necessity before claiming it.
3. **Crossing Time**: 6×5, one horizontal ping-pong patrol. Require passing a specific edge at the safe guard phase.
4. **Locked Route**: 6×6, one stationary or rotating watcher, a key and a locked edge/door. Verify that acquiring the key is required.
5. **Cloud Corridor**: 7×6, fixed guard plus patrol, one Smoke charge, optional EMP reserved for later. Validate that the level cannot be won without Smoke if it is meant to teach Smoke.

These are *design briefs* and not new verified maps. Exact layouts must be created in editor, solved, and playtested. Do NOT treat illustrative AI-generated screenshots as valid node graphs.

## 5. Turn resolution — explicit order
1. Validate action (target exists, edge exists, is traversable, player has gadget charge). Invalid actions do nothing.
2. Save complete undo snapshot.
3. Apply player action (move exactly one node, wait, or gadget); resolve immediate pickups and edge unlocks.
4. Update deterministic guard phase (EMP skips phase advance on activation turn).
5. Calculate all guard positions/facing simultaneously from the same snapshot/phase; reject overlapping guards according to authored collision policy (default: reject level in validator rather than invent dynamic AI).
6. Check player/guard co-occupancy and edge swaps; fail if collided.
7. Evaluate each guard's cone against player node, applying wall/closed-door/cover occlusion and Smoke concealment; fail if detected.
8. Check mission objectives, then exit/win. Decrement timed effects as specified and capture final state.
9. Animate node-to-node movement; animation timing never changes outcome.

## 6. Sample data shape (illustrative, NOT validated puzzle)
```json
{
  "schemaVersion": 2,
  "id": 101,
  "name": "Training Junction",
  "board": {
    "width": 4, "height": 4, "nodeSpacing": 1.0,
    "nodes": [[0,0],[1,0],[2,0],[3,0],[0,1],[1,1],[2,1],[3,1],[0,2],[1,2],[2,2],[3,2],[0,3],[1,3],[2,3],[3,3]],
    "edges": "autoCardinalExceptBlocked",
    "blockedEdges": [ [[1,1],[2,1]] ],
    "walls": [[2,2]]
  },
  "player": { "start": [0,3] },
  "objective": { "type": "reachExit", "exit": [3,0] },
  "guards": [{
    "id": "g1", "movementMode": "rotate", "route": [[1,1]],
    "facing": "E", "rotationStep": 1, "rotateEvery": 1,
    "visionRangeNodes": 2, "coneAngleDegrees": 90
  }],
  "gadgets": { "smokeCharges": 0, "empCharges": 0 },
  "presentation": { "theme": "gardenCompound" }
}
```
For `edges=autoCardinalExceptBlocked`, generate connections **only** for neighboring existing and traversable nodes, excluding `blockedEdges` and closed locked edges. Do not create edges through wall nodes. Save the expanded canonical edge list for stable previews, solver use, and editor visualization.

## 7. Unity implementation and editor parameters
- Model: `LevelDefinition`, `NodeData`, `EdgeData`, `GuardDefinition`, `ObjectiveDefinition`, `GadgetInventory`, `LevelValidationResult`.
- Pure C# simulation: `LevelGraph`, `TurnSimulator`, `VisionService`, `GuardStateResolver`, `LevelSolver`, `LevelMetricsAnalyzer`.
- View: `BoardView`, `NodeView`, `EdgeView`, `CharacterView`, `VisionOverlay`, `GadgetFX`, `HUD`.
- Editor: `ShadowStepsLevelEditorWindow` (tile/node placement, toggle edge, direction arrows, patrol-route drawing, cone range setting, start/exit/key/door, smoke/EMP charges, validate/solve buttons, import/export JSON).
- Parameters configurable via Inspector and JSON, never hardcoded to specific level IDs.
- Validation pipeline: syntax -> graph adjacency -> route integrity -> initial safety -> solvability -> reference-solver parity -> mechanic necessity -> human playtest.
- Editor visualization toggle: node coordinates, neighbors, blocked edges, enemy route arrows, current/next cone, BFS optimal path, unreachable islands, currently invalid objects.

## 8. Legacy levels and compatibility (important)
Existing `reference/levels.json`, `reference/build.py`, and `reference/validation.json` use schema version 1 and rectangular tile-based movement. They do **not** explicitly store editable node-edge graph topology. Introduce a V1-to-V2 converter that generates cardinal edges between the old walkable adjacent tiles, preserving old wall blocking. Then re-run the old shortest solutions against the new graph simulator; do **not** claim compatibility until equivalence tests pass. To design levels with deliberately missing connections, export schema v2 and run solver again. Avoid silently changing the validated rules for older levels.

## 9. Claude Code instructions — implementation task
Implement this document in the existing Shadow Steps Unity project, with priority on the pure C# graph model and schema V2. Make node spacing configurable at the board level but uniform across each level. Add the edge/node Unity editor, deterministic turn system, validation errors with coordinates, live vision/path preview and solver metrics. Preserve legacy schema v1 through explicit migration; implement and run parity tests. Create five **new** authored levels following Section 4, validate each shortest solution, and prove any 'must use gadget/WAIT' mechanic using constrained solving. Do not invent results or mark untested levels validated. Update `CLAUDE.md`, `MASTER_GAME_SPEC.md`, and the milestone plan with the new graph movement contract after inspection.
