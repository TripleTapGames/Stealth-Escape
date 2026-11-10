using System;
using ShadowSteps.Core;
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
                flow.Prefabs = composer.UiPrefabs as UiPrefabSet ?? UiPrefabSet.FromResources(); // D-056: the baked set in Resources unless the scene assigns one
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
            _guardIntro = Router.Register(set.Create<GuardIntroScreen>(canvas.transform, set.GuardIntroScreen));
            _spotlight = set.Create<FtueSpotlightScreen>(canvas.transform, null); // D-077: built from code, managed directly (never on the router stack)
            _spotlight.Hide(true);

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
            Session.ModeChanged += OnModeChangedForGuide;
            Session.ActionCompleted += OnActionCompleted;
            Session.ActionRejected += (a, r) => AudioService.Instance?.Play(Sfx.Error);
            Session.Won += OnWon;
            Session.Caught += OnCaught;
            Session.LockedDown += OnLockedDown;
            Session.AlarmTriggered += OnAlarmTriggered;
            Session.UndoPerformed += () => AudioService.Instance?.Play(Sfx.Tap);
            Session.Retried += () => AudioService.Instance?.Play(Sfx.Tap);
            Session.RulesModeChanged += _ => { CancelGuardQueue(); CancelGadgetGuide(); _messageShownThisVisit = false; Router.PopAllModals(); }; // a mode switch restarts the current level: any results/failure/alarm/guard card is stale; the new mode may show its one card
            Session.Retried += RebuildGuardQueue; // retry and mode switch: a fresh board context; acknowledged cards never repeat, interrupted ones become eligible again
            Session.Retried += () => { CancelGadgetGuide(); StartGadgetGuideIfNeeded(); }; // a retry restarts the guide from step 1, or drops it once the gadget has been used

            _hud = Composer.Hud as GameHudController;
            if (_hud != null) _hud.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            else if (Composer.Hud is IGameHudPause pausable) pausable.PauseRequested += () => { if (!Router.AnyModalOpen) Router.Push<PauseScreen>(); };
            AudioService.Ensure();
        }

        private void OnStageLoaded(Stage stage)
        {
            CancelGuardQueue();
            CancelGadgetGuide();
            StartGadgetGuideIfNeeded(); // D-076: the HUD line sits under whatever card or intro opens next
            _messageShownThisVisit = false;
            Router.PopAllModals();
            var save = AppServices.Save;
            string key = "stage:" + stage.Id;
            BuildGuardQueue();
            bool introPending = save != null && !save.HasSeenTutorial(key) && !string.IsNullOrEmpty(stage.Intro);
            // One message per visit, first-encounter card first: a level that introduces a new guard behaviour (D-041) or hands out a new gadget (D-075) shows that card;
            // its own intro is dropped when it only explains that guard (FTUE-3/4/5) or that gadget (LEVEL-21 / LEVEL-23) and otherwise deferred to the next visit. Every other level shows its intro once.
            if (_guardQueue.Count > 0)
            {
                if (introPending && IntroSupersededBy(_guardQueue[0], stage)) { save.MarkTutorialSeen(key); AppServices.PersistSave(); }
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
        private readonly System.Collections.Generic.List<OnboardingCard> _guardQueue = new System.Collections.Generic.List<OnboardingCard>();
        /// <summary>Board-session token: bumped whenever the board context changes so a stale "Got it" cannot act on the new board or mode.</summary>
        public int BoardSession { get; private set; }
        public int PendingGuardCards => _guardQueue.Count;
        public GuardOnboarding.Card ActiveGuardCard => _guardIntro != null && _guardIntro.IsVisible ? _guardIntro.Card : null;
        /// <summary>The gadget card on screen (D-075), or null.</summary>
        public GadgetOnboarding.Card ActiveGadgetCard => _guardIntro != null && _guardIntro.IsVisible ? _guardIntro.Active as GadgetOnboarding.Card : null;

        /// <summary>
        /// Queue order (one card per level visit): on a gadget's own tutorial level (LEVEL-21 Smoke, LEVEL-23 EMP) that gadget's card
        /// comes first, since the level exists to teach it; otherwise guard cards first (hazards), then gadget cards (tools).
        /// A fresh save that jumps straight to LEVEL-21 therefore still gets the Smoke card, not the stationary-guard card.
        /// </summary>
        private void BuildGuardQueue()
        {
            _guardQueue.Clear();
            if (!Session.HasStage || Session.State == null) return;
            var gadgets = GadgetOnboarding.BuildQueue(Session.Stage, AppServices.Save);
            foreach (var g in gadgets) if (GadgetOnboarding.IntroSupersededByGadgetCard(Session.Stage, g.Kind)) _guardQueue.Add(g);
            _guardQueue.AddRange(GuardOnboarding.BuildQueue(Session.Stage, Session.State, AppServices.Save));
            foreach (var g in gadgets) if (!_guardQueue.Contains(g)) _guardQueue.Add(g);
        }

        private static bool IntroSupersededBy(OnboardingCard card, Stage stage) =>
            card is GadgetOnboarding.Card g ? GadgetOnboarding.IntroSupersededByGadgetCard(stage, g.Kind) : GuardOnboarding.IntroSupersededByGuardCard(stage);

        private void CancelGuardQueue()
        {
            BoardSession++;
            _guardQueue.Clear();
            HighlightGuard(-1);
            HighlightGadget(null);
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
            int token = BoardSession; var mode = Session.RulesMode;
            _guardIntro.OnDismiss = () => DismissGuardCard(token, card, mode);
            _guardIntro.Present(card);
            Router.Push<GuardIntroScreen>();
            if (card is GuardOnboarding.Card guard) HighlightGuard(guard.GuardIndex);
            else if (card is GadgetOnboarding.Card gadget) HighlightGadget(gadget.Kind);
        }

        private void DismissGuardCard(int token, OnboardingCard card, StageMode mode)
        {
            // Stale callback (board/mode changed under the card): acknowledge nothing, close nothing, unlock nothing.
            if (token != BoardSession || mode != Session.RulesMode || !Session.HasStage || _guardIntro.Active != card) return;
            if (Router.Current == _guardIntro) Router.Pop();
            HighlightGuard(-1);
            HighlightGadget(null);
            if (card is GuardOnboarding.Card guard) GuardOnboarding.MarkSeen(AppServices.Save, guard.Kind, guard.Mode); // seen on "Got it", never on display
            else if (card is GadgetOnboarding.Card gadget) GadgetOnboarding.MarkSeen(AppServices.Save, gadget.Kind);
            AppServices.PersistSave();
            AudioService.Instance?.Play(Sfx.Tap);
            ShowNextGuardCard();
            if (_gadgetGuide.Active) ApplyGadgetGuide(); else StartGadgetGuideIfNeeded(); // after the card: "Tap SMOKE to arm it" with the button accented again
        }

        // ---------------------------------------------------------------- scripted spotlight guide (D-076 / D-077)

        private readonly GadgetOnboarding.Guide _gadgetGuide = new GadgetOnboarding.Guide();
        private FtueSpotlightScreen _spotlight;
        public FtueSpotlightScreen Spotlight => _spotlight;
        /// <summary>The step being taught, or null.</summary>
        public GadgetOnboarding.ScriptStep GadgetGuideCurrent => _gadgetGuide.Current;
        public bool GadgetGuideActive => _gadgetGuide.Active;
        public GadgetKind? GadgetGuideKind => _gadgetGuide.Active ? _gadgetGuide.Kind : (GadgetKind?)null;

        /// <summary>Starts the guide for the first offered gadget the player has never deployed (smoke before EMP); no-op when one is already running or nothing is left to teach.</summary>
        private void StartGadgetGuideIfNeeded()
        {
            if (_gadgetGuide.Active || !Session.HasStage || Session.State == null) return;
            var kind = GadgetOnboarding.GuideFor(Session.Stage, AppServices.Save);
            if (!kind.HasValue) return;
            _gadgetGuide.Start(kind.Value, GadgetOnboarding.BuildScript(Session.Stage, Session.State, kind.Value) ?? GadgetOnboarding.FallbackScript(kind.Value));
            ApplyGadgetGuide();
        }

        /// <summary>Re-plans the script from the current state (the player did something the script did not ask for).</summary>
        private void RescriptGadgetGuide(StageState from)
        {
            var kind = _gadgetGuide.Kind;
            _gadgetGuide.Cancel();
            if (from == null || from.IsTerminal || !GadgetOnboarding.Offers(Session.Stage, kind)) { ApplyGadgetGuide(); return; }
            _gadgetGuide.Start(kind, GadgetOnboarding.BuildScript(Session.Stage, from, kind) ?? GadgetOnboarding.FallbackScript(kind));
            ApplyGadgetGuide();
        }

        private void CancelGadgetGuide()
        {
            if (!_gadgetGuide.Active && !_gadgetGuide.Done) { HideSpotlight(); return; }
            _gadgetGuide.Cancel();
            ApplyGadgetGuide();
        }

        private void OnModeChangedForGuide(InputMode mode)
        {
            GadgetKind? targeting = mode == InputMode.SmokeTarget ? GadgetKind.Smoke : mode == InputMode.EmpTarget ? GadgetKind.Emp : (GadgetKind?)null;
            _gadgetGuide.OnTargeting(targeting);
            // Re-apply on every mode change while active: the HUD's own handler (subscribed before this one) clears the pill and the
            // button accent whenever the session returns to movement, including the reset right after a stage loads.
            if (_gadgetGuide.Active) ApplyGadgetGuide();
        }

        private void OnGuideAction(ActionOutcome o)
        {
            if (!_gadgetGuide.Active) return;
            bool ours = o.Free && (_gadgetGuide.Kind == GadgetKind.Smoke ? o.Action.Kind == StageActionKind.Smoke : o.Action.Kind == StageActionKind.Emp);
            if (ours && !GadgetOnboarding.IsUsed(AppServices.Save, _gadgetGuide.Kind)) { GadgetOnboarding.MarkUsed(AppServices.Save, _gadgetGuide.Kind); AppServices.PersistSave(); } // any real throw counts as learned
            var result = _gadgetGuide.OnAction(o.Action.Kind, o.PlayerTo, o.Action.Kind == StageActionKind.Smoke ? o.Action.Target : default, o.Action.CameraIndex);
            switch (result)
            {
                case GadgetOnboarding.Guide.ActionResult.Advanced:
                case GadgetOnboarding.Guide.ActionResult.Completed: ApplyGadgetGuide(); break;
                case GadgetOnboarding.Guide.ActionResult.Diverged:
                    if (ours) { var kind = _gadgetGuide.Kind; _gadgetGuide.Cancel(); ApplyGadgetGuide(); Composer.Hud?.ShowMessage(GadgetOnboarding.ThrownMessage(kind)); StartGadgetGuideIfNeeded(); } // thrown elsewhere: lesson learned, no charge left to guide
                    else RescriptGadgetGuide(o.After);
                    break;
            }
        }

        /// <summary>Mirrors the guide onto the screen: spotlight cut-out + hand + line for the current step, pinned HUD line, button accent while arming; done line (transient) when deployed, then the next gadget's guide if any.</summary>
        private void ApplyGadgetGuide()
        {
            var hud = Composer.Hud as IGameHudGadgetHighlight;
            if (_gadgetGuide.Deployed && !GadgetOnboarding.IsUsed(AppServices.Save, _gadgetGuide.Kind)) { GadgetOnboarding.MarkUsed(AppServices.Save, _gadgetGuide.Kind); AppServices.PersistSave(); }
            var step = _gadgetGuide.Current;
            if (step != null && step.Silent)
            {
                // Dormant: waiting for the player to reach the node on their own; nothing on screen.
                HideSpotlight(); HighlightGadget(null); hud?.ClearGuide();
                return;
            }
            if (step != null)
            {
                hud?.ShowGuide(step.Message);
                HighlightGadget(_gadgetGuide.HighlightButton ? _gadgetGuide.Kind : (GadgetKind?)null);
                Func<Rect?> hole = null;
                switch (step.Kind)
                {
                    case GadgetOnboarding.ScriptStepKind.Arm: var g = step.Gadget; hole = () => ButtonHole(g); break;
                    case GadgetOnboarding.ScriptStepKind.MoveTo:
                    case GadgetOnboarding.ScriptStepKind.FollowUp:
                    case GadgetOnboarding.ScriptStepKind.DeployNode: var n = step.Node; hole = () => NodeHole(n); break;
                    case GadgetOnboarding.ScriptStepKind.DeployCamera: var c = step.CameraIndex; hole = () => CameraHole(c); break;
                }
                if (hole != null) ShowSpotlight(hole, step.Message); else HideSpotlight(); // DeployAny: the session highlights every valid target itself
                return;
            }
            HideSpotlight();
            HighlightGadget(null);
            if (_gadgetGuide.Done)
            {
                hud?.ClearGuide();
                Composer.Hud?.ShowMessage(_gadgetGuide.Message);
                _gadgetGuide.Cancel();
                StartGadgetGuideIfNeeded();
                return;
            }
            hud?.ClearGuide();
        }

        private void ShowSpotlight(Func<Rect?> hole, string message)
        {
            if (_spotlight == null) return;
            _spotlight.ReducedMotion = Router.ReducedMotion;
            _spotlight.Present(hole, message);
            if (!_spotlight.IsVisible) _spotlight.Show(Router.ReducedMotion);
            // Modal cards stay above the overlay (they are pushed later and re-ordered to the top); keep the overlay under them.
            if (Router.Current != null && Router.Current.IsModal) Router.Current.transform.SetAsLastSibling();
        }

        private void HideSpotlight() { if (_spotlight != null && _spotlight.IsVisible) _spotlight.Hide(true); }

        private Rect? ButtonHole(GadgetKind kind) => Composer.Hud is IGameHudGadgetHighlight h && h.TryGetGadgetScreenRect(kind, out var r) ? r : (Rect?)null;

        private Rect? NodeHole(GridCoord node)
        {
            var cam = Composer.BoardCamera; if (cam == null) return null;
            Vector3 world; float spacing;
            if (Composer.NodeWorldPosition != null) { world = Composer.NodeWorldPosition(node); spacing = Composer.NodeSpacingWorld != null ? Composer.NodeSpacingWorld() : 1f; }
            else if (Composer.Board != null && Composer.Board.IsBuilt) { world = Composer.Board.transform.TransformPoint(Composer.Board.Layout.WorldPosition(node)); spacing = Composer.Board.Layout.NodeSpacing; }
            else return null;
            return ScreenSquare(cam, world, spacing * 0.9f);
        }

        private Rect? CameraHole(int cameraIndex)
        {
            var cam = Composer.BoardCamera; if (cam == null || Composer.CameraWorldPosition == null) return null;
            var world = Composer.CameraWorldPosition(cameraIndex); if (!world.HasValue) return null;
            float spacing = Composer.NodeSpacingWorld != null ? Composer.NodeSpacingWorld() : 1f;
            return ScreenSquare(cam, world.Value, spacing * 1.1f);
        }

        private static Rect ScreenSquare(Camera cam, Vector3 world, float sizeWorld)
        {
            var centre = cam.WorldToScreenPoint(world);
            var edge = cam.WorldToScreenPoint(world + cam.transform.right * sizeWorld * 0.5f);
            float half = Mathf.Abs(edge.x - centre.x);
            return Rect.MinMaxRect(centre.x - half, centre.y - half, centre.x + half, centre.y + half);
        }

        private GadgetKind? _highlightedGadget;
        private void HighlightGadget(GadgetKind? kind)
        {
            var hud = Composer.Hud as IGameHudGadgetHighlight;
            if (hud == null) { _highlightedGadget = kind; return; }
            if (_highlightedGadget.HasValue && _highlightedGadget != kind) hud.HighlightGadget(_highlightedGadget.Value, false);
            if (kind.HasValue) hud.HighlightGadget(kind.Value, true);
            _highlightedGadget = kind;
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
            // D-076/D-077: every started action drives the scripted guide (before the audio early-out so it also works without an AudioService).
            OnGuideAction(o);
            var audio = AudioService.Instance;
            if (audio == null) return;
            // D-069: gadgets have their own sounds; movement sounds come from the presentation's touchdown when it times them (2D bridge), else a footstep here.
            if (o.Free) audio.Play(o.Action.Kind == StageActionKind.Smoke ? Sfx.Smoke : o.Action.Kind == StageActionKind.Emp ? Sfx.Emp : Sfx.Gadget);
            else if (o.PlayerFrom != o.PlayerTo) { if (!AudioService.MovementSoundsByPresentation) audio.Play(Sfx.Footstep); }
            else audio.Play(Sfx.Tick);
            if (o.Knockout) HapticsService.Pulse();
            if (o.KeyCollected) audio.Play(Sfx.Key);
            if (o.DoorOpened >= 0) audio.Play(Sfx.DoorOpen);
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
