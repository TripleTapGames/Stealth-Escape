using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stage = ShadowSteps.V3.Stage;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// First10 courtyard levels: reads ShadowSteps_First10_Courtyard_Art_Pack/Data/first10_art_manifest.json (project
    /// root, outside Assets), maps each entry to the shipped stage with the same id, builds an ART PREVIEW scene per
    /// entry under Assets/_ShadowSteps/Art3D/Levels/First10/ with the sunlit theme, validates the generated objects
    /// against the stage data and writes Docs/Reports/First10_Scene_Manifest.json. Playable levels use the Game scene
    /// with Use3DPresentation (same renderer, same palette); these scenes are static previews.
    /// </summary>
    public static class SS3DFirst10Generator
    {
        public const string PackDir = "ShadowSteps_First10_Courtyard_Art_Pack";
        public const string SceneDir = "Assets/_ShadowSteps/Art3D/Levels/First10";
        public const string ReportPath = "Docs/Reports/First10_Scene_Manifest.json";

        [System.Serializable] private class ManifestEntry { public int sequence, source_level_id; public string source_label, source_title, scene_filename; }
        [System.Serializable] private class Manifest { public string manifest_version; public List<ManifestEntry> levels; }
        [System.Serializable] private class SceneReport { public int sequence, stageId; public string code, title, scene, variant; public int width, height, floorNodes, nodeMarkers, wallCells, wallPieces, coverCells, coverProps, guards, guardActors, connections, connectionLines; public string start, exit, exitMarker; public int par; public bool ok; public List<string> problems = new List<string>(); }
        [System.Serializable] private class Report { public string generated, palette, theme; public int count; public List<SceneReport> scenes = new List<SceneReport>(); }

        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static bool PackPresent => File.Exists(Path.Combine(ProjectRoot, PackDir, "Data", "first10_art_manifest.json"));

        /// <summary>Puts the palette into the sunlit theme (kit stays mapped for foliage/cover), orthographic fixed camera, wider padding so the moat shows.</summary>
        public static SS3DArtPalette ApplySunlitTheme()
        {
            var palette = SS3DSetupMenu.EnsurePalette();
            palette.sunlitCourtyard = true;
            palette.usePerspectiveCamera = false;
            palette.sunlitMarginTiles = 1.5f;
            palette.cameraPaddingTiles = palette.sunlitMarginTiles + 0.6f; // margin + plinth + a sliver of moat: the platform fills the width, the grid sits in open floor
            palette.minFramedTiles = new Vector2(6f, 4f);
            palette.kitAccents = false;
            palette.proceduralFloorTiles = true;
            palette.wallHeightScale = 0.5f;
            palette.coverHeightScale = 0.62f;
            palette.tintMaterials = true;
            palette.waterTurquoise = new Color(0.16f, 0.62f, 0.74f, 1f);
            palette.waterEmission = new Color(0.03f, 0.16f, 0.2f, 1f);
            EditorUtility.SetDirty(palette); AssetDatabase.SaveAssets();
            return palette;
        }

        [MenuItem("Shadow Steps/3D/Generate First 10 Courtyard Levels")]
        public static void Generate()
        {
            if (!PackPresent) { Debug.LogError("[SS3D] " + PackDir + "/Data/first10_art_manifest.json not found beside Assets."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(ProjectRoot, PackDir, "Data", "first10_art_manifest.json")));
            if (manifest == null || manifest.levels == null || manifest.levels.Count != 10) { Debug.LogError("[SS3D] First10 manifest did not parse to 10 entries."); return; }
            ApplySunlitTheme();
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            Directory.CreateDirectory(Path.Combine(ProjectRoot, SceneDir));
            var report = new Report { generated = System.DateTime.Now.ToString("s"), palette = SS3DArtPalette.DefaultAssetPath, theme = "SunlitCourtyard" };
            var openPath = EditorSceneManager.GetActiveScene().path;
            foreach (var e in manifest.levels)
            {
                Stage stage = null; foreach (var st in pack.Stages) if (st.Id == e.source_level_id) { stage = st; break; }
                var sr = new SceneReport { sequence = e.sequence, stageId = e.source_level_id, title = e.source_title, scene = SceneDir + "/" + e.scene_filename };
                if (stage == null) { sr.problems.Add("no shipped stage with id " + e.source_level_id); report.scenes.Add(sr); continue; }
                sr.code = stage.Code; sr.width = stage.Width; sr.height = stage.Height; sr.par = stage.Par; sr.start = stage.Start.ToString(); sr.exit = stage.Exit.ToString();
                if (stage.Title != e.source_title) sr.problems.Add("manifest title '" + e.source_title + "' differs from shipped '" + stage.Title + "' (shipped kept)");
                sr.variant = Describe(SunlitVariant.For(stage.Id));
                if (openPath == sr.scene) { sr.problems.Add("scene is open in the editor; close it and regenerate"); report.scenes.Add(sr); continue; }
                SS3DSetupMenu.CreateSceneAt(stage.Code, sr.scene, "SS3D_First10_ArtPreview_Only", renderer => Validate(stage, renderer, sr));
                report.scenes.Add(sr);
            }
            report.count = report.scenes.Count;
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Docs/Reports"));
            File.WriteAllText(Path.Combine(ProjectRoot, ReportPath), JsonUtility.ToJson(report, true));
            int okCount = 0; foreach (var sr in report.scenes) if (sr.ok) okCount++;
            Debug.Log("[SS3D] First10: " + report.scenes.Count + " scenes generated in " + SceneDir + ", " + okCount + " validated clean. Report: " + ReportPath);
        }

        private static string Describe(SunlitVariant v)
        {
            var parts = new List<string>();
            if (v.waterfallNiches) parts.Add("waterfall niches"); if (v.benches) parts.Add("benches"); if (v.cornerTrees) parts.Add("corner trees");
            if (v.flowerBeds) parts.Add("flower beds"); if (v.extraLanterns) parts.Add("newel lanterns"); if (v.pinkFlowers) parts.Add("pink flowers");
            return parts.Count == 0 ? "base courtyard" : string.Join(", ", parts);
        }

        /// <summary>Counts generated gameplay objects against the stage: nodes, walls, cover, connections, guards, exit.</summary>
        private static void Validate(Stage stage, SS3DLevelRenderer r, SceneReport sr)
        {
            int floor = 0, walls = 0, cover = 0; foreach (var c in stage.FloorNodes()) floor++;
            for (int y = 0; y < stage.Height; y++) for (int x = 0; x < stage.Width; x++) { var c = new GridCoord(x, y); if (stage.IsWall(c)) walls++; else if (stage.IsBlocked(c)) cover++; }
            int conns = 0; foreach (var a in stage.FloorNodes()) { if (stage.IsConnected(a, a.Neighbor(Direction.East))) conns++; if (stage.IsConnected(a, a.Neighbor(Direction.South))) conns++; }
            sr.floorNodes = floor; sr.wallCells = walls; sr.coverCells = cover; sr.guards = stage.Guards.Count; sr.connections = conns;
            System.Func<string, string, bool> rx = (text, pattern) => System.Text.RegularExpressions.Regex.IsMatch(text, pattern);
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (rx(n, "^Node_\\d+_\\d+$")) sr.nodeMarkers++;
                else if (rx(n, "^Cover_X_\\d+_\\d+$")) sr.coverProps++;
                else if (rx(n, "^(SolidWall|WallPost)_\\d+_\\d+$")) sr.wallPieces++;
                else if (rx(n, "^Guard_\\d+$")) sr.guardActors++;
                else if (n.StartsWith("Path_")) sr.connectionLines++;
                else if (n == "Exit") sr.exitMarker = r.ToGrid(t.position).ToString();
            }
            if (sr.nodeMarkers != floor) sr.problems.Add("node markers " + sr.nodeMarkers + " != floor nodes " + floor);
            if (sr.coverProps != cover) sr.problems.Add("cover props " + sr.coverProps + " != X cells " + cover);
            if (sr.wallPieces != walls) sr.problems.Add("wall pieces " + sr.wallPieces + " != # cells " + walls);
            if (sr.guardActors != stage.Guards.Count) sr.problems.Add("guard actors " + sr.guardActors + " != guards " + stage.Guards.Count);
            if (sr.connectionLines != conns) sr.problems.Add("connection lines " + sr.connectionLines + " != graph edges " + conns);
            if (sr.exitMarker != null && sr.exitMarker != stage.Exit.ToString()) sr.problems.Add("exit marker at " + sr.exitMarker + " but stage exit is " + stage.Exit);
            if (stage.Cameras.Count != 0) sr.problems.Add("stage has cameras; First10 stages should have none");
            sr.ok = sr.problems.Count == 0;
        }
    }
}
