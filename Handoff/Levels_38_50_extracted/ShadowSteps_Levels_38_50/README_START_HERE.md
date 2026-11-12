# Shadow Steps — Original level designs 38–50

**Version:** R1, 11 November 2026  
**Status:** CANDIDATES ONLY. Exact imported C# rules, actual level solvability and official pars must be checked with Claude's Unity solver before production approval.

Levels 36 (Garden Gambit) and 37 (The Locked Flank V2) are preserved without changes and are included in the combined reference JSON.

## Campaign difficulty curve

| Level | Title | Board | Nodes | Guards | Cameras | Local shortest* | Tactical purpose |
|---:|---|---|---:|---:|---:|---:|---|
| 38 | Crossed Blades | 11×9 | 20 | 2 | 0 | 6 | KO flank vs longer no-KO bypass; crossing patrol schedule |
| 39 | Eyes on the Junction | 13×9 | 23 | 2 | 1 | 9 | Camera exposure versus longer Ghost escape; main KO required |
| 40 | Sweep and Strike | 13×9 | 23 | 2 | 1 | 7 | Sweeping camera and patrol cadence |
| 41 | Key Under Watch | 13×11 | 25 | 2 | 1 | 11 | Mandatory key retrieval + rotating camera |
| 42 | Three-Way Watch | 13×11 | 25 | 3 | 1 | 11 | Three-guard junction; KO route rewarded over detour |
| 43 | Smoke at the Fork | 13×11 | 25 | 3 | 1 | 11 | Mandatory Smoke over a watched central bridge; unreachable watch guard |
| 44 | Overlapping Lenses | 13×11 | 25 | 3 | 2 | 11 | Two-camera overlapping surveillance; three-guard escape |
| 45 | Gatehouse Circuit | 15×11 | 25 | 3 | 2 | 12 | First 15-column horizontal panning; mandatory key and guarded exit |
| 46 | Power Cut | 15×11 | 25 | 3 | 2 | 10 | Mandatory EMP to cross a central camera with one-action alarm |
| 47 | Broken Line of Sight | 15×11 | 25 | 3 | 2 | 12 | Long key detour, 3 guards, 2 cameras; gadgets optional strategy |
| 48 | The Four Sentinels | 15×13 | 25 | 4 | 2 | 10 | Four distinct guards and tactical flanking on a tall board |
| 49 | Three Eyes, One Key | 15×13 | 25 | 4 | 3 | 14 | Three security cameras, four guards, mandatory southern key |
| 50 | The Last Exit | 15×13 | 25 | 4 | 3 | 16 | Final key + required Smoke + required EMP + guarded bridges |

*Local shortest = independent provisional Python simulation, **not** authoritative Unity C# par. The browser reviewer is for visual and phase inspection, not a replacement for Unity gameplay validation.

## Important design decisions

- **No WAIT**; movement/knockout consumes one action; activation of Smoke/EMP does not.
- Knockouts only when entering an occupied guard node from behind or a side. Never from in front. Enemy phase updates after each counted action.
- Guard sight is two raw grid squares straight forward, checked both before and after movement.
- Camera sight is a **70° cone reaching 3 grid squares from the wall face**, occluded by walls `#`, cover `X`, closed doors and Smoke. Alarm starts a countdown rather than immediate capture.
- Cameras sit on `#` wall cells with `mountFace` adjacent to a real `.` node. Their 2D wall/pole sprite is decorative only.
- **Levels 43 and 50 contain isolated guard sentries** who cannot be knocked out through the graph geometry. This is deliberate: do not reconnect their nodes with automatic walkway creation.
- Smoke is required by local solver on Levels 43 and 50. EMP is required on Levels 46 and 50; this must be independently proved in the C# solver too.
- Levels 45–50 are 15 columns wide and intentionally require horizontal camera panning in portrait. Do not vertically pan; use two-finger horizontal drag and player follow.
- Use the four existing background art themes with an explicit per-level theme mapping; adapt/extend visually, do not stretch a portrait image to a 15-column world.
- All levels have at most 25 playable nodes, and map rows are at most 16×16. No new player abilities or guard immunity rules.

## Review considerations

1. **Correctness first:** run C# structural validation and solver, rederive optimal actions, stars, Ghost offers, required Smoke/EMP, and lockdown feasibility. Independent results can differ due to exact occlusion/timing edge cases.
2. **Playtest decision quality:** does each extra enemy alter routing or timing? If any guard/camera can be removed with no effect, flag it for redesign instead of decorating the board with empty threats.
3. **Performance/readability:** guard and camera facing, NEXT tiles, walls, cover, the exit door and camera cones must remain legible in portrait view at correct scale.
4. **Puzzle constraints:** Never silently change existing levels 1–37, the camera cone rules, node topology, selected background, or knockout gameplay.
5. **No score invention:** local shortest-route estimates are provided for QA only. Official par, star thresholds and Ghost badge must come from actual project solver.

## File organization

- `Levels_38_to_50_Candidates.json`: importer-ready candidate array, 13 new levels.
- `Levels_36_to_50_Combined_REFERENCE.json`: exact previous 36/37 plus new 38–50, for continuity; don't re-import 36/37 if already registered.
- `Individual_Levels/`: each level separately.
- `LOCAL_VALIDATION.json` and `EXAMPLE_SOLUTIONS.json`: provisional test results.
- `ShadowSteps_38_50_Interactive_Review.html`: standalone offline visual review with selectable stages, guard/camera phases, action-by-action route playback and horizontal board panning.
- `ShadowSteps_38_50_Atlas.png`: visual grid index. `LEVEL_50_Preview.png`: detailed final layout.
- `CLAUDE_CODE_IMPORT.md`: one-step integration instructions for Claude.
- `build_levels.py`, `tune_levels.py`, `publish_pack.py`: independent reference scripts for reproducible design/provisional test; no Unity runtime dependency.
