using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Stage = ShadowSteps.V3.Stage;
using Placement = ShadowSteps.Art3D.TownPlazaLayout.Placement;
using FitMode = ShadowSteps.Art3D.TownPlazaLayout.FitMode;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// The authored Town Plaza: one deliberately composed environment recreating the approved mock
    /// (ArtReferences/ShadowSteps_Town_Target.png) from real POLYGON Town Pack prefabs, with POLYGON Fantasy Kingdom
    /// pieces only where the Town pack has no equivalent (wooden crates, flower planters). Every placement names the
    /// mock element it stands for; the mapping is written to Docs/ART3D_TOWN_PLAZA_MAPPING.md on generation.
    /// Gameplay geometry (nodes, connections, cover, actors, exit) comes from the level renderer and levels_v4.json.
    /// </summary>
    public static class TownPlazaSetup
    {
        public const string TownRoot = "Assets/PolygonTown";
        public const string KingdomRoot = "Assets/PolygonFantasyKingdom";
        public const string LayoutPath = "Assets/_ShadowSteps/Art3D/Town/TownPlaza_Layout.asset";
        public const string ScenePath = "Assets/_ShadowSteps/Art3D/Levels/Town/SS3D_Town_Plaza_Authored.unity";
        public const string ReportPath = "Docs/Reports/Town_Plaza_Manifest.json";
        public const string MappingPath = "Docs/ART3D_TOWN_PLAZA_MAPPING.md";
        public const string StageCode = "LEVEL-4";

        private static readonly List<string> _missing = new List<string>();
        private static readonly List<(string element, string prefab, string pack, string note)> _mapping = new List<(string, string, string, string)>();

        /// <summary>Prefab by exact file name under a pack root (sub-folders differ between pack versions).</summary>
        private static GameObject Find(string root, string name)
        {
            foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            _missing.Add(root + " … " + name);
            return null;
        }
        private static GameObject Town(string name) => Find(TownRoot, name);
        private static GameObject Kingdom(string name) => Find(KingdomRoot, name);
        private static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(SS3DSetupMenu.Models + name + ".glb");

        private static Placement P(string name, string mockElement, GameObject prefab, string pack, Vector2 anchor, Vector2 offset, float yaw, FitMode fit, Vector3 size, Color? tint = null, float elevation = 0f, int repeat = 0, Vector2 repeatStep = default, string note = null)
        {
            _mapping.Add((mockElement, prefab != null ? prefab.name : "(missing)", pack, note ?? ""));
            return new Placement { name = name, mockElement = mockElement, prefab = prefab, anchor = anchor, offset = offset, yaw = yaw, fit = fit, size = size, tint = tint ?? Color.white, elevation = elevation, repeat = repeat, repeatStep = repeatStep };
        }

        /// <summary>
        /// Plaza anchors: x −1 west … +1 east, y −1 south … +1 north. The camera (yaw 45°) puts the west edge at the
        /// bottom-left of the screen, the north edge top-left, the east edge top-right and the south edge bottom-right,
        /// so tall pieces sit on the north and east edges (behind the grid on screen) and only low props on the west
        /// and south edges, exactly as in the mock.
        /// </summary>
        [MenuItem("Shadow Steps/3D/Town/Create or update the authored Town Plaza layout")]
        public static TownPlazaLayout CreateLayout()
        {
            _missing.Clear(); _mapping.Clear();
            SS3DKitSetup.ConvertMaterials(TownRoot);
            if (AssetDatabase.IsValidFolder(KingdomRoot)) SS3DKitSetup.ConvertMaterials(KingdomRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            var layout = AssetDatabase.LoadAssetAtPath<TownPlazaLayout>(LayoutPath);
            if (layout == null) { layout = ScriptableObject.CreateInstance<TownPlazaLayout>(); AssetDatabase.CreateAsset(layout, LayoutPath); }
            layout.displayName = "Town Plaza (authored)";
            layout.description = "Recreation of ArtReferences/ShadowSteps_Town_Target.png: stone plaza framed by a yellow house (north-west), a gated railing with a fountain beyond (north-east), a grey house (east), low railing, planters, bench, barrels and crates (west), tree and a terracotta roof in the foreground (south-east).";
            layout.stageCode = StageCode;
            layout.marginWest = 0.6f; layout.marginEast = 0.6f; layout.marginNorth = 1.0f; layout.marginSouth = 0.9f;
            layout.floorA = new Color(0.88f, 0.83f, 0.74f); layout.floorB = new Color(0.83f, 0.78f, 0.69f);
            layout.pavementA = new Color(0.79f, 0.75f, 0.68f); layout.pavementB = new Color(0.75f, 0.71f, 0.64f);
            layout.groutColor = new Color(0.60f, 0.54f, 0.46f); layout.kerbColor = new Color(0.72f, 0.68f, 0.62f);
            layout.stepWidth = 0.45f; layout.stepColor = new Color(0.70f, 0.66f, 0.60f);
            layout.outerPavement = true; layout.outerPavementA = new Color(0.69f, 0.65f, 0.59f); layout.outerPavementB = new Color(0.67f, 0.63f, 0.57f); layout.outerPaverSize = 0.5f;
            layout.groundPrefab = null; layout.groundTint = new Color(0.62f, 0.58f, 0.52f);
            layout.kitScale = 0.5f;
            layout.coverProps = new[] { Kingdom("SM_Prop_Crate_Stack_03"), Kingdom("SM_Prop_PlanterBox_Preset_02"), Kingdom("SM_Prop_Crate_Wood_01") };
            _mapping.Add(("Wooden crate stacks on blocked cells (X)", "SM_Prop_Crate_Stack_03 / SM_Prop_Crate_Wood_01", "Fantasy Kingdom", "Town pack has only cardboard boxes; the mock shows wooden crates."));
            _mapping.Add(("Stone flower planters on blocked cells (X)", "SM_Prop_PlanterBox_Preset_02", "Fantasy Kingdom", "Town pack garden boxes hold soil, not flowers."));
            layout.coverHeight = 0.5f;
            layout.wallProp = Kingdom("SM_Prop_PlanterBox_01"); layout.wallHeight = 0.5f;
            layout.connectionColor = new Color(0.93f, 0.90f, 0.84f, 1f); layout.connectionWidth = 0.028f;
            // Upright 2.5D: yaw 0°, 46° down (shallower than before so façades and characters show depth), framed from the playable bounds only:
            // 7 tiles + 2 × 0.6 padding = 8.2 tiles across 1080 px ≈ 85 % board width. The framing band starts at 30 % so the board sits low and the town fills the top; HUD bands stay 14 % / 22 %.
            layout.orthographic = true; layout.pitch = 46f; layout.yaw = 0f; layout.paddingTiles = 0.6f; layout.topReserved = 0.30f; layout.bottomReserved = 0.22f; layout.hudTop = 0.14f; layout.hudBottom = 0.22f;
            // Sun from the front-left (Euler yaw 46°, 46° high): the south-facing façades across the top are lit, shadows run up-right; the fill keeps the plaza even.
            layout.sunColor = new Color(1f, 0.92f, 0.78f); layout.sunIntensity = 1.9f; layout.sunAngles = new Vector2(46f, 46f); layout.shadowStrength = 0.75f; layout.fillIntensity = 0.45f; layout.fillColor = new Color(1f, 0.95f, 0.88f); layout.exitScale = 1.7f;
            layout.background = new Color(0.72f, 0.80f, 0.84f);
            layout.ambientSky = new Color(0.74f, 0.81f, 0.90f); layout.ambientEquator = new Color(0.82f, 0.76f, 0.66f); layout.ambientGround = new Color(0.46f, 0.42f, 0.37f);
            layout.postExposure = 0.12f; layout.saturation = 20f; layout.contrast = 16f; layout.vignette = 0.22f; layout.depthOfField = 0.35f;

            // Screen = world axes (west left, north top). Plaza for the 7×5 stage: x −4.1…4.1 (exactly the frame width), z −3.4…3.5.
            // With the board at 85 % of the width there is no room for side buildings, so the two houses frame the top with the gate between them;
            // the side margins carry low props only, the south edge carries the railing, hedge and the foreground roof.
            Color black = new Color(0.13f, 0.13f, 0.14f), yellow = new Color(1.0f, 0.88f, 0.52f), grey = new Color(0.95f, 0.93f, 0.88f), terracotta = new Color(1.0f, 0.62f, 0.42f), cream = new Color(0.98f, 0.92f, 0.80f), stone = new Color(0.82f, 0.78f, 0.72f), shopTint = new Color(1.0f, 0.74f, 0.56f);
            var list = new List<Placement>
            {
                // ---- North edge (top): yellow house (left) and grey house (right) facing the plaza, gate between stone pillars, lamps, planters; fountain, shop, church and trees beyond.
                P("House_Yellow", "Yellow two-storey house with shutters (top left)", Town("SM_Bld_House_Preset_10"), "Town", new Vector2(-1f, 1f), new Vector2(2.0f, 1.75f), 180f, FitMode.Box, new Vector3(3.8f, 3.2f, 2.2f), yellow, note: "façade faces the plaza (south); siding tinted yellow"),
                P("House_Yellow_Planter", "Flower planters along the yellow façade", Kingdom("SM_Prop_PlanterBox_Preset_03"), "Fantasy Kingdom", new Vector2(-1f, 1f), new Vector2(1.2f, 0.36f), 0f, FitMode.KitScale, Vector3.one, repeat: 1, repeatStep: new Vector2(1.7f, 0f)),
                P("House_Yellow_Lantern", "Wall lantern beside the door", Model("Lantern_Wall"), "Shadow Steps models", new Vector2(-1f, 1f), new Vector2(3.4f, 0.55f), 180f, FitMode.Height, new Vector3(0f, 0.45f, 0f), elevation: 1.3f),
                P("House_Grey", "Grey house with green shutters (top right)", Town("SM_Bld_House_Preset_09"), "Town", new Vector2(1f, 1f), new Vector2(-1.33f, 2.35f), 180f, FitMode.Height, new Vector3(0f, 2.6f, 0f), grey, note: "gable façade faces the plaza (south)"),
                P("House_Grey_Planter", "Flower planter along the grey façade", Kingdom("SM_Env_Planter_Preset_02"), "Fantasy Kingdom", new Vector2(1f, 1f), new Vector2(-1.4f, 0.36f), 90f, FitMode.KitScale, Vector3.one),
                P("House_Grey_Lantern", "Wall lantern on the grey façade", Model("Lantern_Wall"), "Shadow Steps models", new Vector2(1f, 1f), new Vector2(-2.5f, 0.55f), 180f, FitMode.Height, new Vector3(0f, 0.45f, 0f), elevation: 1.3f),
                P("Railing_N_L", "Black metal fence between the yellow house and the gate", Town("SM_Env_Fence_White_Straight_01"), "Town", new Vector2(0f, 1f), new Vector2(-0.525f, 0.3f), 0f, FitMode.Box, new Vector3(0.65f, 0.6f, 0f), black, note: "white picket fence tinted near-black reads as iron railing"),
                P("Gate_Pillar_A", "Stone gate pillar", Model("Pillar_Square"), "Shadow Steps models", new Vector2(0f, 1f), new Vector2(-0.75f, 0.3f), 0f, FitMode.Height, new Vector3(0f, 1.0f, 0f), stone),
                P("Gate_N", "Iron gate at the plaza entrance", Town("SM_Env_Fence_White_Gate_01"), "Town", new Vector2(0f, 1f), new Vector2(0f, 0.3f), 0f, FitMode.Box, new Vector3(1.2f, 0.7f, 0f), black),
                P("Gate_Pillar_B", "Stone gate pillar", Model("Pillar_Square"), "Shadow Steps models", new Vector2(0f, 1f), new Vector2(0.75f, 0.3f), 0f, FitMode.Height, new Vector3(0f, 1.0f, 0f), stone),
                P("Railing_N_R", "Black metal fence between the gate and the grey house", Town("SM_Env_Fence_White_Straight_01"), "Town", new Vector2(0f, 1f), new Vector2(1.145f, 0.3f), 0f, FitMode.Box, new Vector3(0.6f, 0.6f, 0f), black),
                P("Lamp_NW", "Street lamp at the top-left corner", Town("SM_Prop_Streetlamp_02"), "Town", new Vector2(-1f, 1f), new Vector2(0.3f, 0.5f), 0f, FitMode.Height, new Vector3(0f, 2.0f, 0f), black),
                P("Lamp_NE", "Street lamp at the top-right corner", Town("SM_Prop_Streetlamp_02"), "Town", new Vector2(1f, 1f), new Vector2(-0.3f, 0.5f), 0f, FitMode.Height, new Vector3(0f, 2.0f, 0f), black),
                P("Fountain_Base", "Fountain basin beyond the gate", Town("SM_Prop_Fountain_Base_01"), "Town", new Vector2(0f, 1f), new Vector2(0.6f, 2.3f), 0f, FitMode.Footprint, new Vector3(1.5f, 0.3f, 0f), stone),
                P("Fountain", "Tiered fountain", Town("SM_Prop_Fountain_01"), "Town", new Vector2(0f, 1f), new Vector2(0.6f, 2.3f), 0f, FitMode.Footprint, new Vector3(1.0f, 1.2f, 0f), stone, elevation: 0.05f),
                P("Shop_Back", "Terracotta building in the background (top left)", Town("SM_Bld_Shop_02"), "Town", new Vector2(0f, 1f), new Vector2(-2.2f, 5.4f), 180f, FitMode.Box, new Vector3(5.5f, 2.0f, 3.0f), shopTint),
                P("Church_Back", "Church in the far background (top right)", Town("SM_Bld_Church_01"), "Town", new Vector2(0f, 1f), new Vector2(2.6f, 5.8f), 180f, FitMode.Height, new Vector3(0f, 5.5f, 0f), cream),
                P("Tree_NW", "Tree behind the yellow house", Town("SM_Env_Tree_01"), "Town", new Vector2(-1f, 1f), new Vector2(-0.4f, 2.4f), 40f, FitMode.Height, new Vector3(0f, 3.4f, 0f)),
                P("Tree_NE", "Tree behind the grey house", Town("SM_Env_Tree_01"), "Town", new Vector2(1f, 1f), new Vector2(0.4f, 2.4f), 110f, FitMode.Height, new Vector3(0f, 3.2f, 0f)),
                P("Bush_N", "Bush beside the fountain", Town("SM_Env_Bush_03"), "Town", new Vector2(0f, 1f), new Vector2(-0.4f, 2.8f), 0f, FitMode.KitScale, Vector3.one),

                // ---- West margin (left): lamp, bench, barrels, crate — low props only, outside the grid.
                P("Lamp_W", "Street lamp (left)", Town("SM_Prop_Streetlamp_02"), "Town", new Vector2(-1f, 0.7f), new Vector2(0.3f, 0f), 0f, FitMode.Height, new Vector3(0f, 2.0f, 0f), black),
                P("Bench_W", "Wooden bench facing the plaza", Town("SM_Prop_ParkBench_01"), "Town", new Vector2(-1f, 0.2f), new Vector2(0.3f, 0f), 90f, FitMode.KitScale, Vector3.one),
                P("Barrels_W", "Wooden barrels", Town("SM_Prop_Barrel_01"), "Town", new Vector2(-1f, -0.25f), new Vector2(0.3f, 0f), 0f, FitMode.KitScale, Vector3.one, repeat: 1, repeatStep: new Vector2(0f, -0.4f)),
                P("Crate_W", "Single wooden crate", Kingdom("SM_Prop_Crate_Wood_01"), "Fantasy Kingdom", new Vector2(-1f, -0.7f), new Vector2(0.28f, 0f), 0f, FitMode.KitScale, Vector3.one),

                // ---- East margin (right): lamp, planters, bush.
                P("Lamp_E", "Street lamp (right)", Town("SM_Prop_Streetlamp_02"), "Town", new Vector2(1f, 0.7f), new Vector2(-0.3f, 0f), 0f, FitMode.Height, new Vector3(0f, 2.0f, 0f), black),
                P("Planter_E", "Flower planters along the right edge", Kingdom("SM_Prop_PlanterBox_Preset_03"), "Fantasy Kingdom", new Vector2(1f, 0.25f), new Vector2(-0.3f, 0f), 90f, FitMode.KitScale, Vector3.one, repeat: 1, repeatStep: new Vector2(0f, -1.3f)),
                P("Bush_E", "Small bush (right)", Town("SM_Env_Bush_04"), "Town", new Vector2(1f, -0.7f), new Vector2(-0.3f, 0f), 0f, FitMode.KitScale, Vector3.one),

                // ---- South edge (bottom): railing with planters behind it on the left, crates, hedge and bushes, tree and the terracotta roof at the bottom right.
                P("Railing_S", "Black metal fence (bottom left)", Town("SM_Env_Fence_White_Straight_01"), "Town", new Vector2(-1f, -1f), new Vector2(1.0f, -0.3f), 0f, FitMode.Box, new Vector3(1.25f, 0.6f, 0f), black, repeat: 1, repeatStep: new Vector2(1.25f, 0f)),
                P("Railing_S_Post", "Fence posts", Town("SM_Env_Fence_White_Post_01"), "Town", new Vector2(-1f, -1f), new Vector2(0.375f, -0.3f), 0f, FitMode.Box, new Vector3(0.12f, 0.7f, 0.12f), black, repeat: 2, repeatStep: new Vector2(1.25f, 0f)),
                P("Railing_S_Planter", "Flower planters behind the railing", Kingdom("SM_Prop_PlanterBox_Preset_03"), "Fantasy Kingdom", new Vector2(-1f, -1f), new Vector2(1.0f, -0.95f), 0f, FitMode.KitScale, Vector3.one, repeat: 1, repeatStep: new Vector2(1.6f, 0f)),
                P("Flowers_S", "Flowers at the railing", Town("SM_Env_FlowerPatch_01"), "Town", new Vector2(-1f, -1f), new Vector2(0.5f, -0.6f), 0f, FitMode.KitScale, Vector3.one, repeat: 2, repeatStep: new Vector2(1.1f, 0f)),
                P("Crates_S", "Wooden crate stack (bottom left)", Kingdom("SM_Prop_Crate_Stack_04"), "Fantasy Kingdom", new Vector2(-1f, -1f), new Vector2(1.3f, -1.9f), 15f, FitMode.KitScale, Vector3.one),
                P("Barrel_S", "Wooden barrel", Town("SM_Prop_Barrel_01"), "Town", new Vector2(-1f, -1f), new Vector2(2.4f, -1.8f), 0f, FitMode.KitScale, Vector3.one),
                P("Hedge_S", "Low hedge along the bottom edge", Town("SM_Env_Hedge_03"), "Town", new Vector2(0.3f, -1f), new Vector2(0f, -0.8f), 0f, FitMode.KitScale, Vector3.one),
                P("Bush_S", "Greenery at the bottom edge", Town("SM_Env_Bush_03"), "Town", new Vector2(0f, -1f), new Vector2(-0.4f, -1.9f), 0f, FitMode.KitScale, Vector3.one, repeat: 1, repeatStep: new Vector2(1.8f, 0.1f)),
                P("Tree_SE", "Tree at the bottom right", Town("SM_Env_Tree_01"), "Town", new Vector2(1f, -1f), new Vector2(0.6f, -1.5f), 110f, FitMode.Height, new Vector3(0f, 3.2f, 0f)),
                P("House_Foreground", "Orange-roofed building in the foreground (bottom right)", Town("SM_Bld_House_Preset_01"), "Town", new Vector2(1f, -1f), new Vector2(-1.2f, -3.8f), 180f, FitMode.Height, new Vector3(0f, 2.6f, 0f), terracotta),
            };
            layout.placements = list.ToArray();
            EditorUtility.SetDirty(layout); AssetDatabase.SaveAssets();
            if (_missing.Count > 0) Debug.LogWarning("[SS3D] town plaza: " + _missing.Count + " prefab(s) not found: " + string.Join(", ", _missing));
            Debug.Log("[SS3D] town plaza layout written: " + LayoutPath + " (" + layout.placements.Length + " placements)");
            return layout;
        }

        [System.Serializable] private class Report { public string generated, scene, layout, stage; public int width, height, floorNodes, nodeMarkers, coverCells, coverProps, wallCells, wallPieces, guards, guardActors, placements, placedObjects; public bool ok; public List<string> problems = new List<string>(); public List<string> missingPrefabs = new List<string>(); }

        [MenuItem("Shadow Steps/3D/Town/Generate authored Town Plaza scene")]
        public static void GenerateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var layout = CreateLayout();
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var stage = pack.FindByCode(layout.stageCode);
            var report = new Report { generated = System.DateTime.Now.ToString("s"), scene = ScenePath, layout = LayoutPath, stage = layout.stageCode };
            report.missingPrefabs.AddRange(_missing);
            if (stage == null) { report.problems.Add("no shipped stage " + layout.stageCode); WriteReport(report); return; }
            if (EditorSceneManager.GetActiveScene().path == ScenePath && !Application.isBatchMode) { Debug.LogWarning("[SS3D] " + ScenePath + " is open; close it and generate again."); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = SS3DSetupMenu.LoadValidatedPalette();
            layout = AssetDatabase.LoadAssetAtPath<TownPlazaLayout>(LayoutPath); // by path after the scene switch
            var root = new GameObject("SS3D_Town_Plaza_ArtPreview_Only");
            var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
            var composer = root.AddComponent<TownPlazaComposer>(); composer.layout = layout; composer.palette = palette;
            var preview = root.AddComponent<SS3DPreviewController>();
            preview.palette = palette; preview.stageCode = layout.stageCode; preview.levelRenderer = renderer; preview.plazaComposer = composer;
            var camObj = new GameObject("Camera_Fixed") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = layout.background;
            var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter; composer.fitter = fitter;
            new GameObject("Warm_Directional_Sun").AddComponent<Light>().type = LightType.Directional;
            preview.BuildPreview();
            Validate(stage, renderer, composer, report);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            WriteReport(report);
            WriteMapping(layout, stage);
            Debug.Log("[SS3D] town plaza scene: " + ScenePath + (report.ok ? " (validated clean)" : " PROBLEMS: " + string.Join("; ", report.problems)));
        }

        /// <summary>Batch entry: generate the scene, then render it at 1080×1920 with and without HUD guides.</summary>
        public static void GenerateAndRender()
        {
            try
            {
                GenerateScene();
                string name = "Town_Plaza_Authored_1080x1920";
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ssName") name = args[i + 1];
                SS3DBatchRender.RenderSceneTo(ScenePath, "Docs/Reports/Renders/" + name + ".png", 1080, 1920, false);
                SS3DBatchRender.RenderSceneTo(ScenePath, "Docs/Reports/Renders/" + name + "_guides.png", 1080, 1920, true);
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] generate+render failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        private static void Validate(Stage stage, SS3DLevelRenderer r, TownPlazaComposer composer, Report sr)
        {
            int floor = 0, walls = 0, cover = 0; foreach (var c in stage.FloorNodes()) floor++;
            for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (stage.IsWall(c)) walls++; else if (stage.IsBlocked(c)) cover++; }
            sr.width = stage.Width; sr.height = stage.Height; sr.floorNodes = floor; sr.wallCells = walls; sr.coverCells = cover; sr.guards = stage.Guards.Count;
            System.Func<string, string, bool> rx = (t, p) => System.Text.RegularExpressions.Regex.IsMatch(t, p);
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (rx(n, "^Node_\\d+_\\d+$")) sr.nodeMarkers++;
                else if (rx(n, "^Cover_X_\\d+_\\d+$")) sr.coverProps++;
                else if (rx(n, "^(SolidWall|WallPost)_\\d+_\\d+$")) sr.wallPieces++;
                else if (rx(n, "^Guard_\\d+$")) sr.guardActors++;
            }
            sr.placements = composer.layout.placements != null ? composer.layout.placements.Length : 0;
            var deco = composer.transform.Find("Town_Plaza_Decoration_Only");
            if (deco != null) foreach (Transform t in deco) if (!t.name.StartsWith("Pavement_") && !t.name.StartsWith("Outer_") && !t.name.StartsWith("Kerb_") && !t.name.StartsWith("Step_") && t.name != "Plaza_Base" && t.name != "Ground") sr.placedObjects++;
            // Decoration must stay outside the grid footprint (bounds bottom centre); checked on the XZ plane.
            float s = r.tileSpacing; float gx0 = r.ToWorld(0, 0).x - 0.5f * s, gx1 = r.ToWorld(stage.Width - 1, 0).x + 0.5f * s, gz1 = r.ToWorld(0, 0).z + 0.5f * s, gz0 = r.ToWorld(0, stage.Height - 1).z - 0.5f * s;
            if (deco != null) foreach (Transform t in deco)
            {
                if (t.name.StartsWith("Pavement_") || t.name.StartsWith("Outer_") || t.name.StartsWith("Kerb_") || t.name.StartsWith("Step_") || t.name == "Plaza_Base" || t.name == "Ground") continue;
                if (!SS3DKitFit.TryBounds(t.gameObject, out var b)) continue;
                if (b.center.x > gx0 && b.center.x < gx1 && b.center.z > gz0 && b.center.z < gz1) sr.problems.Add("decoration '" + t.name + "' is centred inside the grid");
            }
            if (sr.nodeMarkers != floor) sr.problems.Add("node markers " + sr.nodeMarkers + " != floor nodes " + floor);
            if (sr.coverProps != cover) sr.problems.Add("cover props " + sr.coverProps + " != X cells " + cover);
            if (sr.wallPieces != walls) sr.problems.Add("wall pieces " + sr.wallPieces + " != # cells " + walls);
            if (sr.guardActors != stage.Guards.Count) sr.problems.Add("guard actors " + sr.guardActors + " != guards " + stage.Guards.Count);
            if (sr.missingPrefabs.Count > 0) sr.problems.Add(sr.missingPrefabs.Count + " prefab(s) missing");
            sr.ok = sr.problems.Count == 0;
        }

        private static void WriteReport(Report report)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(Path.Combine(root, "Docs/Reports"));
            File.WriteAllText(Path.Combine(root, ReportPath), JsonUtility.ToJson(report, true));
        }

        private static void WriteMapping(TownPlazaLayout layout, Stage stage)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Town Plaza — mock element to prefab mapping");
            sb.AppendLine();
            sb.AppendLine("Generated by `Shadow Steps ▸ 3D ▸ Town ▸ Generate authored Town Plaza scene` on " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ".");
            sb.AppendLine("Reference: `ArtReferences/ShadowSteps_Town_Target.png`. Scene: `" + ScenePath + "`. Layout asset: `" + LayoutPath + "`.");
            sb.AppendLine("Stage: " + stage.Code + " " + stage.Title + " (" + stage.Width + "×" + stage.Height + ", " + stage.Guards.Count + " guards, " + stage.Cameras.Count + " cameras) — gameplay positions come from `levels_v4.json`, never from the mock.");
            sb.AppendLine();
            sb.AppendLine("Packs: POLYGON Town Pack (`Assets/PolygonTown`) first; POLYGON Fantasy Kingdom (`Assets/PolygonFantasyKingdom`) only where the Town pack has no equivalent; `Art3D/Models` are the project's own glb pieces. Prefabs are instantiated and fitted from their measured bounds; the packages are never modified.");
            sb.AppendLine();
            sb.AppendLine("| Mock element | Prefab | Pack | Note |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var m in _mapping) sb.AppendLine("| " + m.element + " | `" + m.prefab + "` | " + m.pack + " | " + m.note + " |");
            sb.AppendLine();
            sb.AppendLine("Not reproduced on purpose: the mock's security camera and its yellow cone (the stage has no camera; a decorative one would read as a hazard), the triangular guard vision shapes (the engine draws the exact detected cells), the mock's own node layout, and the HUD (lives in the Game scene).");
            if (_missing.Count > 0) { sb.AppendLine(); sb.AppendLine("Missing prefabs: " + string.Join(", ", _missing)); }
            string root = Path.GetDirectoryName(Application.dataPath);
            File.WriteAllText(Path.Combine(root, MappingPath), sb.ToString());
        }
    }
}
