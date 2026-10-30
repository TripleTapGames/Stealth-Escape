# Shadow Steps — 3D presentation layer (Art3D)

The 3D layer is a **visual adapter** on top of the unchanged V3/V4 rules engine. It reads the same `Stage` definitions
(`levels_v4.json`), mirrors the same `GameSessionController` events the HUD uses, and draws every hazard cell the
engine hands it. It never decides movement, visibility, collisions or solver results (D-030).

## One-time setup in the Unity Editor (cannot be done from the command line)

1. Open the project. `Packages/manifest.json` now lists `com.unity.cloud.gltfast` 6.8.0; let Package Manager resolve it
   and let the 26 `.glb` files in `Assets/_ShadowSteps/Art3D/Models/` import (each becomes a model prefab).
2. **Shadow Steps ▸ 3D ▸ Validate imported models** – reports missing models, models without renderers and non-URP
   materials (pink). All 26 should be `ok`.
3. **Shadow Steps ▸ 3D ▸ Create or update Art Palette** – fills `Assets/_ShadowSteps/Art3D/SS3DArtPalette.asset` with
   the models and, if present, `characters_5_02/Prefabs/m_1` (spy) and `m_2` (guard). Change the character prefabs,
   `characterTargetHeight` (0.95 tiles) and `characterYawOffset` on the asset if the models face the wrong way.
4. **Shadow Steps ▸ 3D ▸ Create demo scenes (Clockwise + Camera Basics)** – writes
   `Assets/_ShadowSteps/Art3D/Demo/Clockwise_Rotating_Guard.unity` and `Camera_Basics.unity`: static art previews of
   LEVEL-2 and LEVEL-16 with the courtyard frame, the fixed angled orthographic camera and the engine's initial hazards.
5. **Shadow Steps ▸ 3D ▸ Create demo scenes for all 30 stages** – same for every stage.
6. **Shadow Steps ▸ 3D ▸ Enable 3D presentation in Game scene** – sets `Use3DPresentation` and the palette on the Game
   scene's composer. Press Play: Bootstrap → Home → stage; the 2D sprite board is hidden and the 3D view mirrors the
   session (moves, guard NEXT/rotation markers, camera heads, doors, key, smoke clouds, red/amber hazards, long-press
   preview, smoke targets, EMP highlights). Toggle the flag off to return to the 2D board.

## Rendering pass (milestone 4)

- **Shadow Steps ▸ 3D ▸ Configure URP rendering** writes HDR, depth texture, soft shadows, 80 m shadow distance, 2 cascades, MSAA 2 and an SSAO feature into every pipeline asset and renderer under `Assets/Settings`. Auto-setup runs it once on the upgrade to generator version 6; it is safe to run again.
- The camera is perspective (26°) by default; `usePerspectiveCamera` / `fieldOfView` on the palette switch back to orthographic. Post-processing (ACES, bloom, grading, vignette, depth of field) is a runtime global `Volume` named `SS3D_PostProcessing`, tuned from the palette's "Rendering" section; `postProcessing = false` disables it.
- The floor is procedural stone (`proceduralFloorTiles`); set it to false to use the glb floor tiles.
- Demo scenes rebuild themselves on load (`SS3DPreviewController.rebuildOnLoad`), which marks the scene dirty; that is expected for previews.

## Environment kit (milestone 5)

- Import POLYGON Fantasy Kingdom to `Assets/PolygonFantasyKingdom` (keep the folder name). On the next script reload auto-setup converts its materials to URP Lit, maps the kit into the palette and regenerates the demo scenes; the same steps are under Shadow Steps ▸ 3D ▸ Kit.
- Kit pieces are fitted from their measured bounds (`SS3DKitFit`), so swapping any slot for another prefab needs no size tuning. `kitScale` sets kit metres per tile (0.5), `kitTint` the warm bias on textured materials, `kitFacadeYaw` / `kitStairsYaw` fix a piece that faces the wrong way.
- Turning `kitMode` off restores the milestone-3 procedural trim; each kit slot also falls back individually when empty.

## First10 sunlit courtyard scenes

- `ShadowSteps_First10_Courtyard_Art_Pack/` sits at the project root. Shadow Steps ▸ 3D ▸ **Generate First 10 Courtyard Levels** (auto-run once by version 10) writes ten art-preview scenes to `Assets/_ShadowSteps/Art3D/Levels/First10/` and the validation report to `Docs/Reports/First10_Scene_Manifest.json`.
- The theme is the palette's `sunlitCourtyard` flag (plus orthographic camera, padding 1.5, minimum framed 6 × 4). Turn it off to return to the castle kit layout; both keep `kitMode` for prop fitting.
- Playable levels: the Game scene with `Use3DPresentation` renders the same theme; the First10 scenes are previews only.

## Files

| File | Role |
|---|---|
| `Art3D/Scripts/Runtime/SS3DArtPalette.cs` | Modular prefab palette (ScriptableObject) incl. overlay colours. |
| `SS3DGrid.cs` | The only logical→world mapping: `(x − (w−1)/2, 0, (h−1)/2 − y)`, N/E/S/W = +Z/+X/−Z/−X, plane ray picking. |
| `SS3DLevelRenderer.cs` | Builds floor, walls, cover crates (only on `X`), node markers, connections, doors, exit, key, characters, camera bracket + head; setters for state; pooled hazard overlays; strips colliders from art. |
| `SS3DCourtyardFrame.cs` | Parapets, arch, palace door, stairs, pillars, lanterns, banners, canals, flowerbeds — all outside tile centres, own root, toggleable. |
| `SS3DBoardCameraFitter.cs` | Fixed 63° orthographic camera fitting the board + margin into the HUD-free portrait band and safe area. |
| `SS3DPreviewController.cs` | Editor/art preview of one stage with initial hazards. |
| `SS3DSessionBridge.cs` | Runtime adapter: subscribes to the session, hides the 2D board, swaps the camera fitter, routes taps via plane raycast, tweens actors in step with the 2D animator. |
| `Art3D/Scripts/Editor/SS3DSetupMenu.cs` | The menus above. |

## Known gaps (see KNOWN_ISSUES #28–#31)
- Nothing has run in Unity yet; glTFast import, material look, character facing and camera framing need a visual pass.
- Characters play the pack's `Idle`/`Walk` states; there are no turn, caught or escape animations yet.
- The three "camera on a walkable cell" warnings from the starter package do not apply here: the V4 importer already
  turns that notch cell into a `#` wall, so brackets mount on real walls and vision is unchanged (KNOWN_ISSUES #21).
