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
        [Tooltip("Art-review state: actions applied to the initial state through the real rules before drawing (tokens: N/E/S/W, WAIT, SMOKE@x,y, EMP#i). Invalid tokens are logged and skipped.")]
        public string[] previewActions;
        [Tooltip("Rules mode of the art-review state (D-040): Knockout lets previewActions contain KO#i or a move onto a guard.")]
        public StageMode previewMode = StageMode.Stealth;
        public bool rebuildOnLoad = true;
        public Stage Stage { get; private set; }
        /// <summary>Not serialized: a stage built in code (review frames for synthetic layouts) takes precedence over <see cref="stageCode"/>.</summary>
        public Stage OverrideStage { get; set; }

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
            Stage stage = OverrideStage;
            var pack = StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
            if (stage == null) stage = pack.FindByCode(stageCode) ?? pack.Stages[0];
            Stage = stage;
            boardRenderer.Build(stage);
            var state = StageState.Initial(stage, previewMode);
            if (previewActions != null)
                foreach (var token in previewActions)
                {
                    if (string.IsNullOrWhiteSpace(token)) continue;
                    if (!StageAction.TryParse(token, out var action)) { Debug.LogWarning("[SS2D] preview action not understood: " + token); continue; }
                    var outcome = StageRules.TryApply(stage, state, action);
                    if (!outcome.Valid) { Debug.LogWarning("[SS2D] preview action rejected: " + token + " (" + outcome.Reason + ")"); continue; }
                    state = outcome.After;
                }
            int t = state.T;
            boardRenderer.SetPlayer(state.Player);
            for (int i = 0; i < stage.Guards.Count; i++) { boardRenderer.SetGuardDefeated(i, state.IsDefeated(i)); if (!state.IsDefeated(i)) boardRenderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], t), GuardTrack.NextStep(stage.Guards[i], t)); } // revive before posing: a knocked-out guard ignores poses
            for (int i = 0; i < stage.Cameras.Count; i++) boardRenderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], t), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) boardRenderer.SetDoor(i, state.IsDoorOpen(i));
            boardRenderer.SetKeyCollected(state.HasKey);
            boardRenderer.SetClouds(state.Clouds);
            if (showInitialHazards)
            {
                var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
                for (int i = 0; i < stage.Guards.Count; i++) { if (state.IsDefeated(i)) continue; foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], t))) if (seen.Add(c)) guardCells.Add(c); }
                var camCells = new List<GridCoord>(); seen.Clear();
                for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, t)) if (seen.Add(c)) camCells.Add(c);
                boardRenderer.SetVision(guardCells, camCells);
            }
            if (state.KnockoutsEnabled && !state.IsTerminal)
            {
                var cells = new List<GridCoord>(); foreach (int g in StageRules.KnockoutTargets(stage, state)) cells.Add(GuardTrack.PositionAt(stage.Guards[g], t));
                if (cells.Count > 0) boardRenderer.SetKnockoutTargets(cells);
            }
            var reachNodes = new List<GridCoord>(); var reachEdges = new List<EdgeKey>();
            for (int d = 0; d < 4; d++)
            {
                var nb = stage.Neighbor(state.Player, (Direction)d); // next connected node, across passage cells (D-047)
                if (!nb.HasValue) continue;
                var n = nb.Value;
                int door = stage.DoorIndex(state.Player, n);
                if (door >= 0 && !state.IsDoorOpen(door) && !state.HasKey) continue;
                reachNodes.Add(n); reachEdges.Add(new EdgeKey(state.Player, n));
            }
            boardRenderer.SetReachable(reachNodes, reachEdges);
            var cam = fitter != null ? fitter.GetComponent<Camera>() : Camera.main;
            if (cam != null) { cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = library.background; }
            if (fitter != null) { fitter.board = boardRenderer; fitter.SetReferenceFromPack(pack.Stages); fitter.Fit(cam != null ? cam.aspect : 9f / 16f); }
            boardRenderer.FitBackground(cam);
            if (hud != null) hud.Show(stage, state, cam);
            Debug.Log("[SS2D] preview built: " + stage.Code + " " + stage.Title);
        }
    }
}
