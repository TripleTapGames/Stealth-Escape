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
        [Tooltip("Optional town environment; when set with a profile it replaces the courtyard frame and supplies cover/wall props and floor tints.")]
        public TownEnvironmentGenerator townEnvironment;
        [Tooltip("Optional authored plaza layout; takes precedence over the town environment generator and the courtyard frame.")]
        public TownPlazaComposer plazaComposer;
        [Tooltip("Optional minimalist tabletop board (isolated prototype); takes precedence over every other environment.")]
        public MinimalBoardComposer minimalBoard;
        [Tooltip("Camera padding in tiles for this scene (0 = palette value).")]
        public float paddingOverride = 0f;
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
            bool minimal = minimalBoard != null;
            bool plaza = !minimal && plazaComposer != null && plazaComposer.layout != null;
            bool town = !plaza && !minimal && townEnvironment != null && townEnvironment.profile != null;
            levelRenderer.overrides = minimal ? minimalBoard.RendererOverrides() : plaza ? plazaComposer.layout.RendererOverrides() : town ? townEnvironment.profile.RendererOverrides() : null;
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
            if (boardFitter != null) { boardFitter.board = levelRenderer; boardFitter.usePerspective = resolved.usePerspectiveCamera; boardFitter.fieldOfView = resolved.fieldOfView; boardFitter.paddingTiles = paddingOverride > 0f ? paddingOverride : resolved.cameraPaddingTiles; boardFitter.minWidthTiles = town || plaza || minimal ? 0f : resolved.minFramedTiles.x; boardFitter.minDepthTiles = town || plaza || minimal ? 0f : resolved.minFramedTiles.y; if (plaza) plazaComposer.ApplyCamera(boardFitter); if (minimal) minimalBoard.ApplyCamera(boardFitter); boardFitter.Fit(levelRenderer.BoardBounds()); }
            var cam = boardFitter != null ? boardFitter.GetComponent<Camera>() : Camera.main;
            if (minimal)
            {
                minimalBoard.palette = resolved; minimalBoard.Build(levelRenderer);
                if (exteriorFrame != null) exteriorFrame.Clear(); if (townEnvironment != null) townEnvironment.Clear(); if (plazaComposer != null) plazaComposer.Clear();
                minimalBoard.ApplyLighting(cam);
                Debug.Log("[SS3D] preview built: " + stage.Code + " " + stage.Title + " (minimal tabletop)");
                return;
            }
            if (plaza)
            {
                plazaComposer.fitter = boardFitter; plazaComposer.palette = resolved; plazaComposer.Build(levelRenderer);
                if (exteriorFrame != null) exteriorFrame.Clear(); if (townEnvironment != null) townEnvironment.Clear();
                plazaComposer.ApplyLighting(cam);
                Debug.Log("[SS3D] preview built: " + stage.Code + " " + stage.Title + " (plaza layout " + plazaComposer.layout.name + ")");
                return;
            }
            if (town) { townEnvironment.fitter = boardFitter; townEnvironment.palette = resolved; townEnvironment.Build(levelRenderer); if (exteriorFrame != null) exteriorFrame.Clear(); townEnvironment.profile.ApplyLighting(resolved, cam); }
            else
            {
                if (exteriorFrame != null) { exteriorFrame.fitter = boardFitter; exteriorFrame.Build(levelRenderer); }
                SS3DArtTuning.ApplyLighting(levelRenderer.palette, cam);
            }
            SS3DArtTuning.ApplyPostProcessing(levelRenderer.palette, cam);
            Debug.Log("[SS3D] preview built: " + stage.Code + " " + stage.Title);
        }
    }
}
