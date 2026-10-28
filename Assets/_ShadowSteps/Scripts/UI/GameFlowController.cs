using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Owns the in-game screen stack (pause, settings, results, failure, onboarding) and reacts to session events:
    /// records wins, plays sounds/haptics, locks input while a modal is open, and navigates between levels/scenes.
    /// Created automatically for every Game scene through <see cref="GameSceneHooks"/>.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public const string HomeSceneName = "Home";
        public const string GameSceneName = "Game";

        public GameSceneComposer Composer;
        public ScreenRouter Router;
        public UiPrefabSet Prefabs;

        private GameSessionController Session => Composer.Session;
        private GameHudController _hud;
        private int _lastWinTurns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameSceneHooks.OnComposed = composer =>
            {
                var go = new GameObject("GameFlow");
                var flow = go.AddComponent<GameFlowController>();
                flow.Composer = composer;
                flow.Prefabs = composer.UiPrefabs as UiPrefabSet;
                flow.Build();
            };
        }

        public void Build()
        {
            var canvas = UiBuilder.CreateCanvas("ScreenCanvas", 20);
            Router = canvas.gameObject.AddComponent<ScreenRouter>();
            Router.ReducedMotion = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
            var set = Prefabs != null ? Prefabs : ScriptableObject.CreateInstance<UiPrefabSet>();

            var pause = Router.Register(set.Create<PauseScreen>(canvas.transform, set.PauseScreen));
            var settings = Router.Register(set.Create<SettingsScreen>(canvas.transform, set.SettingsScreen));
            var results = Router.Register(set.Create<ResultsScreen>(canvas.transform, set.ResultsScreen));
            var failure = Router.Register(set.Create<FailureScreen>(canvas.transform, set.FailureScreen));
            var onboarding = Router.Register(set.Create<OnboardingScreen>(canvas.transform, set.OnboardingScreen));

            pause.OnResume = () => Router.Pop();
            pause.OnRestart = () => { Router.PopAllModals(); Session.Restart(); };
            pause.OnSettings = () => Router.Push<SettingsScreen>();
            pause.OnLevelSelect = GoToLevelSelect;
            settings.OnClose = () => Router.Pop();
            settings.OnChanged = s =>
            {
                Router.ReducedMotion = s.ReducedMotion;
                if (Composer.Animator != null) Composer.Animator.ReducedMotion = s.ReducedMotion;
                if (Composer.Board != null && Composer.Board.IsBuilt) { Composer.Board.HighContrastCones = s.ColorBlindAssist; Composer.Board.SyncToState(Session.State); }
            };
            results.OnNext = NextLevel;
            results.OnReplay = () => { Router.PopAllModals(); Session.Restart(); };
            results.OnLevelSelect = GoToLevelSelect;
            failure.OnUndo = () => { Router.PopAllModals(); Session.Undo(); };
            failure.OnRetry = () => { Router.PopAllModals(); Session.Restart(); };
            failure.OnLevelSelect = GoToLevelSelect;
            onboarding.OnDismiss = () => Router.Pop();

            Router.ModalStateChanged += open => { if (Session != null) Session.InputLocked = open; };

            Session.LevelLoaded += OnLevelLoaded;
            Session.TurnStarted += OnTurnStarted;
            Session.TurnCompleted += OnTurnCompleted;
            Session.ActionRejected += (a, r) => AudioService.Instance?.Play(Sfx.Error);
            Session.LevelWon += OnLevelWon;
            Session.PlayerCaptured += OnCaptured;
            Session.UndoPerformed += r => AudioService.Instance?.Play(Sfx.Tap);
            Session.Restarted += () => AudioService.Instance?.Play(Sfx.Tap);

            _hud = Composer.Hud as GameHudController;
            if (_hud != null) _hud.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            AudioService.Ensure();
        }

        private void OnLevelLoaded(LevelDefinition level)
        {
            Router.PopAllModals();
            var save = AppServices.Save;
            string key = "level:" + level.Id;
            if (save != null && !save.HasSeenTutorial(key) && !string.IsNullOrEmpty(level.Hint))
            {
                string body = level.Hint;
                if (!save.HasSeenTutorial("controls")) { body = "Swipe or tap an adjacent node to move. Use WAIT to let guards move.\n\n" + body; save.MarkTutorialSeen("controls"); }
                save.MarkTutorialSeen(key);
                AppServices.PersistSave();
                Router.Push<OnboardingScreen>().Present(body);
            }
        }

        private void OnTurnStarted(TurnResult r)
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            if (r.SmokeDeployed || r.EmpDeployed) audio.Play(Sfx.Gadget);
            else if (r.PlayerMoved) audio.Play(Sfx.Footstep);
            else audio.Play(Sfx.Tick);
            if (r.KeyCollected) audio.Play(Sfx.Unlock);
        }

        private void OnTurnCompleted(TurnResult r)
        {
            bool guardRotated = false;
            for (int i = 0; i < r.GuardsBefore.Count; i++) if (r.GuardsBefore[i].Facing != r.GuardsAfter[i].Facing) guardRotated = true;
            if (guardRotated && !r.IsTerminal) AudioService.Instance?.Play(Sfx.Rotate);
        }

        private void OnLevelWon(TurnResult r)
        {
            AudioService.Instance?.Play(Sfx.Clear);
            HapticsService.Pulse();
            var catalog = AppServices.Levels; var save = AppServices.Save;
            int turns = r.StateAfter.TurnCount;
            int best = turns;
            bool newBest = true;
            if (catalog != null && save != null)
            {
                newBest = Progression.RecordWin(catalog, save, Session.Level.Id, turns);
                best = save.GetResult(Session.Level.Id).BestTurns;
                AppServices.PersistSave();
            }
            _lastWinTurns = turns;
            var next = catalog?.NextAfter(Session.Level.Id);
            Router.Push<ResultsScreen>().Present(turns, best, AppServices.Pars.ParFor(Session.Level.Id), newBest, next != null);
        }

        private void OnCaptured(TurnResult r)
        {
            AudioService.Instance?.Play(Sfx.Alert);
            HapticsService.Pulse();
            Router.Push<FailureScreen>().Present(r);
        }

        private void NextLevel()
        {
            var next = AppServices.Levels?.NextAfter(Session.Level.Id);
            if (next == null) { GoToLevelSelect(); return; }
            Router.PopAllModals();
            Composer.OpenLevel(next);
        }

        private void GoToLevelSelect()
        {
            AppServices.PendingMenu = AppServices.MenuLevelSelect;
            SceneManager.LoadScene(HomeSceneName);
        }
    }
}
