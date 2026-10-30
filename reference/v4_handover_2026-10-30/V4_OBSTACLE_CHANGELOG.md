# Shadow Steps V4 — Blocked node change log (for Claude Code)

**Changed:** The level content is upgraded from V3 to V4, adding physical solid cover as `X` in `rows`. **Unchanged:** the approved core gameplay and mobile input rules. **Art:** still deferred.

- 5 FTUE + 25 main levels; 55 `X` tiles on 25 stages. Full normalized content is in `shadow_steps_levels_v4.json`.
- `X` differs from `#` (structural wall) in appearance/authoring, but both are nonwalkable and opaque. Distinct from `cuts` (missing movement edge), which restricts player/guard travel without necessarily obstructing sight.
- `X` blocks both guards' 2-node straight LOS and wall cameras' per-cell 3×2 LOS; any touching ray/edge/corner is blocked. It also blocks Smoke throws/targets.
- `X` may not host player, exit, key, guard waypoint, or Smoke cloud. Do not generate node markers/route connections through it; crate placeholder art should visually convey cover.
- **IMPORTANT:** V4 JSON already has the obstacle cells placed and straight-guard paths extended/normalized. Do not re-run V3 overlay or patrol extension on this data.
- `optimalActions` and `ghostAvailable` now reflect V4 browser playtest; recompute independently in Unity and report parity, rather than reusing older HUD numbers.
- Verified locally against the V4 HTML logic: 30/30 solutions, par agreement, 55 cover nodes, no overlap on guard nodes in 120 checked phases per level. This is NOT Unity parity and does not guarantee engaging levels.

See `SHADOW_STEPS_CLAUDE_CODE_HANDOVER_V4.md` for the full canonical implementation plan, tests and all earlier finalized gameplay rules.
