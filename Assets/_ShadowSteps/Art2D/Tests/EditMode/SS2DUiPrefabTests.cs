using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using ShadowSteps.UI.Editor;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-056: every screen bakes to a prefab, the set records all nine, a prefab instance keeps its serialized references (buttons, texts,
    /// skinned star images and toggles) and still presents correctly, and the composers resolve the baked set from Resources.
    /// </summary>
    public class SS2DUiPrefabTests
    {
        private const string TempDir = "Assets/_ShadowSteps/Tests/EditMode/_TempUiPrefabs/";
        private const string TempSet = TempDir + "UiPrefabSet.asset";

        [Test]
        public void BakesNineScreens_AndPrefabInstancesKeepTheirReferences()
        {
            var set = UiPrefabBaker.Bake(TempDir, TempSet);
            var root = new GameObject("test-canvas", typeof(RectTransform)); root.AddComponent<Canvas>();
            try
            {
                Assert.AreEqual(9, UiPrefabBaker.Count(set));
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabAsset(set.FailureScreen));
                var f = set.Create<FailureScreen>(root.transform, set.FailureScreen);
                Assert.IsTrue(f.gameObject.name.StartsWith("FailureScreen"), "instantiated (plain copy) from the prefab, not rebuilt from code"); Assert.Greater(f.transform.childCount, 0, "the prefab's hierarchy came along");
                Assert.IsNotNull(f.TitleText); Assert.IsNotNull(f.ReasonText); Assert.IsNotNull(f.RetryButton); Assert.IsNotNull(f.LevelSelectButton); Assert.IsNotNull(f.UndoButton); Assert.IsTrue(f.IsModal);
                f.Present(new ActionOutcome { Lockdown = true }, false); Assert.AreEqual("SECURITY LOCKDOWN", f.TitleText.Text);
                var stage = new StageBuilder { Id = 1, Code = "T", Par = 6, ThreeStarActions = 7, TwoStarActions = 9 }.FromRows(new[] { "....." }).Build();
                var r = set.Create<ResultsScreen>(root.transform, set.ResultsScreen);
                r.Present(stage, StageMode.Knockout, 6, 6, true, false, false, true);
                Assert.AreEqual("★★★", r.StarsText.Text); Assert.IsTrue(r.NextButton.Interactable);
                if (UiSkin.Available)
                {
                    var stars = r.transform.Find("SafeArea/Card/StarsRow"); Assert.IsNotNull(stars, "skinned prefab");
                    Assert.AreEqual(UiSkin.Current.starFull, stars.GetChild(2).GetComponent<Image>().sprite, "serialized star images still driven by Present");
                    r.Present(stage, StageMode.Knockout, 10, 6, false, false, false, true); Assert.AreEqual(UiSkin.Current.starBlank, stars.GetChild(2).GetComponent<Image>().sprite);
                }
                AppServices.ResetForTests(); AppServices.InstallSaveForTests(new SaveData());
                var s = set.Create<SettingsScreen>(root.transform, set.SettingsScreen);
                typeof(SettingsScreen).GetMethod("Refresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(s, null);
                if (UiSkin.Available) Assert.AreEqual(UiSkin.Current.toggleOn, s.MusicButton.transform.Find("Toggle").GetComponent<Image>().sprite, "serialized toggle images still driven by Refresh");
                var l = set.Create<LevelSelectScreen>(root.transform, set.LevelSelectScreen); Assert.IsNotNull(l.Grid); Assert.IsNotNull(l.BackButton); Assert.IsNotNull(l.ProgressText); Assert.IsFalse(l.IsModal);
                var p = set.Create<PauseScreen>(root.transform, set.PauseScreen); Assert.IsNotNull(p.ResumeButton);
                var g = set.Create<GuardIntroScreen>(root.transform, set.GuardIntroScreen); Assert.IsNotNull(g.OkButton); Assert.IsNotNull(g.TitleText);
                var a = set.Create<AlarmPopupScreen>(root.transform, set.AlarmPopupScreen); Assert.IsNotNull(a.OkButton);
                var h = set.Create<HomeScreen>(root.transform, set.HomeScreen); Assert.IsNotNull(h.PlayButton);
            }
            finally
            {
                Object.DestroyImmediate(root); AppServices.ResetForTests();
                AssetDatabase.DeleteAsset(TempDir.TrimEnd('/'));
            }
        }

        [Test]
        public void ShippedSet_InResources_IsCompleteWhenPresent()
        {
            var set = UiPrefabSet.FromResources();
            if (set == null) Assert.Ignore("no baked set in Resources (Shadow Steps ▸ UI ▸ Bake UI prefabs)");
            Assert.AreEqual(9, UiPrefabBaker.Count(set), "every screen baked");
        }
    }
}
