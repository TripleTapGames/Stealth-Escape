using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stage = ShadowSteps.V3.Stage;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// POLYGON Town Pack integration: converts its materials to URP Lit, creates the ten environment profile assets
    /// (prefabs resolved by verified paths under Assets/PolygonTown; a missing prefab is logged and the slot left
    /// empty), generates the ten demo scenes and the level-selection preview scene. Gameplay geometry always comes
    /// from the level renderer; profiles only decorate outside the playable graph.
    /// </summary>
    public static class TownEnvironmentSetup
    {
        public const string TownRoot = "Assets/PolygonTown";
        public const string ProfileDir = "Assets/_ShadowSteps/Art3D/Town/Profiles";
        public const string SceneDir = "Assets/_ShadowSteps/Art3D/Levels/Town";
        public const string PreviewScene = SceneDir + "/SS3D_Town_Preview.unity";
        public const string ReportPath = "Docs/Reports/Town_Scene_Manifest.json";
        public static bool TownPresent => AssetDatabase.IsValidFolder(TownRoot + "/Prefabs");

        private static readonly List<string> _missing = new List<string>();
        private static GameObject P(string sub)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(TownRoot + "/Prefabs/" + sub + ".prefab");
            if (go == null) _missing.Add(sub);
            return go;
        }
        private static GameObject[] PP(params string[] subs) { var l = new List<GameObject>(); foreach (var s in subs) { var g = P(s); if (g != null) l.Add(g); } return l.ToArray(); }

        private struct Spec { public int stageId; public string stageCode, file, title; public System.Action<TownEnvironmentProfile> fill; }

        /// <summary>The ten themes. Prefab names were verified against the imported pack listing; sizes come from the FBX files.</summary>
        private static Spec[] Specs()
        {
            GameObject grass() => P("Environment/SM_Env_Grass_01");
            GameObject concrete() => P("Environment/SM_Env_Concrete_Base_01");
            GameObject road() => P("Environment/SM_Env_Road_01");
            GameObject sidewalk() => P("Environment/SM_Env_Sidewalk_Straight_01");
            GameObject lamp() => P("Props/SM_Prop_Streetlamp_02");
            GameObject bench() => P("Props/SM_Prop_ParkBench_01");
            GameObject bin() => P("Props/SM_Prop_RubbishBin_01");
            GameObject[] boxes() => PP("Props/SM_Prop_CardboardBox_01", "Props/SM_Prop_CardboardBox_03", "Props/SM_Prop_CardboardBox_05", "Props/SM_Prop_Barrel_01");
            GameObject[] gardenCover() => PP("Environment/SM_Env_Gardenbox_Single_01", "Props/SM_Prop_PotPlant_01", "Props/SM_Prop_Compost_01");
            GameObject hedgeBlock() => P("Environment/SM_Env_Hedge_02");
            GameObject planterWall() => P("Environment/SM_Env_Gardenbox_Single_01");
            Color grassTint = new Color(0.78f, 0.9f, 0.7f), concreteTint = new Color(0.9f, 0.9f, 0.88f);
            void Sun(TownEnvironmentProfile p, float intensity, Color color, Vector2 angles, Color sky) { p.sunIntensity = intensity; p.sunColor = color; p.sunAngles = angles; p.background = sky; }
            return new[]
            {
                new Spec { stageId = 1, stageCode = "FTUE-1", file = "SS3D_Town_01_Quiet_Neighborhood", title = "Quiet Neighborhood", fill = p => {
                    p.description = "Houses, sidewalks, lawns and fences."; p.groundPrefab = grass(); p.groundTint = grassTint;
                    p.floorA = new Color(0.86f, 0.85f, 0.82f); p.floorB = new Color(0.82f, 0.81f, 0.78f); p.coverProps = gardenCover(); p.wallProp = hedgeBlock();
                    p.roadNorth = true; p.roadTile = road(); p.sidewalkTile = sidewalk(); p.buildings = PP("Buildings/Presets/SM_Bld_House_Preset_01", "Buildings/Presets/SM_Bld_House_Preset_02"); p.roadVehicles = PP("Vehicles/SM_Veh_Convertable_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_White_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_White_Post_01"); p.fenceGate = P("Environment/SM_Env_Fence_White_Gate_01");
                    p.trees = PP("Environment/SM_Env_Tree_01", "Environment/SM_Env_Tree_02"); p.treeCount = 4; p.bushes = PP("Environment/SM_Env_Bush_01", "Environment/SM_Env_Bush_02"); p.bushCount = 4;
                    p.streetLamp = lamp(); p.bench = null; p.bin = bin(); p.accents = PP("Props/SM_Prop_LetterBox_01", "Props/SM_Prop_Flowers_01", "Environment/SM_Env_FlowerPatch_01"); p.accentCount = 4; } },
                new Spec { stageId = 2, stageCode = "FTUE-2", file = "SS3D_Town_02_Garden_Path", title = "Garden Path", fill = p => {
                    p.description = "Greenery, benches and landscaped walkways."; p.groundPrefab = grass(); p.groundTint = grassTint;
                    p.floorA = new Color(0.84f, 0.78f, 0.68f); p.floorB = new Color(0.80f, 0.74f, 0.64f); p.coverProps = gardenCover(); p.wallProp = hedgeBlock();
                    p.roadNorth = false; p.buildings = PP("Buildings/SM_Bld_GardenShed_01", "Buildings/Presets/SM_Bld_House_Preset_02"); p.buildingHeight = 2.4f;
                    p.hedge = P("Environment/SM_Env_Hedge_01"); p.trees = PP("Environment/SM_Env_Tree_03", "Environment/SM_Env_Tree_02", "Environment/SM_Env_Tree_Tall_01"); p.treeCount = 6;
                    p.bushes = PP("Environment/SM_Env_Bush_02", "Environment/SM_Env_FlowerPatch_01", "Environment/SM_Env_FlowerPatch_02"); p.bushCount = 8;
                    p.streetLamp = null; p.bench = bench(); p.bin = null; p.accents = PP("Props/SM_Prop_BirdBath_01", "Environment/SM_Env_Gardenbox_Double_01", "Props/SM_Prop_Flowers_02"); p.accentCount = 4;
                    Sun(p, 1.6f, new Color(1f, 0.96f, 0.88f), new Vector2(55f, -30f), new Color(0.66f, 0.84f, 0.9f)); } },
                new Spec { stageId = 3, stageCode = "FTUE-3", file = "SS3D_Town_03_Residential_Driveway", title = "Residential Driveway", fill = p => {
                    p.description = "Garages, vehicles and driveway details."; p.groundPrefab = concrete(); p.groundTint = concreteTint;
                    p.floorA = new Color(0.78f, 0.78f, 0.76f); p.floorB = new Color(0.74f, 0.74f, 0.72f); p.coverProps = boxes(); p.wallProp = planterWall(); p.wallHeight = 0.55f;
                    p.roadNorth = false; p.buildings = PP("Buildings/Presets/SM_Bld_House_Preset_Garage_01", "Buildings/Presets/SM_Bld_House_Preset_01"); p.buildingHeight = 3.0f;
                    p.southVehicles = PP("Vehicles/SM_Veh_Pickup_01", "Vehicles/SM_Veh_Convertable_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_Wood_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_Wood_Post_01");
                    p.trees = PP("Environment/SM_Env_Tree_02"); p.treeCount = 2; p.bushes = PP("Environment/SM_Env_Bush_01"); p.bushCount = 3;
                    p.streetLamp = P("Props/SM_Prop_Streetlamp_01"); p.bin = bin(); p.accents = PP("Props/SM_Prop_HoseReel_01", "Props/SM_Prop_LetterBox_02", "Props/SM_Prop_Wheelbarrow_01"); p.accentCount = 3; } },
                new Spec { stageId = 4, stageCode = "FTUE-4", file = "SS3D_Town_04_Street_Corner", title = "Street Corner", fill = p => {
                    p.description = "Corner buildings, streetlights and paved road."; p.groundPrefab = concrete(); p.groundTint = concreteTint;
                    p.floorA = new Color(0.80f, 0.80f, 0.78f); p.floorB = new Color(0.76f, 0.76f, 0.74f); p.coverProps = boxes(); p.wallProp = planterWall(); p.wallHeight = 0.55f;
                    p.roadNorth = true; p.roadTile = road(); p.sidewalkTile = sidewalk(); p.buildings = PP("Buildings/SM_Bld_Shop_01", "Buildings/SM_Bld_Shop_02"); p.buildingHeight = 3.0f; p.roadVehicles = PP("Vehicles/SM_Veh_Bus_01", "Vehicles/SM_Veh_Convertable_01");
                    p.roadSouth = true; p.southVehicles = PP("Vehicles/SM_Veh_Pickup_01");
                    p.trees = PP("Environment/SM_Env_Tree_03"); p.treeCount = 2; p.bushCount = 0;
                    p.streetLamp = lamp(); p.bin = bin(); p.accents = PP("Props/SM_Prop_Sign_Stop_01", "Props/SM_Prop_Sign_BusStop_01", "Props/SM_Prop_StreetSign_Pole_01", "Props/SM_Prop_BikeStand_01"); p.accentCount = 4; } },
                new Spec { stageId = 5, stageCode = "FTUE-5", file = "SS3D_Town_05_Delivery_Yard", title = "Delivery Yard", fill = p => {
                    p.description = "Delivery vehicle, crates and storage area."; p.groundPrefab = concrete(); p.groundTint = new Color(0.84f, 0.84f, 0.82f);
                    p.floorA = new Color(0.72f, 0.72f, 0.70f); p.floorB = new Color(0.68f, 0.68f, 0.66f); p.coverProps = boxes(); p.wallProp = P("Props/SM_Prop_Propane_01"); p.wallHeight = 0.55f;
                    p.roadNorth = false; p.buildings = PP("Buildings/SM_Bld_Shop_Concrete_01", "Buildings/Presets/SM_Bld_House_Preset_Garage_01"); p.buildingHeight = 2.8f;
                    p.southVehicles = PP("Vehicles/SM_Veh_Truck_Delivery_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_Wood_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_Wood_Post_01"); p.fenceGate = P("Environment/SM_Env_Fence_Wood_Gate_01");
                    p.treeCount = 0; p.bushCount = 0; p.streetLamp = P("Props/SM_Prop_Streetlamp_01"); p.bin = P("Props/SM_Prop_RubbishBin_02");
                    p.southProps = PP("Props/SM_Prop_CardboardBox_03", "Props/SM_Prop_Barrel_01", "Props/SM_Prop_Cart_01", "Props/SM_Prop_TrashBag_01"); p.southPropCount = 5;
                    p.accents = PP("Props/SM_Prop_Propane_01", "Props/SM_Prop_CardboardBox_01"); p.accentCount = 3;
                    Sun(p, 1.4f, new Color(1f, 0.93f, 0.82f), new Vector2(48f, -40f), new Color(0.7f, 0.78f, 0.82f)); } },
                new Spec { stageId = 6, stageCode = "LEVEL-1", file = "SS3D_Town_06_Neighborhood_Park", title = "Neighborhood Park", fill = p => {
                    p.description = "Open pathways, trees and benches."; p.groundPrefab = grass(); p.groundTint = grassTint;
                    p.floorA = new Color(0.85f, 0.80f, 0.70f); p.floorB = new Color(0.81f, 0.76f, 0.66f); p.coverProps = gardenCover(); p.wallProp = hedgeBlock();
                    p.roadNorth = false; p.buildings = PP("Props/SM_Prop_Playground_Fort_01", "Props/SM_Prop_Playground_Swing_01"); p.buildingHeight = 1.6f;
                    p.hedge = P("Environment/SM_Env_Hedge_01"); p.trees = PP("Environment/SM_Env_Tree_01", "Environment/SM_Env_Tree_Tall_01", "Environment/SM_Env_Tree_Pine_01"); p.treeCount = 8;
                    p.bushes = PP("Environment/SM_Env_Bush_01", "Environment/SM_Env_Bush_02"); p.bushCount = 4; p.streetLamp = lamp(); p.bench = bench(); p.bin = bin();
                    p.accents = PP("Props/SM_Prop_Fountain_01", "Props/SM_Prop_BirdBath_01", "Environment/SM_Env_FlowerPatch_03"); p.accentCount = 3; } },
                new Spec { stageId = 7, stageCode = "LEVEL-2", file = "SS3D_Town_07_Town_Plaza", title = "Town Plaza", fill = p => {
                    p.description = "Decorative paving, surrounding buildings and street furniture."; p.groundPrefab = concrete(); p.groundTint = new Color(0.92f, 0.9f, 0.86f);
                    p.floorA = new Color(0.88f, 0.82f, 0.72f); p.floorB = new Color(0.80f, 0.74f, 0.64f); p.coverProps = gardenCover(); p.wallProp = planterWall(); p.wallHeight = 0.55f;
                    p.roadNorth = true; p.roadTile = road(); p.sidewalkTile = sidewalk(); p.buildings = PP("Buildings/SM_Bld_Shop_02", "Buildings/SM_Bld_Shop_03", "Buildings/SM_Bld_Church_01"); p.buildingHeight = 3.4f;
                    p.trees = PP("Environment/SM_Env_Tree_02", "Environment/SM_Env_Tree_03"); p.treeCount = 4; p.bushes = PP("Environment/SM_Env_FlowerPatch_01", "Environment/SM_Env_FlowerPatch_02"); p.bushCount = 6;
                    p.streetLamp = lamp(); p.bench = bench(); p.bin = bin(); p.accents = PP("Props/SM_Prop_Fountain_01", "Props/SM_Prop_BikeStand_01", "Props/SM_Prop_Sign_BusStop_01"); p.accentCount = 4; } },
                new Spec { stageId = 8, stageCode = "LEVEL-3", file = "SS3D_Town_08_Backstreet_Alley", title = "Backstreet Alley", fill = p => {
                    p.description = "Building backs, utility areas and service props."; p.groundPrefab = concrete(); p.groundTint = new Color(0.72f, 0.72f, 0.7f);
                    p.floorA = new Color(0.62f, 0.62f, 0.60f); p.floorB = new Color(0.58f, 0.58f, 0.56f); p.coverProps = PP("Props/SM_Prop_RubbishBin_02", "Props/SM_Prop_CardboardBox_03", "Props/SM_Prop_Barrel_01", "Props/SM_Prop_TrashBag_01"); p.wallProp = P("Props/SM_Prop_RubbishBin_02"); p.wallHeight = 0.6f;
                    p.roadNorth = false; p.buildings = PP("Buildings/SM_Bld_Shop_03", "Buildings/SM_Bld_Shop_02"); p.buildingHeight = 3.6f;
                    p.roadSouth = false; p.southVehicles = PP("Vehicles/SM_Veh_Truck_Garbage_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_Wood_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_Wood_Post_01");
                    p.treeCount = 0; p.bushCount = 0; p.streetLamp = P("Props/SM_Prop_Streetlamp_01"); p.bin = P("Props/SM_Prop_RubbishBin_02");
                    p.southProps = PP("Props/SM_Prop_TrashBag_01", "Props/SM_Prop_TrashBag_02", "Props/SM_Prop_CardboardBox_01", "Props/SM_Prop_Drain_01", "Props/SM_Prop_Manhole_01"); p.southPropCount = 6;
                    p.accents = PP("Props/SM_Prop_Airvent_01", "Props/SM_Prop_Barrel_01"); p.accentCount = 3;
                    Sun(p, 1.2f, new Color(0.95f, 0.92f, 0.9f), new Vector2(44f, -60f), new Color(0.58f, 0.66f, 0.74f)); p.ambientEquator = new Color(0.78f, 0.78f, 0.78f); } },
                new Spec { stageId = 9, stageCode = "LEVEL-4", file = "SS3D_Town_09_Garage_Compound", title = "Garage Compound", fill = p => {
                    p.description = "Garages, storage areas and parked vehicles."; p.groundPrefab = concrete(); p.groundTint = new Color(0.86f, 0.86f, 0.84f);
                    p.floorA = new Color(0.74f, 0.74f, 0.72f); p.floorB = new Color(0.70f, 0.70f, 0.68f); p.coverProps = boxes(); p.wallProp = P("Props/SM_Prop_Propane_01"); p.wallHeight = 0.55f;
                    p.roadNorth = false; p.buildings = PP("Buildings/Presets/SM_Bld_House_Preset_Garage_01", "Buildings/SM_Bld_GardenShed_01", "Buildings/Presets/SM_Bld_House_Preset_Garage_01"); p.buildingHeight = 3.0f;
                    p.roadSouth = true; p.roadTile = P("Environment/SM_Env_Road_Parking_01"); p.sidewalkTile = null; p.southVehicles = PP("Vehicles/SM_Veh_Pickup_01", "Vehicles/SM_Veh_Truck_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_Wood_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_Wood_Post_01"); p.fenceGate = P("Environment/SM_Env_Fence_Wood_Gate_01");
                    p.treeCount = 0; p.bushCount = 0; p.streetLamp = P("Props/SM_Prop_Streetlamp_01"); p.bin = P("Props/SM_Prop_RubbishBin_02");
                    p.accents = PP("Props/SM_Prop_Barrel_01", "Props/SM_Prop_CardboardBox_05", "Props/SM_Prop_Cart_01"); p.accentCount = 4; } },
                new Spec { stageId = 10, stageCode = "LEVEL-5", file = "SS3D_Town_10_Gated_Community", title = "Gated Community", fill = p => {
                    p.description = "Residential entrance, fences and security structures."; p.groundPrefab = grass(); p.groundTint = grassTint;
                    p.floorA = new Color(0.84f, 0.82f, 0.78f); p.floorB = new Color(0.80f, 0.78f, 0.74f); p.coverProps = gardenCover(); p.wallProp = hedgeBlock();
                    p.roadNorth = true; p.roadTile = road(); p.sidewalkTile = sidewalk(); p.buildings = PP("Buildings/Presets/SM_Bld_House_Preset_03", "Buildings/Presets/SM_Bld_House_Preset_05"); p.buildingHeight = 3.4f; p.roadVehicles = PP("Vehicles/SM_Veh_Convertable_01");
                    p.fenceSegment = P("Environment/SM_Env_Fence_White_Straight_01"); p.fencePost = P("Environment/SM_Env_Fence_White_Post_01"); p.fenceGate = P("Environment/SM_Env_Fence_White_Gate_01");
                    p.trees = PP("Environment/SM_Env_Tree_01", "Environment/SM_Env_Tree_03"); p.treeCount = 4; p.bushes = PP("Environment/SM_Env_Bush_02", "Environment/SM_Env_FlowerPatch_01"); p.bushCount = 4;
                    p.streetLamp = lamp(); p.bin = null; p.accents = PP("Props/SM_Prop_SecurityLights_01", "Props/SM_Prop_LetterBox_03", "Props/SM_Prop_Sign_01"); p.accentCount = 4; } },
            };
        }

        [MenuItem("Shadow Steps/3D/Town/Create or update the 10 environment profiles")]
        public static List<TownEnvironmentProfile> CreateProfiles()
        {
            if (!TownPresent) { Debug.LogError("[SS3D] " + TownRoot + " not found; import POLYGON Town Pack first."); return null; }
            SS3DKitSetup.ConvertMaterials(TownRoot);
            Directory.CreateDirectory(ProfileDir);
            _missing.Clear();
            var result = new List<TownEnvironmentProfile>();
            foreach (var spec in Specs())
            {
                string path = ProfileDir + "/" + spec.file.Replace("SS3D_Town_", "Town_") + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<TownEnvironmentProfile>(path);
                if (profile == null) { profile = ScriptableObject.CreateInstance<TownEnvironmentProfile>(); AssetDatabase.CreateAsset(profile, path); }
                profile.displayName = spec.title; profile.seed = spec.stageId * 17 + 3;
                spec.fill(profile);
                EditorUtility.SetDirty(profile);
                result.Add(profile);
            }
            AssetDatabase.SaveAssets();
            if (_missing.Count > 0) Debug.LogWarning("[SS3D] town profiles: " + _missing.Count + " prefab(s) not found and left empty: " + string.Join(", ", _missing));
            Debug.Log("[SS3D] " + result.Count + " town environment profiles in " + ProfileDir);
            return result;
        }

        [System.Serializable] private class SceneReport { public int stageId; public string code, title, scene, profile; public int width, height, floorNodes, nodeMarkers, coverCells, coverProps, wallCells, wallPieces, guards, guardActors; public bool ok; public List<string> problems = new List<string>(); }
        [System.Serializable] private class Report { public string generated; public int count; public List<string> missingPrefabs = new List<string>(); public List<SceneReport> scenes = new List<SceneReport>(); }

        [MenuItem("Shadow Steps/3D/Town/Generate 10 town environment scenes + preview")]
        public static void GenerateAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var profiles = CreateProfiles(); if (profiles == null) return;
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            Directory.CreateDirectory(SceneDir);
            var report = new Report { generated = System.DateTime.Now.ToString("s") }; report.missingPrefabs.AddRange(_missing);
            var specs = Specs(); _missing.Clear();
            var openPath = EditorSceneManager.GetActiveScene().path;
            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i]; var profile = profiles[i];
                Stage stage = null; foreach (var st in pack.Stages) if (st.Id == spec.stageId) { stage = st; break; }
                var sr = new SceneReport { stageId = spec.stageId, title = spec.title, scene = SceneDir + "/" + spec.file + ".unity", profile = AssetDatabase.GetAssetPath(profile) };
                if (stage == null) { sr.problems.Add("no shipped stage with id " + spec.stageId); report.scenes.Add(sr); continue; }
                sr.code = stage.Code; sr.width = stage.Width; sr.height = stage.Height;
                if (openPath == sr.scene) { sr.problems.Add("scene is open in the editor; close it and regenerate"); report.scenes.Add(sr); continue; }
                CreateTownScene(stage.Code, sr.scene, profile, r => Validate(stage, r, sr));
                report.scenes.Add(sr);
            }
            if (openPath != PreviewScene) CreatePreviewScene(profiles, specs); else Debug.LogWarning("[SS3D] " + PreviewScene + " is open; close it to regenerate the switcher scene.");
            report.count = report.scenes.Count;
            string root = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(Path.Combine(root, "Docs/Reports"));
            File.WriteAllText(Path.Combine(root, ReportPath), JsonUtility.ToJson(report, true));
            int ok = 0; foreach (var sr in report.scenes) if (sr.ok) ok++;
            Debug.Log("[SS3D] town: " + report.scenes.Count + " scenes in " + SceneDir + " (" + ok + " validated clean), preview " + PreviewScene + ", report " + ReportPath);
        }

        /// <summary>Art-preview scene for one stage with a town environment profile (same preview controller as the courtyard scenes).</summary>
        private static void CreateTownScene(string stageCode, string assetPath, TownEnvironmentProfile profile, System.Action<SS3DLevelRenderer> afterBuild)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = SS3DSetupMenu.LoadValidatedPalette();
            profile = AssetDatabase.LoadAssetAtPath<TownEnvironmentProfile>(AssetDatabase.GetAssetPath(profile)); // reload after the scene switch
            var root = new GameObject("SS3D_Town_ArtPreview_Only");
            var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
            var town = root.AddComponent<TownEnvironmentGenerator>(); town.profile = profile; town.palette = palette;
            var preview = root.AddComponent<SS3DPreviewController>();
            preview.palette = palette; preview.stageCode = stageCode; preview.levelRenderer = renderer; preview.townEnvironment = town; preview.paddingOverride = profile.marginTiles + 0.5f;
            var camObj = new GameObject("Camera_Fixed") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = profile.background;
            var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter; town.fitter = fitter;
            new GameObject("Warm_Directional_Sun").AddComponent<Light>().type = LightType.Directional;
            preview.BuildPreview();
            afterBuild?.Invoke(renderer);
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            EditorSceneManager.SaveScene(scene, assetPath);
            Debug.Log("[SS3D] town scene created: " + assetPath);
        }

        private static void CreatePreviewScene(List<TownEnvironmentProfile> profiles, Spec[] specs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = SS3DSetupMenu.LoadValidatedPalette();
            var root = new GameObject("Town_Preview_Switcher");
            var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
            var town = root.AddComponent<TownEnvironmentGenerator>(); town.palette = palette;
            var preview = root.AddComponent<SS3DPreviewController>(); preview.palette = palette; preview.levelRenderer = renderer; preview.townEnvironment = town; preview.rebuildOnLoad = false;
            var camObj = new GameObject("Camera_Fixed") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor;
            var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter; town.fitter = fitter;
            new GameObject("Warm_Directional_Sun").AddComponent<Light>().type = LightType.Directional;
            var switcher = root.AddComponent<TownPreviewSwitcher>();
            switcher.preview = preview; switcher.environment = town;
            switcher.entries = new TownPreviewSwitcher.Entry[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                var pr = AssetDatabase.LoadAssetAtPath<TownEnvironmentProfile>(AssetDatabase.GetAssetPath(profiles[i]));
                switcher.entries[i] = new TownPreviewSwitcher.Entry { stageCode = specs[i].stageCode, label = specs[i].title, profile = pr };
                preview.paddingOverride = pr.marginTiles + 0.5f;
            }
            switcher.Show(0);
            EditorSceneManager.SaveScene(scene, PreviewScene);
            Debug.Log("[SS3D] town preview switcher scene created: " + PreviewScene + " (select Town_Preview_Switcher, use the buttons or the index field)");
        }

        private static void Validate(Stage stage, SS3DLevelRenderer r, SceneReport sr)
        {
            int floor = 0, walls = 0, cover = 0; foreach (var c in stage.FloorNodes()) floor++;
            for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (stage.IsWall(c)) walls++; else if (stage.IsBlocked(c)) cover++; }
            sr.floorNodes = floor; sr.wallCells = walls; sr.coverCells = cover; sr.guards = stage.Guards.Count;
            System.Func<string, string, bool> rx = (t, p) => System.Text.RegularExpressions.Regex.IsMatch(t, p);
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (rx(n, "^Node_\\d+_\\d+$")) sr.nodeMarkers++;
                else if (rx(n, "^Cover_X_\\d+_\\d+$")) sr.coverProps++;
                else if (rx(n, "^(SolidWall|WallPost)_\\d+_\\d+$")) sr.wallPieces++;
                else if (rx(n, "^Guard_\\d+$")) sr.guardActors++;
            }
            if (sr.nodeMarkers != floor) sr.problems.Add("node markers " + sr.nodeMarkers + " != floor nodes " + floor);
            if (sr.coverProps != cover) sr.problems.Add("cover props " + sr.coverProps + " != X cells " + cover);
            if (sr.wallPieces != walls) sr.problems.Add("wall pieces " + sr.wallPieces + " != # cells " + walls);
            if (sr.guardActors != stage.Guards.Count) sr.problems.Add("guard actors " + sr.guardActors + " != guards " + stage.Guards.Count);
            sr.ok = sr.problems.Count == 0;
        }
    }

    /// <summary>Buttons on the switcher so the ten environments can be flipped through without typing an index.</summary>
    [CustomEditor(typeof(TownPreviewSwitcher))]
    public sealed class TownPreviewSwitcherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var sw = (TownPreviewSwitcher)target;
            if (sw.entries == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Show environment", EditorStyles.boldLabel);
            for (int i = 0; i < sw.entries.Length; i++)
                if (GUILayout.Button((i + 1) + "  " + sw.entries[i].label + "  (" + sw.entries[i].stageCode + ")")) { sw.Show(i); EditorUtility.SetDirty(sw); }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Previous")) sw.Show(sw.index - 1);
            if (GUILayout.Button("Next ▶")) sw.Show(sw.index + 1);
            EditorGUILayout.EndHorizontal();
        }
    }
}
