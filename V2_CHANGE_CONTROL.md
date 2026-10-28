# Shadow Steps V2 — Change Control and Migration Contract

Date: 2026-10-28. Purpose: integrate new decisions into an already-running Unity/Claude Code task **without losing completed work**.

## Frozen product decisions
- Mobile portrait, turn-based, deterministic stealth puzzle. Player acts; then enemies update.
- Camera is **strict top-down orthographic**, not perspective/isometric; all runtime board/character art is **2D sprite-based** (Unity 2D renderer/URP 2D where practical). Preserve the existing Universal 3D project/template if already created; a full template restart is **not required**. Orthographic 2D sprites can work in the existing URP project; change renderer configuration only after testing compatibility. Avoid mixing 3D depth/occlusion into authoritative gameplay.
- Square lattice of nodes with uniform `nodeSpacing` per level, default 1.0 Unity unit. Only cardinally adjacent connected nodes are traversable; every step = exactly one `nodeSpacing`. No diagonal, variable-distance or continuous movement. A path between adjacent nodes can be absent, locked, one-way or switchable. A missing edge stops movement **but does not automatically block sight**.
- Guard behavior data-driven: fixed facing; rotate in place; ping-pong straight route; move + rotate; loop route. Move one edge per eligible turn; patrol route segments MUST correspond to legal edges. Configure move/rotation periods independently. WAIT advances enemy turns. Capture/contact and edge swaps remain lethal.
- Vision is a discrete 90° forward cone, per-guard range, line of sight blocked by explicit occluders. Preserve the reference discrete formula and ordering where sensible. Occluders, not graph missing links, determine sight blockage. Visual cone is informative only; deterministic C# simulation owns detections. **Detection timing (2026-10-28 design change):** after the player acts, a cone captures if the player's node is in guard vision either before or after the guards move (`rules.detection: beforeOrAfterGuardMove`, schema 2 default). This is stricter than V1 and invalidates most V1-era content until it is redesigned (KNOWN_ISSUES #18). The V1 single after-move check remains available as `afterGuardMove` and is what schema 1 files mean; parity tests keep running under it.
- Player gadgets: Smoke, EMP in V1 shipping scope; configurable decoy is reserved until implemented and validated. Keep key/door, exit, undo/restart, progression, preview.
- Build original art: top-down readable silhouettes, high contrast colored guard cones, visible nodes and valid paths, board-first aesthetics. Decorations never change authoritative traversal or visibility unless authored as occluders.

## Compatibility / conflicts
1. Legacy docs suggesting 3D toy dioramas, meshes, angled camera or optional tile adjacency are **superseded**.
2. Existing legacy `reference/levels.json` and `validation.json` represent **V1 grid rules**, not guaranteed to be valid under explicit V2 edges; never present prior 20/20 validation as V2 proof.
3. Keep legacy loader/simulation test fixtures (`schemaVersion: 1`) functioning if practical. Upgrade to `schemaVersion: 2` with `nodeMask`, `edges`, `occluders`, `nodeSpacing`, and versioned semantics. Default legacy grid edge generation ONLY for legacy imports and record what was converted.
4. Save games should carry a version; migrate safely if possible, else clearly communicate/reset incompatible in-progress turns while preserving level unlock progress. Back up user save before altering.

## Claude's first action on an existing repo
- Inspect `git status`, current running task/progress notes, Unity version, scene and renderer, runtime dependencies, level data, and tests.
- Make a non-destructive checkpoint (commit only with user-approved repository policy, or create a diff/backup); never overwrite unknown user files.
- Create `Docs/V2_MIGRATION_AUDIT.md`: classify each subsystem **reuse / adapt / replace / missing**, name exact files, tests, risks and time-independent ordered steps. Do not estimate durations.
- Continue from a safe checkpoint, rather than starting a second conflicting implementation. If there is an active Claude Code invocation, wait for its tool operation to finish or stop it safely after saving; then give it the V2 migration instruction.

## Validation gates (required)
A. Engine: graph adjacency, illegal edges, one-way/locked edges, node spacing, movement animation end positions; guard legal route edges; independent move vs. vision blocking.
B. Vision: 90° cone, occlusion, facing/phase updates, smoke/EMP effects, collision & edge swaps.
C. Level data: import/export and schema roundtrip; strict graph validation, reject disconnected references; explicit versioning.
D. Content: import five authored tutorial levels, run deterministic solver and replay each winning sequence inside Unity. Run no-gadget / no-WAIT constrained solver variants before claiming mechanic is required.
E. Presentation: top-down orthographic 2D sprite scene, HUD, readable cones/edges on small portrait screens.
F. Regression: compare old results for legacy-only mode; migrated levels must have fresh solver traces and documented differences.

## Source priority on conflicts
`V2_CHANGE_CONTROL.md` > `LEVEL_DESIGN_PARAMETERS.md` > `V2_IMPLEMENTATION_ROADMAP.md` > other legacy production docs > legacy reference solver (for its original dataset only).

## Report back each session
What was reused vs changed; exact files changed; tests run and actual results; unverified items; remaining problems; next incremental action. Never assert Unity or device tests without evidence.
