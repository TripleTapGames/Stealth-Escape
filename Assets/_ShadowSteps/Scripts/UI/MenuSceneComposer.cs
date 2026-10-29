using System.Collections;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Composition root of the Home scene: Home, Level Select and Settings screens in one canvas. Opens the requested
    /// stage by setting <see cref="AppServices.PendingStageId"/> and loading the Game scene.
    /// </summary>
    public sealed class MenuSceneComposer : MonoBehaviour
    {
        public UiPrefabSet Prefabs;
        public ScreenRouter Router { get; private set; }
        public bool Ready { get; private set; }

        private IEnumerator Start()
        {
            if (!AppServices.IsInitialized && !AppServices.TryInitializeSync())
            {
                bool ok = false;
                yield return AppServices.InitializeAsync(r => ok = r);
                if (!ok) { Debug.LogError("[ShadowSteps] cannot start menu: " + AppServices.LastError); yield break; }
            }
            Build();
        }

        public void Build()
        {
            var canvas = UiBuilder.CreateCanvas("MenuCanvas", 10);
            Router = canvas.gameObject.AddComponent<ScreenRouter>();
            Router.ReducedMotion = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
            var set = Prefabs != null ? Prefabs : ScriptableObject.CreateInstance<UiPrefabSet>();
            var home = Router.Register(set.Create<HomeScreen>(canvas.transform, set.HomeScreen));
            var levels = Router.Register(set.Create<LevelSelectScreen>(canvas.transform, set.LevelSelectScreen));
            var settings = Router.Register(set.Create<SettingsScreen>(canvas.transform, set.SettingsScreen));
            home.OnPlay = () => OpenStage(StageProgression.FirstUnfinished(AppServices.Stages, AppServices.Save).Id);
            home.OnLevelSelect = () => Router.Push<LevelSelectScreen>();
            home.OnSettings = () => Router.Push<SettingsScreen>();
            levels.OnBack = () => Router.Pop();
            levels.OnLevelChosen = OpenStage;
            settings.OnClose = () => Router.Pop();
            settings.OnChanged = s => Router.ReducedMotion = s.ReducedMotion;
            AudioService.Ensure();
            Router.Replace<HomeScreen>();
            if (AppServices.PendingMenu == AppServices.MenuLevelSelect) Router.Push<LevelSelectScreen>();
            AppServices.PendingMenu = null;
            Ready = true;
        }

        public void OpenStage(int id)
        {
            if (!StageProgression.IsUnlocked(AppServices.Stages, AppServices.Save, id)) return;
            AudioService.Instance?.Play(Sfx.Tap);
            AppServices.PendingStageId = id;
            SceneManager.LoadScene(GameFlowController.GameSceneName);
        }
    }
}
