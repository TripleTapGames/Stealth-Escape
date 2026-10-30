# Approved 3D Art Direction — Sunlit Palace Courtyard

## Source of truth
The supplied `References/Original_Gameplay_Art_Reference.png` is the **primary look reference**, not the currently rendered brown medieval Synty courtyard. `References/Empty_Courtyard_No_Interior_Bushes.png` clarifies the user's later request that decorative bushes remain in the exterior, not in the central play area.

## Must match
- Premium colorful 2.5D miniature architectural composition; slightly angled orthographic 3D, NOT a flat-downward medieval strategy board.
- Ivory / warm cream stone courtyard tiles, gentle visible grout, beveled platform, architectural trim, warm amber lanterns, warm afternoon sunlight and soft distinct shadows.
- Palace entrance at FAR/NORTH side: arched wooden doors, blue-and-gold banners and a short red carpet **outside the playable grid**. Red carpet is decorative; it must NOT imply victory if the actual exit is on a different node.
- Perimeter: turquoise-water channels / waterfall niches, matching low sandstone walls and railings, tasteful flowers, topiary trees and bushes only on the **outside** of the playable board.
- South/front: integrated staircase and pillars leading visually toward the courtyard, but not obscuring gameplay.
- Nodes / lines: dark charcoal subtle connections and readable circular nodes; exact source graph, never inferred from floor seams. White player circle, softly glowing green actual exit.
- Characters: existing imported `characters_5_02` for spy and guards where suitable, not 3D proxy cylinders. Guard cap / visor or facing silhouette must be readable from camera.
- Obstacles: place low silhouette planter / crate / short stone blocker ONLY for authored `X` or `#` cells. Do not create an enclosure of giant dark boulders around gameplay. A ring of rocks contradicts the reference and hurts node visibility.
- Keep walls `#` visually distinct from blocked nodes `X`: walls read as nonwalkable masonry; X as cover objects (planter/crate) of sufficiently low height.
- No repeated checkerboard of bushes outside; larger grouped perimeter foliage spaced intentionally.
- Water should read rich saturated turquoise, not flat grid of blue rectangles. Use compatible shaders/materials, depth highlights, soft reflection where affordable.

## Integration constraints
1. Use imported licensed Synty Fantasy Kingdom assets selectively and non-destructively (as prefabs/prefab variants), NOT the entire supplied medieval demo castle. Do not copy the paid asset contents into handover packages.
2. User's `characters_5_02` characters remain assigned and not overwritten by palette regeneration.
3. Existing gameplay engine and current V4 level source remain authoritative; no new duplicate solver/rules. Implement only Art3D visual generation and scene previews/integration.
4. For first 10, generate all visuals from a shared **SunlitCourtyard** theme plus small per-level appearance variants; no bespoke scene-specific geometry that cannot regenerate.
5. Use deterministic cosmetic seed based on stage ID for variation; do not randomize gameplay obstacles/visibility.
6. Preserve 9:16 and 9:19.5 support, HUD-safe top/bottom bands, unobstructed character and hazard silhouettes.
7. Keep current guard LOS: 2 nodes in a straight line, occluded by real wall/cover. Do NOT render a broad inaccurate triangular red fan. A soft spotlight for atmosphere is allowed only if the exact dangerous nodes have authoritative overlays.
8. No cameras/gadgets are authored in the first 10 V4 stages, so do not add camera models, alarms or gadget charges to those stages. The existing HUD and gameplay should remain capable of supporting them in later stages.
9. Prefer fixed camera in these 10 small levels. Do not implement user-requested large-level camera follow or level enlargement as part of this assignment; isolate that as a later feature.

## Layout / content invariants
- `rows` are read in V4 data order. A dot `.` is walkable; `#` is a blocking wall cell; `X` is nonwalkable solid cover.
- Physical X and # block movement and sight. Decorative bushes on board NEVER placed unless authored X.
- A connection edge exists only if the gameplay graph has it; do not assume every pair of adjacent walkable nodes connects.
- Guard `type`, `dir`, `path`, `offset`, `ccw` copied exactly. No visual guessing about guard routes.
- `start`, `exit`, `optimalActions`, level names and canonical order preserved.

## Aesthetic acceptance target
Compare screen captures side-by-side to primary art reference: camera angle, architectural detail, sunlight/shadows, water/flowers, vibrant palette, readable board. Functional scene generation alone is not art approval.
