using System.Collections;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Tweens the board between the two logical states of an <see cref="ActionOutcome"/>. Cosmetic only: every move
    /// starts and ends exactly on node centres and the final frame is a <see cref="BoardView.SyncToState"/>.
    /// The sequence communicates *when* a capture happened: before the enemy update (only the player moved), after it
    /// (guards and cameras updated first) or after a gadget expired. Honour reduced motion with <see cref="ReducedMotion"/>.
    /// </summary>
    public sealed class TurnAnimationCoordinator : MonoBehaviour
    {
        public bool ReducedMotion;
        public bool IsPlaying { get; private set; }

        public IEnumerator PlayOutcome(BoardView board, ActionOutcome o)
        {
            IsPlaying = true;
            var theme = board.ActiveTheme;
            var layout = board.Layout;
            board.HidePreview();
            board.ClearTargets();

            if (!ReducedMotion)
            {
                if (o.Free)
                {
                    if (o.Action.Kind == StageActionKind.Smoke) { board.ShowClouds(o.After); yield return Pulse(board.Player.transform, 1.25f, theme.GadgetPulseDuration); }
                    else if (o.Action.Kind == StageActionKind.Emp && o.Action.CameraIndex < board.Cameras.Count) yield return Pulse(board.Cameras[o.Action.CameraIndex].transform, 1.5f, theme.GadgetPulseDuration);
                }
                else
                {
                    float dur = Mathf.Max(0.01f, theme.MoveDuration);
                    bool caughtBefore = o.Caught && o.CapturePhase == CapturePhase.BeforeEnemyUpdate;

                    // Phase 1: the player moves. If the old-state check already caught them, nothing else moves.
                    var from = layout.WorldPosition(o.PlayerFrom); var to = layout.WorldPosition(o.PlayerTo);
                    yield return Tween(dur, k => board.Player.transform.position = Vector3.Lerp(from, to, k));
                    if (o.KeyCollected && board.KeyProp != null) board.KeyProp.SetCollected(true);
                    if (caughtBefore)
                    {
                        yield return Flash(board, o, theme);
                        Finish(board, o, theme);
                        yield break;
                    }

                    // Phase 2: guards step/turn and cameras turn, simultaneously.
                    int n = board.Guards.Count;
                    var gFrom = new Vector3[n]; var gTo = new Vector3[n]; var fFrom = new Quaternion[n]; var fTo = new Quaternion[n];
                    for (int i = 0; i < n; i++)
                    {
                        gFrom[i] = layout.WorldPosition(o.GuardsBefore[i].Position); gTo[i] = layout.WorldPosition(o.GuardsAfter[i].Position);
                        fFrom[i] = BoardLayout.RotationFor(o.GuardsBefore[i].Facing); fTo[i] = BoardLayout.RotationFor(o.GuardsAfter[i].Facing);
                    }
                    int m = board.Cameras.Count;
                    var cFrom = new Quaternion[m]; var cTo = new Quaternion[m];
                    for (int i = 0; i < m; i++)
                    {
                        cFrom[i] = BoardLayout.RotationFor(CameraTrack.FacingAt(board.Stage.Cameras[i], o.Before.T));
                        cTo[i] = BoardLayout.RotationFor(CameraTrack.FacingAt(board.Stage.Cameras[i], o.Before.T + 1));
                    }
                    yield return Tween(dur, k =>
                    {
                        for (int i = 0; i < n; i++) { board.Guards[i].transform.position = Vector3.Lerp(gFrom[i], gTo[i], k); board.Guards[i].ArrowTransform.localRotation = Quaternion.Slerp(fFrom[i], fTo[i], k); }
                        for (int i = 0; i < m; i++) board.Cameras[i].LensTransform.localRotation = Quaternion.Slerp(cFrom[i], cTo[i], k);
                    });

                    if (o.Caught && o.CapturePhase == CapturePhase.AfterEnemyUpdate) yield return Flash(board, o, theme);
                    else if (o.Caught && o.CapturePhase == CapturePhase.AfterGadgetExpiry)
                    {
                        board.ShowClouds(o.After); // the expired cloud disappears first, then the exposure flash
                        yield return Flash(board, o, theme);
                    }
                    else if (o.Won)
                    {
                        board.Player.SetTint(theme.Won);
                        yield return Pulse(board.ExitProp.transform, 1.4f, theme.WinPulseDuration);
                    }
                    else if (o.Lockdown)
                    {
                        board.Player.SetTint(theme.Lockdown);
                        yield return Pulse(board.Player.transform, 1.3f, theme.CaptureFlashDuration);
                    }
                }
            }
            Finish(board, o, theme);
        }

        private void Finish(BoardView board, ActionOutcome o, BoardTheme theme)
        {
            board.SyncToState(o.After);
            if (o.Caught && o.CapturingGuard >= 0 && o.CapturingGuard < board.Guards.Count) board.Guards[o.CapturingGuard].SetTint(theme.Captured);
            IsPlaying = false;
        }

        private static IEnumerator Flash(BoardView board, ActionOutcome o, BoardTheme theme)
        {
            board.Player.SetTint(theme.Captured);
            if (o.CapturingGuard >= 0 && o.CapturingGuard < board.Guards.Count) board.Guards[o.CapturingGuard].SetTint(theme.Captured);
            yield return Pulse(board.Player.transform, 1.3f, theme.CaptureFlashDuration);
        }

        private static IEnumerator Tween(float duration, System.Action<float> apply)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                apply(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            apply(1f);
        }

        private static IEnumerator Pulse(Transform target, float scale, float duration)
        {
            var baseScale = target.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                target.localScale = baseScale * Mathf.Lerp(1f, scale, k);
                yield return null;
            }
            target.localScale = baseScale;
        }
    }
}
