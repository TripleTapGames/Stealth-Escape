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
        private const string SessionKey = "SS3D.AutoSetup.Done";
        private static readonly (string code, string file)[] Demos = { ("LEVEL-2", "Clockwise_Rotating_Guard"), ("LEVEL-16", "Camera_Basics") };

        static SS3DAutoSetup() { EditorApplication.delayCall += Run; }

        private static void Run()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            SessionState.SetBool(SessionKey, true);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SS3DSetupMenu.Models + "Floor_Tile_A.glb") == null) { Debug.Log("[SS3D] auto-setup skipped: .glb models not imported yet (glTFast)."); return; }
                var palette = SS3DSetupMenu.BuildPalette(overwriteModels: false);
                if (!palette.HasCoreModels) { Debug.LogWarning("[SS3D] auto-setup: palette is missing core models; check Shadow Steps ▸ 3D ▸ Validate imported models."); return; }
                foreach (var (code, file) in Demos)
                {
                    string path = SS3DSetupMenu.SceneDir + "/" + file + ".unity";
                    if (File.Exists(path)) continue;
                    CreateAdditive(code, path, palette);
                }
            }
            catch (System.Exception e) { Debug.LogError("[SS3D] auto-setup failed: " + e); }
        }

        /// <summary>Builds a preview scene in an additive scene and saves it, leaving the active scene as it was.</summary>
        public static void CreateAdditive(string stageCode, string path, SS3DArtPalette palette)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("SS3D_Visual_Preview_Only");
                SceneManager.MoveGameObjectToScene(root, scene);
                var renderer = root.AddComponent<SS3DLevelRenderer>(); renderer.palette = palette;
                var frame = root.AddComponent<SS3DCourtyardFrame>(); frame.palette = palette;
                var preview = root.AddComponent<SS3DPreviewController>();
                preview.stageCode = stageCode; preview.levelRenderer = renderer; preview.exteriorFrame = frame;
                var camObj = new GameObject("Camera_Fixed_Ortho") { tag = "MainCamera" };
                SceneManager.MoveGameObjectToScene(camObj, scene);
                var cam = camObj.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.67f, 0.84f, 0.87f);
                var fitter = camObj.AddComponent<SS3DBoardCameraFitter>(); fitter.board = renderer; preview.boardFitter = fitter;
                var sunObj = new GameObject("Warm_Directional_Sun");
                SceneManager.MoveGameObjectToScene(sunObj, scene);
                var sun = sunObj.AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(1f, 0.91f, 0.76f);
                sunObj.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                preview.BuildPreview();
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
