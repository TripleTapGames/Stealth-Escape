# Shadow Steps — Level curriculum (V2 detection rule)

Generated 2026-10-28 by `Tools/generate_levels.sh` from `Tools/LevelSpecs/tutorial_v2.json` and `Tools/LevelSpecs/world1_v2.json` (seed 20261028). Every number below is measured by the C# solver under `rules.detection: beforeOrAfterGuardMove`; the traces live in `validation_v2.json` / `validation_tutorial_v2.json`. Regenerating with a different seed or spec changes the levels; update the measured sets in `PackagedPackTests` and `TutorialPackTests` deliberately.

## Design rule that shaped everything
Under the V2 rule a node is lethal if it is lit **now or after the guards move**. "Slip past as the guard turns" no longer exists, so every level needs *dark gaps*: slow rotators (`rotateEvery 2`), slow patrols (`moveEvery 2`), walls/cover that cut cones, and gadgets as the way through. The generator only keeps a board when the guards actually change the solution (`guardsMatter`), at least 55% of the board is reachable (`minReachRatio`), the door is the exit's only approach, and the required mechanic is proven by constrained solves.

## Learning curve
| Tier | Levels | New idea | Guards | Par band |
|---|---|---|---|---|
| Tutorial (chapter 0) | 101–105 | move & cone (101), WAIT vs slow rotator (102), cross a patrol lane (103), key & door (104), smoke (105) | 1 (105: 2) | 8–12 |
| Easy | 1–4 | fixed cone, slow rotator, ping-pong lane, two patrols | 1–2 | 8–10 |
| Easy/medium | 5–8 | cover, two patrols at different speeds, loop route, one-way edges | 2 | 10–12 |
| Medium | 9–12 | key/door with watchers, key + timing, smoke, EMP charge (optional) | 2–3 | 11–12 |
| Medium/hard | 13–16 | three guards with key, cover + smoke, three rotators, smoke as a shortcut vs. required | 3 | 12–15 |
| Hard | 17–20 | three slow rotators, mixed patrol speeds + loop, four guards with key + smoke, finale with one-way edges and every gadget | 3–4 | 14–21 |

## Measured table
| Id | Name | Board | Guards | Par | Forced mechanics | Shortest solutions |
|---|---|---|---|---|---|---|
| 101 | Behind the Watcher | 5×5 | fixed | 8 | — | 5 |
| 102 | Clockwise Window | 5×5 | rotate ×2 | 9 | WAIT | 1 |
| 103 | Crossing Time | 6×5 | pingPong | 10 | WAIT | 1 |
| 104 | Locked Route | 6×6 | rotate ×2, key/door | 11 | WAIT, key | 1 |
| 105 | Cloud Corridor | 7×6 | fixed, pingPong, smoke | 12 | smoke | 1 |
| 1 | First Watch | 5×5 | fixed | 8 | — | 19 |
| 2 | Clockwise Eye | 5×5 | rotate ×2 | 10 | WAIT | 1 |
| 3 | The Corridor | 5×5 | pingPong | 9 | WAIT | 1 |
| 4 | Double Rhythm | 5×5 | moveRotate (legacy) | 8 | — | 8 |
| 5 | Two Watchers | 5×5 | fixed, rotate ×2, cover | 10 | — | 1 |
| 6 | Crossing Paths | 6×5 | pingPong, pingPong (moveEvery 2) | 11 | WAIT | 1 |
| 7 | Square Route | 6×6 | loop, fixed | 11 | WAIT | 1 |
| 8 | Side Step | 6×6 | pingPong (moveEvery 2), rotate ×2, one-way | 12 | WAIT | 1 |
| 9 | The Brass Key | 6×6 | fixed, rotate ×2, key/door | 12 | key | 1 |
| 10 | Locked Timing | 6×6 | 2 pingPong, key/door | 12 | WAIT, key | 1 |
| 11 | Smoke Passage | 6×6 | fixed r3, rotate ×2, smoke | 11 | smoke | 1 |
| 12 | Power Cut | 6×6 | rotate ×1, rotate ×2, fixed, EMP | 12 | WAIT | 1 |
| 13 | Guarded Vault | 7×6 | fixed, rotate ×2, pingPong, key/door | 13 | key | 1 |
| 14 | Smoke and Mirrors | 7×6 | fixed r3, pingPong, rotate ×2, cover, smoke | 12 | smoke | 1 |
| 15 | Blackout Window | 7×7 | rotate ×1, rotate ×2, pingPong, EMP | 15 | WAIT | 1 |
| 16 | The Service Door | 7×7 | fixed r3, pingPong r2, rotate ×2, key/door, smoke | 15 | WAIT, smoke, key | 1 |
| 17 | Three Blind Angles | 7×7 | rotate ×2, rotate ×2, rotate r3 ×1, cover | 16 | WAIT | 1 |
| 18 | Shift Change | 7×7 | pingPong, pingPong (moveEvery 2), loop | 14 | WAIT | 1 |
| 19 | High Security | 8×7 | fixed r3, rotate ×2, 2 pingPong, key/door, smoke | 20 | smoke, key | 2 |
| 20 | Final Extraction | 8×7 | rotate ×2, rotate r3 ×1, pingPong, loop, cover, one-way, key/door, smoke, EMP | 21 | smoke, key | 1 |

"rotate ×2" = `rotateEvery 2`; "r3" = vision range 3. EMP is never forced because EMP cannot change the reachable state space (KNOWN_ISSUES #19); it is carried on 12, 15 and 20 as an optional charge until its rule is revised.

## Known soft spots for playtest
- Several solutions open with one or two WAITs (2, 6, 8, 10, 17, 18): the guard starts in a pose that must pass first. The generator penalises three or more consecutive WAITs; shorter idles are intentional timing lessons.
- Par dips inside tiers (3 < 2, 11 < 10, 18 < 17) are accepted: difficulty is driven by guard count and mechanics, not only by length.
- Levels 1 and 4 keep their legacy loose solution counts (19 and 8); every generated level has a unique shortest solution (19 has two), which is good for intent but may feel strict. Loosen `maxSolutions` in the spec if playtests say so.
