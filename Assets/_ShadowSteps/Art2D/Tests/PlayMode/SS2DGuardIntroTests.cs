using System.Collections;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
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
    /// Guard first-encounter cards (D-041) in the real Game scene: the card replaces the tutorial guard intro on FTUE-3,
    /// shows the selected mode's tip, locks gameplay input, changes no gameplay state, is acknowledged on "Got it" only,
    /// never repeats on retry, shows again in the other mode, queues several behaviours in order on a later level, is
    /// cancelled by a mode switch, and ignores a stale "Got it" from a previous board/mode. Guard-free levels show none.
    /// </summary>
    public class SS2DGuardIntroTests
    {
        private GameSceneComposer _composer;
        private GameFlowController _flow;
        private SS2DSessionBridge _bridge;
        private GameSessionController S => _composer.Session;
        private SS2DHudCanvas Hud => (SS2DHudCanvas)_composer.Hud;
        private GuardIntroScreen Card => _flow.Router.Get<GuardIntroScreen>();

        private IEnumerator OpenGame(string code, SaveData save = null)
        {
            AppServices.ResetForTests();
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            AppServices.InstallSaveForTests(save ?? new SaveData());
            AppServices.PendingStageId = AppServices.Stages.ByCode(code).Id;
#if UNITY_EDITOR
            EditorSceneManager.LoadSceneInPlayMode("Assets/_ShadowSteps/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("scene loaded by path in the editor only");
#endif
            yield return null; yield return null; yield return null;
            _composer = Object.FindObjectOfType<GameSceneComposer>(); Assert.IsNotNull(_composer);
            float t0 = Time.realtimeSinceStartup; while (!_composer.Ready && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Assert.IsTrue(_composer.Ready);
            _flow = Object.FindObjectOfType<GameFlowController>(); Assert.IsNotNull(_flow);
            _bridge = Object.FindObjectOfType<SS2DSessionBridge>(); Assert.IsNotNull(_bridge);
            yield return null;
            Assert.AreEqual(code, S.Stage.Code);
        }

        /// <summary>Real Game-view frame (board camera + every screen-space canvas) written to Docs/Reports/Renders; needs a graphics device (run without -nographics).</summary>
        private IEnumerator CaptureGameView(string fileName)
        {
            var cam = _composer.BoardCamera; const int w = 1080, h = 1920;
            var overlays = new System.Collections.Generic.List<(Canvas c, RenderMode mode, Camera wc, float plane, int layer, int order)>();
            foreach (var c in Object.FindObjectsOfType<Canvas>())
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add((c, c.renderMode, c.worldCamera, c.planeDistance, c.sortingLayerID, c.sortingOrder));
                    // Camera-space canvases sort with the sprites: put the screen canvases on the top-most layer so the painted background cannot cover them.
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = cam; c.planeDistance = 1f; c.sortingLayerName = SS2DLayers.FX; c.sortingOrder = 100 + c.sortingOrder;
                }
            int prevMask = cam.cullingMask; cam.cullingMask = -1; // the screen canvases live on the UI layer
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); float aspect = cam.aspect; var prevRt = cam.targetTexture;
            cam.targetTexture = rt; cam.aspect = (float)w / h;
            var fitter = cam.GetComponent<SS2DCameraFitter>(); if (fitter != null) fitter.Fit((float)w / h);
            Canvas.ForceUpdateCanvases(); yield return null; yield return null; // let the switched canvases rebuild for the camera (WaitForEndOfFrame never fires in batch mode)
            Canvas.ForceUpdateCanvases(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = prevRt; cam.aspect = aspect; cam.cullingMask = prevMask; rt.Release(); Object.Destroy(rt);
            foreach (var o in overlays) { o.c.renderMode = o.mode; o.c.worldCamera = o.wc; o.c.planeDistance = o.plane; o.c.sortingLayerID = o.layer; o.c.sortingOrder = o.order; }
            var dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Docs/Reports/Renders"); System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, fileName), tex.EncodeToPNG()); Object.Destroy(tex);
            Debug.Log("[SS2D] game view captured -> " + fileName);
        }

        private static string Snapshot(StageState s) => s.Key() + "|" + s.T + "|" + s.Defeated + "|" + s.Result;
        private static void Click(UiButton b) => b.Button.onClick.Invoke();
        private static void Click(RectTransform rt) => rt.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

        [UnityTest]
        public IEnumerator Ftue3_StationaryCardReplacesTheIntro_LocksInput_ChangesNothing_SeenOnGotIt_OtherModeShowsItsTip()
        {
            yield return OpenGame("FTUE-3");
            Assert.IsTrue(Card.IsVisible, "stationary-guard card shown at the first encounter");
            Assert.IsFalse(_flow.Router.Get<OnboardingScreen>().IsVisible, "the overlapping tutorial guard intro is not shown as a second popup");
            Assert.IsTrue(AppServices.Save.HasSeenTutorial("stage:" + S.Stage.Id), "the superseded intro counts as delivered");
            Assert.AreEqual(GuardKind.Static, Card.Card.Kind); Assert.AreEqual(StageMode.Knockout, Card.Card.Mode, "the shipped game runs Knockout (D-043)");
            Assert.AreEqual("STATIONARY GUARD", Card.TitleText.Text); Assert.AreEqual("KNOCKOUT MODE", Card.ModeLabelText.Text);
            Assert.IsTrue(Card.ModeTipText.Text.Contains("behind, left or right")); Assert.IsTrue(Card.ModeTipText.Text.Contains("current facing")); Assert.AreEqual("GOT IT", Card.OkButton.Label.Text);
            Assert.AreEqual(0, _bridge.HighlightedGuard, "representative guard highlighted");
            Assert.AreEqual(_bridge.Renderer.library.nodeReachable, _bridge.Renderer.Guards[0].body.color);
            Assert.IsTrue(S.InputLocked); Assert.IsFalse(S.TrySubmit(StageAction.Wait), "gameplay input blocked while the card is open");
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) yield return CaptureGameView("Art2D_GuardCard_FTUE-3_1080x1920.png");
            string before = Snapshot(S.State); int saveFlags = AppServices.Save.SeenTutorials.Count;
            Assert.IsFalse(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Static, StageMode.Knockout), "not marked on display");
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked);
            Assert.AreEqual(before, Snapshot(S.State), "acknowledging changes no gameplay state"); Assert.AreEqual(0, S.State.T);
            Assert.IsTrue(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Static, StageMode.Knockout)); Assert.AreEqual(saveFlags + 1, AppServices.Save.SeenTutorials.Count);
            Assert.AreEqual(-1, _bridge.HighlightedGuard); Assert.AreEqual(Color.white, _bridge.Renderer.Guards[0].body.color);
            Assert.IsFalse(AppServices.Save.IsCompleted(S.Stage.Id), "acknowledging never completes or unlocks anything");
            // Retry: nothing repeats.
            S.Retry(); yield return null; Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked);
            // The other (engine-only) mode: its unseen tip shows on the same board.
            S.SetRulesMode(StageMode.Stealth); yield return null;
            Assert.AreEqual(StageMode.Stealth, S.RulesMode);
            Assert.IsTrue(Card.IsVisible); Assert.AreEqual(StageMode.Stealth, Card.Card.Mode); Assert.AreEqual("STEALTH MODE", Card.ModeLabelText.Text);
            Assert.IsTrue(Card.ModeTipText.Text.Contains("Knockouts are off"));
            Assert.AreEqual(0, S.State.T);
            Click(Card.OkButton); yield return null;
            Assert.IsTrue(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Static, StageMode.Stealth)); Assert.IsFalse(Card.IsVisible);
            // Back to Knockout: already acknowledged there, no card.
            S.SetRulesMode(StageMode.Knockout); yield return null; Assert.IsFalse(Card.IsVisible);
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); // play continues normally
        }

        [UnityTest]
        public IEnumerator GuardFreeLevel_ShowsNoGuardCard_OnlyItsOwnIntro()
        {
            yield return OpenGame("FTUE-1");
            Assert.IsFalse(Card.IsVisible); Assert.AreEqual(0, _flow.PendingGuardCards);
            Assert.IsTrue(_flow.Router.Get<OnboardingScreen>().IsVisible, "unrelated tutorial guidance is kept");
            _flow.Router.Get<OnboardingScreen>().OnDismiss(); yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked);
        }

        [UnityTest]
        public IEnumerator LaterLevel_OneMessagePerVisit_GuardCardFirst_IntroDeferred_ModeSwitchGivesOneCard_StaleGotItIsIgnored()
        {
            yield return OpenGame("LEVEL-5"); // first loop patrol of the campaign; the level also has its own intro
            var stage = S.Stage; Assert.Greater(stage.Guards.Count, 0);
            var expected = GuardOnboarding.BuildQueue(stage, StageState.Initial(stage, StageMode.Knockout), new SaveData());
            Assert.AreEqual(1, expected.Count); Assert.AreEqual(GuardKind.Loop, expected[0].Kind);
            var intro = _flow.Router.Get<OnboardingScreen>();
            // The first-encounter card is this visit's one message; the level intro is deferred, not lost.
            Assert.IsTrue(Card.IsVisible, "loop-patrol card"); Assert.AreEqual(GuardKind.Loop, Card.Card.Kind); Assert.AreEqual(StageMode.Knockout, Card.Card.Mode);
            Assert.IsFalse(intro.IsVisible); Assert.IsFalse(AppServices.Save.HasSeenTutorial("stage:" + stage.Id), "the intro stays pending for the next visit");
            var staleDismiss = Card.OnDismiss; var staleCard = Card.Card;
            // Mode switch (engine API) while the card is open: cancelled (not acknowledged); the other mode shows its own single card; a stale "Got it" does nothing.
            S.SetRulesMode(StageMode.Stealth); yield return null;
            Assert.AreEqual(StageMode.Stealth, S.RulesMode); Assert.AreEqual(0, S.State.T);
            Assert.IsTrue(Card.IsVisible); Assert.AreEqual(StageMode.Stealth, Card.Card.Mode); Assert.AreEqual(GuardKind.Loop, Card.Card.Kind); Assert.AreNotSame(staleCard, Card.Card);
            Assert.IsFalse(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Loop, StageMode.Knockout), "the interrupted Knockout card stays unseen");
            staleDismiss(); yield return null;
            Assert.IsTrue(Card.IsVisible); Assert.IsTrue(S.InputLocked);
            Assert.IsFalse(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Loop, StageMode.Knockout)); Assert.IsFalse(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Loop, StageMode.Stealth));
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked); Assert.AreEqual(0, S.State.T);
            Assert.IsTrue(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Loop, StageMode.Stealth));
            Assert.IsFalse(intro.IsVisible, "still one message this visit: no intro after the card");
            S.Retry(); yield return null; Assert.IsFalse(Card.IsVisible); Assert.IsFalse(intro.IsVisible, "retry never adds a message");
            // Back to Knockout (the shipped mode): its loop card is still unseen → one card.
            S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.IsTrue(Card.IsVisible); Assert.AreEqual(StageMode.Knockout, Card.Card.Mode);
            Click(Card.OkButton); yield return null; Assert.IsTrue(GuardOnboarding.IsSeen(AppServices.Save, GuardKind.Loop, StageMode.Knockout));
            // Next visit: no guard card is pending any more, so the deferred level intro is the message.
            _composer.OpenStage(stage); yield return null; yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsTrue(intro.IsVisible, "deferred intro shows on the next visit"); Assert.IsTrue(AppServices.Save.HasSeenTutorial("stage:" + stage.Id));
            intro.OnDismiss(); yield return null; Assert.IsFalse(S.InputLocked);
            // Third visit: nothing left to say.
            _composer.OpenStage(stage); yield return null; yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(intro.IsVisible);
        }
    }
}
