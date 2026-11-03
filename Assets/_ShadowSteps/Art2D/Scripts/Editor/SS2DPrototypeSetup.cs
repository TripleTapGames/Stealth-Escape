using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Stage = ShadowSteps.V3.Stage;

namespace ShadowSteps.Art2D.Editor
{
    /// <summary>
    /// Builds the isolated 2D prototype scene (LEVEL-2 Clockwise), validates it against the stage data, audits it for
    /// any 3D runtime asset, writes the manifest and renders it headlessly. Nothing here touches the 3D scenes.
    /// </summary>
    public static class SS2DPrototypeSetup
    {
        public const string ScenePath = "Assets/_ShadowSteps/Art2D/Demo/ShadowSteps_2D_Prototype.unity";
        public const string ReportPath = "Docs/Reports/Art2D_Prototype_Manifest.json";
        public const string StageCode = "LEVEL-2";

        [System.Serializable] private class Report
        {
            public string generated, scene, library, stage; public int width, height;
            public int floorNodes, nodeSprites, edges, lineSprites, coverCells, coverSprites, wallCells, wallSprites, doors, doorSprites, guards, guardTokens, cameras, cameraSprites, keyExpected, keySprites, exitSprites;
            public int guardVisionCells, cameraVisionCells, visionOverlaySprites;
            public int meshRenderers, skinnedMeshRenderers, spriteRenderers; public List<string> threeDAssetsReferenced = new List<string>();
            public bool spyFramesComplete, guardFramesComplete; public int characterFrames;
            public bool ok; public List<string> problems = new List<string>();
            public SS2DCharacterCapture.Report capture;
        }

        [MenuItem("Shadow Steps/2D/Generate sprites, capture characters and build the prototype scene")]
        public static void GenerateAll() { Generate(render: false); }

        /// <summary>Standard level scene for any stage: the same composition as the prototype (composer + 2D presentation + HUD prefab + edit-mode preview). Path: Art2D/Levels/SS2D_&lt;code&gt;.unity.</summary>
        public static string CreateLevelScene(string stageCode)
        {
            var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            if (lib == null) lib = SS2DSpriteGenerator.Generate();
            string path = "Assets/_ShadowSteps/Art2D/Levels/SS2D_" + stageCode.Replace("-", "_") + ".unity";
            CreateScene(lib, stageCode, path);
            return path;
        }

        /// <summary>Batch entry: configure and assign the final PNG art (Sprites/Backgrounds, Sprites/Characters/Player|Guards), rebuild the prototype scene and render one 1080×1920 frame.</summary>
        public static void ApplyFinalArtAndRender()
        {
            try
            {
                var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                if (lib == null) lib = SS2DSpriteGenerator.Generate();
                if (!SS2DFinalArtImporter.Import(lib)) throw new System.IO.FileNotFoundException("final art PNGs missing under " + SS2DFinalArtImporter.SpritesRoot);
                lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                CreateScene(lib, StageCode, ScenePath);
                RenderSceneTo(ScenePath, "Docs/Reports/Renders/Art2D_FinalArt_LEVEL-2_1080x1920.png", 1080, 1920, false);
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] final art failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Batch entry for the camera-mount milestone: regenerate the placeholder sprites (adds camera_wall_plate / camera_pole),
        /// re-apply the final art, then render LEVEL-19 (wall-mounted sweeping camera, active vision), a synthetic lone-wall
        /// stage (pole mount), LEVEL-18 at t=1 (rotating) and LEVEL-23 after EMP#0 (disabled) from the prototype scene.
        /// </summary>
        public static void RenderCameraMounts()
        {
            try
            {
                var lib = SS2DSpriteGenerator.Generate();
                lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                SS2DFinalArtImporter.ImportIfPresent(lib);
                lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                CreateScene(lib, StageCode, ScenePath);
                RenderReview(ScenePath, "LEVEL-19", null, "Art2D_Camera_Mounts_LEVEL-19_1080x1920");
                RenderReview(ScenePath, "LEVEL-18", new[] { "WAIT" }, "Art2D_Camera_Mounts_Rotating_t1_LEVEL-18_1080x1920");
                RenderReview(ScenePath, "LEVEL-23", new[] { "EMP#0" }, "Art2D_Camera_Mounts_EMP_LEVEL-23_1080x1920");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var pv = Object.FindObjectOfType<SS2DPreviewController>();
                if (pv != null) { pv.OverrideStage = PoleMountDemoStage(); pv.previewActions = null; pv.BuildPreview(); RenderOpenScene("Docs/Reports/Renders/Art2D_Camera_Mounts_Pole_Demo_1080x1920.png", 1080, 1920, false); pv.OverrideStage = null; }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // leave the saved LEVEL-2 scene untouched
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] camera mounts failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Batch entry for the cone-vision milestone: no regeneration; renders LEVEL-19 (sweeping camera, t=0 active cone and t=2 clipped by cover), LEVEL-18 t=1, LEVEL-23 after EMP#0 and the pole demo.</summary>
        public static void RenderCameraCone()
        {
            try
            {
                var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                if (lib == null) throw new System.IO.FileNotFoundException(SS2DSpriteLibrary.DefaultAssetPath);
                RenderReview(ScenePath, "LEVEL-19", null, "Art2D_Camera_Cone_LEVEL-19_1080x1920");
                RenderReview(ScenePath, "LEVEL-19", new[] { "WAIT", "WAIT" }, "Art2D_Camera_Cone_t2_LEVEL-19_1080x1920");
                RenderReview(ScenePath, "LEVEL-18", new[] { "WAIT" }, "Art2D_Camera_Cone_Rotating_t1_LEVEL-18_1080x1920");
                RenderReview(ScenePath, "LEVEL-23", new[] { "EMP#0" }, "Art2D_Camera_Cone_EMP_LEVEL-23_1080x1920");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var pv = Object.FindObjectOfType<SS2DPreviewController>();
                if (pv != null) { pv.OverrideStage = PoleMountDemoStage(); pv.previewActions = null; pv.BuildPreview(); RenderOpenScene("Docs/Reports/Renders/Art2D_Camera_Cone_Pole_Demo_1080x1920.png", 1080, 1920, false); pv.OverrideStage = null; }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] camera cone render failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Batch entry for the environment milestone: import the obstacle art + LEVEL-19 layout, build the LEVEL-19 level scene and render it.</summary>
        public static void ApplyEnvironmentAndRender()
        {
            try
            {
                var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                if (lib == null) lib = SS2DSpriteGenerator.Generate();
                if (!SS2DFinalArtImporter.Import(lib)) throw new System.IO.FileNotFoundException("final art missing");
                if (!SS2DFinalArtImporter.HasObstacleArt()) throw new System.IO.FileNotFoundException("obstacle art missing under " + SS2DFinalArtImporter.ObstacleDir);
                AssetDatabase.SaveAssets();
                string scene = CreateLevelScene("LEVEL-19");
                RenderSceneTo(scene, "Docs/Reports/Renders/Art2D_Environment_LEVEL-19_1080x1920.png", 1080, 1920, false);
                RenderReview(scene, "LEVEL-19", new[] { "WAIT", "WAIT" }, "Art2D_Environment_t2_LEVEL-19_1080x1920");
                RenderReview(scene, "LEVEL-23", null, "Art2D_Environment_LEVEL-23_1080x1920");
                RenderReview(scene, "LEVEL-2", null, "Art2D_Environment_LEVEL-2_1080x1920");
                RenderReview(scene, "LEVEL-25", null, "Art2D_Environment_LEVEL-25_1080x1920");
                RenderReview(scene, "LEVEL-11", null, "Art2D_Environment_LEVEL-11_1080x1920");
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] environment failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Batch entry: review frames of every sweeping/rotating camera state (LEVEL-24 t0/t2, LEVEL-19 t1/t3, LEVEL-18 t2/t3) for cone debugging.</summary>
        public static void RenderCameraStates()
        {
            try
            {
                RenderReview(ScenePath, "LEVEL-18", null, "Art2D_Debug_LEVEL-18_t0");
                RenderReview(ScenePath, "LEVEL-24", null, "Art2D_Debug_LEVEL-24_t0");
                RenderReview(ScenePath, "LEVEL-24", new[] { "WAIT", "WAIT" }, "Art2D_Debug_LEVEL-24_t2");
                RenderReview(ScenePath, "LEVEL-19", new[] { "WAIT" }, "Art2D_Debug_LEVEL-19_t1");
                RenderReview(ScenePath, "LEVEL-19", new[] { "WAIT", "WAIT", "WAIT" }, "Art2D_Debug_LEVEL-19_t3");
                RenderReview(ScenePath, "LEVEL-18", new[] { "WAIT", "WAIT" }, "Art2D_Debug_LEVEL-18_t2");
                RenderReview(ScenePath, "LEVEL-18", new[] { "WAIT", "WAIT", "WAIT" }, "Art2D_Debug_LEVEL-18_t3");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] debug frames failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Synthetic review stage: a lone wall cell in the middle of the floor carrying a fixed camera (pole mount). Not shipped.</summary>
        public static Stage PoleMountDemoStage()
        {
            var b = new ShadowSteps.V3.StageBuilder { Id = 990, Code = "DEMO-POLE", Title = "Pole Mount", Start = new GridCoord(1, 1), Exit = new GridCoord(5, 3), AlarmBudget = 3 }
                .FromRows(new[] { "#######", "#.....#", "#..#..#", "#.....#", "#######" });
            b.Cameras.Add(new ShadowSteps.V3.CameraSpec("cam1", new GridCoord(3, 2), Direction.South, Direction.South, ShadowSteps.V3.CameraKind.Fixed));
            return b.Build();
        }

        /// <summary>Batch entry: everything plus the 1080×1920 renders (plain and with HUD guides) and the extra verification frames.</summary>
        public static void GenerateAndRender()
        {
            try { Generate(render: true); }
            catch (System.Exception e) { Debug.LogError("[SS2D] generate failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        public static void Generate(bool render)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var lib = SS2DSpriteGenerator.Generate();
            SS2DCharacterCapture.Capture(lib, out var capture);
            lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            SS2DFinalArtImporter.ImportIfPresent(lib); // the generator and capture refill the placeholder slots; final art wins when it is on disk
            lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var stage = pack.FindByCode(StageCode);
            var report = new Report { generated = System.DateTime.Now.ToString("s"), scene = ScenePath, library = SS2DSpriteLibrary.DefaultAssetPath, stage = StageCode, capture = capture };
            if (stage == null) { report.problems.Add("no shipped stage " + StageCode); Write(report); return; }
            CreateScene(lib, StageCode, ScenePath);
            Validate(stage, report);
            Write(report);
            Debug.Log("[SS2D] prototype scene: " + ScenePath + (report.ok ? " (validated clean)" : " PROBLEMS: " + string.Join("; ", report.problems)));
            if (!render) return;
            RenderSceneTo(ScenePath, "Docs/Reports/Renders/Art2D_Prototype_LEVEL-2_1080x1920.png", 1080, 1920, false);
            RenderSceneTo(ScenePath, "Docs/Reports/Renders/Art2D_Prototype_LEVEL-2_1080x1920_guides.png", 1080, 1920, true);
            // Verification frames for element kinds LEVEL-2 does not contain (camera, door + key); the saved scene stays on LEVEL-2.
            foreach (var code in new[] { "LEVEL-16", "LEVEL-11" })
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var pv = Object.FindObjectOfType<SS2DPreviewController>(); if (pv == null) break;
                pv.stageCode = code; pv.BuildPreview();
                RenderOpenScene("Docs/Reports/Renders/Art2D_Check_" + code + "_1080x1920.png", 1080, 1920, false);
            }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); // leave the LEVEL-2 scene as saved
        }

        public const string HudPrefabPath = "Assets/_ShadowSteps/Art2D/Prefabs/SS2DHud.prefab";

        /// <summary>HUD prefab for the composer: an empty object with the SS2DHudCanvas component; the canvas hierarchy is built at runtime in Bind.</summary>
        private static GameObject EnsureHudPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HudPrefabPath));
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            if (existing != null) return existing;
            var go = new GameObject("SS2DHud"); go.AddComponent<SS2DHudCanvas>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, HudPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void CreateScene(SS2DSpriteLibrary lib, string stageCode, string path)
        {
            var hudPrefab = EnsureHudPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); // by path after the scene switch
            hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            // Playable composition root: in Play mode the composer builds the session, input, HUD (this prefab) and, through the 2D flag, the sprite bridge.
            var pack2 = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var composer = new GameObject("GameSceneComposer").AddComponent<ShadowSteps.Runtime.GameSceneComposer>();
            composer.Use2DArtPresentation = true; composer.Art2DLibrary = lib; composer.HudPrefab = hudPrefab;
            var st = pack2.FindByCode(stageCode); composer.DebugStageId = st != null ? st.Id : 0;
            var root = new GameObject("SS2D_Prototype_Board");
            var renderer = root.AddComponent<ShadowSteps2DRenderer>(); renderer.library = lib;
            var preview = root.AddComponent<SS2DPreviewController>(); preview.library = lib; preview.stageCode = stageCode; preview.boardRenderer = renderer;
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" }; var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = lib.background; cam.transform.position = new Vector3(0f, 0f, -10f);
            var data = cam.GetUniversalAdditionalCameraData(); if (data != null) { data.renderPostProcessing = false; data.renderShadows = false; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; }
            var fitter = camGo.AddComponent<SS2DCameraFitter>(); fitter.board = renderer; preview.fitter = fitter;
            var hudGo = new GameObject("HUD"); var hud = hudGo.AddComponent<SS2DHudCanvas>(); preview.hud = hud;
            preview.BuildPreview();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void Validate(Stage stage, Report r)
        {
            var preview = Object.FindObjectOfType<SS2DPreviewController>(); var rend = preview != null ? preview.boardRenderer : null;
            if (rend == null || !rend.IsBuilt) { r.problems.Add("renderer not built"); r.ok = false; return; }
            r.width = stage.Width; r.height = stage.Height;
            foreach (var c in stage.FloorNodes()) r.floorNodes++;
            for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (stage.IsWall(c)) r.wallCells++; else if (stage.IsBlocked(c)) r.coverCells++; }
            foreach (var a in stage.FloorNodes()) foreach (var d in new[] { Direction.East, Direction.South }) if (stage.IsConnected(a, a.Neighbor(d))) r.edges++;
            r.doors = stage.Doors.Count; r.guards = stage.Guards.Count; r.cameras = stage.Cameras.Count; r.keyExpected = stage.Key.HasValue ? 1 : 0;
            var state = ShadowSteps.V3.StageState.Initial(stage);
            var gv = new HashSet<GridCoord>(); for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in ShadowSteps.V3.Sight.GuardVision(stage, state, ShadowSteps.V3.GuardTrack.PoseAt(stage.Guards[i], 0))) gv.Add(c);
            var cv = new HashSet<GridCoord>(); for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in ShadowSteps.V3.Sight.CameraVision(stage, state, i, 0)) cv.Add(c);
            r.guardVisionCells = gv.Count; r.cameraVisionCells = cv.Count;
            System.Func<string, string, bool> rx = (t, p) => System.Text.RegularExpressions.Regex.IsMatch(t, p);
            foreach (var t in rend.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (rx(n, "^Node_\\d+_\\d+$")) r.nodeSprites++;
                else if (rx(n, "^Path_")) r.lineSprites++;
                else if (rx(n, "^Cover_X_\\d+_\\d+$")) r.coverSprites++;
                else if (rx(n, "^Wall_\\d+_\\d+$")) r.wallSprites++;
                else if (rx(n, "^Door_\\d+$")) r.doorSprites++;
                else if (rx(n, "^Guard_\\d+$")) r.guardTokens++;
                else if (rx(n, "^Camera_\\d+$")) r.cameraSprites++;
                else if (n == "Key") r.keySprites++;
                else if (n == "Exit") r.exitSprites++;
                else if (n == "Door") r.doorSprites += 0; // exit door is counted separately below
                else if (n == "NodeRing" && t.gameObject.activeSelf && t.parent != null && (t.parent.name == "CameraVision" || t.parent.name == "GuardVision")) r.visionOverlaySprites++;
            }
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) if (mr.GetComponentInParent<CameraVisionOverlayRenderer>() == null) r.meshRenderers++; // the camera region is a flat 2D mesh on a sprite layer
                r.skinnedMeshRenderers += go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                r.spriteRenderers += go.GetComponentsInChildren<SpriteRenderer>(true).Length;
            }
            foreach (var dep in AssetDatabase.GetDependencies(ScenePath, true))
                if (dep.StartsWith("Assets/characters_5_02") || dep.StartsWith("Assets/Polygon") || dep.StartsWith("Assets/_ShadowSteps/Art3D") || dep.EndsWith(".fbx") || dep.EndsWith(".glb")) r.threeDAssetsReferenced.Add(dep);
            r.spyFramesComplete = rend.library.spy.IsComplete; r.guardFramesComplete = rend.library.guard.IsComplete;
            foreach (var set in new[] { rend.library.spy, rend.library.guard }) foreach (var f in set.facings) { if (f?.idle != null) r.characterFrames += f.idle.Length; if (f?.walk != null) r.characterFrames += f.walk.Length; }
            if (r.nodeSprites != r.floorNodes) r.problems.Add("node sprites " + r.nodeSprites + " != floor nodes " + r.floorNodes);
            if (r.lineSprites != r.edges) r.problems.Add("line sprites " + r.lineSprites + " != connections " + r.edges);
            if (r.coverSprites != r.coverCells) r.problems.Add("cover sprites " + r.coverSprites + " != X cells " + r.coverCells);
            if (r.wallSprites != r.wallCells) r.problems.Add("wall sprites " + r.wallSprites + " != # cells " + r.wallCells);
            if (r.doorSprites != r.doors) r.problems.Add("door sprites " + r.doorSprites + " != doors " + r.doors);
            if (r.guardTokens != r.guards) r.problems.Add("guard tokens " + r.guardTokens + " != guards " + r.guards);
            if (r.cameraSprites != r.cameras) r.problems.Add("camera sprites " + r.cameraSprites + " != cameras " + r.cameras);
            if (r.keySprites != r.keyExpected) r.problems.Add("key sprites " + r.keySprites + " != " + r.keyExpected);
            if (r.exitSprites != 1) r.problems.Add("exit sprites " + r.exitSprites + " != 1");
            var doorT = rend.transform.Find("Props/Door");
            if (doorT == null) r.problems.Add("exit door missing");
            else if (stage.Inside(rend.ToGrid(doorT.position)) && !stage.IsWall(rend.ToGrid(doorT.position)) && stage.IsFloor(rend.ToGrid(doorT.position))) r.problems.Add("exit door is drawn on a floor node");
            if (r.visionOverlaySprites != r.guardVisionCells + r.cameraVisionCells) r.problems.Add("vision overlay cells " + r.visionOverlaySprites + " != engine cells " + (r.guardVisionCells + r.cameraVisionCells));
            if (r.meshRenderers + r.skinnedMeshRenderers > 0) r.problems.Add("scene contains 3D renderers: " + r.meshRenderers + " mesh, " + r.skinnedMeshRenderers + " skinned");
            if (r.threeDAssetsReferenced.Count > 0) r.problems.Add("scene depends on 3D assets: " + string.Join(", ", r.threeDAssetsReferenced));
            if (!r.spyFramesComplete || !r.guardFramesComplete) r.problems.Add("character frames incomplete (placeholder discs in use)");
            if (r.capture != null && r.capture.problems.Count > 0) r.problems.Add("capture: " + string.Join("; ", r.capture.problems));
            r.ok = r.problems.Count == 0;
        }

        private static void Write(Report report)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(Path.Combine(root, "Docs/Reports"));
            File.WriteAllText(Path.Combine(root, ReportPath), JsonUtility.ToJson(report, true));
        }

        // ---- headless render (same approach as the 3D batch renderer; the HUD canvas is screen-space-camera so it is included)

        /// <summary>
        /// Camera demonstration: the standard level scene for LEVEL-16 (fixed camera) plus review frames rendered from
        /// real engine states for every camera kind and effect: active (LEVEL-16), EMP-disabled (LEVEL-23 after EMP#0),
        /// rotating (LEVEL-18 at t=1), sweeping (LEVEL-19 at t=2), guard + camera overlap with a door and smoke charge (LEVEL-25).
        /// </summary>
        public static void GenerateCameraDemo()
        {
            try
            {
                var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
                if (lib == null || lib.cameraLens == null) lib = SS2DSpriteGenerator.Generate();
                string scene = CreateLevelScene("LEVEL-16");
                RenderSceneTo(scene, "Docs/Reports/Renders/Art2D_Camera_Active_LEVEL-16_1080x1920.png", 1080, 1920, false);
                RenderReview(scene, "LEVEL-23", new[] { "EMP#0" }, "Art2D_Camera_EMP_LEVEL-23_1080x1920");
                RenderReview(scene, "LEVEL-18", new[] { "WAIT" }, "Art2D_Camera_Rotating_t1_LEVEL-18_1080x1920");
                RenderReview(scene, "LEVEL-19", new[] { "WAIT", "WAIT" }, "Art2D_Camera_Sweeping_t2_LEVEL-19_1080x1920");
                RenderReview(scene, "LEVEL-25", null, "Art2D_Camera_Overlap_LEVEL-25_1080x1920");
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single); // leave the saved LEVEL-16 scene untouched
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] camera demo failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        /// <summary>Opens the scene, switches the edit-mode preview to another stage/state (not saved) and renders it.</summary>
        private static void RenderReview(string scenePath, string stageCode, string[] actions, string name)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var pv = Object.FindObjectOfType<SS2DPreviewController>(); if (pv == null) return;
            pv.stageCode = stageCode; pv.previewActions = actions; pv.BuildPreview();
            RenderOpenScene("Docs/Reports/Renders/" + name + ".png", 1080, 1920, false);
        }

        /// <summary>Batch entry: re-render the saved prototype scene without regenerating anything (-ssName optional).</summary>
        public static void RenderOnly()
        {
            try
            {
                string name = "Art2D_Prototype_LEVEL-2_1080x1920";
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ssName") name = args[i + 1];
                RenderSceneTo(ScenePath, "Docs/Reports/Renders/" + name + ".png", 1080, 1920, false);
            }
            catch (System.Exception e) { Debug.LogError("[SS2D] render failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        private static void LogHudState()
        {
            var hud = Object.FindObjectOfType<SS2DHudCanvas>();
            if (hud == null) { Debug.Log("[SS2D] HUD: none in scene"); return; }
            var c = hud.canvas;
            Debug.Log("[SS2D] HUD: active=" + hud.gameObject.activeInHierarchy + " canvas=" + (c != null) + (c != null ? " mode=" + c.renderMode + " layer=" + c.sortingLayerName + " order=" + c.sortingOrder + " cam=" + (c.worldCamera != null ? c.worldCamera.name : "null") + " plane=" + c.planeDistance + " children=" + c.transform.childCount + " enabled=" + c.enabled + " scale=" + c.transform.localScale : ""));
        }

        public static void RenderSceneTo(string scenePath, string outPath, int w, int h, bool guides)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var pv = Object.FindObjectOfType<SS2DPreviewController>(); if (pv != null) pv.BuildPreview();
            RenderOpenScene(outPath, w, h, guides);
        }

        public static void RenderOpenScene(string outPath, int w, int h, bool guides)
        {
            var cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.aspect = (float)w / h;
            var fitter = cam.GetComponent<SS2DCameraFitter>(); if (fitter != null) fitter.Fit((float)w / h);
            LogHudState();
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt);
            if (guides)
            {
                float top = fitter != null ? fitter.topReservedFraction : 0.14f, bottom = fitter != null ? fitter.bottomReservedFraction : 0.22f;
                var px = tex.GetPixels32(); int topY = Mathf.RoundToInt(h * (1f - top)), bottomY = Mathf.RoundToInt(h * bottom);
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                {
                    bool band = y >= topY || y < bottomY; bool line = Mathf.Abs(y - topY) <= 1 || Mathf.Abs(y - bottomY) <= 1 || x == w / 2;
                    if (line) px[y * w + x] = new Color32(255, 40, 40, 255);
                    else if (band) { var c = px[y * w + x]; px[y * w + x] = new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), 255); }
                }
                tex.SetPixels32(px); tex.Apply();
            }
            string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), outPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            Debug.Log("[SS2D] rendered -> " + outPath);
        }
    }
}
