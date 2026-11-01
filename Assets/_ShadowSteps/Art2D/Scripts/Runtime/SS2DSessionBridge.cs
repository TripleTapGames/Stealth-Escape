using System.Collections;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    /// <summary>
    /// Attaches the 2D sprite presentation to a composed Game scene when <see cref="GameSceneComposer.Use2DArtPresentation"/>
    /// is on. Mirrors the authoritative <see cref="GameSessionController"/> events (the same ones the HUD uses), hides the
    /// placeholder board and keeps its camera fitter and tap picking (identical layout and camera), so controls, facing
    /// and all rules are unchanged. Never computes rules: hazard cells come from <see cref="Sight"/>, poses from the tracks.
    /// </summary>
    public sealed class SS2DSessionBridge : MonoBehaviour
    {
        public ShadowSteps2DRenderer Renderer { get; private set; }
        private GameSessionController _session;
        private GameSceneComposer _composer;
        private SS2DCameraFitter _fitter;
        private Coroutine _tween;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() { GameSceneHooks.OnComposed += Attach; }

        public static void Attach(GameSceneComposer composer)
        {
            if (composer == null || !composer.Use2DArtPresentation || composer.Use3DPresentation) return;
            var lib = composer.Art2DLibrary as SS2DSpriteLibrary;
            if (lib == null) lib = Resources.Load<SS2DSpriteLibrary>("SS2DSpriteLibrary");
            if (lib == null) { Debug.LogWarning("[SS2D] Use2DArtPresentation is on but no SS2DSpriteLibrary is assigned; staying on the placeholder board."); return; }
            var root = new GameObject("Art2D");
            var bridge = root.AddComponent<SS2DSessionBridge>();
            bridge.Renderer = root.AddComponent<ShadowSteps2DRenderer>(); bridge.Renderer.library = lib;
            bridge.Bind(composer);
        }

        public void Bind(GameSceneComposer composer)
        {
            _composer = composer; _session = composer.Session;
            // The placeholder board keeps running its sync (rules/HUD paths identical) but is hidden; its camera fitter and picking stay in use.
            if (composer.Board != null) foreach (var r in composer.Board.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            composer.BoardCamera.backgroundColor = Renderer.library.background;
            // Camera and picking follow the drawn (possibly rotated) board: the runtime fitter is retired, taps/swipes are mapped back to logical cells and directions.
            var oldFitter = composer.BoardCamera.GetComponent<CameraFitter>(); if (oldFitter != null) oldFitter.enabled = false;
            _fitter = composer.BoardCamera.GetComponent<SS2DCameraFitter>() ?? composer.BoardCamera.gameObject.AddComponent<SS2DCameraFitter>();
            _fitter.board = Renderer;
            if (composer.Input != null)
            {
                composer.Input.ScreenToNodeOverride = screen => Renderer.IsBuilt ? Renderer.ToGrid(composer.BoardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -composer.BoardCamera.transform.position.z))) : (GridCoord?)null;
                composer.Input.ScreenToCameraOverride = screen => Renderer.IsBuilt ? Renderer.CameraIndexNear(composer.BoardCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -composer.BoardCamera.transform.position.z))) : -1;
                composer.Input.ScreenToLogicalDirection = d => Renderer.IsBuilt ? Renderer.Logical(d) : d;
            }
            _session.StageLoaded += OnStageLoaded; _session.StateChanged += OnStateChanged; _session.ActionStarted += OnActionStarted;
            _session.ModeChanged += OnModeChanged; _session.PreviewShown += OnPreviewShown; _session.PreviewHidden += OnPreviewHidden;
            _session.Caught += OnCaught; _session.LockedDown += OnCaught;
            if (_session.HasStage) { OnStageLoaded(_session.Stage); OnStateChanged(_session.State); }
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.StageLoaded -= OnStageLoaded; _session.StateChanged -= OnStateChanged; _session.ActionStarted -= OnActionStarted;
            _session.ModeChanged -= OnModeChanged; _session.PreviewShown -= OnPreviewShown; _session.PreviewHidden -= OnPreviewHidden;
            _session.Caught -= OnCaught; _session.LockedDown -= OnCaught;
        }

        /// <summary>Short capture animation: the spy shakes on its node while the HUD flashes; the state itself is already final.</summary>
        private void OnCaught(ActionOutcome o) { if (Renderer.Player != null) StartCoroutine(Shake(Renderer.Player.transform, 0.38f, 0.06f)); }

        private IEnumerator Shake(Transform t, float duration, float amplitude)
        {
            var origin = t.localPosition; float e = 0f;
            while (e < duration) { e += Time.deltaTime; float k = 1f - e / duration; t.localPosition = origin + new Vector3(Mathf.Sin(e * 60f) * amplitude * k, 0f, 0f); yield return null; }
            t.localPosition = origin;
        }

        private void OnStageLoaded(Stage stage)
        {
            Renderer.Build(stage);
            if (_fitter != null) _fitter.Fit(_composer.BoardCamera.aspect);
            if (_composer.Board != null) foreach (var r in _composer.Board.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        private void OnStateChanged(StageState state)
        {
            if (_tween != null) { StopCoroutine(_tween); _tween = null; }
            var stage = _session.Stage; var lib = Renderer.library;
            Renderer.SetPlayer(state.Player); Renderer.SetPlayerWalking(false);
            Renderer.SetPlayerTint(state.Result == StageResult.Caught ? lib.captured : state.Result == StageResult.Won ? lib.won : state.Result == StageResult.Lockdown ? lib.lockdown : (Color?)null);
            for (int i = 0; i < stage.Guards.Count; i++) { Renderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], state.T), GuardTrack.NextStep(stage.Guards[i], state.T)); Renderer.SetGuardWalking(i, false); Renderer.SetGuardTint(i, null); }
            for (int i = 0; i < stage.Cameras.Count; i++) Renderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], state.T), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) Renderer.SetDoor(i, state.IsDoorOpen(i));
            Renderer.SetKeyCollected(state.HasKey);
            Renderer.SetClouds(state.Clouds);
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], state.T))) if (seen.Add(c)) guardCells.Add(c);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, state.T)) if (seen.Add(c)) camCells.Add(c);
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
            var state = _session.State; var stage = _session.Stage; if (state == null) return;
            if (mode == InputMode.SmokeTarget) Renderer.SetTargets(StageRules.SmokeTargets(stage, state));
            for (int i = 0; i < stage.Cameras.Count; i++) Renderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], state.T), state.EmpLeftOn(i), mode == InputMode.EmpTarget && !state.IsCameraDisabled(i));
        }

        private void OnPreviewShown(ActionOutcome o)
        {
            if (!o.Valid || o.Free) { Renderer.ClearPreview(); return; }
            var stage = _session.Stage; var after = o.After; var lib = Renderer.library;
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, after, o.GuardsAfter[i])) if (seen.Add(c)) guardCells.Add(c);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, after, i, o.Before.T + 1)) if (seen.Add(c)) camCells.Add(c);
            Renderer.SetPreview(guardCells, camCells, o.PlayerTo, o.Caught || o.Lockdown ? lib.previewDanger : o.AlarmTriggered ? lib.previewAlarm : lib.previewSafe);
        }

        private void OnPreviewHidden() => Renderer.ClearPreview();

        private void OnActionStarted(ActionOutcome o)
        {
            if (o.Free) return;
            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(Tween(o));
        }

        /// <summary>Cosmetic tween in step with the placeholder animator's timing; the final frame is always the snapped state.</summary>
        private IEnumerator Tween(ActionOutcome o)
        {
            var stage = _session.Stage; float dur = Mathf.Max(0.01f, Renderer.library.moveDuration);
            Renderer.ClearPreview(); Renderer.ClearTargets();
            var from = Renderer.Pos(o.PlayerFrom); var to = Renderer.Pos(o.PlayerTo);
            if (o.PlayerFrom != o.PlayerTo)
            {
                var d = DirectionUtil.FromDelta(o.PlayerTo.X - o.PlayerFrom.X, o.PlayerTo.Y - o.PlayerFrom.Y);
                if (d.HasValue) Renderer.SetPlayerFacing(d.Value);
                Renderer.SetPlayerWalking(true);
                float t = 0f;
                while (t < dur) { t += Time.deltaTime; Renderer.Player.transform.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur)); Renderer.Player.SetSortingOrderFromY(); yield return null; }
                Renderer.SetPlayerWalking(false);
            }
            if (o.Caught && o.CapturePhase == CapturePhase.BeforeEnemyUpdate) yield break;
            int n = stage.Guards.Count; var gFrom = new Vector3[n]; var gTo = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                gFrom[i] = Renderer.Pos(o.GuardsBefore[i].Position); gTo[i] = Renderer.Pos(o.GuardsAfter[i].Position);
                if (gFrom[i] != gTo[i]) { Renderer.SetGuardWalking(i, true); Renderer.Guards[i].SetFacing(Renderer.View(o.GuardsAfter[i].Facing)); }
            }
            float k = 0f;
            while (k < dur)
            {
                k += Time.deltaTime; float e = Mathf.SmoothStep(0f, 1f, k / dur);
                for (int i = 0; i < n; i++) { Renderer.Guards[i].transform.localPosition = Vector3.Lerp(gFrom[i], gTo[i], e); Renderer.Guards[i].SetSortingOrderFromY(); }
                for (int i = 0; i < stage.Cameras.Count; i++)
                    Renderer.CameraHeads[i].localRotation = Quaternion.Slerp(Renderer.RotationFor(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T)), Renderer.RotationFor(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T + 1)), e);
                yield return null;
            }
            for (int i = 0; i < n; i++) Renderer.SetGuardWalking(i, false);
            if (o.Caught && o.CapturingGuard >= 0) Renderer.SetGuardTint(o.CapturingGuard, Renderer.library.captured);
            _tween = null;
        }
    }
}
