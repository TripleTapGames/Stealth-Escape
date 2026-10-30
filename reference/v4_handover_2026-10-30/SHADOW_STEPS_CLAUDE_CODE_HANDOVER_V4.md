# SHADOW STEPS — CLAUDE CODE IMPLEMENTATION HANDOVER (V4 BLOCKED NODES)

**Project:** Shadow Steps · deterministic turn-based stealth puzzle for mobile  
**Revision:** Gameplay/UX + blocked-cover geometry V4 · 30 October 2026  
**Platform:** Unity mobile (Android and iOS); primary existing project uses Unity 2022.3 LTS on macOS, 3D URP project with true top-down orthographic 2D presentation  
**Content target:** **5 FTUE + 25 numbered main levels = 30 levels**  
**Development assistant:** Claude Code in VS Code  
**Art status:** **NOT finalized. Do not commit production art direction or replace sprite assets in this implementation pass.**  
**Authority:** The finalized gameplay rules below govern implementation; V4 normalized JSON is the latest playtest **level geometry**, and V4 HTML is its behavioral/visual reference. Legacy Unity documents and V3 layouts are superseded where inconsistent. See §§2, 10, 15, 17.

---

## 0. Read this first — what Claude should do

Implement the **approved game rules, content, UI functionality, solver and tests in the user's existing Unity project**, using the attached self-contained HTML playtest (`shadow_steps_30_level_playtest_v4.html`) and its extracted level data as reference assets. **Do not replace the Unity project, create a separate game, or assume the HTML is production-ready code.** First inspect existing Unity scripts/prefabs/data and report a migration plan; then implement incrementally, proving parity with deterministic tests. Preserve project architecture and packages that are already working.

**Priority sequence:** engine/turn logic → data/schema converter → deterministic tests/solver → the 30 levels/FTUE → playable mobile UX → art-ready modular rendering → polish and device testing. No background work or invented completion status. Keep the original Unity content backed up and use separate migrated data rather than destructively rewriting the legacy catalog without validation.

**Hard scope boundaries:** no monetization system; no procedural endless player mode; no hints, checkpoints or production Undo; no switches, pressure plates, decoy or multiple-objective systems; no final illustration/character sprite generation or theme lock-in. We will return to art later. Use replaceable placeholder visuals and faithful tactical overlays for now.

---

## 1. Game identity and scope

A short-session, fixed-progression, single-player stealth puzzle on a graph of cardinally adjacent grid nodes. Every valid **Move** or **Wait** advances a deterministic enemy/camera clock by one counted action. Guard vision and collisions cause immediate capture. Camera detection starts a permanent security lockdown countdown; the player may still escape before the budget runs out. Keys/locked connections and two tactical gadgets add route planning.

- **FTUE:** five standalone tutorial stages before the numbered main levels.
- **Main:** 25 fixed authored/curated stages. **Any successful completion unlocks the next**; 3/2/1 stars and Ghost are optional achievements, never progression gates.
- **Development playtest:** direct access to all 30 levels allowed, plus a debug-only Undo. Final build uses sequential unlocking and hides/remove debug controls.
- **Retry:** deterministic full reset to the authored initial state; no mid-level checkpoint; no auto-restart. On capture show a short reason-revealing animation followed by a Retry button.
- **Art:** colorful, slightly playful, premium miniature spy/stealth style is the *broad preference*, not a locked art-production specification. Exact sprites, materials, palette and animations remain to be finalized. Rendering should be data-driven and themable.

## 2. Definitions and board model

**Coordinates:** `(x,y)` integer grid; `(0,0)` top left; +x right; +y down. A node is an occupiable position. Normal player and guard movement is exactly **one connected cardinal step** (N/E/S/W), with no diagonal movement. This must be enforced in the rules engine, not merely UI.

**Important distinctions:**

| Object / edge | Movement | Vision | Notes |
|---|---|---|---|
| Open bidirectional connection | Player may traverse both ways | Does not block | Draw connection line |
| Missing connection (no physical barrier) | Cannot traverse that edge | **Does not block** | No line; must not look like wall |
| One-way connection (only if present in existing content) | Follow directed edge | Does not block | Legacy schema supports; not necessary to feature in first 30 |
| Solid wall / solid cover | Physically obstructed | **Blocks** | Wall- or cell-based geometry supported |
| Closed locked connection / door | Player cannot cross without matching key | **Blocks** | Acts as a real opaque barrier across this edge |
| Opened locked connection | Player may traverse; stays open this attempt | Does not block | Unlock on *physical crossing*, not on key collection |
| Stationary Smoke node | Does not impede physical occupancy | **Blocks** line of sight through the node | Target node itself is obscured |
| Wall-mounted camera | Occupies **no movement node** | Detects via its own ray/area | Camera is installed on a wall surface |

**Geometry:** a line-of-sight (LOS) or Smoke-throw ray touching the **edge or corner** of a solid wall or closed door counts as **blocked**. Use a shared deterministic grid/segment obstruction routine for guards, cameras, preview and gadgets. Missing graph edges alone never occlude. Camera vision originates just outside the wall on its mounting face: avoid falsely self-occluding its outward view, while its mounting wall must block view toward/through the wall.

**Board size/UX:** the entire board, including wall-mounted cameras and boundaries, fits within a portrait mobile play viewport; no pinch zoom or panning. Prefer small well-designed boards (5–7 columns; test 8-column content on small phones). The old 16×16 engine capacity is not a launch UI target.

### 2.1 V4 new level feature: solid blocked-cover node (`X`) — REQUIRED

**Why this update exists:** The previous 30-level layouts were too open. V4 adds **55 solid blocked nodes across 25 of 30 stages**, creating detours, protected approach lanes, narrower crossings and less trivial shortest paths. **This is approved gameplay geometry, not final art.** Do not add a new action, movement type, key requirement or AI reaction to use cover.

**Authoritative V4 ASCII rows / serialization** (`rows[y][x]`, 0-based coordinates):

| Cell character | Meaning | Can player/guard enter? | Obstructs line of sight / Smoke throws? | Rendering |
|---|---|---|---|---|
| `.` | Playable floor node | Yes, subject to traversable graph edge | No, unless another physical blocker lies between source/target | Visible node and connections |
| `#` | Solid structural wall cell | **No** | **Yes** | Wall segment/barrier (may host a wall camera on an appropriate face) |
| **`X`** | **Solid blocked-cover node (crate/obstacle)** | **No** | **Yes** | Low-profile obstacle occupying this cell, *no usable node*, no connecting movement lines |

**Do not confuse `X` with an absent connection `cuts`:** A cut removes one cardinal movement edge but its empty physical space **does not automatically block vision**; `X` is a *solid cell* that blocks traversal, sight and Smoke targeting/throw paths. A closed locked door blocks sight *across its edge*; neither locked doors nor edge cuts should be converted into `X` cells.

**Exact required behavior:**

1. `IsWalkableNode(x,y)` returns true **only** for valid floor cells `.`. Both `#` and `X` return false. Preserve other dynamic entities separately in level data; `X` may never contain player, exit, key, guard position/patrol waypoint or Smoke target.
2. `CanTraverseEdge(a,b)` requires two walkable cardinal-neighbor nodes, a present graph edge, and satisfaction of any lock/one-way rules. **Never** auto-connect through `X`; never generate decorative movement nodes over `X`.
3. Guard and camera visibility rays regard `X` as **fully opaque for its entire logical grid-cell footprint**, including when a ray only grazes its edge/corner. If cover blocks the first target node, anything behind it is also obscured. An opaque obstacle remains opaque even if its placeholder crate sprite looks visually smaller.
4. Smoke deployment to `X` is invalid: no charge or counted action spent. Smoke throws through `X`, including a touched corner, are blocked; cloud itself stays on a valid floor node. Reuse the same core geometric obstruction algorithm for vision, throws, runtime and previews.
5. Guards never step onto or through `X`, including straight-patrol endpoint extension. **V4 JSON guard paths are already normalized; do not automatically re-extend and overwrite them.** A level generator may expand a route only in a separate authoring pass, after all obstacles are placed and before validation.
6. `X` should appear visibly distinct from `#`, `.` and missing edges at the chosen auto-fit phone scale. Use a simple solid-cover crate placeholder (cross-braced low-profile asset is fine); final art/theme is deferred.
7. If the importer finds an unknown character, invalid coordinate or blocking conflict, **fail validation with the exact level ID and cell/edge**, rather than silently repairing it. `blockedNodes` in V4 JSON is a redundant coordinate list that must exactly match the `X` positions in `rows`; it can be used for diagnostics, but `rows` is the authoritative representation.
8. The level index, `optimalActions`/par and `ghostAvailable` must come from the **V4** data and be **recomputed in Unity**, rather than copied from the older V3 handover.

**Sample real V4 level excerpt (`Level 1 — Safe Side`):**

```json
{
  "rows": ["......", "....X.", "...X..", "......"],
  "blockedNodes": [[4, 1], [3, 2]]
}
```

Here `X` cells are solid cover. The player cannot enter them, a guard/camera cannot see through them, and they cannot receive Smoke. A neighboring pair of `.` cells without a graph edge may still see each other when there is no physical barrier.

**Important scene geometry:** Do not conflate *visual sprite* with *logical occluder*. Rendering can use a crate narrower than one tile, but rules must use the full blocked cell; if that visual mismatch looks unfair, enlarge/square-off the cover art rather than weakening LOS. Camera wall mounts must still be valid after obstacle placement; a crate `X` is not automatically an eligible camera mounting wall.

## 3. Counted turns — canonical resolution order

**Counted actions:** `MoveOneCardinalNode`, `Wait`. Only these advance the clock, guards, cameras, gadget durations and an existing lockdown countdown. Invalid moves cost **zero** actions and cause **no state change**. Selecting/deploying Smoke or EMP is valid but consumes **zero counted actions**. Multiple gadgets may be activated between counted actions if charges remain.

### 3.1 Counted action algorithm (authoritative)

1. Validate action, traversable target, directed connection, key requirement and non-terminal state. On an invalid move, abort without mutation.
2. Apply player movement (or remain for Wait). **If crossing a locked connection, open that exact door immediately as part of this same movement;** the reusable key is not consumed. Pick up any key at destination automatically.
3. **Old-state collision and vision:** if player's *new destination* overlaps any guard's current node, **capture**. If visible to any guard in its **current facing/position** using the post-door/post-move geometry, **capture immediately**. No guard move/rotation after this failure.
4. **Old-state camera check:** cameras in their current orientation that can see player's destination trigger the alarm; this does **not** stop the action.
5. **Enemy update:** all moving guards advance **one node**, stationary rotating guards turn **90°**, and rotating/sweeping cameras update as specified. The update is deterministic and simultaneous at the logical level.
6. **New-state collision and vision:** guard ending on player node is capture; **player/guard edge swaps** (crossing in opposite directions on same connection during this action) are capture; any guard seeing player after update is capture. Camera detection in the new state triggers the alarm but never interrupts enemy updates. If both guard capture and camera alarm are possible, guard capture determines the loss outcome.
7. Increase the counted action total by one. If an alarm was already active **before this action**, subtract one from its remaining budget; the action that first triggers alarm grants its **full per-level subsequent-action budget**, with no decrement that turn. Later detection never refills it.
8. If the player reaches the exit alive with all actual objective prerequisites satisfied, **win here, after the full enemy update**. A camera alarm activated on the winning action is recorded and prevents Ghost, but **does not prevent victory**. Entering the exit on the last permitted lockdown action wins, even as its counter reaches 0.
9. For a non-winning, non-caught action, expire active Smoke/EMP durations after their 2nd protected counted action. **Immediately recheck** the resulting unprotected guard vision for capture and camera vision for alarm. A player exposed by expired Smoke can be caught on this same action, without needing a third move. A camera reactivated when EMP expires can trigger lockdown. Finish with lockdown failure if remaining budget is 0 and the level has not already been won or lost to guard capture.

**Time/tweening:** logical detection occurs at discrete boundaries; never perform continuous frame-by-frame vision during movement animation. The visual sequence must communicate whether capture happened before or after enemy update. No moving-guard animation may cause extra gameplay turns.

**Example A:** Player moves into guard's old vision → immediate caught before guard rotates.  
**Example B:** Player moves to currently safe node → guard rotates/moves → new vision sees player → caught.  
**Example C:** Player enters exit, camera sees them, guard completes its update but does not catch them → win, **no Ghost**.  
**Example D:** Smoke or EMP has one protected action left; player exits safely on that action → win precedes post-action gadget expiration detection.

> **Migration warning:** the older Unity reference describes Smoke/EMP as turn actions, an angular guard cone, and door nodes that open on key pickup. All are obsolete and MUST NOT be copied into the new engine.

## 4. Guards

**Guard vision:** *exactly 2 nodes straight ahead* along the cardinal facing; **no diagonal/cone vision**. The nearest opaque blocker stops sight to it and anything behind it. A guard standing within Smoke cannot see; a guard walking out of it may see immediately at the post-update check. Smoke never hides player–guard collisions.

| Guard type | Movement each counted action | Facing | UI badge |
|---|---|---|---|
| Static | None | Fixed authored direction | Current facing only |
| Rotating | None | **90° every action**, configured CW or CCW | Small `↻` / `↺` marker indicating turn direction |
| Straight ping-pong patrol | **One node every action** along a straight connected route, to the **last accessible/defined endpoint**, then reverses on the next action (no endpoint pause) | Faces actual direction of travel | Distinct cyan **NEXT ↑/→/↓/←** marker showing its next actual step |
| Loop/route patrol | **One connected node every action** around a closed path, corners allowed | Faces actual travel direction | Cyan **NEXT** step marker |

**No independent moving+rotating type.** The old `moveRotate` mode is retired for launch; guards that move always face their travel direction. There is no `moveEvery=2`, waiting on waypoints, or guard AI reacting to a camera alarm.

**Route terminal requirement:** ping-pong guards must truly reach the **end node** before reversing. The V4 browser source extends authored straight corridors before applying X obstacles; the exported V4 JSON contains the resulting patrol paths to their reachable terminal nodes (unless blocked by missing connection, wall, or locked-door edge); do not accidentally stop at the second-last node. **Do not extend the exported V4 patrol paths a second time**; they are already normalized. Preserve their endpoints, verify no blocked `X` intersects them, and validate all scheduled moves. If automatic extension changes a stage, recompute par and verify it remains fun.

**Enemy interactions:** two guards must never occupy the *same node at a logical turn boundary* (validate entire repeating schedule); guards **may swap nodes across the same connection** in one turn, and may reuse the same nodes at different times. Two guards crossing visually do not block each other. Guards never traverse locked-door edges in their patrols **even after unlocked**. They do not change patrol route due to player or camera state. Player vs guard swapping positions **is fatal**.

**Facing subtlety:** the guard's facing icon depicts **current facing**; its cyan NEXT icon depicts the **upcoming movement step**. At ping-pong endpoints the next arrow points back toward the route, even when the current facing still points toward the endpoint. A loop's next arrow must turn at corners. Rotation `↻/↺` marker is **not** a travel arrow.

## 5. Wall-mounted cameras and alarm

**Geometry:** cameras mount to an **outer or internal physical wall**, selected wall face. They do not consume a movement node. Render a visibly attached bracket, camera head/lens and current orientation.

**Detection:** nominally a rectangle **3 nodes wide × 2 nodes forward** in the facing direction, not a widening 90° cone. Each target node needs its own obstruction/Smoke check; wall corners block. Some facing directions may be blocked completely because the camera turns toward the wall — **allowed**. Do not animate a misleading full rectangular/fan region through solid walls.

| Camera type | Clock behavior | Marker |
|---|---|---|
| Fixed | Facing never changes | Lens direction only |
| Rotating | Turn **90° every counted action**, configured CW or CCW | Small `↻` / `↺` rotation marker |
| Sweeping | Alternate between two **adjacent 90°-apart facings**, **hold each for 2 counted actions** | Sweep indicator and visible current facing |

A sweeping N↔E camera follows before→after states such as N→N, N→E, E→E, E→N, repeat. This hold is essential: rotating N↔E every action while evaluating both old and new vision would make the union of hazardous nodes constant, destroying timing opportunities.

**Camera alarms:** guard capture is immediate failure; camera sight is **not**. First camera detection activates permanent lockdown with a **level-specific fixed budget** of subsequent counted Move/Wait actions. All guards/cameras finish the trigger turn as normal. Repeated detection does not reset or add time. Alarm persists even if the player leaves sight or disables cameras. Guards do not change AI behavior. Smoke/EMP activation costs no alarm budget. When budget expires, fail unless the action already successfully reached exit. Show exact actions left and failure reason.

**Ghost:** offer only on camera-bearing levels **with a solver-proved alarm-free completion route**. Never offer for levels without cameras or forced-alarm stages. Ghost is earned by any successful attempt with **zero alarm activations**, including runs that used Smoke or EMP. Once triggered, this attempt cannot earn Ghost. Keep best stars and Ghost as independent persistent achievements, even if earned in different runs.

## 6. Keys and locked paths

- On entering a key node, collect automatically; no extra counted action. **One reusable matching key may unlock multiple matching connections**. Initial launch levels use one key type; do not build multiple-key inventory puzzles unless data already needs it.
- Collecting a key **does not remotely open any door**.
- To open, the player must physically reach and cross a specific closed locked connection. Unlock/open and move across in **one counted Move** (no separate unlock action).
- Closed connection blocks both movement (unless key) and LOS; after crossing it remains open for the attempt and immediately ceases to block LOS, even on the **old-vision check** of that crossing action.
- Attempt without the key is invalid and free. No guard patrol ever uses a locked connection.
- Keys only gate their associated connection(s); avoid forcing an additional generic "has key" win check on levels where the exit is otherwise reachable unless a specific objective explicitly requires it.

## 7. Gadgets

Both gadgets have **fixed, per-level starting charges**. Retry restores them; unused charges **never carry forward** to another level. Required gadgets must be supplied, never acquired through ads or purchases. Multiple distinct free activations between counted turns are allowed; gadget charges decrement on successful deployment only.

### 7.1 Smoke

- **Target:** player's node or any grid node in the surrounding **3×3** square (Chebyshev radius 1). Diagonals allowed **for throwing only**, not movement.
- **Placement:** one stationary cloud, exactly **one node**; can be placed **directly on a guard's occupied node** or on player's node. Placement ray blocked by physical walls, closed doors and touching blocked corners. No throws through barriers. Do not overwrite an already active cloud on same node; invalid target costs no charge.
- **Effect:** blocks guard/camera sight **into or through** its cell, not physical movement/collision. A guard inside a cloud is blinded; when it steps out, sight works normally if unobstructed. Does not stop patrol or camera motion; does not cancel active alarm.
- **Duration:** active for **two full counted Move/Wait actions following deployment**, then expires and triggers immediate exposure checks **after** the second action (unless that action successfully completed level).

### 7.2 EMP

- **Target:** exactly **one selected wall camera anywhere on the board**, no distance restriction; may not waste a charge on a camera already disabled.
- **Effect:** camera cannot detect for **two full counted Move/Wait actions**; camera continues its rotation/sweep schedule. EMP does **not** freeze guards, disable guard vision, prevent collisions, or cancel lockdown.
- **Expiry:** immediately after second protected action, camera resumes detection at its then-current facing; if player visible, alarm triggers at once unless player already safely won on that action.

**Free gadget input flow:** tap Smoke or EMP to enter mutually exclusive targeting mode; show valid targets; tap one valid target → **deploy instantly**, no confirmation tap → consume one charge and **automatically return to Move mode**. Tap invalid target → no action/charge; targeting remains. Re-enter for another charge. Cancel with the active button or Cancel control; in gadget mode taps/swipes must never move the player. The deployment pointer/tap cannot also be interpreted as a movement input after mode switch.

## 8. Mobile gameplay UI and accessibility

**Orientation/viewport:** portrait, top-down orthographic, full board automatically fit in remaining viewport between compact HUD and lower action strip. Preserve aspect ratio, include wall camera bodies and margin in fit. No user-controlled zoom/pan. Use Unity canvas safe-area support and test short/tall phones.

**HUD:** level number; current **Actions** count (not "Moves", because Wait counts); exact **3★ and 2★ action thresholds** displayed throughout play; Ghost status only when offered; pause/Retry; gadget charges; clear alarm countdown when triggered. On active Smoke/EMP show remaining **protected actions** (`2`/`1`) attached to cloud/camera as distinct from inventory charges.

**Board:** V4 additionally draws every `X` as a clearly impassable solid-cover obstacle with **no node and no movement connections**. Its full logical cell blocks guard and camera range overlays and Smoke throws. Do not accidentally tint nodes behind `X` as visible. The placement must remain clear when the board automatically fits a small phone. All movement connections remain **visible** as subtle lines; those currently traversable next to player are brighter/cyan. Missing connection = no line; locked connection has a clear closed/open door sprite; solid wall uses an unmistakable opaque physical barrier. Do not confuse **reachable** (cyan) with **safe** (vision). No visual pathfinding/autonomous routing on tap.

**Characters/markers:** player high-contrast small silhouette; guard current facing always apparent; rotating guard shows `↻/↺`, moving guard shows cyan **NEXT** cardinal step (computed from actual `position(t+1)-position(t)`), rotating camera shows `↻/↺`, sweeping camera shows sweep indicator plus orientation. On camera the lens/barrel shows actual facing; never display a camera as occupying a node.

**Vision styling:** current guard LOS is faint red tinted area with **solid red danger-node borders**; camera LOS faint amber with **solid amber danger-node borders**, plus visible originating camera/range illustration. Where both cover the same node, **retain both**: red outer, amber inner outline (do not hide one or blend into unidentified color). All overlays match actual unobstructed detected nodes. During optional preview, show **dashed** future hazard borders; distinguish old/current from upcoming. Disabled camera has conspicuous cyan/grey state, no active amber hazard, and duration badge.

**Touch controls:**

| Gesture | Behavior |
|---|---|
| Tap immediately adjacent connected node | One counted Move |
| Cardinal swipe **from anywhere on playable board** | One counted Move in requested direction, no matter swipe length |
| Long-press a candidate adjacent node | Optional **one-action** consequence preview; releasing cancels without moving |
| Tap Wait | One counted Wait; long-press Wait may preview its result |
| Tap Smoke/EMP, then a valid target | Instant free deployment; automatically return to movement |
| Invalid/blocked/ambiguous tap or gesture | No move, no clock update, no charge consumption |

Use a **single gesture recognizer/state machine** to arbitrate taps, swipes and long-presses. Enlarge invisible node/camera hit areas where possible; ambiguous overlaps should be rejected, not guessed. Swipe should require one dominant axis; never use gestures beginning on HUD/buttons as board movement. No input queues during move/tween/capture animations. A completed long-press preview must **not** execute movement on release. Initial thresholds (e.g., ~24dp swipe, ~350–400ms long press) are *testing suggestions*, not immutable rules.

**Preview correctness:** simulate the *same* rules engine without committing state. It must distinguish old-vision guard capture, post-update guard capture, collision, camera alarm, expired Smoke exposure, EMP reactivation, lockdown failure and successful exit. In previewing locked door crossing, render door temporarily open before showing old-vision exposure. Do not reveal multi-turn solutions, auto-hints, or change clock from preview.

**Failures:** short cause-revealing animation; guard turns/moves visually *only if that update actually occurred*; then Retry. Lockdown failure has distinct cause but reuses Retry screen. No auto-retry, no player Undo, no hints, no checkpoints.

## 9. Progression, scoring and achievements

- Player release progression: **FTUE 1–5 → Main 1–25**. Any successful escape unlocks next. Completing levels again to improve stars/Ghost is allowed. Debug playtest has all level selection unlocked.
- **Actions = counted Moves + Waits**. Gadget deployment and invalid actions are free.
- Each level's shortest-winning action count `P` is found by a complete solver using its guaranteed loadout and all timing/door/camera mechanics.
- Every successful completion earns ≥1 star. **Confirmed policy:** slight 3★ tolerance scaled to level length; HUD shows precise 3★ and 2★ targets; Ghost independent of stars.
- **Provisional exact numeric formula** currently in HTML V4, *not separately signed off as a fixed release policy*: `T3 = P + max(1, min(3, floor(P/10)))`; `T2 = P + max(2, ceil(0.30 * P))`. Use as V4 parity/temporary score defaults; flag for final tuning before release. Do not silently present this formula as a previously confirmed final rule.
- For future monetization (out of scope), additional purchasable/earned charges must not invalidate fair scoring or become necessary for mandatory progression.

## 10. Level content and curriculum — exact playable reference

**Authoritative *playtest content*:** bundled `shadow_steps_30_level_playtest_v4.html` (single-file browser prototype) and **`shadow_steps_levels_v4.json` (30 normalized levels)**. `level_index_v4.csv` and `level_index_v4.json` contain the actual V4 par, Ghost and blocked-node data. In the browser HTML, straight-patrol paths are extended *before* blocked `X` cover is injected; the exported V4 JSON already contains final normalized `rows` and guard paths. **Import the V4 JSON as data, rather than layering V4 obstacle lists over V3 levels or re-extending V4 routes.**

**V4 JSON structure:** The root is `{ "version": "v4", "levels": [...], "stats": {...}, "bad": [] }`. Each normalized level carries `rows`, `w`, `h`, `start`, `exit`, optional `key`, `guards`, `cams`, `doors`, `cuts`, `smoke`, `emp`, `budget`, `intro`, `group`, `id`, `label`, `optimalActions`, `ghostAvailable`, `blockedNodes`. **`X` coordinates live in `rows` already.** `blockedNodes` is a checkable redundant index, not a second placement pass. This JSON is a browser-reference schema; map into Unity's actual serializable format without discarding semantics.

**FTUE levels (5):**

| FTUE | Name | Teaches |
|---|---|---|
| 1 | First Steps | One-node tap/swipe movement and exit |
| 2 | Follow the Lines | Connected paths and walls |
| 3 | Stay Out of Sight | Static guard; 2-ahead LOS |
| 4 | Watch the Rotation | Rotating guard and Wait |
| 5 | Moving Patrol | Moving patrol reaches endpoint; cyan NEXT-step arrow |

**Main levels (25):**

| Level | Name | Main lesson |
|---|---|---|
| 1 | Safe Side | Navigate stationary guard |
| 2 | Clockwise | Time rotating guard |
| 3 | One Step Ahead | Cross a moving patrol |
| 4 | Double Trouble | Two stationary/rotating guard threats |
| 5 | Corner Patrol | Route patrol turns at corners |
| 6 | Narrow Crossing | Synchronize multiple patrols |
| 7 | Crossroads | Timing and alternate route choice |
| 8 | Predict the Return | Anticipate endpoint reversal |
| 9 | Around the Loop | Loop plus rotating guard |
| 10 | Patrol Junction | Shared pathways and guard timing |
| 11 | Find the Key | Key pickup and physical door crossing |
| 12 | Key Detour | Reusable key and non-remote unlocking |
| 13 | Door as Cover | Closed door blocks vision |
| 14 | Unlock at the Right Time | Opening door changes LOS immediately |
| 15 | Protected Corridor | Moving patrol stays off door connection |
| 16 | Camera Basics | Fixed wall camera, alarm rather than instant failure |
| 17 | Race the Alarm | Fixed lockdown budget |
| 18 | Rotating Security | Rotating wall-mounted camera |
| 19 | Sweep Window | Two-turn camera sweep cadence |
| 20 | Risky Shortcut | Fast alarm route versus Ghost route |
| 21 | Smoke Screen | Deploy Smoke, block guard LOS |
| 22 | Cloud Timing | Stationary cloud versus moving guard |
| 23 | EMP Training | Target a wall camera with EMP |
| 24 | Security Gap | EMP/reactivation and sweeping camera |
| 25 | Final Extraction | Key + Smoke + EMP + alarm interaction |

**QA status of provided V4 HTML/data (rechecked for this package):** Executed its embedded solver against all **30 levels** (`maxTurns=30`, state exploration cap `130000` per level). **30/30** returned a winning solution, all shortest solution action counts equaled the V4 `optimalActions` values, **55 `X` obstacles** were present across **25** stages, no start/exit/key/guard position occupied an `X`, and no same-node guard overlaps were found across logical phases **0–119** for each stage. See `v4_validation_report.json`. These are **browser-model-only** checks; they do not demonstrate Unity parity or that the layouts are fun. The blocked nodes are a first playtest geometry revision and are subject to further gameplay review.

**Level design principle:** short, legible puzzles with meaningful timing and route decisions, not random decoration or lots of waiting. Target no more than two consecutive Waits in an intended shortest solution, review >20-action stages, allow multiple valid solutions, and prove required-gadget levels by solving again with relevant gadget allowance removed. A level may require its *supplied* Smoke/EMP. Forced-alarm levels must not offer Ghost. No level may depend on a paid/ad-earned gadget.

## 11. Solver, validation and generator requirements

**Solver must share runtime logic, not reimplement approximated rules.** Use a deterministic transition function (`TryApplyAction(state, action) -> outcome + new state`) callable from runtime, preview and solver. Use BFS/Dijkstra with **0-cost gadget activation edges** and 1-cost Move/Wait edges, or 0–1 BFS, to minimize counted actions exactly. Plain BFS treating every gadget as a turn is incorrect. Avoid state explosion from arbitrary free multi-deployment by finite per-level charges and canonical state hashing.

**Solver state needs at least:** player position, global counted turn/phase, key ownership, door-open bitset, guard/camera schedule phase (or global time), active Smoke node(s) and remaining protected actions, EMP camera IDs and remaining protected actions, remaining charges, alarm-active/budget remaining, alarmEver, and terminal status. Use mod LCM period for cyclic phases when safely equivalent, retaining gadget/alarm durations and total action count separately.

**Validator acceptance checks:**

1. Every level has a valid board, start/exit, connection graph, walls, **`X` cover nodes**, camera wall+face and deterministic schedules; input `rows` has `h` rows of `w` valid characters and the redundant `blockedNodes` list exactly matches `X` coordinates.
2. Start, exit, any key and **every guard patrol node** are walkable `.` cells, and no patrol edge crosses `X`. Start is in no initial guard or camera vision and does not require instant gadget deployment merely to survive load.
3. No guard shares a node with another at any phase across the true patrol/rotation cycle; **guard-guard swaps are allowed**.
4. Guard patrol steps are valid cardinal connections, one per counted action, never through locked-door edges, with correct end-node reversals and correct current/next facing markers.
5. Full-loadout level solvable; `P` recorded from solver. Winning solution trace replay matches expected outcome in the Unity runtime.
6. For designated mandatory Smoke or EMP levels, solver confirms no win without that type. For combined mandatory levels, test each type independently removed.
7. Camera countdown correctly requires the intended number of *subsequent* actions and cannot be bypassed unintentionally; forced-alarm means genuinely no no-alarm solution.
8. Ghost offered only if at least one **alarm-free** winning solution exists and there is at least one camera. Ghost eligibility remains valid with gadgets.
9. Vision and throwing obey exactly the same physical occlusion (both structural `#` and blocked-cover `X`, their corners, closed door, Smoke where applicable). Preview and runtime show precisely the same danger states.
10. UI renders no playable node or connection through `X`; LOS highlighting is clipped to valid visible target cells; physically blocked movement is invalid and free; no Smoke can be targeted on/through `X`. Layout fits portrait small devices without unreadable text, tiny targets or off-board cameras; human review rejects dead-space/tedious waits and trivial unintended shortcuts.

**Generator:** use authored geometry/patrol/door/camera templates + deterministic variations, then solver and manual playtest acceptance. Do not simply port the legacy random-wall generator unmodified: it assumes cone guards, slower rhythms, node-door model, old Smoke/EMP, and 20 main stages rather than 25.

### 11.1 Additional V4 blocked-node regression fixtures (mandatory)

Implement deterministic EditMode/unit tests (use the same runtime graph and obstruction routine):

| Fixture | Expected outcome |
|---|---|
| Move onto adjacent `X` | Invalid, no turn increment, no guard/camera update |
| Move onto adjacent `#` | Invalid, same free handling |
| Swipe in direction of `X` | Rejected, exactly zero movement; no alternative auto-route |
| Guard route or start intersects `X` | Fail authored-level validation |
| Guard sight through an `X` directly ahead | Guard cannot see cells inside/behind solid cover |
| Camera rectangular 3×2 range partly hidden by `X` | Only individually unobstructed target cells highlighted/detected |
| Ray just touches corner of `X` | Blocked by strict corner rule |
| Smoke target is an `X` | Invalid, charge unchanged |
| Smoke target is floor behind an `X` | Invalid when throw ray intersects/touches cover |
| Smoke on guard's valid occupied `.` cell | Still allowed (unless physically obstructed) |
| Missing graph connection between `.` cells, no physical blocker | Traversal blocked but sight can still pass |
| Closed door edge between `.` cells | Blocks sight until crossed and opened with key |
| Rendering of `X` | No floor node/hit target or movement line to center of crate; cover visible at auto-fit mobile scale |
| Unity import 30 V4 levels | Exactly 55 `X` across 25 stages, and each `blockedNodes` agrees with its `rows` |
| One obstacle removed/added | Re-run solver, par, Ghost eligibility, patrol occupancy checks; never silently reuse old values |

Be aware that a camera's nominal 3×2 range is **not** an excuse to draw a large uninterrupted fan through solid cover. The V4 HTML may render an approximate illustration; authoritative hazard nodes are the obstruction-filtered cells.

## 12. Unity implementation structure — proposal (adapt to existing repo)

Do **not** create duplicates if equivalent components already exist. Inspect actual namespaces and injection setup before editing.

```text
Assets/_ShadowSteps/
  Scripts/
    Core/
      LevelDefinition.cs            # V4 rows (# structural wall / X solid cover / . floor), cameras, edge locks, gadget charges/budget
      LevelGraph.cs                 # cardinal edges + X/# nonwalkable cells + separate physical occluders
      GuardSchedule.cs             # deterministic one-step path and rotation
      CameraSchedule.cs            # fixed/90° rotating/2-turn sweeping
      VisionSystem.cs              # guard 2-ahead LOS; camera 3×2 wall origin; X/# opacity + strict corners
      RulesEngine.cs               # single canonical immutable/cloneable turn transition
      GameState.cs                 # full rewindable/testable snapshot
      Solver.cs                    # 0–1 shortest path with free gadget actions
      LevelValidator.cs            # full-cycle occupancy and gameplay checks
    Gameplay/
      LevelSession.cs              # start/retry/completion/progression
      InputModeController.cs       # Move / Preview / SmokeTarget / EMPTarget / Resolving
      MobileGestureRecognizer.cs   # classify tap/swipe/long-press once
      BoardRenderer.cs             # floor nodes/paths, X obstacle placeholders, walls, doors, hazards
      GuardView.cs                 # current facing + NEXT step + rotation marker
      CameraView.cs                # wall mount, lens, 3×2 coverage, sweep/rot marker
      GadgetView.cs                # Smoke clouds, EMP statuses, duration badges
    UI/
      GameHUD.cs                   # Actions/stars/Ghost/alarm/gadgets
      FailurePanel.cs              # caught/lockdown with Retry
      LevelSelect.cs               # FTUE + 25 sequential unlocked main levels
    Persistence/
      ProgressSave.cs              # best stars + independent Ghost, safe save upgrades
  StreamingAssets/ShadowSteps/
    levels_v4_converted.json       # new Unity schema derived from browser reference
  Tests/EditMode/
    CoreRulesTests.cs               # includes blocked-node, ray-occlusion, Smoke-throw cases
    SolverParityTests.cs
    LevelValidationTests.cs         # includes normalized V4 X/rows/blockedNodes consistency
  Tests/PlayMode/
    InputAndUITests.cs
```

**Existing reference files to inspect:** `Assets/_ShadowSteps/Scripts/Core/RulesEngine.cs`, `VisionSystem.cs`, `GuardSchedule.cs`, `LevelDefinition.cs`, `LevelGraph.cs`, `Solver.cs`; `Tools/CoreTestRunner/Generator.cs`; `Assets/StreamingAssets/ShadowSteps/levels.json`. These are paths stated by the supplied *legacy reference*, not verified against the user's current local Unity checkout. **Do not assume they still exist unchanged.** Use what the repository actually contains.

**Implementation rules:** no gameplay calculation in animation scripts; no Unity `Physics.Raycast` logic inconsistent with grid occlusion; no nondeterministic AI; separate world/viewmodel from rendering. Prefer small pure C# core that can be run in edit-mode tests without scene/Unity UI. Preserve existing DI framework if present (e.g. Zenject), but do not add new packages just for this handover.

## 13. Required deterministic test cases

Set up explicit fixture boards and machine-check all transitions:

**Player/guards**
- Enter old guard vision → caught before rotation; guard phase remains old.
- Enter node safe in old vision but seen after rotation/movement → caught.
- Player steps onto guard's old node → caught regardless of facing and despite guard moving off it.
- Guard moves onto player destination → caught.
- Player and guard swap positions along connection → caught.
- Two guards swap positions along edge → valid; two guards occupy same logical node → validator error.
- Ping-pong reaches last accessible endpoint, then next turn goes one node back with no pause; NEXT badge changes before reversal.
- Loop patrol turns at corners and NEXT arrow matches actual next step.
- Fixed/rotating guard sees exactly 2 cardinal nodes; walls, closed door, Smoke block; no diagonal detection.

**Doors/visibility**
- Without key locked-edge attempt invalid, zero turn; with key opens connection and moves in same action.
- Crossing opens LOS before old guard-vision check; player may be captured immediately through newly opened door.
- Collecting key does not open any distant door; key reusable for multiple doors; guards never patrol door connection.
- Missing movement edge without wall does not block sight; physical wall corner contact blocks sight/Smoke throws.

**Cameras/alarms**
- Camera wall placement outside playable nodes; obstructed rotation into wall produces zero detected nodes where appropriate.
- Fixed camera has true 3-wide × 2-forward node footprint with per-node occlusion.
- Rotating camera turns 90° per counted action; sweeping holds each of two adjacent facings for 2 turns.
- Triggering camera alarm does **not** stop the remaining guard/camera update on that action.
- Trigger grants full B subsequent actions; each subsequent Move/Wait reduces B by 1; Wait counts; free Smoke/EMP do not.
- Additional camera detection never resets alarm; a camera sees player on exit move → win/no Ghost if no guard capture.
- Last countdown action reaches exit safely → win; last countdown action elsewhere → lockdown failure.
- Ghost with Smoke/EMP but no alarm → eligible; forced alarm or camera-free level → no Ghost offered.

**Gadgets**
- Smoke on player/guard node within 3×3 allowed; blocked corner/wall/closed door placement rejected without charge loss.
- Smoke cloud stays placed; two protected counted actions; immediate post-expiry guard capture if revealed; physical collisions remain fatal.
- EMP remote-selects exactly one wall camera; two protected actions; camera still rotates; immediate camera reactivation/alarm on expiry if visible.
- Multiple free gadgets between turns possible; charge bookkeeping correct; deployed free gadget does not advance clock.
- Winning during second protected action occurs before gadget post-action exposure check.

**Input/UI/score**
- One swipe from anywhere = exactly one step; no movement from HUD/swipe during gadget mode; touch after gadget deployment not forwarded to movement.
- Long-press preview never mutates state, even on release; previewed doors/LOS match actual action outcome.
- Current guard and camera hazards both visible on overlapping node; disabled camera excludes active amber danger.
- Actions count includes Wait, not Smoke/EMP; 3★/2★ HUD thresholds visible.
- Retry restores exact full initial state; successful completion unlocks next level and persists best stars/Ghost separately.

## 14. Suggested implementation milestones and acceptance

| Milestone | Scope | Acceptance test |
|---|---|---|
| M0 — Audit | Inspect Unity project, back up files, compare legacy and new rules | Written migration map; no destructive edits |
| M1 — Canonical model | V4 `X` vs `#` vs `.` schema, graphs, guards, camera mounts/schedules, shared occlusion and turn resolution | Blocked-node + core deterministic fixture tests pass |
| M2 — Gadgets and score | Free targeted Smoke/EMP, alarm, keys, doors, win and Ghost | Two-phase and expiry edge cases all pass |
| M3 — Solver | 0–1 solver, validator, generator gates | Recompute par; no impossible content; no guard overlaps |
| M4 — Level migration | Import 5 FTUE + 25 main from V4 JSON **including 55 `X` cover cells**; do not reapply V4 obstacles or re-extend patrols | Unity replay all 30 and regenerate par/Ghost; parity report |
| M5 — Controls/UI | Tap/swipe/long press, targeting, board auto fit, range overlays, NEXT arrows | Device QA on narrow/tall Android/iPhone viewports |
| M6 — Release prep | Sequential unlocks, fast Retry, persistence, disable debug UI, profiling | All 30 playable start-to-exit; no production Undo/hints/checkpoints |
| Future — Art pass | Final sprites, effects, palette, animations | **Explicitly deferred; do not start in current task** |

**Definition of done for this handover:** **All 55 V4 obstacles are imported and behave as physical blocked cover**, with correct movement, camera/guard LOS and Smoke throw restrictions. The existing Unity project can play FTUE 1–5 and Main 1–25 with identical approved logical rules, each level is solver-validated, UI shows accurate camera/guard hazards and direction markers, controls work reliably on touch, and level progress/stars/Ghost persist. This does **not** require polished final art.

## 15. Legacy vs prototype discrepancies — do not ignore

The supplied `RULES_AND_GENERATOR_REFERENCE.md` describes *older* Unity code and is included in this package for **migration context only**, not as the final rules source. Differences:

| Topic | Older Unity reference | Approved target / HTML V4 reference |
|---|---|---|
| Guard LOS | 90° expanding cone, configurable range | **2 cardinal nodes straight ahead**, no cone |
| Guard cadence | `moveEvery` / `rotateEvery` may slow down | Moving guards move 1 node every counted action; rotating guards turn 90° every action |
| Moving+rotating | Independent `moveRotate` | **Retired**; moving guards face travel |
| Camera entities | Not implemented in legacy reference | Wall-mounted, no node; fixed/rot/sweep |
| Detection | Hybrid before/after cone in V2 | Two stage **straight** guard LOS and old/new camera check |
| Key/door | Door node; key possession can open its sight barrier | **Locked connection**; physical crossing opens it; reusable key |
| Smoke | Player invisibility / turn-cost, follows player conceptually | Free targeted stationary opaque one-node cloud lasting 2 counted turns |
| EMP | Clock freeze with no useful effect | Free selected-camera disable for 2 counted actions; camera rotates normally |
| Undo | Unlimited player Undo | **Debug/testing only** |
| Stars | Strict par / par+2 | **Length-scaled 3★ tolerance**; provisional V4 formulas |
| Content | Old 5 FTUE + 20 main | **V4 5 FTUE + 25 main, now with 55 `X` blocked nodes** |

**The HTML V4 prototype is also not a perfect production specification**: it is one-file JavaScript with embedded game data and placeholder SVG art; camera fan drawing is an illustrative polygon and may span obstructed floor despite per-node detection using obstruction tests. Unity range art must render only the **actual visible cells/rays**, without false wedges through physical barriers. Some FTUE/main stages need manual playtesting for pacing; a valid solver path is not proof of fun. Browser playtest's button/touch event system is not Unity's input system. The browser preview and its precise failure visuals should be parity-tested separately, not blindly ported. The browser's exported `PARS` and Ghost flags are test fixtures, to be recomputed after any Unity migration/change.

## 16. Intentionally deferred / pending final tuning

- **Final art assets**: detailed character, environment, camera, wall/floor, visual palette, animation and audio production. Broad aesthetic preference only: colorful, slightly playful, premium tactical clarity.
- **Exact numeric star threshold formula**: approved *concept* is forgiving length-scaled 3★; browser V4 numbers remain provisional pending release tuning.
- **Monetization/IAP/rewarded gadgets**: deferred entirely. No dependence on them for mandatory completion.
- **Future mechanics**: switches, pressure plates, collectible side objectives, more key types, future environments, endless mode and hint system are out of launch scope.
- **Optional quality-of-life changes** like camera range opacity, gesture threshold values and animation durations should be device-tested, not mistaken for canonical gameplay rules.

---

## 17. V4 blocked-node migration checklist and change log

**Supersedes:** `ShadowSteps_ClaudeCode_Handover_2026-10-29.zip` (V3 level geometry), previous `levels_v3_*` references and HTML V3 *level layouts*. The approved game rules and control decisions otherwise remain unchanged.

**Latest V4 data:** 5 FTUE + 25 main stages, 55 `X` blocked cells distributed across 25 stages; `shadow_steps_levels_v4.json` contains all exact positions and current shortest-action targets, while the HTML provides a runnable comparison. The exact V4 data is more important than manually re-creating each crate from screenshots.

**Claude's requested implementation order:**

1. Inspect existing Unity project and canonical data model. Identify current non-walkable cell representation, visibility ray routine, graph builder and solver. Plan a small change without rewriting working logic unnecessarily.
2. Add `SolidCover`/`BlockedNode` terrain type for `X`, distinct from physical wall `#` and movement-edge cut. Keep data parsing and rendering separate.
3. Update movement, key/exit/guard placement validation, guard patrol scheduler and path extender to reject `X`. Prefer **V4 explicit patrol paths** as they are exported; do not extend twice.
4. Update shared physical LOS and Smoke-throw tests to treat both `#` and `X` as full-cell opaque; prevent diagonal corner exploits. Preserve locked door dynamic LOS behavior.
5. Render crate placeholders at `X` positions. No selectable node/hit area or glowing connection through a blocked node. Keep guards and camera hazard indicators accurate when their rays are obstructed.
6. Convert/import **all 30 V4 levels** as a new, versioned data set. Don't mix V3 par/row assumptions. Keep older authored content backed up until migration passes.
7. Run Unity deterministic tests and solver on all 30; compare V4 browser `optimalActions` and `ghostAvailable`, report any difference rather than forcing the expected number. Validate all guard schedule phases, initial safety and required-gadget conditions. Save a CSV/JSON report and report the integration status.
8. Playtest for *interesting decisions*, not just feasibility: measure how many obstacles produce true alternate routes/cover versus incidental detours. Adjust geometry and revalidate as needed; do **not** automatically change final art.

**Unchanged mechanics:** two detection phases; two straight guard sight nodes; rotating guard 90°/turn; moving guard one node/turn with cyan NEXT direction marker and full-route endpoints; wall-mounted cameras with 3-wide×2-forward range; alarm budgets; door-crossing unlock; 2-action stationary Smoke and 2-action EMP; free gadget activation; Ghost/stars; normal mobile controls, instant-target gadget deployment and no release Undo/hints/checkpoints.

**Current limitation:** Browser solver validation proves reachable exits in its own rules engine, but **does not establish that obstacles make every stage enjoyable**. Human playtesting and Unity replay/validator parity remain necessary. Final asset/art direction is deliberately postponed.

## 18. Copy-and-paste starting instruction for Claude Code

> You are working inside my **existing Unity Shadow Steps project**, not creating a new project. Read `SHADOW_STEPS_CLAUDE_CODE_HANDOVER_V4.md` in full, then inspect the repository's current rules engine, level data, guard schedule, solver, input/UI and tests. Treat the approved handover as gameplay authority; use `shadow_steps_30_level_playtest_v4.html`, `shadow_steps_levels_v4.json`, `level_index_v4.csv` and `v4_validation_report.json` as the latest browser-playtest references, and use `RULES_AND_GENERATOR_REFERENCE_LEGACY.md` only to identify what needs migrating. First report a concise implementation plan and incompatible legacy rules. Then implement in milestones: canonical deterministic core, tests, converter and 30 levels, solver parity, mobile controls/visualization and save/progression. Preserve existing project conventions and working packages. Run tests after each milestone. Do not invent success claims. **Do not finalize or generate production art; we will return to art later.**

**Handover package contents:**

- `SHADOW_STEPS_CLAUDE_CODE_HANDOVER_V4.md` — canonical implementation requirements (this document)
- `CLAUDE_START_HERE.txt` — shorter VS Code starter prompt
- `shadow_steps_30_level_playtest_v4.html` — latest runnable single-file browser game
- `shadow_steps_levels_v4.json` — **30 normalized V4 definitions, with all 55 solid blocked nodes and current par/Ghost metadata**, not final Unity schema
- `level_index_v4.csv` and `level_index_v4.json` — authored level metadata, par and Ghost offerings
- `v4_validation_report.json` — V4 browser solver/obstacle/guard-occupancy checks
- `RULES_AND_GENERATOR_REFERENCE_LEGACY.md` — old Unity implementation notes, deliberately labeled legacy
- `CLAUDE_START_HERE.txt` — use this quick-start with Claude Code
- `V4_OBSTACLE_CHANGELOG.md` — short delta-only summary

**Version freeze:** V4 level geometry/playtest reference verified 30 October 2026. Gameplay rules are ready to hand over; **level fun/difficulty and art are not finalized**.
