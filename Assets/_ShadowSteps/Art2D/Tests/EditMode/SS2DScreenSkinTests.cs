using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-055 screen skin: with the imported UiSkin the four screens build from the user's art (card, pill buttons, stars, cop, toggles,
    /// level boxes), keep their public API (texts, buttons, Present/Rebuild/Refresh) and fall back to the placeholder look without it.
    /// </summary>
    public class SS2DScreenSkinTests
    {
        private static UiSkin Skin() { var s = AssetDatabase.LoadAssetAtPath<UiSkin>(UiSkin.DefaultAssetPath); if (s == null || !s.IsComplete) Assert.Ignore("screen skin not imported (Shadow Steps ▸ UI ▸ Import screen skin)"); return s; }
        private static Transform Canvas() { var go = new GameObject("test-canvas", typeof(RectTransform)); go.AddComponent<Canvas>(); return go.transform; }
        private static T Build<T>(Transform parent) where T : UIScreen => ScriptableObject.CreateInstance<UiPrefabSet>().Create<T>(parent, null);
        private static Sprite SpriteOf(Component c) => c.GetComponent<Image>()?.sprite;

        [Test]
        public void Pause_UsesCardAndPillButtons_MatchingTheMock()
        {
            var skin = Skin(); UiSkin.Current = skin; var root = Canvas();
            try
            {
                var p = Build<PauseScreen>(root);
                Assert.IsTrue(p.IsModal);
                Assert.AreEqual(skin.card, p.transform.Find("SafeArea/Card").GetComponent<Image>().sprite);
                Assert.AreEqual("PAUSED", p.transform.Find("SafeArea/Card/Title").GetComponent<UiText>().Text);
                Assert.AreEqual(skin.buttonGreen, SpriteOf(p.ResumeButton), "RESUME is the teal pill");
                Assert.AreEqual(skin.buttonWhite, SpriteOf(p.RestartButton)); Assert.AreEqual(skin.buttonWhite, SpriteOf(p.SettingsButton)); Assert.AreEqual(skin.buttonWhite, SpriteOf(p.LevelSelectButton));
                Assert.AreEqual("RESTART LEVEL", p.RestartButton.Label.Text); Assert.AreEqual("SETTINGS", p.SettingsButton.Label.Text); Assert.AreEqual("LEVEL SELECT", p.LevelSelectButton.Label.Text);
                Assert.AreEqual(Image.Type.Sliced, p.ResumeButton.GetComponent<Image>().type, "pill buttons are 9-sliced");
                var card = p.transform.Find("SafeArea/Card"); Assert.AreEqual(5, card.childCount, "title + four buttons, in order");
                Assert.AreEqual("Resume", card.GetChild(1).name); Assert.AreEqual("Restart", card.GetChild(2).name); Assert.AreEqual("Settings", card.GetChild(3).name); Assert.AreEqual("Levels", card.GetChild(4).name);
                int resumed = 0, restarted = 0; p.OnResume = () => resumed++; p.OnRestart = () => restarted++;
                p.ResumeButton.Button.onClick.Invoke(); p.RestartButton.Button.onClick.Invoke();
                Assert.AreEqual(1, resumed); Assert.AreEqual(1, restarted);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [Test]
        public void Failure_UsesCardCopAndPillButtons_AndPresentsTheCause()
        {
            var skin = Skin(); UiSkin.Current = skin; var root = Canvas();
            try
            {
                var f = Build<FailureScreen>(root);
                Assert.AreEqual(skin.card, f.transform.Find("SafeArea/Card").GetComponent<Image>().sprite);
                Assert.AreEqual(skin.cop, f.transform.Find("SafeArea/Card/Cop/Image").GetComponent<Image>().sprite, "the guard illustration");
                Assert.AreEqual(skin.buttonGreen, SpriteOf(f.RetryButton)); Assert.AreEqual(skin.buttonWhite, SpriteOf(f.LevelSelectButton)); Assert.AreEqual(skin.buttonWhite, SpriteOf(f.UndoButton));
                Assert.AreEqual(Image.Type.Sliced, f.RetryButton.GetComponent<Image>().type, "pill buttons are 9-sliced");
                f.Present(new ActionOutcome { CaptureCause = CaptureCause.Seen, CapturePhase = CapturePhase.BeforeEnemyUpdate }, false);
                Assert.AreEqual("BUSTED", f.TitleText.Text); StringAssert.Contains("before it moved", f.ReasonText.Text); Assert.IsFalse(f.UndoButton.gameObject.activeSelf);
                f.Present(new ActionOutcome { Lockdown = true }, true); Assert.AreEqual("SECURITY LOCKDOWN", f.TitleText.Text); Assert.IsTrue(f.UndoButton.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [Test]
        public void Results_ShowsStarsThresholdsAndButtons_FromTheSkin()
        {
            var skin = Skin(); UiSkin.Current = skin; var root = Canvas();
            var stage = new StageBuilder { Id = 1, Code = "T", Par = 6, ThreeStarActions = 7, TwoStarActions = 9, KnockoutPar = 4, KnockoutThreeStarActions = 5, KnockoutTwoStarActions = 7 }.FromRows(new[] { "....." }).Build();
            Sprite Pick(Sprite own, Sprite shared) => own != null ? own : shared; // the level-complete overrides win when imported
            try
            {
                var r = Build<ResultsScreen>(root);
                r.Present(stage, StageMode.Knockout, 4, 4, true, false, false, true);
                var card = r.transform.Find("SafeArea/CardBounds/Card"); Assert.IsNotNull(card, "card inside the aspect-fitted bounds");
                Assert.AreEqual(Pick(skin.resultsCard, skin.card), card.GetComponent<Image>().sprite);
                var stars = card.Find("StarsRow"); Assert.AreEqual(3, stars.childCount);
                for (int i = 0; i < 3; i++) Assert.AreEqual(Pick(skin.resultsStarFull, skin.starFull), stars.GetChild(i).GetComponent<Image>().sprite, "three stars at par");
                Assert.AreEqual(3, r.StarCount); Assert.IsNull(r.StarsText, "no text mirror on the skinned card (user layout)"); Assert.IsNull(r.GhostText);
                Assert.AreEqual("4 actions", r.ActionsText.Text); Assert.AreEqual("New best!", r.BestText.Text);
                var items = card.Find("Thresholds/Items");
                Assert.AreEqual(Pick(skin.resultsStatBox, skin.statBox), card.Find("Thresholds").GetComponent<Image>().sprite);
                Assert.AreEqual("≤ 5", items.GetChild(0).Find("Text").GetComponent<UiText>().Text); Assert.AreEqual("≤ 7", items.GetChild(1).Find("Text").GetComponent<UiText>().Text); Assert.AreEqual("> 7", items.GetChild(2).Find("Text").GetComponent<UiText>().Text);
                r.Present(stage, StageMode.Knockout, 8, 4, false, false, false, false);
                Assert.AreEqual(1, r.StarCount);
                Assert.AreEqual(Pick(skin.resultsStarFull, skin.starFull), stars.GetChild(0).GetComponent<Image>().sprite); Assert.AreEqual(Pick(skin.resultsStarBlank, skin.starBlank), stars.GetChild(2).GetComponent<Image>().sprite, "one star past the two-star threshold");
                Assert.AreEqual("ALL CLEAR", r.NextButton.Label.Text); Assert.IsFalse(r.NextButton.Interactable);
                Assert.AreEqual(Pick(skin.resultsButtonGreen, skin.buttonGreen), SpriteOf(r.NextButton)); Assert.AreEqual(Pick(skin.resultsButtonWhite, skin.buttonWhite), SpriteOf(r.ReplayButton));
                Assert.AreEqual(Image.Type.Sliced, r.NextButton.GetComponent<Image>().type);
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        [Test]
        public void ResultsOverrides_AreImportedFromTheLevelCompleteArt()
        {
            var skin = Skin();
            Assert.IsNotNull(skin.resultsCard, "resultsCard"); Assert.IsNotNull(skin.resultsStatBox, "resultsStatBox"); Assert.IsNotNull(skin.resultsButtonGreen, "resultsButtonGreen");
            Assert.IsNotNull(skin.resultsButtonWhite, "resultsButtonWhite"); Assert.IsNotNull(skin.resultsStarFull, "resultsStarFull"); Assert.IsNotNull(skin.resultsStarBlank, "resultsStarBlank");
            Assert.Greater(skin.resultsCard.border.x, 0f, "the card is 9-sliced"); Assert.Greater(skin.resultsButtonGreen.border.x, 0f, "pill caps"); Assert.Greater(skin.resultsStatBox.border.x, 0f, "strip caps");
            Assert.AreEqual(Vector4.zero, skin.resultsStarFull.border, "stars are plain");
        }

        [Test]
        public void Settings_RowsCarryToggleArt_AndRefreshFlipsThem()
        {
            var skin = Skin(); UiSkin.Current = skin; var root = Canvas();
            AppServices.ResetForTests(); AppServices.InstallSaveForTests(new SaveData());
            try
            {
                var s = Build<SettingsScreen>(root);
                Assert.AreEqual(skin.statBox, SpriteOf(s.MusicButton)); Assert.AreEqual(skin.statBox, SpriteOf(s.PrivacyButton)); Assert.AreEqual(skin.buttonGreen, SpriteOf(s.CloseButton));
                Assert.AreEqual(skin.chevron, s.PrivacyButton.transform.Find("Chevron").GetComponent<Image>().sprite);
                var toggle = s.MusicButton.transform.Find("Toggle").GetComponent<Image>(); var state = s.MusicButton.transform.Find("State").GetComponent<UiText>();
                typeof(SettingsScreen).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(s, null); // SendMessage asserts in edit mode
                Assert.AreEqual(skin.toggleOn, toggle.sprite); Assert.AreEqual("ON", state.Text); Assert.AreEqual("MUSIC", s.MusicButton.Label.Text, "the row label stays clean; the state has its own word");
                s.MusicButton.Button.onClick.Invoke();
                Assert.IsFalse(AppServices.Save.Settings.MusicOn); Assert.AreEqual(skin.toggleOff, toggle.sprite); Assert.AreEqual("OFF", state.Text);
            }
            finally { Object.DestroyImmediate(root.gameObject); AppServices.ResetForTests(); }
        }

        [Test]
        public void LevelSelect_BuildsOneBoxPerStage_GreenWithStarsWhenOpen_GreyWithLockWhenLocked()
        {
            var skin = Skin(); UiSkin.Current = skin; var root = Canvas();
            AppServices.ResetForTests(); AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            var save = new SaveData(); save.RecordWin(1, 2, 3, false, 2, StageMode.Knockout); AppServices.InstallSaveForTests(save);
            try
            {
                var l = Build<LevelSelectScreen>(root); l.Rebuild();
                Assert.AreEqual(skin.cardTall, l.transform.Find("SafeArea/Card").GetComponent<Image>().sprite);
                Assert.AreEqual(skin.backButton, SpriteOf(l.BackButton)); Assert.AreEqual(skin.backArrow, l.BackButton.transform.Find("Arrow").GetComponent<Image>().sprite);
                Assert.AreEqual("LEVELS", l.transform.Find("SafeArea/Card/Header/Title").GetComponent<UiText>().Text); Assert.AreEqual("1/" + AppServices.Stages.Count, l.ProgressText.Text); // one win out of the whole pack (50 since D-081)
                Assert.AreEqual(AppServices.Stages.Count, l.Grid.childCount, "one box per stage");
                var first = l.Grid.GetChild(0); Assert.AreEqual(skin.levelBoxOpen, first.GetComponent<Image>().sprite); Assert.IsTrue(first.GetComponent<Button>().interactable);
                Assert.AreEqual("FTUE 1", first.Find("Code").GetComponent<UiText>().Text);
                var stars = first.Find("Stars"); Assert.AreEqual(3, stars.childCount); for (int i = 0; i < 3; i++) Assert.AreEqual(skin.starFull, stars.GetChild(i).GetComponent<Image>().sprite, "three stars recorded");
                var second = l.Grid.GetChild(1); Assert.AreEqual(skin.levelBoxOpen, second.GetComponent<Image>().sprite, "unlocked by the win"); Assert.AreEqual(skin.starBlank, second.Find("Stars").GetChild(0).GetComponent<Image>().sprite, "no stars yet");
                var locked = l.Grid.GetChild(2); Assert.AreEqual(skin.levelBoxLocked, locked.GetComponent<Image>().sprite); Assert.IsFalse(locked.GetComponent<Button>().interactable);
                Assert.AreEqual(skin.lockIcon, locked.Find("Lock").GetComponent<Image>().sprite); Assert.IsNull(locked.Find("Stars"));
                int chosen = -1; l.OnLevelChosen = id => chosen = id; first.GetComponent<Button>().onClick.Invoke(); Assert.AreEqual(1, chosen);
            }
            finally { Object.DestroyImmediate(root.gameObject); AppServices.ResetForTests(); }
        }

        [Test]
        public void WithoutTheSkin_EveryScreenStillBuildsItsPlaceholderLook()
        {
            var keep = UiSkin.Current; UiSkin.Current = ScriptableObject.CreateInstance<UiSkin>(); var root = Canvas();
            try
            {
                Assert.IsFalse(UiSkin.Available);
                var f = Build<FailureScreen>(root); Assert.IsNotNull(f.transform.Find("SafeArea/Card/Title")); Assert.IsNull(f.transform.Find("SafeArea/Card/Cop"));
                var r = Build<ResultsScreen>(root); Assert.IsNotNull(r.StarsText); Assert.IsNull(r.transform.Find("SafeArea/Card/StarsRow"));
                var s = Build<SettingsScreen>(root); Assert.IsNotNull(s.CloseButton);
            }
            finally { Object.DestroyImmediate(UiSkin.Current); UiSkin.Current = keep; Object.DestroyImmediate(root.gameObject); }
        }
    }
}
