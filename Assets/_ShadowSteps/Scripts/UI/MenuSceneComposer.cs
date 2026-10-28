using System.Collections;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ShadowSteps.UI
{
    /// <summary>Composition root of the Home scene: Home, Level Select and Settings screens on one canvas.</summary>
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
            AudioService.Ensure();
            Build();
            Ready = true;
        }

        public void Build()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.13f);

            var canvas = UiBuilder.CreateCanvas("MenuCanvas", 10);
            Router = canvas.gameObject.AddComponent<ScreenRouter>();
            Router.ReducedMotion = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
            var set = Prefabs != null ? Prefabs : ScriptableObject.CreateInstance<UiPrefabSet>();
            var home = Router.Register(set.Create<HomeScreen>(canvas.transform, set.HomeScreen));
            var levels = Router.Register(set.Create<LevelSelectScreen>(canvas.transform, set.LevelSelectScreen));
            var settings = Router.Register(set.Create<SettingsScreen>(canvas.transform, set.SettingsScreen));

            home.OnPlay = () => OpenLevel(Progression.FirstUnfinished(AppServices.Levels, AppServices.Save).Id);
            home.OnLevelSelect = () => Router.Push<LevelSelectScreen>();
            home.OnSettings = () => Router.Push<SettingsScreen>();
            levels.OnBack = () => Router.Pop();
            levels.OnLevelChosen = OpenLevel;
            settings.OnClose = () => Router.Pop();
            settings.OnChanged = s => Router.ReducedMotion = s.ReducedMotion;

            Router.Replace<HomeScreen>();
            if (AppServices.PendingMenu == AppServices.MenuLevelSelect) Router.Push<LevelSelectScreen>();
            AppServices.PendingMenu = null;
        }

        public void OpenLevel(int id)
        {
            if (!Progression.IsUnlocked(AppServices.Levels, AppServices.Save, id)) return;
            AudioService.Instance?.Play(Sfx.Tap);
            AppServices.PendingLevelId = id;
            SceneManager.LoadScene(GameFlowController.GameSceneName);
        }
    }
}
