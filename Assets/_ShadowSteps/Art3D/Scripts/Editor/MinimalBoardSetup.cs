using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Stage = ShadowSteps.V3.Stage;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>Isolated minimalist tabletop prototype scene: procedural slab + the data-driven grid, nothing else. Independent of the town, courtyard and palace scenes.</summary>
    public static class MinimalBoardSetup
    {
        public const string ScenePath = "Assets/_ShadowSteps/Art3D/Levels/Minimal/SS3D_Minimal_Tabletop.unity";
        public const string ReportPath = "Docs/Reports/Minimal_Tabletop_Manifest.json";
        public const string StageCode = "LEVEL-4";

        [System.Serializable] private class Report { public string generated, scene, stage; public int width, height, floorNodes, nodeMarkers, coverCells, coverProps, guards, guardActors, decorationObjects; public bool ok; public System.Collections.Generic.List<string> problems = new System.Collections.Generic.List<string>(); }

        [MenuItem("Shadow Steps/3D/Minimal/Generate minimalist tabletop scene")]
        public static void GenerateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var stage = pack.FindByCode(StageCode);
            var report = new Report { generated = System.DateTime.Now.ToString("s"), scene = ScenePath, stage = StageCode };
            if (stage == null) { report.problems.Add("no shipped stage " + StageCode); Write(report); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = SS3DSetupMenu.LoadValidatedPalette();
            var root = new GameObject("SS3D_Minimal_Tabletop_ArtPreview_Only");
            var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
            var board = root.AddComponent<MinimalBoardComposer>(); board.palette = palette;
            var crate = AssetDatabase.LoadAssetAtPath<GameObject>(SS3DSetupMenu.Models + "Obstacle_Crate_1x1.glb");
            var block = AssetDatabase.LoadAssetAtPath<GameObject>(SS3DSetupMenu.Models + "Obstacle_Stone_Block_1x1.glb");
            var list = new System.Collections.Generic.List<GameObject>(); if (crate != null) list.Add(crate); if (block != null) list.Add(block);
            board.coverProps = list.ToArray();
            var preview = root.AddComponent<SS3DPreviewController>();
            preview.palette = palette; preview.stageCode = StageCode; preview.levelRenderer = renderer; preview.minimalBoard = board;
            var camObj = new GameObject("Camera_Fixed") { tag = "MainCamera" };
            var cam = camObj.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = board.background;
            var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter;
            new GameObject("Warm_Directional_Sun").AddComponent<Light>().type = LightType.Directional;
            preview.BuildPreview();
            Validate(stage, renderer, board, report);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Write(report);
            Debug.Log("[SS3D] minimal tabletop scene: " + ScenePath + (report.ok ? " (validated clean)" : " PROBLEMS: " + string.Join("; ", report.problems)));
        }

        public static void GenerateAndRender()
        {
            try
            {
                GenerateScene();
                string name = "Minimal_Tabletop_1080x1920";
                var args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ssName") name = args[i + 1];
                SS3DBatchRender.RenderSceneTo(ScenePath, "Docs/Reports/Renders/" + name + ".png", 1080, 1920, false);
                SS3DBatchRender.RenderSceneTo(ScenePath, "Docs/Reports/Renders/" + name + "_guides.png", 1080, 1920, true);
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] minimal generate+render failed: " + e); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }

        private static void Validate(Stage stage, SS3DLevelRenderer r, MinimalBoardComposer board, Report sr)
        {
            int floor = 0, cover = 0; foreach (var c in stage.FloorNodes()) floor++;
            for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (!stage.IsWall(c) && stage.IsBlocked(c)) cover++; }
            sr.width = stage.Width; sr.height = stage.Height; sr.floorNodes = floor; sr.coverCells = cover; sr.guards = stage.Guards.Count;
            System.Func<string, string, bool> rx = (t, p) => System.Text.RegularExpressions.Regex.IsMatch(t, p);
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                if (rx(t.name, "^Node_\\d+_\\d+$")) sr.nodeMarkers++;
                else if (rx(t.name, "^Cover_X_\\d+_\\d+$")) sr.coverProps++;
                else if (rx(t.name, "^Guard_\\d+$")) sr.guardActors++;
            }
            var deco = board.transform.Find("Minimal_Board_Decoration_Only");
            if (deco != null) sr.decorationObjects = deco.childCount;
            if (sr.nodeMarkers != floor) sr.problems.Add("node markers " + sr.nodeMarkers + " != floor nodes " + floor);
            if (sr.coverProps != cover) sr.problems.Add("cover props " + sr.coverProps + " != X cells " + cover);
            if (sr.guardActors != stage.Guards.Count) sr.problems.Add("guard actors " + sr.guardActors + " != guards " + stage.Guards.Count);
            if (sr.decorationObjects != 4) sr.problems.Add("decoration objects " + sr.decorationObjects + " != 4 (slab top, side, base, ground)");
            sr.ok = sr.problems.Count == 0;
        }

        private static void Write(Report report)
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            Directory.CreateDirectory(Path.Combine(root, "Docs/Reports"));
            File.WriteAllText(Path.Combine(root, ReportPath), JsonUtility.ToJson(report, true));
        }
    }
}
