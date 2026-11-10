# Shadow Steps — Level Design Handover (for ChatGPT)

You are designing and modifying levels for **Shadow Steps**, a deterministic, turn-based stealth puzzle for portrait phones
(Unity). This document is self-contained: it has the exact rules the engine enforces, the JSON format a level is written in,
the validation rules, the current 30 levels, and the workflow. You cannot run the game or the solver; the developer runs
them and pastes results back. Design levels as JSON objects exactly in the format below.

## 1. How the collaboration works

1. You propose a level (or a change to one) as a complete JSON object (section 4) plus a short design note: intended route,
   intended trick, expected par, which gadgets must be used.
2. The developer validates it with the C# solver (section 7). The solver is the authority: it reports the par (shortest
   win), whether a win exists without Smoke / EMP / WAIT, whether an alarm-free (Ghost) route exists, and any rule errors.
3. If the solver disagrees with your intent (level unsolvable, trick skippable, par off), revise and resend.
4. Never hand-write `scoring`; it is exported from the solver. Never claim a level is solvable — only the solver can.

## 2. Board, coordinates, notation

- Grid of cells, **origin top-left, x to the right, y down**. Coordinates are `[x, y]`, 0-based.
- `rows` is an array of strings, one per row (y), one character per cell (x). Characters:
  - `.` floor node (walkable; nodes connect to their 4 orthogonal floor neighbours unless a `cut` removes the link).
  - `#` structural wall: not walkable, blocks all vision, can host a camera. Every Main level has a full ring of `#`
    around the playable interior (the courtyard art paints the boundary there, which only works when the ring is *all*
    walls); the first FTUE levels omit it, but new levels should keep the ring.
  - `X` solid cover: not walkable, blocks all vision, **cannot** host a camera. Drawn as a bush planter or stone wall.
- Size limits: 1–16 per axis; the comfortable, standard size is **7 wide × 5 tall** including the wall ring (so a 5×3
  playable interior) — up to 7×6 is used. Wider-than-tall boards are rotated 90° on the phone; that is automatic and
  never affects the rules, so design in logical coordinates only.
- Directions: `N` = y−1 (up), `E` = x+1 (right), `S` = y+1 (down), `W` = x−1 (left).

## 3. Exact rules

### 3.1 Actions
- **Move** N/E/S/W to an adjacent floor node along an existing (not cut) connection. Counted action.
- **WAIT**: stay in place. Counted action. **Limited per level by `waitLimit`** (section 3.9).
- **Smoke** `SMOKE@x,y` and **EMP** `EMP#i`: free (not counted), see 3.6 / 3.7.
- `T` = number of counted actions taken. Guards and cameras are pure functions of `T` (deterministic, no randomness).

### 3.2 Turn resolution for one counted action (in this order)
1. Validate the action (walkable, connected; a locked door needs the key; WAIT needs a wait left).
2. The player moves and collects a key on the destination node. With the key, every door touching that node opens
   immediately for this attempt, before vision checks. The key is reusable; unlocking adds no action.
3. **Old-state check** with guards at their current poses: if a guard stands on the destination node → caught
   ("collision"); if a guard sees the destination node → caught ("seen before moving").
4. Old-state camera check: a camera that sees the destination triggers the alarm (never stops the move).
5. Guards and cameras advance to `T+1`.
6. **New-state check**: guard now on the destination → caught; guard and player swapped nodes → caught; guard now
   sees the destination → caught. Cameras rechecked (alarm).
7. `T` becomes `T+1`; an active alarm countdown loses 1 (not on the action that triggered it).
8. If the destination is the exit → **win** (checked after the enemy update, before gadget expiry).
9. Smoke clouds and EMP effects lose one action; anything that just expired is rechecked immediately: a guard that now
   sees the player catches them; a camera that now sees the player triggers the alarm.
10. If the alarm countdown reached 0 on a node that is not the exit → **lockdown** (loss).

A capture ends the attempt immediately (the failed action is still shown). Retry restarts from the authored state.

### 3.3 Guards
- Kinds: `static` (holds node and facing), `rotating` (turns 90° every counted action; `clockwise` true/false),
  `pingPong` (walks its `route` to the end and back; a straight route of n nodes has period 2n−2), `loop` (walks its
  route and jumps from the last node back to the first; period n). `offset` shifts a moving guard's schedule.
- A moving guard faces the direction it just travelled; at a ping-pong endpoint it still faces the endpoint.
- Routes must be adjacent floor nodes with no cut and **no door** between steps; a route never visits a node twice.
- **Guard vision**: exactly the **two nodes straight ahead** in its facing, stopping at the first wall, cover, board
  edge, closed door, or smoke cloud (that node is not seen). A guard standing in smoke sees nothing. Guards never see
  diagonally or sideways.
- Two guards may never occupy the same node at the same `T` (swapping places is allowed).

### 3.4 Cameras
- Mounted on a `#` cell (`wall`), looking onto a floor node through `mountFace` (the wall's face toward the floor).
  `facing` is the initial lens direction. Kinds: `fixed`; `rotating` (turns 90° every counted action, `clockwise`);
  `sweeping` (pans back and forth from `facing` in the clockwise/counter-clockwise direction, holding each facing for exactly
  2 actions). Optional `"sweep": 2` (default: facing and its neighbour, T = 0,1 → facing; 2,3 → other; 4,5 → facing …, period 4)
  or `"sweep": 3` (a half turn out and back, e.g. facing W clockwise: W W N N E E N N, period 8).
- **Vision = a 70° cone** (35° either side of the facing), **origin at the wall face** (half a cell from the wall
  centre toward `mountFace`), **forward reach 2 node spacings**. A node is seen when its centre is inside the cone,
  its forward distance is > 0 and ≤ 2, and the straight line from the origin to it crosses no wall, cover, closed door
  or smoke cloud (touching a corner counts as blocked). On open floor, a camera looking into the room sees a **T**:
  the node directly in front of the lens plus the three nodes of the next row. The two nodes beside the lens node are
  NOT seen (63°). A camera facing along its own wall sees the two nodes along the wall. A camera facing into its wall
  sees nothing.
- Detection does not capture: it **triggers the alarm** (3.8). An EMP-disabled camera sees nothing (3.7).

### 3.5 Doors, key, cuts
- `cuts`: pairs of adjacent nodes whose connection is removed (a gap in the path graph, not a wall; it does not block
  vision).
- `doors`: a locked connection between two adjacent floor nodes. Needs the level `key` (one key node; any number of
  doors). Reaching either endpoint with the key opens that door for the rest of the attempt, including when the key is picked up there. **Closed doors block vision** (guards and
  cameras); open doors do not. Guards never pass doors.

### 3.6 Smoke (`gadgets.smoke` charges)
- Free action. Target: any floor node within the 3×3 around the player (including the player's node) with a clear
  straight line from the player (no wall/cover/closed door between), not already smoked.
- The cloud hides its node from guards and cameras and blocks their lines of sight through it, for **2 counted
  actions**; when it expires the state is rechecked at once (3.2 step 9).

### 3.7 EMP (`gadgets.emp` charges)
- Free action on a chosen camera (`EMP#index`). The camera is blind for **2 counted actions** but keeps rotating /
  sweeping; when the EMP expires it rechecks immediately. Cannot stack on an already disabled camera.

### 3.8 Alarm, lockdown, Ghost
- First camera detection starts a countdown of `alarmBudget` counted actions. Each further Move/WAIT costs 1. Reaching
  the exit on the last countdown action still wins; reaching 0 anywhere else is **lockdown** (loss). Repeated
  detections do **not** refill the budget. The alarm never refills.
- **Ghost** (bonus) is offered on a level only if it has at least one camera **and** an alarm-free winning route
  exists; a player earns it by winning without ever triggering the alarm. A level where every win triggers the alarm
  is "forced alarm" (no Ghost).

### 3.9 Wait limit
- `waitLimit` = maximum WAIT actions per attempt (moves and gadgets don't count). Current policy: the smallest number of
  WAITs a par route needs plus 2, minimum 3 (so par is always reachable with slack). Set it deliberately; too low can
  make a level unsolvable.

### 3.10 Scoring
- `par` = solver's shortest win in counted actions. 3★ ≤ par + max(1, min(3, par/10)); 2★ ≤ par + max(2, ceil(0.3·par));
  otherwise 1★. Guidance: keep par ≤ 20; avoid routes that need more than 2 consecutive WAITs.

## 4. Level JSON format

Every level is one object in `Assets/StreamingAssets/ShadowSteps/levels_v4.json` (`levels` array). Fields:

| Field | Type | Notes |
|---|---|---|
| `id` | int | unique, positive; progression order |
| `code` | string | unique, e.g. `LEVEL-26` (FTUE levels use `FTUE-n`) |
| `group` | string | `FTUE` or `Main` |
| `title`, `intro` | string | shown in the HUD / level card; one sentence intro |
| `rows` | string[] | the board (section 2) |
| `start`, `exit` | [x,y] | floor nodes, different; start must not be seen by any guard or camera at T=0 and no guard may stand on it |
| `key` | [x,y] or null | required when `doors` is non-empty |
| `cuts` | [[x,y],[x,y]][] | removed connections between adjacent nodes |
| `doors` | {a:[x,y], b:[x,y]}[] | locked connections (adjacent floor nodes, not cut, no duplicates, max 30) |
| `guards` | object[] | `{id, kind, route:[[x,y],…], facing, clockwise?, offset?}` — static/rotating use `route[0]` as the node |
| `cameras` | object[] | `{id, wall:[x,y], mountFace, facing, kind, clockwise?}` — `wall` must be `#`, `mountFace` must look onto a floor node |
| `gadgets` | object | `{smoke: n, emp: n}` (EMP without a camera is pointless) |
| `alarmBudget` | int ≥ 1 | actions allowed after the first detection |
| `waitLimit` | int ≥ 0 | WAITs per attempt (omit = unlimited; every shipped level sets one) |
| `scoring` | object | **solver output, do not author**: `{par, threeStar, twoStar, ghostOffered}` |
| `notes` | string[] | free text; record design intent and changes with a date |

Example (LEVEL-19, a shipped level):

```json
{
  "id": 24,
  "code": "LEVEL-19",
  "group": "Main",
  "title": "Sweep Window",
  "intro": "Sweeping cameras hold each direction for two actions.",
  "rows": [
    "#######",
    "#...X.#",
    "#.....#",
    "#X....#",
    "#######"
  ],
  "start": [
    1,
    1
  ],
  "exit": [
    5,
    3
  ],
  "key": null,
  "cuts": [],
  "doors": [],
  "guards": [],
  "cameras": [
    {
      "id": "cam1",
      "wall": [
        3,
        0
      ],
      "mountFace": "S",
      "facing": "S",
      "kind": "sweeping",
      "clockwise": false
    }
  ],
  "gadgets": {
    "smoke": 0,
    "emp": 0
  },
  "alarmBudget": 2,
  "waitLimit": 3,
  "scoring": {
    "par": 7,
    "threeStar": 8,
    "twoStar": 10,
    "ghostOffered": true
  },
  "notes": [
    "camera cam1 mounting node (3, 0) was floor in the browser export; converted to wall",
    "imported from browser playtest export (Level 19, 2 blocked cover nodes)",
    "redesigned 2026-11-03 for the front-node camera footprint (start (1,1), cover at (4,1),(1,3)); browser par/ghost no longer apply"
  ]
}
```

## 5. Validation rules (what the solver/validator rejects or warns about)

Errors (level rejected): start/exit/key not floor; start == exit; a cell both `#` and `X`; cut between non-adjacent
nodes; door not between adjacent floor nodes, on a cut, or duplicated; doors without a key; `alarmBudget` < 1; negative
charges; camera not on `#` or its `mountFace` not looking onto floor; guard route through non-floor, non-adjacent, cut
or door connections, revisiting a node, or a moving guard with < 2 route nodes; two guards starting on the same node or
sharing a node at any `T`; a guard on the start node or seeing it at T=0; a camera seeing the start at T=0; no winning
route with the given loadout (also covers an impossible `waitLimit`).

Warnings (review): width > 7; EMP charges with no camera; stationary guard with extra route nodes; guard starting on the
exit; stored scoring differs from the solver; par > 20; more than 2 consecutive WAITs in the shortest solution; a
straight ping-pong route that stops short of the wall.

## 6. Presentation constraints (what the art can show)

- The boundary ring of `#` is painted as the courtyard walls and is not drawn separately; interior `#` runs are drawn
  as stone walls, lone interior `#` cells and all `X` cells as bush planters / stone pieces. Prefer `X` for cover inside
  the room; use interior `#` for corridor walls.
- Cameras are drawn standing on the wall face they look through (or on a post when their wall cell is a lone interior
  `#`). Their 70° amber cone is drawn exactly as detected; guards show a red fan over the two nodes they see.
- The exit is a highlighted node (no door sprite). Nodes are dark discs on white paths over the courtyard floor.
- Characters: one spy (player) and blue guards. No other enemy types exist.

## 7. Workflow for the developer (what happens with your JSON)

```
# syntax + rule validation + metrics for every level
Tools/run_core_tests.sh
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll --v3-validate Assets/StreamingAssets/ShadowSteps/levels_v4.json
# full export (par, gadget/wait requirements, Ghost) — scoring is copied from this into levels_v4.json
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll --v3-export-validation Assets/StreamingAssets/ShadowSteps/levels_v4.json /tmp/validation.json
# trace one level's solution
dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll --v3-trace Assets/StreamingAssets/ShadowSteps/levels_v4.json LEVEL-19
```

The export reports per level: `par`, the solution tokens (`N E S W WAIT SMOKE@x,y EMP#i`), `shortest_without_smoke` /
`_emp` / `_gadgets` / `_wait` (−1 = impossible → that gadget/WAIT is mandatory), `shortest_alarm_free`,
`ghost_offered`, `forced_alarm`, `waits_in_par`, `min_waits_for_par`. Ask for these numbers when you need them.

## 8. Current levels (solver results under the current rules)

| Code | Title | Size | Start→Exit | Guards (kind facing@anchor) | Cameras (kind facing, face@wall) | Door/Key | X | Smoke/EMP | Alarm | Waits | Par | Ghost | Mandatory |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FTUE-1 | First Steps | 4x1 | (0, 0)→(3, 0) | — | — | — | 0 | 0/0 | 4 | 3 | 3 | n/a | — |
| FTUE-2 | Follow the Lines | 3x3 | (0, 0)→(0, 2) | — | — | — | 0 | 0/0 | 4 | 3 | 6 | n/a | — |
| FTUE-3 | Stay Out of Sight | 5x5 | (0, 2)→(4, 2) | static S@(2, 0) | — | — | 1 | 0/0 | 4 | 3 | 8 | n/a | — |
| FTUE-4 | Watch the Rotation | 5x5 | (0, 2)→(4, 2) | rotating E@(2, 1) | — | — | 1 | 0/0 | 4 | 3 | 5 | n/a | — |
| FTUE-5 | Moving Patrol | 5x5 | (0, 2)→(4, 2) | pingPong N@(2, 0) | — | — | 1 | 0/0 | 4 | 3 | 6 | n/a | — |
| LEVEL-1 | Safe Side | 6x4 | (0, 2)→(5, 2) | static S@(2, 1) | — | — | 2 | 0/0 | 4 | 3 | 9 | n/a | — |
| LEVEL-2 | Clockwise | 6x4 | (0, 2)→(5, 2) | rotating E@(3, 1) | — | — | 2 | 0/0 | 4 | 3 | 7 | n/a | — |
| LEVEL-3 | One Step Ahead | 6x4 | (0, 2)→(5, 2) | pingPong N@(3, 0) | — | — | 3 | 0/0 | 4 | 3 | 11 | n/a | — |
| LEVEL-4 | Double Trouble | 7x5 | (0, 2)→(6, 2) | rotating E@(2, 1), static N@(4, 4) | — | — | 3 | 0/0 | 4 | 4 | 14 | n/a | — |
| LEVEL-5 | Corner Patrol | 7x5 | (0, 2)→(6, 2) | loop N@(3, 1) | — | — | 2 | 0/0 | 4 | 3 | 8 | n/a | — |
| LEVEL-6 | Narrow Crossing | 7x5 | (0, 2)→(6, 2) | pingPong N@(2, 0), pingPong N@(4, 4) | — | — | 3 | 0/0 | 4 | 3 | 13 | n/a | — |
| LEVEL-7 | Crossroads | 7x5 | (0, 2)→(6, 2) | rotating E@(3, 1), static N@(3, 4) | — | — | 3 | 0/0 | 4 | 4 | 14 | n/a | — |
| LEVEL-8 | Predict the Return | 7x5 | (0, 2)→(6, 2) | pingPong N@(2, 0), rotating W@(4, 1) | — | — | 2 | 0/0 | 4 | 3 | 10 | n/a | — |
| LEVEL-9 | Around the Loop | 7x5 | (0, 2)→(6, 2) | loop N@(3, 0), rotating N@(5, 3) | — | — | 3 | 0/0 | 4 | 3 | 10 | n/a | — |
| LEVEL-10 | Patrol Junction | 7x5 | (0, 2)→(6, 2) | pingPong N@(0, 1), pingPong N@(4, 4) | — | — | 3 | 0/0 | 4 | 3 | 13 | n/a | — |
| LEVEL-11 | Find the Key | 7x5 | (0, 2)→(6, 4) | — | — | 1, key (2, 2) | 3 | 0/0 | 4 | 3 | 12 | n/a | — |
| LEVEL-12 | Key Detour | 7x5 | (0, 2)→(6, 4) | — | — | 1, key (2, 4) | 2 | 0/0 | 4 | 3 | 14 | n/a | — |
| LEVEL-13 | Door as Cover | 7x5 | (0, 2)→(6, 2) | rotating W@(5, 1) | — | 1, key (2, 2) | 2 | 0/0 | 4 | 3 | 10 | n/a | — |
| LEVEL-14 | Unlock at the Right Time | 7x5 | (0, 2)→(6, 2) | rotating N@(5, 2) | — | 1, key (2, 2) | 3 | 0/0 | 4 | 3 | 12 | n/a | — |
| LEVEL-15 | Protected Corridor | 7x5 | (0, 2)→(6, 2) | pingPong N@(5, 0) | — | 1, key (2, 2) | 2 | 0/0 | 4 | 3 | 8 | n/a | — |
| LEVEL-16 | Camera Basics | 7x5 | (1, 3)→(5, 3) | — | fixed S, S@(3, 0) | — | 2 | 0/0 | 5 | 3 | 8 | no | — |
| LEVEL-17 | Race the Alarm | 7x5 | (1, 3)→(5, 3) | — | fixed W, W@(6, 2) | — | 2 | 0/0 | 3 | 3 | 8 | no | — |
| LEVEL-18 | Rotating Security | 7x5 | (1, 3)→(5, 3) | — | rotating S, S@(1, 0) | — | 2 | 0/0 | 3 | 3 | 8 | yes | — |
| LEVEL-19 | Sweep Window | 7x7 (3x3 long-edge) | (1, 1)→(5, 5) | rotating S@(3, 5) | sweeping E ccw, sweep 3 (E,N,W), N@(3, 6) | — | 2 | 0/0 | 6 | 7 | 8 | yes | D-074 redesign 2026-11-10 |
| LEVEL-20 | Risky Shortcut | 7x6 | (1, 4)→(5, 4) | — | fixed W, W@(6, 3) | — | 2 | 0/0 | 2 | 3 | 6 | no | — |
| LEVEL-21 | Smoke Screen | 7x4 | (0, 2)→(6, 2) | static S@(3, 1) | — | — | 0 | 1/0 | 4 | 3 | 6 | n/a | Smoke |
| LEVEL-22 | Cloud Timing | 7x5 | (0, 2)→(6, 2) | pingPong N@(3, 0) | — | — | 2 | 1/0 | 4 | 3 | 10 | n/a | — |
| LEVEL-23 | EMP Training | 7x4 | (0, 2)→(5, 2) | — | fixed W, S@(4, 1) | — | 0 | 0/1 | 2 | 3 | 5 | no | EMP |
| LEVEL-24 | Security Gap | 7x5 | (1, 3)→(5, 3) | — | sweeping W, W@(6, 2) | — | 2 | 0/1 | 1 | 3 | 8 | yes | — |
| LEVEL-25 | Final Extraction | 7x4 | (0, 2)→(6, 2) | static S@(3, 1) | fixed W, S@(4, 1) | 1, key (1, 2) | 0 | 1/1 | 1 | 3 | 6 | yes | Smoke, EMP |

**LEVEL-23 correction:**
- LEVEL-23 fixed on 2026-11-03: `alarmBudget` 1 → 2. Solver and replay confirm `E EMP#0 E E E E` wins in five counted actions; EMP remains mandatory.

**Pending decisions the developer still has to make (do not design around these until resolved):**
- LEVEL-24 no longer forces EMP under the cone (proposed: make its camera `fixed`, or move it to wall `[6,1]`).
- LEVEL-18's stored par is 9 but the solver says 8 (accept 8).
See `Docs/Reports/CAMERA_CONE_VALIDATION_2026-11-03.md` for the measurements.

## 9. Design guidance

- Teach one idea per level; name the level after the idea. FTUE 1–5 introduce movement, walls, static sight,
  rotation, patrol. Main 1–10 combine guards, 11–15 keys and doors, 16–20 cameras and the alarm, 21–25 gadgets.
- Difficulty levers: guard period alignment (rotating = 4, ping-pong = 2n−2, loop = n; the level period is their
  lcm), cover that cuts sight lines, the wait limit (fewer waits = tighter timing), the alarm budget (a budget equal to
  the remaining distance to the exit makes the alarm a race), cuts to force detours, doors as vision blockers.
- Make gadget levels **require** the gadget (check `shortest_without_smoke` / `_emp` = −1), otherwise the lesson is
  skippable. Keep a Ghost route on camera levels where you want the bonus to exist; make it 2–4 actions longer than par.
- Every node the player may stand on must be reachable from the start; dead ends are fine if they teach something.
- Start node must be safe at T=0 (see validator). Prefer exits on the boundary of the interior.
- When modifying a shipped level, keep `id`, `code` and `group`; add a dated note; expect the developer to re-export
  scoring. Changes to guards, cameras, cover, budgets or the wait limit can change par, Ghost and gadget requirements.

## 10. Response template for a new or changed level

```
Level: LEVEL-26 "Title" — idea: <one sentence>
Intended route: <tokens, e.g. E E WAIT N N E> (expected par N, expected waits W)
Why the trick is required: <which gadget/wait/timing and why the obvious route fails>
Ghost intended: yes/no (and the alarm-free route if yes)
JSON: <complete object in the section-4 format, scoring omitted or zeroed>
Checks for the solver: par == N; <gadget> mandatory; waits_in_par <= waitLimit−2; ghost_offered == <yes/no>
```
