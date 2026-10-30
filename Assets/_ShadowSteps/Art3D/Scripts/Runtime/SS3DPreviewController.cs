using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Art review only: loads the shipped schema-3 pack (levels_v4.json) and shows one stage with the 3D renderer,
    /// the courtyard frame and the initial-state hazards computed by the real engine. Not a playable runtime; the
    /// playable path is <see cref="SS3DSessionBridge"/>.
    /// </summary>
    [ExecuteAlways]
    public sealed class SS3DPreviewController : MonoBehaviour
    {
        [Tooltip("Optional TextAsset with a schema-3 pack; when empty the StreamingAssets levels_v4.json is read.")]
        public TextAsset packJson;
        [Tooltip("Palette used for the renderer and the frame. When empty, the renderer's palette or (in the editor) the default asset path is used.")]
        public SS3DArtPalette palette;
        public string stageCode = "LEVEL-2";
        public SS3DLevelRenderer levelRenderer;
        public SS3DCourtyardFrame exteriorFrame;
        public SS3DBoardCameraFitter boardFitter;
        public bool showInitialHazards = true;
        [Tooltip("Rebuild the preview whenever the scene loads. Generated meshes, materials and the post-processing profile are not assets, so a saved scene only stores the seed; this keeps it self-healing.")]
        public bool rebuildOnLoad = true;

        private void OnEnable()
        {
            if (!rebuildOnLoad || levelRenderer == null || palette == null) return; // freshly added by the generator: it calls BuildPreview itself
            if (Application.isPlaying) { BuildPreview(); return; }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && !Application.isPlaying && isActiveAndEnabled) BuildPreview(); };
#endif
        }

        private SS3DArtPalette ResolvePalette()
        {
            if (palette != null) return palette;
            if (levelRenderer != null && levelRenderer.palette != null) return levelRenderer.palette;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<SS3DArtPalette>(SS3DArtPalette.DefaultAssetPath);
#else
            return null;
#endif
        }

        [ContextMenu("Build Preview")]
        public void BuildPreview()
        {
            if (levelRenderer == null) { Debug.LogError("[SS3D] assign a level renderer"); return; }
            var resolved = ResolvePalette();
            if (resolved == null) { Debug.LogError("[SS3D] no SS3DArtPalette: assign one on the preview controller or create " + SS3DArtPalette.DefaultAssetPath + " via Shadow Steps ▸ 3D ▸ Create or update Art Palette."); return; }
            var problems = new System.Collections.Generic.List<string>();
            bool usable = resolved.Validate(problems);
            foreach (var pr in problems) { if (usable) Debug.LogWarning("[SS3D] " + pr); else Debug.LogError("[SS3D] " + pr); }
            if (!usable) return;
            palette = resolved; levelRenderer.palette = resolved;
            if (exteriorFrame != null) exteriorFrame.palette = resolved;
            string json = packJson != null ? packJson.text : File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json"));
            var pack = StageJson.Parse(json);
            var stage = pack.FindByCode(stageCode) ?? pack.Stages[0];
            levelRenderer.Build(stage);
            var state = StageState.Initial(stage);
            for (int i = 0; i < stage.Guards.Count; i++) levelRenderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], 0), GuardTrack.NextStep(stage.Guards[i], 0));
            for (int i = 0; i < stage.Cameras.Count; i++) levelRenderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], 0), 0, false);
            if (showInitialHazards)
            {
                var guardCells = new System.Collections.Generic.List<GridCoord>();
                for (int i = 0; i < stage.Guards.Count; i++) guardCells.AddRange(Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], 0)));
                var camCells = new System.Collections.Generic.List<GridCoord>();
                for (int i = 0; i < stage.Cameras.Count; i++) { var cells = Sight.CameraVision(stage, state, i, 0); levelRenderer.SetCameraCoverage(i, cells); camCells.AddRange(cells); }
                levelRenderer.SetVision(guardCells, camCells);
            }
            if (boardFitter != null) { boardFitter.board = levelRenderer; boardFitter.usePerspective = resolved.usePerspectiveCamera; boardFitter.fieldOfView = resolved.fieldOfView; boardFitter.paddingTiles = resolved.cameraPaddingTiles; boardFitter.minWidthTiles = resolved.minFramedTiles.x; boardFitter.minDepthTiles = resolved.minFramedTiles.y; boardFitter.Fit(levelRenderer.BoardBounds()); }
            if (exteriorFrame != null) { exteriorFrame.fitter = boardFitter; exteriorFrame.Build(levelRenderer); }
            var cam = boardFitter != null ? boardFitter.GetComponent<Camera>() : Camera.main;
            SS3DArtTuning.ApplyLighting(levelRenderer.palette, cam);
            SS3DArtTuning.ApplyPostProcessing(levelRenderer.palette, cam);
            Debug.Log("[SS3D] preview built: " + stage.Code + " " + stage.Title);
        }
    }
}
