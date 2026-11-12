using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-080: wide boards (levels 33-45, 11-15 columns) in the real Game scene are drawn landscape, zoomed to their height and panned
    /// sideways: the view opens on the player, a drag pans it without touching the session (no turn, no state change), and the next
    /// counted action brings the view back onto the player. A board that fits (LEVEL-30, 9 columns) has no pan range at all.
    /// </summary>
    public class SS2DWideBoardPanTests
    {
        private GameSceneComposer _composer;
        private GameSessionController S => _composer.Session;
        private SS2DSessionBridge Bridge => Object.FindObjectOfType<SS2DSessionBridge>();
        private SS2DCameraFitter Fitter => _composer.BoardCamera.GetComponent<SS2DCameraFitter>();

        private IEnumerator OpenGame(string code)
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData { DebugUnlockAll = true }; GuardOnboarding.MarkAllSeen(save); AppServices.InstallSaveForTests(save);
            AppServices.PendingStageId = AppServices.Stages.ByCode(code).Id;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/_ShadowSteps/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("scene loaded by path in the editor only");
#endif
            yield return null; yield return null; yield return null;
            _composer = Object.FindObjectOfType<GameSceneComposer>(); Assert.IsNotNull(_composer);
            float t0 = Time.realtimeSinceStartup; while (!_composer.Ready && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsTrue(_composer.Ready); yield return null;
            Assert.AreEqual(code, S.Stage.Code);
            var intro = Object.FindObjectOfType<ShadowSteps.UI.OnboardingScreen>(); if (intro != null && intro.IsVisible) { intro.OnDismiss(); yield return null; yield return new WaitForSecondsRealtime(0.4f); }
            _composer.BoardCamera.aspect = 1080f / 1920f; // the batch Game view is not portrait: fit and measure at the phone aspect (the fitter only refits when the screen size changes)
            Fitter.Fit(1080f / 1920f); Fitter.SnapToPlayer(); yield return null;
        }

        /// <summary>Waits for the action's presentation, then for any modal that locks input (the alarm popup after a camera detection: the player taps OK, here it auto-dismisses) so the next action is accepted as it would be in play.</summary>
        private IEnumerator Settle()
        {
            // The alarm popup is pushed by the session's Run coroutine the frame after its animation ends, so wait for three consecutive idle frames (not busy, no modal).
            float t0 = Time.realtimeSinceStartup; yield return null;
            for (int idle = 0; idle < 3 && Time.realtimeSinceStartup - t0 < 14f;) { if (S.IsBusy || S.InputLocked) idle = 0; else idle++; yield return null; }
        }

        [UnityTest]
        public IEnumerator Level45_OpensOnThePlayer_DragPansWithoutATurn_AndTheNextMoveFollows()
        {
            yield return OpenGame("LEVEL-45");
            var bridge = Bridge; var fitter = Fitter; var cam = _composer.BoardCamera;
            Assert.AreEqual(15, S.Stage.Width); Assert.IsTrue(bridge.Renderer.PanHorizontal, "15 columns pan"); Assert.IsFalse(bridge.Renderer.Rotated, "drawn landscape");
            Assert.Greater(fitter.PanRange, 0f, "the board is wider than the view");
            // The view opened on the player: the player is centred horizontally (within the clamp) and the far exit column is off-screen.
            float playerX = bridge.Renderer.Player.transform.position.x;
            Assert.AreEqual(fitter.PlayerPanTarget(), fitter.PanX, 1e-3f, "snapped to the player");
            var pv = cam.WorldToViewportPoint(bridge.Renderer.Player.transform.position); Assert.IsTrue(pv.x > 0.05f && pv.x < 0.95f, "player in view " + pv);
            // The board is wider than the view: the leftmost and rightmost columns cannot both be inside it at once.
            float viewW = cam.orthographicSize * 2f * cam.aspect; var leftW = bridge.Renderer.transform.TransformPoint(bridge.Renderer.Pos(new GridCoord(0, 5))); var rightW = bridge.Renderer.transform.TransformPoint(bridge.Renderer.Pos(new GridCoord(14, 5)));
            Assert.Greater(rightW.x - leftW.x + bridge.Renderer.Layout.NodeSpacing, viewW, "the 15 columns overflow the view");
            // A drag pans the view and changes nothing in the session.
            var before = S.State; int t0 = S.State.T; var pos0 = S.State.Player; float camX0 = cam.transform.position.x;
            fitter.PanByScreenPixels(-300f); yield return null;
            Assert.Greater(cam.transform.position.x, camX0, "dragging the fingers left reveals the right of the board");
            Assert.IsFalse(fitter.FollowPlayer, "a manual drag suspends the follow");
            Assert.AreSame(before, S.State, "no state change"); Assert.AreEqual(t0, S.State.T, "no turn consumed"); Assert.AreEqual(pos0, S.State.Player);
            Assert.IsFalse(S.IsBusy, "no animation started");
            // Dragging keeps the view inside the board.
            fitter.PanByScreenPixels(-100000f); yield return null; Assert.AreEqual(fitter.PanRange, fitter.PanX, 1e-4f, "clamped at the right end");
            fitter.PanByScreenPixels(100000f); yield return null; Assert.AreEqual(-fitter.PanRange, fitter.PanX, 1e-4f, "clamped at the left end");
            Assert.AreSame(before, S.State, "still no state change");
            // The first counted move resumes the follow: the view glides back onto the player.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East)), "E accepted on LEVEL-45"); yield return Settle();
            Assert.IsTrue(fitter.FollowPlayer, "a counted action resumes the follow");
            float t1 = Time.realtimeSinceStartup; while (Mathf.Abs(fitter.PanX - fitter.PlayerPanTarget()) > 1e-3f && Time.realtimeSinceStartup - t1 < 5f) yield return null;
            Assert.AreEqual(fitter.PlayerPanTarget(), fitter.PanX, 1e-3f, "the view settled on the player");
            Assert.AreEqual(1, S.State.T, "exactly one turn: the pans counted nothing");
            pv = cam.WorldToViewportPoint(bridge.Renderer.Player.transform.position); Assert.IsTrue(pv.x > 0.05f && pv.x < 0.95f, "player in view after the move " + pv);
            _ = playerX;
        }

        [UnityTest]
        public IEnumerator PanGesture_ThroughTheInputController_NeverReachesTheSession()
        {
            yield return OpenGame("LEVEL-40");
            var fitter = Fitter; Assert.Greater(fitter.PanRange, 0f);
            var before = S.State; float camX0 = _composer.BoardCamera.transform.position.x; int requests = 0;
            _composer.Input.PanRequested += dx => requests++;
            // The bridge's handler is what the gesture drives; invoke it the way the controller does (pixels per frame, fingers moving left).
            var handler = typeof(SS2DSessionBridge).GetMethod("OnPanRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(handler, "bridge pan handler");
            handler.Invoke(Bridge, new object[] { -120f }); yield return null;
            Assert.Greater(_composer.BoardCamera.transform.position.x, camX0, "the view moved");
            Assert.AreSame(before, S.State, "the session saw nothing"); Assert.AreEqual(0, S.State.T);
            Assert.AreEqual(0, requests, "the controller raised no gesture of its own in a headless frame");
        }

        [UnityTest]
        public IEnumerator Level30_NineColumns_DoesNotPan_AndKeepsItsFraming()
        {
            yield return OpenGame("LEVEL-30");
            var fitter = Fitter; var bridge = Bridge;
            Assert.IsFalse(bridge.Renderer.PanHorizontal); Assert.AreEqual(0f, fitter.PanRange, 1e-6f, "9x11 fits as before");
            float camX0 = _composer.BoardCamera.transform.position.x;
            fitter.PanByScreenPixels(-300f); yield return null;
            Assert.AreEqual(camX0, _composer.BoardCamera.transform.position.x, 1e-5f, "a drag on a board that fits does nothing");
            foreach (var n in S.Stage.FloorNodes())
            {
                var v = _composer.BoardCamera.WorldToViewportPoint(bridge.Renderer.transform.TransformPoint(bridge.Renderer.Pos(n)));
                Assert.IsTrue(v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f, "LEVEL-30 node " + n + " inside the view: " + v);
            }
        }
    }
}
