using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Owns the in-game screen stack (pause, settings, results, failure, onboarding) and reacts to session events:
    /// records wins (best actions, stars, Ghost), plays sounds/haptics, locks input while a modal is open, and
    /// navigates between stages/scenes. Created automatically for every Game scene through <see cref="GameSceneHooks"/>.
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            GameSceneHooks.OnComposed += composer =>
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
            var alarm = Router.Register(set.Create<AlarmPopupScreen>(canvas.transform, set.AlarmPopupScreen));

            pause.OnResume = () => Router.Pop();
            pause.OnRestart = () => { Router.PopAllModals(); Session.Retry(); };
            pause.OnSettings = () => Router.Push<SettingsScreen>();
            pause.OnLevelSelect = GoToLevelSelect;
            settings.OnClose = () => Router.Pop();
            settings.OnChanged = s =>
            {
                Router.ReducedMotion = s.ReducedMotion;
                if (Composer.Animator != null) Composer.Animator.ReducedMotion = s.ReducedMotion;
            };
            results.OnNext = NextStage;
            results.OnReplay = () => { Router.PopAllModals(); Session.Retry(); };
            results.OnLevelSelect = GoToLevelSelect;
            failure.OnUndo = () => { Router.PopAllModals(); Session.DebugUndo(); };
            failure.OnRetry = () => { Router.PopAllModals(); Session.Retry(); };
            failure.OnLevelSelect = GoToLevelSelect;
            onboarding.OnDismiss = () => Router.Pop();
            alarm.OnDismiss = () => { if (Router.Current == alarm) Router.Pop(); };

            Router.ModalStateChanged += open => { if (Session != null) Session.InputLocked = open; };

            Session.StageLoaded += OnStageLoaded;
            Session.ActionStarted += OnActionStarted;
            Session.ActionCompleted += OnActionCompleted;
            Session.ActionRejected += (a, r) => AudioService.Instance?.Play(Sfx.Error);
            Session.Won += OnWon;
            Session.Caught += OnCaught;
            Session.LockedDown += OnLockedDown;
            Session.AlarmTriggered += OnAlarmTriggered;
            Session.UndoPerformed += () => AudioService.Instance?.Play(Sfx.Tap);
            Session.Retried += () => AudioService.Instance?.Play(Sfx.Tap);

            _hud = Composer.Hud as GameHudController;
            if (_hud != null) _hud.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            AudioService.Ensure();
        }

        private void OnStageLoaded(Stage stage)
        {
            Router.PopAllModals();
            var save = AppServices.Save;
            string key = "stage:" + stage.Id;
            if (save != null && !save.HasSeenTutorial(key) && !string.IsNullOrEmpty(stage.Intro))
            {
                save.MarkTutorialSeen(key);
                AppServices.PersistSave();
                Router.Push<OnboardingScreen>().Present(stage.Intro);
            }
        }

        private void OnActionStarted(ActionOutcome o)
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            if (o.Free) audio.Play(Sfx.Gadget);
            else if (o.PlayerFrom != o.PlayerTo) audio.Play(Sfx.Footstep);
            else audio.Play(Sfx.Tick);
            if (o.KeyCollected || o.DoorOpened >= 0) audio.Play(Sfx.Unlock);
        }

        private void OnActionCompleted(ActionOutcome o)
        {
            if (o.Free || o.Terminal) return;
            bool guardTurned = false;
            for (int i = 0; i < o.GuardsBefore.Length; i++) if (o.GuardsBefore[i].Facing != o.GuardsAfter[i].Facing) guardTurned = true;
            if (guardTurned) AudioService.Instance?.Play(Sfx.Rotate);
        }

        private void OnWon(ActionOutcome o)
        {
            AudioService.Instance?.Play(Sfx.Clear);
            HapticsService.Pulse();
            var catalog = AppServices.Stages; var save = AppServices.Save;
            var stage = Session.Stage;
            int actions = o.After.T;
            bool ghostEarned = stage.GhostOffered && !o.After.AlarmEver;
            bool newBest = true; int best = actions; bool ghostEver = ghostEarned;
            if (catalog != null && save != null)
            {
                newBest = StageProgression.RecordWin(catalog, save, stage, actions, ghostEarned);
                var r = save.GetResult(stage.Id);
                best = r.BestTurns; ghostEver = r.Ghost;
                AppServices.PersistSave();
            }
            var next = catalog?.NextAfter(stage.Id);
            Router.Push<ResultsScreen>().Present(stage, actions, best, newBest, ghostEarned, ghostEver, next != null);
        }

        /// <summary>First camera detection of the attempt: sound, haptic and the countdown popup (unless the same action already ended the stage).</summary>
        private void OnAlarmTriggered(ActionOutcome o)
        {
            AudioService.Instance?.Play(Sfx.Alert);
            HapticsService.Pulse();
            if (o.Terminal) return; // results/failure screens take over
            Router.Push<AlarmPopupScreen>().Present(o.After.Alarm);
        }

        private void OnCaught(ActionOutcome o)
        {
            AudioService.Instance?.Play(Sfx.Alert);
            HapticsService.Pulse();
            Router.Push<FailureScreen>().Present(o, Session.DebugUndoEnabled);
        }

        private void OnLockedDown(ActionOutcome o)
        {
            AudioService.Instance?.Play(Sfx.Alert);
            HapticsService.Pulse();
            Router.Push<FailureScreen>().Present(o, Session.DebugUndoEnabled);
        }

        private void NextStage()
        {
            var next = AppServices.Stages?.NextAfter(Session.Stage.Id);
            if (next == null) { GoToLevelSelect(); return; }
            Router.PopAllModals();
            Composer.OpenStage(next);
        }

        private void GoToLevelSelect()
        {
            AppServices.PendingMenu = AppServices.MenuLevelSelect;
            SceneManager.LoadScene(HomeSceneName);
        }
    }
}
