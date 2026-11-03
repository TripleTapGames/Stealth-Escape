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
            _session.StageLoaded += OnStageLoaded; _session.StateChanged += OnStateChanged; _session.ActionStarted += OnActionStarted; _session.ActionCompleted += OnActionCompleted; _session.Retried += OnRetried;
            _session.ModeChanged += OnModeChanged; _session.PreviewShown += OnPreviewShown; _session.PreviewHidden += OnPreviewHidden;
            _session.Caught += OnCaught; _session.LockedDown += OnCaught;
            _session.AlarmTriggered += OnAlarm;
            composer.WinTransition = Renderer.library.playExitDoorSequence ? OnWinTransition : (System.Func<ActionOutcome, System.Action, bool>)null; // off: the results screen follows the winning action directly
            composer.CaptureTransition = OnCaptureTransition;
            if (composer.Hud is SS2DHudCanvas hudCanvas) hudCanvas.deferCaptureFeedback = true;
            if (_session.HasStage) { OnStageLoaded(_session.Stage); OnStateChanged(_session.State); }
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.StageLoaded -= OnStageLoaded; _session.StateChanged -= OnStateChanged; _session.ActionStarted -= OnActionStarted; _session.ActionCompleted -= OnActionCompleted; _session.Retried -= OnRetried;
            _session.ModeChanged -= OnModeChanged; _session.PreviewShown -= OnPreviewShown; _session.PreviewHidden -= OnPreviewHidden;
            _session.Caught -= OnCaught; _session.LockedDown -= OnCaught; _session.AlarmTriggered -= OnAlarm;
        }

        private void OnAlarm(ActionOutcome o) { Renderer.FlashCameraVision(); }

        /// <summary>
        /// Level completion (called by the flow after the win is recorded): a short success beat, then the spy walks from
        /// the exit node to the door, the door opens, the spy steps in and fades, the door closes, and either the next
        /// stage opens automatically or the continuation (results screen) runs. Visual only; the state is already Won.
        /// </summary>
        private bool OnWinTransition(ActionOutcome o, System.Action continuation)
        {
            if (Renderer == null || !Renderer.IsBuilt || Renderer.Player == null || !Renderer.HasExitDoor || !Renderer.library.playExitDoorSequence) return false;
            StartCoroutine(DoorSequence(o, continuation));
            return true;
        }

        public bool DoorSequenceRunning { get; private set; }

        private IEnumerator DoorSequence(ActionOutcome o, System.Action continuation)
        {
            DoorSequenceRunning = true;
            var lib = Renderer.library; var stage = _session.Stage; var p = Renderer.Player;
            Renderer.ClearPreview(); Renderer.ClearTargets();
            yield return new WaitForSeconds(lib.successBeat);
            var outVec = new Vector3(DirectionUtil.DeltaX(Renderer.DoorOutward), -DirectionUtil.DeltaY(Renderer.DoorOutward), 0f);
            var from = Renderer.Pos(stage.Exit); var threshold = Renderer.DoorPosition - outVec * (lib.doorSize * 0.30f);
            p.SetFacing(Renderer.DoorOutward); p.SetWalking(true);
            float t = 0f, d = Mathf.Max(0.05f, lib.doorWalkDuration);
            while (t < d) { t += Time.deltaTime; p.transform.localPosition = Vector3.Lerp(from, threshold, Mathf.SmoothStep(0f, 1f, t / d)); p.SetSortingOrderFromY(); yield return null; }
            p.SetWalking(false);
            Renderer.SetExitDoorOpen(true);
            yield return new WaitForSeconds(lib.doorOpenDelay);
            var inside = Renderer.DoorPosition + outVec * (lib.doorSize * 0.05f); var baseScale = p.body.transform.localScale; float shadowAlpha = p.shadow != null ? p.shadow.color.a : 0f;
            t = 0f; d = Mathf.Max(0.05f, lib.doorEnterDuration);
            while (t < d)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / d);
                p.transform.localPosition = Vector3.Lerp(threshold, inside, k);
                p.body.transform.localScale = baseScale * Mathf.Lerp(1f, 0.55f, k);
                var c = p.body.color; c.a = 1f - k; p.body.color = c; if (p.ring != null) { var rc = p.ring.color; rc.a = 0.9f * (1f - k); p.ring.color = rc; } if (p.shadow != null) { var sc = p.shadow.color; sc.a = shadowAlpha * (1f - k); p.shadow.color = sc; }
                yield return null;
            }
            Renderer.SetExitDoorOpen(false);
            yield return new WaitForSeconds(lib.doorCloseDelay);
            DoorSequenceRunning = false;
            var next = AppServices.Stages != null ? AppServices.Stages.NextAfter(stage.Id) : null;
            if (lib.autoAdvanceAfterDoor && next != null) _composer.OpenStage(next); // the bridge rebuilds on StageLoaded; the HUD follows the session
            else continuation?.Invoke();
        }

        /// <summary>Session capture/lockdown event: nothing happens here any more; the choreography runs from <see cref="OnCaptureTransition"/> once the enemy tween has finished.</summary>
        private void OnCaught(ActionOutcome o) { }

        private ActionOutcome _pendingCapture; private System.Action _pendingCapturePresent;
        public bool CaptureSequenceRunning { get; private set; }

        /// <summary>
        /// Capture or lockdown (called by the flow before the failure screen): the guard first completes its step or turn
        /// (the running tween), then the capturing guard is tinted with its fan on the new facing, a short beat passes, the
        /// spy shakes and the HUD flashes, and only then does the continuation (failure screen) run. State is already final.
        /// </summary>
        private bool OnCaptureTransition(ActionOutcome o, System.Action continuation)
        {
            if (Renderer == null || !Renderer.IsBuilt || Renderer.Player == null) return false;
            _pendingCapture = o; _pendingCapturePresent = continuation; CaptureSequenceRunning = true;
            if (_tween == null) StartCoroutine(CaptureSequence());
            return true;
        }

        private IEnumerator CaptureSequence()
        {
            var o = _pendingCapture; var present = _pendingCapturePresent; _pendingCapture = null; _pendingCapturePresent = null;
            if (o == null) { CaptureSequenceRunning = false; yield break; }
            var lib = Renderer.library;
            if (o.Caught && o.CapturingGuard >= 0) Renderer.SetGuardTint(o.CapturingGuard, lib.captured);
            yield return new WaitForSeconds(Mathf.Max(0f, lib.captureBeat));
            if (_composer.Hud is SS2DHudCanvas hud) hud.PresentCapture(o);
            if (Renderer.Player != null) yield return Shake(Renderer.Player.transform, 0.38f, 0.06f);
            CaptureSequenceRunning = false;
            present?.Invoke();
        }

        private IEnumerator Shake(Transform t, float duration, float amplitude)
        {
            var origin = t.localPosition; float e = 0f;
            while (e < duration) { e += Time.deltaTime; float k = 1f - e / duration; t.localPosition = origin + new Vector3(Mathf.Sin(e * 60f) * amplitude * k, 0f, 0f); yield return null; }
            t.localPosition = origin;
        }

        private void OnStageLoaded(Stage stage)
        {
            if (_tween != null) { StopCoroutine(_tween); _tween = null; } _pendingState = null; _lastOutcome = null; _pendingCapture = null; _pendingCapturePresent = null; CaptureSequenceRunning = false;
            Renderer.Build(stage);
            if (_fitter != null) _fitter.Fit(_composer.BoardCamera.aspect);
            Renderer.FitBackground(_composer.BoardCamera);
            if (_composer.Board != null) foreach (var r in _composer.Board.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }

        private ActionOutcome _lastOutcome; private StageState _pendingState;
        private void OnActionCompleted(ActionOutcome o) { _lastOutcome = o; }
        private void OnRetried() { _lastOutcome = null; }

        /// <summary>
        /// Schedule index to present a state at. The rules keep the clock at the old index on a capture ("the failed action is
        /// shown at the old schedule index"), but when the guard caught the player by moving or turning (capture after the
        /// enemy update or after gadget expiry) the guards and cameras must be shown where they ended up — otherwise the guard
        /// would snap back to its previous facing and its fan would point the old way.
        /// </summary>
        private int ShownClock(StageState state)
        {
            if (state.Result == StageResult.Caught && _lastOutcome != null && _lastOutcome.Caught && _lastOutcome.After != null && _lastOutcome.After.T == state.T
                && _lastOutcome.CapturePhase != CapturePhase.BeforeEnemyUpdate) return state.T + 1;
            return state.T;
        }

        /// <summary>A state change while the cosmetic tween is still running is applied when the tween ends (the tween's last frame is the snapped state).</summary>
        private void OnStateChanged(StageState state)
        {
            if (_tween != null) { _pendingState = state; return; }
            ApplyState(state);
        }

        private void ApplyState(StageState state)
        {
            _pendingState = null;
            var stage = _session.Stage; var lib = Renderer.library; int t = ShownClock(state);
            Renderer.SetPlayer(state.Player); Renderer.SetPlayerWalking(false);
            Renderer.SetPlayerTint(state.Result == StageResult.Caught ? lib.captured : state.Result == StageResult.Won ? lib.won : state.Result == StageResult.Lockdown ? lib.lockdown : (Color?)null);
            for (int i = 0; i < stage.Guards.Count; i++) { Renderer.SetGuard(i, GuardTrack.PoseAt(stage.Guards[i], t), GuardTrack.NextStep(stage.Guards[i], t)); Renderer.SetGuardWalking(i, false); Renderer.SetGuardTint(i, state.Result == StageResult.Caught && _lastOutcome != null && _lastOutcome.CapturingGuard == i ? lib.captured : (Color?)null); }
            for (int i = 0; i < stage.Cameras.Count; i++) Renderer.SetCamera(i, CameraTrack.FacingAt(stage.Cameras[i], t), state.EmpLeftOn(i), false);
            for (int i = 0; i < stage.Doors.Count; i++) Renderer.SetDoor(i, state.IsDoorOpen(i));
            Renderer.SetKeyCollected(state.HasKey);
            Renderer.SetClouds(state.Clouds);
            var guardCells = new List<GridCoord>(); var seen = new HashSet<GridCoord>();
            for (int i = 0; i < stage.Guards.Count; i++) foreach (var c in Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[i], t))) if (seen.Add(c)) guardCells.Add(c);
            var camCells = new List<GridCoord>(); seen.Clear();
            for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, t)) if (seen.Add(c)) camCells.Add(c);
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
            if (_tween != null) { StopCoroutine(_tween); _tween = null; if (_pendingState != null) ApplyState(_pendingState); }
            _tween = StartCoroutine(Tween(o));
        }

        /// <summary>Tween end: snap to the state the session published meanwhile (if any), then run a pending capture choreography.</summary>
        private void FinishTween()
        {
            _tween = null;
            if (_pendingState != null) ApplyState(_pendingState);
            if (_pendingCapture != null) StartCoroutine(CaptureSequence());
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
            if (o.Caught && o.CapturePhase == CapturePhase.BeforeEnemyUpdate) { FinishTween(); yield break; }
            int n = stage.Guards.Count; var gFrom = new Vector3[n]; var gTo = new Vector3[n]; var turned = new bool[n];
            for (int i = 0; i < n; i++)
            {
                gFrom[i] = Renderer.Pos(o.GuardsBefore[i].Position); gTo[i] = Renderer.Pos(o.GuardsAfter[i].Position);
                // Walking guards face their step from the start; guards turning in place switch facing halfway through the phase so the turn is visible.
                if (gFrom[i] != gTo[i]) { Renderer.SetGuardWalking(i, true); Renderer.Guards[i].SetFacing(Renderer.View(o.GuardsAfter[i].Facing)); turned[i] = true; }
            }
            float k = 0f;
            while (k < dur)
            {
                k += Time.deltaTime; float e = Mathf.SmoothStep(0f, 1f, k / dur);
                for (int i = 0; i < n; i++)
                {
                    Renderer.Guards[i].transform.localPosition = Vector3.Lerp(gFrom[i], gTo[i], e); Renderer.Guards[i].SetSortingOrderFromY();
                    if (!turned[i] && e >= 0.5f) { Renderer.Guards[i].SetFacing(Renderer.View(o.GuardsAfter[i].Facing)); turned[i] = true; }
                }
                for (int i = 0; i < stage.Cameras.Count; i++)
                    Renderer.CameraHeads[i].localRotation = Quaternion.Slerp(Renderer.RotationFor(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T)), Renderer.RotationFor(CameraTrack.FacingAt(stage.Cameras[i], o.Before.T + 1)), e);
                yield return null;
            }
            for (int i = 0; i < n; i++) { Renderer.SetGuardWalking(i, false); if (!turned[i]) Renderer.Guards[i].SetFacing(Renderer.View(o.GuardsAfter[i].Facing)); }
            FinishTween(); // the guard has finished its step/turn: snap to the published state (guards/fans at their new facing), then the capture
        }
    }
}
