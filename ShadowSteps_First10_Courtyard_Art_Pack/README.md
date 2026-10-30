# Shadow Steps — First 10 Campaign Levels in the Original Courtyard Art Direction

This is an **implementation handover and 10 exact layout blueprints**, not a claim that the 10 Unity scenes have been built in your local repo. The pack provides the actual V4 source stage data and instructions so Claude Code can generate 10 3D scenes **using your imported Synty assets and character pack**.

## Selection
- Canonical stage IDs **1–10** in V4 campaign order.
- **FTUE 1–5**, then **Main Levels 1–5** (including Main Level 2, `Clockwise`).
- Source rows, wall cells `#`, blocked cells `X`, start/exit, guard rules, and optimal-action par are preserved exactly in `Data/first10_v4_levels.json`.
- `Data/first10_art_manifest.json` adds non-gameplay decoration choices only.

## Read first
1. `References/Original_Gameplay_Art_Reference.png` — original art target.
2. `References/Empty_Courtyard_No_Interior_Bushes.png` — approved clean central floor and perimeter greenery.
3. `CLAUDE_FIRST10_IMPLEMENTATION_PROMPT.md` — copy into Claude Code.
4. `ART_DIRECTION_RULES.md` — non-negotiable appearance and placement rules.
5. `Level_Layout_Blueprints/` — exact V4 layout drawings, not perspective renders.

## Level scope
| # | Original V4 campaign stage | Board | X | Guards | Optimal | Courtyard sub-variation |
|---:|---|---|---:|---:|---:|---|
| 1 | FTUE 1 — First Steps | 4×1 | 0 | 0 | 3 | Entrance Walk |
| 2 | FTUE 2 — Follow the Lines | 3×3 | 0 | 0 | 6 | Side Passage |
| 3 | FTUE 3 — Stay Out of Sight | 5×5 | 1 | 1 | 8 | Guard Court |
| 4 | FTUE 4 — Watch the Rotation | 5×5 | 1 | 1 | 5 | Clockwise Pavilion |
| 5 | FTUE 5 — Moving Patrol | 5×5 | 1 | 1 | 6 | Patrol Terrace |
| 6 | Level 1 — Safe Side | 6×4 | 2 | 1 | 9 | Side Garden |
| 7 | Level 2 — Clockwise | 6×4 | 2 | 1 | 7 | Clockwise |
| 8 | Level 3 — One Step Ahead | 6×4 | 3 | 1 | 11 | One Step Ahead |
| 9 | Level 4 — Double Trouble | 7×5 | 3 | 2 | 14 | Double Trouble |
| 10 | Level 5 — Corner Patrol | 7×5 | 2 | 1 | 8 | Corner Patrol |

## Important limitations
- These 10 PNGs are **2D level layout blueprints**, *not* screenshots from Unity or final rendered 3D art.
- Your Unity project contains newer Art3D scripts, Synty prefab references, and materials that were not uploaded into this conversation. Claude must inspect that project and integrate with the current versions, not overwrite them with the older starter code.
- You must own and import the Synty assets locally; they are **not bundled** in this handover.
- The real reference images are **visual only**. Do not use them as a single flattened gameplay background.
- For first 10, prioritize readable fixed camera; later-stage camera pan is a future separate task.
