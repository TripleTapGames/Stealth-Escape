using System.Collections;
using NUnit.Framework;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowSteps.Tests.PlayMode
{
    /// <summary>Home scene: the menu builds, is clickable (EventSystem present) and resolves the first stage to play.</summary>
    public class MenuScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator HomeScene_BuildsMenu_WithEventSystem_AndPlayTargetsFirstStage()
        {
            AppServices.ResetForTests();
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            AppServices.InstallStages(System.IO.File.ReadAllText(AppServices.StagePackPath));
            AppServices.InstallSaveForTests(new SaveData());
            var composer = new GameObject("Menu").AddComponent<MenuSceneComposer>();
            yield return null; yield return null;
            Assert.IsTrue(composer.Ready);
            Assert.IsNotNull(Object.FindObjectOfType<EventSystem>(), "menu buttons need an EventSystem to receive taps");
            var home = composer.Router.Get<HomeScreen>();
            Assert.IsTrue(home.IsVisible);
            Assert.IsNotNull(home.OnPlay, "PLAY is wired");
            Assert.AreEqual(1, StageProgression.FirstUnfinished(AppServices.Stages, AppServices.Save).Id);
            Assert.IsTrue(StageProgression.IsUnlocked(AppServices.Stages, AppServices.Save, 1));
            Assert.IsNotNull(composer.Router.Get<LevelSelectScreen>());
        }
    }
}
