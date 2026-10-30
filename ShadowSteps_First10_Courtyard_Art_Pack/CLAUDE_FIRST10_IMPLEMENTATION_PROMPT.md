# Claude Code — IMPLEMENT FIRST 10 V4 LEVELS IN THE APPROVED 3D ART STYLE

I am providing `ShadowSteps_First10_Courtyard_Art_Pack` in my ShadowSteps Unity project root (not inside Assets).

My **PRIMARY visual reference** is `References/Original_Gameplay_Art_Reference.png`: a premium colorful miniature palace courtyard, warm ivory stone, turquoise waterways and natural green perimeter landscaping, blue/gold banners, wood entrance and soft sunset lighting. The clean-floor reference is `References/Empty_Courtyard_No_Interior_Bushes.png`.

I want the **first 10 real 3D Unity levels**, NOT just more concept sketches or a single repeated medieval castle scene. These 10 refer to canonical V4 stage IDs 1–10: FTUE 1–5, followed by Main Levels 1–5. Source data is attached in `Data/first10_v4_levels.json`, and per-level cosmetic specifications are in `Data/first10_art_manifest.json`. Layout PNGs are supplied solely to cross-check node/obstacle geometry.

## Execute in this exact order

### A — Read current project; no blind overwrite
1. Inspect current Unity version, URP, `Assets/_ShadowSteps/Art3D`, existing scene generator, palette, level loader, V4 runtime source, the already imported Synty Fantasy Kingdom package, and `characters_5_02`.
2. Check latest code including the artist/palette preservation changes; do not copy outdated Art3D scripts from the original prototype ZIP over newer ones.
3. Make a Git commit/checkpoint before edits. Document selected existing Synty prefab paths after an AssetDatabase audit, and list missing categories. Do not assume exact Synty prefab filenames.
4. Compare attached `Data/first10_v4_levels.json` against authoritative level assets currently used by gameplay. If they differ, report exact differences and preserve the project source as gameplay authority; do not silently overwrite gameplay data.

### B — Create a coherent reusable `SunlitCourtyard` art theme
5. Rework existing Art3D prefab palette into a primary reference-matching environment, **not** the Synty fortress/rock ring. Select Synty stone floors, arch/door, low parapet, pillars, stairs, wooden trims, lanterns, flower/trees selectively; use prefab variants, custom low-poly meshes if necessary for the exact look. Do not touch source Synty assets.
6. Theme objects: exterior teal water channels and small waterfall corners, pale stone raised play platform, light tile grout, beautiful symmetric entry façade, outer foliage and benches only OUTSIDE gameplay board, front stairs, blue-gold banners, warm URP lighting. Do not use flat plane green ground filling half the phone; no huge repeated rock borders.
7. Create deterministic variant decoration from each entry in `Data/first10_art_manifest.json`, while using the same base courtyard set for visual consistency. Maintain a clear board and strong nodes/guard visibility.
8. Reuse assigned player and guard prefabs from `characters_5_02`; never reset character selection when rebuilding the palette.

### C — Build ten actual Unity scenes
9. Extend the **current** level-to-3D scene generator with a batch menu e.g. `Tools > Shadow Steps > 3D > Generate First 10 Courtyard Levels`.
10. Generate exact 10 distinct scene assets in `Assets/_ShadowSteps/Art3D/Levels/First10/` with filenames in `Data/first10_art_manifest.json`.
11. Each must contain source-authoritative node graph, `#` walls and `X` low cover, guard models and their scheduled type/initial direction, true exit position, and shared generated environment. No gameplay-affecting decor.
12. Make generation repeatable, safe for edited/open scenes, and idempotent. Do not discard manually placed character assignments or add duplicate lights/cameras. Preserve source assets and create no new gameplay mechanics.
13. For initial stage previews use a slightly angled orthographic fixed camera that makes the board easily readable in portrait; preserve 9:16 and 9:19.5. For small FTUE grids avoid giant empty borders; use proportional architecture rather than scaling nodes microscopic. Do not implement camera follow yet.
14. Distinguish STATIC ART PREVIEW scenes from PLAYABLE scenes. If the existing runtime bindings support full gameplay, wire the rendering scene to them. Otherwise clearly label art previews and implement needed binding without changing gameplay logic. Do not claim fully playable scenes unless tested.

### D — Verify and deliver
15. Unity compile check with zero new errors. Validate 10 generated scenes and exactly 10 original V4 stage IDs. For each assert unchanged `rows`, `blockedNodes`, graph/cuts, `start`, `exit`, guards/path/phase, `optimalActions`.
16. Generate deterministic evidence: stage manifest + screenshot at 9:16 for all ten scenes if Editor/Game view access allows; screenshot at 9:19.5 for two larger boards. Do not claim visual approval based on arithmetic only.
17. Explicitly verify only authored `X` and `#` block gameplay. Foliage and water purely outside gameplay geometry. The large dark rocks around the board from the prior screenshot should disappear unless actually authored obstacles.
18. Report scene paths, assets/prefabs selected, import or render blockers, portrait readability, and list which models still look like placeholders.

**Stop after these first 10 and present result for visual review. Do not expand levels 11–30, modify puzzles or add pan-camera behavior yet.**
