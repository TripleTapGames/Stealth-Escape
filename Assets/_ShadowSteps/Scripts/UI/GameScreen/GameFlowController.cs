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
            _guardIntro = Router.Register(set.Create<GuardIntroScreen>(canvas.transform, null));

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
            onboarding.OnDismiss = () => { Router.Pop(); ShowNextGuardCard(); }; // guard cards follow the stage intro
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
            Session.RulesModeChanged += _ => { CancelGuardQueue(); _messageShownThisVisit = false; Router.PopAllModals(); }; // a mode switch restarts the current level: any results/failure/alarm/guard card is stale; the new mode may show its one card
            Session.Retried += RebuildGuardQueue; // retry and mode switch: a fresh board context; acknowledged cards never repeat, interrupted ones become eligible again

            _hud = Composer.Hud as GameHudController;
            if (_hud != null) _hud.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            else if (Composer.Hud is IGameHudPause pausable) pausable.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            AudioService.Ensure();
        }

        private void OnStageLoaded(Stage stage)
        {
            CancelGuardQueue();
            _messageShownThisVisit = false;
            Router.PopAllModals();
            var save = AppServices.Save;
            string key = "stage:" + stage.Id;
            BuildGuardQueue();
            bool introPending = save != null && !save.HasSeenTutorial(key) && !string.IsNullOrEmpty(stage.Intro);
            // One message per visit, first-encounter guard card first: a level that introduces a new guard behaviour shows that card;
            // its own intro is dropped when it only explains that guard (FTUE-3/4/5) and otherwise deferred to the next visit. Every other level shows its intro once.
            if (_guardQueue.Count > 0)
            {
                if (introPending && GuardOnboarding.IntroSupersededByGuardCard(stage)) { save.MarkTutorialSeen(key); AppServices.PersistSave(); }
                ShowNextGuardCard();
                return;
            }
            if (introPending)
            {
                save.MarkTutorialSeen(key);
                AppServices.PersistSave();
                _messageShownThisVisit = true;
                Router.Push<OnboardingScreen>().Present(stage.Intro);
            }
        }

        /// <summary>One tutorial message per level visit (user decision 2026-11-04: "don't crowd it"): the stage intro or a single guard card; a mode switch resets it so the other mode can show its one card.</summary>
        private bool _messageShownThisVisit;
        public bool MessageShownThisVisit => _messageShownThisVisit;

        // ---------------------------------------------------------------- guard first-encounter cards (D-041)

        private GuardIntroScreen _guardIntro;
        private readonly System.Collections.Generic.List<GuardOnboarding.Card> _guardQueue = new System.Collections.Generic.List<GuardOnboarding.Card>();
        /// <summary>Board-session token: bumped whenever the board context changes so a stale "Got it" cannot act on the new board or mode.</summary>
        public int BoardSession { get; private set; }
        public int PendingGuardCards => _guardQueue.Count;
        public GuardOnboarding.Card ActiveGuardCard => _guardIntro != null && _guardIntro.IsVisible ? _guardIntro.Card : null;

        private void BuildGuardQueue()
        {
            _guardQueue.Clear();
            if (!Session.HasStage || Session.State == null) return;
            _guardQueue.AddRange(GuardOnboarding.BuildQueue(Session.Stage, Session.State, AppServices.Save));
        }

        private void CancelGuardQueue()
        {
            BoardSession++;
            _guardQueue.Clear();
            HighlightGuard(-1);
            if (_guardIntro != null && _guardIntro.IsVisible && Router.Current == _guardIntro) Router.Pop();
        }

        private void RebuildGuardQueue()
        {
            CancelGuardQueue();
            BuildGuardQueue();
            ShowNextGuardCard();
        }

        /// <summary>Shows the next unseen behaviour card, one at a time, only when no other modal owns the screen.</summary>
        private void ShowNextGuardCard()
        {
            if (_guardIntro == null || _guardQueue.Count == 0 || Router.AnyModalOpen || !Session.HasStage || _messageShownThisVisit) return;
            _messageShownThisVisit = true; // the remaining unseen behaviours stay eligible for a later level
            var card = _guardQueue[0]; _guardQueue.RemoveAt(0);
            int token = BoardSession; var mode = card.Mode;
            _guardIntro.OnDismiss = () => DismissGuardCard(token, card, mode);
            _guardIntro.Present(card);
            Router.Push<GuardIntroScreen>();
            HighlightGuard(card.GuardIndex);
        }

        private void DismissGuardCard(int token, GuardOnboarding.Card card, StageMode mode)
        {
            // Stale callback (board/mode changed under the card): acknowledge nothing, close nothing, unlock nothing.
            if (token != BoardSession || mode != Session.RulesMode || !Session.HasStage || _guardIntro.Card != card) return;
            if (Router.Current == _guardIntro) Router.Pop();
            HighlightGuard(-1);
            GuardOnboarding.MarkSeen(AppServices.Save, card.Kind, mode); // seen on "Got it", never on display
            AppServices.PersistSave();
            AudioService.Instance?.Play(Sfx.Tap);
            ShowNextGuardCard();
        }

        private void HighlightGuard(int index)
        {
            Composer.GuardHighlight?.Invoke(index);
            var board = Composer.Board;
            if (board == null || !board.IsBuilt) return;
            if (index >= 0 && index < board.Guards.Count) board.Guards[index].SetTint(board.ActiveTheme.NextMarker);
            else if (Session.HasStage && Session.State != null) board.SyncToState(Session.State);
        }

        private void OnActionStarted(ActionOutcome o)
        {
            var audio = AudioService.Instance;
            if (audio == null) return;
            if (o.Free) audio.Play(Sfx.Gadget);
            else if (o.PlayerFrom != o.PlayerTo) audio.Play(Sfx.Footstep);
            else audio.Play(Sfx.Tick);
            if (o.Knockout) HapticsService.Pulse();
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
            var stage = Session.Stage; var mode = o.After.Mode; // the run's own mode: results, stars and bests are recorded per mode (D-040)
            int actions = o.After.T;
            bool ghostEarned = stage.GhostOffered && !o.After.AlarmEver;
            bool newBest = true; int best = actions; bool ghostEver = ghostEarned;
            if (catalog != null && save != null)
            {
                newBest = StageProgression.RecordWin(catalog, save, stage, actions, ghostEarned, mode);
                var r = save.GetResult(stage.Id, mode);
                best = r.BestTurns; ghostEver = r.Ghost;
                AppServices.PersistSave();
            }
            var next = catalog?.NextAfter(stage.Id);
            System.Action showResults = () => Router.Push<ResultsScreen>().Present(stage, mode, actions, best, newBest, ghostEarned, ghostEver, next != null);
            if (Composer.WinTransition != null && Composer.WinTransition(o, showResults)) return; // presentation plays its completion sequence first
            showResults();
        }

        /// <summary>First camera detection of the attempt: sound, haptic and the countdown popup (unless the same action already ended the stage).</summary>
        private void OnAlarmTriggered(ActionOutcome o)
        {
            AudioService.Instance?.Play(Sfx.Alert);
            HapticsService.Pulse();
            if (o.Terminal) return; // results/failure screens take over
            Router.Push<AlarmPopupScreen>().Present(o.After.Alarm);
        }

        private void OnCaught(ActionOutcome o) => PresentFailure(o);

        private void OnLockedDown(ActionOutcome o) => PresentFailure(o);

        /// <summary>The state is already final; a presentation may finish the enemy's step/turn and its capture beat before the screen appears.</summary>
        private void PresentFailure(ActionOutcome o)
        {
            System.Action present = () =>
            {
                AudioService.Instance?.Play(Sfx.Alert);
                HapticsService.Pulse();
                Router.Push<FailureScreen>().Present(o, Session.DebugUndoEnabled);
            };
            if (Composer.CaptureTransition != null && Composer.CaptureTransition(o, present)) return;
            present();
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
