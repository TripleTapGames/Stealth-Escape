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
        public string stageCode = "LEVEL-2";
        public SS3DLevelRenderer levelRenderer;
        public SS3DCourtyardFrame exteriorFrame;
        public SS3DBoardCameraFitter boardFitter;
        public bool showInitialHazards = true;

        [ContextMenu("Build Preview")]
        public void BuildPreview()
        {
            if (levelRenderer == null) { Debug.LogError("[SS3D] assign a level renderer"); return; }
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
            if (boardFitter != null) { boardFitter.board = levelRenderer; boardFitter.Fit(levelRenderer.BoardBounds()); }
            if (exteriorFrame != null) { exteriorFrame.fitter = boardFitter; exteriorFrame.Build(levelRenderer); }
            SS3DArtTuning.ApplyLighting(levelRenderer.palette, boardFitter != null ? boardFitter.GetComponent<Camera>() : Camera.main);
            Debug.Log("[SS3D] preview built: " + stage.Code + " " + stage.Title);
        }
    }
}
