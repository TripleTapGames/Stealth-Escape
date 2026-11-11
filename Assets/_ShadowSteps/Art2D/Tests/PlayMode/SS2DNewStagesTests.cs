using System.Collections;
using System.Collections.Generic;
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
    /// D-078: LEVEL-31 "Garden Gambit" and LEVEL-32 "The Locked Flank" (kit "LEVEL-36/37", 2026-11-11) in the real Game scene:
    /// both 9x9 boards draw with the 2D layer, fit the portrait view, and the solver's par route replays to a win through the
    /// session (knockout from behind included). Captures the Game view for the handoff report.
    /// </summary>
    public class SS2DNewStagesTests
    {
        private GameSceneComposer _composer;
        private GameSessionController S => _composer.Session;

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
            var intro = Object.FindObjectOfType<ShadowSteps.UI.OnboardingScreen>(); if (intro != null && intro.IsVisible) { intro.OnDismiss(); yield return null; yield return new WaitForSecondsRealtime(0.4f); } // let the card fade before any capture
        }

        private IEnumerator CaptureGameView(string fileName)
        {
            var cam = _composer.BoardCamera; const int w = 1080, h = 1920;
            var overlays = new List<(Canvas c, RenderMode mode, Camera wc, float plane, int layer, int order)>();
            foreach (var c in Object.FindObjectsOfType<Canvas>())
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add((c, c.renderMode, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder));
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = SS2DLayers.FX; c.sortingOrder = 100 + c.sortingOrder;
                }
            int prevMask = cam.cullingMask; cam.cullingMask = -1;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); float aspect = cam.aspect; var prevRt = cam.targetTexture;
            cam.targetTexture = rt; cam.aspect = (float)w / h;
            var fitter = cam.GetComponent<SS2DCameraFitter>(); if (fitter != null) fitter.Fit((float)w / h);
            Canvas.ForceUpdateCanvases(); yield return null; yield return null;
            Canvas.ForceUpdateCanvases(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = prevRt; cam.aspect = aspect; cam.cullingMask = prevMask; rt.Release(); Object.Destroy(rt);
            foreach (var o in overlays) { o.c.renderMode = o.mode; o.c.worldCamera = o.wc; o.c.planeDistance = o.plane; o.c.sortingLayerID = o.layer; o.c.sortingOrder = o.order; }
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Docs/Reports/Renders"); System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, fileName), tex.EncodeToPNG()); Object.Destroy(tex);
        }

        private IEnumerator Settle() { float t0 = Time.realtimeSinceStartup; yield return null; while (S.IsBusy && Time.realtimeSinceStartup - t0 < 8f) yield return null; yield return null; }
        private bool CanCapture => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        private IEnumerator OpenDrawCaptureAndReplayPar(string code, string capture, int expectedPar, int expectedKnockouts)
        {
            yield return OpenGame(code);
            var bridge = Object.FindObjectOfType<SS2DSessionBridge>(); Assert.IsNotNull(bridge); Assert.IsTrue(bridge.Renderer.IsBuilt, code + " drawn by the 2D layer");
            Assert.AreEqual(9, S.Stage.Width); Assert.AreEqual(9, S.Stage.Height);
            Assert.AreEqual(S.Stage.Guards.Count, bridge.Renderer.Guards.Count);
            // Every node of the board is inside the portrait view (no panning, nothing cropped).
            var cam = _composer.BoardCamera; cam.GetComponent<SS2DCameraFitter>()?.Fit(1080f / 1920f);
            foreach (var n in S.Stage.FloorNodes())
            {
                var v = cam.WorldToViewportPoint(bridge.Renderer.transform.TransformPoint(bridge.Renderer.Pos(n)));
                Assert.IsTrue(v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f, code + " node " + n + " inside the view: " + v);
            }
            if (CanCapture) yield return CaptureGameView(capture);
            // The solver's par route, through the session (the knockout is the move onto the guard, from behind or the side).
            var m = StageMetrics.Analyze(S.Stage, StagePackTools.ShippedSolveOptions());
            Assert.AreEqual(expectedPar, m.Par, code + " par"); Assert.AreEqual(expectedKnockouts, m.KnockoutsInPar, code + " knockouts in par");
            foreach (var a in m.Solution) { Assert.IsTrue(S.TrySubmit(a), code + " action " + a + " accepted"); yield return Settle(); if (S.State.IsTerminal) break; }
            Assert.AreEqual(StageResult.Won, S.State.Result, code + " par route wins in the real scene");
            Assert.AreEqual(expectedPar, S.State.T);
        }

        [UnityTest]
        public IEnumerator Level31_GardenGambit_DrawsFitsAndParRouteWins() => OpenDrawCaptureAndReplayPar("LEVEL-31", "Art2D_Level31_GardenGambit_1080x1920.png", 8, 1);

        [UnityTest]
        public IEnumerator Level32_LockedFlank_DrawsFitsAndParRouteWins_FrontKnockoutRejected()
        {
            yield return OpenDrawCaptureAndReplayPar("LEVEL-32", "Art2D_Level32_LockedFlank_1080x1920.png", 8, 1);
            // The front approach to g1 is illegal: a player standing at (4,4) (inside its sight, so never alive there in play) may not knock it out or step into it.
            var stage = S.Stage; var init = StageState.Initial(stage, StageMode.Knockout);
            var atFront = new StageState(new GridCoord(4, 4), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 0UL);
            var ko = StageRules.TryApply(stage, atFront, StageAction.Knockout(0));
            Assert.IsFalse(ko.Valid, "frontal knockout rejected"); Assert.AreEqual(RejectReason.FrontalApproach, ko.Reason);
            var walk = StageRules.TryApply(stage, atFront, StageAction.Move(Direction.East));
            Assert.IsFalse(walk.Valid, "walking into the guard's face is the same rejected action"); Assert.AreEqual(RejectReason.FrontalApproach, walk.Reason);
            // From behind, (8,4), the knockout is the legal move onto the guard.
            var behind = new StageState(new GridCoord(8, 4), 0, false, 0, null, null, 0, 0, -1, false, StageResult.Playing, null, 0, StageMode.Knockout, 0UL);
            var fromBehind = StageRules.TryApply(stage, behind, StageAction.Move(Direction.West));
            Assert.IsTrue(fromBehind.Valid && fromBehind.Knockout && fromBehind.KnockedOutGuard == 0, "rear approach knocks g1 out");
            _ = init;
        }
    }
}
