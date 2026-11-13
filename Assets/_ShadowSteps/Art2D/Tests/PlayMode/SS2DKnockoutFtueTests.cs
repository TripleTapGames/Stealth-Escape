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
    /// First-knockout spotlight guide (D-083) in the real Game scene: a fresh save on FTUE-3 gets the stationary-guard card, then
    /// nothing until the player flanks the guard on their own; on that node the guard is spotlit with the knockout line, the
    /// knockout ends the guide with "Guard down", writes the used flag, and no later level (retry, FTUE-4) teaches it again.
    /// Stealth mode never runs it.
    /// </summary>
    public class SS2DKnockoutFtueTests
    {
        private GameSceneComposer _composer;
        private GameFlowController _flow;
        private GameSessionController S => _composer.Session;
        private SS2DHudCanvas Hud => (SS2DHudCanvas)_composer.Hud;
        private GuardIntroScreen Card => _flow.Router.Get<GuardIntroScreen>();
        private FtueSpotlightScreen Spot => _flow.Spotlight;

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
            yield return null;
            Assert.AreEqual(code, S.Stage.Code);
        }

        private IEnumerator CaptureGameView(string fileName)
        {
            var cam = _composer.BoardCamera; const int w = 1080, h = 1920;
            var overlays = new System.Collections.Generic.List<(Canvas c, RenderMode mode, Camera wc, float plane, int layer, int order)>();
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
            Debug.Log("[SS2D] game view captured -> " + fileName);
        }

        private static void Click(UiButton b) => b.Button.onClick.Invoke();
        private bool CanCapture => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
        private Vector2 NodeScreen(GridCoord n) => _composer.BoardCamera.WorldToScreenPoint(_composer.NodeWorldPosition(n));
        private IEnumerator Settle() { float t0 = Time.realtimeSinceStartup; yield return null; while (S.IsBusy && Time.realtimeSinceStartup - t0 < 5f) yield return null; yield return null; }

        [UnityTest]
        public IEnumerator Ftue3_FreshSave_GuardCard_ThenDormant_ThenGuardSpotlitOnTheFlank_KnockoutEndsIt_NeverAgain()
        {
            yield return OpenGame("FTUE-3");
            Assert.IsTrue(Card.IsVisible, "the stationary-guard card is still the level's one message"); Assert.AreEqual(GuardKind.Static, Card.Card.Kind);
            Assert.IsTrue(_flow.KnockoutGuideActive, "the guide is armed under the card"); Assert.IsTrue(_flow.KnockoutGuideCurrent.Silent);
            Assert.IsFalse(Spot.IsVisible, "nothing spotlit under the card");
            Assert.IsFalse(_flow.GadgetGuideActive);
            int flags = AppServices.Save.SeenTutorials.Count;
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked);
            Assert.AreEqual(flags + 1, AppServices.Save.SeenTutorials.Count, "Got it writes the card flag only; the knockout flag waits for a real knockout");
            Assert.IsFalse(KnockoutOnboarding.IsUsed(AppServices.Save));
            yield return new WaitForSecondsRealtime(0.3f);
            // Dormant: the flanking move is the player's own; no overlay, no pinned line.
            Assert.IsTrue(_flow.KnockoutGuideActive); Assert.IsTrue(_flow.KnockoutGuideCurrent.Silent); Assert.AreEqual(new GridCoord(0, 0), _flow.KnockoutGuideCurrent.Node);
            Assert.IsFalse(Spot.IsVisible, "nothing shown while waiting for the player's own move");
            yield return new WaitForSecondsRealtime(3.4f); // the HUD's 3 s mode hint expires
            Assert.AreEqual(string.Empty, Hud.messageLabel.text, "no guide line while dormant");
            Assert.IsTrue(S.TapNode(new GridCoord(0, 0)), "the board is fully tappable"); yield return Settle();
            Assert.AreEqual(1, S.State.T);
            // The knockout step: the guard's node is the only thing open; the line names the rule.
            Assert.IsTrue(_flow.KnockoutGuideActive); Assert.IsTrue(_flow.KnockoutGuideCurrent.IsKnockout); Assert.AreEqual(0, _flow.KnockoutGuideCurrent.GuardIndex); Assert.AreEqual(new GridCoord(2, 0), _flow.KnockoutGuideCurrent.Node);
            yield return null;
            Assert.IsTrue(Spot.IsVisible, "spotlight visible"); Assert.IsTrue(Spot.Hole.HasValue, "cut-out present");
            Assert.IsTrue(Spot.Hole.Value.Contains(NodeScreen(new GridCoord(2, 0))), "cut-out " + Spot.Hole.Value + " contains the guard's node " + NodeScreen(new GridCoord(2, 0)));
            Assert.IsFalse(Spot.Hole.Value.Contains(NodeScreen(new GridCoord(0, 0))), "the player's node is covered");
            Assert.IsFalse(Spot.Hole.Value.Contains(NodeScreen(new GridCoord(4, 0))), "the node past the guard is covered");
            Assert.AreEqual(KnockoutOnboarding.StepMessage, Spot.MessageText.Text); Assert.AreEqual(KnockoutOnboarding.StepMessage, Hud.messageLabel.text, "HUD line pinned");
            Assert.IsTrue(Spot.Top.raycastTarget && Spot.Bottom.raycastTarget, "dim panels block taps");
            Assert.LessOrEqual(Vector2.Distance(Spot.Hand.anchoredPosition, Spot.HoleCentreLocal), 26f * 1.2f + 0.5f, "fingertip on the guard");
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Knockout_FTUE-3_1080x1920.png");
            yield return new WaitForSecondsRealtime(3.4f);
            Assert.AreEqual(KnockoutOnboarding.StepMessage, Hud.messageLabel.text, "guide line survives transient lines");
            // The knockout: the flag is written, the guide ends with the done line, the overlay goes.
            Assert.IsTrue(S.TapNode(new GridCoord(2, 0))); yield return Settle();
            Assert.IsTrue(S.State.IsDefeated(0), "guard knocked out"); Assert.AreEqual(2, S.State.T); Assert.AreEqual(StageResult.Playing, S.State.Result);
            Assert.IsTrue(KnockoutOnboarding.IsUsed(AppServices.Save), "learned on the knockout itself");
            Assert.IsFalse(_flow.KnockoutGuideActive); Assert.IsFalse(Spot.IsVisible, "overlay removed");
            Assert.AreEqual(KnockoutOnboarding.DoneMessage, Hud.messageLabel.text);
            yield return new WaitForSecondsRealtime(3f);
            Assert.AreNotEqual(KnockoutOnboarding.StepMessage, Hud.messageLabel.text, "no guide line comes back once done");
            // Retry: nothing repeats; the level still plays.
            S.Retry(); yield return null; yield return null;
            Assert.IsFalse(_flow.KnockoutGuideActive); Assert.IsFalse(Spot.IsVisible); Assert.IsFalse(Card.IsVisible);
            Assert.IsTrue(S.TapNode(new GridCoord(0, 0))); yield return Settle();
            Assert.IsFalse(Spot.IsVisible, "no spotlight on the flank once learned");
            // The next guard level: only its own (rotating-guard) card, no knockout guide.
            _composer.OpenStage(AppServices.Stages.ByCode("FTUE-4")); yield return null; yield return null;
            Assert.AreEqual("FTUE-4", S.Stage.Code);
            Assert.IsFalse(_flow.KnockoutGuideActive);
            Assert.IsTrue(Card.IsVisible); Assert.AreEqual(GuardKind.Rotating, Card.Card.Kind);
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(_flow.KnockoutGuideActive); Assert.IsFalse(Spot.IsVisible);
        }

        [UnityTest]
        public IEnumerator Ftue3_OffScriptMove_Replans_AndStealthModeNeverGuides()
        {
            yield return OpenGame("FTUE-3");
            Click(Card.OkButton); yield return null;
            Assert.IsTrue(_flow.KnockoutGuideActive); Assert.IsTrue(_flow.KnockoutGuideCurrent.Silent);
            // Walking east instead puts the player in front of the guard: the guide re-plans from there and stays dormant until the new flank.
            Assert.IsTrue(S.TapNode(new GridCoord(2, 2))); yield return Settle();
            Assert.AreEqual(new GridCoord(2, 2), S.State.Player);
            var expected = KnockoutOnboarding.BuildScript(S.Stage, S.State);
            Assert.AreEqual(expected != null, _flow.KnockoutGuideActive, "re-planned from the new state");
            if (expected != null) { Assert.AreEqual(expected[0].ToString(), _flow.KnockoutGuideCurrent.ToString()); Assert.AreEqual(expected[0].Silent, !Spot.IsVisible); }
            Assert.IsFalse(KnockoutOnboarding.IsUsed(AppServices.Save));
            // Stealth: knockouts are off, so the guide is dropped; the stealth card is that visit's message.
            S.SetRulesMode(StageMode.Stealth); yield return null;
            Assert.AreEqual(StageMode.Stealth, S.RulesMode);
            Assert.IsFalse(_flow.KnockoutGuideActive); Assert.IsFalse(Spot.IsVisible);
            Assert.IsTrue(Card.IsVisible); Assert.AreEqual(StageMode.Stealth, Card.Card.Mode);
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(_flow.KnockoutGuideActive); Assert.IsFalse(Spot.IsVisible);
            // Back to Knockout: the board restarts and the guide arms again from the start.
            S.SetRulesMode(StageMode.Knockout); yield return null;
            Assert.IsTrue(_flow.KnockoutGuideActive); Assert.IsTrue(_flow.KnockoutGuideCurrent.Silent); Assert.IsFalse(Spot.IsVisible);
        }
    }
}
