using System.Collections.Generic;
using System.IO;
using ShadowSteps.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// Editor entry points for the 3D art layer: validate the imported models, build/update the prefab palette (glb
    /// models + characters_5_02 prefabs), and generate art-preview scenes (Clockwise, Camera Basics, or all 30).
    /// </summary>
    public static class SS3DSetupMenu
    {
        public const string Base = "Assets/_ShadowSteps/Art3D";
        public const string Models = Base + "/Models/";
        public const string PalettePath = Base + "/SS3DArtPalette.asset";
        public const string SceneDir = Base + "/Demo";
        public const string CharacterPrefabs = "Assets/characters_5_02/Prefabs/";
        public const string DefaultSpy = "m_1";
        public const string DefaultGuard = "m_2";

        private static readonly string[] ExpectedModels =
        {
            "Banner_BlueGold", "Bench_Short", "Character_Guard_PROXY", "Character_Spy_PROXY", "Collectible_Key", "Door_Double_Wood", "Door_Locked_Path",
            "Door_Open_Path", "Entrance_Arch_Frame", "Exit_Pad", "Floor_Tile_A", "Floor_Tile_B", "Flowerbed_OutsideBoard", "Lantern_Wall", "Node_Marker",
            "Obstacle_Crate_1x1", "Obstacle_Planter_1x1", "Obstacle_Stone_Block_1x1", "Pillar_Square", "Security_Camera", "Security_Camera_Head",
            "Security_Camera_Mount", "Stairs_3Step_2m", "Wall_Block_1m", "Wall_Straight_1m", "Water_Tile"
        };

        [MenuItem("Shadow Steps/3D/Validate imported models")]
        public static void ValidateModels()
        {
            var sb = new System.Text.StringBuilder();
            int missing = 0, noRenderer = 0, pink = 0;
            foreach (var name in ExpectedModels)
            {
                var go = Model(name);
                if (go == null) { missing++; sb.AppendLine("MISSING  " + name + ".glb (not imported: is com.unity.cloud.gltfast installed?)"); continue; }
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { noRenderer++; sb.AppendLine("EMPTY    " + name + " has no renderer"); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                bool bad = false;
                foreach (var r in rs) foreach (var m in r.sharedMaterials) if (m == null || m.shader == null || m.shader.name.Contains("InternalErrorShader") || m.shader.name == "Standard") bad = true;
                if (bad) pink++;
                sb.AppendLine((bad ? "MATERIAL " : "ok       ") + name + "  size " + b.size.ToString("0.00") + (bad ? "  (shader not URP: will render pink)" : ""));
            }
            string summary = "[SS3D] models: " + (ExpectedModels.Length - missing) + "/" + ExpectedModels.Length + " imported, " + noRenderer + " without renderer, " + pink + " with non-URP materials";
            Debug.Log(summary + "\n" + sb);
            EditorUtility.DisplayDialog("Shadow Steps 3D", summary + "\n\nDetails in the Console.", "OK");
        }

        /// <summary>Fills only EMPTY slots, so manual choices (especially the characters) survive every rebuild.</summary>
        [MenuItem("Shadow Steps/3D/Create or update Art Palette (fills empty slots only)")]
        public static SS3DArtPalette EnsurePalette() => BuildPalette(overwriteModels: false);

        /// <summary>Re-resolves every model slot from the .glb files; characters stay untouched while lockCharacterSelection is on.</summary>
        [MenuItem("Shadow Steps/3D/Reset model slots in Art Palette (keeps characters)")]
        public static void ResetModelSlots() => BuildPalette(overwriteModels: true);

        public static SS3DArtPalette BuildPalette(bool overwriteModels)
        {
            var palette = AssetDatabase.LoadAssetAtPath<SS3DArtPalette>(PalettePath);
            if (palette == null) { palette = ScriptableObject.CreateInstance<SS3DArtPalette>(); AssetDatabase.CreateAsset(palette, PalettePath); }
            GameObject Slot(GameObject current, string model) => overwriteModels || current == null ? Model(model) : current;
            palette.floorA = Slot(palette.floorA, "Floor_Tile_A"); palette.floorB = Slot(palette.floorB, "Floor_Tile_B");
            palette.wallBlock = Slot(palette.wallBlock, "Wall_Block_1m"); palette.wallStraight = Slot(palette.wallStraight, "Wall_Straight_1m");
            if (overwriteModels || palette.coverObstacles == null || palette.coverObstacles.Length == 0 || System.Array.Exists(palette.coverObstacles, o => o == null))
                palette.coverObstacles = new[] { Model("Obstacle_Crate_1x1"), Model("Obstacle_Planter_1x1"), Model("Obstacle_Stone_Block_1x1") };
            palette.node = Slot(palette.node, "Node_Marker"); palette.exitPad = Slot(palette.exitPad, "Exit_Pad"); palette.collectibleKey = Slot(palette.collectibleKey, "Collectible_Key");
            palette.lockedDoor = Slot(palette.lockedDoor, "Door_Locked_Path"); palette.openedDoor = Slot(palette.openedDoor, "Door_Open_Path");
            palette.spyProxy = Slot(palette.spyProxy, "Character_Spy_PROXY"); palette.guardProxy = Slot(palette.guardProxy, "Character_Guard_PROXY");
            // Characters: never overwritten once set (lockCharacterSelection); only filled when empty.
            if (palette.spyCharacter == null && !(palette.lockCharacterSelection && palette.guardCharacter != null)) palette.spyCharacter = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabs + DefaultSpy + ".prefab");
            if (palette.guardCharacter == null && !(palette.lockCharacterSelection && palette.spyCharacter != null)) palette.guardCharacter = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabs + DefaultGuard + ".prefab");
            if (palette.spyCharacter != null || palette.guardCharacter != null) palette.lockCharacterSelection = true;
            palette.cameraModel = Slot(palette.cameraModel, "Security_Camera"); palette.cameraMount = Slot(palette.cameraMount, "Security_Camera_Mount"); palette.cameraHead = Slot(palette.cameraHead, "Security_Camera_Head");
            palette.archFrame = Slot(palette.archFrame, "Entrance_Arch_Frame"); palette.palaceDoor = Slot(palette.palaceDoor, "Door_Double_Wood"); palette.stairs = Slot(palette.stairs, "Stairs_3Step_2m");
            palette.pillar = Slot(palette.pillar, "Pillar_Square"); palette.water = Slot(palette.water, "Water_Tile"); palette.bench = Slot(palette.bench, "Bench_Short");
            palette.lantern = Slot(palette.lantern, "Lantern_Wall"); palette.banner = Slot(palette.banner, "Banner_BlueGold"); palette.flowerbed = Slot(palette.flowerbed, "Flowerbed_OutsideBoard");
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            Debug.Log("[SS3D] palette " + (overwriteModels ? "model slots reset" : "updated (empty slots only)") + " at " + PalettePath +
                      (palette.spyCharacter != null ? "; spy = " + palette.spyCharacter.name : "; spy = proxy") + (palette.guardCharacter != null ? ", guard = " + palette.guardCharacter.name : ", guard = proxy"));
            return palette;
        }

        /// <summary>Numeric portrait framing and occlusion check for the demo stages at 9:16 and 9:19.5 (no rendering needed).</summary>
        [MenuItem("Shadow Steps/3D/Framing report (portrait 9:16 and 9:19.5)")]
        public static void FramingReport()
        {
            var palette = AssetDatabase.LoadAssetAtPath<SS3DArtPalette>(PalettePath);
            float wallH = 0.93f * (palette != null ? palette.wallHeightScale : 0.7f), coverH = 0.88f * (palette != null ? palette.coverHeightScale : 0.85f);
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var sb = new System.Text.StringBuilder();
            foreach (var code in new[] { "LEVEL-2", "LEVEL-16", "LEVEL-20", "FTUE-1" })
            {
                var st = pack.FindByCode(code); if (st == null) continue;
                foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f })
                    sb.AppendLine(code + " " + st.Width + "x" + st.Height + " | " + SS3DFraming.Compute(st.Width, st.Height, 63f, aspect, 0.14f, 0.22f, 1.0f, 2.4f, wallH, coverH));
            }
            Debug.Log("[SS3D] framing report\n" + sb);
        }

        [MenuItem("Shadow Steps/3D/Create demo scenes (Clockwise + Camera Basics)")]
        public static void CreateTwoDemoScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pal = EnsurePalette();
            if (!pal.HasCoreModels) { EditorUtility.DisplayDialog("Shadow Steps 3D", "3D model imports are missing. Install com.unity.cloud.gltfast in Package Manager, let the .glb files import, then run this again.", "OK"); return; }
            CreateScene("LEVEL-2", "Clockwise_Rotating_Guard", pal);
            CreateScene("LEVEL-16", "Camera_Basics", pal);
            EditorUtility.DisplayDialog("Shadow Steps 3D", "Created 2 art-preview scenes in " + SceneDir + ". They are visual previews; play the Game scene with Use3DPresentation for the playable version.", "OK");
        }

        [MenuItem("Shadow Steps/3D/Create demo scenes for all 30 stages")]
        public static void CreateAllDemoScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pal = EnsurePalette();
            if (!pal.HasCoreModels) { EditorUtility.DisplayDialog("Shadow Steps 3D", "3D model imports are missing (install com.unity.cloud.gltfast).", "OK"); return; }
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            foreach (var s in pack.Stages) CreateScene(s.Code, s.Code.Replace("-", "_") + "_" + s.Title.Replace(" ", "_"), pal);
            Debug.Log("[SS3D] created " + pack.Stages.Count + " preview scenes in " + SceneDir);
        }

        [MenuItem("Shadow Steps/3D/Enable 3D presentation in Game scene")]
        public static void EnableInGameScene()
        {
            var pal = EnsurePalette();
            var scene = EditorSceneManager.OpenScene("Assets/_ShadowSteps/Scenes/Game.unity");
            var composer = Object.FindObjectOfType<ShadowSteps.Runtime.GameSceneComposer>();
            if (composer == null) { Debug.LogError("[SS3D] Game.unity has no GameSceneComposer"); return; }
            composer.Use3DPresentation = true; composer.Art3DPalette = pal;
            EditorUtility.SetDirty(composer); EditorSceneManager.SaveScene(scene);
            Debug.Log("[SS3D] Game scene now uses the 3D presentation (toggle Use3DPresentation on the composer to go back).");
        }

        private static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".glb");

        private static void CreateScene(string stageCode, string filename, SS3DArtPalette palette)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SS3D_Visual_Preview_Only");
            var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
            var frame = root.AddComponent<SS3DCourtyardFrame>(); frame.palette = palette;
            var preview = root.AddComponent<SS3DPreviewController>();
            preview.stageCode = stageCode; preview.levelRenderer = renderer; preview.exteriorFrame = frame;
            var camObj = new GameObject("Camera_Fixed_Ortho") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.67f, 0.84f, 0.87f);
            var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter;
            var sun = new GameObject("Warm_Directional_Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(1f, 0.91f, 0.76f);
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.79f, 0.82f, 0.86f);
            preview.BuildPreview();
            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, SceneDir + "/" + filename + ".unity");
            Debug.Log("[SS3D] preview scene created: " + filename);
        }
    }
}
