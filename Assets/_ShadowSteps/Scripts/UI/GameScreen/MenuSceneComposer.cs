using System.Collections;
using ShadowSteps.Data;
using ShadowSteps.Data.Analytics;
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
            UiBuilder.EnsureEventSystem(); // without it no button in the menu receives taps (the PLAY button was dead)
            var canvas = UiBuilder.CreateCanvas("MenuCanvas", 10);
            Router = canvas.gameObject.AddComponent<ScreenRouter>();
            Router.ReducedMotion = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
            var set = Prefabs != null ? Prefabs : (UiPrefabSet.FromResources() ?? ScriptableObject.CreateInstance<UiPrefabSet>()); // D-056: baked prefabs from Resources unless the scene assigns a set
            var home = Router.Register(set.Create<HomeScreen>(canvas.transform, set.HomeScreen));
            var levels = Router.Register(set.Create<LevelSelectScreen>(canvas.transform, set.LevelSelectScreen));
            var settings = Router.Register(set.Create<SettingsScreen>(canvas.transform, set.SettingsScreen));
            home.OnPlay = () => { var resume = StageProgression.ResumeStage(AppServices.Stages, AppServices.Save); GameAnalytics.Tracker.MenuAction("home", "play", resume?.Code); GameAnalytics.Tracker.SetNextSource(GameAnalyticsTracker.SourceResume); OpenStage(resume.Id); };
            home.OnLevelSelect = () => { GameAnalytics.Tracker.MenuAction("home", "level_select"); Router.Push<LevelSelectScreen>(); };
            home.OnSettings = () => { GameAnalytics.Tracker.MenuAction("home", "settings"); Router.Push<SettingsScreen>(); };
            levels.OnBack = () => { GameAnalytics.Tracker.MenuAction("level_select", "back"); Router.Pop(); };
            levels.OnLevelChosen = id => { GameAnalytics.Tracker.MenuAction("level_select", "open", AppServices.Stages?.ById(id)?.Code); GameAnalytics.Tracker.SetNextSource(GameAnalyticsTracker.SourceSelect); OpenStage(id); };
            settings.OnClose = () => Router.Pop();
            settings.OnChanged = s => Router.ReducedMotion = s.ReducedMotion;
            AudioService.Ensure();
            ShadowSteps.Runtime.Analytics.AnalyticsService.Ensure();
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
