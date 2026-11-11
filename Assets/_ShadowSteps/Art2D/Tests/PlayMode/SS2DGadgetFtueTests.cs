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
    /// Smoke / EMP first-encounter cards (D-075) and the two-step guide (D-076) in the real Game scene, reproducing the user's
    /// report of 2026-11-10: a fresh save (nothing acknowledged, debug unlock) jumping straight to LEVEL-21 must get the Smoke
    /// card, not the stationary-guard card; after "Got it" the HUD pill reads "Tap SMOKE to arm it" with the button accented,
    /// arming switches it to the node prompt, deploying finishes the guide once and for all. LEVEL-23 does the same for EMP.
    /// </summary>
    public class SS2DGadgetFtueTests
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
            AppServices.InstallSaveForTests(save ?? new SaveData { DebugUnlockAll = true });
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
        private Color SmokeButtonColor => Hud.smokeButton.GetComponent<UnityEngine.UI.Image>().color;
        private static bool SameRgb(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        private FtueSpotlightScreen Spot => _flow.Spotlight;
        private Vector2 NodeScreen(GridCoord n) => _composer.BoardCamera.WorldToScreenPoint(_composer.NodeWorldPosition(n));
        private Vector2 ButtonScreen(RectTransform rt) { var c = new Vector3[4]; rt.GetWorldCorners(c); var cam = Hud.canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Hud.canvas.worldCamera; return RectTransformUtility.WorldToScreenPoint(cam, (c[0] + c[2]) * 0.5f); }
        private IEnumerator Settle() { float t0 = Time.realtimeSinceStartup; yield return null; while (S.IsBusy && Time.realtimeSinceStartup - t0 < 5f) yield return null; yield return null; }
        private void AssertSpotlightOn(Vector2 screenPoint, string message, string why)
        {
            Assert.IsTrue(Spot.IsVisible, why + ": spotlight visible");
            Assert.IsTrue(Spot.Hole.HasValue, why + ": cut-out present");
            Assert.IsTrue(Spot.Hole.Value.Contains(screenPoint), why + ": cut-out " + Spot.Hole.Value + " contains " + screenPoint + " | screen " + Screen.width + "x" + Screen.height
                + " smoke " + ButtonScreen(Hud.smokeButton) + " emp " + ButtonScreen(Hud.empButton) + " node(3,2) " + NodeScreen(new GridCoord(3, 2)) + " node(2,2) " + NodeScreen(new GridCoord(2, 2))
                + " step " + _flow.GadgetGuideCurrent + " mode " + S.Mode + " pulsing " + Hud.IsPulsing(Hud.smokeButton) + " smokeScale " + Hud.smokeButton.localScale);
            Assert.AreEqual(message, Spot.MessageText.Text, why); Assert.AreEqual(message, Hud.messageLabel.text, why + ": HUD line");
            Assert.IsTrue(Spot.Top.raycastTarget && Spot.Bottom.raycastTarget, why + ": dim panels block taps");
            // The fingertip (the hand's pivot) stays within one tap-lift of the cut-out centre and touches it at the bottom of each tap.
            Assert.LessOrEqual(Vector2.Distance(Spot.Hand.anchoredPosition, Spot.HoleCentreLocal), 26f * 1.2f + 0.5f, why + ": fingertip on the target (hand " + Spot.Hand.anchoredPosition + ", centre " + Spot.HoleCentreLocal + ")");
        }

        [UnityTest]
        public IEnumerator Level21_FreshSave_SmokeCardFirst_ThenSpotlightScript_MoveArmDrop_UntilTheSmokeIsThrown()
        {
            yield return OpenGame("LEVEL-21"); // fresh save, nothing acknowledged, exactly the user's 2026-11-10 session
            Assert.IsTrue(Card.IsVisible, "a card is shown");
            Assert.IsNotNull(_flow.ActiveGadgetCard, "the Smoke card wins the one-message slot on its own tutorial level even though the stationary-guard card is unseen too");
            Assert.AreEqual(GadgetKind.Smoke, _flow.ActiveGadgetCard.Kind); Assert.IsNull(_flow.ActiveGuardCard);
            Assert.AreEqual("SMOKE", Card.TitleText.Text); Assert.AreEqual("HOW TO USE", Card.ModeLabelText.Text);
            Assert.IsFalse(_flow.Router.Get<OnboardingScreen>().IsVisible, "the level intro (which only repeats the card) is not a second popup");
            Assert.IsTrue(S.InputLocked);
            Assert.IsTrue(Card.transform.GetSiblingIndex() > Spot.transform.GetSiblingIndex(), "the card sits above the spotlight overlay");
            if (CanCapture) yield return CaptureGameView("Art2D_GadgetCard_Smoke_LEVEL-21_1080x1920.png");
            Click(Card.OkButton); yield return null;
            Assert.IsFalse(Card.IsVisible); Assert.IsFalse(S.InputLocked);
            Assert.IsTrue(GadgetOnboarding.IsSeen(AppServices.Save, GadgetKind.Smoke));
            yield return new WaitForSecondsRealtime(0.3f); // let the card fade out before the captures
            // Dormant: the route moves first, but that move is the player's own (user, 2026-11-10): no overlay, no line, until they reach (2,2).
            Assert.IsTrue(_flow.GadgetGuideActive); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.MoveTo, _flow.GadgetGuideCurrent.Kind); Assert.IsTrue(_flow.GadgetGuideCurrent.Silent);
            Assert.IsFalse(Spot.IsVisible, "nothing shown while waiting for the player's own move"); Assert.IsFalse(Hud.IsPulsing(Hud.smokeButton));
            yield return new WaitForSecondsRealtime(3.4f); // the HUD's 3 s mode hint expires
            Assert.AreEqual(string.Empty, Hud.messageLabel.text, "no guide line while dormant");
            Assert.IsTrue(S.TapNode(new GridCoord(2, 2)), "the board is fully tappable"); yield return Settle();
            Assert.AreEqual(1, S.State.T);
            // FTUE 1: the SMOKE button, accented and pulsing, everything else dimmed.
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, _flow.GadgetGuideCurrent.Kind);
            AssertSpotlightOn(ButtonScreen(Hud.smokeButton), "Tap to select", "FTUE 1");
            if (UiSkin.Current != null && UiSkin.Current.tapHand != null) { Assert.IsTrue(Spot.HandIsArt, "the user's hand art is used"); Assert.AreEqual(UiSkin.Current.tapHand, Spot.Hand.GetComponent<UnityEngine.UI.Image>().sprite); }
            Assert.IsTrue(SameRgb(SmokeButtonColor, Hud.accent)); Assert.IsTrue(Hud.IsPulsing(Hud.smokeButton));
            Assert.IsFalse(Spot.Hole.Value.Contains(NodeScreen(new GridCoord(3, 2))), "the board is covered");
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Step1_Arm_LEVEL-21_1080x1920.png");
            yield return new WaitForSecondsRealtime(3.4f);
            Assert.AreEqual("Tap to select", Hud.messageLabel.text, "guide line survives transient lines");
            Hud.smokeButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.AreEqual(InputMode.SmokeTarget, S.Mode);
            // FTUE 2: only the node under the guard is open; the line states the cast limits.
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployNode, _flow.GadgetGuideCurrent.Kind); Assert.AreEqual(new GridCoord(3, 2), _flow.GadgetGuideCurrent.Node);
            yield return null;
            AssertSpotlightOn(NodeScreen(new GridCoord(3, 2)), _flow.GadgetGuideCurrent.Message, "FTUE 2");
            StringAssert.Contains("Smoke reaches only the nodes beside you", Spot.MessageText.Text); StringAssert.Contains("lasts 2 moves", Spot.MessageText.Text);
            Assert.IsFalse(Spot.Hole.Value.Contains(NodeScreen(new GridCoord(2, 2))), "the player's own node (also a valid target) is covered");
            Assert.IsFalse(Hud.IsPulsing(Hud.smokeButton), "button pulse off once armed");
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Step2_Cast_LEVEL-21_1080x1920.png");
            // Backing out of targeting returns to step 2; arming again returns to step 3.
            S.ToggleMode(InputMode.SmokeTarget); yield return null;
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, _flow.GadgetGuideCurrent.Kind);
            yield return null; // the cut-out follows the step on the next frame (one-frame lag observed in batch runs)
            AssertSpotlightOn(ButtonScreen(Hud.smokeButton), "Tap to select", "step back to arming");
            S.ToggleMode(InputMode.SmokeTarget); yield return null; Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployNode, _flow.GadgetGuideCurrent.Kind);
            // Drop it: the used flag is written now; FTUE 3 asks the player to walk into the smoke.
            Assert.IsTrue(S.TapNode(new GridCoord(3, 2))); yield return Settle();
            Assert.AreEqual(1, S.State.Clouds.Count, "smoke thrown"); Assert.AreEqual(1, S.State.T, "deployment is free");
            Assert.IsTrue(GadgetOnboarding.IsUsed(AppServices.Save, GadgetKind.Smoke), "used flag on the throw, not only at the end");
            Assert.IsTrue(_flow.GadgetGuideActive); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.FollowUp, _flow.GadgetGuideCurrent.Kind); Assert.AreEqual(new GridCoord(3, 2), _flow.GadgetGuideCurrent.Node);
            yield return null;
            AssertSpotlightOn(NodeScreen(new GridCoord(3, 2)), _flow.GadgetGuideCurrent.Message, "FTUE 3");
            StringAssert.Contains("Walk into the smoke", Spot.MessageText.Text); StringAssert.Contains("can't catch you inside", Spot.MessageText.Text);
            Assert.IsFalse(SameRgb(SmokeButtonColor, Hud.accent)); Assert.IsFalse(Hud.IsPulsing(Hud.smokeButton));
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Step3_WalkIn_LEVEL-21_1080x1920.png");
            Assert.IsTrue(S.TapNode(new GridCoord(3, 2))); yield return Settle();
            Assert.AreEqual(new GridCoord(3, 2), S.State.Player); Assert.AreEqual(2, S.State.T); Assert.AreEqual(StageResult.Playing, S.State.Result, "hidden in the smoke under the guard's nose");
            Assert.IsFalse(_flow.GadgetGuideActive); Assert.IsFalse(Spot.IsVisible, "overlay removed");
            StringAssert.StartsWith("Hidden in the smoke", Hud.messageLabel.text);
            yield return new WaitForSecondsRealtime(3f);
            Assert.IsFalse(Spot.IsVisible); Assert.AreNotEqual("Tap to select", Hud.messageLabel.text, "no guide line comes back once done");
            // Retry never adds a message (D-041) and the used gadget is not taught again.
            S.Retry(); yield return null; yield return null;
            Assert.IsFalse(_flow.GadgetGuideActive); Assert.IsFalse(Spot.IsVisible); Assert.IsFalse(Card.IsVisible);
            // Next visit: card seen, gadget used, so the deferred stationary-guard card is that visit's one message.
            _composer.OpenStage(S.Stage); yield return null; yield return null;
            Assert.IsFalse(_flow.GadgetGuideActive);
            Assert.IsTrue(Card.IsVisible); Assert.IsNotNull(_flow.ActiveGuardCard); Assert.AreEqual(GuardKind.Static, _flow.ActiveGuardCard.Kind);
            Click(Card.OkButton); yield return null;
        }

        [UnityTest]
        public IEnumerator Level21_PlayerIgnoresTheScript_GuideReplansFromWhereTheyAre()
        {
            var save = new SaveData { DebugUnlockAll = true }; GuardOnboarding.MarkAllSeen(save);
            save.SeenTutorials.Remove(GadgetOnboarding.UsedKey(GadgetKind.Smoke));
            yield return OpenGame("LEVEL-21", save);
            var intro = _flow.Router.Get<OnboardingScreen>(); if (intro.IsVisible) { intro.OnDismiss(); yield return null; } // card seen earlier: the level's own intro is this visit's message
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.MoveTo, _flow.GadgetGuideCurrent.Kind); Assert.IsTrue(_flow.GadgetGuideCurrent.Silent); Assert.IsFalse(Spot.IsVisible);
            // The expected move by keyboard/swipe advances the dormant script; an off-script smoke on the player's own node diverges and re-plans.
            Assert.IsTrue(S.TrySubmit(StageAction.Move(Direction.East))); yield return Settle();
            Assert.IsTrue(_flow.GadgetGuideActive); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, _flow.GadgetGuideCurrent.Kind, "reached the second node: arming");
            yield return null;
            AssertSpotlightOn(ButtonScreen(Hud.smokeButton), "Tap to select", "arm after the player's own move");
            S.ToggleMode(InputMode.SmokeTarget); yield return null;
            Assert.IsTrue(S.TrySubmit(StageAction.Smoke(new GridCoord(2, 2))), "smoke on the wrong node (engine API)"); yield return Settle();
            Assert.IsTrue(GadgetOnboarding.IsUsed(AppServices.Save, GadgetKind.Smoke), "any real throw counts as used");
            Assert.IsFalse(_flow.GadgetGuideActive, "re-plan from a state with no smoke left and a route that no longer uses it: nothing left to teach");
            Assert.IsFalse(Spot.IsVisible);
        }

        [UnityTest]
        public IEnumerator Level23_EmpCard_ThenSpotlight_ArmThenCamera_UntilTheCameraIsShutDown()
        {
            var save = new SaveData { DebugUnlockAll = true }; GuardOnboarding.MarkAllSeen(save);
            save.SeenTutorials.Remove(GadgetOnboarding.SeenKey(GadgetKind.Emp)); save.SeenTutorials.Remove(GadgetOnboarding.UsedKey(GadgetKind.Emp));
            yield return OpenGame("LEVEL-23", save);
            Assert.IsNotNull(_flow.ActiveGadgetCard); Assert.AreEqual(GadgetKind.Emp, _flow.ActiveGadgetCard.Kind);
            Assert.AreEqual("EMP", Card.TitleText.Text); StringAssert.Contains("Tap EMP", Card.ModeTipText.Text);
            Click(Card.OkButton); yield return null; yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, _flow.GadgetGuideCurrent.Kind);
            AssertSpotlightOn(ButtonScreen(Hud.empButton), "Tap to select", "EMP arm");
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Step1_Arm_LEVEL-23_1080x1920.png");
            Assert.IsTrue(SameRgb(Hud.empButton.GetComponent<UnityEngine.UI.Image>().color, Hud.accent), "EMP button accented");
            Hud.empButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.AreEqual(InputMode.EmpTarget, S.Mode); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployCamera, _flow.GadgetGuideCurrent.Kind); Assert.AreEqual(0, _flow.GadgetGuideCurrent.CameraIndex);
            var camScreen = (Vector2)_composer.BoardCamera.WorldToScreenPoint(_composer.CameraWorldPosition(0).Value);
            AssertSpotlightOn(camScreen, _flow.GadgetGuideCurrent.Message, "EMP camera"); StringAssert.Contains("Tap the camera to shut it down", Spot.MessageText.Text);
            if (CanCapture) yield return CaptureGameView("Art2D_Spotlight_Step2_Camera_LEVEL-23_1080x1920.png");
            Assert.IsTrue(S.TapCamera(0)); yield return Settle();
            Assert.IsTrue(S.State.IsCameraDisabled(0)); Assert.AreEqual(0, S.State.T); Assert.IsTrue(GadgetOnboarding.IsUsed(AppServices.Save, GadgetKind.Emp));
            // FTUE 3 for EMP: the first move past the dark camera.
            Assert.IsTrue(_flow.GadgetGuideActive); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.FollowUp, _flow.GadgetGuideCurrent.Kind); Assert.AreEqual(new GridCoord(2, 2), _flow.GadgetGuideCurrent.Node);
            yield return null;
            AssertSpotlightOn(NodeScreen(new GridCoord(2, 2)), _flow.GadgetGuideCurrent.Message, "EMP follow-up"); StringAssert.Contains("camera stays dark", Spot.MessageText.Text);
            Assert.IsTrue(S.TapNode(new GridCoord(2, 2))); yield return Settle();
            Assert.IsFalse(_flow.GadgetGuideActive); Assert.IsFalse(Spot.IsVisible);
            StringAssert.StartsWith("Camera down", Hud.messageLabel.text);
        }

        [UnityTest]
        public IEnumerator LaterGadgetLevel_CardAlreadySeen_RouteNeedsNoSmoke_FallbackGuide_UntilFirstUse_ThenNeverAgain()
        {
            var save = new SaveData { DebugUnlockAll = true }; GuardOnboarding.MarkAllSeen(save);
            save.SeenTutorials.Remove(GadgetOnboarding.UsedKey(GadgetKind.Smoke)); // card seen on LEVEL-21, smoke never thrown
            yield return OpenGame("LEVEL-22", save);
            Assert.IsFalse(Card.IsVisible, "card acknowledged earlier");
            var intro = _flow.Router.Get<OnboardingScreen>();
            if (intro.IsVisible) { intro.OnDismiss(); yield return null; }
            // LEVEL-22's shortest route needs no smoke: the fallback script spotlights the button, then lets the player pick any target.
            Assert.IsTrue(_flow.GadgetGuideActive); Assert.AreEqual(GadgetOnboarding.ScriptStepKind.Arm, _flow.GadgetGuideCurrent.Kind);
            AssertSpotlightOn(ButtonScreen(Hud.smokeButton), "Tap to select", "fallback arm");
            Hud.smokeButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.AreEqual(GadgetOnboarding.ScriptStepKind.DeployAny, _flow.GadgetGuideCurrent.Kind);
            Assert.IsFalse(Spot.IsVisible, "no cut-out: every valid target is highlighted by the session itself");
            StringAssert.Contains("glowing node", Hud.messageLabel.text);
            S.ToggleMode(InputMode.SmokeTarget); yield return null;
            GadgetOnboarding.MarkUsed(AppServices.Save, GadgetKind.Smoke);
            S.Retry(); yield return null; yield return null;
            Assert.IsFalse(_flow.GadgetGuideActive, "nothing to teach once used"); Assert.IsFalse(Spot.IsVisible);
            Assert.AreNotEqual("Tap to select", Hud.messageLabel.text);
        }
    }
}
