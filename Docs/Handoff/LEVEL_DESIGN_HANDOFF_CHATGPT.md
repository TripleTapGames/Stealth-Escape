# Shadow Steps — Level Design Brief for an AI Designer

You are designing puzzle levels for **Shadow Steps**, a turn-based stealth puzzle for phones (portrait). Levels are small node graphs drawn over a courtyard. Everything is deterministic: no randomness, no timers, no real-time. The player taps a node to move one edge per action. Guards and cameras follow fixed schedules keyed to the action count. The game ships in **Knockout mode only**: the player may knock out guards from behind or the side, and there is **no WAIT action**. Every action is a move (or a knockout).

Your job: propose levels as JSON in the format in section 8. Do not invent pars or star thresholds: our solver computes them. Give the intended solution and why each mechanic matters in `notes`.

---

## 1. Board notation

A level is a list of `rows` of equal length. Coordinates are `[x, y]` with the origin at the **top-left**, x to the right, y down. Row 0 is the top row.

| Glyph | Meaning |
|---|---|
| `.` | **Node.** A place the player or a guard can stand. |
| `#` | **Wall.** Not walkable, blocks sight. Cameras are mounted on walls. |
| `X` | **Cover** (planter / crate). Not walkable, blocks sight, but is not a wall: cameras cannot be mounted on it. |
| `-` | **Horizontal walkway cell.** No node. A connection runs across it left–right between the nodes on either side. |
| `\|` | **Vertical walkway cell.** No node. A connection runs across it up–down. |
| `+` | **Walkway cell on both axes.** |
| `_` | **Empty ground.** No node, no connection through it, but sight passes over it. |

Connections (edges) exist between a node and the next node in each of the four cardinal directions when every cell between them is a walkway cell on that axis. Two nodes side by side are connected directly. **A connection may span at most two squares**: `.-.` is legal (one walkway cell between), `.--.` is not. There are no diagonals.

Walkways and empty ground are **not** nodes: nothing can stand there, they cannot be a start, exit, key, door end or patrol node, and a walkway must join two nodes along its axis (a dangling walkway is an error).

Why sparse boards: a full grid of nodes is boring to read. Keep only the nodes that matter and join them with walkways; use `_` to leave open ground that guards can see across.

Example (LEVEL-8 "Predict the Return", 7×5):

```
__..-..
.-...-.
.-..-..
.-.|_||
....-..
```

## 2. Movement, actions and winning

- One **action** = move along one connection to the next node (a long walkway edge is still one action). Reaching the **exit** node wins.
- **Cuts** (`cuts`: pairs of node coordinates) remove a connection between two nodes that would otherwise be joined. A cut blocks movement, not sight.
- **Key and doors.** At most one key (`key`: `[x, y]`, a node). `doors` are edges `{ "a": [x,y], "b": [x,y] }` between two nodes joined by one straight connection. A door is closed until the player **arrives on either of its two nodes while holding the key** (picked up by stepping on the key node; picking the key up on a door node opens that door at once). It then stays open. A closed door blocks movement for everyone and blocks sight; an open door blocks nothing. Guards never pass doors, open or closed, in their routes.
- **Smoke** (`gadgets.smoke` charges): a **free** action (does not count). Target: any node in the 3×3 around the player, plus the next node straight ahead across a walkway, if the line to it is not blocked by walls, cover or a closed door, and the node is not already smoked. The cloud makes anyone on that node invisible to guards and cameras for the next **2 counted actions**. A guard walking onto a smoked player still catches them.
- **EMP** (`gadgets.emp` charges): a **free** action. Disables one camera for **2 counted actions**.
- **Knockout**: in one counted action the player moves onto a live guard's current node from **behind, left or right** relative to the guard's current facing, along a connection with no closed door. Never from the front. The guard is gone for the rest of the attempt. Other guards and cameras still see the player during that action.

## 3. Guards

Fields: `{ "id": "g1", "kind": ..., "route": [[x,y], ...], "facing": "N|E|S|W", "clockwise": true|false, "offset": 0 }`.

| kind | Behaviour |
|---|---|
| `static` | Stands on `route[0]`, facing `facing` forever. |
| `rotating` | Stands on `route[0]`, turns 90° **every action** (`clockwise` true: N→E→S→W; false: N→W→S→E). |
| `pingPong` | Walks the route one node per action to its last node and back, forever. Faces the direction it just travelled; at an endpoint it still faces the way it arrived while its next step goes back. `offset` = how many steps into that cycle it is at the start (0 = on `route[0]` about to walk forward). |
| `loop` | Walks a closed route one node per action, wrapping from the last node to the first (that closing step must also be a legal connection). Faces travel. |

Route rules: every route node is a node (`.`); consecutive nodes are joined by one straight connection (direct or across walkway cells), with no cut and no door on it; a moving route visits no node twice; a `pingPong` route is a straight or bent chain, a `loop` route is a cycle. Two guards may never share a node at any action count (we check the whole cycle). A guard may not start on the start node.

**Guard sight:** the **two squares straight ahead**, only along the facing. Walkway and empty cells are looked across but still count as distance, so a node two squares ahead across a walkway is seen, and nothing beyond it. Blocked by walls, cover, closed doors and smoke. Guards do not see sideways or diagonally.

**Being caught.** After the player's move, guards update (move or turn). The player is caught if their node is inside any guard's sight **either before or after** that update, or if the player and a guard end on the same node or swap along an edge. "Slip past while he turns" does not exist: a node must be dark at the current phase and at the next one.

The HUD shows a cyan NEXT marker on a patrol's next node, and a guard's two-square fan, so players can predict.

## 4. Cameras and the alarm

Fields: `{ "id": "cam1", "wall": [x,y], "mountFace": "N|E|S|W", "facing": "N|E|S|W", "kind": "fixed|rotating|sweeping", "clockwise": true|false }`.

- A camera sits on a **wall cell** (`#`) and looks out of `mountFace`, which must face a node (`.`). It is usually on the boundary wall ring, so most camera levels have a full `#` border.
- Its cone is **70°** wide (35° each side of `facing`) and reaches **3 squares** from the wall face: the node in front and the next two node rows, walkway or not. Blocked by walls, cover, closed doors and smoke; disabled by EMP.
- `fixed` keeps `facing`. `rotating` turns 90° every action (`clockwise`). `sweeping` alternates between `facing` and `facing` turned one step (`clockwise` decides which way), **holding each for two actions**: actions 0–1 at `facing`, 2–3 turned, 4–5 back, and so on.
- Stepping into a cone (before or after the update) does not catch the player; it **triggers the alarm**: the player then has `alarmBudget` counted actions to reach the exit, or the level fails in lockdown. The alarm triggers once per attempt. `alarmBudget` must be ≥ 1.
- The start node may not be inside any camera's initial cone or any guard's initial sight.
- **Ghost** is a bonus on camera levels for escaping without ever triggering the alarm. We decide where it is offered; design camera levels so both a quick alarm-dash route and a longer clean route exist when you want a Ghost.

## 5. Hard validation rules (a level with any of these is rejected)

- Rows of equal length, size at most 16×16; **prefer width ≤ 7** (a phone in portrait); heights up to 11 are used for late levels, which zoom out.
- Start, exit and key on nodes; exit ≠ start; `doors` require a `key`; at most 30 doors.
- Every walkway cell joins two nodes along its axis; no connection longer than two squares.
- Cameras only on `#` with the mount face onto a node; `rotating`/`sweeping` need `clockwise`.
- Guard routes legal as in section 3; no two guards on one node at any time; no guard on the start node; start not in any initial sight or cone.
- `alarmBudget ≥ 1`, gadget charges ≥ 0.
- The level must be **solvable by our solver** under Knockout rules with no WAIT. Smoke or EMP "required" is only claimed after the solver confirms it.

## 6. Design rules and lessons (please follow)

- **One idea per level.** Name it in the title (≤ 24 characters) and teach it in the intro (≤ 70 characters, one sentence, plain words).
- **Node budget:** tutorials 3–13 nodes, main levels 5–25. Dissolve corridors into walkways; use `_` for open ground. Cover cells (`X`) are the only sight blockers inside the yard, so place them deliberately (we draw them as planters).
- **Knockouts must mean something.** Either the knockout is forced (no knockout-free route), or it is a reward (a shorter route than sneaking). Never let a knockout be available from the start node for free, and never make the knockout route and the sneaking route the same length.
- **Walkways change tempo.** A walkway on the approach to a patrol is a free tempo (two squares in one action), which often creates an unintended knockout-free route. Never put a walkway cell on a patrol route or directly in front of a camera.
- **Outer-row walkways** that shorten a detour to the length of the knockout route make the knockout pointless. Leave the detour side plain or cut it.
- **Phases matter.** Rotating guards have period 4, ping-pong period 2·(n−1), loops period n, sweeping cameras period 4. Design the safe moment, then check the "before or after" rule: the node the player steps onto must be dark now and after the update.
- **Key levels** should force a detour or a timing: pick the key up where the patrol can see the pickup node only on some phases, or hide the door behind the patrol's blind side. A closed door blocks the patrol's sight until the key-holder steps next to it, so the beat is "arrive beside the door only when he is turned away": the moment you stand on a door node with the key, the door is open and whoever looks through it sees you.
- **Camera levels** are about the alarm budget: a tight budget (1–3) makes the dash route a real choice; the clean route should be 2–4 actions longer.
- **Gadget levels**: one charge, one place where it is the only answer. Smoke is for crossing a guard's line; EMP for crossing a cone with a small alarm budget.
- Keep the player's first two moves obvious; put the decision in the middle; end with a short run to the exit.

## 7. The shipped campaign (what already exists)

Knockout par = the solver's shortest winning route under the shipped rules. Nodes exclude cover.

| code | title | grid | nodes | guards | cameras | smoke/emp | key | alarm | par |
|---|---|---|---|---|---|---|---|---|---|
| FTUE-1 | First Steps | 5x1 | 3 | - | - | 0/0 | - | 4 | 2 |
| FTUE-2 | Follow the Lines | 3x3 | 7 | - | - | 0/0 | - | 4 | 6 |
| FTUE-3 | Stay Out of Sight | 5x4 | 6 (+1 cover) | static | - | 0/0 | - | 4 | 4 |
| FTUE-4 | Watch the Rotation | 7x3 | 11 | rotating | - | 0/0 | - | 4 | 4 |
| FTUE-5 | Moving Patrol | 7x3 | 13 | pingPong | - | 0/0 | - | 4 | 7 |
| LEVEL-1 | Blind Side | 6x4 | 19 (+2) | static | - | 0/0 | - | 4 | 6 |
| LEVEL-2 | Clockwise | 7x4 | 23 (+3) | rotating | - | 0/0 | - | 4 | 8 |
| LEVEL-3 | One Step Ahead | 7x4 | 23 (+4) | pingPong | - | 0/0 | - | 4 | 8 |
| LEVEL-4 | Double Trouble | 7x5 | 13 (+4) | static, rotating | - | 0/0 | - | 4 | 6 |
| LEVEL-5 | Corner Patrol | 7x5 | 17 (+1) | loop | - | 0/0 | - | 4 | 7 |
| LEVEL-6 | Narrow Crossing | 7x5 | 17 (+1) | pingPong ×2 | - | 0/0 | - | 4 | 7 |
| LEVEL-7 | Crossroads | 7x5 | 16 (+4) | static, rotating | - | 0/0 | - | 4 | 7 |
| LEVEL-8 | Predict the Return | 7x5 | 22 | pingPong, rotating | - | 0/0 | - | 4 | 6 |
| LEVEL-9 | Around the Loop | 7x5 | 20 (+1) | loop, rotating | - | 0/0 | - | 4 | 8 |
| LEVEL-10 | Patrol Junction | 7x5 | 22 | pingPong ×2 | - | 0/0 | - | 4 | 7 |
| LEVEL-11 | Find the Key | 7x5 | 15 | - | - | 0/0 | yes | 4 | 6 |
| LEVEL-12 | Key Detour | 7x5 | 14 (+4) | - | - | 0/0 | yes | 4 | 12 |
| LEVEL-13 | Door as Cover | 7x4 | 13 | rotating | - | 0/0 | yes | 4 | 9 |
| LEVEL-14 | Unlock at the Right Time | 7x4 | 13 | pingPong | - | 0/0 | yes | 4 | 7 |
| LEVEL-15 | Protected Corridor | 7x4 | 13 | pingPong | - | 0/0 | yes | 4 | 8 |
| LEVEL-16 | Camera Basics | 7x5 | 5 (+2) | - | fixed | 0/0 | - | 5 | 4 |
| LEVEL-17 | Race the Alarm | 7x5 | 6 (+1) | - | fixed | 0/0 | - | 3 | 5 |
| LEVEL-18 | Rotating Security | 7x5 | 7 (+2) | - | rotating | 0/0 | - | 3 | 5 (Ghost) |
| LEVEL-19 | Sweep Window | 7x5 | 13 (+2) | - | sweeping | 0/0 | - | 2 | 6 (Ghost) |
| LEVEL-20 | Risky Shortcut | 7x6 | 8 (+2) | - | fixed | 0/0 | - | 2 | 4 |
| LEVEL-21 | Smoke Screen | 7x4 | 6 | static | - | 1/0 | - | 4 | 4 |
| LEVEL-22 | Cloud Timing | 7x5 | 17 (+2) | pingPong | - | 1/0 | - | 4 | 7 |
| LEVEL-23 | EMP Training | 7x4 | 5 | - | fixed | 0/1 | - | 2 | 4 |
| LEVEL-24 | Security Gap | 7x5 | 7 (+2) | - | sweeping | 0/1 | - | 1 | 6 (Ghost) |
| LEVEL-25 | Final Extraction | 7x4 | 8 | static | fixed | 1/1 | yes | 1 | 6 (Ghost) |
| LEVEL-26 | Courtyard Relay | 7x9 | 13 (+2) | rotating | - | 0/0 | yes | 3 | 17 |
| LEVEL-27 | Borrowed Cover | 7x9 | 19 (+1) | static, rotating | - | 1/0 | - | 3 | 14 |
| LEVEL-28 | Divided Watch | 9x9 | 20 (+2) | - | sweeping, fixed | 0/1 | yes | 2 | 11 (Ghost) |
| LEVEL-29 | Two Locks | 7x11 | 19 (+2) | rotating, pingPong | fixed | 0/1 | yes | 2 | 14 (Ghost) |
| LEVEL-30 | Inner Citadel | 9x11 | 25 (+1) | static, rotating | sweeping, rotating | 1/1 | yes | 3 | 19 (Ghost) |

Progression: moves and sight (FTUE) → one guard type each (1–3) → two guards (4–10) → key and doors (11–15) → cameras and the alarm (16–20) → smoke, EMP (21–25) → big combined boards (26–30).

## 8. What to return

A JSON array. One object per candidate. Use `base` to name the shipped level it replaces or most resembles (we copy that level's remaining defaults), and give every field below in full.

```json
[
  {
    "base": "LEVEL-8",
    "name": "C1",
    "title": "Predict the Return",
    "intro": "Cyan NEXT shows a patrol's next step, even its turn. Meet it sideways.",
    "rows": ["__..-..", ".-...-.", ".-..-..", ".-.|_||", "....-.."],
    "start": [0, 2],
    "exit": [6, 2],
    "key": null,
    "cuts": [],
    "doors": [],
    "guards": [
      { "id": "g1", "kind": "pingPong", "route": [[3, 0], [3, 1], [3, 2]], "facing": "N", "offset": 3 },
      { "id": "g2", "kind": "rotating", "route": [[3, 4]], "facing": "N", "clockwise": true }
    ],
    "cameras": [],
    "gadgets": { "smoke": 0, "emp": 0 },
    "alarmBudget": 4,
    "notes": [
      "Idea: read the patrol's NEXT marker and meet him from the side when he turns at the end.",
      "Intended route (7 actions): E, E, N, knockout g1 from the east when he faces N at the top, E, E, S.",
      "Without the knockout the only other route passes g2's fan on phase 2; expected to be 2 actions longer."
    ]
  }
]
```

More shipped examples in the same format:

Key and door (LEVEL-14 "Unlock at the Right Time", par 7):
```json
{ "base": "LEVEL-14", "name": "C2", "title": "Unlock at the Right Time", "intro": "The patrol cannot see through the door. Open it once he turns away.",
  "rows": ["___#___", "___#_._", ".......", ".-.#..."], "start": [0, 2], "exit": [6, 2], "key": [2, 3], "cuts": [],
  "doors": [{ "a": [3, 2], "b": [4, 2] }],
  "guards": [{ "id": "g1", "kind": "pingPong", "route": [[5, 1], [5, 2], [5, 3]], "facing": "N", "offset": 0 }],
  "cameras": [], "gadgets": { "smoke": 0, "emp": 0 }, "alarmBudget": 4, "notes": [] }
```

Rotating camera with a Ghost (LEVEL-18 "Rotating Security", par 5):
```json
{ "base": "LEVEL-18", "name": "C3", "title": "Rotating Security", "intro": "The camera turns every move. A clean route earns a Ghost.",
  "rows": ["#######", "#_.-..#", "#_._X|#", "#..X_.#", "#######"], "start": [1, 3], "exit": [5, 1], "key": null, "cuts": [], "doors": [], "guards": [],
  "cameras": [{ "id": "cam1", "wall": [4, 0], "mountFace": "S", "facing": "S", "kind": "rotating", "clockwise": false }],
  "gadgets": { "smoke": 0, "emp": 0 }, "alarmBudget": 3, "notes": [] }
```

Everything at once (LEVEL-25 "Final Extraction", par 6, Smoke and EMP both required):
```json
{ "base": "LEVEL-25", "name": "C4", "title": "Final Extraction", "intro": "Combine keys, Smoke, EMP and the security countdown to escape.",
  "rows": ["#######", "###.###", ".......", "#######"], "start": [0, 2], "exit": [6, 2], "key": [1, 2], "cuts": [],
  "doors": [{ "a": [1, 2], "b": [2, 2] }],
  "guards": [{ "id": "g1", "kind": "static", "route": [[3, 1]], "facing": "S" }],
  "cameras": [{ "id": "cam1", "wall": [4, 1], "mountFace": "S", "facing": "W", "kind": "fixed", "clockwise": true }],
  "gadgets": { "smoke": 1, "emp": 1 }, "alarmBudget": 1, "notes": [] }
```

## 9. Checklist before you answer

1. Every row the same length; only the glyphs from section 1.
2. Every `-`, `|`, `+` joins two nodes along its axis; no gap longer than two squares.
3. Start, exit, key on `.`; start not seen by any guard or camera at the start.
4. Routes legal (section 3); guards never meet; no guard on the start.
5. Cameras on `#`, looking onto a node; `clockwise` set for rotating and sweeping.
6. Trace the intended route action by action, writing the guard phase at each step, and apply the "before or after" rule.
7. Say which mechanic is mandatory and why you believe there is no way around it; we will confirm with the solver.
8. Title ≤ 24 characters, intro ≤ 70 characters, one idea per level.
