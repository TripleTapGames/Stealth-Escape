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
            try
            {
                var r = Build<ResultsScreen>(root);
                r.Present(stage, StageMode.Knockout, 4, 4, true, false, false, true);
                var stars = r.transform.Find("SafeArea/Card/StarsRow"); Assert.AreEqual(3, stars.childCount);
                for (int i = 0; i < 3; i++) Assert.AreEqual(skin.starFull, stars.GetChild(i).GetComponent<Image>().sprite, "three stars at par");
                Assert.AreEqual("★★★", r.StarsText.Text, "text mirror kept for tools"); Assert.AreEqual("4 actions", r.ActionsText.Text); Assert.AreEqual("New best!", r.BestText.Text);
                var items = r.transform.Find("SafeArea/Card/Thresholds/Items");
                Assert.AreEqual("≤ 5", items.GetChild(0).Find("Text").GetComponent<UiText>().Text); Assert.AreEqual("≤ 7", items.GetChild(1).Find("Text").GetComponent<UiText>().Text); Assert.AreEqual("> 7", items.GetChild(2).Find("Text").GetComponent<UiText>().Text);
                Assert.IsFalse(r.GhostText.gameObject.activeSelf, "no Ghost line on a level without cameras");
                r.Present(stage, StageMode.Knockout, 8, 4, false, false, false, false);
                Assert.AreEqual(skin.starFull, stars.GetChild(0).GetComponent<Image>().sprite); Assert.AreEqual(skin.starBlank, stars.GetChild(2).GetComponent<Image>().sprite, "one star past the two-star threshold");
                Assert.AreEqual("ALL CLEAR", r.NextButton.Label.Text); Assert.IsFalse(r.NextButton.Interactable);
                Assert.AreEqual(skin.buttonGreen, SpriteOf(r.NextButton)); Assert.AreEqual(skin.buttonWhite, SpriteOf(r.ReplayButton));
            }
            finally { Object.DestroyImmediate(root.gameObject); }
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
                Assert.AreEqual("LEVELS", l.transform.Find("SafeArea/Card/Header/Title").GetComponent<UiText>().Text); Assert.AreEqual("1/35", l.ProgressText.Text);
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
