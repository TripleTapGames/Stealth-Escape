using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Art review of one stage with the 2D renderer: loads the shipped levels_v4.json, builds the stage and shows the
    /// engine's initial hazards, reachable nodes and markers. Not a playable runtime (that is <see cref="SS2DSessionBridge"/>
    /// in the Game scene); it rebuilds itself whenever the scene loads so the saved scene stays in sync with the sprites.
    /// </summary>
    [ExecuteAlways]
    public sealed class SS2DPreviewController : MonoBehaviour
    {
        public SS2DSpriteLibrary library;
        public string stageCode = "LEVEL-2";
        public ShadowSteps2DRenderer boardRenderer;
        public SS2DCameraFitter fitter;
        public SS2DHudCanvas hud;
        public bool showInitialHazards = true;
        public bool rebuildOnLoad = true;
        public Stage Stage { get; private set; }

        private void OnEnable()
        {
            if (!rebuildOnLoad || boardRenderer == null || library == null) return;
            if (Application.isPlaying)
            {
                // A composed (playable) scene drives its own renderer through the session bridge; the art preview steps aside.
                if (FindObjectOfType<ShadowSteps.Runtime.GameSceneComposer>() != null) { boardRenderer.Clear(); if (hud != null) hud.gameObject.SetActive(false); enabled = false; return; }
                BuildPreview(); return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && !Application.isPlaying && isActiveAndEnabled) BuildPreview(); };
#endif
        }

        [ContextMenu("Build Preview")]
        public void BuildPreview()
        {
            if (boardRenderer == null || library == null) { Debug.LogError("[SS2D] assign a renderer and a sprite library"); return; }
            boardRenderer.library = library;
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            var stage = pack.FindByCode(stageCode) ?? pack.Stages[0];
            Stage = stage;
            boardRenderer.Build(stage);
            var state = StageState.Initial(stage);
            boardRenderer.SetPlayer(state.Player);
            for (int i = 0; i < stage.Guards.Count; i++) boardRenderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], 0), GuardTrack.NextStep(stage.Guards[i], 0));
            for (int i = 0; i < stage.Cameras.Count; i++) boardRenderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], 0), 0, false);
            for (int i = 0; i < stage.Doors.Count; i++) boardRenderer.SetDoor(i, state.IsDoorOpen(i));
            boardRenderer.SetKeyCollected(state.HasKey);
            if (showInitialHazards)
            {
                var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
                for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], 0))) if (seen.Add(c)) guardCells.Add(c);
                var camCells = new List<GridCoord>(); seen.Clear();
                for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, 0)) if (seen.Add(c)) camCells.Add(c);
                boardRenderer.SetVision(guardCells, camCells);
            }
            var reachNodes = new List<GridCoord>(); var reachEdges = new List<EdgeKey>();
            for (int d = 0; d < 4; d++)
            {
                var n = state.Player.Neighbor((Direction)d);
                if (!stage.IsConnected(state.Player, n)) continue;
                int door = stage.DoorIndex(state.Player, n);
                if (door >= 0 && !state.IsDoorOpen(door) && !state.HasKey) continue;
                reachNodes.Add(n); reachEdges.Add(new EdgeKey(state.Player, n));
            }
            boardRenderer.SetReachable(reachNodes, reachEdges);
            var cam = fitter != null ? fitter.GetComponent<Camera>() : Camera.main;
            if (cam != null) { cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = library.background; }
            if (fitter != null) { fitter.board = boardRenderer; fitter.Fit(cam != null ? cam.aspect : 9f / 16f); }
            if (hud != null) hud.Show(stage, state, cam);
            Debug.Log("[SS2D] preview built: " + stage.Code + " " + stage.Title);
        }
    }
}
