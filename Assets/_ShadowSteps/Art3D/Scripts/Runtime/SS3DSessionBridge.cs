using System.Collections;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Attaches the 3D presentation to a composed Game scene when <see cref="GameSceneComposer.Use3DPresentation"/> is
    /// on. Mirrors the authoritative <see cref="GameSessionController"/> (the same events the 2D HUD uses), hides the 2D
    /// sprite board, replaces the camera fit with the angled orthographic fitter and routes taps through a plane
    /// raycast. It never computes rules: every hazard cell comes from <see cref="Sight"/> and every pose from the tracks.
    /// </summary>
    public sealed class SS3DSessionBridge : MonoBehaviour
    {
        public SS3DLevelRenderer Renderer { get; private set; }
        public SS3DCourtyardFrame Frame { get; private set; }
        public SS3DBoardCameraFitter Fitter { get; private set; }
        public float moveDuration = 0.16f;

        private GameSessionController _session;
        private GameSceneComposer _composer;
        private Coroutine _tween;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { GameSceneHooks.OnComposed += Attach; }

        public static void Attach(GameSceneComposer composer)
        {
            if (composer == null || !composer.Use3DPresentation) return;
            var palette = composer.Art3DPalette as SS3DArtPalette;
            if (palette == null) palette = Resources.Load<SS3DArtPalette>("SS3DArtPalette");
            if (palette == null) { Debug.LogWarning("[SS3D] Use3DPresentation is on but no SS3DArtPalette is assigned; staying on the 2D board."); return; }
            var root = new GameObject("Art3D");
            var bridge = root.AddComponent<SS3DSessionBridge>();
            bridge.Renderer = root.AddComponent<SS3DLevelRenderer>(); bridge.Renderer.palette = palette;
            bridge.Frame = root.AddComponent<SS3DCourtyardFrame>(); bridge.Frame.palette = palette;
            bridge.Bind(composer);
        }

        public void Bind(GameSceneComposer composer)
        {
            _composer = composer; _session = composer.Session;
            // Hide the 2D board (it still runs its sync so rules/HUD paths stay identical) and retire its camera fitter.
            if (composer.Board != null) { var fitter2D = composer.BoardCamera.GetComponent<CameraFitter>(); if (fitter2D != null) fitter2D.enabled = false; composer.Board.gameObject.SetActive(false); }
            Fitter = composer.BoardCamera.GetComponent<SS3DBoardCameraFitter>() ?? composer.BoardCamera.gameObject.AddComponent<SS3DBoardCameraFitter>();
            Fitter.board = Renderer;
            SS3DArtTuning.ApplyLighting(Renderer.palette, composer.BoardCamera);
            if (composer.Input != null)
            {
                composer.Input.ScreenToNodeOverride = ScreenToNode;
                composer.Input.ScreenToCameraOverride = ScreenToCamera;
            }
            _session.StageLoaded += OnStageLoaded;
            _session.StateChanged += OnStateChanged;
            _session.ActionStarted += OnActionStarted;
            _session.ModeChanged += OnModeChanged;
            _session.PreviewShown += OnPreviewShown;
            _session.PreviewHidden += OnPreviewHidden;
            if (_session.HasStage) { OnStageLoaded(_session.Stage); OnStateChanged(_session.State); }
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.StageLoaded -= OnStageLoaded; _session.StateChanged -= OnStateChanged; _session.ActionStarted -= OnActionStarted;
            _session.ModeChanged -= OnModeChanged; _session.PreviewShown -= OnPreviewShown; _session.PreviewHidden -= OnPreviewHidden;
        }

        private void OnStageLoaded(Stage stage)
        {
            Renderer.Build(stage);
            Fitter.Fit(Renderer.BoardBounds());
            Frame.fitter = Fitter;
            Frame.Build(Renderer);
        }

        /// <summary>Snap every visual to the authoritative state (same data the 2D board shows).</summary>
        private void OnStateChanged(StageState state)
        {
            if (_tween != null) { StopCoroutine(_tween); _tween = null; }
            var stage = _session.Stage;
            Renderer.SetPlayer(state.Player);
            Renderer.SetPlayerWalking(false);
            for (int i = 0; i < stage.Guards.Count; i++) { Renderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], state.T), GuardTrack.NextStep(stage.Guards[i], state.T)); Renderer.SetGuardWalking(i, false); }
            for (int i = 0; i < stage.Cameras.Count; i++) Renderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], state.T), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) Renderer.SetDoor(i, state.IsDoorOpen(i));
            Renderer.SetKeyCollected(state.HasKey);
            Renderer.SetClouds(state.Clouds);
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], state.T))) if (seen.Add(c)) guardCells.Add(c);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                var cells = Sight.CameraVision(stage, state, i, state.T);
                Renderer.SetCameraCoverage(i, cells);
                foreach (var c in cells) if (seen.Add(c)) camCells.Add(c);
            }
            Renderer.SetVision(guardCells, camCells);
            var reachNodes = new List<GridCoord>(); var reachEdges = new List<EdgeKey>();
            if (!state.IsTerminal)
                for (int d = 0; d < 4; d++)
                {
                    var n = state.Player.Neighbor((Direction)d);
                    if (!stage.IsConnected(state.Player, n)) continue;
                    int door = stage.DoorIndex(state.Player, n);
                    if (door >= 0 && !state.IsDoorOpen(door) && !state.HasKey) continue;
                    reachNodes.Add(n); reachEdges.Add(new EdgeKey(state.Player, n));
                }
            Renderer.SetReachable(reachNodes, reachEdges);
            Renderer.ClearPreview();
            OnModeChanged(_session.Mode);
        }

        private void OnModeChanged(InputMode mode)
        {
            Renderer.ClearTargets();
            var state = _session.State; var stage = _session.Stage;
            if (state == null) return;
            if (mode == InputMode.SmokeTarget) Renderer.SetTargets(StageRules.SmokeTargets(stage, state));
            for (int i = 0; i < stage.Cameras.Count; i++) Renderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], state.T), state.EmpLeftOn(i), mode == InputMode.EmpTarget && !state.IsCameraDisabled(i));
        }

        private void OnPreviewShown(ActionOutcome o)
        {
            if (!o.Valid || o.Free) { Renderer.ClearPreview(); return; }
            var stage = _session.Stage; var after = o.After;
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, after, o.GuardsAfter[i])) if (seen.Add(c)) guardCells.Add(c);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, after, i, o.Before.T + 1)) if (seen.Add(c)) camCells.Add(c);
            var pal = Renderer.palette;
            Renderer.SetPreview(guardCells, camCells, o.PlayerTo, o.Caught || o.Lockdown ? pal.previewDanger : o.AlarmTriggered ? pal.previewAlarm : pal.previewSafe);
        }

        private void OnPreviewHidden() => Renderer.ClearPreview();

        /// <summary>Cosmetic tween in step with the 2D animator's timing; the final frame is always the snapped state.</summary>
        private void OnActionStarted(ActionOutcome o)
        {
            if (o.Free) return;
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Tween(o));
        }

        private IEnumerator Tween(ActionOutcome o)
        {
            var stage = _session.Stage;
            Renderer.ClearPreview(); Renderer.ClearTargets();
            var from = Renderer.ToWorld(o.PlayerFrom); var to = Renderer.ToWorld(o.PlayerTo);
            bool moved = o.PlayerFrom != o.PlayerTo;
            if (moved)
            {
                var d = DirectionUtil.FromDelta(o.PlayerTo.X - o.PlayerFrom.X, o.PlayerTo.Y - o.PlayerFrom.Y);
                if (d.HasValue) Renderer.SetPlayerFacing(d.Value);
                Renderer.SetPlayerWalking(true);
            }
            float t = 0f;
            while (t < moveDuration) { t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / moveDuration); if (Renderer.PlayerTransform != null) Renderer.PlayerTransform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Renderer.PlayerTransform.position.y - from.y); yield return null; }
            Renderer.SetPlayerWalking(false);
            if (o.Caught && o.CapturePhase == CapturePhase.BeforeEnemyUpdate) yield break;
            int n = stage.Guards.Count;
            var gFrom = new Vector3[n]; var gTo = new Vector3[n];
            for (int i = 0; i < n; i++) { gFrom[i] = Renderer.ToWorld(o.GuardsBefore[i].Position); gTo[i] = Renderer.ToWorld(o.GuardsAfter[i].Position); if (gFrom[i] != gTo[i]) Renderer.SetGuardWalking(i, true); }
            t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / moveDuration);
                for (int i = 0; i < n; i++)
                {
                    var g = Renderer.GuardTransforms[i];
                    var p = Vector3.Lerp(gFrom[i], gTo[i], k); g.position = new Vector3(p.x, g.position.y, p.z);
                    g.rotation = Quaternion.Slerp(SS3DGrid.Facing(o.GuardsBefore[i].Facing), SS3DGrid.Facing(o.GuardsAfter[i].Facing), k) * Quaternion.Euler(0f, Renderer.palette.characterYawOffset, 0f);
                }
                for (int i = 0; i < stage.Cameras.Count; i++)
                    Renderer.CameraHeads[i].rotation = Quaternion.Slerp(SS3DGrid.Facing(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T)), SS3DGrid.Facing(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T + 1)), k);
                yield return null;
            }
            for (int i = 0; i < n; i++) Renderer.SetGuardWalking(i, false);
            _tween = null;
        }

        // ---- input picking on the XZ plane

        private GridCoord? ScreenToNode(Vector2 screen)
        {
            if (!Renderer.IsBuilt || !SS3DGrid.RayToPlane(_composer.BoardCamera, screen, Renderer.FloorTop, out var hit)) return null;
            return Renderer.ToGrid(hit);
        }

        private int ScreenToCamera(Vector2 screen)
        {
            if (!Renderer.IsBuilt || !SS3DGrid.RayToPlane(_composer.BoardCamera, screen, Renderer.FloorTop + 0.5f, out var hit)) return -1;
            int best = -1; float bestDist = 0.6f * Renderer.tileSpacing;
            for (int i = 0; i < _session.Stage.Cameras.Count; i++)
            {
                var p = Renderer.CameraWorldPosition(i);
                float d = Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(p.x, p.z));
                if (d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }
    }
}
