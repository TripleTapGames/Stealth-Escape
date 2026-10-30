# Shadow Steps — Stylized 3D Art Direction and Technical Specification

## Target: premium colorful miniature palace courtyard

User-selected reference: `UnityDrop/Assets/_ShadowSteps/Art3D/References/Gameplay_Art_Reference.png`.
The look is **not a literal 3D extraction of the 2D concept image**. It's a target for reusable 3D models, lighting and composition.

### Nonnegotiable visual rules

1. **Actual 3D meshes**, Unity URP, fixed **orthographic 2.5D camera**, approximately 60–65 degrees downward pitch to start; aim at center of level board. No camera follow, pinch-zoom or player panning.
2. The puzzle is still an **exact cardinal grid**. The entire current level remains on screen, and 3D visuals **cannot** change simulation geometry.
3. **Central playable floor is mostly clean.** No fixed decorative bushes, pots, benches or pillars occupying usable nodes. Only place solid obstacles (crate, planter, stone block) on actual V4 `X` cells. Cells marked `#` are structural opaque walls.
4. Greenery, water, waterfalls, topiary, banners, entrance façade, stairs and occasional benches can decorate **outside the logical board** (do not introduce fake blockers).
5. Warm cream-colored stone floor, pale ivory railings, turquoise canals, soft golden afternoon light, navy/gold banners, brown wooden doors and gentle shadows.
6. Color contrast: black-suited player, blue-uniform guards, red **actual guard detection**, amber **actual camera detection**, cyan next-move/EMP indicators and green exit.
7. Each guard sees **2 discrete nodes in a straight cardinal line**. Do NOT bake a broad triangular guard cone into production art. A soft triangular decorative glow may be used only if the highlighted node cells precisely match danger.
8. Each wall camera looks at **3 columns × 2 forward rows**, with wall/door/Smoke occlusion computed by authoritative engine. Show amber solid current node borders, dashed next-turn borders in preview, and red+amber dual hazards on overlaps.
9. Guards that rotate: `↻` clockwise, `↺` counterclockwise; moving guards: a visible cyan NEXT arrow from the **next path node**, not just current facing. At endpoints they move to final accessible node before reversing.
10. Fixed camera bracket remains **attached to wall** while head turns. Sweeping cameras alternate adjacent 90° facings and hold each for **two counted actions**; rotating cameras turn 90° per counted action.
11. Mobile first: readable danger and thin node connection lines before decorative detail; no foreground foliage covering the playable board. UI sits outside board bounds.

## Geometry coordinate contract

| Property | Contract |
|---|---|
| Unity plane | XZ (Unity Y vertical) |
| JSON `(x,y)` | World X grows right, row Y grows toward Unity -Z |
| Tile size | 1 Unity unit per logical cell center spacing |
| Tile centers | `(x - (w-1)/2, 0, (h-1)/2 - y)` |
| Camera default model forward | Local +Z (Unity forward) |
| Facing `N/E/S/W` | +Z / +X / -Z / -X |
| Floor top | Approximately Unity `Y=.112` |
| Art objects | Origin at footprint center, ground Y=0 (except camera head geometry) |
| Blocked `X` | 1×1 solid, non-walkable and opaque |
| Structural `#` | 1×1 solid wall/void, non-walkable and opaque |
| Missing `cuts` | Missing edge only, blocks traversal, does NOT imply opaque wall |
| Door | At the connection midpoint. Closed door blocks movement and vision; opened door does not. |
| Camera position | Grid wall anchor + `.501` tile toward mounting face. See known warnings. |
| Exterior décor | Only outside logical tile centers; must be separate root and toggleable. |

Note: The supplied meshes are Y-up `.glb` files with plain PBR colors; no texture atlas, skeleton or animations. They can be reworked in Blender. Models/prefabs may need small per-asset origin offsets after Unity importer verification.

## Prototype mesh inventory (26 `.glb` files)

- Grid: `Floor_Tile_A`, `Floor_Tile_B`, `Node_Marker`, `Wall_Straight_1m`, `Wall_Block_1m`.
- Cover/blockers: `Obstacle_Planter_1x1`, `Obstacle_Crate_1x1`, `Obstacle_Stone_Block_1x1`.
- Courtyard architecture: `Pillar_Square`, `Bench_Short`, `Stairs_3Step_2m`, `Entrance_Arch_Frame`, `Door_Double_Wood`.
- Interactive: `Door_Locked_Path`, `Door_Open_Path`, `Exit_Pad`, `Collectible_Key`.
- Surveillance: `Security_Camera` (one-piece fallback), `Security_Camera_Mount` (static), `Security_Camera_Head` (rotating), `Lantern_Wall`.
- Perimeter: `Banner_BlueGold`, `Water_Tile`, `Flowerbed_OutsideBoard`.
- Stand-in characters: `Character_Spy_PROXY` and `Character_Guard_PROXY` (not rigged).

## Suggested hero art pipeline after gameplay validation

- Model/simplify high-quality **floor, walls, planter, crate, camera** in Blender first. Keep a single material palette and consistent pivots.
- Use the `.glb` kit as proportion/placement placeholders; improve topology, bevel edge highlights, UVs and normal maps only where visible at target phone size.
- Model the **spy** and **guard** as separate rigged low-poly characters. Use simple short walk, pivot turn, caught, idle and celebration loops. Confirm facial detail is readable before investing.
- Rig camera head on independent pivot and bracket stationary; animate only through authoritative turn signals.
- Use URP lit shaders and directional shadows with limited cascades and mobile-appropriate baked/static lighting. Keep danger overlays emissive and independent of changing shadows.
- Optimise for 30 levels reusing prefabs, not 30 standalone baked background models. Consider material atlas and GPU instancing only after profiling.
- Keep alternate environment themes in future milestones; ship courtyard kit first.

## Recommended initial color samples

- Floor cream `#F2DEBD`, tile variation `#E9D2B0`, ivory wall `#E9D5AE`, warm stone `#D8BE96`
- Canal `#3BAFC7`, navy banner `#19456B`, gold `#D6A33E`
- Spy suit `#20242A`, security uniform `#2B75BC`, green exit `#3BC482`
- Danger guard `#D95F5A`, danger camera `#E9B84F`, targeting cyan `#62D8D9`

## Negative guidelines

No Candy-style game, no realistic military aesthetic, no overly dark level, no single flattened whole-level artwork as runtime board, no arbitrary bushes placed on walkable nodes, no visual blockers that do not exist in data, no unbounded free camera rotation, no misleading continuous guard cones.
