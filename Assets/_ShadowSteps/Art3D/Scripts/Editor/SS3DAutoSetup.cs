using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ShadowSteps.Art3D.Editor
{
    /// <summary>
    /// Runs once per editor session after scripts reload: builds the palette if it is missing or has empty slots and
    /// generates the two demo scenes if they do not exist yet, additively so the scene you are working in is untouched.
    /// Everything it does is also available from the Shadow Steps ▸ 3D menu; it never overwrites existing scenes or
    /// hand-picked palette entries.
    /// </summary>
    [InitializeOnLoad]
    public static class SS3DAutoSetup
    {
        private static string SessionKey => "SS3D.AutoSetup.Done." + GeneratorVersion; // per version: a bump re-runs in the same editor session
        /// <summary>Bump when the generator output changes; the two demo scenes are regenerated once per version.</summary>
        public const int GeneratorVersion = 9;
        private static string VersionKey => "SS3D.GeneratorVersion." + Application.dataPath.GetHashCode();
        private static readonly (string code, string file)[] Demos = { ("LEVEL-2", "Clockwise_Rotating_Guard"), ("LEVEL-16", "Camera_Basics") };

        static SS3DAutoSetup() { EditorApplication.delayCall += Run; }

        private static void Run()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            SessionState.SetBool(SessionKey, true);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SS3DSetupMenu.Models + "Floor_Tile_A.glb") == null) { Debug.Log("[SS3D] auto-setup skipped: .glb models not imported yet (glTFast)."); return; }
                if (EditorPrefs.GetInt(VersionKey, 0) < 6) SS3DSetupMenu.ConfigureUrp(); // rendering pass: once
                var palette = SS3DSetupMenu.BuildPalette(overwriteModels: false);
                if (SS3DKitSetup.KitPresent && (EditorPrefs.GetInt(VersionKey, 0) < GeneratorVersion || !palette.kitMode)) SS3DKitSetup.MapKit(); // environment kit: materials to URP + palette slots, re-mapped on every generator version
                if (!palette.HasCoreModels) { Debug.LogWarning("[SS3D] auto-setup: palette is missing core models; check Shadow Steps ▸ 3D ▸ Validate imported models."); return; }
                bool regenerate = EditorPrefs.GetInt(VersionKey, 0) != GeneratorVersion;
                foreach (var (code, file) in Demos)
                {
                    string path = SS3DSetupMenu.SceneDir + "/" + file + ".unity";
                    if (File.Exists(path) && !regenerate) continue;
                    if (File.Exists(path) && EditorSceneManager.GetActiveScene().path == path) { Debug.LogWarning("[SS3D] " + path + " is open; close it to let auto-setup regenerate it (or use Shadow Steps ▸ 3D ▸ Create demo scenes)."); continue; }
                    CreateAdditive(code, path);
                }
                EditorPrefs.SetInt(VersionKey, GeneratorVersion);
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] auto-setup failed: " + e); }
        }

        /// <summary>Builds a preview scene in an additive scene and saves it, leaving the active scene as it was.</summary>
        public static void CreateAdditive(string stageCode, string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var palette = SS3DSetupMenu.LoadValidatedPalette(); // by path, after the scene switch
                var root = new GameObject("SS3D_Visual_Preview_Only");
                SceneManager.MoveGameObjectToScene(root, scene);
                var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
                var frame = root.AddComponent<SS3DCourtyardFrame>(); frame.palette = palette;
                var preview = root.AddComponent<SS3DPreviewController>();
                preview.palette = palette; preview.stageCode = stageCode; preview.levelRenderer = renderer; preview.exteriorFrame = frame;
                var camObj = new GameObject("Camera_Fixed_Ortho") { tag = "MainCamera" };
                SceneManager.MoveGameObjectToScene(camObj, scene);
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = palette.backgroundColor;
                var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter;
                var sunObj = new GameObject("Warm_Directional_Sun");
                SceneManager.MoveGameObjectToScene(sunObj, scene);
                sunObj.AddComponent<Light>().type = LightType.Directional;
                preview.BuildPreview(); // applies palette lighting to the sun and camera
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                EditorSceneManager.SaveScene(scene, path);
                Debug.Log("[SS3D] preview scene created: " + path);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
