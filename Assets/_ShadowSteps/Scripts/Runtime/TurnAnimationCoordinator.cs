using System.Collections;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Tweens the board between the two logical states of a <see cref="TurnResult"/>. Cosmetic only: every move
    /// starts and ends exactly on node centres (one nodeSpacing apart) and the final frame is a <see cref="BoardView.SyncToState"/>.
    /// Honour reduced motion by setting <see cref="ReducedMotion"/>; then everything snaps.
    /// </summary>
    public sealed class TurnAnimationCoordinator : MonoBehaviour
    {
        public bool ReducedMotion;
        public bool IsPlaying { get; private set; }

        public IEnumerator PlayTurn(BoardView board, TurnResult result)
        {
            IsPlaying = true;
            var theme = board.ActiveTheme;
            var layout = board.Layout;
            board.HidePreview();

            if (!ReducedMotion)
            {
                if (result.EmpDeployed)
                {
                    foreach (var g in board.Guards) g.SetFrozen(true);
                    yield return Pulse(board.Player.Body.transform, 1.6f, theme.GadgetPulseDuration);
                }
                if (result.SmokeDeployed)
                {
                    board.Player.SetConcealed(true);
                    yield return Pulse(board.Player.transform, 1.25f, theme.GadgetPulseDuration);
                }

                // Player and guards move simultaneously (all resolved from the same snapshot).
                float t = 0f;
                float dur = Mathf.Max(0.01f, theme.MoveDuration);
                var playerFrom = layout.WorldPosition(result.PlayerFrom);
                var playerTo = layout.WorldPosition(result.PlayerTo);
                int n = board.Guards.Count;
                var gFrom = new Vector3[n]; var gTo = new Vector3[n];
                var fFrom = new Quaternion[n]; var fTo = new Quaternion[n];
                for (int i = 0; i < n; i++)
                {
                    gFrom[i] = layout.WorldPosition(result.GuardsBefore[i].Position);
                    gTo[i] = layout.WorldPosition(result.GuardsAfter[i].Position);
                    fFrom[i] = BoardLayout.RotationFor(result.GuardsBefore[i].Facing);
                    fTo[i] = BoardLayout.RotationFor(result.GuardsAfter[i].Facing);
                }
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                    board.Player.transform.position = Vector3.Lerp(playerFrom, playerTo, k);
                    for (int i = 0; i < n; i++)
                    {
                        board.Guards[i].transform.position = Vector3.Lerp(gFrom[i], gTo[i], k);
                        board.Guards[i].ArrowTransform.localRotation = Quaternion.Slerp(fFrom[i], fTo[i], k);
                    }
                    yield return null;
                }
                if (result.KeyCollected && board.KeyProp != null) board.KeyProp.SetCollected(true);
                if (result.Captured)
                {
                    board.Player.SetTint(theme.Captured);
                    if (result.CapturingGuard >= 0) board.Guards[result.CapturingGuard].SetTint(theme.Captured);
                    yield return Pulse(board.Player.transform, 1.3f, theme.CaptureFlashDuration);
                }
                else if (result.Won)
                {
                    board.Player.SetTint(theme.Won);
                    yield return Pulse(board.ExitProp.transform, 1.4f, theme.WinPulseDuration);
                }
            }

            board.SyncToState(result.StateAfter);
            if (result.Captured && result.CapturingGuard >= 0) board.Guards[result.CapturingGuard].SetTint(theme.Captured);
            IsPlaying = false;
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
