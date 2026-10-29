# Shadow Steps — Rules and Level Generator Reference

Written 2026-10-29 for design discussion. Everything here describes what the code actually does today
(`Assets/_ShadowSteps/Scripts/Core`, `Tools/CoreTestRunner/Generator.cs`), not what a document wishes it did.
Where a rule is a deliberate design choice rather than a technical necessity it is marked **[design choice]**
so it can be questioned.

---

## 1. The game in one paragraph

Shadow Steps is a turn-based stealth puzzle on a small grid of nodes (5×5 to 8×7). The player moves one node per
action along explicit edges. Every valid action advances a deterministic clock; guards move and turn according to
that clock. Guards have a 90° vision cone of limited range. If the player ends an action in a node a guard can see,
or a guard steps onto the player, the level fails. The player can undo any number of actions. The goal is to reach
the exit node, collecting a key first if the level has a door. There is no randomness anywhere in play.

---

## 2. Board model

| Concept | Rule |
|---|---|
| Coordinates | Integer `(x, y)`, origin top-left, x right, y down. |
| Node | A standable position. Nodes are the cells of a `width × height` rectangle unless a node mask lists them explicitly. |
| Wall | A node that cannot be entered and blocks sight. |
| Cover | A node that blocks sight. `traversable: true` = low cover the player and guards can stand on; `false` = solid like a wall. |
| Edge | Directed link between two cardinally adjacent nodes. Movement is only possible along an edge. Types: `open`, `locked` (needs the key), and one-way (an edge whose reverse does not exist). |
| Auto edges | A level may say `"edges": "autoCardinalExceptBlocked"`; then every pair of adjacent traversable nodes gets a bidirectional open edge, minus `blockedEdges`, with `lockedEdges` and door edges locked. Generated levels always write the explicit edge list. |
| Edges and sight are independent | A missing edge stops movement but never blocks sight. Only wall, cover and (while the key is not held) the door node block sight. |
| Start / exit | Nodes; must differ; both must be traversable. |
| Key / door | Optional. The key is a node; picking it up is automatic when the player enters it. The door is a node whose incoming edges are locked and which blocks sight until the key is held. |
| Board size limit | 1–16 on each axis (validator). Design bands use 5–8. |

---

## 3. Player actions

Seven actions, always evaluated in this fixed order by the solver: `N, E, S, W, WAIT, SMOKE, EMP`.

### 3.1 Validation (an invalid action does nothing: no turn, no phase, no charge)

An action is rejected when:
1. the level is already won or lost;
2. it is `WAIT` and the level sets `restrictions.noWait`;
3. it is `SMOKE` / `EMP` with no charge left;
4. it is a move and the target has no node (off board / masked);
5. it is a move and the target is a wall or solid cover;
6. it is a move and there is no edge from the current node in that direction;
7. it is a move over a locked edge and the player does not hold the key. **The key counts as held if the target node is the key itself** (you can step through a locked edge onto the key).

### 3.2 What a valid action does (turn resolution, exact order)

1. **Player moves** (or stays for WAIT/SMOKE/EMP). Entering the key node sets `hasKey`.
2. **Clock advances:** `phase = (phase + 1) mod period`, **except for EMP, which leaves the phase unchanged.**
3. **Guards are placed** at the new phase (all guards simultaneously, from the schedule; see §4).
4. **Occupancy capture:** a guard's new position equals the player's node → caught (`Occupied`).
5. **Swap capture:** a guard's old position equals the player's new node and its new position equals the player's old node (they crossed on the same edge) → caught (`Swapped`).
6. **Smoke bookkeeping:** `SMOKE` sets `smokeRemaining = smokeTurns` (default 2); any other action decrements it toward 0. Charges are decremented for SMOKE/EMP.
7. **Sight capture** (`Seen`), only if not already caught and `smokeRemaining == 0`. See §5 for the exact rule.
8. **Win:** player on the exit node **and** (no door in the level **or** key held). Win is only checked if not caught.
9. Turn counter +1. Terminal states (won / caught) accept no more actions; undo is still allowed.

### 3.3 Gadgets

| Gadget | Effect | Notes |
|---|---|---|
| SMOKE | Player is invisible to cones for the activation action and the next action (`smokeTurns = 2`, configurable per level). | Occupancy and swap captures still apply. Using SMOKE again resets the timer. Does not consume a move; the clock still advances. |
| EMP | The clock does not advance for this action. | **Known problem (KNOWN_ISSUES #19):** the player also stays put, so after EMP the state is identical to before EMP minus one charge. EMP can never help; the solver proves it is never required on any level under either detection rule. Candidates for a real rule: guards frozen for this action *and* the next one, or guards blinded for one action. |
| WAIT | Clock advances, player stays. | The basic timing tool. `noWait` levels forbid it. |
| Decoy | Reserved in the schema, **not simulated**; the validator rejects `decoyCharges > 0`. | — |

### 3.4 Undo, restart, scoring

- Undo pops the last valid action; unlimited depth. `restrictions.noUndo` is stored but **not enforced** by the engine (UI-level only, currently unused).
- `maxMoves`, `requiredGadget` are stored for UI/analysis and **not enforced**.
- Stars: 3 if the win used ≤ par actions, 2 if ≤ par + 2, 1 for any win. Par = solver shortest path for the level, read from the validation files.
- Levels unlock sequentially in catalog order (tutorials 101–105 first, then 1–20).

---

## 4. Guards

### 4.1 Definition fields

| Field | Meaning |
|---|---|
| `movementMode` | `fixed`, `rotate`, `pingPong`, `moveRotate`, `loop` (see below). |
| `route` | List of nodes; `route[0]` is the start node. Consecutive nodes must be cardinally adjacent **and joined by an edge** (ping-pong needs both directions). Routes may not include the player start or the exit. |
| `facing` | Initial facing `N/E/S/W`. |
| `visionRangeNodes` | Cone length in nodes (1–4 in practice). |
| `coneAngleDegrees` | Always 90 (stored, not variable). |
| `moveEvery` | Position advances once every `moveEvery` phases. |
| `rotateEvery` | Facing turns once every `rotateEvery` phases (rotate / moveRotate only). |
| `rotationStep` | +1 = clockwise (N→E→S→W), −1 = counter-clockwise. |

### 4.2 Modes

| Mode | Position over time | Facing over time |
|---|---|---|
| `fixed` | `route[0]` forever | authored facing forever |
| `rotate` | `route[0]` forever | turns `rotationStep` every `rotateEvery` phases |
| `pingPong` | walks `route` forward then backward (0,1,…,n-1,n-2,…,1,0,…), one step per `moveEvery` phases | the direction it just travelled; at phase 0 (before any step) the authored facing |
| `loop` | walks `route` forward and wraps from the last node to the first (the route must form a closed ring) | direction of travel, as above |
| `moveRotate` | like `pingPong` | ignores travel; turns like `rotate` |

Exact schedule for phase `p`: `q = p / moveEvery` (integer division) selects the route index;
`r = p / rotateEvery` selects the rotation count. The facing of a travelling guard is computed from its position at
`p` minus its position at `p − moveEvery`; when that is not a cardinal step (ping-pong turnaround, or `q == 0`) the
authored facing is used. **Legacy quirk kept on purpose:** when the global phase wraps to 0 the travelling guard
shows its authored facing for that phase even if it just arrived from a step.

### 4.3 Clock and period

- One global phase counter for the whole level. Guard cycle lengths: fixed 1; rotate `4·rotateEvery`; pingPong `(2n−2)·moveEvery`; loop `n·moveEvery`; moveRotate lcm of its move and rotation cycles.
- `period = lcm` of all guard cycles; the phase is stored modulo `period`. Typical periods: 4, 6, 8, 12, 24.
- Guards ignore locks, doors and the key: their routes pass through door nodes freely (the validator only warns).
- Two guards must never stand on the same node at the same phase: **validation error** for schema 2 levels (warning for legacy files).

---

## 5. Vision and detection

### 5.1 Cone membership (discrete, exact)

For a guard at `(gx, gy)` facing `F` with range `R`, a node at offset `(dx, dy)` is inside the cone when

```
forward = component of (dx, dy) along F         (N: -dy, E: dx, S: dy, W: -dx)
side    = perpendicular component
1 <= forward <= R  and  |side| <= forward
```

So range 1 lights 3 nodes (ahead and the two diagonals), range 2 lights 3 + 5, range 3 lights 3 + 5 + 7.
Diagonal nodes with `|side| == forward` are lit by **two** adjacent facings; nodes straight ahead by one.

### 5.2 Line of sight

From the guard's node centre to the target's node centre, `12 × ChebyshevDistance` sample points are tested.
If any sampled node other than the two endpoints is an occluder (wall, cover of either kind, or the door node while
the key is not held), the target is hidden. Edges never occlude. This is the reference V1 formula and must not be
replaced by a physics raycast.

### 5.3 Detection rule — **the V2 change** [design choice]

Stored per level as `rules.detection`:

| Value | Rule |
|---|---|
| `afterGuardMove` (V1, what schema 1 files mean) | After the guards are placed at the new phase, if any cone sees the player's node → caught. |
| `beforeOrAfterGuardMove` (V2, default for schema 2, all shipped levels) | After the player's action, the player's **new** node is checked against the cones at the **old** phase (guards where they were when the player acted) **and** at the **new** phase. A sighting in either → caught. |

Consequences of the V2 rule:
- Stepping into a lit node is always lethal, even if the guard turns or walks away this turn. "Slip past as the guard turns away" (the whole V1 toolkit) is gone.
- To enter a node at phase `p` it must be dark at `p` **and** at `p+1` (unless smoke is active).
- A node straight ahead of a `rotateEvery 1` rotator is lit 1 phase in 4 → enterable 2 of 4 phases. A diagonal node is lit 2 phases in 4 → enterable 1 phase in 4. With `rotateEvery 2` every gap doubles, which is why most generated levels use slow rotators.
- "Guard vision" is the union of all guards: lit before by guard A and after by guard B still captures. The captor reported is the guard that sees the player after the move, else the one that saw it before.
- EMP keeps both looks identical (phase frozen). Smoke hides both looks.
- The V2 rule is never more lenient than V1 (tested over all reachable states of the 20 reference levels).
- Under this rule the original 20 V1 levels and 4 of 5 hand-made tutorials became unsolvable, which is why the content was regenerated.

### 5.4 Capture priority

Occupancy → swap → seen. Smoke suppresses only "seen". The failure screen distinguishes "Spotted" (seen) from "Caught" (occupied / swapped).

---

## 6. Solver and metrics (used by the generator, the tests and the par table)

- Breadth-first search over the full state `(player node, phase, hasKey, smokeCharges, empCharges, smokeRemaining)`, actions in the fixed order above, budget 250 000 states (never reached; real levels explore < 5 000). This matches the Python reference search exactly for V1 levels (same path, same explored-state count).
- Per level the analyzer records: shortest solution; shortest without WAIT / without smoke / without EMP / without any gadget (−1 = unsolvable, which **proves** the mechanic is required); `keyRequired` (door present, or exit unreachable over unlocked edges); period; `safeNodeRatio` (fraction of nodes neither lit nor occupied at phase 0); average branching factor; number of distinct shortest solutions (capped).
- `validation_v2.json` / `validation_tutorial_v2.json` store these per level together with the winning action list; tests replay them and re-run the solver to detect drift.
- Reachability map (`Tools/level_reach.sh`): exhaustive search printing every node the player can ever stand on and the "fringe" nodes where every entry attempt is a capture. This is how unsolvable levels are diagnosed.

---

## 7. The level generator

Location: `Tools/CoreTestRunner/Generator.cs`, run by `Tools/generate_levels.sh` (or `--generate <spec> <out>`).
Not shipped in the game. Deterministic: the same spec and seed always produce the same pack.

### 7.1 Spec format (`Tools/LevelSpecs/*.json`)

```json
{
  "seed": 20261028, "world": 1, "chapter": 1,
  "levels": [
    { "id": 8, "name": "Side Step", "width": 6, "height": 6,
      "start": [0, 5], "exit": [5, 0],          // optional; defaults bottom-left / top-right
      "walls": [3, 6], "cover": [0, 0], "oneWay": [1, 2],   // counts, [min, max]
      "guards": [
        { "mode": "pingPong", "range": 2, "routeLength": [3, 4], "moveEvery": 2 },
        { "mode": "rotate",   "range": 1, "rotateEvery": 2 }
      ],
      "smoke": 0, "emp": 0, "key": false, "door": true,
      "require": "wait",                        // or ["key","smoke"], "emp", "none"
      "moves": [9, 13], "maxSolutions": 3,
      "minSafeRatio": 0.0, "minReachRatio": 0.55, "guardsMatter": true,
      "tries": 3000, "stopAfter": 30,
      "hint": "shown once as onboarding text" },
    { "id": 1, "name": "First Watch", "copyFrom": "Assets/StreamingAssets/ShadowSteps/levels.json" }
  ]
}
```

Guard template fields: `mode`, `range` (value or `[min,max]`), `routeLength` (pingPong/moveRotate), `moveEvery`,
`rotateEvery`, `loopWidth`/`loopHeight` (loop routes are rectangular rings). Rotation direction and facing are
randomised unless the mode fixes them (pingPong/loop face their travel direction).

### 7.2 One try (candidate construction)

1. Full rectangular node set; start and exit reserved.
2. Random walls (count in range) on free nodes; random cover (count in range), each 50 % traversable.
3. Key (if requested): random free node at Manhattan distance ≥ 2 from start. Door: **one neighbour of the exit becomes the door and every other exit neighbour becomes a wall**, so the door is the exit's only approach (the key is then structurally required, not just by the win rule).
4. Guards, in template order, on free nodes: fixed/rotate on one node; pingPong/moveRotate on a straight line of `routeLength` free nodes in a random direction; loop on a free rectangular ring, random start node and direction. Guards never share nodes with walls, cover, key, door, start, exit or each other's routes.
5. Edges: auto-cardinal, door-incoming edges locked, then `oneWay` random open edges lose their reverse (never on guard routes).
6. Level built; the candidate moves on to the gates.

### 7.3 Acceptance gates (all measured with the real engine)

| Gate | Rejects when |
|---|---|
| Validator | any error (unreachable exit ignoring guards, guard overlap at any phase, illegal route, etc.) |
| Solvable | no solution |
| Par band | shortest solution outside `moves` |
| `require` | `wait`: solvable without WAIT; `smoke`/`emp`: solvable without it; `key`: key not structurally required; `none`: not solvable with WAIT and gadgets all forbidden |
| `guardsMatter` | the same board **without guards** has a shortest path at least as long (guards would be decoration) |
| `minReachRatio` | fewer than 55 % of traversable nodes are ever standable (dead space) |
| `maxSolutions` | more distinct shortest solutions than allowed |
| `minSafeRatio` | too few dark nodes at phase 0 |

### 7.4 Scoring (lower is better) and selection

```
score = 3·shortestSolutions + |par − (min + 0.6·(max − min))| + 0.5·branching − 0.05·period + 4·max(0, longestWaitRun − 2)
```

Up to `tries` candidates are built; the search stops after `stopAfter` acceptable ones; the best score wins.
Intent of each term: a single intended solution; par toward the upper half of the band; fewer trivial choices;
longer schedules slightly preferred (more temporal planning); three or more consecutive WAITs are dead time.

### 7.5 Output

Schema 2 JSON with explicit nodes/edges, `rules.detection` stated, generator provenance in `conversionNotes`
(seed, try index, number of acceptable candidates). The script then re-exports the validation traces. Tests assert
the measured mechanic sets, the par curve and the gadget placement, so any regeneration is a deliberate change.

### 7.6 Things the generator does **not** do

- No hand-crafted shapes: walls are random; readability of the board is judged by the reach map and by playing.
- No guarantee of *interesting* intermediate structure (chokepoints, backtracking) beyond what the gates imply.
- No EMP-required levels (impossible under the current EMP rule).
- No level masks (holes), no `moveRotate` templates in the current specs (the mode is supported).
- No tuning of hints or names; those come from the spec.

---

## 8. Current curriculum (measured, 2026-10-28)

| Id | Name | Board | Guards | Par | Forced | Teaches |
|---|---|---|---|---|---|---|
| 101 | Behind the Watcher | 5×5 | fixed | 8 | — | move, cone shape, walls occlude |
| 102 | Clockwise Window | 5×5 | rotate ×2 | 9 | WAIT | lit-now-or-next, waiting out a slow rotator |
| 103 | Crossing Time | 6×5 | pingPong | 10 | WAIT | crossing a patrol lane behind its back |
| 104 | Locked Route | 6×6 | rotate ×2, key/door | 11 | WAIT, key | key first, door occludes |
| 105 | Cloud Corridor | 7×6 | fixed, pingPong, smoke | 12 | smoke | smoke timing, occupancy still kills |
| 1 | First Watch | 5×5 | fixed | 8 | — | kept legacy |
| 2 | Clockwork Eye | 5×5 | rotate ×2 | 10 | WAIT | |
| 3 | The Corridor | 5×5 | pingPong | 9 | WAIT | |
| 4 | Double Rhythm | 5×5 | moveRotate | 8 | — | kept legacy |
| 5 | Two Watchers | 5×5 | fixed, rotate ×2, cover | 10 | — | cover |
| 6 | Crossing Paths | 6×5 | 2 pingPong (one `moveEvery 2`) | 11 | WAIT | two speeds |
| 7 | Square Route | 6×6 | loop, fixed | 11 | WAIT | loop routes |
| 8 | Side Step | 6×6 | pingPong ×2 speed, rotate ×2, one-way | 12 | WAIT | one-way edges |
| 9 | The Brass Key | 6×6 | fixed, rotate ×2, key/door | 12 | key | |
| 10 | Locked Timing | 6×6 | 2 pingPong, key/door | 12 | WAIT, key | |
| 11 | Smoke Passage | 6×6 | fixed r3, rotate ×2, smoke | 11 | smoke | |
| 12 | Power Cut | 6×6 | rotate ×1, rotate ×2, fixed, EMP | 12 | WAIT | EMP optional |
| 13 | Guarded Vault | 7×6 | fixed, rotate ×2, pingPong, key/door | 13 | key | |
| 14 | Smoke and Mirrors | 7×6 | fixed r3, pingPong, rotate ×2, cover, smoke | 12 | smoke | |
| 15 | Blackout Window | 7×7 | rotate ×1, rotate ×2, pingPong, EMP | 15 | WAIT | |
| 16 | The Service Door | 7×7 | fixed r3, pingPong r2, rotate ×2, key/door, smoke | 15 | WAIT, smoke, key | |
| 17 | Three Blind Angles | 7×7 | 2 rotate ×2, rotate r3 ×1, cover | 16 | WAIT | |
| 18 | Shift Change | 7×7 | pingPong, pingPong ×2 speed, loop | 14 | WAIT | |
| 19 | High Security | 8×7 | fixed r3, rotate ×2, 2 pingPong, key/door, smoke | 20 | smoke, key | |
| 20 | Final Extraction | 8×7 | rotate ×2, rotate r3 ×1, pingPong, loop, cover, one-way, key/door, smoke, EMP | 21 | smoke, key | finale |

"rotate ×2" = `rotateEvery 2`; "r3" = range 3. Tier par averages: 8.75 → 11 → 11.75 → 13.75 → 17.75.
Design bands from `LEVEL_DESIGN_PARAMETERS.md`: Tutorial 4–9, Easy 6–12, Medium 9–20, Hard 14–28.

---

## 9. Open questions worth debating

1. **EMP has no effect** (§3.3). What should it do? Freeze guards through the next action? Blind them for one action? Something else that creates a dark gap?
2. **Before-or-after detection** removes the "move as the guard turns" trick entirely. Is that the intended feel, or should the before-look only count for guards that do not move/turn this phase, or for the player's *origin* node rather than the destination?
3. **Union vs per-guard**: should being seen before by guard A and after by guard B count as a capture (current), or must the same guard see you both times?
4. **Tutorial pars 8–12** exceed the 4–9 band. Shorten boards, or accept longer tutorials because the rule needs idle turns?
5. **Opening WAITs**: several levels start with one or two WAITs because the guard's initial pose must pass. Should the generator forbid WAIT as the first action, or shift the initial phase instead?
6. **Unique shortest solutions** on every generated level: good for readable intent, possibly rigid. Allow 2–3?
7. **Door placement**: always the exit's only approach. Alternatives: door on a corridor chokepoint between zones (needs a chokepoint detector in the generator).
8. **Overlay**: the HUD shows cones at the current phase only; under the V2 rule the lethal set is current ∪ next cones (`RulesEngine.GetDangerCells` exists, unused). How should that be visualised?
9. **Guard overlap** is a hard error. Should guards be allowed to share nodes (e.g., stacked at a crossing) with some rule?
10. **Cover**: traversable cover blocks sight but can be stood on by player and guards. Should standing on low cover hide the player, or should guards not be able to enter it?

---

## 10. File map

| Purpose | Path |
|---|---|
| Rules engine | `Assets/_ShadowSteps/Scripts/Core/RulesEngine.cs`, `VisionSystem.cs`, `GuardSchedule.cs`, `DetectionRule.cs` |
| Level data model | `Core/LevelDefinition.cs`, `Core/LevelGraph.cs`, `Core/GuardDefinition.cs` |
| Validator / solver / metrics | `Core/LevelValidator.cs`, `Core/Solver.cs`, `Core/LevelMetrics.cs` |
| JSON (schema 1 + 2) | `Assets/_ShadowSteps/Scripts/Data/LevelRepository.cs` |
| Generator and tools | `Tools/CoreTestRunner/Generator.cs`, `Runner.cs` (`--generate`, `--reach`, `--metrics`, `--solve`, `--export-validation`, `--migrate`, `--search`), `Tools/generate_levels.sh`, `Tools/level_reach.sh`, `Tools/level_metrics.sh` |
| Specs | `Tools/LevelSpecs/tutorial_v2.json`, `Tools/LevelSpecs/world1_v2.json` |
| Shipped content | `Assets/StreamingAssets/ShadowSteps/levels.json`, `levels_tutorial_v2.json`, `validation_v2.json`, `validation_tutorial_v2.json` |
| Design docs | `LEVEL_DESIGN_PARAMETERS.md`, `V2_CHANGE_CONTROL.md`, `Docs/LEVEL_CURRICULUM.md`, `Docs/DECISIONS.md` (D-025 detection, D-026 generator), `Docs/KNOWN_ISSUES.md` (#17 overlay, #19 EMP) |
