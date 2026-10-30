# Shadow Steps — 3D Courtyard Starter Kit for Claude Code

**Status:** Delivered as a complete **starter integration package**, including 26 editable `.glb` 3D prototype models, 7 Unity C# source files (visual-only runtime/editor helpers), the latest V4 30-level JSON, photo references, source model generator and a detailed Claude plan. Models are genuinely 3D, not screenshots. They are **stylized blockouts**, not finished reference-quality assets.

## Install in the existing Unity project

1. Extract the ZIP anywhere on your Mac.
2. Open your existing Shadow Steps Unity URP project in Unity Hub (do not make a new project).
3. Copy the entire `UnityDrop/Assets/_ShadowSteps/Art3D` directory into `YOUR_PROJECT/Assets/_ShadowSteps/Art3D`.
4. In Unity Package Manager, install **glTFast** (`com.unity.cloud.gltfast`). This is the Editor importer for our `.glb` models. Also install/enable **Newtonsoft JSON** (`com.unity.nuget.newtonsoft-json`) if the project does not already provide it.
5. Wait for import and script compilation.
6. Open Unity menu **Tools → Shadow Steps → 3D → Create Both Demo Scenes**.
7. Open `Assets/_ShadowSteps/Art3D/Demo/Clockwise_Rotating_Guard.unity` and `Camera_Basics.unity`. These are *art review* scenes, not gameplay-integrated scenes yet.
8. Open VS Code with your existing Unity project, then give Claude the instructions in `CLAUDE_TASK_PROMPT.txt`, along with this ZIP extracted to a folder Claude can read.

**Important:** The core game must continue using the V4 rules engine. Don't overwrite its scripts with the example JSON DTO or turn the art preview into a separate second game.

## Package contents

| File / folder | Purpose |
|---|---|
| `CLAUDE_START_HERE.md` | Claude reading order and first actions |
| `CLAUDE_IMPLEMENTATION_PLAN.md` | Milestones, acceptance tests, known limitations |
| `ART_DIRECTION_AND_TECH_SPEC.md` | Exact model/scene coordinate and art constraints |
| `CLAUDE_TASK_PROMPT.txt` | Paste-ready instruction for Claude Code |
| `UnityDrop/Assets/_ShadowSteps/Art3D/Models/` | 26 `.glb` mesh assets |
| `UnityDrop/Assets/_ShadowSteps/Art3D/Runtime/` | Renderer, data DTO, camera fitter, preview, exterior decorator, palette |
| `UnityDrop/Assets/_ShadowSteps/Art3D/Editor/` | Auto palette creation and Unity scene generator |
| `UnityDrop/Assets/_ShadowSteps/Art3D/Data/` | 30 V4 levels as JSON |
| `UnityDrop/Assets/_ShadowSteps/Art3D/References/` | User-approved art direction + clean courtyard reference |
| `Model_Kit_Contact_Sheet.png` | Preview of all 26 assets |
| `ReferenceV4/` | Prior V4 gameplay handover, HTML and validation (reference only) |
| `Tools_generate_models.py` | Procedural source to regenerate/modify geometry |
| `validate_package.py` | Check shipped models, blocked cells and known mount warnings |
| `MODEL_VALIDATION_REPORT.json` | Generated mesh and V4 data inspection report |

## Expected limits

- The 26 `.glb` meshes are a **functional, geometric material starter kit**. To match the illustrated polished concept, Claude should iterate in Blender or with assisted 3D modeling, including more elegant bevels, lighting and UVs.
- Proxy characters have **no bone rig or animations**. They need to be replaced with separate rigged models.
- The Unity C# layer was **not compiled inside Unity** in this environment; if the user's Unity project already has classes in overlapping namespaces or GLB-import quirks, Claude must resolve them.
- The Editor scenes visualise the raw V4 layout; they do not implement controls or drive the authoritative game. Connecting snapshot events is Claude's next task.
- The V4 level camera-anchor warning is real: in three levels camera `wall` anchors are on walkable nodes. Do not silently change the game; use visual mounting props or carefully reauthor/resolve.

## Model file units

All assets authored with **Y up**, **+Z front**, footprint centered at origin, and **one grid node spacing = one Unity unit**. Floors are 1×1, blocked-cover assets fit a 1×1 cell.

## Licensing

The procedural starter meshes and related source in this package were newly generated for this project, without downloading third-party mesh files. The included image references were supplied/generated in this user's conversation; treat them as art references rather than 3D geometry or reusable model source.
