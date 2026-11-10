# Stage Editor (levels_v4.json)

Menu: **Shadow Steps ▸ Stage Editor (levels_v4)**. Edits the pack the game ships, `Assets/StreamingAssets/ShadowSteps/levels_v4.json` (schema 3, 35 stages: FTUE-1…5 then LEVEL-1…30). The older **Shadow Steps ▸ Legacy ▸ Level Editor (schema 2)** window edits the pre-V3 format and cannot open this pack.

## Workflow
1. Pick a stage from the dropdown (or ◀ ▶). The board shows every cell with its coordinate; letters mark start `S`, `EXIT`, key `K`, guard routes `A`, `A1`, `A2`… (anchor first), cameras `C1`… on their wall cell, and the facing arrows at the chosen action index.
2. Edit with the tool row:
   - **Floor . / Wall # / Cover X / Pass - | + / Empty _**: click or drag to paint. Clicking a cell that already has that type turns it back into floor; right-click always paints floor. Turning a node into something else drops the cuts, doors, key and camera that sat on it.
   - **Start / Exit / Key**: click a node (the key toggles off when clicked again).
   - **Cut / Door**: click a node, then the next node along a straight run (across passage cells too). Cut removes or restores the connection; Door locks or unlocks it (a door needs a key on the board).
   - **Route**: select a guard on the right, then click nodes to extend its route; right-click drops the last node. With no guard selected, clicking a node creates a static guard there.
   - **Camera**: click a wall cell to mount one (click again to remove); set mount face, facing and kind on the right. A floor cell clicked here becomes a wall first. A sweeping camera has a **Sweep arc**: 2 facings (facing and its neighbour, A A B B) or 3 facings (a half turn out and back, A A B B C C B B, e.g. W → N → E → N → W); the panel shows the exact sequence and period. The clockwise toggle picks which way the arc runs.
3. The right panel edits id, code, group, title, intro, size (content is kept when resizing), smoke / EMP charges, alarm budget, wait limit, guards, cameras, doors, cuts and notes.
4. **Show sight** tints guard sight orange and camera sight magenta at the chosen action index `t` (slider over one full period). Cuts are red ✕, locked doors yellow `L`.
5. **Validate** runs the structural checks plus the solver under the shipped rules (Knockout, no WAIT) and highlights offending cells in red. **Solve** prints the par, the solver's route and whether Smoke / EMP / knockouts are needed; **Step solution** walks that route in the simulator.
6. **Simulate**: Reset, N E S W, EMP#i, Smoke targets, Undo. Uses `StageRules.TryApply`, the same code the game runs, so what you see here is what the player gets.
7. **Recompute scoring** after any layout change: the game scores stars against `par` / `knockoutPar`, and the pack test (`V3PackParityTests`) expects them to match the solver.
8. **Save pack** writes `levels_v4.json`; the previous file is copied to `Backups/levels/levels_v4_<timestamp>.json` (outside Assets, git-ignored). **Save + export validation_v4** also regenerates `validation_v4.json`, which the tests and the 2D prototype read.
9. **▶ Play stage** saves (if needed), opens the Game scene and enters Play Mode on this stage. Installed builds are unaffected.

Add / Duplicate / Delete / Move up / Move down manage the stage list. Ids are what save data and the level select use: keep the ids of shipped stages stable, and add new stages with new ids.

## Equivalent command line
`Tools/run_core_tests.sh` builds `Tools/CoreTestRunner`; then:
- `dotnet /tmp/shadowsteps_coretests/bin/CoreTestRunner.dll --v3-validate Assets/StreamingAssets/ShadowSteps/levels_v4.json`
- `… --v3-rescore <pack> <out.json>` rewrites every scoring block the way the editor's Recompute does (diff the output against the input).
- `… --v3-export-validation <pack> Assets/StreamingAssets/ShadowSteps/validation_v4.json`

Both the editor and the runner call `StagePackTools` (Data assembly), so they cannot drift apart.

## Caveats
- The solver explores up to 250 000 states per solve; a very open 9×11 board can take a few seconds per Validate / Solve / Recompute.
- Saving re-serialises the whole pack with the C# writer (short arrays inline). The first save after a hand-formatted file shows a whitespace-only diff in git; the data is identical.
- The editor does not touch `levels.json` (schema 2) or `levels_tutorial_v2.json`; those are legacy test fixtures.
